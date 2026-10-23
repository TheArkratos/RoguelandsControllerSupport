using System;
namespace GadgetCore.API
{
    [Flags] public enum ItemType { NONSTACKING=1, WEAPON=2, OFFHAND=4, HELMET=8, ARMOR=16, RING=32, DROID=64, EQUIP_MASK=126 }
    public static class ItemRegistry
    {
        public static ItemType GetTypeByID(int id) { return id == 500 ? ItemType.WEAPON | ItemType.NONSTACKING : 0; }
    }
    public static class GadgetCoreAPI
    {
        internal static bool Frozen;
        public static bool IsInputFrozen() { return Frozen; }
        public static bool CanItemsStack(Item a, Item b) { return a != null && b != null && a.id == b.id && a.tier == b.tier && a.exp == b.exp && a.corrupted == b.corrupted; }
        public static Item CopyItem(Item a) { return new Item { id=a.id,q=a.q,exp=a.exp,tier=a.tier,corrupted=a.corrupted,aspect=a.aspect,aspectLvl=a.aspectLvl }; }
    }
}
namespace ControllerSupport
{
    // These fixtures test production click routing. Lifecycle movement and GUI
    // rendering have dedicated production-linked integration fixtures.
    internal static class ControllerGameplayInput { internal static void ConsumeInventoryInput() { } }
    internal static class InventoryItemLifecycle
    {
        internal static Item Held(GameScript game) { return game == null ? null : game.holdingItem; }
        internal static int CurrentPage(GameScript game) { return game.currentStoragePage; }
        internal static void ObserveContext(GameScript game) { }
        internal static bool BlockMutations;
        internal static bool MutationsBlocked(GameScript game) { return BlockMutations; }
        internal static void ClearIfEmpty(GameScript game) { }
        internal static bool TryReturn(GameScript game, out string reason) { reason="No pickup source"; return false; }
        internal static bool TryDropAbovePlayer(GameScript game, out string reason) { reason="No player"; return false; }
        internal static void PlaySound(GameScript game, string path) { }
        internal static void Reset() { BlockMutations=false; }
    }
    internal static class InventoryFeedback
    {
        internal static string LastReason;
        internal static void SetContext(GameScript game, UnityEngine.Collider selected, bool controller,
            BindingConfig config, string primary, string secondary, bool transfer, bool held, UnityEngine.Bounds? heldBadgeBounds = null, string cancelLabel = null, System.Func<UnityEngine.Bounds?> badgeProvider = null, bool storagePaging = false) { }
        internal static void ClearPresentation() { LastReason=null; }
        internal static void Reject(string reason) { LastReason=reason; }
        internal static void Reset() { LastReason=null; }
    }
}

// These legacy fixtures observe selection/actions only. Actual native cursor
// synchronization and conversion visuals are covered by native_inventory_cursor_repro.
namespace ControllerSupport
{
    internal static class InventoryNativeCursor
    {
        internal static void Update(GameScript game) { }
        internal static void Reset() { }
    }
}
