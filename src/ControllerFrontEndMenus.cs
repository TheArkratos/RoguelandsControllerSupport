using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // Only the three native menu Update methods see these virtual mouse reads.
    // All actual actions still run through their own raycast/click branches.
    internal static class ControllerFrontEndMenus
    {
        private const float StickThreshold = 0.45f;
        private const float FirstRepeatDelay = 0.30f;
        private const float RepeatInterval = 0.12f;
        private const float CornerFraction = 0.32f;
        private const int SortingOrder = 30001;

        private static readonly List<Collider> choices = new List<Collider>();
        private static Func<JoystickInputState> stateProvider;
        private static BindingConfig bindings;
        private static Action<string> warning;
        private static Action consumeStoryInput;
        private static SelectionCornerRenderer corners;
        private static UnityEngine.Object owner;
        private static GameObject menuRoot;
        private static Collider selected;
        private static Vector3 lastPhysicalMouse;
        private static bool sampledMouse;
        private static bool controllerOwns;
        private static bool activateArmed;
        private static bool cancelArmed;
        private static bool syntheticClick;
        private static bool failed;
        private static int heldDirection;
        private static bool navigationRequiresRelease;
        private static float nextRepeatAt;
        private static bool activeNativeUpdate;
        private static ButtonMenu nativeHovered;
        private static bool dispatchingNativeHover;
        private static BoxCollider resizedCharacterSlot;
        private static Vector3 originalCharacterSlotSize;

        internal static Collider SelectedCollider { get { return selected; } }

        internal static void Configure(Func<JoystickInputState> provider,
            BindingConfig config, Action<string> warningLogger, Action consumeStory = null)
        {
            Reset();
            stateProvider = provider;
            bindings = config;
            warning = warningLogger;
            consumeStoryInput = consumeStory;
        }

        internal static void Reset()
        {
            ClearNativeHover(false);
            RestoreCharacterHitbox();
            if (corners != null) corners.Dispose();
            corners = null;
            owner = null;
            menuRoot = null;
            selected = null;
            choices.Clear();
            stateProvider = null;
            bindings = null;
            warning = null;
            consumeStoryInput = null;
            sampledMouse = false;
            controllerOwns = false;
            activateArmed = false;
            cancelArmed = false;
            syntheticClick = false;
            failed = false;
            heldDirection = 0;
            navigationRequiresRelease = false;
            nextRepeatAt = 0f;
            activeNativeUpdate = false;
        }

        internal static void Begin(Menuu menu)
        {
            if (menu == null) return;
            GameObject root = null;
            string[] allowed = null;
            bool stuffGrid = false;
            if (Active(menu.menuDeleteChar))
            {
                root = menu.menuDeleteChar;
                allowed = new[] { "delNo", "delYes" };
            }
            else if (Active(menu.menuStuffSelect))
            {
                root = menu.menuStuffSelect;
                allowed = new[] { "bBack2", "bBack" };
                stuffGrid = true;
            }
            else if (Active(menu.menuCreateCharacter))
            {
                root = menu.menuCreateCharacter;
                allowed = new[] { "bRace", "bVariant", "bAugment", "bUniform",
                    "bFlag", "bProf", "bTrait0", "bTrait1", "bCreate",
                    "bBack2", "bBack", "enterName" };
            }
            else if (Active(menu.menuCharSelect))
            {
                root = menu.menuCharSelect;
                allowed = new[] { "bs0", "bs1", "bs2", "bs3", "bs4", "bs5",
                    "del0", "del1", "del2", "del3", "del4", "del5", "bBack" };
            }
            else if (Active(menu.menuOptions))
            {
                root = menu.menuOptions;
                allowed = new[] { "bBack", "bHigherM", "bLowerM",
                    "bHigherS", "bLowerS", "bFullscreen" };
            }
            else if (Active(menu.menuData))
            {
                // Data contains delete/reset controls. They remain mouse-only.
                root = menu.menuData;
                allowed = new[] { "bBack" };
            }
            else if (Active(menu.menuMain))
            {
                root = menu.menuMain;
                allowed = new[] { "bPlay", "bData", "bOptions", "bQuit" };
            }
            BeginMenu(menu, root, allowed, stuffGrid);
        }

        internal static void Begin(MenuScript menu)
        {
            if (menu == null || MenuScript.inGame)
            {
                activeNativeUpdate = false;
                if (owner == menu) Clear();
                return;
            }
            if (Active(menu.menuServer))
            {
                // Reuse native server/connect clicks. Removing saved entries
                // stays mouse-only; IP/port text still uses the native keyboard.
                BeginMenu(menu, menu.menuServer,
                    new[] { "bServer0", "bServer1", "bServer2", "bServer3",
                        "enterIP", "enterPort", "bAddServer", "bBackMulti" }, false);
                return;
            }
            BeginMenu(menu, Active(menu.menu) ? menu.menu : null,
                new[] { "bSingleplayer", "bMultiplayer", "bHost", "bConnect",
                    "bName", "hostPort", "bBack", "bBackMulti", "bQuit", "bQuit2" }, false);
        }

        internal static void Begin(GameScript game)
        {
            bool story = StoryOpen(game);
            // Consume before closing/committing so this frame cannot also run a
            // hotkey/attack even after the native choice hides its root.
            if (story && OwnershipActive() && consumeStoryInput != null) consumeStoryInput();
            BeginMenu(game, game != null && GameScript.pausing && Active(game.menuPause)
                    ? game.menuPause : story ? game.menuStory : null,
                story && !GameScript.pausing ? new[] { "bChoice0", "bChoice1", "bChoice2" } :
                new[] { "bResume", "bOptions", "bQuit", "bHigherM",
                    "bLowerM", "bHigherS", "bLowerS", "bFullscreen" }, false);
            if (!story || GameScript.pausing || !OwnershipActive() || !activeNativeUpdate ||
                failed || bindings == null || stateProvider == null) return;
            JoystickInputState state = stateProvider();
            if (state == null || !state.Connected || !cancelArmed ||
                !BoundPressed(state, "MenuCancel") || Input.GetMouseButtonDown(0) ||
                Input.GetMouseButtonDown(1)) return;
            // LeaveNPC dispatches these station roots before menuStory. Refuse
            // a contradictory context rather than close a different menu/drop items.
            if (Active(game.menuEmblemForge) || Active(game.menuTrasher) || Active(game.menuPrism)) return;
            cancelArmed = activateArmed = false;
            // Use native walk-away closure: no SelectChoice, story state or rewards.
            game.LeaveNPC();
            Clear();
        }

        private static bool StoryOpen(GameScript game)
        { return game != null && !GameScript.dead && Active(game.menuStory); }

        private static void BeginMenu(UnityEngine.Object nextOwner, GameObject root,
            string[] names, bool stuffGrid)
        {
            activeNativeUpdate = false;
            if (!OwnershipActive()) return;
            if (failed || stateProvider == null || bindings == null || !root ||
                Camera.main == null)
            {
                if (owner == nextOwner) Clear();
                return;
            }
            try
            {
                bool changed = owner != nextOwner || menuRoot != root;
                if (changed) { ClearNativeHover(false); RestoreCharacterHitbox(); }
                owner = nextOwner;
                menuRoot = root;
                activeNativeUpdate = true;
                Collect(root, names, stuffGrid);
                Menuu title = nextOwner as Menuu;
                if (title != null && root == title.menuCharSelect) NormalizeCharacterHitbox();
                if (changed)
                {
                    selected = null;
                    sampledMouse = false;
                    controllerOwns = false;
                    activateArmed = false;
                    cancelArmed = false;
                    heldDirection = 0;
                }
                bool deleting = title != null && root == title.menuDeleteChar;
                if (!Contains(selected)) selected = deleting ? choices.Find(c => c.gameObject.name == "delNo") :
                    choices.Count > 0 ? choices[0] : null;
                bool deleteEntry = changed && deleting;
                if (deleteEntry) selected = choices.Find(c => c.gameObject.name == "delNo");
                bool characterEntry = changed && title != null && root == title.menuCharSelect;
                GameScript storyGame = nextOwner as GameScript;
                bool storyEntry = changed && storyGame != null && root == storyGame.menuStory;
                if (storyEntry)
                    selected = choices.Find(c => c.gameObject.name == "bChoice0") ??
                        choices.Find(c => c.gameObject.name == "bChoice1") ??
                        choices.Find(c => c.gameObject.name == "bChoice2");
                if (characterEntry)
                {
                    // Native character slots are row-major: bs0/bs1 on top.
                    // Prefer bs0 on every entry, independently of scene child order.
                    for (int slot = 0; slot < 6; slot++)
                    {
                        Collider first = choices.Find(c => c.gameObject.name == "bs" + slot);
                        if (first != null) { selected = first; break; }
                    }
                }

                Vector3 mouse = Input.mousePosition;
                bool physicalClick = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                bool mouseMoved = sampledMouse &&
                    (mouse - lastPhysicalMouse).sqrMagnitude > 0.25f;
                lastPhysicalMouse = mouse;
                sampledMouse = true;
                if (changed || mouseMoved || physicalClick)
                {
                    Collider hovered = UnderPhysicalMouse(mouse);
                    if (hovered != null && (!(characterEntry || storyEntry || deleteEntry) || mouseMoved || physicalClick)) selected = hovered;
                    if (mouseMoved || physicalClick)
                    {
                        controllerOwns = false;
                        heldDirection = 0;
                    }
                }

                JoystickInputState state = stateProvider();
                if (!OwnershipActive()) return;
                bool connected = state != null && state.Connected;
                bool held = connected && BoundHeld(state, "MenuActivate");
                bool cancelHeld = connected && BoundHeld(state, "MenuCancel");
                if (changed) cancelArmed = !cancelHeld;
                else if (!cancelHeld) cancelArmed = true;
                if (!held) activateArmed = true;
                if (storyEntry && connected && !mouseMoved && !physicalClick) controllerOwns = true;
                int rawDirection = connected ? Direction(state) : 0;
                if (changed) navigationRequiresRelease = rawDirection != 0;
                if (rawDirection == 0) navigationRequiresRelease = false;
                int direction = connected && !mouseMoved && !physicalClick && !navigationRequiresRelease
                    ? rawDirection : 0;
                if (direction == 0) heldDirection = 0;
                else if (direction != heldDirection)
                {
                    heldDirection = direction;
                    Step(direction);
                    controllerOwns = true;
                    nextRepeatAt = Time.unscaledTime + FirstRepeatDelay;
                }
                else if (Time.unscaledTime >= nextRepeatAt)
                {
                    Step(direction);
                    controllerOwns = true;
                    nextRepeatAt = Time.unscaledTime + RepeatInterval;
                }

                syntheticClick = false;
                if (!changed && !mouseMoved && !physicalClick && connected && activateArmed &&
                    BoundPressed(state, "MenuActivate") && Contains(selected) &&
                    !(deleting && cancelArmed && BoundPressed(state, "MenuCancel")))
                {
                    ActivateSelected();
                }
                ShowCorners();
            }
            catch (Exception error)
            {
                failed = true;
                Clear();
                if (warning != null) warning("Front-end controller menus disabled: " + error.Message);
            }
        }

        private static void ActivateSelected()
        {
            // Native handlers retain every action, including delete confirmation.
            if (!Contains(selected) || Camera.main == null) return;
            Vector3 point = Camera.main.WorldToScreenPoint(selected.bounds.center);
            RaycastHit hit;
            if (point.z <= 0f || !Physics.Raycast(Camera.main.ScreenPointToRay(point),
                out hit, 10f) || hit.collider != selected) return;
            controllerOwns = true;
            syntheticClick = true;
            activateArmed = cancelArmed = false;
        }

        internal static void End()
        {
            activeNativeUpdate = false;
            syntheticClick = false;
        }

        // Transpiler replacements affect only the three explicitly patched Updates.
        internal static Vector3 MousePosition()
        {
            if (!OwnershipActive()) return Input.mousePosition;
            if (activeNativeUpdate && controllerOwns && Contains(selected) &&
                !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
                return Camera.main.WorldToScreenPoint(selected.bounds.center);
            return ControllerMenuSelection.VirtualMousePosition();
        }

        internal static bool MouseButtonDown(int button)
        {
            if (ControllerMenuSelection.QuestMouseInputBlocked(button)) return false;
            if (!OwnershipActive()) return Input.GetMouseButtonDown(button);
            return Input.GetMouseButtonDown(button) ||
                (button == 0 && activeNativeUpdate && syntheticClick && Contains(selected)) ||
                ControllerMenuSelection.VirtualMouseButtonDown(button);
        }

        // Cancel is a named native input shared by keyboard Escape and pad B.
        // Front-end menus own this replacement; GameScript's Cancel route stays
        // with GameplayInputPatches so inventory lifecycle handling is unchanged.
        internal static bool ButtonDown(string name)
        {
            bool native = Input.GetButtonDown(name);
            if (!OwnershipActive()) return native;
            if (name != "Cancel" || !activeNativeUpdate || failed || bindings == null || stateProvider == null)
                return native;
            JoystickInputState state = stateProvider();
            if (!OwnershipActive() || state == null || !state.Connected) return native;
            // Keep Escape even if a physical B press occurs on the same frame.
            bool keyboard = Input.GetKeyDown(KeyCode.Escape) ||
                (native && !state.ButtonHeld(1) && !state.ButtonPressed(1));
            bool cancel = cancelArmed && BoundPressed(state, "MenuCancel");
            if (cancel) cancelArmed = false;
            Menuu title = owner as Menuu;
            if (cancel && title != null && menuRoot == title.menuDeleteChar)
            {
                // Native Cancel returns to Main Menu. Route controller B through
                // the dialog's native No action so character selection stays open.
                selected = choices.Find(c => c.gameObject.name == "delNo");
                ActivateSelected();
                return keyboard;
            }
            return keyboard || cancel;
        }

        private static void Collect(GameObject root, string[] names, bool stuffGrid)
        {
            choices.Clear();
            if (root == null || names == null) return;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                string name = collider.gameObject.name;
                bool permitted = Array.IndexOf(names, name) >= 0;
                if (!permitted && stuffGrid && collider.gameObject.layer == 22)
                {
                    int number;
                    permitted = int.TryParse(name, NumberStyles.None,
                        CultureInfo.InvariantCulture, out number) && number >= 0 && number < 36;
                }
                if (permitted) choices.Add(collider);
            }
        }

        private static void NormalizeCharacterHitbox()
        {
            BoxCollider target = choices.Find(c => c.gameObject.name == "bs5" &&
                c is BoxCollider) as BoxCollider;
            if (!target) return;
            BoxCollider peer = null;
            for (int slot = 0; slot < 5 && !peer; slot++)
            {
                string name = "bs" + slot.ToString(CultureInfo.InvariantCulture);
                peer = choices.Find(c => c.gameObject.name == name && c is BoxCollider &&
                    ValidCharacterSize(((BoxCollider)c).size)) as BoxCollider;
            }
            if (!peer) return;
            // The native sixth card has a larger collider than the other five.
            // Change the real click area so both mouse hits and corner bounds agree.
            if (resizedCharacterSlot != target)
            {
                RestoreCharacterHitbox();
                resizedCharacterSlot = target;
                originalCharacterSlotSize = target.size;
            }
            Vector3 size = target.size;
            target.size = new Vector3(peer.size.x, peer.size.y, size.z);
        }

        private static bool ValidCharacterSize(Vector3 size)
        {
            return size.x > 0f && !float.IsInfinity(size.x) &&
                size.y > 0f && !float.IsInfinity(size.y);
        }

        private static void RestoreCharacterHitbox()
        {
            if (resizedCharacterSlot) resizedCharacterSlot.size = originalCharacterSlotSize;
            resizedCharacterSlot = null;
        }

        private static Collider UnderPhysicalMouse(Vector3 mouse)
        {
            Camera camera = Camera.main;
            if (!camera) return null;
            RaycastHit hit;
            if (!Physics.Raycast(camera.ScreenPointToRay(mouse), out hit, 10f)) return null;
            return Contains(hit.collider) ? hit.collider : null;
        }

        private static bool Contains(Collider collider)
        {
            return collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                choices.Contains(collider);
        }

        private static int Direction(JoystickInputState state)
        {
            // The raw pad snapshot already latches the first D-pad direction
            // through rolling diagonals until neutral. Feed that same direction
            // into the shared repeat/neighbor path, never an activation click.
            if (state.DPadHeld("DPadUp")) return -2;
            if (state.DPadHeld("DPadRight")) return 1;
            if (state.DPadHeld("DPadDown")) return 2;
            if (state.DPadHeld("DPadLeft")) return -1;
            // Front-end menus do not move the player. Either stick can steer
            // their shared selection; right wins if both are outside deadzone.
            // Resolve to one direction before repeat handling so two sticks
            // can never advance the highlight twice in the same update.
            int right = StickDirection(state.AimX, state.AimY);
            return right != 0 ? right : StickDirection(state.MoveX, state.MoveY);
        }

        private static int StickDirection(float x, float y)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            if (ax < StickThreshold && ay < StickThreshold) return 0;
            return ax >= ay ? (x < 0f ? -1 : 1) : (y < 0f ? -2 : 2);
        }

        private static void Step(int direction)
        {
            if (!Contains(selected))
            {
                Menuu dialog = owner as Menuu;
                selected = dialog != null && menuRoot == dialog.menuDeleteChar ?
                    choices.Find(c => c.gameObject.name == "delNo") : choices.Count > 0 ? choices[0] : null;
                return;
            }
            // These two title screens have intentional logical rows. Collider
            // spacing (and therefore the generic nearest-neighbor score) does
            // not reliably represent their navigation order.
            Menuu title = owner as Menuu;
            if (title != null && menuRoot == title.menuMain && StepMainMenu(direction)) return;
            if (title != null && menuRoot == title.menuCharSelect &&
                StepCharacterSelect(direction)) return;

            Vector3 from = selected.bounds.center;
            Collider best = null;
            float bestScore = float.MaxValue;
            foreach (Collider candidate in choices)
            {
                if (candidate == selected) continue;
                Vector3 delta = candidate.bounds.center - from;
                float primary = direction == -1 ? -delta.x : direction == 1 ? delta.x :
                    direction == -2 ? delta.y : -delta.y;
                float lateral = Math.Abs(direction) == 1 ? Mathf.Abs(delta.y) : Mathf.Abs(delta.x);
                if (primary <= 0.05f || lateral > primary * 2.5f) continue;
                float score = primary + lateral * 2f;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            if (best != null) selected = best;
        }

        private static bool StepMainMenu(int direction)
        {
            string from = selected.gameObject.name;
            string target = null;
            if (direction == 1 && (from == "bData" || from == "bPlay"))
                target = "bOptions";
            else if (direction == -2 && from == "bQuit")
                target = "bData";
            else if (direction == -2 && from == "bOptions")
                target = "bPlay";
            if (target == null) return false;
            SelectNamed(target);
            return true;
        }

        private static bool StepCharacterSelect(int direction)
        {
            string from = selected.gameObject.name;
            if (from == "bBack")
            {
                if (direction == -2) SelectNamed("bs4");
                return true;
            }
            if (from.Length == 4 && from.StartsWith("del", StringComparison.Ordinal) &&
                from[3] >= '0' && from[3] <= '5')
            {
                int deletion = from[3] - '0';
                if (direction == (deletion % 2 == 0 ? 1 : -1)) SelectNamed("bs" + deletion);
                else if (direction == -2 && deletion >= 2) SelectNamed("del" + (deletion - 2));
                else if (direction == 2 && deletion < 4) SelectNamed("del" + (deletion + 2));
                else if (direction == 2) SelectNamed("bBack");
                return true;
            }
            if (from.Length != 3 || from[0] != 'b' || from[1] != 's' ||
                from[2] < '0' || from[2] > '5') return false;

            int slot = from[2] - '0';
            if (direction == (slot % 2 == 0 ? -1 : 1))
            {
                // Delete buttons sit on the outside edge of their own card.
                // Empty character slots hide their native delete control.
                SelectNamed("del" + slot);
                return true;
            }
            int target = -1;
            if (direction == -1 && slot % 2 == 1) target = slot - 1;
            else if (direction == 1 && slot % 2 == 0) target = slot + 1;
            else if (direction == -2 && slot >= 2) target = slot - 2;
            else if (direction == 2 && slot < 4) target = slot + 2;
            else if (direction == 2 && slot >= 4)
            {
                SelectNamed("bBack");
                return true;
            }
            if (target >= 0) SelectNamed("bs" + target.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private static void SelectNamed(string name)
        {
            foreach (Collider candidate in choices)
            {
                if (Contains(candidate) && candidate.gameObject.name == name)
                {
                    selected = candidate;
                    return;
                }
            }
        }

        private static bool BoundHeld(JoystickInputState state, string action)
        {
            string token = bindings[action];
            if (token == "LeftTrigger") return state.LeftTriggerHeld;
            if (token == "RightTrigger") return state.RightTriggerHeld;
            int number;
            return ButtonNumber(token, out number) && state.ButtonHeld(number);
        }

        private static bool BoundPressed(JoystickInputState state, string action)
        {
            string token = bindings[action];
            if (token == "LeftTrigger") return state.LeftTriggerPressed;
            if (token == "RightTrigger") return state.RightTriggerPressed;
            int number;
            return ButtonNumber(token, out number) && state.ButtonPressed(number);
        }

        private static bool ButtonNumber(string token, out int number)
        {
            const string prefix = "Joystick1Button";
            number = -1;
            return token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(token.Substring(prefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out number);
        }

        private static bool NativeHighlightScreen()
        {
            Menuu title = owner as Menuu;
            if (title != null) return menuRoot == title.menuMain || menuRoot == title.menuCharSelect || menuRoot == title.menuDeleteChar;
            GameScript game = owner as GameScript;
            if (game != null) return menuRoot == game.menuStory ||
                (GameScript.pausing && menuRoot == game.menuPause);
            MenuScript modes = owner as MenuScript;
            return modes != null && menuRoot == modes.menu;
        }

        private static ButtonMenu NativeHighlight(Collider target)
        {
            if (!target) return null;
            ButtonMenu button = target.gameObject.GetComponent<ButtonMenu>();
            return button && !button.isPlanet && !button.storageButton && button.buttonSelect ? button : null;
        }

        private static void DispatchNativeHover(ButtonMenu button, bool enter)
        {
            if (!button) return;
            bool previous = dispatchingNativeHover;
            dispatchingNativeHover = true;
            try { if (enter) button.OnMouseEnter(); else button.OnMouseExit(); }
            finally { dispatchingNativeHover = previous; }
        }

        private static void ClearNativeHover(bool restorePhysicalMouse)
        {
            ButtonMenu previous = nativeHovered;
            nativeHovered = null;
            if (!previous) return;
            DispatchNativeHover(previous, false);
            // Unity may still consider the stationary physical pointer inside a
            // button: replay its own enter once when virtual hover relinquishes.
            if (restorePhysicalMouse && Camera.main)
                DispatchNativeHover(NativeHighlight(UnderPhysicalMouse(Input.mousePosition)), true);
        }

        internal static bool NativeHoverAllowed(ButtonMenu button)
        {
            if (dispatchingNativeHover) return true;
            if (!ControllerInputOwnership.UsingController)
            {
                // This Unity callback itself restores physical hover. Do not
                // replay another enter before the original callback executes.
                activeNativeUpdate = false;
                Clear();
                return true;
            }
            if (!NativeHighlightScreen() || !button) return true;
            foreach (Collider choice in choices)
                if (choice && choice.gameObject == button.gameObject && NativeHighlight(choice)) return false;
            return true;
        }

        private static void ShowCorners()
        {
            ButtonMenu next = NativeHighlightScreen() && Contains(selected) ? NativeHighlight(selected) : null;
            if (next != nativeHovered)
            {
                ClearNativeHover(false);
                // A physical hover may predate controller ownership. Retire it
                // before showing the selected button, including bs0 defaults.
                if (next && Camera.main)
                {
                    ButtonMenu physical = NativeHighlight(UnderPhysicalMouse(Input.mousePosition));
                    if (physical != next) DispatchNativeHover(physical, false);
                }
                nativeHovered = next;
                DispatchNativeHover(next, true);
            }
            if (next) { if (corners != null) corners.Hide(); return; }
            if (!Contains(selected)) { if (corners != null) corners.Hide(); return; }
            if (corners != null && !corners.IsAlive)
            {
                corners.Dispose();
                corners = null;
            }
            if (corners == null)
            {
                float side = Mathf.Min(selected.bounds.size.x, selected.bounds.size.y);
                if (side <= 0f) return;
                corners = SelectionCornerRenderer.TryCreate(6f / (side * CornerFraction),
                    SortingOrder, warning);
            }
            if (corners != null) corners.Show(selected);
        }

        private static bool Active(GameObject gameObject)
        {
            return gameObject && gameObject.activeInHierarchy;
        }

        private static bool OwnershipActive()
        {
            if (ControllerInputOwnership.UsingController) return true;
            activeNativeUpdate = false;
            ClearNativeHover(true);
            Clear();
            return false;
        }

        private static void Clear()
        {
            ClearNativeHover(false);
            RestoreCharacterHitbox();
            owner = null;
            menuRoot = null;
            selected = null;
            choices.Clear();
            controllerOwns = false;
            syntheticClick = false;
            activateArmed = false;
            cancelArmed = false;
            heldDirection = 0;
            navigationRequiresRelease = false;
            if (corners != null) corners.Hide();
        }
    }

    // Unity mouse callbacks do not use the virtual raycast adapter. Let the
    // selected native button own hover while controller input owns these menus.
    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class FrontEndNativeHoverPatch
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            yield return typeof(ButtonMenu).GetMethod("OnMouseEnter");
            yield return typeof(ButtonMenu).GetMethod("OnMouseExit");
        }

        [HarmonyPrefix]
        internal static bool Prefix(ButtonMenu __instance)
        { return ControllerFrontEndMenus.NativeHoverAllowed(__instance); }
    }

    internal static class FrontEndMenuTranspiler
    {
        internal static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions,
            string target)
        {
            List<CodeInstruction> original = new List<CodeInstruction>(instructions);
            MethodInfo mouse = typeof(Input).GetProperty("mousePosition").GetGetMethod();
            MethodInfo click = typeof(Input).GetMethod("GetMouseButtonDown", new[] { typeof(int) });
            MethodInfo buttonDown = typeof(Input).GetMethod("GetButtonDown", new[] { typeof(string) });
            MethodInfo virtualButtonDown = typeof(ControllerFrontEndMenus).GetMethod("ButtonDown",
                BindingFlags.Static | BindingFlags.NonPublic);
            // GameScript.Update has a separate Cancel transpiler and item-return
            // policy. Only title and ship-choice menus use this named-input route.
            bool replaceButtons = target == "Menuu.Update" || target == "MenuScript.Update";
            MethodInfo virtualMouse = typeof(ControllerFrontEndMenus).GetMethod("MousePosition",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo virtualClick = typeof(ControllerFrontEndMenus).GetMethod("MouseButtonDown",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (mouse == null || click == null || virtualMouse == null || virtualClick == null)
            {
                Debug.LogWarning("Controller Support: menu input lookup failed for " + target);
                return original;
            }
            int mouseCount = 0, clickCount = 0;
            foreach (CodeInstruction instruction in original)
            {
                if (Equals(instruction.operand, mouse)) mouseCount++;
                if (Equals(instruction.operand, click)) clickCount++;
            }
            if (mouseCount < 1 || clickCount < 1)
            {
                Debug.LogWarning("Controller Support: menu input pattern missing in " + target +
                    "; leaving native IL unchanged.");
                return original;
            }
            List<CodeInstruction> patched = new List<CodeInstruction>(original.Count);
            foreach (CodeInstruction instruction in original)
            {
                CodeInstruction replacement = new CodeInstruction(instruction);
                if (Equals(instruction.operand, mouse)) replacement.operand = virtualMouse;
                else if (Equals(instruction.operand, click)) replacement.operand = virtualClick;
                else if (replaceButtons && buttonDown != null && virtualButtonDown != null &&
                    Equals(instruction.operand, buttonDown)) replacement.operand = virtualButtonDown;
                patched.Add(replacement);
            }
            Debug.Log("Controller Support: patched " + mouseCount + " menu mouse positions and " +
                clickCount + " click reads in " + target + ".");
            return patched;
        }
    }

    [HarmonyPatch(typeof(Menuu), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class TitleControllerMenuPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(Menuu __instance) { ControllerFrontEndMenus.Begin(__instance); }
        [HarmonyPostfix]
        internal static void Postfix() { ControllerFrontEndMenus.End(); }
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return FrontEndMenuTranspiler.Replace(instructions, "Menuu.Update"); }
    }

    [HarmonyPatch(typeof(MenuScript), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class ShipChoiceControllerMenuPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(MenuScript __instance) { ControllerFrontEndMenus.Begin(__instance); }
        [HarmonyPostfix]
        internal static void Postfix() { ControllerFrontEndMenus.End(); }
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return FrontEndMenuTranspiler.Replace(instructions, "MenuScript.Update"); }
    }

    [HarmonyPatch(typeof(GameScript), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class PauseControllerMenuPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(GameScript __instance) { ControllerFrontEndMenus.Begin(__instance); }
        [HarmonyPostfix]
        internal static void Postfix() { ControllerFrontEndMenus.End(); }
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return FrontEndMenuTranspiler.Replace(instructions, "GameScript.Update"); }
    }
}
