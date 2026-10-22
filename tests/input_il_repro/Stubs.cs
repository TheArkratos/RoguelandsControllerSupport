using System;

namespace GadgetCore.API
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyGadgetAttribute : Attribute
    {
        public HarmonyGadgetAttribute(string name) { }
    }
}

namespace UnityEngine
{
    public static class Input
    {
        public static bool GetButton(string name) { return false; }
        public static bool GetButtonDown(string name) { return false; }
        public static bool GetButtonUp(string name) { return false; }
        public static float GetAxis(string name) { return 0f; }
    }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
    }
}

public class PlayerScript { public void Update() { } }
public class ActionCursor { public void Update() { } }
public class GameScript
{
    public static bool inventoryOpen;
    public void Update() { }
    public void CloseAllMenus() { }
    public void PauseMenu() { }
}

namespace ControllerSupport
{
    internal static class ControllerExploration
    {
        internal static void UpdateAndDispatch(GameScript game) { }
    }
    internal static class ControllerMenuSelection
    {
        internal static void Reject(GameScript game, string reason) { }
        internal static void NativeError(GameScript game, int code) { }
        internal static void Prepare(GameScript game) { }
        internal static void Update(GameScript game) { }
        internal static void EndNativeUpdate() { }
        internal static void PlaceHeldItemCursor(ActionCursor cursor) { }
        internal static bool ClosingStorageWouldDropHeldItem(GameScript game) { return false; }
    }
    internal static class InventoryItemLifecycle { internal static bool LeaveRange(GameScript game, out string reason) { reason=null; return true; } }
    internal static class ControllerGameplayInput
    {
        internal static bool PlayerButton(string name) { return false; }
        internal static bool PlayerButtonDown(string name) { return false; }
        internal static bool PlayerInteractionButtonDown(string name, PlayerScript player) { return false; }
        internal static bool PlayerButtonUp(string name) { return false; }
        internal static float PlayerAxis(string name) { return 0; }
        internal static bool GameButtonDown(string name) { return false; }
        internal static bool GameDroidButtonDown(string name, GameScript game) { return false; }
        internal static bool GamePausePressed() { return false; }
        internal static void SetGameUpdateContext(GameScript game) { }
    }
}
