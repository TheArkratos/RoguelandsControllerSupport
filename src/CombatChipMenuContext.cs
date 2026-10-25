using System;
using System.Reflection;
using UnityEngine;

namespace ControllerSupport
{
    // Combat chips use native integer IDs, not ordinary item-stack quantities.
    // This context discovers targets and describes eligibility; native clicks
    // retain all chip movement, equipment hooks, stat updates and sounds.
    internal static class CombatChipMenuContext
    {
        internal const int FirstSlot = 152, TargetCount = 38;
        private static readonly FieldInfo Menu = Field("menuCombat"), Chips = Field("combatChips");
        private static readonly FieldInfo Holding = Field("holdingItem");
        private static readonly FieldInfo Exiting = Field("exitingcombatmode"), Bar = Field("inventoryBar");
        private static readonly FieldInfo Dead = StaticField("dead"), Build = StaticField("buildMode");
        private static FieldInfo StaticField(string name)
        { return typeof(GameScript).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
        private static bool Flag(FieldInfo field) { return field != null && (bool)field.GetValue(null); }
        private static FieldInfo Field(string name)
        { return typeof(GameScript).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
        internal static bool IsOpen(GameScript game)
        {
            GameObject root = game != null && Menu != null ? Menu.GetValue(game) as GameObject : null;
            return game != null && GameScript.inventoryOpen && !GameScript.pausing && !Flag(Dead) &&
                !Flag(Build) && root && root.activeInHierarchy;
        }
        internal static GameObject Root(GameScript game)
        { return IsOpen(game) ? Menu.GetValue(game) as GameObject : null; }
        internal static bool IsTarget(int index) { return index >= FirstSlot && index < FirstSlot + TargetCount; }
        internal static bool IsHeldChip(GameScript game)
        { return InventoryItemLifecycle.IsHoldingChip(game); }
        internal static bool HasHeldItem(GameScript game)
        { return InventoryItemLifecycle.HasHeldItem(game); }
        internal static int ReadChip(GameScript game, int index)
        {
            int[] chips = game != null && Chips != null ? Chips.GetValue(game) as int[] : null;
            int native = index - FirstSlot;
            return IsTarget(index) && chips != null && native < chips.Length ? chips[native] : 0;
        }
        internal static void CollectTargets(GameScript game, Collider[] targets)
        {
            if (targets == null || targets.Length < FirstSlot + TargetCount) return;
            for (int i = FirstSlot; i < FirstSlot + TargetCount; i++) targets[i] = null;
            GameObject root = Root(game);
            if (!root) return;
            Collect(root, targets);
            GameObject bar = Bar != null ? Bar.GetValue(game) as GameObject : null;
            if (bar && bar.activeInHierarchy) Collect(bar, targets);
        }
        private static void Collect(GameObject root, Collider[] targets)
        {
            foreach (Collider candidate in root.GetComponentsInChildren<Collider>())
            {
                int number;
                if (candidate && candidate.enabled && candidate.gameObject.activeInHierarchy &&
                    candidate.gameObject.layer == 16 && candidate.gameObject.tag == "combat" &&
                    int.TryParse(candidate.gameObject.name, out number) && number >= 0 && number < TargetCount)
                    targets[FirstSlot + number] = candidate;
            }
        }
        internal static bool CanActivate(GameScript game, int index, out string reason)
        {
            reason = null;
            if (!ControllerInputOwnership.UsingController || !IsOpen(game) || !IsTarget(index))
                reason = "Combat Chips unavailable";
            else if (HasHeldItem(game) && !IsHeldChip(game))
                reason = "Place the held item before moving Combat Chips";
            else if (!HasHeldItem(game) && Exiting != null && (bool)Exiting.GetValue(game))
                reason = "Wait for Combat Mode to finish changing";
            else if (!HasHeldItem(game) && ReadChip(game, index) <= 0)
                reason = "This chip slot is empty";
            else if (IsHeldChip(game) && ReadChip(game,index) == ((Item)Holding.GetValue(game)).id)
                reason = "This chip is already in that slot";
            return reason == null;
        }
        internal static string ActionLabel(GameScript game, int index)
        {
            // Hint queries are pure: never select/place/swap or trigger effects.
            string reason;
            if (!CanActivate(game,index,out reason)) return null;
            return !IsHeldChip(game) ? "Pick up chip" : ReadChip(game,index) == 0 ? "Place chip" : "Swap chip";
        }
    }
}
