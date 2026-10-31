using System;
using System.Collections.Generic;
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
        private static int actionInputFrame = -1;
        private static readonly HashSet<string> consumedActions = new HashSet<string>();
        private static JoystickInputState state;
        private static bool left, right, up, down;
        private static bool leftPressed, rightPressed, downPressed;
        private static bool leftReleased, rightReleased, upReleased, downReleased;
        private static bool attackWasInCombat;
        private static bool owned;
        private static int ownershipEntryFrame = -1;
        private static bool attackRequiresRelease = true;
        private static bool jumpRequiresRelease = true;
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
            owned = false; ownershipEntryFrame = -1;
            inventoryInputFrame = actionInputFrame = -1;
            consumedActions.Clear();
            state = null;
            left = right = up = down = false;
            leftPressed = rightPressed = downPressed = false;
            leftReleased = rightReleased = upReleased = downReleased = false;
            attackWasInCombat = false;
            attackRequiresRelease = jumpRequiresRelease = true;
        }

        private static bool Refresh()
        {
            // Ownership is live, even after another adapter cached this frame.
            if (!ControllerInputOwnership.UsingController)
            {
                ResetFrame();
                return false;
            }
            if (!owned)
            {
                owned = true;
                ownershipEntryFrame = Time.frameCount;
                attackRequiresRelease = jumpRequiresRelease = true;
            }
            if (frame == Time.frameCount) return state != null && state.Connected;
            frame = Time.frameCount;
            JoystickInputState next = stateProvider == null ? null : stateProvider();
            if (next == null || !next.Connected)
            {
                ResetDirections();
                state = null;
                attackRequiresRelease = jumpRequiresRelease = true;
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
            // Logical UI consumption must not masquerade as a released trigger.
            if (bindings == null || !bindings.Held("PrimaryAction", state)) attackRequiresRelease = false;
            // Unity's legacy A=Jump edge and Linux joystick events can arrive in
            // different frames. A menu owns jump independently of raw button-held
            // state, and closing it cannot turn a confirm into a gameplay press.
            // Test physical state directly: consumed bindings are not releases.
            if (InventoryOwnsInput() || GameScript.pausing) jumpRequiresRelease = true;
            else if ((bindings == null || !bindings.Held("Jump", state)) &&
                !PhysicalButtonActive(0) && !Input.GetButtonDown("Jump")) jumpRequiresRelease = false;
            return true;
        }

        private static void ResetDirections()
        {
            left = right = up = down = false;
            leftPressed = rightPressed = downPressed = false;
            leftReleased = rightReleased = upReleased = downReleased = false;
        }

        // Native PlayerScript allows jump, dash and attack during dialogue;
        // it retains its own immobile/beaming/cooldown restrictions. Combat
        // switching and NPC interaction have separate native talking guards.
        private static bool SafeActions()
        {
            return !ControllerInputOwnership.GameplayBlocked && !GameScript.pausing && !GameScript.dead && !GameScript.buildMode &&
                   !GameScript.inventoryOpen && inventoryInputFrame != Time.frameCount;
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
            if (!ControllerInputOwnership.UsingController) { ResetFrame(); return; }
            inventoryInputFrame = Time.frameCount;
            attackRequiresRelease = jumpRequiresRelease = true;
            if (ControllerInputOwnership.GameplayBlocked) attackWasInCombat = false;
            ControllerGameplayActions.ConsumeForFrame();
        }

        // Dialogue owns its configured Interact binding, not the inventory or
        // every gameplay input. Shared bindings must not activate twice.
        internal static void ConsumeActionForFrame(string action)
        {
            if (!ControllerInputOwnership.UsingController) { ResetFrame(); return; }
            if (actionInputFrame != Time.frameCount) consumedActions.Clear();
            actionInputFrame = Time.frameCount;
            consumedActions.Add(action);
        }

        internal static bool IsActionConsumed(string action)
        {
            if (!ControllerInputOwnership.UsingController)
            {
                ResetFrame();
                return false;
            }
            return bindings != null && TokenConsumed(bindings[action]);
        }

        internal static bool PhysicalButtonConsumed(int button)
        {
            return ControllerInputOwnership.UsingController && TokenConsumed("Joystick1Button" + button) &&
                Refresh() && PhysicalButtonActive(button);
        }

        private static bool TokenConsumed(string token)
        {
            if (!ControllerInputOwnership.UsingController || actionInputFrame != Time.frameCount ||
                bindings == null || token == "Unbound") return false;
            foreach (string action in consumedActions)
                if (bindings[action] == token) return true;
            return false;
        }

        private static bool ConsumedBindingHeld()
        {
            if (actionInputFrame != Time.frameCount || bindings == null) return false;
            foreach (string action in consumedActions)
                if (bindings.Held(action, state)) return true;
            return false;
        }

        private static bool InventoryOwnsInput()
        {
            return ControllerInputOwnership.GameplayBlocked || InventoryActive() || inventoryInputFrame == Time.frameCount;
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
                BoundHeld("Inventory") || BoundHeld("Pause") || ConsumedBindingHeld());
        }

        private static bool Bound(string action, int kind)
        {
            return ControllerInputOwnership.UsingController && bindings != null && !IsActionConsumed(action) &&
                (kind != 1 || ownershipEntryFrame != Time.frameCount) && (kind == 1 ? bindings.Pressed(action, state) : bindings.Held(action, state));
        }

        // These signatures deliberately mirror the Unity calls replaced by
        // GameplayInputPatches. Vanilla input is ORed in outside an owned modal UI.
        internal static bool PlayerButton(string name)
        {
            if (ControllerInputOwnership.GameplayBlocked) return false;
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
            if (ControllerInputOwnership.GameplayBlocked)
            {
                jumpRequiresRelease = true;
                return false;
            }
            bool vanilla = Input.GetButtonDown(name);
            if (!Refresh()) return vanilla;
            // Native menu opening can happen after another adapter refreshed
            // this frame. Modal ownership must stay live across that cache.
            if (name == "Jump" && (InventoryOwnsInput() || GameScript.pausing)) jumpRequiresRelease = true;
            switch (name)
            {
                case "Left": return vanilla || leftPressed;
                case "Right": return vanilla || rightPressed;
                case "Down": return vanilla || downPressed;
                // The game's legacy pad mapping has Y=Up and A=Jump.
                // Keyboard ownership returns native values above. While the
                // controller owns a menu, jump is blocked independently of raw
                // A timing; gameplay still filters the legacy A alias.
                case "Up": return vanilla && !state.ButtonHeld(2) && !state.ButtonHeld(3) && !MenuInputHeld();
                case "Jump": return !jumpRequiresRelease &&
                    ((vanilla && !PhysicalButtonActive(0) && !MenuInputHeld()) ||
                    (SafeActions() && BoundPressed("Jump")));
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
            if (ControllerInputOwnership.GameplayBlocked) return false;
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
            // Releases clear native movement/augment latches (upping/downing).
            // A modal blocks new actions, but must not swallow this cleanup.
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
            if (ControllerInputOwnership.GameplayBlocked) return 0f;
            float vanilla = Input.GetAxis(name);
            if (name == "Horizontal" && Refresh() && Math.Abs(state.MoveX) > DirectionThreshold)
                return state.MoveX;
            if (name == "Attack" && Refresh() && (MenuInputHeld() ||
                ((attackRequiresRelease || TokenConsumed("RightTrigger")) && state.RightTriggerHeld))) return 0f;
            if (name != "Attack" || !Refresh() || !SafeActions() || !GameScript.combatMode ||
                attackRequiresRelease || !BoundHeld("PrimaryAction")) return vanilla;
            // Existing PlayerScript.Update tests Attack < 0 and starts its
            // ordinary Attack coroutine only when its own gates allow it.
            return Math.Min(vanilla, -1f);
        }

        internal static bool GameButtonDown(string name)
        {
            if (ControllerInputOwnership.GameplayBlocked) return false;
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
                bool keyboard = vanilla && !PhysicalButtonConsumed(3) && !(InventoryOwnsInput() &&
                    (state.ButtonHeld(3) || MenuInputHeld()));
                // Build Mode shares this input with tile destruction.
                return keyboard || (SafeActions() && !GameScript.talking && MenuScript.player && BoundPressed("CombatMode"));
            }
            return vanilla;
        }

        internal static bool GamePausePressed()
        {
            return !ControllerInputOwnership.GameplayBlocked && Refresh() && !GameScript.inventoryOpen && !InventoryOwnsInput() && MenuScript.player && !GameScript.dead &&
                   !PlayerScript.beaming && BoundPressed("Pause");
        }

        internal static bool ExplorationPrimaryHeld()
        {
            return Refresh() && !attackRequiresRelease && !InventoryOwnsInput() && BoundHeld("PrimaryAction");
        }

        internal static bool ExplorationPrimaryPressed()
        {
            return Refresh() && !attackRequiresRelease && !InventoryOwnsInput() && BoundPressed("PrimaryAction");
        }

        internal static bool ExplorationContextSwitchPressed()
        {
            if (ControllerInputOwnership.GameplayBlocked) return false;
            if (!Refresh()) return false;
            return BoundPressed("CombatMode") || BoundPressed("Inventory") ||
                BoundPressed("Pause") || Input.GetButtonDown("CombatMode") ||
                Input.GetButtonDown("Inventory");
        }

        internal static bool GameDroidButtonDown(string name, GameScript game)
        {
            if (ControllerInputOwnership.GameplayBlocked) return false;
            bool vanilla = Input.GetButtonDown(name);
            return vanilla && !(Refresh() && PhysicalButtonConsumed(5)) && !(Refresh() && InventoryOwnsInput() &&
                (state.ButtonHeld(5) || MenuInputHeld())) &&
                !ControllerExploration.SuppressControllerDroidButton(game);
        }
    }
}
