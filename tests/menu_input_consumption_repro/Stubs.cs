using System.Collections.Generic;

namespace UnityEngine
{
    public static class Time
    {
        public static int frameCount;
    }

    public static class Input
    {
        internal static readonly HashSet<string> Down = new HashSet<string>();
        public static bool GetButtonDown(string name) { return Down.Contains(name); }
        public static bool GetButton(string name) { return false; }
        public static bool GetButtonUp(string name) { return false; }
        public static float GetAxis(string name) { return 0f; }
    }

    public class GameObject
    {
        public static implicit operator bool(GameObject obj) { return obj != null; }
    }

    public class NetworkView
    {
        public bool isMine = true;
        public static implicit operator bool(NetworkView view) { return view != null; }
    }
}

public class GameScript
{
    public static bool pausing, dead, buildMode, inventoryOpen, talking, combatMode;
}

public class PlayerScript
{
    public static bool beaming;
    private bool canInteract;
    private bool interacting;
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public T GetComponent<T>() where T : class { return new UnityEngine.NetworkView() as T; }
    public void SetCanInteract(bool value) { canInteract = value; }
    public void SetInteracting(bool value) { interacting = value; }
}

public static class MenuScript
{
    public static UnityEngine.GameObject player = new UnityEngine.GameObject();
}

namespace ControllerSupport
{
    internal static class ControllerExploration
    {
        internal static bool SuppressControllerDroidButton(GameScript game) { return false; }
    }
}
