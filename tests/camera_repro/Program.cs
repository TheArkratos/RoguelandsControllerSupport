using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ControllerSupport;
using HarmonyLib;
using UnityEngine;

internal static class Program
{
    private static void Main()
    {
        JoystickInputState input = new JoystickInputState();
        input.Connect();
        ControllerAim.Configure(() => input);
        CameraScript camera = new CameraScript();
        Camera.main.gameObject = camera.gameObject;
        camera.Awake();
        camera.StartCamera(MenuScript.player);

        // Controller points right, then the physical mouse is moved left.
        input.Apply(new RawJoystickEvent { Type = 2, Number = 3, Value = 32767 });
        NextFrame(1, 0.02f);
        AssertAimX(14f, "initial controller aim");
        Input.mousePosition = new Vector3(-20f, 0f, 0f);
        NextFrame(2, 0.04f);
        if (ControllerAim.TryWorldPoint(out _))
            throw new Exception("Mouse motion failed to reclaim aim.");

        // Stick movement reclaims controller aim after the existing 350 ms grace period.
        NextFrame(3, 0.5f);
        AssertAimX(14f, "controller aim after mouse");

        // First prove the unpatched game code reproduces the reported failure.
        camera.Update();
        AssertClose(-7f, camera.GetAvgX(), "unpatched camera follows the stale mouse");

        // Exercise both production transpilers against the actual decompiled
        // method IL. The bundled 2020 Harmony cannot live-patch .NET 10's
        // MethodBody, so this checks the exact replacement at the IL seam.
        MethodInfo xSource = AssertCameraPatch(nameof(CameraScript.GetAvgX),
            CameraAvgXPatch.Transpiler);
        MethodInfo ySource = AssertCameraPatch(nameof(CameraScript.GetAvgy),
            CameraAvgYPatch.Transpiler);

        AssertClose(7f, CameraTarget(camera, xSource, true),
            "controller camera X after mouse handoff");

        // Vertical framing must use the same virtual aim point, not the mouse.
        input.Apply(new RawJoystickEvent { Type = 2, Number = 3, Value = 0 });
        input.Apply(new RawJoystickEvent { Type = 2, Number = 4, Value = -32768 });
        for (int frame = 4; frame <= 12; frame++)
        {
            NextFrame(frame, 0.5f + frame / 60f);
            if (!ControllerAim.TryWorldPoint(out _))
                throw new Exception("Controller lost aim during vertical camera test.");
        }
        AssertClose(8f, CameraTarget(camera, ySource, false), "controller camera Y");

        // Physical mouse motion must still take ownership and frame the mouse.
        Input.mousePosition = new Vector3(-20f, -10f, 0f);
        NextFrame(13, 0.8f);
        if (ControllerAim.TryWorldPoint(out _))
            throw new Exception("Mouse motion failed to reclaim aim after camera patch.");
        AssertClose(-7f, CameraTarget(camera, xSource, true), "mouse camera X fallback");
        AssertClose(-4f, CameraTarget(camera, ySource, false), "mouse camera Y fallback");
        Console.WriteLine("Both camera transpilers route one real mouse read through virtual aim; mouse fallback remains intact.");
    }

