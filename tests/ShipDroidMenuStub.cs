// Legacy action fixtures keep the ship panel closed; actual context/action
// dispatch is production-linked in ship_droid_inventory_repro.
namespace ControllerSupport
{
    internal static class ShipDroidMenuContext
    {
        internal const int FirstSlot=190, FirstItem=196, TargetCount=15;
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static bool IsTarget(int index) { return index>=FirstSlot && index<FirstSlot+TargetCount; }
        internal static bool IsItemSlot(int index) { return index>=FirstItem && index<FirstSlot+TargetCount; }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] targets) { }
        internal static bool CanActivate(GameScript game,int index,int action,out string reason) { reason="Ship panel unavailable";return false; }
        internal static void TransferItem(GameScript game,int index) { }
        internal static Item ReadTarget(GameScript game,int index) { return null; }
        internal static string ActionLabel(GameScript game,int index) { return null; }
        internal static string SecondaryLabel(GameScript game,int index) { return null; }
        internal static bool CanTransfer(GameScript game,int index) { return false; }
    }
}
