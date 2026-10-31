using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Application { public static int loadedLevel; }
    public class NetworkView : Object { public bool isMine = true; }
    public class Object
    {
        public static implicit operator bool(Object value) { return value != null; }
    }
    public class GameObject : Object
    {
        public string name;
        public string tag = "Untagged";
        public bool activeInHierarchy = true;
        public int SetActiveCalls;
        public void SetActive(bool active) { SetActiveCalls++; activeInHierarchy=active; }
        public int layer = 16;
        public Transform transform;
        public Collider collider;
        public Renderer renderer;
        public object extraComponent;
        public NetworkView view = new NetworkView();
        public GameObject(string name = "") { this.name = name; transform = new Transform(this); }
        public T GetComponent<T>() where T : class { return collider as T ?? renderer as T ?? view as T ?? extraComponent as T; }
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class
        {
            var found=new List<T>();
            if(includeInactive || activeInHierarchy)
            {
                T own=GetComponent<T>(); if(own!=null) found.Add(own);
                for(int i=0;i<transform.childCount;i++)
                    found.AddRange(transform.GetChild(i).gameObject.GetComponentsInChildren<T>(includeInactive));
            }
            return found.ToArray();
        }
    }
    public class Transform : Object
    {
        public GameObject gameObject;
        public Transform parent;
        public Vector3 position;
        public Vector3 localScale = new Vector3(1f, 1f, 1f);
        private readonly List<Transform> children = new List<Transform>();
        public Transform(GameObject gameObject) { this.gameObject = gameObject; }
        public void Add(Transform child) { child.parent = this; children.Add(child); }
        public int childCount { get { return children.Count; } }
        public Transform GetChild(int index) { return children[index]; }
        public T GetComponent<T>() where T : class { return returnComponent<T>(); }
        private T returnComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
    }
    public class Collider : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public bool enabled = true;
        public Bounds bounds;
        public Collider(GameObject gameObject) { this.gameObject = gameObject; gameObject.collider = this; }
    }
    public class Renderer : Object
    {
        public Bounds bounds;
    }
    public struct Bounds { public Vector3 center; public Vector3 size; }
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public Vector3(float x, float y, float z = 0f) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public static Vector3 operator -(Vector3 a, Vector3 b)
        { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
    }
    public struct Ray { public Vector3 origin; public Ray(Vector3 origin) { this.origin = origin; } }
    public struct RaycastHit { public Collider collider; }
    public static class Physics
    {
        public static Collider Hit;
        public static bool Raycast(Ray ray, out RaycastHit hit, float distance)
        {
            hit = new RaycastHit { collider = Hit };
            return Hit != null;
        }
        public static bool Raycast(Ray ray, out RaycastHit hit, float distance, int mask)
        {
            hit = new RaycastHit { collider = Hit };
            return Hit != null && (mask & (1 << Hit.gameObject.layer)) != 0;
        }
    }
    public class Camera : Object
    {
        public static Camera main = new Camera();
        public Ray ScreenPointToRay(Vector3 point) { return new Ray(point); }
        public Vector3 WorldToScreenPoint(Vector3 point) { return point; }
        public Vector3 ScreenToWorldPoint(Vector3 point) { return point; }
    }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool left, right;
        public static bool GetMouseButtonDown(int button) { return button == 0 ? left : right; }
    }
    public static class Time { public static float unscaledTime; public static int frameCount; }
    public static class Mathf
    {
        public static float Abs(float value) { return Math.Abs(value); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
    }
}

