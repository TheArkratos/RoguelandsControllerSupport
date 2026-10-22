using System;
using ControllerSupport;

internal static class Program
{
    private static int checks, failures;

    private static void Expect(string label, bool condition)
    {
        checks++;
        if (condition) Console.WriteLine("PASS " + label);
        else { failures++; Console.WriteLine("FAIL " + label); }
    }

    private static void Leave(GameScript game)
    {
        // Harmony skips the native method when a prefix returns false.
        // The linked Prefix is compiled from production GameplayInputPatches.cs.
        if (StorageHeldItemLeaveNpcPatch.Prefix(game)) game.LeaveNPC();
    }

    private static int Main()
    {
        var game = new GameScript();
        game.StorageOpen = true;
        game.SetHeld(21);
        Leave(game);
        Expect("storage held item drops at chest fallback before native closure",
            game.LeaveCalls == 1 && game.DropCalls == 0 && game.ChestDropCalls == 1 && !game.HasHeld && !game.StorageOpen);

        game.StorageOpen = true;
        game.SetHeld(21);
        game.ReturnAllowed = true;
        Leave(game);
        Expect("safe return precedes native closure without a drop",
            game.LeaveCalls == 2 && game.ReturnCalls == 1 && game.ChestDropCalls == 1 && !game.HasHeld);

        game.StorageOpen = true;
        game.SetHeld(21);
        game.ReturnAllowed = game.DropAllowed = false;
        Leave(game);
        Expect("drop failure preserves item and reports rejection without native loss",
            game.LeaveCalls == 2 && game.HasHeld && game.StorageOpen && ControllerMenuSelection.Rejections == 1);

        game.SetHeld(0);
        Leave(game);
        Expect("empty hand permits native LeaveNPC", game.LeaveCalls == 3 && !game.StorageOpen);

        game.StorageOpen = false;
        game.SetHeld(21);
        Leave(game);
        Expect("other menu + held item permits native LeaveNPC/drop",
            game.LeaveCalls == 4 && game.DropCalls == 1 && !game.HasHeld);

        var prefix = typeof(StorageHeldItemLeaveNpcPatch).GetMethod("Prefix",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var priority = Attribute.GetCustomAttribute(prefix,
            typeof(HarmonyLib.HarmonyPriorityAttribute)) as HarmonyLib.HarmonyPriorityAttribute;
        Expect("prefix is first-priority before GadgetCore menu close",
            priority != null && priority.Value == HarmonyLib.Priority.First);

        Console.WriteLine(checks + " checks; " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
