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
        internal static bool ThrowOnCopy;
        internal static bool Frozen;
        internal static System.Collections.Generic.Dictionary<int,Tuple<int,int>> emblemForgeRecipes = new System.Collections.Generic.Dictionary<int,Tuple<int,int>>();
        public static bool IsInputFrozen() { return Frozen; }
        public static bool CanItemsStack(Item a, Item b) { return a != null && b != null && a.id == b.id && a.tier == b.tier && a.exp == b.exp && a.corrupted == b.corrupted && a.extraData == b.extraData; }
        public static Item CopyItem(Item a) { if (ThrowOnCopy) throw new InvalidOperationException("injected copy failure"); return new Item { extraData=a.extraData,id=a.id,q=a.q,exp=a.exp,tier=a.tier,corrupted=a.corrupted,aspect=a.aspect,aspectLvl=a.aspectLvl }; }
    }
}
namespace ControllerSupport
{
    // These fixtures test production click routing. Lifecycle movement and GUI
    // rendering have dedicated production-linked integration fixtures.
    internal static class InventoryItemLifecycle
    {
        internal static Item Held(GameScript game) { return game == null ? null : game.holdingItem; }
        internal static int CurrentPage(GameScript game) { return game.currentStoragePage; }
        internal static void ObserveContext(GameScript game) { }
        internal static bool BlockMutations;
        internal static bool MutationsBlocked(GameScript game) { return BlockMutations; }
        internal static int ReturnCalls, DropCalls, SoundCalls;
        internal static bool ReturnSuccess = true;
        internal static void ClearIfEmpty(GameScript game) { }
        internal static bool TryReturn(GameScript game, out string reason) { ReturnCalls++; reason="No pickup source"; if (!ReturnSuccess) return false; game.holdingItem = new Item(); return true; }
        internal static bool TryDropAbovePlayer(GameScript game, out string reason) { DropCalls++; reason=null; game.holdingItem = new Item(); return true; }
        internal static void PlaySound(GameScript game, string path) { SoundCalls++; }
        internal static void Reset() { ReturnCalls=DropCalls=SoundCalls=0; ReturnSuccess=true; BlockMutations=false; }
    }
    internal static class InventoryFeedback
    {
        internal static string LastReason, Primary, Secondary;
        internal static bool Transfer, Held, Controller;
        internal static void SetContext(GameScript game, UnityEngine.Collider selected, bool controller,
            BindingConfig config, string primary, string secondary, bool transfer, bool held, UnityEngine.Bounds? heldBadgeBounds = null, string cancelLabel = null, System.Func<UnityEngine.Bounds?> badgeProvider = null, bool storagePaging = false) { Primary=primary; Secondary=secondary; Transfer=transfer; Held=held; Controller=controller; }
        internal static void Reject(string reason) { LastReason=reason; }
        internal static void Reset() { LastReason=null; }
    }
}

namespace ControllerSupport { internal static class ControllerGameplayInput { internal static int Consumed; internal static void ConsumeInventoryInput() { Consumed++; } } }
