using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) { return value != null; }
        public static void Destroy(Object value) { }
    }
    public class GameObject : Object
    {
        private readonly Dictionary<Type, Object> components = new Dictionary<Type, Object>();
        public bool activeSelf = true;
        public void Add(Object component) { components[component.GetType()] = component; }
        public T GetComponent<T>() where T : Object
        {
            foreach (Object component in components.Values)
                if (component is T result) return result;
            return null;
        }
        public T GetComponentInChildren<T>(bool includeInactive) where T : Object
        { return GetComponent<T>(); }
    }
    public class Renderer : Object { public Material sharedMaterial; }
    public class SpriteRenderer : Renderer { public Sprite sprite; }
    public class Material : Object
    {
        public Texture2D mainTexture;
        public Material() { }
        public Material(Material source) { mainTexture = source.mainTexture; }
    }
    public class Sprite : Object
    {
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float ppu)
        { return new Sprite { rect = rect, pivot = new Vector2(pivot.x * rect.width, pivot.y * rect.height), pixelsPerUnit = ppu }; }
    }
    public enum TextureFormat { ARGB32 }
    public enum FilterMode { Point }
    public enum TextureWrapMode { Clamp }
    public class Texture2D : Object
    {
        public int width, height;
        private readonly Color[,] pixels;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Texture2D(int width, int height, TextureFormat format, bool mipmap)
        { this.width = width; this.height = height; pixels = new Color[width, height]; }
        public void SetPixel(int x, int y, Color color) { pixels[x, y] = color; }
        public Color GetPixel(int x, int y) { return pixels[x, y]; }
        public void Apply() { }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a)
        { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Vector2 size { get { return new Vector2(width, height); } }
        public Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
}

public class PlayerScript : UnityEngine.Object { public UnityEngine.GameObject w; }
public static class MenuScript { public static UnityEngine.GameObject player; }
