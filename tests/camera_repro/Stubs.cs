using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 right => new Vector2(1f, 0f);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float n = (float)Math.Sqrt(sqrMagnitude); return n > 0 ? new Vector2(x / n, y / n) : new Vector2(0, 0); } }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }
    public static class Input
    {
        public static Vector3 mousePosition { get; set; }
        public static bool GetMouseButtonDown(int button) => false;
    }
    public static class Time
    {
        public static int frameCount;
        public static float unscaledTime;
        public static float unscaledDeltaTime = 1f / 60f;
        public static float smoothDeltaTime = 1f / 60f;
    }
    public class Transform
    {
        public Vector3 position;
        public static implicit operator bool(Transform value) => value != null;
    }
    public class GameObject
    {
        public Transform transform = new Transform();
        public object GetComponent(string name) => null;
        public T GetComponent<T>() where T : class => new NetworkView() as T;
        public static implicit operator bool(GameObject value) => value != null;
    }
    public class MonoBehaviour
    {
        public GameObject gameObject = new GameObject();
        public Transform transform => gameObject.transform;
    }
    public class Camera
    {
        public static Camera main = new Camera();
        public GameObject gameObject;
        public Vector3 ScreenToWorldPoint(Vector3 point) => point;
        public Vector3 WorldToScreenPoint(Vector3 point) => point;
    }
    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogWarning(object message) => Console.WriteLine("WARNING: " + message);
    }
    public class NetworkView
    {
        public bool isMine = true;
        public static implicit operator bool(NetworkView value) => value != null;
    }
}
public class MenuScript { public static UnityEngine.GameObject player = new UnityEngine.GameObject(); }
public class Menuu { public static bool usingController; }
public class GameScript
{
    public static bool combatMode = true, dead, pausing, buildMode, inventoryOpen, talking;
}
namespace ControllerSupport
{
    internal static class ControllerGameplayInput
    {
        internal static bool CloseAimHeld() => false;
    }
}
namespace GadgetCore.API
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyGadgetAttribute : Attribute
    {
        public HarmonyGadgetAttribute(string name) { }
    }
}