public static class MenuScript { public static UnityEngine.GameObject player = new UnityEngine.GameObject("player"); }
public partial class GameScript : UnityEngine.Object
{
    public int PageCalls;
    public void SetStorageLevel(int level) { storageLevel = level; }
    public void SelectPage(int page) { if (page <= storageLevel) { currentStoragePage = page; PageCalls++; } }
    public static bool inventoryOpen = true, pausing, dead, buildMode;
    public UnityEngine.GameObject hoverItem, menuEmblemForge, cursorEmblemForge;
    public UnityEngine.GameObject menuTrasher, menuPrism;
    public bool emblemAgain, trashAgain, prismAgain;
    public int TrashCalls, WorthCalls, PrismCalls;
    public Func<int,int> ItemValue = id => 2;
    public int GetItemWorth(int id) { WorthCalls++; return ItemValue(id); }
    public void Prismer() { prisming=!prisming; ToggleCalls++; }
    public void Trasher() { trashing=!trashing; ToggleCalls++; }
    public int ForgeCalls, ToggleCalls, slotID=-1;
    public void EmblemForging() { emblemForging=!emblemForging; ToggleCalls++; }
    public UnityEngine.GameObject inventoryMain, inventoryBar, menuStorage, holdingItemObj,
        holdingItemIcon;
    public UnityEngine.GameObject[] storageButton = new UnityEngine.GameObject[12];
    private int storageLevel = 2;
    public Item holdingItem = new Item();
    public Item[] inventory = new Item[45], storage = new Item[432];
    public bool shifting, emblemForging, trashing, prisming;
    public int Credits = 1000, UpgradeCost = 500;
    public int GetCredits() { return Credits; }
    public int GetStorageUpgradeCost(int level) { return UpgradeCost; }
    public bool CraftAllowed = true;
    public Item[] CraftItems;
    public int CraftEligibilityCalls, NativeSameTierErrors;
    public bool CanPlaceCraft(int id)
    {
        CraftEligibilityCalls++;
        // Native GameScript.CanPlaceCraft validates by emitting Error(4/5).
        // It is an action validator, not a side-effect-free UI query.
        if (CraftItems != null)
        {
            for (int i=0;i<3;i++)
            {
                if (CraftItems[i].id == id) return false;
                if (CraftItems[i].id > 0 && CraftItems[i].id % 10 != id % 10)
                { NativeSameTierErrors++; ControllerSupport.ControllerMenuSelection.NativeError(this,5); return false; }
            }
        }
        return CraftAllowed;
    }
    public int TransferInventoryCalls, TransferStorageCalls, TransferCraftCalls, SwapCalls, SwapStorageCalls;
    public int LastNativeSlot = -1, CoroutineSteps;
    public Item CraftReceived;
    public void StartCoroutine(IEnumerator action) { if (action.MoveNext()) CoroutineSteps++; }
    private int StorageIndex(int slot)
    { return slot < 30 ? currentStoragePage * 30 + slot : 360 + currentStoragePage * 6 + slot - 30; }
    // Execute mutation on the first coroutine step, as native dispatch does.
    // Keep object identity so routing tests can detect metadata loss and duplication.
    public IEnumerator ShiftClick(int slot)
    {
        TransferInventoryCalls++; LastNativeSlot = slot;
        for (int i=0;i<36;i++)
        {
            int target=StorageIndex(i);
            if (storage[target] == null || storage[target].id == 0)
            { storage[target]=inventory[slot]; inventory[slot]=new Item(); break; }
        }
        yield return null;
    }
    public IEnumerator ShiftClickStorage(int slot)
    {
        TransferStorageCalls++; LastNativeSlot = slot;
        int source=StorageIndex(slot);
        for (int i=6;i<36;i++)
            if (inventory[i] == null || inventory[i].id == 0)
            { inventory[i]=storage[source]; storage[source]=new Item(); break; }
        yield return null;
    }
    public IEnumerator ShiftClick2(int slot)
    {
        TransferCraftCalls++; LastNativeSlot = slot;
        // Native Update validates before invoking ShiftClick2; the coroutine
        // and GadgetCore replacement do not repeat CanPlaceCraft themselves.
        CraftReceived=inventory[slot]; inventory[slot]=new Item();
        yield return null;
    }
    public void SwapItem(int slot)
    { SwapCalls++; LastNativeSlot=slot; Item previous=inventory[slot]; inventory[slot]=holdingItem; holdingItem=previous; }
    public void SwapItemStorage(int slot)
    { SwapStorageCalls++; LastNativeSlot=slot; int index=StorageIndex(slot); Item previous=storage[index]; storage[index]=holdingItem; holdingItem=previous; }
    public UnityEngine.GameObject menuCraft, menuRecipe;
    public int craftType, curRecipePage, RecipeUpCalls, RecipeDownCalls;
    public void RecipeUp() { RecipeUpCalls++; curRecipePage=(curRecipePage+1)%(craftType==0?6:2); }
    public void RecipeDown() { RecipeDownCalls++; int pages=craftType==0?6:2; curRecipePage=(curRecipePage+pages-1)%pages; }
    public bool canCraft, crafting;
    public Item[] craft = {new Item(),new Item(),new Item(),new Item()};
    public int[,] recipeUnlocked1 = new int[2,12];
    public int[,] recipeUnlocked0 = new int[10,12];
    public int GearRecipeLookupCalls;
    // Source-backed native visual-page to unlock-row mapping; production
    // context must call the native method rather than index a visual page.
    public int GetRecipeUnlocked0(int page,int slot)
    {
        GearRecipeLookupCalls++;
        int[] firstRows={0,2,4,6,7,8}; int row=firstRows[page];
        if(page<4&&slot>5) { row=page==3?9:row+1; slot-=6; }
        return recipeUnlocked0[row,slot];
    }
    public int TransferCraftOutputCalls, SwapCraftCalls;
    public IEnumerator ShiftClickCraft(int slot)
    {
        TransferCraftOutputCalls++; LastNativeSlot=slot;
        for(int i=0;i<36;i++) if(inventory[i]==null||inventory[i].id==0)
        { inventory[i]=craft[slot]; craft[slot]=new Item(); break; }
        yield return null;
    }
    public void SwapItemCraft(int slot)
    { SwapCraftCalls++; LastNativeSlot=slot; Item previous=craft[slot]; craft[slot]=holdingItem; holdingItem=previous; }
    public int currentStoragePage, CloseCalls, PauseCalls;
    public void CloseAllMenus() { CloseCalls++; inventoryOpen = false; }
    public void PauseMenu() { PauseCalls++; pausing = true; }
    public void SetHeldItem(int id) { holdingItem.id = id; holdingItem.q = id == 0 ? 0 : 1; }
}
public class Item { public string extraData; public int id, q, exp, tier, corrupted; public int[] aspect, aspectLvl; }
public class ActionCursor
{
    public float z;
    public UnityEngine.GameObject gameObject;
    public UnityEngine.Transform transform { get { return gameObject.transform; } }
}
namespace ControllerSupport
{
    internal sealed class SelectionCornerRenderer
    {
        internal UnityEngine.Bounds? HeldItemBounds { get { return null; } }
        internal static SelectionCornerRenderer Latest;
        internal static int CreateCount;
        internal UnityEngine.Collider ShowTarget;
        internal int HeldIconId;
        internal bool HeldIconIsChip;
        internal int HeldIconSetCount;
        internal SelectionCornerStyle Style;
        internal void SetStyle(SelectionCornerStyle style) { Style=style; }
        internal bool IsAlive { get { return true; } }
        internal static SelectionCornerRenderer TryCreate(float scale, int order,
            Action<string> warning)
        {
            CreateCount++;
            return Latest = new SelectionCornerRenderer();
        }
        internal void Show(UnityEngine.Collider target) { ShowTarget = target; }
        internal float HeldIconWidth, HeldIconHeight;
        internal void SetHeldItemIcon(int id, float width, float height, bool isChip = false)
        { HeldIconId = id; HeldIconIsChip=isChip; HeldIconWidth = width; HeldIconHeight = height; HeldIconSetCount++; }
        internal void Hide() { ShowTarget = null; HeldIconId = 0; }
        internal void Dispose() { }
    }
}
