using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using ControllerSupport;
using HarmonyLib;
using UnityEngine;

internal static class Program
{
    private static string ModDirectory([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source), "..", ".."));
    }

    private static void Main()
    {
        string managed = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".local/share/Steam/steamapps/common/Roguelands/Roguelands_Data/Managed");
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string path = Path.Combine(managed, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Assembly game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        Type player = game.GetType("PlayerScript", throwOnError: true);
        Type iterator = player.GetNestedType("<TripleShot>c__Iterator30", BindingFlags.NonPublic)
            ?? throw new Exception("Verified TripleShot iterator missing.");
        MethodInfo moveNext = iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("TripleShot MoveNext missing.");
        List<CodeInstruction> original = ReadInstructions(moveNext);
        int directMouseReads = CountMouseReads(original);
        if (directMouseReads != 2)
            throw new Exception("Verified native TripleShot iterator changed: expected two delayed mouse reads, found " +
                directMouseReads + ".");

        // In PlayerScript.Attack, the first bullet is routed through the mod's
        // virtual aim. The two delayed TripleShot bullets each call this raw
        // getter after a yield, so a controller->mouse->controller transition
        // can leave them aimed at the old physical cursor instead.
        Console.WriteLine("First shot: controller world target (+14, 0)");
        Console.WriteLine("Delayed shots: " + directMouseReads +
            " direct physical mouse reads, target (-20, 0)");
        string modPath = Path.Combine(ModDirectory(), "build", "ControllerSupport.dll");
        Assembly mod = Assembly.LoadFrom(modPath);
        Type patch = mod.GetType("ControllerSupport.TripleShotAimPatch");
        if (patch == null)
            throw new Exception("TripleShot delayed bullets still use stale physical mouse aim (2 direct reads); " +
                "ControllerSupport.TripleShotAimPatch is missing.");
        MethodInfo targetMethod = patch.GetMethod("TargetMethod", BindingFlags.Public | BindingFlags.Static);
        MethodInfo transpiler = patch.GetMethod("Transpiler", BindingFlags.Public | BindingFlags.Static);
        if (targetMethod == null || transpiler == null)
            throw new Exception("TripleShot patch must expose TargetMethod and Transpiler.");
        if (!Equals(targetMethod.Invoke(null, null), moveNext))
            throw new Exception("TripleShot patch targets the wrong iterator.");
        var patched = new List<CodeInstruction>((IEnumerable<CodeInstruction>)transpiler.Invoke(null,
            new object[] { original }));
        if (patched.Count != original.Count)
            throw new Exception("TripleShot patch changed unrelated IL instruction count.");
        int replaced = 0;
        for (int i = 0; i < original.Count; i++)
        {
            if (patched[i].opcode != original[i].opcode)
                throw new Exception("TripleShot patch changed unrelated opcode at " + i);
            if (IsPhysicalMouseRead(original[i]))
            {
                if (patched[i].operand is MethodBase replacement &&
                    replacement.DeclaringType?.FullName == "ControllerSupport.ControllerAim" &&
                    replacement.Name == "AttackMousePosition") replaced++;
                else throw new Exception("TripleShot delayed bullet " + (replaced + 1) +
                    " still reads the stale physical mouse.");
            }
            else if (!Equals(original[i].operand, patched[i].operand))
                throw new Exception("TripleShot patch changed unrelated operand at " + i);
        }
        if (replaced != 2 || CountMouseReads(patched) != 0)
            throw new Exception("Both delayed bullets must use virtual controller aim.");
        AssertGuard(transpiler, original, patched, addRead: false);
        AssertGuard(transpiler, original, patched, addRead: true);
        AssertDeviceHandoff();
        Console.WriteLine("PASS: both delayed TripleShot bullets route through ControllerAim.AttackMousePosition.");
    }

    private static void AssertGuard(MethodInfo transpiler, List<CodeInstruction> original,
        List<CodeInstruction> patched, bool addRead)
    {
        var changed = new List<CodeInstruction>();
        foreach (CodeInstruction instruction in original)
            changed.Add(new CodeInstruction(instruction));
        if (addRead)
        {
            MethodBase getter = null;
            foreach (CodeInstruction instruction in original)
                if (IsPhysicalMouseRead(instruction)) { getter = (MethodBase)instruction.operand; break; }
            changed.Add(new CodeInstruction(OpCodes.Call, getter));
        }
        else
        {
            MethodBase replacement = null;
            foreach (CodeInstruction instruction in patched)
                if (instruction.operand is MethodBase candidate &&
                    candidate.DeclaringType?.FullName == "ControllerSupport.ControllerAim" &&
                    candidate.Name == "AttackMousePosition") { replacement = candidate; break; }
            if (replacement == null) throw new Exception("Virtual aim replacement missing for guard.");
            for (int i = 0; i < changed.Count; i++)
                if (IsPhysicalMouseRead(changed[i])) { changed[i].operand = replacement; break; }
        }
        List<CodeInstruction> output;
        try
        {
            output = new List<CodeInstruction>((IEnumerable<CodeInstruction>)transpiler.Invoke(null,
                new object[] { changed }));
        }
        catch (TargetInvocationException e) when (e.InnerException is System.Security.SecurityException &&
            e.InnerException.StackTrace?.Contains("UnityEngine.DebugLogHandler.Internal_Log") == true)
        {
            // The exact-count guard reports its refusal through Unity's native
            // logger. Outside Unity that ECall cannot execute. Reaching this
            // specific logger is the isolated test's observable guard signal.
            Console.WriteLine((addRead ? "3-read" : "1-read") +
                " guard reached Unity warning path (native ECall unavailable outside game).");
            return;
        }
        if (output.Count != changed.Count)
            throw new Exception("TripleShot patch changed " + (addRead ? "3-read" : "1-read") + " IL.");
        for (int i = 0; i < changed.Count; i++)
            if (output[i].opcode != changed[i].opcode || !Equals(output[i].operand, changed[i].operand))
                throw new Exception("TripleShot patch ignored exact-two-read guard (" +
                    (addRead ? "3-read" : "1-read") + ").");
    }

    private static void AssertDeviceHandoff()
    {
        JoystickInputState pad = new JoystickInputState();
        pad.Connect();
        pad.Apply(new RawJoystickEvent { Type = 2, Number = 3, Value = 32767 });
        ControllerAim.Configure(() => pad);
        Input.mousePosition = new Vector3(-20f, 0f, 0f);
        Time.frameCount = 1;
        Time.unscaledTime = 0.02f;
        AssertX(14f, ControllerAim.AttackMousePosition().x, "controller owns first shot");
        ControllerInputOwnership.Configure(() => false);
        AssertX(-20f, ControllerAim.AttackMousePosition().x, "shared false defeats same-frame delayed aim");
        Time.frameCount++;
        Time.unscaledTime = 1f;
        AssertX(-20f, ControllerAim.AttackMousePosition().x, "held stick cannot reclaim unowned delayed aim");
        ControllerInputOwnership.Configure(() => true);
        ControllerAim.Configure(() => pad);
        Time.frameCount = 1;
        Time.unscaledTime = .02f;
        AssertX(14f, ControllerAim.AttackMousePosition().x, "controller return reacquires delayed aim");
        Input.mousePosition = new Vector3(-25f, 0f, 0f);
        Time.frameCount = 2;
        Time.unscaledTime = 0.04f;
        AssertX(-25f, ControllerAim.AttackMousePosition().x, "mouse takes over");
        Time.frameCount = 3;
        Time.unscaledTime = 0.5f;
        AssertX(14f, ControllerAim.AttackMousePosition().x, "controller retakes delayed shots");
    }

    private static void AssertX(float expected, float actual, string stage)
    {
        if (Math.Abs(actual - expected) > 0.01f)
            throw new Exception(stage + ": expected x=" + expected + ", got " + actual);
    }

    private static bool IsPhysicalMouseRead(CodeInstruction instruction) =>
        (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
        instruction.operand is MethodBase callee &&
        callee.DeclaringType?.FullName == "UnityEngine.Input" &&
        callee.Name == "get_mousePosition";

    private static int CountMouseReads(IEnumerable<CodeInstruction> instructions)
    {
        int count = 0;
        foreach (CodeInstruction instruction in instructions)
            if (IsPhysicalMouseRead(instruction)) count++;
        return count;
    }

    private static List<CodeInstruction> ReadInstructions(MethodInfo method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray()
            ?? throw new Exception("TripleShot has no method body.");
        var opcodes = new Dictionary<short, OpCode>();
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetValue(null) is OpCode opcode) opcodes[opcode.Value] = opcode;
        var instructions = new List<CodeInstruction>();
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
                    operand = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset),
                        method.DeclaringType.GetGenericArguments(), method.GetGenericArguments());
                    offset += 4; break;
                case OperandType.InlineI8:
                case OperandType.InlineR: offset += 8; break;
                case OperandType.InlineSwitch:
                    int count = BitConverter.ToInt32(il, offset);
                    offset += 4 + count * 4; break;
                default: throw new Exception("Unsupported IL operand in " + method.Name);
            }
            instructions.Add(new CodeInstruction(opcode, operand));
        }
        return instructions;
    }
}
