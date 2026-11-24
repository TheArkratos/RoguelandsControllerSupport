using System;
using System.Collections.Generic;

// Authored fixture boundaries only; no Unity, GadgetCore or game assemblies.
namespace GadgetCore
{
    public sealed class GadgetLogger
    {
        public void Log(string message) { }
        public void LogWarning(string message, bool ignored) { }
    }
}

namespace UnityEngine
{
    public enum KeyCode { None, Space, Q, E, Mouse0, Mouse1, JoystickButton0, Joystick1Button0 }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public static Vector3 operator -(Vector3 a, Vector3 b)
        { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
    }

    public static class Mathf
    {
        public static float Abs(float value) { return Math.Abs(value); }
    }

    public static class Time
    {
        public static int frameCount;
        public static float unscaledTime;
    }

    public static class Input
    {
        public static Vector3 mousePosition;
        internal static readonly HashSet<KeyCode> KeysDown = new HashSet<KeyCode>();
        internal static readonly HashSet<int> MouseDown = new HashSet<int>();
        internal static int NameReads, AxisReads, KeyUpReads, JoystickKeyReads;
        public static bool GetKeyDown(KeyCode key)
        {
            if (key.ToString().StartsWith("Joystick", StringComparison.Ordinal)) JoystickKeyReads++;
            return KeysDown.Contains(key);
        }
        public static bool GetMouseButtonDown(int button) { return MouseDown.Contains(button); }
        public static string[] GetJoystickNames() { NameReads++; return new string[0]; }
        public static bool GetKeyUp(KeyCode key) { KeyUpReads++; return false; }
        public static float GetAxisRaw(string axis) { AxisReads++; return 0f; }
    }
}
