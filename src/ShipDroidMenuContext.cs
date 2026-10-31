using System;
using System.Reflection;
using System.Collections;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Ship droid upgrades and their output-only gather cells share inventory
    // selection. Native clicks/loader methods retain Credits, upgrades and loot.
    internal static class ShipDroidMenuContext
    {
        internal const int FirstSlot = 190, UpgradeCount = 6;
        internal const int FirstItem = FirstSlot + UpgradeCount, ItemCount = 9;
        internal const int TargetCount = UpgradeCount + ItemCount;
        private static readonly FieldInfo Menu = Field("menuGather"), Items = Field("gatherStorage"), Inventory = Field("inventory");
        private static readonly FieldInfo Levels = typeof(GameScript).GetField("shipDroid", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo Transfer = typeof(GameScript).GetMethod("ShiftClickGather", new[] { typeof(int) });
        private static readonly MethodInfo Credits = typeof(GameScript).GetMethod("GetCredits", Type.EmptyTypes);
        private static readonly MethodInfo Cost = typeof(GameScript).GetMethod("GetDroidCost", new[] { typeof(int) });
        private static FieldInfo Field(string name)
        { return typeof(GameScript).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
        internal static GameObject Root(GameScript game)
        {
            GameObject root = game == null || Menu == null ? null : Menu.GetValue(game) as GameObject;
            return game != null && GameScript.inventoryOpen && !GameScript.pausing && !GameScript.dead && !GameScript.buildMode &&
                game.inventoryMain && game.inventoryMain.activeInHierarchy && root && root.activeInHierarchy ? root : null;
        }
        internal static bool IsOpen(GameScript game) { return Root(game) != null; }
        internal static bool IsTarget(int index) { return index >= FirstSlot && index < FirstSlot + TargetCount; }
        internal static bool IsItemSlot(int index) { return index >= FirstItem && index < FirstItem + ItemCount; }
        internal static int NativeSlot(int index) { return IsItemSlot(index) ? index - FirstItem : index - FirstSlot; }
        internal static Item ReadTarget(GameScript game, int index)
        {
            Item[] items = game == null || Items == null ? null : Items.GetValue(game) as Item[];
            int slot = index - FirstItem;
            return items != null && slot >= 0 && slot < ItemCount && slot < items.Length ? items[slot] : null;
        }
        internal static void CollectTargets(GameScript game, Collider[] targets)
        {
            if (targets == null || targets.Length < FirstSlot + TargetCount) return;
            for (int i = FirstSlot; i < FirstSlot + TargetCount; i++) targets[i] = null;
            GameObject root = Root(game); if (!root) return;
            foreach (Collider target in root.GetComponentsInChildren<Collider>())
            {
                if (!target || !target.enabled || !target.gameObject.activeInHierarchy || target.gameObject.layer != 16) continue;
                int slot;
                if (!int.TryParse(target.gameObject.name, out slot)) continue;
                if (target.gameObject.tag == "sd" && slot >= 0 && slot < UpgradeCount) targets[FirstSlot + slot] = target;
                else if (target.gameObject.tag == "gather" && slot >= 0 && slot < ItemCount) targets[FirstItem + slot] = target;
            }
        }
        internal static bool CanActivate(GameScript game, int index, int action, out string reason)
        {
            reason = null;
            if (!ControllerInputOwnership.UsingController || !IsOpen(game) || !IsTarget(index)) reason = "Ship droid menu unavailable";
            else if (GadgetCoreAPI.IsInputFrozen()) reason = "Input is frozen";
            else if (InventoryItemLifecycle.MutationsBlocked(game)) reason = "A drop needs inspection before more actions";
            else if (IsItemSlot(index))
            {
                Item item = ReadTarget(game, index);
                if (InventoryItemLifecycle.HasHeldItem(game)) reason = "Place or return the held item first";
                else if (item == null || item.id == 0 || item.q <= 0) reason = "No gathered item";
                else if (action == 2 && Transfer == null) reason = "Gather transfer unavailable";
                else if (action == 2 && !TransferSpace(game, item)) reason = "Inventory is full";
                else if (action < 0 || action > 2) reason = "Action unavailable";
            }
            else
            {
                int[] levels = Levels == null ? null : Levels.GetValue(null) as int[];
                int slot = NativeSlot(index);
                if (action != 0) reason = "Use primary to upgrade ship droids";
                else if (levels == null || slot < 0 || slot >= levels.Length || Cost == null || Credits == null) reason = "Ship droid upgrade unavailable";
                else if (levels[slot] >= 5) reason = "Ship droid is at maximum level";
                else if ((int)Credits.Invoke(game, null) < (int)Cost.Invoke(game, new object[] { levels[slot] + 1 })) reason = "Not enough Credits";
            }
            return reason == null;
        }
        // The installed loader handles metadata-aware partial stacking. This
        // checks for any useful capacity, never requires whole-stack room and
        // never performs/duplicates the loader's transfer mutation algorithm.
        private static bool TransferSpace(GameScript game, Item item)
        {
            Item[] items = Inventory == null ? null : Inventory.GetValue(game) as Item[];
            if (items == null) return false;
            bool stackable = (ItemRegistry.GetTypeByID(item.id) & ItemType.NONSTACKING) == 0;
            for (int i = 0; i < 36 && i < items.Length; i++)
            {
                Item target = items[i];
                if (target == null || target.id == 0) return true;
                if (stackable && target.q < 9999 && GadgetCoreAPI.CanItemsStack(target, item)) return true;
            }
            return false;
        }
        internal static void TransferItem(GameScript game, int index)
        { game.StartCoroutine((IEnumerator)Transfer.Invoke(game, new object[] { NativeSlot(index) })); }
        internal static string ActionLabel(GameScript game, int index)
        { string reason; return CanActivate(game,index,0,out reason) ? IsItemSlot(index) ? "Take" : "Upgrade" : null; }
        internal static string SecondaryLabel(GameScript game, int index)
        { string reason; return IsItemSlot(index) && CanActivate(game,index,1,out reason) ? "Take" : null; }
        internal static bool CanTransfer(GameScript game, int index)
        { string reason; return IsItemSlot(index) && CanActivate(game,index,2,out reason); }
    }
}
