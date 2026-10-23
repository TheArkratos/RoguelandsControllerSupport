using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(); } }
    }
    public sealed class Transform { public Vector3 position; }
    public class GameObject
    {
        public string name;
        public bool activeSelf = true;
        public bool activeInHierarchy { get { return activeSelf; } }
        public readonly Transform transform = new Transform();
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<Light> lights = new List<Light>();
        public GameObject(string name = "") { this.name = name; }
        public T[] GetComponentsInChildren<T>(bool includeInactive)
        {
            if (typeof(T) == typeof(Renderer)) return renderers.ToArray() as T[];
            if (typeof(T) == typeof(Light)) return lights.ToArray() as T[];
            throw new Exception("Unexpected component type");
        }
        public static implicit operator bool(GameObject obj) { return obj != null; }
    }
    public sealed class Renderer
    {
        public bool enabled = true;
        public static implicit operator bool(Renderer obj) { return obj != null; }
    }
    public sealed class Light
    {
        public bool enabled = true;
        public static implicit operator bool(Light obj) { return obj != null; }
    }
    public class MonoBehaviour
    {
        public readonly GameObject gameObject = new GameObject();
        public Transform transform { get { return gameObject.transform; } }
        public static implicit operator bool(MonoBehaviour obj) { return obj != null; }
    }
    public static class Input { public static Vector3 mousePosition = new Vector3(25, 15, 0); }
    public sealed class Camera
    {
        public static Camera main = new Camera();
        public Vector3 ScreenToWorldPoint(Vector3 point) { return point; }
    }
}

public sealed class GameScript
{
    public static bool inventoryOpen = true;
    public UnityEngine.GameObject cursorTrasher, cursorEmblemForge, cursorPrism, holdingItemObj;
}
public static class Menuu { public static bool usingController; }
public static class ControllerCursor { public static float staticX = 999, staticY = 20; }

namespace ControllerSupport
{
    // Neither combat module owns aim in these UI tests. The native bridge must
    // repair a flag set by the game itself without depending on combat history.
    internal static class ControllerAim
    {
        internal static bool TryWorldPoint(out UnityEngine.Vector3 point)
        { point = UnityEngine.Vector3.zero; return false; }
    }
    internal static class ControllerExploration
    {
        internal static bool TryWorldPoint(out UnityEngine.Vector3 point)
        { point = UnityEngine.Vector3.zero; return false; }
    }
}
