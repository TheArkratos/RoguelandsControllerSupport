// Legacy menu fixtures do not open the ship panel. Its production context has
// separate integration coverage; keep unrelated navigation fixtures isolated.
namespace ControllerSupport
{
    internal static class ShipDroidMenuContext
    {
        internal const int FirstSlot=190, FirstItem=196, TargetCount=15;
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static bool IsTarget(int index) { return index>=FirstSlot && index<FirstSlot+TargetCount; }
        internal static bool IsItemSlot(int index) { return index>=FirstItem && index<FirstSlot+TargetCount; }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] targets) { }
    }
}
