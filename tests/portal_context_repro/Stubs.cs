using System;
using System.Collections.Generic;
using System.Reflection.Emit;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object o) {return o!=null;} }
 public class Component:Object { public GameObject gameObject; public Transform transform { get{return gameObject.transform;} } public T GetComponent<T>() where T:class{return gameObject.GetComponent<T>();} }
 public class GameObject:Object { public string name,tag="Untagged"; public int layer; public bool activeInHierarchy=true; public Transform transform; public readonly List<Component> components=new List<Component>(); public readonly List<GameObject> children=new List<GameObject>(); public GameObject(string n="") {name=n;transform=new Transform{gameObject=this};} public T Add<T>() where T:Component,new(){T t=new T{gameObject=this};components.Add(t);return t;} public T GetComponent<T>() where T:class{return components.Find(x=>x is T) as T;} public T[] GetComponentsInChildren<T>() where T:class {var r=new List<T>();foreach(var c in components)if(c is T)r.Add(c as T);foreach(var child in children)r.AddRange(child.GetComponentsInChildren<T>());return r.ToArray();} public void Child(GameObject c){children.Add(c);c.transform.parent=transform;} }
 public class Transform:Component {public Transform parent;}
 public struct Vector3 {public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}public float sqrMagnitude{get{return x*x+y*y+z*z;}}}
 public struct Bounds {public Vector3 center;}
 public class Collider:Component {public bool enabled=true;public Bounds bounds;}
 public class Renderer:Component {public bool enabled=true;}
 public class Camera:Object {public static Camera main=new Camera();public Vector3 WorldToScreenPoint(Vector3 v){return new Vector3(v.x*10,v.y*10,1);}}
 public static class Input {public static Vector3 mousePosition{get;set;}public static bool Left,Right;public static bool GetMouseButtonDown(int b){return b==0?Left:Right;}}
 public static class Time {public static int frameCount;}
 public static class Debug {public static void LogWarning(string s){} }
}
public class ButtonMenu:UnityEngine.Component {public bool isPlanet;}
public class GameScript {public static bool inventoryOpen=true,pausing,dead,buildMode;public static int curPlanet,curPlanetTrue=-1,challengeLevel;public UnityEngine.GameObject inventoryMain=new UnityEngine.GameObject(),menuPlanets=new UnityEngine.GameObject();public UnityEngine.GameObject[] planetGuard=new UnityEngine.GameObject[26];private int[] portalUses=new int[26];private bool planetTeleing;public int credits=50000;public int GetCredits(){return credits;}public int GetChallengeCost(int v){return v<3?5000*(v+1):0;}public void Uses(int n,int v){portalUses[n]=v;}public void Cooling(bool b){planetTeleing=b;}}
namespace GadgetCore.API {public class PlanetInfo {public int PortalUses;}public static class PlanetRegistry {public static int PlanetSelectorPage=1,PlanetSelectorPages=1;private static PlanetInfo[] selectorPlanets;public static void Custom(params PlanetInfo[] p){selectorPlanets=p;}}public static class GadgetCoreAPI {public static bool Frozen;public static bool IsInputFrozen(){return Frozen;}}public class HarmonyGadget:Attribute {public HarmonyGadget(string n){}}}
namespace HarmonyLib {public class HarmonyPatch:Attribute{}public class HarmonyTargetMethods:Attribute{}public class HarmonyPrefix:Attribute{}public class HarmonyFinalizer:Attribute{}public class HarmonyTranspiler:Attribute{}public class CodeInstruction {public OpCode opcode;public object operand;public List<object> labels=new List<object>();public CodeInstruction(OpCode o,object p=null){opcode=o;operand=p;}public CodeInstruction(CodeInstruction c){opcode=c.opcode;operand=c.operand;labels.AddRange(c.labels);}}}
