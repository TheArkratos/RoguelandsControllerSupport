using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 right { get { return new Vector2(1f, 0f); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized
        {
            get
            {
                float length = (float)Math.Sqrt(sqrMagnitude);
                return length > 0f ? new Vector2(x / length, y / length) : new Vector2(0f, 0f);
            }
        }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
    }

    public static class Input
    {
        public static Vector3 mousePosition { get; set; }
        public static bool MouseButtonDown0;
        public static bool GetMouseButtonDown(int button) { return button == 0 && MouseButtonDown0; }
    }

    public static class Application { public static int loadedLevel; }

    public static class Time
    {
        public static int frameCount;
        public static float unscaledTime;
        public static float unscaledDeltaTime = 1f / 60f;
        public static float deltaTime = 1f / 60f;
    }

    public class Camera
    {
        public static Camera main = new Camera();
        public Vector3 WorldToScreenPoint(Vector3 point) { return point; }
        public Vector3 ScreenToWorldPoint(Vector3 point) { return point; }
    }

    public class Transform
    {
        public Vector3 position;
        public Vector3 eulerAngles;
    }

    public class NetworkView
    {
        public static bool Owned=true; public bool isMine { get { return Owned; } }
        public static implicit operator bool(NetworkView view) { return view != null; }
    }

    public class GameObject
    {
        public string name;
        public bool activeInHierarchy=true;
        public T[] GetComponentsInChildren<T>(bool includeInactive) { return new T[0]; }
        public Transform transform = new Transform();
        public static implicit operator bool(GameObject obj) { return obj != null; }
        public global::PlayerScript Player;
        public GameObject(bool withPlayer=true) { if(withPlayer)Player=new global::PlayerScript(); }
        public T GetComponent<T>() where T : class { return typeof(T)==typeof(global::PlayerScript) ? Player as T : new NetworkView() as T; }
    }

    public class Renderer { public bool enabled=true; public static implicit operator bool(Renderer obj) { return obj!=null; } }
    public class Light { public bool enabled=true; public static implicit operator bool(Light obj) { return obj!=null; } }

    public class MonoBehaviour
    {
        public GameObject gameObject = new GameObject();
        public Transform transform { get { return gameObject.transform; } }
    }
}

public static class Menuu
{
    public static bool usingController;
}

public static class ControllerCursor
{
    public static float staticX, staticY;
}

public class GameScript
{
    public UnityEngine.GameObject cursorTrasher,cursorEmblemForge,cursorPrism;
    public static int[] equippedIDs = new[] { 327 };
    public static bool combatMode = true, dead, pausing, buildMode, inventoryOpen, talking;
}

public static class MenuScript
{
    public static UnityEngine.GameObject player = new UnityEngine.GameObject();
}

namespace ControllerSupport
{
    internal static class ControllerExploration
    {
        internal static bool TryWorldPoint(out UnityEngine.Vector3 point)
        {
            point = UnityEngine.Vector3.zero;
            return false;
        }
    }

}
