using System.Collections.Generic;

namespace UnityEngine
{
    public static class Time { public static int frameCount; }
    public static class Application { public static int loadedLevel; }
    public static class Input
    {
        public static readonly HashSet<string> Down = new HashSet<string>();
        public static readonly Dictionary<string, float> Axes = new Dictionary<string, float>();
        public static readonly HashSet<int> MouseDown = new HashSet<int>();
        public static bool GetMouseButtonDown(int button) { return MouseDown.Contains(button); }
        public static bool GetButtonDown(string name) { return Down.Contains(name); }
        public static bool GetButton(string name) { return false; }
        public static bool GetButtonUp(string name) { return false; }
        public static float GetAxis(string name) { return Axes.TryGetValue(name, out float value) ? value : 0; }
    }
    public class GameObject
    {
        public bool activeInHierarchy = true;
        public NetworkView view = new NetworkView();
        public int Clicks;
        public static implicit operator bool(GameObject obj) { return obj != null; }
        public T GetComponent<T>() where T : class { return view as T; }
        public void SendMessage(string name)
        {
            if (name != "Click") throw new System.Exception("Unexpected message " + name);
            Clicks++;
            MenuTalking.isWriting = false;
        }
    }
    public class NetworkView
    {
        public bool isMine = true;
        public static implicit operator bool(NetworkView obj) { return obj != null; }
    }
}
public class GameScript
{
    public static bool inventoryOpen, pausing, dead, talking, buildMode, combatMode;
    public UnityEngine.GameObject menuTalking = new UnityEngine.GameObject();
    private int curA = 99, curB = 1, curC = 2;
    private bool exitTalking;
    public bool CloseOnTalk;
    public int Talks;
    public int[] LastArgs;
    public void Talk(int a, int b, int c)
    {
        Talks++;
        LastArgs = new[] { a, b, c };
        curC++;
        if (CloseOnTalk) { talking = false; menuTalking.activeInHierarchy = false; }
        else MenuTalking.isWriting = true;
    }
    public void SetExiting(bool value) { exitTalking = value; }
}
public static class MenuTalking { public static bool isWriting; }
public class PlayerScript
{
    public static bool beaming;
    private bool canInteract = true, interacting;
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
}
public static class MenuScript { public static UnityEngine.GameObject player; }
namespace GadgetCore.API
{
    public static class GadgetCoreAPI
    {
        public static bool Frozen;
        public static bool IsInputFrozen() { return Frozen; }
    }
}
namespace ControllerSupport
{
    internal static class ControllerExploration
    {
        internal static bool SuppressControllerDroidButton(GameScript game) { return false; }
    }
}
