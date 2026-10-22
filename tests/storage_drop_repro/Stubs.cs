using System;
using System.Collections.Generic;
using System.Collections;

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
        public string name, tag = "Untagged";
        public bool activeInHierarchy = true;
        public void SetActive(bool active) { activeInHierarchy=active; }
        public int layer = 16;
        public Transform transform;
        public Collider collider;
        public Renderer renderer;
        public NetworkView view = new NetworkView();
        public GameObject(string name = "") { this.name = name; transform = new Transform(this); }
        public T GetComponent<T>() where T : class { return collider as T ?? renderer as T ?? view as T; }
        public T[] GetComponentsInChildren<T>() where T : class
        {
            var found = new List<T>();
            foreach (Transform child in transform.Children)
            {
                if (child.gameObject.collider is T item) found.Add(item);
                found.AddRange(child.gameObject.GetComponentsInChildren<T>());
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
        public readonly List<Transform> Children = new List<Transform>();
        public Transform(GameObject gameObject) { this.gameObject = gameObject; }
        public void Add(Transform child) { child.parent = this; Children.Add(child); }
        public int childCount { get { return Children.Count; } }
        public Transform GetChild(int index) { return Children[index]; }
        public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
    }
    public class Collider : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public bool enabled = true;
        public Bounds bounds;
        public Collider(GameObject gameObject) { this.gameObject = gameObject; gameObject.collider = this; }
    }
    public class BoxCollider : Collider
    {
        public Vector3 size, center;
        public BoxCollider(GameObject gameObject) : base(gameObject) { }
    }
    public class Renderer : Object
    {
        public Bounds bounds;
    }
    public struct Bounds { public Vector3 center, size; }
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public Vector3(float x, float y, float z = 0f) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public static Vector3 operator -(Vector3 a, Vector3 b)
        { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
    }
    public struct Ray { public Vector3 origin; public Ray(Vector3 point) { origin = point; } }
    public struct RaycastHit { public Collider collider; }
    public static class Physics
    {
        public static Collider InventorySlot, DropZone;
        public static bool Raycast(Ray ray, out RaycastHit hit, float distance)
        {
            hit = new RaycastHit { collider = ray.origin.x >= 100 ? DropZone : InventorySlot };
            return hit.collider != null;
        }
        public static bool Raycast(Ray ray, out RaycastHit hit, float distance, int mask)
        {
            bool found = Raycast(ray, out hit, distance);
            return found && (mask & (1 << hit.collider.gameObject.layer)) != 0;
        }
    }
    public class Camera : Object
    {
        public static Camera main = new Camera();
        public Ray ScreenPointToRay(Vector3 point) { return new Ray(point); }
        public Vector3 WorldToScreenPoint(Vector3 point) { return point; }
        public Vector3 ScreenToWorldPoint(Vector3 point) { return point; }
    }
    public enum KeyCode { Escape }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool left, right;
        public static readonly HashSet<string> ButtonsDown = new HashSet<string>();
        public static readonly HashSet<KeyCode> KeysDown = new HashSet<KeyCode>();
        public static bool GetButtonDown(string button) { return ButtonsDown.Contains(button); }
        public static bool GetKeyDown(KeyCode key) { return KeysDown.Contains(key); }
        public static bool GetMouseButtonDown(int button) { return button == 0 ? left : right; }
    }
    public static class Time { public static float unscaledTime; public static int frameCount; }
    public static class Mathf
    {
        public static float Abs(float value) { return Math.Abs(value); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
    }
    public static class Debug
    {
        public static void Log(string message) { }
        public static void LogWarning(string message) { }
    }
}

