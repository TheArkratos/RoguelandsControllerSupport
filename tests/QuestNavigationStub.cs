namespace ControllerSupport
{
    internal static class QuestMenuContext
    {
        internal const int FirstSlot=209, TargetCount=4, Shuffle=212;
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static bool IsTarget(int i) { return i>=FirstSlot && i<FirstSlot+TargetCount; }
        internal static bool IsItemSlot(int i) { return i>=FirstSlot && i<Shuffle; }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] targets) { }
    }
}
