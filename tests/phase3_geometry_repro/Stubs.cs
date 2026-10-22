using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) { return value != null; }
    }
    public class GameObject : Object
    {
        public string name;
        public string tag = "Untagged";
        public bool activeInHierarchy = true;
        public int layer = 16;
        public Transform transform;
        public Collider collider;
        public GameObject(string name = "") { this.name = name; transform = new Transform(this); }
        public T GetComponent<T>() where T : class { return collider as T; }
    }
    public class Transform : Object
    {
        public GameObject gameObject;
        public Transform parent;
        private readonly List<Transform> children = new List<Transform>();
        public Transform(GameObject gameObject) { this.gameObject = gameObject; }
        public void Add(Transform child) { child.parent = this; children.Add(child); }
        public int childCount { get { return children.Count; } }
        public Transform GetChild(int index) { return children[index]; }
        public Transform Find(string name)
        {
            foreach (Transform child in children)
                if (child.gameObject.name == name) return child;
            return null;
        }
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
    public struct Bounds
    {
        public Vector3 center;
        public Vector3 size;
    }
    public struct Vector3
    {
        public float x, y, z;
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
    }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool GetMouseButtonDown(int button) { return false; }
    }
    public static class Time { public static float unscaledTime; }
    public static class Mathf { public static float Abs(float value) { return Math.Abs(value); } }
}

public class GameScript : UnityEngine.Object
{
    public static bool inventoryOpen = true;
    public UnityEngine.GameObject inventoryMain;
    public UnityEngine.GameObject inventoryBar;
    public UnityEngine.GameObject menuStorage;
}

namespace ControllerSupport
{
    internal class JoystickInputState
    {
        public bool Connected = true;
        public float AimX, AimY;
    }
}