    private static MethodInfo AssertCameraPatch(string targetName,
        Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> transpiler)
    {
        MethodInfo target = typeof(CameraScript).GetMethod(targetName);
        MethodInfo mouseGetter = typeof(Input).GetProperty("mousePosition").GetGetMethod();
        MethodInfo virtualGetter = typeof(ControllerAim).GetMethod("AttackMousePosition",
            BindingFlags.Static | BindingFlags.NonPublic);
        List<CodeInstruction> original = ReadInstructions(target);
        List<CodeInstruction> patched = new List<CodeInstruction>(transpiler(original));
        if (original.Count != patched.Count)
            throw new Exception(targetName + " changed IL instruction count.");
        int mouseReads = 0, replacements = 0;
        for (int i = 0; i < original.Count; i++)
        {
            if (original[i].opcode != patched[i].opcode)
                throw new Exception(targetName + " changed an opcode.");
            if (Equals(original[i].operand, mouseGetter))
            {
                mouseReads++;
                if (!Equals(patched[i].operand, virtualGetter))
                    throw new Exception(targetName + " left the physical mouse read intact.");
                replacements++;
            }
            else if (!Equals(original[i].operand, patched[i].operand))
                throw new Exception(targetName + " changed an unrelated operand.");
        }
        if (mouseReads != 1 || replacements != 1)
            throw new Exception(targetName + " expected exactly one virtualized mouse read.");
        List<CodeInstruction> missingRead = new List<CodeInstruction>(original.Count);
        foreach (CodeInstruction instruction in original)
            missingRead.Add(new CodeInstruction(instruction));
        for (int i = 0; i < missingRead.Count; i++)
            if (Equals(missingRead[i].operand, mouseGetter))
                missingRead[i].operand = virtualGetter;
        AssertUnchanged(targetName + " missing-read guard", missingRead, transpiler);

        List<CodeInstruction> extraRead = new List<CodeInstruction>(original.Count + 1);
        foreach (CodeInstruction instruction in original)
            extraRead.Add(new CodeInstruction(instruction));
        extraRead.Add(new CodeInstruction(OpCodes.Call, mouseGetter));
        AssertUnchanged(targetName + " duplicate-read guard", extraRead, transpiler);
        Console.WriteLine(targetName + ": one real mouse getter replaced in production transpiler.");
        return virtualGetter;
    }

    private static void AssertUnchanged(string stage, List<CodeInstruction> source,
        Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> transpiler)
    {
        List<CodeInstruction> result = new List<CodeInstruction>(transpiler(source));
        if (result.Count != source.Count)
            throw new Exception(stage + " changed instruction count.");
        for (int i = 0; i < source.Count; i++)
            if (result[i].opcode != source[i].opcode ||
                !Equals(result[i].operand, source[i].operand))
                throw new Exception(stage + " changed IL despite unverified read count.");
    }

    private static float CameraTarget(CameraScript camera, MethodInfo patchedSource, bool x)
    {
        // Model only the replaced getter: the rest of the real camera method
        // still performs its original averaging and dLimit clamp.
        Vector3 physicalMouse = Input.mousePosition;
        try
        {
            Input.mousePosition = (Vector3)patchedSource.Invoke(null, null);
            return x ? camera.GetAvgX() : camera.GetAvgy();
        }
        finally { Input.mousePosition = physicalMouse; }
    }

    private static List<CodeInstruction> ReadInstructions(MethodInfo method)
    {
        if (method == null) throw new Exception("Camera method missing.");
        byte[] il = method.GetMethodBody().GetILAsByteArray();
        Dictionary<short, OpCode> opcodes = new Dictionary<short, OpCode>();
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetValue(null) is OpCode opcode) opcodes[opcode.Value] = opcode;
        List<CodeInstruction> result = new List<CodeInstruction>();
        for (int offset = 0; offset < il.Length;)
        {
            short code = il[offset++] == 0xFE
                ? (short)(0xFE00 | il[offset++]) : (short)il[offset - 1];
            OpCode opcode = opcodes[code];
            object operand = null;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: offset += 1; break;
                case OperandType.InlineVar: offset += 2; break;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.ShortInlineR:
                case OperandType.InlineField:
                case OperandType.InlineString:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                case OperandType.InlineSig: offset += 4; break;
                case OperandType.InlineMethod:
                    operand = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset));
                    offset += 4; break;
                case OperandType.InlineI8:
                case OperandType.InlineR: offset += 8; break;
                case OperandType.InlineSwitch:
                    int count = BitConverter.ToInt32(il, offset);
                    offset += 4 + count * 4; break;
                default: throw new Exception("Unsupported IL operand in " + method.Name);
            }
            result.Add(new CodeInstruction(opcode, operand));
        }
        return result;
    }

    private static void AssertClose(float expected, float actual, string stage)
    {
        Console.WriteLine(stage + ": " + actual + " (expected " + expected + ")");
        if (Math.Abs(actual - expected) > 0.01f)
            throw new Exception(stage + " was " + actual + ", expected " + expected);
    }

    private static void NextFrame(int frame, float time)
    {
        Time.frameCount = frame;
        Time.unscaledTime = time;
    }

    private static void AssertAimX(float expected, string stage)
    {
        if (!ControllerAim.TryWorldPoint(out Vector3 point) || Math.Abs(point.x - expected) > 0.01f)
            throw new Exception(stage + " was not at expected x=" + expected);
    }
}
