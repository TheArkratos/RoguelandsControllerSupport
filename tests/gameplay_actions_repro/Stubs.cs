using System.Collections.Generic;
namespace UnityEngine {
 public static class Time { public static int frameCount; }
 public static class Application { public static int loadedLevel; }
 public static class Input {
  public static readonly HashSet<string> Down=new HashSet<string>();
  public static bool GetButtonDown(string name){return Down.Contains(name);}
  public static bool GetButton(string name){return false;}
  public static bool GetButtonUp(string name){return false;}
  public static float GetAxis(string name){return 0;}
 }
 public class GameObject {
  public NetworkView view=new NetworkView();
  public static implicit operator bool(GameObject obj){return obj!=null;}
  public T GetComponent<T>() where T:class{return view as T;}
 }
 public class NetworkView { public bool isMine=true;public static implicit operator bool(NetworkView obj){return obj!=null;} }
}
public class GameScript { public static bool inventoryOpen,pausing,dead,talking,buildMode,combatMode; }
public class PlayerScript {
 public static bool beaming;
 private bool canInteract,interacting;
 public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
 public T GetComponent<T>() where T:class{return gameObject.GetComponent<T>();}
}
public static class MenuScript {public static UnityEngine.GameObject player;}
namespace GadgetCore.API { public static class GadgetCoreAPI {public static bool Frozen;public static bool IsInputFrozen(){return Frozen;}} }
namespace ControllerSupport {internal static class ControllerExploration {internal static bool SuppressControllerDroidButton(GameScript game){return false;}}}
