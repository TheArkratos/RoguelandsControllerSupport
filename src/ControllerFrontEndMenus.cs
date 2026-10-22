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
        private static SelectionCornerRenderer corners;
        private static UnityEngine.Object owner;
        private static GameObject menuRoot;
        private static Collider selected;
        private static Vector3 lastPhysicalMouse;
        private static bool sampledMouse;
        private static bool controllerOwns;
        private static bool activateArmed;
        private static bool syntheticClick;
        private static bool failed;
        private static int heldDirection;
        private static float nextRepeatAt;
        private static bool activeNativeUpdate;

        internal static Collider SelectedCollider { get { return selected; } }

        internal static void Configure(Func<JoystickInputState> provider,
            BindingConfig config, Action<string> warningLogger)
        {
            Reset();
            stateProvider = provider;
            bindings = config;
            warning = warningLogger;
        }

        internal static void Reset()
        {
            if (corners != null) corners.Dispose();
            corners = null;
            owner = null;
            menuRoot = null;
            selected = null;
            choices.Clear();
            stateProvider = null;
            bindings = null;
            warning = null;
            sampledMouse = false;
            controllerOwns = false;
            activateArmed = false;
            syntheticClick = false;
            failed = false;
            heldDirection = 0;
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
                allowed = new[] { "delNo" };
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
                allowed = new[] { "bs0", "bs1", "bs2", "bs3", "bs4", "bs5", "bBack" };
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
            // The server-list/connect page is intentionally not in this slice.
            if (menu == null || MenuScript.inGame || Active(menu.menuServer))
            {
                activeNativeUpdate = false;
                if (owner == menu) Clear();
                return;
            }
            BeginMenu(menu, Active(menu.menu) ? menu.menu : null,
                new[] { "bSingleplayer", "bMultiplayer", "bHost",
                    "bBack", "bBackMulti", "bQuit", "bQuit2" }, false);
        }

        internal static void Begin(GameScript game)
        {
            BeginMenu(game, game != null && GameScript.pausing && Active(game.menuPause)
                    ? game.menuPause : null,
                new[] { "bResume", "bOptions", "bQuit", "bHigherM",
                    "bLowerM", "bHigherS", "bLowerS", "bFullscreen" }, false);
        }

        private static void BeginMenu(UnityEngine.Object nextOwner, GameObject root,
            string[] names, bool stuffGrid)
        {
            activeNativeUpdate = false;
            if (failed || stateProvider == null || bindings == null || !root ||
                Camera.main == null)
            {
                if (owner == nextOwner) Clear();
                return;
            }
            try
            {
                bool changed = owner != nextOwner || menuRoot != root;
                owner = nextOwner;
                menuRoot = root;
                activeNativeUpdate = true;
                Collect(root, names, stuffGrid);
                if (changed)
                {
                    selected = null;
                    sampledMouse = false;
                    controllerOwns = false;
                    activateArmed = false;
                    heldDirection = 0;
                }
                if (!Contains(selected)) selected = choices.Count > 0 ? choices[0] : null;

                Vector3 mouse = Input.mousePosition;
                bool physicalClick = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                bool mouseMoved = sampledMouse &&
                    (mouse - lastPhysicalMouse).sqrMagnitude > 0.25f;
                lastPhysicalMouse = mouse;
                sampledMouse = true;
                if (changed || mouseMoved || physicalClick)
                {
                    Collider hovered = UnderPhysicalMouse(mouse);
                    if (hovered != null) selected = hovered;
                    if (mouseMoved || physicalClick)
                    {
                        controllerOwns = false;
                        heldDirection = 0;
                    }
                }

                JoystickInputState state = stateProvider();
                bool connected = state != null && state.Connected;
                bool held = connected && BoundHeld(state, "MenuActivate");
                if (!held) activateArmed = true;
                int direction = connected && !mouseMoved && !physicalClick
                    ? Direction(state) : 0;
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
                if (!changed && !physicalClick && connected && activateArmed &&
                    BoundPressed(state, "MenuActivate") && Contains(selected))
                {
                    // Validate the virtual ray before allowing a native click.
                    Vector3 point = Camera.main.WorldToScreenPoint(selected.bounds.center);
                    RaycastHit hit;
                    if (point.z > 0f && Physics.Raycast(Camera.main.ScreenPointToRay(point),
                        out hit, 10f) && hit.collider == selected)
                    {
                        controllerOwns = true;
                        syntheticClick = true;
                        activateArmed = false;
                    }
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

        internal static void End()
        {
            activeNativeUpdate = false;
            syntheticClick = false;
        }

        // Transpiler replacements affect only the three explicitly patched Updates.
        internal static Vector3 MousePosition()
        {
            if (activeNativeUpdate && controllerOwns && Contains(selected) &&
                !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
                return Camera.main.WorldToScreenPoint(selected.bounds.center);
            return ControllerMenuSelection.VirtualMousePosition();
        }

        internal static bool MouseButtonDown(int button)
        {
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
            if (name != "Cancel" || !activeNativeUpdate || failed || bindings == null || stateProvider == null)
                return native;
            JoystickInputState state = stateProvider();
            if (state == null || !state.Connected) return native;
            // Keep Escape even if a physical B press occurs on the same frame.
            bool keyboard = Input.GetKeyDown(KeyCode.Escape) ||
                (native && !state.ButtonHeld(1) && !state.ButtonPressed(1));
            return keyboard || BoundPressed(state, "MenuCancel");
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
            if (!Contains(selected)) { selected = choices.Count > 0 ? choices[0] : null; return; }
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
            if (from.Length != 3 || from[0] != 'b' || from[1] != 's' ||
                from[2] < '0' || from[2] > '5') return false;

            int slot = from[2] - '0';
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

        private static void ShowCorners()
        {
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

        private static void Clear()
        {
            owner = null;
            menuRoot = null;
            selected = null;
            choices.Clear();
            controllerOwns = false;
            syntheticClick = false;
            activateArmed = false;
            heldDirection = 0;
            if (corners != null) corners.Hide();
        }
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
