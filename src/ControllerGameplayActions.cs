using System;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Gameplay-only button edges. Native GameScript still owns the six hotkey
    // effects, resources and mode-dependent item/chip dispatch.
    internal static class ControllerGameplayActions
    {
        private static readonly string[] Actions = {
            "AimAssist", "Hotkey1", "Hotkey2", "Hotkey3", "Hotkey4", "Hotkey5", "Hotkey6"
        };
        private static readonly bool[] armed = new bool[Actions.Length];
        private static readonly bool[] queued = new bool[6];
        private static Func<JoystickInputState> stateProvider;
        private static BindingConfig bindings;
        private static Action toggleAssist;
        private static GameScript owner;
        private static GameObject player;
        private static bool initialized, previouslyAllowed, previousCombat;
        private static int frame = -1, blockedFrame = -1, scene = -1;

        internal static void Configure(Func<JoystickInputState> provider, BindingConfig config,
            Action toggleAimAssist = null)
        {
            Reset();
            stateProvider = provider;
            bindings = config;
            toggleAssist = toggleAimAssist;
        }

        internal static void Reset()
        {
            stateProvider = null; bindings = null; toggleAssist = null;
            owner = null; player = null;
            initialized = previouslyAllowed = previousCombat = false;
            frame = blockedFrame = scene = -1;
            Array.Clear(armed, 0, armed.Length);
            Array.Clear(queued, 0, queued.Length);
        }

        internal static void ConsumeForFrame()
        {
            blockedFrame = Time.frameCount;
            Array.Clear(queued, 0, queued.Length);
        }

        internal static void Prepare(GameScript game)
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            Array.Clear(queued, 0, queued.Length);
            JoystickInputState state = stateProvider == null ? null : stateProvider();
            bool allowed = state != null && state.Connected && bindings != null && Available(game);
            bool changed = !initialized || owner != game || player != MenuScript.player ||
                scene != Application.loadedLevel || previousCombat != GameScript.combatMode ||
                previouslyAllowed != allowed;
            owner = game; player = MenuScript.player; scene = Application.loadedLevel;
            initialized = true; previouslyAllowed = allowed; previousCombat = GameScript.combatMode;
            if (bindings == null) return;
            bool switching = (!ControllerGameplayInput.IsActionConsumed("CombatMode") && bindings.Pressed("CombatMode", state)) ||
                (!ControllerGameplayInput.IsActionConsumed("Inventory") && bindings.Pressed("Inventory", state)) ||
                (!ControllerGameplayInput.IsActionConsumed("Pause") && bindings.Pressed("Pause", state)) ||
                (Input.GetButtonDown("CombatMode") && !ControllerGameplayInput.PhysicalButtonConsumed(3)) ||
                (Input.GetButtonDown("Inventory") && !ControllerGameplayInput.PhysicalButtonConsumed(7));
            for (int i = 0; i < Actions.Length; i++)
            {
                bool held = bindings.Held(Actions[i], state);
                if (!allowed || changed || switching || ControllerGameplayInput.IsActionConsumed(Actions[i]))
                {
                    armed[i] = !held;
                    continue;
                }
                if (!held) armed[i] = true;
                if (!armed[i] || !bindings.Pressed(Actions[i], state)) continue;
                armed[i] = false;
                if (i == 0)
                {
                    if (GameScript.combatMode && toggleAssist != null) toggleAssist();
                }
                else queued[i - 1] = true;
            }
        }

        internal static bool HotkeyPressed(int slot)
        {
            if (slot < 0 || slot >= queued.Length || frame != Time.frameCount ||
                !queued[slot] || ControllerGameplayInput.IsActionConsumed(Actions[slot + 1]) || player != MenuScript.player || scene != Application.loadedLevel ||
                previousCombat != GameScript.combatMode || !Available(owner)) return false;
            queued[slot] = false;
            return true;
        }

        private static bool Available(GameScript game)
        {
            if (game == null || blockedFrame == Time.frameCount || GameScript.inventoryOpen ||
                GameScript.pausing || GameScript.dead || GameScript.buildMode ||
                PlayerScript.beaming || !MenuScript.player || GadgetCoreAPI.IsInputFrozen()) return false;
            NetworkView view = MenuScript.player.GetComponent<NetworkView>();
            return view && view.isMine;
        }
    }
}
