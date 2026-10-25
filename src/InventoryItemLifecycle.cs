using System;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // This owns provenance only. The game's holdingItem and inventory/storage remain
    // the sole item state; native methods own eligibility, equipment and UI updates.
    internal static class InventoryItemLifecycle
    {
        private static readonly FieldInfo Holding = AccessToolsField("holdingItem");
        private static readonly FieldInfo Inventory = AccessToolsField("inventory");
        private static readonly FieldInfo Storage = AccessToolsField("storage");
        private static readonly FieldInfo Craft = AccessToolsField("craft");
        private static readonly FieldInfo CraftMenu = AccessToolsField("menuCraft");
        private static readonly FieldInfo RecipeMenu = AccessToolsField("menuRecipe");
        private static readonly FieldInfo CraftType = AccessToolsField("craftType");
        private static readonly FieldInfo Page = AccessToolsField("curStoragePage");
        private static readonly FieldInfo CombatChip = AccessToolsField("holdingCombatChip");
        private static readonly FieldInfo CombatMenu = AccessToolsField("menuCombat");
        private static readonly FieldInfo CombatChips = AccessToolsField("combatChips");
        private static readonly FieldInfo PortalMenu = AccessToolsField("menuPlanets");
        private static readonly FieldInfo EmblemForgeMenu = AccessToolsField("menuEmblemForge");
        private static readonly FieldInfo TrasherMenu = AccessToolsField("menuTrasher");
        private static readonly FieldInfo PrismMenu = AccessToolsField("menuPrism");
        private static GameScript owner;
        private static int scene = -1;
        private static int sourceSlot = -1;
        private static int sourcePage;
        private static bool sourceStorage, sourceCraft, sourceChip;
        private static GameObject sourceChipMenu;
        private static int sourceCraftType = -1, interactionCraftType = -1;
        private static bool returning;
        private static bool dropping;
        private static bool uncertainDrop;
        private static bool captureReady;
        private static bool captureUnknown;
        private static readonly System.Collections.Generic.List<GameObject> capturedDrops =
            new System.Collections.Generic.List<GameObject>();
        private static bool interactionCaptured;
        private static GameObject interactionRoot;
        private static Vector3 interactionPosition;

        private static FieldInfo AccessToolsField(string name)
        {
            return typeof(GameScript).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        internal static Item Held(GameScript game) { return game == null ? null : Holding.GetValue(game) as Item; }
        // Combat chips deliberately use q=0; only the native chip flag makes
        // that ID a held chip. Ordinary zero-quantity items remain empty.
        internal static bool IsHoldingChip(GameScript game)
        {
            Item item = Held(game);
            return game != null && CombatChip != null && (bool)CombatChip.GetValue(game) &&
                item != null && item.id > 0;
        }
        internal static bool HasHeldItem(GameScript game)
        { return IsHoldingChip(game) || !Empty(Held(game)); }
        private static GameObject ChipRoot(GameScript game)
        { return game == null || CombatMenu == null ? null : CombatMenu.GetValue(game) as GameObject; }
        private static bool Empty(Item item) { return item == null || item.id == 0 || item.q <= 0; }
        private static Item EmptyItem() { return new Item(0, 0, 0, 0, 0, new int[3], new int[3]); }
        internal static int CurrentPage(GameScript game) { return (int)Page.GetValue(game); }

        private static GameObject CraftingRoot(GameScript game)
        {
            if (game == null || CraftType == null) return null;
            int kind = (int)CraftType.GetValue(game);
            if (kind != 0 && kind != 1) return null;
            GameObject pane = CraftMenu == null ? null : CraftMenu.GetValue(game) as GameObject;
            GameObject recipes = RecipeMenu == null ? null : RecipeMenu.GetValue(game) as GameObject;
            // Keep a stable opener identity when the recipe book replaces the
            // ingredient pane; the original station may already be out of range.
            bool open = pane != null && pane.activeSelf || recipes != null && recipes.activeSelf;
            return open ? pane ?? recipes : null;
        }

        internal static void ObserveContext(GameScript game)
        {
            if (game == null) return;
            if (!ReferenceEquals(owner, game) || scene != Application.loadedLevel)
            {
                // A scene transition invalidates provenance, but cannot prove
                // that an uncertain real pickup was removed. Keep its quarantine
                // for this live GameScript until an explicit session reset.
                bool preserveQuarantine = ReferenceEquals(owner, game) && uncertainDrop;
                Reset();
                uncertainDrop = preserveQuarantine;
                owner = game;
                scene = Application.loadedLevel;
            }
            GameObject portal = PortalMenu == null ? null : PortalMenu.GetValue(game) as GameObject;
            GameObject forge = EmblemForgeMenu == null ? null : EmblemForgeMenu.GetValue(game) as GameObject;
            GameObject trasher = TrasherMenu == null ? null : TrasherMenu.GetValue(game) as GameObject;
            GameObject prism = PrismMenu == null ? null : PrismMenu.GetValue(game) as GameObject;
            GameObject crafting = CraftingRoot(game);
            GameObject chips = ChipRoot(game);
            GameObject root = !GameScript.inventoryOpen ? null
                : game.menuStorage != null && game.menuStorage.activeSelf ? game.menuStorage
                : portal != null && portal.activeSelf ? portal
                : forge != null && forge.activeSelf ? forge
                : trasher != null && trasher.activeSelf ? trasher
                : prism != null && prism.activeSelf ? prism
                : chips != null && chips.activeSelf ? chips : crafting;
            int craftingType = root != null && ReferenceEquals(root, crafting) ? (int)CraftType.GetValue(game) : -1;
            if (!ReferenceEquals(root, interactionRoot) || interactionCraftType != craftingType)
            {
                // A shared inventory pane may belong to a chest, portal console
                // or item conversion station. Capture its opener once, never the new interaction
                // target encountered while walking out of the original range.
                interactionRoot = root;
                interactionCraftType = craftingType;
                interactionCaptured = false;
                if (root != null && PlayerScript.curInteractObj != null)
                {
                    interactionPosition = PlayerScript.curInteractObj.transform.position;
                    interactionCaptured = true;
                }
            }
            if (!HasHeldItem(game)) sourceSlot = -1;
        }

        // Called around native methods, including physical mouse actions. Tracking
        // needs no storage reads and therefore remains correct inside SixBySix's
        // nested temporary projection of the sixth row during GameScript.Update.
        internal static void RecordSource(GameScript game, int slot, int page, bool storage)
        { RecordSource(game, slot, page, storage, false); }

        internal static void RecordCraftSource(GameScript game, int slot)
        { RecordSource(game, slot, 0, false, true); }

        internal static void RecordChipSource(GameScript game, int slot)
        {
            ObserveContext(game);
            if (returning || !IsHoldingChip(game)) return;
            sourceSlot = slot;
            sourceStorage = sourceCraft = false;
            sourceCraftType = -1;
            sourceChip = true;
            sourceChipMenu = ChipRoot(game);
        }

        private static void RecordSource(GameScript game, int slot, int page, bool storage, bool crafting)
        {
            ObserveContext(game);
            if (returning || !HasHeldItem(game)) return;
            sourceSlot = slot;
            sourcePage = page;
            sourceStorage = storage;
            sourceCraft = crafting;
            sourceChip = false; sourceChipMenu = null;
            sourceCraftType = crafting && CraftType != null ? (int)CraftType.GetValue(game) : -1;
        }

        internal static void ClearIfEmpty(GameScript game)
        {
            if (ReferenceEquals(owner, game) && !HasHeldItem(game)) sourceSlot = -1;
        }

        internal static bool TryReturn(GameScript game, out string reason)
        {
            ObserveContext(game);
            reason = null;
            Item held = Held(game);
            if (!HasHeldItem(game)) return true;
            if (uncertainDrop) { reason = "Drop outcome is uncertain; inspect the item before continuing."; return false; }
            if (IsHoldingChip(game)) return ReturnChip(game, out reason);
            if (sourceChip)
            { reason = "The held item's original type is unavailable."; return false; }
            if (sourceSlot < 0) { reason = "The held item's source is unavailable."; return false; }
            if (sourceCraft && (!GameScript.inventoryOpen || CraftingRoot(game) == null || Craft == null ||
                CraftType == null || sourceCraftType != (int)CraftType.GetValue(game)))
            { reason = "The original crafting station is unavailable."; return false; }
            FieldInfo source = sourceCraft ? Craft : sourceStorage ? Storage : Inventory;
            Item[] items = source == null ? null : source.GetValue(game) as Item[];
            int index = sourceStorage ? MapStorageSlot(sourcePage, sourceSlot, items == null ? 0 : items.Length) : sourceSlot;
            if (items == null || index < 0 || index >= items.Length)
            { reason = "The original slot is unavailable."; return false; }
            Item target = items[index];
            bool empty = Empty(target);
            if (!empty && (!GadgetCoreAPI.CanItemsStack(target, held) || target.q > 9999 - held.q))
            { reason = "The original slot cannot accept the entire held stack."; return false; }
            int previousPage = CurrentPage(game);
            returning = true;
            try
            {
                if (sourceStorage) Page.SetValue(game, sourcePage);
                if (sourceCraft)
                {
                    // Return only the complete, still-held native item to its
                    // exact source, including the output slot. Native placement
                    // retains CraftCheck and loader metadata handling.
                    string method = empty ? "PlaceItemCraft" : "CombineItemCraft";
                    MethodInfo placement = typeof(GameScript).GetMethod(method,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(int) }, null);
                    if (placement == null) { reason = "Crafting return is unavailable."; return false; }
                    placement.Invoke(game, new object[] { sourceSlot });
                }
                else if (sourceStorage)
                {
                    if (empty) game.PlaceItemStorage(sourceSlot); else game.CombineItemStorage(sourceSlot);
                }
                else
                {
                    if (empty) game.PlaceItem(sourceSlot); else game.CombineItem(sourceSlot);
                }
                if (HasHeldItem(game))
                { reason = "The original slot rejected the return."; return false; }
                sourceSlot = -1;
                PlaySound(game, "Au/confirm");
                return true;
            }
            catch (Exception)
            {
                // Native placement may have completed before a cosmetic refresh
                // threw. Never refund a copy and risk duplicating the real item.
                if (!HasHeldItem(game)) { sourceSlot = -1; return true; }
                reason = "The item could not be returned safely.";
                return false;
            }
            finally
            {
                Page.SetValue(game, previousPage);
                returning = false;
                if (sourceStorage && sourcePage != previousPage)
                    try { game.StartCoroutine(game.RefreshStoragePage(previousPage)); } catch { }
            }
        }

        private static bool ReturnChip(GameScript game, out string reason)
        {
            reason = null;
            GameObject menu = ChipRoot(game);
            int[] chips = CombatChips == null ? null : CombatChips.GetValue(game) as int[];
            if (!sourceChip || sourceSlot < 0)
            { reason = "The held chip's source is unavailable."; return false; }
            if (!GameScript.inventoryOpen || menu == null || !menu.activeSelf ||
                !ReferenceEquals(menu, sourceChipMenu) || chips == null || sourceSlot >= chips.Length)
            { reason = "The original Combat Chip menu is unavailable."; return false; }
            if (chips[sourceSlot] != 0)
            { reason = "The original chip slot is occupied."; return false; }
            MethodInfo placement = typeof(GameScript).GetMethod("PlaceCombatChip",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(int) }, null);
            if (placement == null) { reason = "Combat Chip return is unavailable."; return false; }
            returning = true;
            try
            {
                // The native method and loader hooks own equip effects and stats.
                placement.Invoke(game, new object[] { sourceSlot });
                if (HasHeldItem(game)) { reason = "The original chip slot rejected the return."; return false; }
                sourceSlot = -1;
                PlaySound(game, "Au/confirm");
                return true;
            }
            catch (Exception)
            {
                // A cosmetic callback can fail after placement committed. Do
                // not refund a copy of a chip already equipped by the native path.
                if (!HasHeldItem(game)) { sourceSlot = -1; return true; }
                reason = "The chip could not be returned safely.";
                return false;
            }
            finally { returning = false; }
        }

        internal static int MapStorageSlot(int page, int slot, int length)
        {
            if (page < 0 || page >= 12 || slot < 0 || slot >= (length >= 432 ? 36 : 30)) return -1;
            return slot < 30 ? page * 30 + slot : 360 + page * 6 + slot - 30;
        }

        internal static bool TryDropAbovePlayer(GameScript game, out string reason)
        {
            ObserveContext(game);
            if (!HasHeldItem(game)) { reason = null; return true; }
            if (MenuScript.player == null) { reason = "Player position is unavailable."; return false; }
            return Drop(game, MenuScript.player.transform.position + new Vector3(0f, 3f, 0f), out reason);
        }

        internal static bool LeaveRange(GameScript game, out string reason)
        {
            ObserveContext(game);
            if (TryReturn(game, out reason)) return true;
            if (!interactionCaptured) { reason = "Interaction position is unavailable; the held item was preserved."; return false; }
            // Range exits from chest, portal and forge UIs share the same native drop
            // transaction. Lift clear of the opener collider; validate in-game.
            return Drop(game, interactionPosition + new Vector3(0f, 1.5f, 0f), out reason);
        }

        // Set only by the exact installed GadgetCore DropItem transpiler. Missing
        // instrumentation disables our drop actions, never unrelated native drops.
        internal static void SetDropCaptureReady(bool ready) { captureReady = ready; }

        internal static UnityEngine.Object TrackSpawn(UnityEngine.Object spawned)
        {
            if (dropping)
            {
                GameObject obj = spawned as GameObject;
                if (obj == null) captureUnknown = true;
                else capturedDrops.Add(obj);
            }
            return spawned;
        }

        internal static bool MutationsBlocked(GameScript game)
        {
            return ReferenceEquals(owner, game) && (dropping || uncertainDrop);
        }

        private static bool RollBackDrop()
        {
            bool complete = !captureUnknown;
            foreach (GameObject spawned in capturedDrops)
            {
                try { Network.Destroy(spawned); }
                catch { complete = false; }
            }
            return complete;
        }

        private static bool Drop(GameScript game, Vector3 position, out string reason)
        {
            reason = null;
            if (dropping || uncertainDrop) { reason = "A drop is already in progress or needs inspection."; return false; }
            bool isChip = IsHoldingChip(game);
            Item held = Held(game);
            if (!HasHeldItem(game)) return true;
            if (!captureReady)
            { reason = "Safe network drop capture is unavailable; the held item was preserved."; return false; }
            capturedDrops.Clear();
            captureUnknown = false;
            dropping = true;
            try
            {
                // GadgetCore serializes extra data and host IDs and uses its native
                // network Init RPC. Do not replace this with a local visual spawn.
                ItemScript dropped = GadgetCoreAPI.DropItem(position, GadgetCoreAPI.CopyItem(held), isChip);
                if (dropped == null)
                {
                    uncertainDrop = !RollBackDrop();
                    reason = uncertainDrop ? "Drop rollback failed; item actions are blocked. Inspect before continuing."
                        : "The item could not be spawned; the held item was preserved.";
                    return false;
                }
                if (captureUnknown || capturedDrops.Count != 1)
                {
                    uncertainDrop = true;
                    reason = "Unexpected drop capture; item actions are blocked. Inspect before continuing.";
                    return false;
                }
                Holding.SetValue(game, EmptyItem());
                if (isChip) CombatChip.SetValue(game, false);
                sourceSlot = -1;
                try { game.RefreshHoldingSlot(); } catch { }
                return true;
            }
            catch (Exception)
            {
                // The instrumentation captures the real network object before
                // GetComponent/RPC/cosmetic initialization can throw. Remove that
                // object before allowing another operation with the held item.
                uncertainDrop = !RollBackDrop();
                reason = uncertainDrop ? "Drop rollback failed; item actions are blocked. Inspect before continuing."
                    : "The drop failed and was rolled back; the held item was preserved.";
                return false;
            }
            finally { dropping = false; capturedDrops.Clear(); captureUnknown = false; }
        }

        internal static void PlaySound(GameScript game, string path)
        {
            try
            {
                AudioSource audio = game.GetComponent<AudioSource>();
                AudioClip clip = Resources.Load(path) as AudioClip;
                if (audio != null && clip != null) audio.PlayOneShot(clip, Menuu.soundLevel / 10f);
            }
            catch { }
        }

        internal static void Reset()
        {
            capturedDrops.Clear();
            captureUnknown = false;
            owner = null;
            sourceSlot = -1;
            sourcePage = 0;
            scene = -1;
            interactionRoot = null;
            sourceChipMenu = null;
            sourceCraftType = interactionCraftType = -1;
            interactionPosition = new Vector3();
            sourceStorage = sourceCraft = sourceChip = returning = dropping = uncertainDrop = interactionCaptured = false;
        }
    }
}
