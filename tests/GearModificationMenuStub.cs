// Unrelated fixtures leave this station closed; its production adapter has
// dedicated gear_modification_repro coverage.
namespace ControllerSupport
{
    internal static class GearModificationMenuContext
    {
        internal const int FirstSlot=205, TargetCount=4, ItemSlot=205;
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static bool IsOpen(GameScript game) { return false; }
        internal static bool IsTarget(int index) { return index>=FirstSlot && index<FirstSlot+TargetCount; }
        internal static bool IsItemSlot(int index) { return index==ItemSlot; }
        internal static bool IsButton(int index) { return IsTarget(index)&&!IsItemSlot(index); }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] targets) { }
        internal static bool CanActivate(GameScript game,int index,int action,out string reason) { reason="Gear Modification unavailable";return false; }
        internal static Item ReadTarget(GameScript game,int index) { return null; }
        internal static bool NeedsLoaderDispatch(GameScript game,int index) { return false; }
        internal static void DispatchLoaderItem(GameScript game) { }
        internal static string ActionLabel(GameScript game,int index) { return null; }
        internal static string SecondaryLabel(GameScript game,int index) { return null; }
    }
}
