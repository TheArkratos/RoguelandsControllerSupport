using System.Collections.Generic;
namespace UnityEngine
{
    public class GameObject
    {
        public string name, tag;
        public int layer = 16;
        public bool activeInHierarchy = true;
        public Transform transform;
        public Collider collider;
        public readonly List<Collider> colliders = new List<Collider>();
        public GameObject(string name = "", string tag = "") { this.name = name; this.tag = tag; transform = new Transform(this); }
        public T[] GetComponentsInChildren<T>() where T : class { return colliders.ToArray() as T[]; }
        public static implicit operator bool(GameObject obj) { return obj != null; }
    }
    public class Transform
    {
        public GameObject gameObject;
        public Transform parent;
        public Transform(GameObject owner) { gameObject = owner; }
        public T GetComponent<T>() where T : class { return gameObject.collider as T; }
    }
    public class Collider
    {
        public bool enabled = true;
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public Collider(GameObject owner) { gameObject = owner; owner.collider = this; }
        public static implicit operator bool(Collider obj) { return obj != null; }
    }
}
public class Item { public int id, q; public string extraData; public Item(int id = 0, int q = 0) { this.id = id; this.q = q; } }
public partial class GameScript
{
    public static bool inventoryOpen = true, pausing, dead, buildMode;
    public UnityEngine.GameObject inventoryMain = new UnityEngine.GameObject();
    public UnityEngine.GameObject menuCraft = new UnityEngine.GameObject("menuCraft");
    public UnityEngine.GameObject menuRecipe = new UnityEngine.GameObject("menuRecipe") { activeInHierarchy = false };
    private int craftType = 1, curRecipePage;
    private bool canCraft = true;
    private Item[] craft = { new Item(11, 2), new Item(21, 2), new Item(31, 2), new Item() };
    private Item[] inventory = new Item[45];
    private int[,] recipeUnlocked1 = new int[2, 12];
    public Item Held = new Item();
    public bool Blocked, PlacementAllowed = true;
    public int ValidationCalls;
    public GameScript() { for (int i = 0; i < inventory.Length; i++) inventory[i] = new Item(); }
    public bool CanPlaceCraft(int id)
    {
        ValidationCalls++;
        if (!PlacementAllowed) return false;
        if (craftType == 0)
            for (int i = 0; i < 3; i++)
                if (craft[i].id == id || (craft[i].id > 0 && craft[i].id % 10 != id % 10)) return false;
        return true;
    }
    public void SetType(int value) { craftType = value; }
    public void SetCanCraft(bool value) { canCraft = value; }
    public void SetPage(int value) { curRecipePage = value; }
    public void Unlock(int page, int slot) { recipeUnlocked1[page, slot] = 1; }
    public Item[] Craft { get { return craft; } }
    public Item[] Inventory { get { return inventory; } }
}
namespace GadgetCore.API
{
    public enum ItemType { LOOT = 2, EMBLEM = 3, EQUIPABLE = 16, ORGANIC = 16384 }
    public class ItemInfo { public ItemType Type; }
    public static class ItemRegistry
    {
        public static ItemType GetTypeByID(int id) { return id >= 11 && id <= 39 || id == 10001 ? ItemType.LOOT | ItemType.ORGANIC :
                id >= 101 && id < 200 || id == 10002 ? ItemType.EMBLEM : (ItemType)0; }
    }
    public static class GadgetCoreAPI
    {
        public static bool Frozen;
        public static bool IsInputFrozen() { return Frozen; }
        public static bool CanItemsStack(Item a, Item b) { return a.id == b.id && a.extraData == b.extraData; }
    }
}
namespace ControllerSupport
{
    internal static class InventoryItemLifecycle
    {
        internal static Item Held(GameScript game) { return game.Held; }
        internal static bool MutationsBlocked(GameScript game) { return game.Blocked; }
    }
}
