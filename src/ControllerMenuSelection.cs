using System;
using System.Collections.Generic;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // A single native-click adapter for every player-inventory region. Game and
    // GadgetCore methods retain ownership of items, eligibility and side effects.
    internal static class ControllerMenuSelection
    {
        private const float CornerFractionOfSlot = 0.32f;
        private const int CornerPixels = 6;
        private const int SortingOrder = 30000;
        private static readonly FieldInfo InventoryField = Field("inventory");
        private static readonly FieldInfo StorageField = Field("storage");
        private static readonly FieldInfo[] SpecialModes = { Field("emblemForging"), Field("trashing"), Field("prisming") };
        private static readonly FieldInfo StorageLevel = Field("storageLevel");
        private static readonly bool[] pagingArmed = new bool[2];
        private static GameObject pagingPlayer;
        private static int pagingScene = -1;
        private static readonly string[] Actions = { "MenuActivate", "MenuSecondary", "MenuTransfer",
            "MenuDrop", "MenuCancel", "Inventory", "Pause" };
        private static readonly bool[] armed = new bool[Actions.Length];
        private static readonly InventorySelectionNavigator navigator = new InventorySelectionNavigator();
        private static Func<JoystickInputState> stateProvider;
        private static Func<bool> usingControllerProvider;
        private static Action<string> logWarning;
        private static BindingConfig bindings;
        private static SelectionCornerRenderer renderer;
        private static readonly Func<Bounds?> HeldBadgeProvider = CurrentHeldBadgeBounds;
        private static bool rendererIsPage, assetFailed, updateFailed;
        private static GameScript preparedGame, selectionGame;
        private static int preparedFrame = -1;
        private static bool storageClick;
        private static bool ownershipSuspended;
        private static int virtualButton = -1;
        private static Collider clickCollider;
        private static Vector3 lastPhysicalMouse;
        private static bool sampledPhysicalMouse, controllerOwnsSelection;
        private static GameObject auxiliaryPane;
        private static bool portalContext, conversionModeContext;
        private static readonly FieldInfo NativeHoverField = Field("hoverItem");
        private static GameScript hoverGame;
        private static Collider hoverCollider;
        private static bool hoverControllerOwned;
        private static GameObject hiddenNativeHeldObject;
        private static Vector3 hiddenNativeHeldScale;
        private static float nativeHeldIconWidth, nativeHeldIconHeight;
        private static Collider repeatTarget;
        private static bool repeating, repeatBlocked;
        private static float nextRepeat;
        private static Item beforeAction;
        private static Item beforeTarget;
        private static Item beforeActionReference, beforeTargetReference;
        private static int actionFrame = -1;
        private static bool actionIsTransfer;
        private static int actionSlot;
        private static bool actionIsStorage;
        private static int nativeErrorFrame = -1;
        private static int actionPage;

        private static FieldInfo Field(string name)
        { return typeof(GameScript).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }

        internal static void Configure(Func<JoystickInputState> provider, BindingConfig config,
            Action<string> warningLogger, Func<bool> controllerSource = null)
        {
            Reset(); stateProvider = provider; bindings = config;
            logWarning = warningLogger; usingControllerProvider = controllerSource;
        }

        internal static bool InventoryOpen(GameScript game)
        {
            return game != null && !GameScript.pausing && !GameScript.dead && !GameScript.buildMode &&
                GameScript.inventoryOpen && game.inventoryMain && game.inventoryMain.activeInHierarchy;
        }

        internal static bool InventoryActionsOwnInput
        { get { return OwnershipActive() && stateProvider != null && InventoryOpen(selectionGame); } }

        private static bool StorageOpen(GameScript game)
        { return InventoryOpen(game) && game.menuStorage && game.menuStorage.activeInHierarchy; }

        internal static void Prepare(GameScript game)
        {
            InventoryNativeCursor.Update(game);
            if (!OwnershipActive()) return;
            if (stateProvider == null || updateFailed || bindings == null) return;
            if (preparedGame == game && preparedFrame == Time.frameCount) return;
            EndNativeUpdate(); preparedGame = game; preparedFrame = Time.frameCount;
            try
            {
                InventoryItemLifecycle.ObserveContext(game);
                JoystickInputState state = stateProvider();
                bool open = InventoryOpen(game);
                if (open) ControllerGameplayInput.ConsumeInventoryInput();
                bool portal = PortalMenuContext.IsOpen(game);
                GameObject pane = game != null && game.menuStorage && game.menuStorage.activeInHierarchy ? game.menuStorage : null;
                if (pane == null && ItemConversionContext.IsOpen(game)) pane = ItemConversionContext.Root(game);
                bool conversionMode = ItemConversionContext.ModeActive(game);
                bool newlyOpen = ownershipSuspended || selectionGame != game || !open || portalContext != portal || auxiliaryPane != pane || conversionModeContext != conversionMode;
                ownershipSuspended = false;
                conversionModeContext = conversionMode;
                portalContext = portal; auxiliaryPane = pane;
                selectionGame = open ? game : null;
                Vector3 mouse = Input.mousePosition;
                bool moved = sampledPhysicalMouse && (mouse - lastPhysicalMouse).sqrMagnitude > 0.25f;
                bool clicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                lastPhysicalMouse = mouse; sampledPhysicalMouse = true;
                navigator.Update(game, state);
                if (usingControllerProvider != null)
                    controllerOwnsSelection = open && !moved && !clicked && usingControllerProvider();
                else
                {
                    if (!open || newlyOpen || moved || clicked) controllerOwnsSelection = false;
                    if (open && !moved && !clicked && state != null && state.Connected &&
                        (Mathf.Abs(state.AimX) >= 0.45f || Mathf.Abs(state.AimY) >= 0.45f))
                        controllerOwnsSelection = true;
                }
                bool[] pressed = new bool[Actions.Length];
                for (int i = 0; i < Actions.Length; i++)
                {
                    bool held = bindings.Held(Actions[i], state);
                    if (!open || newlyOpen) armed[i] = !held;
                    else if (!held) armed[i] = true;
                    pressed[i] = open && !newlyOpen && armed[i] && bindings.Pressed(Actions[i], state);
                    if (pressed[i]) armed[i] = false;
                }
                bool storage = StorageOpen(game);
                bool pagingContextChanged = newlyOpen || pagingPlayer != MenuScript.player ||
                    pagingScene != Application.loadedLevel;
                pagingPlayer = MenuScript.player; pagingScene = Application.loadedLevel;
                NetworkView pagingView = pagingPlayer ? pagingPlayer.GetComponent<NetworkView>() : null;
                bool pagingAllowed = storage && state != null && state.Connected &&
                    pagingView && pagingView.isMine && !GadgetCoreAPI.IsInputFrozen();
                int pageDirection = 0;
                bool bumperActive = false;
                for (int i = 0; i < pagingArmed.Length; i++)
                {
                    bool held = state != null && state.ButtonHeld(4 + i);
                    bool edge = state != null && state.ButtonPressed(4 + i);
                    bumperActive |= held || edge;
                    if (!pagingAllowed || pagingContextChanged) pagingArmed[i] = !held && pagingAllowed;
                    else if (!held) pagingArmed[i] = true;
                    if (pagingAllowed && !pagingContextChanged && pagingArmed[i] && edge)
                    { pagingArmed[i] = false; pageDirection += i == 0 ? -1 : 1; }
                }
                // GadgetCore modal dialogs freeze native game UI. Disarm held
                // controller actions as well, so dismissing a modal cannot drop,
                // return or close using the same press.
                if (open && GadgetCoreAPI.IsInputFrozen())
                {
                    for (int i = 0; i < Actions.Length; i++)
                        if (bindings.Held(Actions[i], state)) armed[i] = false;
                    repeating = false; repeatBlocked = true;
                    return;
                }
                bool secondaryHeld = bindings.Held("MenuSecondary", state);
                if (!open || !secondaryHeld || moved || clicked ||
                    (repeatTarget && repeatTarget != navigator.SelectedCollider))
                {
                    repeating = false;
                    if (!secondaryHeld) repeatBlocked = false;
                    else if (moved || clicked || repeatTarget != navigator.SelectedCollider) repeatBlocked = true;
                }
                if (!open || newlyOpen || moved || clicked || state == null || !state.Connected) return;
                if (InventoryItemLifecycle.MutationsBlocked(game))
                {
                    const string reason = "A drop needs inspection before more item actions";
                    bool attempted = false;
                    for (int i = 0; i < pressed.Length; i++) attempted |= pressed[i];
                    if (attempted) Reject(game, reason);
                    else InventoryFeedback.Reject(reason);
                    return;
                }
                // In storage the physical bumpers own paging before any mapped
                // item/close action. In particular RB must never drop a held stack.
                if (storage && bumperActive)
                {
                    controllerOwnsSelection = true;
                    repeating = false; repeatBlocked = true;
                    if (pageDirection != 0) SwitchStoragePage(game, pageDirection);
                    return;
                }
                if (pressed[4] || pressed[5] || pressed[6])
                {
                    controllerOwnsSelection = true;
                    bool hadItem = HasItem(InventoryItemLifecycle.Held(game));
                    if (pressed[4] && !hadItem && ItemConversionContext.ModeActive(game))
                    {
                        ItemConversionContext.CancelMode(game); repeating = false; repeatBlocked = true;
                        InventoryItemLifecycle.PlaySound(game,"Au/confirm");
                        return;
                    }
                    string reason;
                    if (hadItem && !InventoryItemLifecycle.TryReturn(game, out reason))
                    { Reject(game, reason); return; }
                    repeating = false;
                    if (pressed[4] && hadItem) return;
                    game.CloseAllMenus();
                    if (pressed[6]) game.PauseMenu();
                    return;
                }
                if (pressed[3])
                {
                    controllerOwnsSelection = true;
                    string reason;
                    if (!InventoryItemLifecycle.TryDropAbovePlayer(game, out reason)) Reject(game, reason);
                    repeating = false; repeatBlocked = true;
                    return;
                }
                Collider candidate = navigator.SelectedCollider;
                int index = navigator.SelectedSlot;
                if (navigator.SelectedIsConversionToggle || ItemConversionContext.ModeActive(game))
                {
                    repeating = false; repeatBlocked = true;
                    if (!pressed[0] && !pressed[1] && !pressed[2]) return;
                    if (!pressed[0] || pressed[1] || pressed[2])
                    { Reject(game, ItemConversionContext.SecondaryRejection(game)); return; }
                    bool inventoryTarget = index >= 6 && index <= 35;
                    if (!navigator.SelectedIsConversionToggle && inventoryTarget && HasItem(InventoryItemLifecycle.Held(game)))
                    {
                        controllerOwnsSelection = true; ItemConversionContext.CancelMode(game);
                        return;
                    }
                    string reason;
                    if (!ItemConversionContext.CanActivate(game,index,ReadTarget(game,index),out reason))
                    { Reject(game,reason); return; }
                    if (!ValidateRay(candidate)) return;
                    // Native Update assigns slotID from this raycast before the
                    // loader conversion prefixes may read it. Never call conversion directly.
                    controllerOwnsSelection = true;
                    storageClick = true; virtualButton = 0; clickCollider = candidate;
                    return;
                }
                // Portal controls share the inventory's selection and lifecycle,
                // but never enter item placement, splitting or transfer rules.
                if (navigator.SelectedIsPortal)
                {
                    repeating = false; repeatBlocked = true;
                    if (!pressed[0] && !pressed[1] && !pressed[2]) return;
                    if (!pressed[0] || pressed[1] || pressed[2])
                    { Reject(game, "Use Select for this control"); return; }
                    string reason;
                    if (!PortalMenuContext.CanActivate(game, index, out reason))
                    { Reject(game, reason); return; }
                    if (!ValidateRay(candidate)) return;
                    PortalMenuContext.PrepareClick(game, index, candidate);
                    controllerOwnsSelection = true;
                    storageClick = true; virtualButton = 0; clickCollider = candidate;
                    return;
                }
                Item heldItem = InventoryItemLifecycle.Held(game);
                Item target = ReadTarget(game, index);
                if (repeating && secondaryHeld && !CanPlaceOne(game, index, heldItem, target))
                { repeating = false; repeatBlocked = true; }
                bool singleRepeat = repeating && secondaryHeld && !repeatBlocked &&
                    Time.unscaledTime >= nextRepeat && CanPlaceOne(game, index, heldItem, target);
                int button = pressed[0] || pressed[2] ? 0 : pressed[1] || singleRepeat ? 1 : -1;
                if (button < 0) return;
                if (!IsActionableSlot(game, candidate, index) ||
                    (button == 1 && (navigator.SelectedIsStoragePage || navigator.SelectedIsStorageUpgrade)))
                { Reject(game, "No item action for this target"); repeating = false; repeatBlocked = true; return; }
                if (HasItem(heldItem) && !navigator.SelectedIsStoragePage &&
                    !navigator.SelectedIsStorageUpgrade && !EligibleInventorySlot(index, heldItem))
                { Reject(game, "Wrong item type"); repeating = false; repeatBlocked = true; return; }
                if (pressed[2] && (HasItem(heldItem) || !CanOfferTransfer(game, index, target)))
                { Reject(game, "No transfer available"); return; }
                if (!ValidateRay(candidate)) return;
                if (pressed[2] && !StorageOpen(game))
                {
                    // Native Update validates once before ShiftClick2. This call
                    // can emit Error, so it belongs to the deliberate action,
                    // never the per-frame hint eligibility path.
                    bool allowed;
                    storageClick = true;
                    try { allowed = game.CanPlaceCraft(target.id); }
                    finally { storageClick = false; }
                    if (!allowed)
                    {
                        if (nativeErrorFrame != Time.frameCount) Reject(game, "No transfer available");
                        return;
                    }
                }
                if (navigator.SelectedIsStorageUpgrade && !UpgradeAvailable(game))
                { Reject(game, "Not enough Credits or storage is fully upgraded"); return; }
                controllerOwnsSelection = true;
                beforeActionReference = heldItem; beforeTargetReference = target;
                beforeAction = Copy(heldItem); beforeTarget = Copy(target);
                actionFrame = Time.frameCount; actionIsTransfer = pressed[2];
                actionSlot = index; actionIsStorage = index >= 51 && index <= 86;
                actionPage = InventoryItemLifecycle.CurrentPage(game);
                if (pressed[2])
                {
                    // Invoke the effective Shift-click method directly. Never change
                    // the keyboard's shifting latch or depend on Shift release timing.
                    if (actionIsStorage) game.StartCoroutine(game.ShiftClickStorage(index - 51));
                    else if (StorageOpen(game)) game.StartCoroutine(game.ShiftClick(NativeSlot(index)));
                    else game.StartCoroutine(game.ShiftClick2(NativeSlot(index)));
                    repeating = false; repeatBlocked = true;
                    return;
                }
                if (button == 1 && HasItem(heldItem) && HasItem(target) && heldItem.id == target.id &&
                    !CanStack(heldItem, target))
                {
                    // Native PlaceOneItem compares IDs alone; its merge can discard
                    // metadata. Use the existing native swap instead of that unsafe merge.
                    storageClick = true; virtualButton = -1; clickCollider = candidate;
                    try
                    {
                        if (actionIsStorage) game.SwapItemStorage(index - 51);
                        else game.SwapItem(NativeSlot(index));
                    }
                    finally { storageClick = false; clickCollider = null; }
                    repeating = false; repeatBlocked = true;
                    return;
                }
                storageClick = true; virtualButton = button; clickCollider = candidate;
                if (pressed[1])
                {
                    repeating = CanPlaceOne(game, index, heldItem, target);
                    repeatBlocked = !repeating; repeatTarget = candidate;
                    nextRepeat = Time.unscaledTime + 0.35f;
                }
                else if (singleRepeat) nextRepeat = Time.unscaledTime + 0.10f;
            }
            catch (Exception error) { Disable(error); }
            finally { UpdateNativeHoverTransition(game); }
        }

        // Native hover branches share hoverItem, but some targets (including
        // planets) do not populate or hide it. Clear the previous target once
        // before native Update runs; item/stat/difficulty hover can then render
        // its own content normally. Physical mouse dispatch remains native.
        private static void UpdateNativeHoverTransition(GameScript game)
        {
            if (!OwnershipActive()) return;
            bool open = !updateFailed && InventoryOpen(game);
            Collider next = open ? navigator.SelectedCollider : null;
            bool owns = open && controllerOwnsSelection;
            if (owns && (!hoverControllerOwned || hoverGame != game || hoverCollider != next))
            {
                GameObject tooltip = NativeHoverField != null ? NativeHoverField.GetValue(game) as GameObject : null;
                if (tooltip && tooltip.activeInHierarchy) tooltip.SetActive(false);
            }
            hoverGame = open ? game : null;
            hoverCollider = next;
            hoverControllerOwned = owns;
        }

        internal static void Update(GameScript game)
        {
            InventoryNativeCursor.Update(game);
            Prepare(game);
            if (!OwnershipActive() || stateProvider == null || updateFailed) return;
            try
            {
                InventoryItemLifecycle.ClearIfEmpty(game);
                Item held = InventoryItemLifecycle.Held(game);
                Collider selected = navigator.SelectedCollider;
                if (actionFrame == Time.frameCount && !navigator.SelectedIsStoragePage &&
                    !navigator.SelectedIsStorageUpgrade)
                {
                    Item after = ReadTargetAt(game, actionSlot, actionIsStorage, actionPage);
                    // Reference changes are meaningful too: a native swap can exchange
                    // equal base fields while preserving distinct mod extra-data.
                    if (nativeErrorFrame != Time.frameCount &&
                        ReferenceEquals(beforeActionReference, held) && ReferenceEquals(beforeTargetReference, after) &&
                        Same(beforeAction, held) && Same(beforeTarget, after))
                    {
                        Reject(game, actionIsTransfer ? "No space in destination" : "Item action unavailable");
                        repeating = false; repeatBlocked = true;
                    }
                    if (!HasItem(held)) { repeating = false; repeatBlocked = true; }
                    actionFrame = -1;
                }
                if (!InventoryOpen(game) || !selected)
                {
                    RestoreNativeHeldVisual(); if (renderer != null) renderer.Hide();
                    InventoryFeedback.SetContext(game, null, false, bindings, null, null, false, false);
                    return;
                }
                if (renderer != null && !renderer.IsAlive)
                { renderer.Dispose(); renderer = null; assetFailed = false; }
                bool page = navigator.SelectedIsStoragePage || navigator.SelectedIsStorageUpgrade || navigator.SelectedIsPortal || navigator.SelectedIsConversionToggle;
                if (renderer != null && rendererIsPage != page)
                { renderer.Dispose(); renderer = null; }
                if (renderer == null && !assetFailed)
                {
                    float side = Mathf.Min(selected.bounds.size.x, selected.bounds.size.y);
                    if (side <= 0f) return;
                    renderer = SelectionCornerRenderer.TryCreate(CornerPixels / (side * CornerFractionOfSlot), SortingOrder, logWarning);
                    assetFailed = renderer == null; rendererIsPage = page;
                }
                if (renderer != null)
                {
                    int id = controllerOwnsSelection && HasItem(held) ? held.id : 0;
                    if (id > 0) HideNativeHeldVisual(game); else RestoreNativeHeldVisual();
                    renderer.SetStyle(ItemConversionContext.ActiveStyle(game));
                    renderer.SetHeldItemIcon(id, nativeHeldIconWidth, nativeHeldIconHeight);
                    renderer.Show(selected);
                }
                else RestoreNativeHeldVisual();
                string primary, secondary;
                Item target = ReadTarget(game, navigator.SelectedSlot);
                Describe(game, navigator.SelectedSlot, held, target, out primary, out secondary);
                InventoryFeedback.SetContext(game, selected, controllerOwnsSelection && !InventoryItemLifecycle.MutationsBlocked(game), bindings,
                    primary, secondary, !HasItem(held) && CanOfferTransfer(game, navigator.SelectedSlot, target), HasItem(held),
                    renderer != null ? renderer.HeldItemBounds : null,
                    ItemConversionContext.ModeActive(game) && !HasItem(held) ? ItemConversionContext.CancelLabel(game) : null,
                    HeldBadgeProvider, StorageOpen(game));
            }
            catch (Exception error) { Disable(error); }
        }

        private static Bounds? CurrentHeldBadgeBounds()
        {
            return renderer != null ? renderer.HeldItemBounds : null;
        }

        private static void SwitchStoragePage(GameScript game, int direction)
        {
            if (StorageLevel == null) return;
            int last = Math.Min(11, (int)StorageLevel.GetValue(game));
            if (last < 0) return;
            int current = InventoryItemLifecycle.CurrentPage(game);
            for (int step = 1; step <= last + 1; step++)
            {
                int page = ((current + direction * step) % (last + 1) + last + 1) % (last + 1);
                if (page == current || !navigator.StoragePageAvailable(page)) continue;
                // Native SelectPage owns curStoragePage and starts the effective
                // loader/SixBySixChest refresh. It does not touch the held item or
                // its recorded pickup source, nor the selected visual cell.
                game.SelectPage(page);
                return;
            }
        }

        private static void Describe(GameScript game, int slot, Item held, Item target,
            out string primary, out string secondary)
        {
            primary = secondary = null;
            if (InventoryItemLifecycle.MutationsBlocked(game)) return;
            if (slot == ItemConversionContext.ToggleSlot || ItemConversionContext.ModeActive(game))
            {
                if (slot >= 6 && slot <= 35 && HasItem(held)) primary = ItemConversionContext.CancelLabel(game);
                else
                {
                    string reason;
                    if (ItemConversionContext.CanActivate(game,slot,target,out reason)) primary = ItemConversionContext.ActionLabel(game,slot);
                }
                return;
            }
            if (slot >= PortalMenuContext.FirstSlot && slot < PortalMenuContext.FirstSlot + PortalMenuContext.TargetCount)
            {
                string reason;
                if (PortalMenuContext.CanActivate(game, slot, out reason)) primary = PortalMenuContext.ActionLabel(slot);
                return;
            }
            if (navigator.SelectedIsStoragePage) { primary = "Select page"; return; }
            if (navigator.SelectedIsStorageUpgrade) { primary = UpgradeAvailable(game) ? "Upgrade" : null; return; }
            if (!IsActionableSlot(game, navigator.SelectedCollider, slot)) return;
            if (!HasItem(held))
            {
                if (HasItem(target))
                { primary = "Pick up"; secondary = Stackable(target) && target.q > 1 ? "Split" : "Pick up"; }
                return;
            }
            if (!EligibleInventorySlot(slot, held)) return;
            primary = !HasItem(target) ? "Place" : CanStack(held, target) ? "Combine" : "Swap";
            if (CanStack(held, target) && target.q >= 9999) primary = null;
            secondary = CanPlaceOne(game, slot, held, target) ? "Place one" : !HasItem(target) ? "Place" : "Swap";
        }

        private static bool ValidateRay(Collider candidate)
        {
            if (!candidate || !Camera.main) return false;
            Vector3 point = Camera.main.WorldToScreenPoint(candidate.bounds.center); RaycastHit hit;
            return point.z > 0f && Physics.Raycast(Camera.main.ScreenPointToRay(point), out hit, 7f) && hit.collider == candidate;
        }

        private static bool IsActionableSlot(GameScript game, Collider candidate, int index)
        {
            if (!InventoryOpen(game) || !candidate || !SupportedItemContext(game) ||
                InventoryItemLifecycle.MutationsBlocked(game)) return false;
            if (index >= 6 && index <= 44)
                return candidate.transform.parent == game.inventoryMain.transform;
            if (index >= 45 && index <= 50 && game.inventoryBar)
                return candidate.transform.parent == game.inventoryBar.transform && candidate.gameObject.name == (index - 45).ToString();
            if (!StorageOpen(game)) return false;
            return (index >= 51 && index <= 86 && candidate.transform.parent == game.menuStorage.transform) ||
                navigator.SelectedIsStoragePage || navigator.SelectedIsStorageUpgrade;
        }

        // Selection IDs and native inventory IDs differ only for hotkey targets.
        private static int NativeSlot(int index) { return index >= 45 && index <= 50 ? index - 45 : index; }
        private static Item ReadTarget(GameScript game, int index)
        { return ReadTargetAt(game, index, index >= 51 && index <= 86, InventoryItemLifecycle.CurrentPage(game)); }
        private static Item ReadTargetAt(GameScript game, int index, bool storage, int page)
        {
            FieldInfo field = storage ? StorageField : InventoryField;
            Item[] items = game != null && field != null ? field.GetValue(game) as Item[] : null;
            int native = storage ? index - 51 : NativeSlot(index);
            if (storage) native = native < 30 ? page * 30 + native : 360 + page * 6 + native - 30;
            if (!storage && (index < 6 || index > 50)) return null;
            return items != null && native >= 0 && native < items.Length ? items[native] : null;
        }
        // Offer the transfer action based on context, without probing native
        // crafting rules: CanPlaceCraft also creates rejection text objects.
        private static bool CanOfferTransfer(GameScript game, int index, Item item)
        {
            if (!HasItem(item)) return false;
            if (index >= 51 && index <= 86) return StorageOpen(game);
            return SupportedItemContext(game) && index >= 6 && index <= 50 && NativeSlot(index) < 45 &&
                (StorageOpen(game) || (game.menuCraft && game.menuCraft.activeInHierarchy));
        }
        private static bool SupportedItemContext(GameScript game)
        {
            foreach (FieldInfo mode in SpecialModes)
                if (mode != null && (bool)mode.GetValue(game)) return false;
            return true;
        }
        private static bool UpgradeAvailable(GameScript game)
        {
            MethodInfo credits = typeof(GameScript).GetMethod("GetCredits", Type.EmptyTypes);
            MethodInfo cost = typeof(GameScript).GetMethod("GetStorageUpgradeCost", new[] { typeof(int) });
            if (StorageLevel == null || credits == null || cost == null) return false;
            int level = (int)StorageLevel.GetValue(game);
            return level < 11 && (int)credits.Invoke(game, null) >= (int)cost.Invoke(game, new object[] { level });
        }
        private static bool EligibleInventorySlot(int selection, Item held)
        {
            if (!HasItem(held) || selection < 36 || selection > 44) return true;
            // Symbolic native ItemType masks; native methods remain the final validator.
            ItemType type = ItemRegistry.GetTypeByID(held.id) & ItemType.EQUIP_MASK;
            ItemType expected = selection == 36 ? ItemType.WEAPON : selection == 37 ? ItemType.OFFHAND :
                selection == 38 ? ItemType.HELMET : selection == 39 ? ItemType.ARMOR :
                selection <= 41 ? ItemType.RING : ItemType.DROID;
            return type == (expected & ItemType.EQUIP_MASK);
        }
        private static bool CanPlaceOne(GameScript game, int slot, Item held, Item target)
        {
            return HasItem(held) && Stackable(held) && EligibleInventorySlot(slot, held) &&
                ((slot >= 6 && slot <= 50) || (slot >= 51 && slot <= 86)) &&
                (!HasItem(target) || (CanStack(held, target) && target.q < 9999));
        }
        private static bool Stackable(Item item)
        { return HasItem(item) && (ItemRegistry.GetTypeByID(item.id) & ItemType.NONSTACKING) == 0; }
        private static bool CanStack(Item a, Item b)
        { return HasItem(a) && HasItem(b) && GadgetCoreAPI.CanItemsStack(a, b); }
        private static bool HasItem(Item item) { return item != null && item.id != 0 && item.q > 0; }
        private static Item Copy(Item item) { return item == null ? null : GadgetCoreAPI.CopyItem(item); }
        private static bool Same(Item a, Item b)
        {
            if (!HasItem(a) && !HasItem(b)) return true;
            return HasItem(a) && HasItem(b) && a.id == b.id && a.q == b.q &&
                a.exp == b.exp && a.tier == b.tier && a.corrupted == b.corrupted &&
                ArrayEqual(a.aspect,b.aspect) && ArrayEqual(a.aspectLvl,b.aspectLvl);
        }
        private static bool ArrayEqual(int[] a, int[] b)
        {
            if (ReferenceEquals(a,b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i=0;i<a.Length;i++) if(a[i]!=b[i]) return false;
            return true;
        }
        internal static void NativeError(GameScript game, int code)
        {
            if (!OwnershipActive() || !storageClick || !ReferenceEquals(game, preparedGame)) return;
            nativeErrorFrame = Time.frameCount;
            string reason = code == 3 || code == 7 ? "Not enough Credits" :
                code == 8 ? "Not enough World Fragments" : code == 9 ? "This is not your ship" :
                code == 10 ? "Not enough Scrap Metal" : code == 11 ? "Only one of this item is allowed" :
                code == 12 ? "Gear must be level 10" : code == 6 ? "Insufficient materials" :
                code == 5 ? "Emblems must have the same tier" : "Cannot place that there";
            repeating = false; repeatBlocked = true;
            Reject(game, reason);
        }

        internal static void Reject(GameScript game, string reason)
        {
            if (!OwnershipActive()) return;
            InventoryFeedback.Reject(string.IsNullOrEmpty(reason) ? "Item action unavailable" : reason);
            InventoryItemLifecycle.PlaySound(game,"Au/error");
        }
        private static void Disable(Exception error)
        {
            EndNativeUpdate(); RestoreNativeHeldVisual(); updateFailed = true; navigator.Reset();
            if (renderer != null) renderer.Hide();
            InventoryFeedback.SetContext(null, null, false, bindings, null, null, false, false);
            if (logWarning != null) logWarning("Inventory controller actions disabled: " + error.Message);
        }
        internal static void EndNativeUpdate()
        {
            storageClick = false; virtualButton = -1; clickCollider = null;
        }
        internal static Vector3 VirtualMousePosition()
        {
            if (!OwnershipActive()) return Input.mousePosition;
            Collider selected = storageClick ? clickCollider : navigator.SelectedCollider;
            if (InventoryOpen(selectionGame) && controllerOwnsSelection && selected && Camera.main &&
                !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
                return Camera.main.WorldToScreenPoint(selected.bounds.center);
            return Input.mousePosition;
        }
        internal static bool VirtualMouseButtonDown(int button)
        {
            bool active = OwnershipActive();
            return Input.GetMouseButtonDown(button) || (active && storageClick && button == virtualButton && clickCollider &&
            (button != 0 || !navigator.SelectedIsPortal || PortalMenuContext.NativeClickAllowed(preparedGame)) &&
            !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1));
        }
        internal static int ExtraRowVirtualClickSlot(GameScript game)
        {
            Prepare(game);
            return OwnershipActive() && storageClick && clickCollider && navigator.SelectedSlot >= 81 && navigator.SelectedSlot <= 86 ?
                navigator.SelectedSlot - 51 : -1;
        }
        internal static bool ClosingStorageWouldDropHeldItem(GameScript game)
        { return StorageOpen(game) && HasItem(InventoryItemLifecycle.Held(game)); }
        internal static void PlaceHeldItemCursor(ActionCursor cursor)
        {
            if (!OwnershipActive()) return;
            if (!InventoryOpen(selectionGame) || !controllerOwnsSelection) RestoreNativeHeldVisual();
            InventoryCursorPosition.Update(cursor, selectionGame, navigator.SelectedCollider, controllerOwnsSelection);
        }
        // Restoration is safe during mouse ownership; item provenance and any
        // pending drop quarantine deliberately survive this presentation handoff.
        private static bool OwnershipActive()
        {
            if (ControllerInputOwnership.UsingController) return true;
            EndNativeUpdate(); RestoreNativeHeldVisual();
            if (renderer != null) renderer.Hide();
            InventoryFeedback.ClearPresentation();
            if (!ownershipSuspended)
            {
                navigator.Reset();
                Array.Clear(armed, 0, armed.Length);
                Array.Clear(pagingArmed, 0, pagingArmed.Length);
                repeating = false; repeatBlocked = true; repeatTarget = null;
                actionFrame = nativeErrorFrame = -1;
                hoverGame = null; hoverCollider = null; hoverControllerOwned = false;
                preparedGame = null; preparedFrame = -1;
            }
            ownershipSuspended = true;
            controllerOwnsSelection = false;
            return false;
        }
        private static void HideNativeHeldVisual(GameScript game)
        {
            GameObject held = game.holdingItemObj;
            if (!held || !held.activeInHierarchy) { RestoreNativeHeldVisual(); return; }
            if (hiddenNativeHeldObject == held) return;
            RestoreNativeHeldVisual();
            if (game.holdingItemIcon)
            {
                Renderer icon = game.holdingItemIcon.GetComponent<Renderer>();
                if (icon != null)
                {
                    Vector3 size=icon.bounds.size;
                    if(size.x>0f&&size.y>0f) { nativeHeldIconWidth=size.x; nativeHeldIconHeight=size.y; }
                }
            }
            hiddenNativeHeldObject=held; hiddenNativeHeldScale=held.transform.localScale;
            held.transform.localScale=Vector3.zero;
        }
        private static void RestoreNativeHeldVisual()
        {
            if(hiddenNativeHeldObject) hiddenNativeHeldObject.transform.localScale=hiddenNativeHeldScale;
            hiddenNativeHeldObject=null;
        }
        internal static void Reset()
        {
            EndNativeUpdate(); RestoreNativeHeldVisual(); InventoryNativeCursor.Reset();
            stateProvider=null; usingControllerProvider=null; bindings=null; logWarning=null;
            preparedGame=selectionGame=null; preparedFrame=-1; Array.Clear(armed,0,armed.Length);
            Array.Clear(pagingArmed,0,pagingArmed.Length); pagingPlayer=null; pagingScene=-1;
            sampledPhysicalMouse=controllerOwnsSelection=ownershipSuspended=false; auxiliaryPane=null; portalContext=conversionModeContext=false;
            hoverGame=null; hoverCollider=null; hoverControllerOwned=false;
            nativeHeldIconWidth=nativeHeldIconHeight=0f;
            repeatTarget=null; repeating=repeatBlocked=false; actionFrame=nativeErrorFrame=-1;
            navigator.Reset(); if(renderer!=null) renderer.Dispose(); renderer=null;
            rendererIsPage=assetFailed=updateFailed=false;
            InventoryFeedback.Reset(); InventoryItemLifecycle.Reset(); PortalMenuContext.Reset();
        }
    }
}
