using System;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Eligibility and target discovery only. Native item methods/AddMod retain
    // mutations, registered item metadata, audio and the three aspect limits.
    internal static class GearModificationMenuContext
    {
        internal const int FirstSlot = 205, TargetCount = 4;
        internal const int ItemSlot = FirstSlot, FirstButton = FirstSlot + 1;
        private static readonly FieldInfo Menu = Field("menuGearMods"), Items = Field("modSlot"), Holding = Field("holdingItem");
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
        internal static bool IsItemSlot(int index) { return index == ItemSlot; }
        internal static bool IsButton(int index) { return index >= FirstButton && index < FirstSlot + TargetCount; }
        internal static int NativeSlot(int index) { return IsItemSlot(index) ? 0 : index - FirstButton; }
        internal static Item ReadTarget(GameScript game, int index)
        {
            Item[] items = game == null || Items == null ? null : Items.GetValue(game) as Item[];
            return IsItemSlot(index) && items != null && items.Length > 0 ? items[0] : null;
        }
        internal static void CollectTargets(GameScript game, Collider[] targets)
        {
            if (targets == null || targets.Length < FirstSlot + TargetCount) return;
            for (int i = FirstSlot; i < FirstSlot + TargetCount; i++) targets[i] = null;
            GameObject root = Root(game); if (!root) return;
            foreach (Collider target in root.GetComponentsInChildren<Collider>())
            {
                if (!target || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                if (target.gameObject.layer == 16 && target.gameObject.tag == "mod" && target.gameObject.name == "0") targets[ItemSlot] = target;
                else if (target.gameObject.GetComponent<ButtonMenu>()) for (int i = 0; i < 3; i++)
                    if (target.gameObject.name == "bAddMod" + i) targets[FirstButton + i] = target;
            }
        }
        internal static bool IsModable(Item item)
        {
            if (item == null || item.id <= 0 || item.q <= 0) return false;
            if (item.id >= 300 && item.id < 1000) return true;
            return (ItemRegistry.GetTypeByID(item.id) & ItemType.MODABLE) == ItemType.MODABLE;
        }
        internal static bool CanActivate(GameScript game, int index, int action, out string reason)
        {
            reason = null;
            if (!ControllerInputOwnership.UsingController || !IsOpen(game) || !IsTarget(index)) reason = "Gear Modification unavailable";
            else if (GadgetCoreAPI.IsInputFrozen()) reason = "Input is frozen";
            else if (InventoryItemLifecycle.MutationsBlocked(game)) reason = "A drop needs inspection before more actions";
            else if (InventoryItemLifecycle.IsHoldingChip(game)) reason = "Place the held Combat Chip first";
            else if (action < 0 || action > 1 || IsButton(index) && action != 0) reason = "Action unavailable";
            else
            {
                Item held = Holding == null ? null : Holding.GetValue(game) as Item;
                Item gear = ReadTarget(game, ItemSlot);
                bool holding = held != null && held.id > 0 && held.q > 0;
                if (IsItemSlot(index))
                {
                    if (!IsModable(holding ? held : gear)) reason = holding ? "This item cannot be modified" : "No modifiable gear";
                }
                else
                {
                    int aspect = NativeSlot(index);
                    if (!holding || held.id <= 200 || held.id >= 230) reason = "Hold a gear modification item";
                    else if (gear == null || gear.id <= 0 || gear.q <= 0) reason = "Place gear in the modification slot first";
                    else if (gear.aspect == null || gear.aspectLvl == null || gear.aspect.Length < 3 || gear.aspectLvl.Length < 3) reason = "Gear aspects unavailable";
                    else if (gear.aspectLvl[aspect] >= 5) reason = "This aspect is at maximum level";
                    else if (gear.aspect[aspect] != 0 && gear.aspect[aspect] != held.id && gear.aspectLvl[aspect] != 0) reason = "This aspect already has a different modification";
                    else if (!game.CanAddMod(aspect)) reason = "This modification is already on another aspect";
                }
            }
            return reason == null;
        }
        internal static bool NeedsLoaderDispatch(GameScript game, int index)
        {
            if (!IsItemSlot(index)) return false;
            Item held = Holding == null ? null : Holding.GetValue(game) as Item;
            Item item = held != null && held.id > 0 && held.q > 0 ? held : ReadTarget(game,index);
            return IsModable(item) && (item.id < 300 || item.id >= 1000);
        }
        internal static void DispatchLoaderItem(GameScript game)
        {
            // The loader's Update postfix reads physical mouse input, so a
            // virtual controller click cannot enter its custom-item branch.
            // Reuse the very same native mutations after shared validation.
            if (!InventoryItemLifecycle.HasHeldItem(game)) game.SelectItemMod(0);
            else if (ReadTarget(game,ItemSlot) == null || ReadTarget(game,ItemSlot).id == 0) game.PlaceItemMod(0);
            else game.SwapItemMod(0);
        }
        internal static string ActionLabel(GameScript game, int index)
        {
            string reason; if (!CanActivate(game,index,0,out reason)) return null;
            if (IsButton(index)) return "Add modification";
            return !InventoryItemLifecycle.HasHeldItem(game) ? "Take" : IsModable(ReadTarget(game,index)) ? "Swap" : "Place";
        }
        internal static string SecondaryLabel(GameScript game, int index)
        { return IsItemSlot(index) ? ActionLabel(game,index) : null; }
    }
}
