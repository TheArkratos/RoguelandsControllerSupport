using System;
using System.Reflection;
using UnityEngine;

namespace ControllerSupport
{
    // Selection state only. Never clicks, moves, or activates an item/page.
    internal sealed class InventorySelectionNavigator
    {
        // Six stat hitboxes (names 1..6), 30 inventory grid slots (6..35),
        // six equipped items (36..41), and three equipped droids (42..44).
        // Faith and grid slot 6
        // deliberately share a child name but have different roles/tags.
        private const int MainSlotCount = 45;
        private const int HotkeySlotCount = 6;
        private const int StorageFirstSlot = MainSlotCount + HotkeySlotCount;
        private const int StorageSlotCount = 36;
        private const int PageFirstSlot = StorageFirstSlot + StorageSlotCount;
        private const int PageSlotCount = 12;
        private const int UpgradeSlot = PageFirstSlot + PageSlotCount;
        private const int SlotCount = EmblemForgeContext.ToggleSlot + 1;
        private const int GridFirstSlot = 6;
        private const int GridSlotCount = 30;
        private const int GridColumns = 6;
        private const int EquippedFirstSlot = GridFirstSlot + GridSlotCount;
        private const int EquippedSlotCount = GridColumns;
        private const float StickThreshold = 0.45f;
        private const float FirstRepeatDelay = 0.30f;
        private const float RepeatInterval = 0.12f;
        private const int InventoryLayerMask = 1 << 16;
        private static readonly FieldInfo StorageLevelField = typeof(GameScript).GetField(
            "storageLevel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo StorageButtonsField = typeof(GameScript).GetField(
            "storageButton", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private readonly Collider[] slots = new Collider[SlotCount];
        private GameObject inventoryRoot;
        private GameObject hotkeyRoot;
        private GameObject storageRoot;
        private bool portalOpen;
        private GameObject forgeRoot;
        private GameScript owner;
        private Vector3 lastMousePosition;
        private bool sampledMouse;
        private int heldDirection;
        private float nextRepeatAt;

        internal int SelectedSlot { get; private set; } = -1;
        internal int SelectedStoragePage
        {
            get { return SelectedSlot >= PageFirstSlot && SelectedSlot < UpgradeSlot ?
                SelectedSlot - PageFirstSlot : -1; }
        }
        internal bool SelectedIsStoragePage { get { return SelectedStoragePage >= 0; } }
        internal bool StoragePageAvailable(int page)
        { return page >= 0 && page < PageSlotCount && ValidSlot(PageFirstSlot + page); }
        internal bool SelectedIsStorageUpgrade { get { return SelectedSlot == UpgradeSlot; } }
        internal bool SelectedIsPortal { get { return SelectedSlot >= PortalMenuContext.FirstSlot && SelectedSlot < PortalMenuContext.FirstSlot + PortalMenuContext.TargetCount; } }
        internal bool SelectedIsForgeToggle { get { return SelectedSlot == EmblemForgeContext.ToggleSlot; } }
        internal Collider SelectedCollider
        {
            get
            {
                if (SelectedSlot >= 0 && !ValidSlot(SelectedSlot)) ClearSelection();
                return SelectedSlot >= 0 ? slots[SelectedSlot] : null;
            }
        }

        internal void Update(GameScript game, JoystickInputState state)
        {
            if (game == null || !GameScript.inventoryOpen || !game.inventoryMain ||
                !game.inventoryMain.activeInHierarchy)
            {
                Reset();
                return;
            }

            GameObject activeStorage = game.menuStorage &&
                game.menuStorage.activeInHierarchy ? game.menuStorage : null;
            bool nextPortalOpen = PortalMenuContext.IsOpen(game);
            bool openingPortal = nextPortalOpen && (!portalOpen || owner != game ||
                inventoryRoot != game.inventoryMain);
            if (owner != game || inventoryRoot != game.inventoryMain ||
                hotkeyRoot != game.inventoryBar || storageRoot != activeStorage || portalOpen != nextPortalOpen ||
                forgeRoot != (EmblemForgeContext.IsOpen(game) ? EmblemForgeContext.Root(game) : null))
            {
                owner = game;
                inventoryRoot = game.inventoryMain;
                hotkeyRoot = game.inventoryBar;
                storageRoot = activeStorage;
                portalOpen = nextPortalOpen;
                forgeRoot = EmblemForgeContext.IsOpen(game) ? EmblemForgeContext.Root(game) : null;
                RefreshSlots();
                sampledMouse = false;
                heldDirection = 0;
                // Portal entry starts at its first eligible planet (native cell
                // zero is Desolate Canyon), rather than an incidental stationary
                // inventory hover. This changes selection only; native planet
                // choice is still performed by a fresh activation. Other panes
                // retain their initial physical-hover behavior, falling back to
                // the top-left backpack cell when no slot is under the mouse.
                int portalDefault = openingPortal ? FirstPortalSlot() : -1;
                int hovered = portalDefault >= 0 ? -1 : SlotUnderMouse(Input.mousePosition);
                SelectedSlot = portalDefault >= 0 ? portalDefault : hovered >= 0 ? hovered :
                    ValidSlot(GridFirstSlot) ? GridFirstSlot : FirstValidSlot();
            }
            else if (storageRoot != null || portalOpen || forgeRoot)
            {
                // SixBySixChest may add its final row after this navigator has
                // initialized. Page unlocks can also change while storage is open.
                RefreshSlots();
                if (SelectedSlot >= 0 && !ValidSlot(SelectedSlot)) ClearSelection();
            }
            else if (SelectedSlot >= 0 && !ValidSlot(SelectedSlot))
            {
                RefreshSlots();
                ClearSelection();
            }

            // Only actual mouse motion or a click takes selection back from the
            // controller. Sampling the current position on open is not motion.
            Vector3 mouse = Input.mousePosition;
            bool mouseMoved = sampledMouse &&
                (mouse - lastMousePosition).sqrMagnitude > 0.25f;
            bool mouseClicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            lastMousePosition = mouse;
            sampledMouse = true;
            if (mouseMoved || mouseClicked)
            {
                int mouseSlot = SlotUnderMouse(mouse);
                if (mouseSlot >= 0) SelectedSlot = mouseSlot;
                // Mouse activity wins this frame, even if it did not land on
                // an inventory slot; it must not produce a controller step.
                heldDirection = 0;
                return;
            }

            int direction = Direction(state);
            if (direction == 0)
            {
                heldDirection = 0;
                return;
            }

            float now = Time.unscaledTime;
            if (direction != heldDirection)
            {
                heldDirection = direction;
                Move(direction);
                nextRepeatAt = now + FirstRepeatDelay;
            }
            else if (now >= nextRepeatAt)
            {
                Move(direction);
                nextRepeatAt = now + RepeatInterval;
            }
        }

        internal void Reset()
        {
            owner = null;
            inventoryRoot = null;
            hotkeyRoot = null;
            storageRoot = null;
            portalOpen = false;
            forgeRoot = null;
            Array.Clear(slots, 0, slots.Length);
            sampledMouse = false;
            heldDirection = 0;
            nextRepeatAt = 0f;
            SelectedSlot = -1;
        }

        private void RefreshSlots()
        {
            Array.Clear(slots, 0, slots.Length);
            if (!inventoryRoot) return;
            Transform root = inventoryRoot.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!child || child.gameObject.layer != 16) continue;
                int number;
                if (!int.TryParse(child.gameObject.name, out number)) continue;
                bool stat = child.gameObject.tag == "stat";
                int slot = stat ? number - 1 : number;
                if (stat ? number < 1 || number > 6 :
                    number < GridFirstSlot || number >= MainSlotCount) continue;
                slots[slot] = child.GetComponent<Collider>();
            }
            if (hotkeyRoot)
            {
                Transform bar = hotkeyRoot.transform;
                for (int i = 0; i < bar.childCount; i++)
                {
                    Transform child = bar.GetChild(i);
                    if (!child || child.gameObject.layer != 16) continue;
                    int number;
                    if (int.TryParse(child.gameObject.name, out number) &&
                        number >= 0 && number < HotkeySlotCount)
                        slots[MainSlotCount + number] = child.GetComponent<Collider>();
                }
            }
            PortalMenuContext.CollectTargets(owner, slots);
            slots[EmblemForgeContext.ToggleSlot] = EmblemForgeContext.CollectTarget(owner);
            if (!storageRoot) return;
            Transform storage = storageRoot.transform;
            for (int i = 0; i < storage.childCount; i++)
            {
                Transform child = storage.GetChild(i);
                if (!child) continue;
                // Native GameScript dispatches this exact button name to
                // UpgradeStorage; ButtonMenu buttons need not use layer 16.
                if (child.gameObject.name == "bStorageU")
                {
                    slots[UpgradeSlot] = child.GetComponent<Collider>();
                    continue;
                }
                if (child.gameObject.layer != 16 ||
                    child.gameObject.tag != "storage") continue;
                int number;
                if (int.TryParse(child.gameObject.name, out number) &&
                    number >= 0 && number < StorageSlotCount)
                    slots[StorageFirstSlot + number] = child.GetComponent<Collider>();
            }
            // The native level gate is authoritative; a visible locked page
            // collider must never become a controller or mouse selection.
            int storageLevel = -1;
            try
            {
                if (StorageLevelField != null)
                    storageLevel = (int)StorageLevelField.GetValue(owner);
            }
            catch (Exception) { /* Fail closed on page eligibility. */ }
            GameObject[] buttons = null;
            try
            {
                if (StorageButtonsField != null)
                    buttons = StorageButtonsField.GetValue(owner) as GameObject[];
            }
            catch (Exception) { /* Missing page metadata leaves pages unfocusable. */ }
            if (buttons == null) return;
            for (int page = 0; page < PageSlotCount && page <= storageLevel &&
                page < buttons.Length; page++)
            {
                GameObject button = buttons[page];
                if (button && button.name == "page" + page)
                    slots[PageFirstSlot + page] = button.GetComponent<Collider>();
            }
        }

