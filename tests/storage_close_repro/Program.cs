using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ControllerSupport;
using HarmonyLib;
using UnityEngine;

internal static class Program
{
    private static int checks, failures;

    private static void Expect(string label, bool result)
    {
        checks++;
        if (!result) { failures++; Console.WriteLine("FAIL " + label); }
        else Console.WriteLine("PASS " + label);
    }

    private static void MenuCloseCases()
    {
        var game = new GameScript();
        GameScript.inventoryOpen = true;
        game.SetHeld(21);
        ControllerGameplayInput.PausePressed = true;
        GameGameplayInputPatch.Prefix(game);
        Expect("Menu with held item does not close chest or spawn world item",
            game.CloseCalls == 0 && game.PauseCalls == 0 && game.DropCalls == 0 && game.HasHeld);

        Expect("Prefix prepared lifecycle before legacy pause", ControllerMenuSelection.PrepareCalls == 1);
        game.ReturnAllowed = true;
        GameGameplayInputPatch.Prefix(game);
        Expect("Menu delegates safe return then close and pause exactly once",
            game.CloseCalls == 1 && game.PauseCalls == 1 && game.DropCalls == 0 &&
            game.ReturnCalls == 1 && !game.HasHeld);

        GameScript.inventoryOpen = false;
        ControllerGameplayInput.PausePressed = true;
        GameGameplayInputPatch.Prefix(game);
        Expect("Menu with no inventory open can pause", game.PauseCalls == 2);
    }

    // The native Cancel branch must be routed through the same guarded adapter
    // as View/Inventory. A missing read leaves B able to call CloseAllMenus.
    private static void CancelTranspilerCase()
    {
        var input = new List<CodeInstruction>();
        Add(input, "CombatMode", 1);
        Add(input, "Inventory", 1);
        Add(input, "Droid", 1);
        for (int slot = 1; slot <= 6; slot++) Add(input, "UseItem" + slot, 1);
        Add(input, "Cancel", 1);
        var output = new List<CodeInstruction>(GameplayInputPatches.Game(input));
        MethodInfo adapter = typeof(ControllerGameplayInput).GetMethod("GameButtonDown",
            BindingFlags.Static | BindingFlags.NonPublic);
        bool routed = false;
        for (int i = 1; i < output.Count; i++)
            if (output[i - 1].opcode == OpCodes.Ldstr &&
                Equals(output[i - 1].operand, "Cancel") &&
                Equals(output[i].operand, adapter)) routed = true;
        Expect("Cancel read uses guarded controller adapter",
            routed);
    }

    private static void Add(List<CodeInstruction> code, string name, int count)
    {
        for (int i = 0; i < count; i++)
        {
            code.Add(new CodeInstruction(OpCodes.Ldstr, name));
            code.Add(new CodeInstruction(OpCodes.Call,
                typeof(Input).GetMethod("GetButtonDown", new[] { typeof(string) })));
        }
    }

    private static int Main()
    {
        MenuCloseCases();
        CancelTranspilerCase();
        Console.WriteLine(checks + " checks; " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
