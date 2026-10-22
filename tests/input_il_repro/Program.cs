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
        Check(true, new[] {
            ("GetButton", "Left", 3), ("GetButton", "Right", 3),
            ("GetButton", "Up", 1), ("GetButton", "Down", 1),
            ("GetButtonDown", "Left", 1), ("GetButtonDown", "Right", 1),
            ("GetButtonDown", "Down", 1), ("GetButtonDown", "Up", 1),
            ("GetButtonDown", "Dash Left", 1), ("GetButtonDown", "Dash Right", 1),
            ("GetButtonDown", "Jump", 1),
            ("GetButtonUp", "Left", 1), ("GetButtonUp", "Right", 1),
            ("GetButtonUp", "Up", 1), ("GetButtonUp", "Down", 1),
            ("GetAxis", "Horizontal", 4), ("GetAxis", "Attack", 1)
        }, "Up", "PlayerInteractionButtonDown");
        Check(false, new[] {
            ("GetButtonDown", "Cancel", 1),
            ("GetButtonDown", "CombatMode", 1),
            ("GetButtonDown", "Inventory", 1),
            ("GetButtonDown", "Droid", 1),
            ("GetButtonDown", "UseItem1", 1), ("GetButtonDown", "UseItem2", 1),
            ("GetButtonDown", "UseItem3", 1), ("GetButtonDown", "UseItem4", 1),
            ("GetButtonDown", "UseItem5", 1), ("GetButtonDown", "UseItem6", 1)
        }, "Droid", "GameDroidButtonDown");
        CheckCancelAdapter();
        Console.WriteLine("Input transpilers preserve exact-count guards, instance stack order, and call labels.");
    }

    private static void CheckCancelAdapter()
    {
        List<CodeInstruction> source = new List<CodeInstruction> {
            new CodeInstruction(OpCodes.Ldstr, "Cancel"),
            new CodeInstruction(OpCodes.Call,
                typeof(Input).GetMethod("GetButtonDown", new[] { typeof(string) }))
        };
        // Include the other required reads, because the production
        // transpiler intentionally leaves incomplete native IL unchanged.
        foreach (string name in new[] { "CombatMode", "Inventory", "Droid", "UseItem1", "UseItem2", "UseItem3", "UseItem4", "UseItem5", "UseItem6" })
        {
            source.Add(new CodeInstruction(OpCodes.Ldstr, name));
            source.Add(new CodeInstruction(OpCodes.Call,
                typeof(Input).GetMethod("GetButtonDown", new[] { typeof(string) })));
        }
        List<CodeInstruction> output = new List<CodeInstruction>(GameplayInputPatches.Game(source));
        MethodInfo adapter = typeof(ControllerGameplayInput).GetMethod("GameButtonDown",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (!Equals(output[1].operand, adapter))
            throw new Exception("Native Cancel read bypasses controller adapter.");
    }

    private static void Check(bool player, (string method, string name, int count)[] reads,
        string special, string adapter)
    {
        List<CodeInstruction> source = new List<CodeInstruction>();
        foreach (var read in reads)
        {
            MethodInfo getter = typeof(Input).GetMethod(read.method, new[] { typeof(string) });
            for (int i = 0; i < read.count; i++)
            {
                source.Add(new CodeInstruction(OpCodes.Ldstr, read.name));
                source.Add(new CodeInstruction(OpCodes.Call, getter));
            }
        }
        int call = FindCall(source, special, "GetButtonDown");
        // A branch to the original call must now land on ldarg. This also
        // verifies exception-block metadata travels with the inserted op.
        source[call].labels.Add(default(Label));
        List<CodeInstruction> output = new List<CodeInstruction>(player
            ? GameplayInputPatches.Player(source) : GameplayInputPatches.Game(source));
        if (source[call].labels.Count != 1)
            throw new Exception("Production transpiler mutated source label metadata.");
        if (output.Count != source.Count + 1) throw new Exception("Expected one instance injection.");
        if (output[call].opcode != OpCodes.Ldarg_0 || output[call].labels.Count != 1 ||
            output[call + 1].opcode != OpCodes.Call ||
            ((MethodInfo)output[call + 1].operand).Name != adapter ||
            output[call + 1].labels.Count != 0)
            throw new Exception("Instance argument or label moved incorrectly.");
        if (output[call - 1].opcode != OpCodes.Ldstr ||
            (string)output[call - 1].operand != special)
            throw new Exception("String and instance stack order changed.");

        List<CodeInstruction> mismatch = new List<CodeInstruction>(source);
        mismatch.RemoveAt(call);
        List<CodeInstruction> unchanged = new List<CodeInstruction>(player
            ? GameplayInputPatches.Player(mismatch) : GameplayInputPatches.Game(mismatch));
        if (unchanged.Count != mismatch.Count)
            throw new Exception("Mismatch did not fail closed.");
        for (int i = 0; i < mismatch.Count; i++)
            if (unchanged[i].opcode != mismatch[i].opcode ||
                !Equals(unchanged[i].operand, mismatch[i].operand))
                throw new Exception("Mismatch changed vanilla IL.");
    }

    private static int FindCall(List<CodeInstruction> source, string name, string method)
    {
        for (int i = 1; i < source.Count; i++)
            if (source[i - 1].opcode == OpCodes.Ldstr &&
                Equals(source[i - 1].operand, name) &&
                ((MethodInfo)source[i].operand).Name == method) return i;
        throw new Exception("Missing " + name);
    }
}
