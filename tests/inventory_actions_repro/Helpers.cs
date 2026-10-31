using System;
namespace GadgetCore.API
{
    [Flags] public enum ItemType { NONSTACKING=1, WEAPON=2, OFFHAND=4, HELMET=8, ARMOR=16, RING=32, DROID=64, EQUIP_MASK=126, LOOT=128, ORGANIC=256, EQUIPABLE=512, EMBLEM=LOOT|1024, MODABLE=2048 }
    public static class ItemRegistry
    {
        public static ItemType GetTypeByID(int id) { return id == 10001 ? ItemType.MODABLE | ItemType.NONSTACKING : id == 500 ? ItemType.WEAPON | ItemType.NONSTACKING : id >= 11 && id <= 39 ? ItemType.LOOT | ItemType.ORGANIC : id >= 101 && id <= 199 ? ItemType.EMBLEM : 0; }
    }
    public static class GadgetCoreAPI
    {
        internal static bool ThrowOnCopy;
        internal static bool Frozen;
        internal static System.Collections.Generic.Dictionary<int,Tuple<int,int>> emblemForgeRecipes = new System.Collections.Generic.Dictionary<int,Tuple<int,int>>();
        internal static System.Collections.Generic.Dictionary<int,Tuple<int,int>> prismForgeRecipes = new System.Collections.Generic.Dictionary<int,Tuple<int,int>>();
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
        internal static bool IsHoldingChip(GameScript game)
        {
            var flag=typeof(GameScript).GetField("holdingCombatChip",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            var held=Held(game); return game!=null && held!=null && held.id>0 && flag!=null && (bool)flag.GetValue(game);
        }
        internal static bool HasHeldItem(GameScript game)
        { var held=Held(game);return held!=null && held.id>0 && (held.q>0 || IsHoldingChip(game)); }

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
            BindingConfig config, string primary, string secondary, bool transfer, bool held, UnityEngine.Bounds? heldBadgeBounds = null, string cancelLabel = null, System.Func<UnityEngine.Bounds?> badgeProvider = null, bool storagePaging = false, bool recipePaging = false) { Primary=primary; Secondary=secondary; Transfer=transfer; Held=held; Controller=controller; }
        internal static void ClearPresentation() { LastReason=null; }
        internal static void Reject(string reason) { LastReason=reason; }
        internal static void Reset() { LastReason=null; }
    }
}

namespace ControllerSupport { internal static class ControllerGameplayInput { internal static void ConsumeActionForFrame(string action) { } internal static int Consumed; internal static void ConsumeInventoryInput() { Consumed++; } } }

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
