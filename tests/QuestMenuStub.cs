namespace ControllerSupport
{
    internal static class QuestMenuContext
    {
        internal const int FirstSlot=209, TargetCount=4, Shuffle=212;
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static bool IsTarget(int i) { return i>=FirstSlot && i<FirstSlot+TargetCount; }
        internal static bool IsItemSlot(int i) { return i>=FirstSlot && i<Shuffle; }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] targets) { }
        internal static Item ReadTarget(GameScript game,int index) { return null; }
        internal static bool PhysicalMutationBlocked(GameScript game) { return false; }
        internal static bool CanActivate(GameScript game,int index,int action,out string reason) { reason="Questbot unavailable";return false; }
        internal static string ActionLabel(GameScript game,int index) { return null; }
    }
}
