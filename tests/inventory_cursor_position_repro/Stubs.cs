namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) { return value != null; } }
    public class GameObject : Object { public string name; public bool activeInHierarchy = true; public Transform transform = new Transform(); public T[] GetComponentsInChildren<T>(bool includeInactive) { return new T[0]; } }
    public class Transform { public Vector3 position; }
    public class Renderer : Object { public bool enabled = true; }
    public class Light : Object { public bool enabled = true; }
    public class MonoBehaviour : Object { public GameObject gameObject = new GameObject(); public Transform transform { get { return gameObject.transform; } } }
    public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) { x=a;y=b;z=c; } }
    public struct Bounds { public Vector3 center; }
    public class Collider : Object { public Bounds bounds; }
    public class Camera : Object { public static Camera main = new Camera(); public Vector3 ScreenToWorldPoint(Vector3 v) { return new Vector3(v.x / 10, v.y / 10, 0); } }
    public static class Input { public static Vector3 mousePosition; }
}
public static class Menuu { public static bool usingController; }
public static class ControllerCursor { public static float staticX, staticY; }
public class GameScript { public static bool inventoryOpen; public UnityEngine.GameObject holdingItemObj, cursorTrasher, cursorEmblemForge, cursorPrism; }
