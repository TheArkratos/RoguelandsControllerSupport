namespace ControllerSupport
{
    internal static class ItemConversionContext
    {
        internal const int ToggleSlot=130;
        internal static bool IsOpen(GameScript game) { return false; }
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static bool ModeActive(GameScript game) { return false; }
        internal static SelectionCornerStyle ActiveStyle(GameScript game) { return SelectionCornerStyle.Normal; }
        internal static UnityEngine.Collider CollectTarget(GameScript game) { return null; }
        internal static bool CanActivate(GameScript game,int slot,object target,out string reason) { reason="Forge unavailable"; return false; }
        internal static string ActionLabel(GameScript game,int slot) { return null; }
        internal static string CancelLabel(GameScript game) { return null; }
        internal static string SecondaryRejection(GameScript game) { return null; }
        internal static void CancelMode(GameScript game) { }
    }
}
