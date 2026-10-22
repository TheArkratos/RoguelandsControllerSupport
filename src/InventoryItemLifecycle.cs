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
        private static readonly FieldInfo Page = AccessToolsField("curStoragePage");
        private static readonly FieldInfo CombatChip = AccessToolsField("holdingCombatChip");
        private static readonly FieldInfo PortalMenu = AccessToolsField("menuPlanets");
        private static readonly FieldInfo EmblemForgeMenu = AccessToolsField("menuEmblemForge");
        private static readonly FieldInfo TrasherMenu = AccessToolsField("menuTrasher");
        private static GameScript owner;
        private static int scene = -1;
        private static int sourceSlot = -1;
        private static int sourcePage;
        private static bool sourceStorage;
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
        private static bool Empty(Item item) { return item == null || item.id == 0 || item.q <= 0; }
        private static Item EmptyItem() { return new Item(0, 0, 0, 0, 0, new int[3], new int[3]); }
        internal static int CurrentPage(GameScript game) { return (int)Page.GetValue(game); }

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
            GameObject root = !GameScript.inventoryOpen ? null
                : game.menuStorage != null && game.menuStorage.activeSelf ? game.menuStorage
                : portal != null && portal.activeSelf ? portal
                : forge != null && forge.activeSelf ? forge
                : trasher != null && trasher.activeSelf ? trasher : null;
            if (!ReferenceEquals(root, interactionRoot))
            {
                // A shared inventory pane may belong to a chest, portal console
                // or item conversion station. Capture its opener once, never the new interaction
                // target encountered while walking out of the original range.
                interactionRoot = root;
                interactionCaptured = false;
                if (root != null && PlayerScript.curInteractObj != null)
                {
                    interactionPosition = PlayerScript.curInteractObj.transform.position;
                    interactionCaptured = true;
                }
            }
            if (Empty(Held(game))) sourceSlot = -1;
        }

        // Called around native methods, including physical mouse actions. Tracking
        // needs no storage reads and therefore remains correct inside SixBySix's
        // nested temporary projection of the sixth row during GameScript.Update.
        internal static void RecordSource(GameScript game, int slot, int page, bool storage)
        {
            ObserveContext(game);
            if (returning || Empty(Held(game))) return;
            sourceSlot = slot;
            sourcePage = page;
            sourceStorage = storage;
        }

        internal static void ClearIfEmpty(GameScript game)
        {
            if (ReferenceEquals(owner, game) && Empty(Held(game))) sourceSlot = -1;
        }

        internal static bool TryReturn(GameScript game, out string reason)
        {
            ObserveContext(game);
            reason = null;
            Item held = Held(game);
            if (Empty(held)) return true;
            if (uncertainDrop) { reason = "Drop outcome is uncertain; inspect the item before continuing."; return false; }
            if (CombatChip != null && (bool)CombatChip.GetValue(game))
            { reason = "Combat Chip return is not supported yet."; return false; }
            if (sourceSlot < 0) { reason = "The held item's source is unavailable."; return false; }
            Item[] items = (Item[])(sourceStorage ? Storage : Inventory).GetValue(game);
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
                if (sourceStorage)
                {
                    if (empty) game.PlaceItemStorage(sourceSlot); else game.CombineItemStorage(sourceSlot);
                }
                else
                {
                    if (empty) game.PlaceItem(sourceSlot); else game.CombineItem(sourceSlot);
                }
                if (!Empty(Held(game)))
                { reason = "The original slot rejected the return."; return false; }
                sourceSlot = -1;
                PlaySound(game, "Au/confirm");
                return true;
            }
            catch (Exception)
            {
                // Native placement may have completed before a cosmetic refresh
                // threw. Never refund a copy and risk duplicating the real item.
                if (Empty(Held(game))) { sourceSlot = -1; return true; }
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

        internal static int MapStorageSlot(int page, int slot, int length)
        {
            if (page < 0 || page >= 12 || slot < 0 || slot >= (length >= 432 ? 36 : 30)) return -1;
            return slot < 30 ? page * 30 + slot : 360 + page * 6 + slot - 30;
        }

        internal static bool TryDropAbovePlayer(GameScript game, out string reason)
        {
            ObserveContext(game);
            if (Empty(Held(game))) { reason = null; return true; }
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
            if (CombatChip != null && (bool)CombatChip.GetValue(game))
            { reason = "Combat Chip dropping is not supported yet."; return false; }
            Item held = Held(game);
            if (Empty(held)) return true;
            if (!captureReady)
            { reason = "Safe network drop capture is unavailable; the held item was preserved."; return false; }
            capturedDrops.Clear();
            captureUnknown = false;
            dropping = true;
            try
            {
                // GadgetCore serializes extra data and host IDs and uses its native
                // network Init RPC. Do not replace this with a local visual spawn.
                ItemScript dropped = GadgetCoreAPI.DropItem(position, GadgetCoreAPI.CopyItem(held));
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
            interactionPosition = new Vector3();
            sourceStorage = returning = dropping = uncertainDrop = interactionCaptured = false;
        }
    }
}
