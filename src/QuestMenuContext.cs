using System;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Native Questbot owns progress, generation, costs, rewards and XP.
    internal static class QuestMenuContext
    {
        internal const int FirstSlot = 209, TargetCount = 4, Shuffle = 212;
        private static readonly FieldInfo Menu = Field("menuQuest"), Rewards = Field("inventoryQuest"), Quests = Field("quest");
        private static FieldInfo Field(string name)
        { return typeof(GameScript).GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
        internal static GameObject Root(GameScript game)
        {
            GameObject root = game == null || Menu == null ? null : Menu.GetValue(game) as GameObject;
            return game != null && GameScript.inventoryOpen && !GameScript.pausing && !GameScript.dead && !GameScript.buildMode &&
                game.inventoryMain && game.inventoryMain.activeInHierarchy && root && root.activeInHierarchy ? root : null;
        }
        internal static bool IsTarget(int index) { return index >= FirstSlot && index < FirstSlot + TargetCount; }
        internal static bool IsItemSlot(int index) { return index >= FirstSlot && index < Shuffle; }
        internal static Item ReadTarget(GameScript game, int index)
        {
            Item[] items = game == null || Rewards == null ? null : Rewards.GetValue(game) as Item[];
            int slot = index - FirstSlot;
            return IsItemSlot(index) && items != null && slot < items.Length ? items[slot] : null;
        }
        private static object Quest(int index)
        {
            Array quests = Quests == null ? null : Quests.GetValue(null) as Array;
            int slot = index - FirstSlot;
            return IsItemSlot(index) && quests != null && slot < quests.Length ? quests.GetValue(slot) : null;
        }
        private static int Value(object quest, string name)
        {
            FieldInfo field = quest == null ? null : quest.GetType().GetField(name);
            return field == null ? -1 : (int)field.GetValue(quest);
        }
        internal static void CollectTargets(GameScript game, Collider[] targets)
        {
            if (targets == null || targets.Length < FirstSlot + TargetCount) return;
            for (int i = FirstSlot; i < FirstSlot + TargetCount; i++) targets[i] = null;
            GameObject root = Root(game); if (!root) return;
            foreach (Collider target in root.GetComponentsInChildren<Collider>())
            {
                if (!target || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                int slot;
                if (target.gameObject.layer == 16 && target.gameObject.tag == "quest" &&
                    int.TryParse(target.gameObject.name, out slot) && slot >= 0 && slot < 3)
                    targets[FirstSlot + slot] = target;
                else if (target.gameObject.name == "bShuffle" && target.gameObject.GetComponent<ButtonMenu>())
                    targets[Shuffle] = target;
            }
        }
        internal static bool PhysicalMutationBlocked(GameScript game)
        {
            if (!InventoryItemLifecycle.MutationsBlocked(game)) return false;
            GameObject root = Root(game);
            if (!root || Camera.main == null || !Input.GetMouseButtonDown(0)) return false;
            RaycastHit hit;
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 7f)) return false;
            foreach (Collider target in root.GetComponentsInChildren<Collider>())
            {
                if (target != hit.collider || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                int slot;
                return target.gameObject.name == "bShuffle" ||
                    (target.gameObject.layer == 16 && target.gameObject.tag == "quest" &&
                     int.TryParse(target.gameObject.name, out slot) && slot >= 0 && slot < 3);
            }
            return false;
        }
        internal static bool CanActivate(GameScript game, int index, int action, out string reason)
        {
            reason = null;
            if (!ControllerInputOwnership.UsingController || !Root(game) || !IsTarget(index)) reason = "Questbot unavailable";
            else if (GadgetCoreAPI.IsInputFrozen()) reason = "Input is frozen";
            else if (InventoryItemLifecycle.MutationsBlocked(game)) reason = "A drop needs inspection before more actions";
            else if (action != 0) reason = "Use primary for Questbot actions";
            else if (InventoryItemLifecycle.IsHoldingChip(game)) reason = "Place the held Combat Chip first";
            else if (index == Shuffle)
            {
                if (game.GetCredits() < 1500) reason = "Shuffle requires 1500 Credits";
            }
            else
            {
                object quest = Quest(index); int type = Value(quest, "type"), required = Value(quest, "numberOf"), progress = Value(quest, "numberOf2");
                Item held = InventoryItemLifecycle.Held(game);
                bool holding = held != null && held.id > 0 && held.q > 0;
                if (type != 1 && type != 2 || required <= 0 || progress < 0) reason = "Quest unavailable";
                else if (holding)
                {
                    if (type != 1 || held.id != Value(quest, "thingID") || progress >= required)
                        reason = "Hold the requested delivery item, or place it before claiming";
                }
                else if (progress != required) reason = "Quest is not complete";
                else
                {
                    Item reward = ReadTarget(game, index);
                    if (reward == null || reward.id <= 0 || reward.q <= 0) reason = "Quest reward is unavailable";
                }
            }
            return reason == null;
        }
        internal static string ActionLabel(GameScript game, int index)
        {
            string reason; if (!CanActivate(game, index, 0, out reason)) return null;
            return index == Shuffle ? "Shuffle (1500 Credits)" : InventoryItemLifecycle.HasHeldItem(game) ? "Deliver" : "Claim reward";
        }
    }
}
