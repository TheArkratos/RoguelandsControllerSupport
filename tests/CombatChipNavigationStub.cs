namespace ControllerSupport
{
    internal static class CombatChipMenuContext
    {
        internal const int FirstSlot=152, TargetCount=38;
        internal static bool IsTarget(int slot) { return slot>=FirstSlot && slot<FirstSlot+TargetCount; }
        internal static bool IsOpen(GameScript game) { return false; }
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] targets) { }
    }
}