public class Menuu : UnityEngine.Object
{
    public UnityEngine.GameObject menuDeleteChar, menuStuffSelect, menuCreateCharacter;
    public UnityEngine.GameObject menuCharSelect, menuOptions, menuData, menuMain;
}
public class MenuScript : UnityEngine.Object
{
    public static UnityEngine.GameObject player = new UnityEngine.GameObject("player");
    public static bool inGame;
    public UnityEngine.GameObject menuServer, menu;
}
public class GameScript : UnityEngine.Object
{
    public void SelectPage(int page) { if (page <= storageLevel) currentStoragePage = page; }
    public static bool inventoryOpen = true, pausing, dead, buildMode;
    public UnityEngine.GameObject menuPause, inventoryMain, inventoryBar, menuStorage,
        holdingItemObj, holdingItemIcon;
    public UnityEngine.GameObject[] storageButton = new UnityEngine.GameObject[12];
    private int storageLevel = 2;
    public Item holdingItem = new Item();
    public Item[] inventory = new Item[45], storage = new Item[432];
    public bool shifting;
    public UnityEngine.GameObject menuCraft;
    public int currentStoragePage, CloseCalls, PauseCalls;
    // This fixture exercises native click dispatch, not transfers or crafting.
    // Fail loudly if production routing unexpectedly reaches those paths.
    public void StartCoroutine(IEnumerator routine) { while (routine.MoveNext()) { } }
    public IEnumerator ShiftClickStorage(int slot) { throw new NotSupportedException("Unexpected storage transfer in click-dispatch fixture"); }
    public IEnumerator ShiftClick(int slot) { throw new NotSupportedException("Unexpected inventory transfer in click-dispatch fixture"); }
    public IEnumerator ShiftClick2(int slot) { throw new NotSupportedException("Unexpected crafting transfer in click-dispatch fixture"); }
    public bool CanPlaceCraft(int id) { return false; }
    public void SwapItem(int slot)
    { Item previous = inventory[slot]; inventory[slot] = holdingItem; holdingItem = previous; }
    public void SwapItemStorage(int slot)
    {
        int index = slot < 30 ? currentStoragePage * 30 + slot : 360 + currentStoragePage * 6 + slot - 30;
        Item previous = storage[index]; storage[index] = holdingItem; holdingItem = previous;
    }
    public void CloseAllMenus() { CloseCalls++; inventoryOpen = false; }
    public void PauseMenu() { PauseCalls++; pausing = true; }
}
public class Item { public int id, q, exp, tier, corrupted; public int[] aspect, aspectLvl; }
public class ActionCursor
{
    public float z;
    public UnityEngine.GameObject gameObject;
    public UnityEngine.Transform transform { get { return gameObject.transform; } }
}
namespace ControllerSupport
{
    internal sealed class JoystickInputState
    {
        internal bool Connected = true;
        internal float MoveX, MoveY, AimX, AimY;
        internal bool LeftTriggerHeld, RightTriggerHeld, LeftTriggerPressed, RightTriggerPressed;
        internal bool Held, Pressed;
        internal bool ButtonHeld(int number) { return number == 0 && Held; }
        internal bool ButtonPressed(int number) { return number == 0 && Pressed; }
        internal bool DPadHeld(string token) { return false; }
        internal bool DPadPressed(string token) { return false; }
    }
    internal sealed class SelectionCornerRenderer
    {
        internal UnityEngine.Bounds? HeldItemBounds { get { return null; } }
        internal bool IsAlive { get { return true; } }
        internal static SelectionCornerRenderer TryCreate(float scale, int order,
            Action<string> warning) { return new SelectionCornerRenderer(); }
        internal void Show(UnityEngine.Collider target) { }
        internal void SetHeldItemIcon(int id, float width, float height) { }
        internal void Hide() { }
        internal void Dispose() { }
    }
}
namespace GadgetCore.API
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyGadgetAttribute : Attribute
    { public HarmonyGadgetAttribute(string name) { } }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    { public HarmonyPatch(Type type, string method) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyTranspiler : Attribute { }
    public class CodeInstruction
    {
        public object operand;
        public CodeInstruction(CodeInstruction other) { operand = other.operand; }
    }
}
