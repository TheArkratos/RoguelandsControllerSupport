using System;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Native Gear Forge/Alchemy contexts, target IDs and eligibility. Native clicks and
    // GadgetCore retain recipes, randomness, progress and every item mutation.
    internal static class CraftingMenuContext
    {
        internal const int FirstSlot = 131;
        internal const int TargetCount = 21;
        internal const int CraftButton = 135, RecipeToggle = 136, RecipeBack = 137;
        internal const int PageUp = 138, PageDown = 139, FirstRecipe = 140;
        // All 24 native Alchemy recipe cells use Random.Range with upper bound
        // at most 8 (exclusive). Reserve the maximum without rolling RNG twice.
        private const int MaximumQuickCraftQuantity = 7;
        private static readonly FieldInfo Type = Field("craftType"), Items = Field("craft");
        private static readonly FieldInfo Inventory = Field("inventory"), Ready = Field("canCraft");
        private static readonly FieldInfo Page = Field("curRecipePage"), Unlocked = Field("recipeUnlocked1");
        private static readonly MethodInfo GearRecipeUnlocked = typeof(GameScript).GetMethod("GetRecipeUnlocked0",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo Placement = typeof(GameScript).GetMethod("CanPlaceCraft",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo ItemLevel = typeof(GameScript).GetMethod("GetItemLevel",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static FieldInfo Field(string name)
        { return typeof(GameScript).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }

        internal static bool IsOpen(GameScript game)
        {
            return game != null && Type != null && (int)Type.GetValue(game) >= 0 && (int)Type.GetValue(game) <= 2 &&
                GameScript.inventoryOpen && !GameScript.pausing && !GameScript.dead && !GameScript.buildMode &&
                game.inventoryMain && game.inventoryMain.activeInHierarchy &&
                ((game.menuCraft && game.menuCraft.activeInHierarchy) ||
                 (game.menuRecipe && game.menuRecipe.activeInHierarchy));
        }

        internal static int ModeIdentity(GameScript game)
        { return IsOpen(game) ? (int)Type.GetValue(game) : -1; }
        internal static bool IsGear(GameScript game) { return ModeIdentity(game) == 0; }
        internal static bool IsUltimate(GameScript game) { return ModeIdentity(game) == 2; }
        private static string Station(GameScript game) { return IsUltimate(game) ? "Ultimate Forge" : IsGear(game) ? "Gear Forge" : "Alchemy"; }

        internal static bool IsRecipeView(GameScript game)
        { return IsOpen(game) && game.menuRecipe && game.menuRecipe.activeInHierarchy; }

        internal static GameObject Root(GameScript game)
        { return !IsOpen(game) ? null : IsRecipeView(game) ? game.menuRecipe : game.menuCraft; }

        internal static bool IsTarget(int index)
        { return index >= FirstSlot && index < FirstSlot + TargetCount; }
        internal static bool IsItemSlot(int index)
        { return index >= FirstSlot && index < FirstSlot + 4; }
        internal static int NativeSlot(int index)
        { return IsItemSlot(index) ? index - FirstSlot : -1; }
        internal static Item ReadTarget(GameScript game, int index)
        {
            Item[] items = game != null && Items != null ? Items.GetValue(game) as Item[] : null;
            int slot = NativeSlot(index);
            return items != null && slot >= 0 && slot < items.Length ? items[slot] : null;
        }

        internal static void CollectTargets(GameScript game, Collider[] targets)
        {
            if (targets == null || targets.Length < FirstSlot + TargetCount) return;
            for (int i = FirstSlot; i < FirstSlot + TargetCount; i++) targets[i] = null;
            GameObject root = Root(game);
            if (!root) return;
            bool recipes = IsRecipeView(game);
            foreach (Collider candidate in root.GetComponentsInChildren<Collider>())
            {
                if (!candidate || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
                int index = Index(candidate, recipes);
                // Ultimate's middle ingredient is unused. Its recipe book is
                // a read-only unlock display: native recipe buttons are hidden.
                if (IsUltimate(game) && (index == FirstSlot + 1 || index >= FirstRecipe)) continue;
                if (index >= FirstSlot) targets[index] = candidate;
            }
        }

        private static int Index(Collider target, bool recipes)
        {
            string name = target.gameObject.name;
            int number;
            if (target.gameObject.layer == 16 && int.TryParse(name, out number))
            {
                if (recipes && target.gameObject.tag == "recipe" && number >= 0 && number < 12)
                    return FirstRecipe + number;
                if (!recipes && target.gameObject.tag == "craft" && number >= 0 && number < 4)
                    return FirstSlot + number;
            }
            if (recipes)
            {
                if (name == "bRecipe2") return RecipeBack;
                if (name == "bRecUp") return PageUp;
                if (name == "bRecDown") return PageDown;
            }
            else
            {
                if (name == "bCraft" || name == "bCraftAlch") return CraftButton;
                if (name == "bRecipe") return RecipeToggle;
            }
            return -1;
        }

        internal static bool CanActivate(GameScript game, int index, int button, out string reason)
        {
            reason = null;
            if (!ControllerInputOwnership.UsingController || !IsOpen(game) || !IsTarget(index))
                reason = "Crafting unavailable";
            else if (GadgetCoreAPI.IsInputFrozen()) reason = "Input is frozen";
            else if (InventoryItemLifecycle.MutationsBlocked(game)) reason = "A drop needs inspection before more item actions";
            else if (button < 0 || button > 2) reason = "Action unavailable";
            if (reason != null) return false;

            if (IsUltimate(game) && (index == FirstSlot + 1 || index >= FirstRecipe))
            { reason = "This Ultimate Forge target is display-only"; return false; }

            bool recipes = IsRecipeView(game);
            if (recipes != (index >= RecipeBack))
            { reason = "Select a target in the active crafting panel"; return false; }
            if (!TargetAvailable(game, index))
            { reason = "This crafting target is unavailable"; return false; }
            if (!IsItemSlot(index))
            {
                if (button != 0) reason = "Use primary for crafting controls";
                else if (index == CraftButton && (Ready == null || !(bool)Ready.GetValue(game) ||
                    HasItem(ReadTarget(game, FirstSlot + 3))))
                    reason = IsUltimate(game) ? "Place valid Lv.10 gear and a Prism, then take the previous output first" :
                        "Place three valid ingredients and take the previous output first";
                else if (index >= FirstRecipe)
                {
                    if (!RecipeAvailable(game, index)) reason = "This recipe is locked";
                    else
                    {
                        Item held = InventoryItemLifecycle.Held(game);
                        if (HasItem(held) && IsGear(game))
                            reason = "Place the held item before quick crafting gear";
                        else if (HasItem(held) && held.q > 9999 - MaximumQuickCraftQuantity)
                            reason = "Place the held stack before quick crafting near the stack limit";
                        // Native click checks the held output ID and rolls its
                        // quantity once. Hints/preflight never call GetRecipeItem.
                    }
                }
                return reason == null;
            }

            Item target = ReadTarget(game, index), heldItem = InventoryItemLifecycle.Held(game);
            if (IsUltimate(game)) return CanActivateUltimate(game, index, button, heldItem, target, out reason);
            if (button == 2)
            {
                if (HasItem(heldItem)) reason = "Place the held item before transferring";
                else if (!CanTransfer(game, index)) reason = "No item or backpack space available";
            }
            else if (button == 1)
            {
                if (!HasItem(heldItem))
                {
                    if (!HasItem(target)) reason = "This slot is empty";
                }
                else if (!IsGear(game)) reason = "Use primary to place Alchemy ingredients";
                else if (NativeSlot(index) == 3) reason = "Take the output before placing ingredients";
                else if (!NativeSecondaryIngredient(game, heldItem)) reason = "Use primary to place this item";
                // Native secondary always validates, including its duplicate-ID
                // rejection after one emblem has been placed. Preserve that rule.
                else if (!ValidatePlacement(game, heldItem)) reason = "These ingredients cannot be combined";
            }
            else if (!HasItem(heldItem))
            {
                if (!HasItem(target)) reason = "This slot is empty";
            }
            else if (NativeSlot(index) == 3) reason = "Take the output before placing ingredients";
            else if (!Ingredient(game, heldItem)) reason = IsGear(game) ? "Gear Forge needs Emblems" : "Alchemy needs Plants, Bugs, or Monster Parts";
            else if (HasItem(target) && target.id == heldItem.id)
            {
                if (GadgetCoreAPI.CanItemsStack(target, heldItem) && target.q > 9999 - heldItem.q)
                    reason = "The entire held stack will not fit";
            }
            else if (!ValidatePlacement(game, heldItem))
                reason = "These ingredients cannot be combined";
            return reason == null;
        }

        private static bool CanActivateUltimate(GameScript game, int index, int button,
            Item held, Item target, out string reason)
        {
            reason = null;
            int slot = NativeSlot(index);
            if (button == 2)
            {
                if (HasItem(held)) reason = "Place the held item before transferring";
                else if (!CanTransfer(game, index)) reason = "No item or backpack space available";
            }
            else if (!HasItem(held))
            {
                if (!HasItem(target)) reason = "This slot is empty";
            }
            else if (button == 1) reason = "Use primary to place Ultimate Forge ingredients";
            else if (slot == 3) reason = "Take the output before placing ingredients";
            else if (HasItem(target)) reason = "Take the previous ingredient first";
            else if (!UltimateIngredient(game, slot, held))
                reason = slot == 0 ? "Place eligible Lv.10 gear here" : "Place a Prism here";
            return reason == null;
        }

        private static bool UltimateIngredient(GameScript game, int slot, Item item)
        {
            if (!HasItem(item)) return false;
            if (slot == 2) return item.id >= 89 && item.id <= 91;
            if (slot != 0 || ItemLevel == null || !NativeUltimateGear(item.id)) return false;
            return (int)ItemLevel.Invoke(game, new object[] { item.exp }) >= 10;
        }

        // These are native Update's actual placement ranges, not the broader
        // CraftCheck ranges. GadgetCore's extra registered inputs run in a
        // physical-mouse postfix, so virtual clicks must not advertise them.
        private static bool NativeUltimateGear(int id)
        {
            return id >= 1000 && id < 1006 || id >= 450 && id < 456 ||
                id >= 500 && id < 506 || id >= 600 && id < 612 ||
                id >= 550 && id < 562 || id >= 350 && id < 362 ||
                id >= 400 && id < 412 || id >= 300 && id < 312 ||
                id >= 700 && id < 712 || id >= 800 && id < 812;
        }

        internal static bool TryChangeRecipePage(GameScript game, int direction, out string reason)
        {
            reason = null;
            if (direction == 0) return false;
            int target = direction < 0 ? PageDown : PageUp;
            if (!IsRecipeView(game) || !CanActivate(game, target, 0, out reason))
            {
                if (reason == null) reason = "Open the crafting recipe book first";
                return false;
            }
            MethodInfo change = typeof(GameScript).GetMethod(direction < 0 ? "RecipeDown" : "RecipeUp",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (change == null) { reason = "Recipe paging unavailable"; return false; }
            // Native methods own page bounds, wraparound and recipe refresh.
            change.Invoke(game, null);
            return true;
        }

        private static bool RecipeAvailable(GameScript game, int index)
        {
            int page = Page == null ? -1 : (int)Page.GetValue(game);
            int recipe = index - FirstRecipe;
            if (IsGear(game))
            {
                // Native Gear Forge maps six displayed pages to ten stored
                // unlock rows. Its pure getter owns that mapping for both hints
                // and activation; do not duplicate or roll recipe output here.
                return GearRecipeUnlocked != null && page >= 0 && page < 6 && recipe >= 0 && recipe < 12 &&
                    (int)GearRecipeUnlocked.Invoke(game, new object[] { page, recipe }) == 1;
            }
            int[,] unlocked = Unlocked == null ? null : Unlocked.GetValue(game) as int[,];
            return unlocked != null && page >= 0 && page < unlocked.GetLength(0) &&
                recipe >= 0 && recipe < unlocked.GetLength(1) && unlocked[page, recipe] == 1;
        }

        private static bool TargetAvailable(GameScript game, int index)
        {
            GameObject root = Root(game);
            if (!root) return false;
            bool recipes = IsRecipeView(game);
            foreach (Collider candidate in root.GetComponentsInChildren<Collider>())
                if (candidate && candidate.enabled && candidate.gameObject.activeInHierarchy &&
                    Index(candidate, recipes) == index) return true;
            return false;
        }

        private static bool Ingredient(GameScript game, Item item)
        {
            if (!HasItem(item)) return false;
            if (IsUltimate(game)) return UltimateIngredient(game, 0, item) || UltimateIngredient(game, 2, item);
            ItemType type = ItemRegistry.GetTypeByID(item.id);
            if (IsGear(game)) return (type & (ItemType.EMBLEM | ItemType.EQUIPABLE)) == ItemType.EMBLEM;
            return (type & (ItemType.EMBLEM | ItemType.EQUIPABLE)) == ItemType.LOOT &&
                (type & ItemType.ORGANIC) == ItemType.ORGANIC;
        }
        private static bool NativeSecondaryIngredient(GameScript game, Item item)
        { return Ingredient(game, item) && item.id > 100 && item.id < 200; }
        private static bool ValidatePlacement(GameScript game, Item held)
        { return Placement != null && (bool)Placement.Invoke(game, new object[] { held.id }); }

        internal static bool CanPlaceOne(GameScript game, int index, Item held, Item target)
        {
            if (!IsGear(game) || IsRecipeView(game) || !IsItemSlot(index) || NativeSlot(index) == 3 ||
                !NativeSecondaryIngredient(game, held) || (HasItem(target) &&
                (!GadgetCoreAPI.CanItemsStack(target, held) || target.q >= 9999))) return false;
            // This mirrors only the native duplicate-ID rejection for a pure
            // hint/repeat decision. Native validation still runs on fresh input.
            for (int i = FirstSlot; i < FirstSlot + 3; i++)
            {
                Item ingredient = ReadTarget(game, i);
                if (HasItem(ingredient) && ingredient.id == held.id) return false;
            }
            return true;
        }
        private static bool HasItem(Item item) { return item != null && item.id > 0 && item.q > 0; }

        internal static bool CanTransfer(GameScript game, int index)
        {
            if (!IsOpen(game) || IsRecipeView(game) || !IsItemSlot(index)) return false;
            Item item = ReadTarget(game, index);
            Item[] inventory = Inventory == null ? null : Inventory.GetValue(game) as Item[];
            if (!HasItem(item) || inventory == null) return false;
            for (int i = 0; i < Math.Min(36, inventory.Length); i++)
                if (!HasItem(inventory[i]) || (GadgetCoreAPI.CanItemsStack(inventory[i], item) && inventory[i].q < 9999))
                    return true;
            return false;
        }

        internal static bool CanTransferInventory(GameScript game, Item item)
        {
            if (!IsOpen(game) || IsUltimate(game) || IsRecipeView(game) || !Ingredient(game, item)) return false;
            for (int i = FirstSlot; i < FirstSlot + 3; i++)
            {
                Item target = ReadTarget(game, i);
                if (!HasItem(target) || (GadgetCoreAPI.CanItemsStack(target, item) && target.q <= 9999 - item.q))
                    return true;
            }
            return false;
        }

        // Read-only hints intentionally do not call native validation, which
        // emits Error objects, or recipe helpers, which consume random numbers.
        internal static string ActionLabel(GameScript game, int index)
        {
            if (!IsOpen(game) || !IsTarget(index) || IsRecipeView(game) != (index >= RecipeBack)) return null;
            if (IsUltimate(game) && (index == FirstSlot + 1 || index >= FirstRecipe)) return null;
            Item held = InventoryItemLifecycle.Held(game);
            if (IsItemSlot(index))
            {
                if (HasItem(held))
                {
                    if (IsUltimate(game)) return !HasItem(ReadTarget(game, index)) &&
                        UltimateIngredient(game, NativeSlot(index), held) ? "Place ingredient" : null;
                    if (NativeSlot(index) == 3 || !Ingredient(game, held)) return null;
                    Item target = ReadTarget(game, index);
                    if (HasItem(target) && GadgetCoreAPI.CanItemsStack(target, held) && target.q > 9999 - held.q)
                        return null;
                    return "Place ingredient";
                }
                return HasItem(ReadTarget(game, index)) ? NativeSlot(index) == 3 ? "Take output" : "Pick up ingredient" : null;
            }
            if (index == CraftButton) return Ready != null && (bool)Ready.GetValue(game) &&
                !HasItem(ReadTarget(game, FirstSlot + 3)) ? "Craft" : null;
            if (index == RecipeToggle) return "Open recipes";
            if (index == RecipeBack) return "Back to " + Station(game);
            if (index == PageUp) return "Next recipe page";
            if (index == PageDown) return "Previous recipe page";
            return RecipeAvailable(game, index) && (!HasItem(held) || (!IsGear(game) && held.q <= 9999 - MaximumQuickCraftQuantity))
                ? "Quick craft recipe" : null;
        }
        internal static string SecondaryLabel(GameScript game, int index)
        {
            if (!IsOpen(game) || IsRecipeView(game) || !IsItemSlot(index)) return null;
            Item held = InventoryItemLifecycle.Held(game), target = ReadTarget(game, index);
            if (IsUltimate(game)) return NativeSlot(index) == 1 || HasItem(held) || !HasItem(target) ? null :
                NativeSlot(index) == 2 ? "Split stack" : NativeSlot(index) == 3 ? "Take output" : "Pick up ingredient";
            if (HasItem(held))
            {
                if (CanPlaceOne(game, index, held, target)) return "Place one";
                if (IsGear(game) && NativeSlot(index) < 3 && NativeSecondaryIngredient(game, held) &&
                    HasItem(target) && target.id != held.id) return "Swap";
                return null;
            }
            return !HasItem(target) ? null : NativeSlot(index) == 3 ? "Take output" : "Split stack";
        }
    }
}
