using System;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Reuse Interact (default X) for text reveal/advance. Native Talk retains
    // ownership of dialogue chains, effects and the final close coroutine.
    internal static class ControllerDialogueInput
    {
        private static readonly FieldInfo CurA = DialogueField("curA");
        private static readonly FieldInfo CurB = DialogueField("curB");
        private static readonly FieldInfo CurC = DialogueField("curC");
        private static readonly FieldInfo Exiting = DialogueField("exitTalking");
        private static Func<JoystickInputState> stateProvider;
        private static BindingConfig bindings;
        private static GameScript owner;
        private static GameObject player, popup;
        private static int frame = -1, scene = -1;
        private static bool initialized, previouslyAllowed, armed, awaitingRelease;

        private static FieldInfo DialogueField(string name)
        {
            return typeof(GameScript).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        internal static void Configure(Func<JoystickInputState> provider, BindingConfig config)
        {
            Reset();
            stateProvider = provider;
            bindings = config;
        }

        internal static void Reset()
        {
            stateProvider = null; bindings = null;
            owner = null; player = popup = null;
            frame = scene = -1;
            initialized = previouslyAllowed = armed = awaitingRelease = false;
        }

        internal static void Prepare(GameScript game)
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            JoystickInputState state = stateProvider == null ? null : stateProvider();
            bool held = bindings != null && bindings.Held("Interact", state);
            GameObject nextPopup = game != null ? game.menuTalking : null;
            bool active = nextPopup && nextPopup.activeInHierarchy;
            bool allowed = state != null && state.Connected && bindings != null && active &&
                Available(game);
            bool changed = !initialized || owner != game || player != MenuScript.player ||
                scene != Application.loadedLevel || popup != nextPopup || previouslyAllowed != allowed;
            owner = game; player = MenuScript.player; popup = nextPopup;
            scene = Application.loadedLevel; initialized = true; previouslyAllowed = allowed;

            // Include timed system/tutorial popups: EnterTalking2 opens this
            // same native menu without setting GameScript.talking.
            if (active)
            {
                ControllerGameplayInput.ConsumeInventoryInput();
                awaitingRelease = held;
            }
            else if (awaitingRelease)
            {
                ControllerGameplayInput.ConsumeInventoryInput();
                awaitingRelease = held;
            }

            if (!allowed || changed)
            {
                armed = allowed && !held;
                return;
            }
            if (!held) armed = true;
            if (!armed || !bindings.Pressed("Interact", state)) return;
            armed = false;

            // Let the native raycast handle a simultaneous physical click once.
            // Do not reveal here and then advance again in native Update.
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) ||
                bindings.Pressed("Inventory", state) || bindings.Pressed("Pause", state) ||
                Input.GetButtonDown("Inventory") || Input.GetButtonDown("Cancel")) return;
            if (MenuTalking.isWriting) popup.SendMessage("Click");
            else game.Talk((int)CurA.GetValue(game), (int)CurB.GetValue(game), (int)CurC.GetValue(game));
        }

        private static bool Available(GameScript game)
        {
            if (game == null || !MenuScript.player || GameScript.pausing || GameScript.dead ||
                GameScript.buildMode || GameScript.inventoryOpen || PlayerScript.beaming ||
                GadgetCoreAPI.IsInputFrozen() || CurA == null || CurB == null || CurC == null ||
                Exiting == null || (bool)Exiting.GetValue(game)) return false;
            NetworkView view = MenuScript.player.GetComponent<NetworkView>();
            return view && view.isMine;
        }
    }
}
