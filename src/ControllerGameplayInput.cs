using System;
using System.Reflection;
using UnityEngine;

namespace ControllerSupport
{
    // Adapter for the existing game input reads. This never changes the game's
    // movement, energy, cooldown, network, or combat-mode implementations.
    internal static class ControllerGameplayInput
    {
        private const float DirectionThreshold = 0.5f;
        private static Func<JoystickInputState> stateProvider;
        private static BindingConfig bindings;
        private static int frame = -1;
        private static int inventoryInputFrame = -1;
        private static JoystickInputState state;
        private static bool left, right, up, down;
        private static bool leftPressed, rightPressed, downPressed;
        private static bool leftReleased, rightReleased, upReleased, downReleased;
        private static bool attackWasInCombat;
        private static bool attackRequiresRelease = true;
        private static readonly FieldInfo CanInteractField = typeof(PlayerScript).GetField("canInteract",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo InteractingField = typeof(PlayerScript).GetField("interacting",
            BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Configure(Func<JoystickInputState> provider, BindingConfig config)
        {
            stateProvider = provider;
            bindings = config;
            ResetFrame();
        }

        internal static void Reset()
        {
            stateProvider = null;
            bindings = null;
            ResetFrame();
        }

        private static void ResetFrame()
        {
            frame = -1;
            inventoryInputFrame = -1;
            state = null;
            left = right = up = down = false;
            leftPressed = rightPressed = downPressed = false;
            leftReleased = rightReleased = upReleased = downReleased = false;
            attackWasInCombat = false;
            attackRequiresRelease = true;
        }

        private static bool Refresh()
        {
            if (frame == Time.frameCount) return state != null && state.Connected;
            frame = Time.frameCount;
            JoystickInputState next = stateProvider == null ? null : stateProvider();
            if (next == null || !next.Connected)
            {
                ResetDirections();
                state = null;
                attackRequiresRelease = true;
                attackWasInCombat = false;
                return false;
            }
            state = next;
            bool wasLeft = left, wasRight = right, wasUp = up, wasDown = down;
            left = next.MoveX < -DirectionThreshold;
            right = next.MoveX > DirectionThreshold;
            // Linux joystick Y grows downward; no Up Down press is synthesized
            // from the stick, so up never triggers NPC interaction.
            up = next.MoveY < -DirectionThreshold;
            down = next.MoveY > DirectionThreshold;
            leftPressed = left && !wasLeft;
            rightPressed = right && !wasRight;
            downPressed = down && !wasDown;
            leftReleased = !left && wasLeft;
            rightReleased = !right && wasRight;
            upReleased = !up && wasUp;
            downReleased = !down && wasDown;

            bool inCombat = SafeActions() && GameScript.combatMode;
            if (inCombat != attackWasInCombat)
            {
                attackRequiresRelease = true;
                attackWasInCombat = inCombat;
            }
            if (!BoundHeld("PrimaryAction")) attackRequiresRelease = false;
            return true;
        }

        private static void ResetDirections()
        {
            left = right = up = down = false;
            leftPressed = rightPressed = downPressed = false;
            leftReleased = rightReleased = upReleased = downReleased = false;
        }

        private static bool SafeActions()
        {
            return !GameScript.pausing && !GameScript.dead && !GameScript.buildMode &&
                   !GameScript.inventoryOpen && !GameScript.talking && inventoryInputFrame != Time.frameCount;
        }

        private static bool BoundHeld(string action) { return Bound(action, 0); }
        private static bool BoundPressed(string action) { return Bound(action, 1); }
        private static bool PhysicalButtonActive(int button)
        {
            return state != null && (state.ButtonHeld(button) || state.ButtonPressed(button));
        }
        internal static void SetGameUpdateContext(GameScript game)
        {
            // Retained for patch/test compatibility; UI lifecycle owns closure.
        }

        internal static void ConsumeInventoryInput()
        {
            inventoryInputFrame = Time.frameCount;
            ControllerGameplayActions.ConsumeForFrame();
        }

        private static bool InventoryOwnsInput()
        {
            return InventoryActive() || inventoryInputFrame == Time.frameCount;
        }

        private static bool InventoryActive()
        {
            return bindings != null && GameScript.inventoryOpen && !GameScript.pausing &&
                !GameScript.dead && !GameScript.buildMode;
        }

        private static bool MenuInputHeld()
        {
            return InventoryOwnsInput() && (BoundHeld("MenuActivate") || BoundHeld("MenuSecondary") ||
                BoundHeld("MenuTransfer") || BoundHeld("MenuDrop") || BoundHeld("MenuCancel") ||
                BoundHeld("Inventory") || BoundHeld("Pause"));
        }

        private static bool Bound(string action, int kind)
        {
            return bindings != null && (kind == 1 ? bindings.Pressed(action, state) : bindings.Held(action, state));
        }

        // These signatures deliberately mirror the Unity calls replaced by
        // GameplayInputPatches. Vanilla input is always ORed in.
        internal static bool PlayerButton(string name)
        {
            bool vanilla = Input.GetButton(name);
            if (!Refresh()) return vanilla;
            switch (name)
            {
                case "Left": return vanilla || left;
                case "Right": return vanilla || right;
                // On the tested Xbox pad, Y is also vanilla Up. Suppress that
                // joystick contribution so toggling combat cannot interact.
                case "Up": return (vanilla && !state.ButtonHeld(2) &&
                    !state.ButtonHeld(3) && !MenuInputHeld()) || up;
                case "Down": return vanilla || down;
                default: return vanilla;
            }
        }

        internal static bool PlayerButtonDown(string name)
        {
            bool vanilla = Input.GetButtonDown(name);
            if (!Refresh()) return vanilla;
            switch (name)
            {
                case "Left": return vanilla || leftPressed;
                case "Right": return vanilla || rightPressed;
                case "Down": return vanilla || downPressed;
                // The game's legacy pad mapping has Y=Up and A=Jump.
                // Keep keyboard input unless its key is pressed in the same
                // frame or held period as the conflicting pad button (an unavoidable legacy
                // InputManager ambiguity, confined to these events).
                case "Up": return vanilla && !state.ButtonHeld(2) && !state.ButtonHeld(3) && !MenuInputHeld();
                case "Jump": return (vanilla && !PhysicalButtonActive(0) && !MenuInputHeld()) ||
                    (SafeActions() && BoundPressed("Jump"));
                // Vanilla also maps LB/RB to separate dash directions.
                case "Dash Left": return (vanilla && !state.ButtonHeld(4) && !MenuInputHeld()) ||
                    (SafeActions() && left && BoundPressed("Dash"));
                case "Dash Right": return (vanilla && !state.ButtonHeld(5) && !MenuInputHeld()) ||
                    (SafeActions() && right && BoundPressed("Dash"));
                default: return vanilla;
            }
        }

        // This is the single Up edge in PlayerScript.Update. X must not
        // trigger the augment's Up-edge branch when no NPC is in range.
        internal static bool PlayerInteractionButtonDown(string name, PlayerScript player)
        {
            bool vanilla = Input.GetButtonDown(name);
            if (!Refresh()) return vanilla;
            bool xHeld = state.ButtonHeld(2);
            bool yHeld = state.ButtonHeld(3);
            bool local = player != null && MenuScript.player == player.gameObject;
            NetworkView view = local ? player.GetComponent<NetworkView>() : null;
            bool canInteract = local && view && view.isMine && CanInteractField != null &&
                InteractingField != null && (bool)CanInteractField.GetValue(player) &&
                !(bool)InteractingField.GetValue(player);
            return (vanilla && !xHeld && !yHeld && !MenuInputHeld()) ||
                (canInteract && !GameScript.dead && !GameScript.pausing &&
                 !GameScript.talking && !InventoryOwnsInput() && BoundPressed("Interact"));
        }

        internal static bool PlayerButtonUp(string name)
        {
            bool vanilla = Input.GetButtonUp(name);
            if (!Refresh()) return vanilla;
            switch (name)
            {
                case "Left": return vanilla || leftReleased;
                case "Right": return vanilla || rightReleased;
                case "Up": return vanilla || upReleased;
                case "Down": return vanilla || downReleased;
                default: return vanilla;
            }
        }

        internal static float PlayerAxis(string name)
        {
            float vanilla = Input.GetAxis(name);
            if (name == "Horizontal" && Refresh() && Math.Abs(state.MoveX) > DirectionThreshold)
                return state.MoveX;
            if (name == "Attack" && Refresh() && MenuInputHeld()) return 0f;
            if (name != "Attack" || !Refresh() || !SafeActions() || !GameScript.combatMode ||
                attackRequiresRelease || !BoundHeld("PrimaryAction")) return vanilla;
            // Existing PlayerScript.Update tests Attack < 0 and starts its
            // ordinary Attack coroutine only when its own gates allow it.
            return Math.Min(vanilla, -1f);
        }

        internal static bool GameButtonDown(string name)
        {
            bool vanilla = Input.GetButtonDown(name);
            if (!Refresh()) return vanilla;
            if (name != null && name.Length == 8 && name.StartsWith("UseItem", StringComparison.Ordinal) &&
                name[7] >= '1' && name[7] <= '6')
                return vanilla || ControllerGameplayActions.HotkeyPressed(name[7] - '1');
            if (name == "Cancel")
            {
                // B is hotkey 6 in gameplay. UI cancellation is handled by the
                // owning menu; keyboard Escape retains the native Cancel route.
                return vanilla && !PhysicalButtonActive(1) && !MenuInputHeld();
            }
            if (name == "Inventory")
            {
                // Native Menu is Inventory regardless of its configured use.
                // Inventory actions are consumed once by the active UI.
                return (vanilla && !state.ButtonHeld(7) && !MenuInputHeld()) ||
                    (!InventoryOwnsInput() && BoundPressed("Inventory"));
            }
            if (name == "CombatMode")
            {
                bool keyboard = vanilla && !(InventoryOwnsInput() &&
                    (state.ButtonHeld(3) || MenuInputHeld()));
                // Build Mode shares this input with tile destruction.
                return keyboard || (SafeActions() && MenuScript.player && BoundPressed("CombatMode"));
            }
            return vanilla;
        }

        internal static bool GamePausePressed()
        {
            return Refresh() && !GameScript.inventoryOpen && !InventoryOwnsInput() && MenuScript.player && !GameScript.dead &&
                   !PlayerScript.beaming && BoundPressed("Pause");
        }

        internal static bool ExplorationPrimaryHeld()
        {
            return Refresh() && !InventoryOwnsInput() && BoundHeld("PrimaryAction");
        }

        internal static bool ExplorationPrimaryPressed()
        {
            return Refresh() && !InventoryOwnsInput() && BoundPressed("PrimaryAction");
        }

        internal static bool ExplorationContextSwitchPressed()
        {
            if (!Refresh()) return false;
            return BoundPressed("CombatMode") || BoundPressed("Inventory") ||
                BoundPressed("Pause") || Input.GetButtonDown("CombatMode") ||
                Input.GetButtonDown("Inventory");
        }

        internal static bool GameDroidButtonDown(string name, GameScript game)
        {
            bool vanilla = Input.GetButtonDown(name);
            return vanilla && !(Refresh() && InventoryOwnsInput() &&
                (state.ButtonHeld(5) || MenuInputHeld())) &&
                !ControllerExploration.SuppressControllerDroidButton(game);
        }
    }
}
