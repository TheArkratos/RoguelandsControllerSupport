using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string description)
    { checks++; if (!condition) throw new Exception(description); }
    private static void Add(GameObject root, string name, string tag = "")
    {
        var obj = new GameObject(name, tag);
        obj.transform.parent = root.transform;
        root.colliders.Add(new Collider(obj));
    }
    private static void Main()
    {
        var game = new GameScript();
        game.SetType(0);
        for (int i = 0; i < 4; i++) game.Craft[i] = new Item();
        for (int i = 0; i < 4; i++) Add(game.menuCraft, i.ToString(), "craft");
        Add(game.menuCraft, "bCraft"); Add(game.menuCraft, "bRecipe");
        Add(game.menuRecipe, "bRecipe2"); Add(game.menuRecipe, "bRecUp"); Add(game.menuRecipe, "bRecDown");
        for (int i = 0; i < 12; i++) Add(game.menuRecipe, i.ToString(), "recipe");
        string reason;
        Check(CraftingMenuContext.IsOpen(game) && CraftingMenuContext.IsGear(game), "Gear Forge joins the shared crafting context");
        Check(CraftingMenuContext.ModeIdentity(game) == 0, "Gear mode identity distinguishes the shared panel");
        var targets = new Collider[152];
        CraftingMenuContext.CollectTargets(game, targets);
        Check(targets[131] != null && targets[134] != null && targets[135] != null && targets[136] != null,
            "native Gear inputs/output and controls reuse shared selection IDs");
        game.Held = new Item(101, 4) { extraData = "emblem" };
        Check(CraftingMenuContext.CanActivate(game, 131, 0, out reason), "primary delegates valid native emblem placement");
        Check(CraftingMenuContext.CanPlaceOne(game, 131, game.Held, game.Craft[0]), "empty input offers the native first secondary placement");
        int calls = game.ValidationCalls;
        Check(CraftingMenuContext.CanActivate(game, 131, 1, out reason) && game.ValidationCalls == calls + 1,
            "fresh secondary evaluates native validation once before its native place-one branch");
        // Replay only the resulting native item references; helper must remain read-only.
        game.Craft[0] = new Item(101, 1) { extraData = "emblem" };
        Check(!CraftingMenuContext.CanPlaceOne(game, 131, game.Held, game.Craft[0]),
            "native duplicate-ID rule stops held secondary repetition after the first placement");
        calls = game.ValidationCalls;
        Check(!CraftingMenuContext.CanActivate(game, 131, 1, out reason) && game.ValidationCalls == calls + 1,
            "fresh secondary on an existing same-ID input retains native duplicate rejection");
        calls = game.ValidationCalls;
        Check(CraftingMenuContext.CanActivate(game, 131, 0, out reason) && game.ValidationCalls == calls,
            "primary combine retains its native validation bypass");
        Check(!CraftingMenuContext.CanPlaceOne(game, 132, game.Held, game.Craft[1]),
            "duplicate emblem in another input also disables secondary repeat hints");
        game.Held = new Item(102, 2);
        calls = game.ValidationCalls;
        Check(!CraftingMenuContext.CanActivate(game, 132, 0, out reason) && game.ValidationCalls == calls + 1,
            "effective native tier rejection occurs only on a deliberate primary attempt");
        game.Held = new Item(11, 2);
        calls = game.ValidationCalls;
        Check(!CraftingMenuContext.CanActivate(game, 132, 0, out reason) && game.ValidationCalls == calls,
            "Alchemy organic loot is not advertised or validated as a Gear emblem");
        game.Held = new Item(10002, 1);
        for (int i = 0; i < 3; i++) game.Craft[i] = new Item();
        Check(CraftingMenuContext.CanActivate(game, 131, 0, out reason), "custom effective emblem metadata supports native loader primary placement");
        Check(!CraftingMenuContext.CanActivate(game, 131, 1, out reason), "custom emblem outside native secondary ID range does not invent place-one support");
        calls = game.ValidationCalls;
        for (int i = 0; i < 100; i++)
        {
            CraftingMenuContext.ActionLabel(game, 131);
            CraftingMenuContext.SecondaryLabel(game, 131);
            CraftingMenuContext.CanTransferInventory(game, game.Held);
            CraftingMenuContext.CollectTargets(game, targets);
        }
        Check(game.ValidationCalls == calls, "idle hints and collection never invoke native error-producing validation");
        var output = new Item(300, 1) { extraData = "rolled native tier/modifier data" };
        game.Craft[3] = output;
        Check(ReferenceEquals(output, CraftingMenuContext.ReadTarget(game, 134)), "Gear output retains its native object and metadata");
        Check(!CraftingMenuContext.CanActivate(game, 134, 0, out reason), "held emblem cannot overwrite generated Gear output");
        game.Held = new Item();
        Check(CraftingMenuContext.CanActivate(game, 134, 0, out reason) && CraftingMenuContext.CanActivate(game, 134, 1, out reason),
            "empty-hand primary and secondary can take the native whole Gear output");
        Check(!CraftingMenuContext.CanActivate(game, 135, 0, out reason), "occupied Gear output blocks stale-ready overwrite");
        Check(CraftingMenuContext.CanTransfer(game, 134), "Gear output can use native transfer to backpack");
        game.Craft[3] = new Item();
        Check(CraftingMenuContext.CanActivate(game, 135, 0, out reason), "native-ready Gear crafting uses existing progress action");
        game.menuCraft.activeInHierarchy = false;
        game.menuRecipe.activeInHierarchy = true;
        CraftingMenuContext.CollectTargets(game, targets);
        Check(targets[131] == null && targets[140] != null && targets[151] != null, "Gear recipe panel reuses all twelve native cells");
        // Native displayed-page mapping is deliberately absent from production
        // controller code. Its getter routes the original page/cell arguments.
        int[] firstRows = { 0, 2, 4, 6, 7, 8 }, secondRows = { 1, 3, 5, 9, 7, 8 };
        for (int page = 0; page < 6; page++)
        {
            game.SetPage(page);
            Check(!CraftingMenuContext.CanActivate(game, 140, 0, out reason), "locked first-half Gear recipe stays unavailable on page " + page);
            game.UnlockGear(firstRows[page], 0);
            Check(CraftingMenuContext.CanActivate(game, 140, 0, out reason), "first-half unlock follows native mapping on page " + page);
            Check(game.LastUnlockPage == page && game.LastUnlockCell == 0, "native unlock getter receives unchanged displayed page/cell");
            int storedSecond = page < 4 ? 0 : 6;
            game.UnlockGear(secondRows[page], storedSecond);
            Check(CraftingMenuContext.CanActivate(game, 146, 0, out reason), "second-half unlock follows native mapping on page " + page);
            game.LockGear(firstRows[page], 0);
            game.LockGear(secondRows[page], storedSecond);
        }
        game.SetPage(5); game.UnlockGear(8, 0);
        game.Held = new Item(800, 1);
        Check(!CraftingMenuContext.CanActivate(game, 140, 0, out reason) && CraftingMenuContext.ActionLabel(game, 140) == null,
            "Gear quick crafting requires empty hand as native gear outputs cannot stack into held gear");
        game.Held = new Item();
        Check(CraftingMenuContext.TryChangeRecipePage(game, 1, out reason) && game.CurrentPage == 0 && game.NextPageCalls == 1,
            "shared next-page helper delegates native six-page Gear wraparound");
        Check(CraftingMenuContext.TryChangeRecipePage(game, -1, out reason) && game.CurrentPage == 5 && game.PreviousPageCalls == 1,
            "shared previous-page helper delegates native Gear wraparound");
        game.Blocked = true;
        Check(!CraftingMenuContext.CanActivate(game, 137, 0, out reason), "pending drop quarantine blocks Gear controls");
        game.Blocked = false;
        ControllerInputOwnership.Configure(() => false);
        Check(!CraftingMenuContext.CanActivate(game, 137, 0, out reason), "mouse ownership cannot activate controller Gear controls");
        ControllerInputOwnership.Configure(() => true);
        game.SetType(1);
        Check(CraftingMenuContext.ModeIdentity(game) == 1 && !CraftingMenuContext.IsGear(game), "same-root Alchemy switch changes context identity");
        game.SetType(2);
        Check(CraftingMenuContext.ModeIdentity(game) == 2 && CraftingMenuContext.IsUltimate(game),
            "same-root Ultimate Forge switch retains distinct supported context identity");
        Check(!CraftingMenuContext.CanActivate(game, 140, 0, out reason),
            "Gear quick recipes cannot carry over into Ultimate read-only recipe display");
        game.SetType(3);
        Check(!CraftingMenuContext.IsOpen(game), "Creation Machine remains outside this phase");
        Console.WriteLine(checks + " Gear Forge context checks passed.");
    }
}
