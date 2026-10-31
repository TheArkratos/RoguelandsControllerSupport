using System;
using UnityEngine;

namespace ControllerSupport
{
    // Gameplay walking intent is shared by exploration sprite facing and drops.
    // Menu navigation never changes it; combat still owns its existing aim-facing.
    internal static class ControllerLookDirection
    {
        private static Func<JoystickInputState> stateProvider;
        private static GameObject player;
        private static int scene;
        private static bool hasDirection;
        private static float direction;

        internal static void Configure(Func<JoystickInputState> provider)
        { stateProvider = provider; Clear(); }
        internal static void Reset() { stateProvider = null; Clear(); }
        private static void Clear() { player = null; hasDirection = false; direction = 1f; }

        internal static float NativeDirection()
        {
            PlayerScript script = MenuScript.player == null ? null : MenuScript.player.GetComponent<PlayerScript>();
            if (script == null || script.e == null) return 1f;
            float yaw = script.e.transform.eulerAngles.y;
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) return 1f;
            return Math.Cos(yaw * Math.PI / 180d) < 0d ? 1f : -1f;
        }

        private static bool Refresh()
        {
            if (stateProvider == null || !ControllerInputOwnership.UsingController ||
                MenuScript.player == null || GameScript.dead || GameScript.buildMode)
            { Clear(); return false; }
            GameObject current = MenuScript.player;
            NetworkView view = current.GetComponent<NetworkView>();
            if (view == null || !view.isMine) { Clear(); return false; }
            if (player != current || scene != Application.loadedLevel)
            { Clear(); player = current; scene = Application.loadedLevel; }
            JoystickInputState state = stateProvider();
            // Polling can hand input back to the mouse in this same query.
            if (!ControllerInputOwnership.UsingController || MenuScript.player != current ||
                GameScript.dead || GameScript.buildMode || !view.isMine ||
                scene != Application.loadedLevel || state == null || !state.Connected)
            { Clear(); return false; }
            if (!hasDirection) { direction = NativeDirection(); hasDirection = true; }
            // Combat walking updates memory too, without taking over combat's
            // visual aim. Returning to exploration uses the latest walking side.
            if (!GameScript.inventoryOpen && !GameScript.pausing &&
                !ControllerInputOwnership.GameplayBlocked && Math.Abs(state.MoveX) > 0.5f)
                direction = state.MoveX < 0f ? -1f : 1f;
            return true;
        }

        internal static void Observe() { Refresh(); }

        internal static bool TryExplorationX(out float x)
        {
            x = 0f;
            if (!Refresh() || GameScript.combatMode) return false;
            x = MenuScript.player.transform.position.x + direction;
            return true;
        }

        internal static float DropSide()
        {
            bool available = Refresh();
            return available && !GameScript.combatMode ? direction : NativeDirection();
        }
    }
}
