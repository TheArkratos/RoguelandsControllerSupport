using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string description)
    { checks++; if (!condition) throw new Exception(description); }
    private static Collider Add(GameObject root, string name, string tag = "")
    {
        var obj = new GameObject(name, tag);
        obj.transform.parent = root.transform;
        var collider = new Collider(obj);
        root.colliders.Add(collider);
        return collider;
    }
    private static void Main()
    {
        var game = new GameScript();
        var slots = new Collider[CraftingMenuContext.FirstSlot + CraftingMenuContext.TargetCount];
        Add(game.menuCraft, "0", "craft"); Add(game.menuCraft, "1", "craft");
        Add(game.menuCraft, "2", "craft"); Add(game.menuCraft, "3", "craft");
        Add(game.menuCraft, "bCraft"); Add(game.menuCraft, "bRecipe");
        Check(CraftingMenuContext.IsOpen(game), "native Alchemy panel must be recognized");
        CraftingMenuContext.CollectTargets(game, slots);
        Check(slots[131] != null && slots[134] != null && slots[135] != null && slots[136] != null,
            "all four native craft slots and both native controls must be selectable");
        string reason;
        Check(CraftingMenuContext.ReadTarget(game, 131) == game.Craft[0] && CraftingMenuContext.NativeSlot(134) == 3,
            "selection IDs expose actual native item references and output slot");
        Check(CraftingMenuContext.CanActivate(game, 135, 0, out reason), "ready native Craft button accepts fresh primary");
        Check(!CraftingMenuContext.CanActivate(game, 135, 1, out reason), "secondary cannot activate native Craft control");
        game.SetCanCraft(false);
        Check(CraftingMenuContext.ActionLabel(game, 135) == null, "unready Craft control has no active action hint");
        Check(!CraftingMenuContext.CanActivate(game, 135, 0, out reason), "native canCraft false rejects Craft");
        game.SetCanCraft(true);
        game.Held = new Item(12, 2);
        Check(CraftingMenuContext.SecondaryLabel(game, 131) == null &&
            !CraftingMenuContext.CanActivate(game, 131, 1, out reason), "held ingredient secondary does not invent place-one support");
        int validations = game.ValidationCalls;
        CraftingMenuContext.ActionLabel(game, 131);
        CraftingMenuContext.SecondaryLabel(game, 131);
        CraftingMenuContext.CanTransferInventory(game, game.Held);
        CraftingMenuContext.CollectTargets(game, slots);
        Check(game.ValidationCalls == validations, "hints and collection never invoke native placement validation");
        Check(CraftingMenuContext.CanActivate(game, 131, 0, out reason) && game.ValidationCalls == validations + 1,
            "actual ingredient placement evaluates effective native CanPlaceCraft exactly once");
        game.PlacementAllowed = false;
        Check(!CraftingMenuContext.CanActivate(game, 131, 0, out reason), "native validation rejection prevents virtual placement");
        game.PlacementAllowed = true;
        game.Held = new Item(10001, 1);
        Check(CraftingMenuContext.CanActivate(game, 131, 0, out reason), "GadgetCore custom organic loot follows effective item metadata");
        game.Held = new Item(101, 1);
        Check(CraftingMenuContext.ActionLabel(game, 131) == null, "non-organic held item does not advertise ingredient placement");
        validations = game.ValidationCalls;
        Check(!CraftingMenuContext.CanActivate(game, 131, 0, out reason) && game.ValidationCalls == validations,
            "non-organic held item rejects without native error spam");
        game.Held = new Item(11, 2);
        Check(!CraftingMenuContext.CanActivate(game, 134, 0, out reason), "ordinary input placement cannot overwrite output slot");
        game.Craft[0] = new Item(11, 9998);
        Check(!CraftingMenuContext.CanActivate(game, 131, 0, out reason), "whole held stack capacity is checked before combine");
        game.Held = new Item();
        Check(CraftingMenuContext.CanActivate(game, 131, 1, out reason), "empty-hand secondary supports native input split");
        game.Craft[3] = new Item(60, 3);
        Check(CraftingMenuContext.CanActivate(game, 134, 1, out reason), "output secondary takes the native whole output");
        Check(!CraftingMenuContext.CanActivate(game, 135, 0, out reason), "occupied output cannot be overwritten by stale canCraft flag");
        game.Craft[3] = new Item();
        Check(CraftingMenuContext.CanTransfer(game, 131), "craft ingredient can transfer to empty backpack");
        for (int n = 0; n < 36; n++) game.Inventory[n] = new Item(51, 9999);
        Check(!CraftingMenuContext.CanTransfer(game, 131), "full backpack disables craft-to-inventory transfer");
        game.Inventory[7] = new Item(11, 9998);
        Check(CraftingMenuContext.CanTransfer(game, 131), "partial compatible backpack capacity preserves native transfer behavior");
        game.menuCraft.activeInHierarchy = false;
        game.menuRecipe.activeInHierarchy = true;
        Add(game.menuRecipe, "bRecipe2"); Add(game.menuRecipe, "bRecUp"); Add(game.menuRecipe, "bRecDown");
        for (int n = 0; n < 12; n++) Add(game.menuRecipe, n.ToString(), "recipe");
        Add(game.menuRecipe, "0", "ignored-button-overlay");
        CraftingMenuContext.CollectTargets(game, slots);
        Check(slots[131] == null && slots[135] == null && slots[137] != null && slots[138] != null && slots[139] != null &&
            slots[140] != null && slots[151] != null && slots[140].gameObject.tag == "recipe",
            "recipe view invalidates craft targets and collects twelve cells plus back/page controls");
        Check(!CraftingMenuContext.CanActivate(game, 140, 0, out reason), "locked native recipe rejects activation");
        Check(CraftingMenuContext.ActionLabel(game, 140) == null, "locked recipe has no quick-craft action hint");
        game.Unlock(0, 0);
        Check(CraftingMenuContext.CanActivate(game, 140, 0, out reason), "unlocked native recipe permits original quick-craft click");
        game.Held = new Item(60, 9993);
        Check(!CraftingMenuContext.CanActivate(game, 140, 0, out reason), "quick craft reserves native maximum output to prevent held-stack truncation");
        Check(CraftingMenuContext.ActionLabel(game, 140) == null, "near-cap held stack does not advertise unsafe quick craft");
        game.Held.q = 9992;
        Check(CraftingMenuContext.CanActivate(game, 140, 0, out reason), "safe held output headroom permits native same-item guard");
        Check(!CraftingMenuContext.CanActivate(game, 131, 0, out reason), "hidden craft input cannot activate while recipe view is open");
        game.SetPage(1);
        Check(!CraftingMenuContext.CanActivate(game, 140, 0, out reason), "recipe availability reads current page");
        game.Unlock(1, 0);
        Check(CraftingMenuContext.CanActivate(game, 140, 0, out reason), "second native Alchemy recipe page is supported");
        Check(!CraftingMenuContext.CanTransferInventory(game, new Item(11, 1)), "recipe view does not shift inventory into hidden craft inputs");
        game.Blocked = true;
        Check(!CraftingMenuContext.CanActivate(game, 137, 0, out reason), "pending drop quarantine blocks native virtual clicks");
        game.Blocked = false;
        ControllerInputOwnership.Configure(() => false);
        Check(!CraftingMenuContext.CanActivate(game, 137, 0, out reason), "mouse ownership cannot activate controller Alchemy actions");
        ControllerInputOwnership.Configure(() => true);
        game.SetType(2);
        Check(CraftingMenuContext.IsUltimate(game) && CraftingMenuContext.ModeIdentity(game) == 2,
            "same-root Ultimate Forge switch has a distinct supported context");
        Check(!CraftingMenuContext.CanActivate(game, 140, 0, out reason),
            "Alchemy quick recipes cannot carry over into Ultimate read-only recipe display");
        Console.WriteLine(checks + " Alchemy context checks passed.");
    }
}
