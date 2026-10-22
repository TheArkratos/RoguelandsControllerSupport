using System;

namespace UnityEngine
{
    public class Object
    {
        public bool exists = true;
        private static int nextId;
        private readonly int id = ++nextId;
        public int GetInstanceID() { return id; }
        public static implicit operator bool(Object value) { return value != null && value.exists; }
        public static T[] FindObjectsOfType<T>() where T : Object
        { return ControllerSupport.ExplorationFixture.Objects as T[]; }
    }

    public class Transform
    {
        public Vector3 position;
    }

    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        public bool activeSelf = true;
        public Transform transform = new Transform();
        public NetworkView view = new NetworkView();
        public void SetActive(bool value) { activeSelf = value; }
        public T GetComponent<T>() where T : class { return view as T; }
    }

    public class NetworkView : Object { public bool isMine = true; }

    public class Camera : Object
    {
        public static Camera main;
        public Transform transform = new Transform();
        public float orthographicSize = 15f;
        public float aspect = 16f / 9f;

        public Vector3 WorldToViewportPoint(Vector3 point)
        {
            float halfWidth = orthographicSize * aspect;
            return new Vector3(
                0.5f + (point.x - transform.position.x) / (2f * halfWidth),
                0.5f + (point.y - transform.position.y) / (2f * orthographicSize),
                point.z - transform.position.z);
        }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 operator -(Vector3 a, Vector3 b)
        { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static implicit operator Vector2(Vector3 value) { return new Vector2(value.x, value.y); }
    }

    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool LeftDown, RightDown;
        public static bool GetMouseButtonDown(int button) { return button == 0 ? LeftDown : RightDown; }
    }

    public static class Time { public static float unscaledTime; }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { float m = magnitude; return m > 0f ? this / m : new Vector2(); } }
        public void Normalize() { this = normalized; }
        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator /(Vector2 a, float b) { return new Vector2(a.x / b, a.y / b); }
    }
}

public class ObjectScript : UnityEngine.Object
{
    public int id;
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public UnityEngine.Transform transform { get { return gameObject.transform; } }
}

public class Item { public int id; }

public class GameScript : UnityEngine.Object
{
    private bool inShip;
    private Item[] inventory = new Item[45];
    public static bool inInstance = true, isTown, combatMode, buildMode, dead,
        pausing, inventoryOpen, talking;
    public bool[] activated = new bool[3];
    public UnityEngine.GameObject[] droid = new UnityEngine.GameObject[3];
    public UnityEngine.GameObject actionCursor = new UnityEngine.GameObject();
    public bool activateSucceeds = true;
    public int activateCalls;
    public GameScript()
    {
        for (int i = 0; i < inventory.Length; i++) inventory[i] = new Item();
        for (int i = 0; i < droid.Length; i++) droid[i] = new UnityEngine.GameObject();
        inventory[42].id = 1000;
    }
    public void SetInShip(bool value) { inShip = value; }
    public void SetDroidItem(int value) { inventory[42].id = value; }
    public void ActivateDroid(int slot)
    {
        activateCalls++;
        if (activateSucceeds) activated[slot] = true;
    }
}

public static class MenuScript { public static UnityEngine.GameObject player; }
public static class Menuu { public static bool usingController; }
public static class ControllerCursor { public static float staticX, staticY; }

namespace ControllerSupport
{
    internal static class ExplorationFixture { internal static ObjectScript[] Objects = new ObjectScript[0]; }

    internal class JoystickInputState
    {
        public bool Connected = true;
        public float AimX, AimY;
    }

    internal static class ControllerGameplayInput
    {
        internal static bool Pressed;
        internal static bool ExplorationPrimaryHeld() { return Pressed; }
        internal static bool ExplorationPrimaryPressed() { return Pressed; }
        internal static bool ExplorationContextSwitchPressed() { return false; }
    }
}