        private int FirstPortalSlot()
        {
            // Collection already applies native eligibility. Planet cells precede
            // controls, so a locked/missing cell zero falls back to another
            // available planet, then an available portal control.
            for (int slot = PortalMenuContext.FirstSlot;
                slot < PortalMenuContext.FirstSlot + PortalMenuContext.TargetCount; slot++)
                if (ValidSlot(slot)) return slot;
            return -1;
        }

        private int FirstValidSlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (ValidSlot(i)) return i;
            return -1;
        }

        private bool ValidSlot(int slot)
        {
            return slot >= 0 && slot < SlotCount && slots[slot] &&
                slots[slot].enabled && slots[slot].gameObject.activeInHierarchy &&
                (slot >= PageFirstSlot || slots[slot].gameObject.layer == 16);
        }

        private void ClearSelection() { SelectedSlot = -1; }

        private int SlotUnderMouse(Vector3 mouse)
        {
            Camera camera = Camera.main;
            if (!camera) return -1;
            Ray ray = camera.ScreenPointToRay(mouse);
            RaycastHit hit;
            // Page buttons can be on a different layer from inventory slots.
            if ((storageRoot || portalOpen || forgeRoot) && Physics.Raycast(ray, out hit, 5f, ~0))
            {
                int page = FindHitSlot(hit.collider, PageFirstSlot, SlotCount);
                if (page >= 0) return page;
            }
            if (Physics.Raycast(ray, out hit, 5f, InventoryLayerMask))
                return FindHitSlot(hit.collider, 0, PageFirstSlot);
            return -1;
        }

        private int FindHitSlot(Collider collider, int first, int end)
        {
            if (!collider) return -1;
            for (int i = first; i < end; i++)
                if (ValidSlot(i) && slots[i] == collider) return i;
            return -1;
        }

        // -1/1 = left/right, -2/2 = up/down. Linux joystick Y grows down.
        private static int Direction(JoystickInputState state)
        {
            if (state == null || !state.Connected) return 0;
            float x = state.AimX, y = state.AimY;
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            if (ax < StickThreshold && ay < StickThreshold) return 0;
            return ax >= ay ? (x < 0f ? -1 : 1) : (y < 0f ? -2 : 2);
        }

        private void Move(int direction)
        {
            if (!ValidSlot(SelectedSlot))
            {
                SelectedSlot = FirstValidSlot();
                return;
            }

            int next = -1;
            if (storageRoot && SelectedSlot >= StorageFirstSlot && SelectedSlot <= UpgradeSlot)
            {
                next = SelectedSlot == UpgradeSlot ? NearestInDirection(direction, false) :
                    SelectedSlot < PageFirstSlot ? StorageNeighbor(direction) : PageNeighbor(direction);
                // Only an actual edge may lead to the capacity button. A locked
                // page is still a blocked route, never a reason to skip pages.
                if (next < 0 && ValidSlot(UpgradeSlot) &&
                    IsInDirection(UpgradeSlot, direction)) next = UpgradeSlot;
                if (ValidSlot(next)) SelectedSlot = next;
                return;
            }
            if (SelectedSlot >= GridFirstSlot &&
                SelectedSlot < GridFirstSlot + GridSlotCount)
            {
                int column = (SelectedSlot - GridFirstSlot) % GridColumns;
                int row = (SelectedSlot - GridFirstSlot) / GridColumns;
                if (direction == -1 && column > 0) next = SelectedSlot - 1;
                else if (direction == 1 && column < GridColumns - 1) next = SelectedSlot + 1;
                else if (direction == -2 && row > 0) next = SelectedSlot - GridColumns;
                else if (direction == -2 && row == 0)
                    next = EquippedFirstSlot + column;
                else if (direction == 2 && row < GridSlotCount / GridColumns - 1)
                    next = SelectedSlot + GridColumns;
                else if (storageRoot && direction == 1 && column == GridColumns - 1)
                    next = StorageFirstSlot + row * GridColumns;
                if (ValidSlot(next))
                {
                    SelectedSlot = next;
                    return;
                }
                if (storageRoot && direction == 1 && column == GridColumns - 1)
                    return;
            }
            else if (SelectedSlot >= EquippedFirstSlot &&
                SelectedSlot < EquippedFirstSlot + EquippedSlotCount && direction == 2)
            {
                next = GridFirstSlot + SelectedSlot - EquippedFirstSlot;
                if (ValidSlot(next))
                {
                    SelectedSlot = next;
                    return;
                }
            }

            // Equipped items, droids and hotkeys are outside the item grid.
            // At section edges choose the nearest on-screen slot in that
            // direction, without wrapping to an unrelated grid row.
            next = NearestInDirection(direction,
                SelectedSlot >= GridFirstSlot &&
                SelectedSlot < GridFirstSlot + GridSlotCount ||
                direction == -2 && SelectedSlot >= EquippedFirstSlot &&
                SelectedSlot < EquippedFirstSlot + EquippedSlotCount);
            if (next >= 0) SelectedSlot = next;
        }

        private int StorageNeighbor(int direction)
        {
            int offset = SelectedSlot - StorageFirstSlot;
            int row = offset / GridColumns, column = offset % GridColumns;
            if (direction == -1)
                return column > 0 ? SelectedSlot - 1 :
                    row < GridSlotCount / GridColumns ?
                    GridFirstSlot + row * GridColumns + GridColumns - 1 :
                    GridFirstSlot + GridSlotCount - 1;
            if (direction == 1) return column < GridColumns - 1 ? SelectedSlot + 1 : -1;
            if (direction == 2)
                return row < StorageSlotCount / GridColumns - 1 ?
                    SelectedSlot + GridColumns : -1;
            if (row > 0) return SelectedSlot - GridColumns;
            int lowerPage = PageFirstSlot + GridColumns + column;
            return ValidSlot(lowerPage) ? lowerPage : PageFirstSlot + column;
        }

        private int PageNeighbor(int direction)
        {
            int offset = SelectedSlot - PageFirstSlot;
            int row = offset / GridColumns, column = offset % GridColumns;
            // At the left edge of either page row, return to the inventory's
            // top-right item slot, matching the chest/inventory panel route.
            if (direction == -1) return column > 0 ? SelectedSlot - 1 :
                GridFirstSlot + GridColumns - 1;
            if (direction == 1) return column < GridColumns - 1 ? SelectedSlot + 1 : -1;
            if (direction == -2) return row == 1 ? SelectedSlot - GridColumns : -1;
            if (row == 1) return StorageFirstSlot + column;
            int lowerPage = SelectedSlot + GridColumns;
            return ValidSlot(lowerPage) ? lowerPage : StorageFirstSlot + column;
        }

        private bool IsInDirection(int target, int direction)
        {
            Camera camera = Camera.main;
            if (!camera || !ValidSlot(SelectedSlot) || !ValidSlot(target)) return false;
            Vector3 source = camera.WorldToScreenPoint(slots[SelectedSlot].bounds.center);
            Vector3 point = camera.WorldToScreenPoint(slots[target].bounds.center);
            float forward = direction == -1 ? source.x - point.x :
                direction == 1 ? point.x - source.x :
                direction == -2 ? point.y - source.y : source.y - point.y;
            return forward > 1f;
        }

        private int NearestInDirection(int direction, bool equipmentOnly)
        {
            Camera camera = Camera.main;
            if (!camera) return -1;
            Vector3 source = camera.WorldToScreenPoint(slots[SelectedSlot].bounds.center);
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < SlotCount; i++)
            {
                if (equipmentOnly && i >= GridFirstSlot &&
                    i < GridFirstSlot + GridSlotCount) continue;
                if (i == SelectedSlot || !ValidSlot(i)) continue;
                Vector3 point = camera.WorldToScreenPoint(slots[i].bounds.center);
                float dx = point.x - source.x, dy = point.y - source.y;
                float forward = direction == -1 ? -dx : direction == 1 ? dx :
                    direction == -2 ? dy : -dy;
                if (forward <= 1f) continue;
                float cross = direction == -1 || direction == 1 ? Mathf.Abs(dy) : Mathf.Abs(dx);
                // A strong directional preference prevents distant off-axis
                // slots from winning over an obvious neighbor.
                float score = forward + 2f * cross;
                if (score < bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
