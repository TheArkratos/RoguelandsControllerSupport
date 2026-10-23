using System;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
    }
    public class Transform { public Vector3 position; }
    public class Object { }
    public struct Quaternion { }
    public static class Network
    {
        public static bool FailDestroy;
        public static readonly List<Object> Live = new List<Object>();
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation, int group)
        { var spawned = new GameObject(); Live.Add(spawned); return spawned; }
        public static void Destroy(Object spawned)
        { if (FailDestroy) throw new Exception("Network destroy failure"); Live.Remove(spawned); }
    }
    public class GameObject : Object { public bool activeSelf; public Transform transform = new Transform(); }
    public class AudioClip { }
    public class AudioSource { public void PlayOneShot(AudioClip clip, float volume) { } }
    public static class Resources { public static object Load(string path) { return new AudioClip(); } }
    public static class Application { public static int loadedLevel; }
}
namespace GadgetCore.API
{
    public class HarmonyGadget : Attribute { public HarmonyGadget(string name) { } }
    public static class GadgetCoreAPI
    {
        public static int Drops;
        public static bool FailSpawn;
        public static bool ThrowSpawn;
        public static UnityEngine.Vector3 LastPosition;
        public static Item LastDrop;
        public static bool CanItemsStack(Item a, Item b) { return a.id == b.id && a.extra == b.extra && a.id < 300; }
        public static Item CopyItem(Item item) { return new Item(item.id, item.q) { extra = item.extra }; }
        public static ItemScript DropItem(UnityEngine.Vector3 position, Item item)
        {
            Drops++;
            LastPosition = position;
            LastDrop = item;
            if (FailSpawn) return null;
            // Test-double equivalent of the injected call, not a replacement for
            // the production capture helper tested here.
            ControllerSupport.InventoryItemLifecycle.TrackSpawn(UnityEngine.Network.Instantiate(null, position, new UnityEngine.Quaternion(), 0));
            if (ThrowSpawn) throw new Exception("RPC failure after real spawn");
            return new ItemScript();
        }
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type type, string method) { } public HarmonyPatch(Type type, string method, Type[] args) { } }
    public class HarmonyTranspiler : Attribute { }
    public class CodeInstruction
    {
        public OpCode opcode; public object operand;
        public List<string> labels = new List<string>(); public List<string> blocks = new List<string>();
        public CodeInstruction(OpCode opcode, object operand = null) { this.opcode = opcode; this.operand = operand; }
        public CodeInstruction(CodeInstruction original)
        { opcode = original.opcode; operand = original.operand; labels.AddRange(original.labels); blocks.AddRange(original.blocks); }
    }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public class HarmonyFinalizer : Attribute { }
    public class HarmonyPriority : Attribute { public HarmonyPriority(int priority) { } }
    public static class Priority { public const int First = 800, Last = 0; }
    public static class AccessTools { public static MethodInfo Method(Type t, string n, Type[] types) { return t.GetMethod(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, types, null); } }
}
public class Item
{
    public int id, q;
    public string extra;
    public Item(int id, int q) { this.id = id; this.q = q; }
    public Item(int id, int q, int exp, int tier, int corrupted, int[] aspect, int[] aspectLvl) : this(id, q) { }
}
public class ItemScript { }
public static class PlayerScript { public static UnityEngine.GameObject curInteractObj; }
public static class MenuScript { public static UnityEngine.GameObject player; }
public static class Menuu { public static float soundLevel = 10; }
public class GameScript
{
    public Item holdingItem;
    public Item[] inventory = NewItems(45), storage = NewItems(432);
    public int curStoragePage;
    public bool holdingCombatChip;
    public bool RejectPlace;
    public bool ThrowRefresh;
    public static bool inventoryOpen = true;
    public UnityEngine.GameObject menuStorage = new UnityEngine.GameObject { activeSelf = true };
    public UnityEngine.GameObject menuPlanets = new UnityEngine.GameObject { activeSelf = false };
    public UnityEngine.GameObject menuEmblemForge = new UnityEngine.GameObject { activeSelf = false };
    public UnityEngine.GameObject menuTrasher = new UnityEngine.GameObject { activeSelf = false };
    public UnityEngine.GameObject menuPrism = new UnityEngine.GameObject { activeSelf = false };
    public void Prism() { menuStorage.activeSelf = menuPlanets.activeSelf = menuEmblemForge.activeSelf = menuTrasher.activeSelf = false; menuPrism.activeSelf = true; inventoryOpen = true; }
    public void PrismItem(int slot) { int amount = inventory[slot].q / 10; inventory[slot].q -= amount * 10; holdingItem = new Item(inventory[slot].id + 3, amount); }
    public void Trash() { menuStorage.activeSelf = menuPlanets.activeSelf = menuEmblemForge.activeSelf = false; menuTrasher.activeSelf = true; inventoryOpen = true; }
    public void TrashItem(int slot) { inventory[slot] = new Item(52, inventory[slot].q * 2); }
    public void Planets() { menuStorage.activeSelf = false; menuPlanets.activeSelf = true; inventoryOpen = true; }
    public void EmblemForge() { menuStorage.activeSelf = menuPlanets.activeSelf = false; menuEmblemForge.activeSelf = true; inventoryOpen = true; }
    public void ForgeEmblem(int slot) { holdingItem = new Item(80 + slot, 1) { extra = "generated emblem" }; }
    public static Item[] NewItems(int count) { Item[] items = new Item[count]; for (int i = 0; i < count; i++) items[i] = new Item(0, 0); return items; }
    public T GetComponent<T>() where T : new() { return new T(); }
    public void RefreshHoldingSlot() { if (ThrowRefresh) throw new Exception("Visual failure"); }
    public object RefreshStoragePage(int p) { return null; }
    public void StartCoroutine(object routine) { }
    private int StorageIndex(int s) { return s < 30 ? curStoragePage * 30 + s : 360 + curStoragePage * 6 + s - 30; }
    public void PlaceItem(int s) { if (RejectPlace) return; inventory[s] = holdingItem; holdingItem = new Item(0, 0); RefreshHoldingSlot(); }
    public void PlaceItemStorage(int s) { if (RejectPlace) return; storage[StorageIndex(s)] = holdingItem; holdingItem = new Item(0, 0); RefreshHoldingSlot(); }
    public void CombineItem(int s) { inventory[s].q += holdingItem.q; holdingItem.q = 0; }
    public void CombineItemStorage(int s) { storage[StorageIndex(s)].q += holdingItem.q; holdingItem.q = 0; }
    public void DropItem() { }
    public void CloseAllMenus() { }
    public void SelectItemCraft(int s) { }
    public void PlaceOneItem(int s) { }
    public void SelectItem(int s) { holdingItem = inventory[s]; inventory[s] = new Item(0, 0); }
    public void SelectItemStorage(int s) { holdingItem = storage[StorageIndex(s)]; storage[StorageIndex(s)] = new Item(0, 0); }
    public void SwapItem(int s) { if (RejectPlace) return; Item prior = holdingItem; holdingItem = inventory[s]; inventory[s] = prior; }
    public void SwapItemStorage(int s) { Item prior = holdingItem; holdingItem = storage[StorageIndex(s)]; storage[StorageIndex(s)] = prior; }
    public void SplitItem(int s) { holdingItem = new Item(inventory[s].id, inventory[s].q / 2) { extra = inventory[s].extra }; inventory[s].q -= holdingItem.q; }
    public void SplitItemStorage(int s) { int i = StorageIndex(s); holdingItem = new Item(storage[i].id, storage[i].q / 2) { extra = storage[i].extra }; storage[i].q -= holdingItem.q; }
}
