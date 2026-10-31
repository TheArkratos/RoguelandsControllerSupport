using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// Audit the installed native assembly, not the regenerated C# reference. The
// optional `audit` argument lists all named helpers without requiring a mod
// patch; the default mode is a red/green regression for helper attacks.
internal static class Program
{
    private static string ModDirectory([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source), "..", ".."));
    }

    private static readonly string[] IteratorNames = {
        "Repeater", "Wrath", "Cerberus", "SwordEffects", "GunEffects",
        "LanceEffects", "SchegI", "SiriusS", "Devilsbane", "LostVoice",
        "LimitBreak", "CasterSword", "AirSplit", "Savage2", "TripleShot"
    };
    private static readonly string[] DirectNames = {
        "Trigoddess", "Fractured", "MageMash", "Excalibur", "ColdsnapP",
        "ValflameProj", "Doom", "KingLance", "Ringabolt", "Atma",
        "Stormbringer", "Shock", "Messenger", "Inferno", "Blaze",
        "HyperBeam", "TurretS", "TurretF", "Turret", "Copter", "Plasma"
    };
    private static readonly Dictionary<string, int> MouseOnlyCounts = new Dictionary<string, int> {
        { "SwordEffects", 2 }, { "LanceEffects", 4 }, { "SchegI", 3 },
        { "Devilsbane", 2 }, { "LostVoice", 2 }, { "CasterSword", 2 },
        { "AirSplit", 3 }, { "Savage2", 1 },
        { "Trigoddess", 1 }, { "Fractured", 1 }, { "MageMash", 1 },
        { "Excalibur", 1 }, { "ColdsnapP", 1 }, { "ValflameProj", 1 },
        { "Doom", 1 }, { "KingLance", 1 }, { "Ringabolt", 2 },
        { "Atma", 1 }, { "Stormbringer", 1 }, { "Shock", 1 },
        { "Messenger", 2 }, { "Inferno", 1 }, { "Blaze", 1 },
        { "HyperBeam", 1 }, { "TurretS", 2 }, { "TurretF", 2 },
        { "Turret", 2 }, { "Copter", 2 }, { "Plasma", 1 }
    };
    private static readonly Dictionary<short, OpCode> Opcodes = BuildOpcodes();

    private static void Main(string[] args)
    {
        string managed = LocalReferences.Get("RoguelandsManagedDir");
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string path = Path.Combine(managed, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Assembly game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        Type player = game.GetType("PlayerScript", throwOnError: true);
        var targets = new Dictionary<string, MethodInfo>();
        foreach (string name in IteratorNames)
        {
            Type[] matches = player.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(type => type.Name.StartsWith("<" + name + ">", StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1) throw new Exception(name + ": expected one iterator type, got " + matches.Length);
            targets[name] = matches[0].GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new Exception(name + ": iterator MoveNext missing");
        }
        foreach (string name in DirectNames)
            targets[name] = player.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new Exception(name + ": method missing");
        foreach (var pair in targets)
            Console.WriteLine(pair.Key + " -> " + pair.Value.DeclaringType.FullName + "." + pair.Value.Name +
                " | direct Input.mousePosition reads: " + CountMouse(ReadInstructions(pair.Value)));
        if (args.Length == 1 && args[0] == "audit") return;

        // Repeater aims every 0.6 seconds, Wrath samples inside its repeating
        // coroutine, Cerberus captures a direction before its delayed volley,
        // and SiriusS waits 0.2 seconds before its extra shot.
        string[] required = { "Repeater", "Wrath", "Cerberus", "SiriusS" };
        foreach (string name in required) AssertNativeCount(targets[name], 1, name);
        string modPath = Path.Combine(ModDirectory(), "build", "ControllerSupport.dll");
        Assembly mod = Assembly.LoadFrom(modPath);
        Type patch = mod.GetType("ControllerSupport.HelperAttackAimPatch")
            ?? throw new Exception("RED: Repeater, Wrath, Cerberus, and SiriusS still read physical mouse; HelperAttackAimPatch is missing.");
        MethodInfo targetMethods = patch.GetMethod("TargetMethods", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("HelperAttackAimPatch.TargetMethods missing.");
        if (!targetMethods.GetCustomAttributes(inherit: false).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyTargetMethods"))
            throw new Exception("HelperAttackAimPatch.TargetMethods lacks HarmonyTargetMethods attribute.");
        MethodInfo transpiler = patch.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("HelperAttackAimPatch.Transpiler missing.");
        if (!transpiler.GetCustomAttributes(inherit: false).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyTranspiler"))
            throw new Exception("HelperAttackAimPatch.Transpiler lacks HarmonyTranspiler attribute.");
        MethodInfo prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("HelperAttackAimPatch.Prefix missing: controller cursor branch would remain stale.");
        if (!prefix.GetCustomAttributes(inherit: false).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyPrefix"))
            throw new Exception("HelperAttackAimPatch.Prefix lacks HarmonyPrefix attribute.");
        var declared = ((IEnumerable)targetMethods.Invoke(null, null)).Cast<MethodBase>().ToArray();
        MethodInfo replacementGetter = mod.GetType("ControllerSupport.ControllerAim")?.GetMethod(
            "AttackMousePosition", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("ControllerAim.AttackMousePosition missing.");
        foreach (string name in required)
        {
            MethodInfo method = targets[name];
            if (!declared.Contains(method)) throw new Exception(name + " iterator not targeted by helper patch.");
            List<CodeInstruction> original = ReadInstructions(method);
            object[] parameters = transpiler.GetParameters().Length == 2
                ? new object[] { original, method }
                : new object[] { original };
            var patched = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, parameters)).ToList();
            if (patched.Count != original.Count) throw new Exception(name + ": helper patch changed IL instruction count.");
            int replaced = 0;
            for (int i = 0; i < original.Count; i++)
            {
                if (patched[i].opcode != original[i].opcode) throw new Exception(name + ": helper patch changed opcode at " + i);
                if (IsMouse(original[i]))
                {
                    if (patched[i].operand is MethodBase replacement &&
                        replacement.DeclaringType?.FullName == "ControllerSupport.ControllerAim" &&
                        replacement.Name == "AttackMousePosition") replaced++;
                    else throw new Exception(name + ": delayed attack still reads physical mouse.");
                }
                else if (!Equals(original[i].operand, patched[i].operand))
                    throw new Exception(name + ": helper patch changed unrelated operand at " + i);
            }
            if (replaced != 1 || CountMouse(patched) != 0)
                throw new Exception(name + ": expected exactly one routed helper shot.");
            AssertGuard(transpiler, method, original, replacementGetter, addRead: false, name);
            AssertGuard(transpiler, method, original, replacementGetter, addRead: true, name);
        }
        AssertMouseOnlyHelpers(mod, targets, replacementGetter);
        Console.WriteLine("PASS: four helper attacks route through active aim, with zero/two-read guards.");
    }

    private static void AssertMouseOnlyHelpers(Assembly mod, Dictionary<string, MethodInfo> targets,
        MethodInfo replacementGetter)
    {
        Type patch = mod.GetType("ControllerSupport.MouseOnlyHelperAimPatch")
            ?? throw new Exception("RED: mouse-only combat helpers still read physical mouse; MouseOnlyHelperAimPatch is missing.");
        MethodInfo targetMethods = patch.GetMethod("TargetMethods", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("MouseOnlyHelperAimPatch.TargetMethods missing.");
        MethodInfo transpiler = patch.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("MouseOnlyHelperAimPatch.Transpiler missing.");
        if (!targetMethods.GetCustomAttributes(inherit: false).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyTargetMethods") ||
            !transpiler.GetCustomAttributes(inherit: false).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyTranspiler"))
            throw new Exception("MouseOnlyHelperAimPatch lacks Harmony target/transpiler attributes.");
        var expected = new HashSet<MethodBase>(MouseOnlyCounts.Keys.Select(name => targets[name]));
        var declared = new HashSet<MethodBase>(((IEnumerable)targetMethods.Invoke(null, null)).Cast<MethodBase>());
        if (!declared.SetEquals(expected))
            throw new Exception("MouseOnlyHelperAimPatch target set differs: expected " + expected.Count +
                ", declared " + declared.Count + ".");
        foreach (var pair in MouseOnlyCounts)
        {
            string name = pair.Key;
            MethodInfo method = targets[name];
            List<CodeInstruction> original = ReadInstructions(method);
            int count = CountMouse(original);
            if (count != pair.Value)
                throw new Exception(name + " native IL changed: expected " + pair.Value +
                    " mouse reads, found " + count + ".");
            List<CodeInstruction> patched = InvokeTranspiler(transpiler, original, method);
            if (patched.Count != original.Count)
                throw new Exception(name + ": mouse-only patch changed IL instruction count.");
            MethodInfo expectedGetter = name == "ValflameProj"
                ? replacementGetter.DeclaringType.Assembly.GetType("ControllerSupport.AimAssistValflamePatch", true)
                    .GetMethod("MousePosition", BindingFlags.Static | BindingFlags.NonPublic)
                : replacementGetter;
            int replaced = 0;
            for (int i = 0; i < original.Count; i++)
            {
                if (patched[i].opcode != original[i].opcode)
                    throw new Exception(name + ": mouse-only patch changed opcode at " + i);
                if (IsMouse(original[i]))
                {
                    if (Equals(patched[i].operand, expectedGetter)) replaced++;
                    else throw new Exception(name + ": physical mouse getter remains at " + i);
                }
                else if (!Equals(original[i].operand, patched[i].operand))
                    throw new Exception(name + ": unrelated IL operand changed at " + i);
            }
            if (replaced != count || CountMouse(patched) != 0)
                throw new Exception(name + ": expected " + count + " routed mouse reads.");
        }
        AssertMouseOnlyMismatchGuard(transpiler, targets["Plasma"], replacementGetter, addRead: false);
        AssertMouseOnlyMismatchGuard(transpiler, targets["SwordEffects"], replacementGetter, addRead: true);
        Console.WriteLine("PASS: all " + MouseOnlyCounts.Count +
            " mouse-only helpers route through active aim; mismatch guards retained vanilla IL.");
    }

    private static List<CodeInstruction> InvokeTranspiler(MethodInfo transpiler,
        List<CodeInstruction> source, MethodInfo target)
    {
        object[] parameters = transpiler.GetParameters().Length == 2
            ? new object[] { source, target }
            : new object[] { source };
        return ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, parameters)).ToList();
    }

    private static void AssertMouseOnlyMismatchGuard(MethodInfo transpiler, MethodInfo target,
        MethodInfo replacement, bool addRead)
    {
        var changed = ReadInstructions(target).Select(instruction => new CodeInstruction(instruction)).ToList();
        if (addRead)
            changed.Add(new CodeInstruction(OpCodes.Call, (MethodBase)changed.First(IsMouse).operand));
        else
            changed.First(IsMouse).operand = replacement;
        List<CodeInstruction> output;
        try
        {
            output = InvokeTranspiler(transpiler, changed, target);
        }
        catch (TargetInvocationException e) when (e.InnerException is System.Security.SecurityException &&
            e.InnerException.StackTrace?.Contains("UnityEngine.DebugLogHandler.Internal_Log") == true)
        {
            Console.WriteLine(target.DeclaringType.Name + "." + target.Name + " mismatch guard reached " +
                "Unity warning path (native ECall unavailable).");
            return;
        }
        if (output.Count != changed.Count)
            throw new Exception(target + ": mismatch guard changed IL length.");
        for (int i = 0; i < changed.Count; i++)
            if (output[i].opcode != changed[i].opcode || !Equals(output[i].operand, changed[i].operand))
                throw new Exception(target + ": mismatch guard altered IL at " + i);
    }

    private static void AssertGuard(MethodInfo transpiler, MethodInfo target, List<CodeInstruction> original,
        MethodInfo replacement, bool addRead, string name)
    {
        var changed = original.Select(instruction => new CodeInstruction(instruction)).ToList();
        if (addRead)
        {
            MethodBase getter = (MethodBase)original.Single(IsMouse).operand;
            changed.Add(new CodeInstruction(OpCodes.Call, getter));
        }
        else
        {
            CodeInstruction read = changed.Single(IsMouse);
            read.operand = replacement;
        }
        List<CodeInstruction> output;
        try
        {
            object[] parameters = transpiler.GetParameters().Length == 2
                ? new object[] { changed, target }
                : new object[] { changed };
            output = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, parameters)).ToList();
        }
        catch (TargetInvocationException e) when (e.InnerException is System.Security.SecurityException &&
            e.InnerException.StackTrace?.Contains("UnityEngine.DebugLogHandler.Internal_Log") == true)
        {
            // Exact-count rejection logs a Unity warning; its native ECall is
            // unavailable from this standalone test process.
            Console.WriteLine(name + " " + (addRead ? "two" : "zero") +
                "-read guard reached Unity warning path (native ECall unavailable).");
            return;
        }
        if (output.Count != changed.Count)
            throw new Exception(name + ": guard changed " + (addRead ? "two" : "zero") + "-read IL length.");
        for (int i = 0; i < changed.Count; i++)
            if (output[i].opcode != changed[i].opcode || !Equals(output[i].operand, changed[i].operand))
                throw new Exception(name + ": exact-one-read guard altered " +
                    (addRead ? "two" : "zero") + "-read IL at " + i);
    }

    private static void AssertNativeCount(MethodInfo method, int expected, string name)
    {
        int actual = CountMouse(ReadInstructions(method));
        if (actual != expected) throw new Exception(name + " native IL changed: expected " + expected + " mouse read, got " + actual);
    }

    private static bool IsMouse(CodeInstruction instruction) =>
        (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
        instruction.operand is MethodBase callee && callee.DeclaringType?.FullName == "UnityEngine.Input" &&
        callee.Name == "get_mousePosition";

    private static int CountMouse(IEnumerable<CodeInstruction> instructions) => instructions.Count(IsMouse);

    private static Dictionary<short, OpCode> BuildOpcodes()
    {
        var result = new Dictionary<short, OpCode>();
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetValue(null) is OpCode opcode) result[opcode.Value] = opcode;
        return result;
    }

    private static List<CodeInstruction> ReadInstructions(MethodInfo method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray() ?? throw new Exception(method + " has no IL body.");
        var instructions = new List<CodeInstruction>();
        for (int offset = 0; offset < il.Length;)
        {
            short code = il[offset++] == 0xFE ? (short)(0xFE00 | il[offset++]) : (short)il[offset - 1];
            OpCode opcode = Opcodes[code];
            object operand = null;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: offset++; break;
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
                default: throw new Exception("Unsupported IL operand in " + method);
            }
            instructions.Add(new CodeInstruction(opcode, operand));
        }
        return instructions;
    }
}
