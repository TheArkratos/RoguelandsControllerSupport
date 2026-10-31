namespace ControllerSupport
{
    internal static class GearModificationMenuContext
    {
        internal const int FirstSlot=205, TargetCount=4, ItemSlot=205;
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static bool IsTarget(int index) { return index>=FirstSlot && index<FirstSlot+TargetCount; }
        internal static bool IsItemSlot(int index) { return index==ItemSlot; }
        internal static bool IsButton(int index) { return IsTarget(index)&&!IsItemSlot(index); }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] targets) { }
    }
}
