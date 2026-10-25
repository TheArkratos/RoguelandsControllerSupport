using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) { return value != null; }
    }
    public class GameObject : Object
    {
        private readonly List<GameObject> children = new List<GameObject>();
        public string name;
        public bool activeInHierarchy = true;
        public int layer;
        public Collider collider;
        private readonly List<Component> components = new List<Component>();
        public T AddComponent<T>() where T : Component, new() { var value = new T { gameObject = this }; components.Add(value); return value; }
        public T GetComponent<T>() where T : class { if (collider is T match) return match; return components.Find(c => c is T) as T; }
        public void SetActive(bool active) { activeInHierarchy = active; }
        public GameObject(string name) { this.name = name; }
        public void AddChild(GameObject child) { children.Add(child); }
        public T[] GetComponentsInChildren<T>() where T : class
        {
            var found = new List<T>();
            foreach (GameObject child in children)
            {
                if (child.collider is T item) found.Add(item);
                found.AddRange(child.GetComponentsInChildren<T>());
            }
            return found.ToArray();
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
    }
    public class MonoBehaviour : Component { public void StartCoroutine(System.Collections.IEnumerator routine) { } }
    public class Material : Object { }
    public class Renderer : Component { public Material material; }
    public class AudioClip : Object { }
    public class AudioSource : Component { public int Plays; public void PlayOneShot(AudioClip clip, float volume = 1f) { Plays++; } }
    public class Animation : Component { public int Plays; public void Play(string name = null) { Plays++; } public void Stop() { } }
    public class TextMesh : Component { }
    public class WaitForSeconds { public WaitForSeconds(float seconds) { } }
    public static class Random { public static int Range(int min, int max) { return min; } }
    public static class Resources { public static Object Load(string path) { return new AudioClip(); } }
    public class Collider : Object
    {
        public GameObject gameObject;
        public bool enabled = true;
        public virtual Bounds bounds { get; set; }
        public Collider(GameObject gameObject)
        { this.gameObject = gameObject; gameObject.collider = this; }
    }
    public class BoxCollider : Collider
    {
        public Vector3 size, center, position;
        public Vector3 scale = new Vector3(8f, 8f, 1f);
        public BoxCollider(GameObject gameObject) : base(gameObject) { }
        public override Bounds bounds
        {
            get { return new Bounds {
                center = new Vector3(position.x + center.x * scale.x,
                    position.y + center.y * scale.y, position.z + center.z * scale.z),
                size = new Vector3(size.x * scale.x, size.y * scale.y, size.z * scale.z) }; }
            set { throw new InvalidOperationException("Set local size/center on boxes"); }
        }
    }
    public struct Bounds { public Vector3 center, size; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z = 0f) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public static Vector3 operator -(Vector3 a, Vector3 b)
        { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
    }
    public struct Ray { }
    public struct RaycastHit { public Collider collider; }
    public static class Physics
    {
        public static Collider Hit;
        public static bool Raycast(Ray ray, out RaycastHit hit, float distance)
        { hit = new RaycastHit { collider=Hit }; return Hit!=null; }
    }
    public class Camera : Object
    {
        public static Camera main = new Camera();
        public Ray ScreenPointToRay(Vector3 position) { return new Ray(); }
        public Vector3 WorldToScreenPoint(Vector3 position) { return position; }
    }
    public static class Input
    {
        public static Vector3 mousePosition { get; set; }
        public static bool NativeCancel, EscapeDown, Left, Right;
        public static bool GetButtonDown(string name) { return NativeCancel; }
        public static bool GetKeyDown(KeyCode key) { return EscapeDown; }
        public static bool GetMouseButtonDown(int button) { return button==0?Left:Right; }
    }
    public enum KeyCode { Escape }
    public static class Time { public static float unscaledTime; }
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
    public static float soundLevel = 10;
    public UnityEngine.GameObject menuDeleteChar, menuStuffSelect, menuCreateCharacter;
    public UnityEngine.GameObject menuCharSelect, menuOptions, menuData, menuMain;
}
public class MenuScript : UnityEngine.Object
{
    public static bool inGame;
    public UnityEngine.GameObject menuServer, menu;
}
public class GameScript : UnityEngine.Object
{
    public static bool pausing;
    public UnityEngine.GameObject menuPause;
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
    { public HarmonyPatch() { } public HarmonyPatch(Type type, string method) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyTargetMethods : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyTranspiler : Attribute { }
    public class CodeInstruction
    {
        public object operand;
        public CodeInstruction(object value) { operand=value; }
        public CodeInstruction(CodeInstruction other) { operand = other.operand; }
    }
}

namespace ControllerSupport
{
    internal static class ControllerMenuSelection
    {
        internal static UnityEngine.Vector3 VirtualMousePosition()
        { return UnityEngine.Input.mousePosition; }
        internal static bool VirtualMouseButtonDown(int button)
        { return UnityEngine.Input.GetMouseButtonDown(button); }
    }
    internal class SelectionCornerRenderer
    {
        internal SelectionCornerStyle Style;
        internal void SetStyle(SelectionCornerStyle style) { Style=style; }
        public bool IsAlive { get { return true; } }
        public static SelectionCornerRenderer TryCreate(float scale, int order, Action<string> warn)
        { return new SelectionCornerRenderer(); }
        public static UnityEngine.Collider Visible;
        public void Show(UnityEngine.Collider collider) { Visible = collider; }
        public void Hide() { Visible = null; }
        public void Dispose() { Visible = null; }
    }
}
