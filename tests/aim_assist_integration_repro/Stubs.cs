using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
namespace UnityEngine {
 public struct Vector2 {
  public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 zero=>new Vector2(); public static Vector2 right=>new Vector2(1,0);
  public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude); public Vector2 normalized=>this/(magnitude==0?1:magnitude);
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
  public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
  public static Vector2 operator /(Vector2 a,float b)=>new Vector2(a.x/b,a.y/b);
  public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
 }
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new Vector3();public float sqrMagnitude=>x*x+y*y+z*z;
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
  public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
  public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y,0);
 }
 public class Object {
  private static int next;private int id=++next;public int GetInstanceID()=>id;
  internal bool destroyed;public static List<Object> All=new List<Object>();public Object(){All.Add(this);}
  public static implicit operator bool(Object o)=>o!=null&&!o.destroyed&&(!(o is Component c)||c.gameObject==null||!c.gameObject.destroyed);
  public static void DontDestroyOnLoad(Object o){}public static void Destroy(Object o){if(o!=null)o.destroyed=true;}
  public static T[] FindObjectsOfType<T>() where T:Object {var found=new List<T>();foreach(Object o in All)if(o is T t&&t)found.Add(t);return found.ToArray();}
  public static Object Instantiate(Object original){var source=original as GameObject;if(source==null)return null;return source.Clone();}
 }
 public class GameObject:Object {
  public string name;public int layer;public bool activeInHierarchy=true;public Transform transform;
  readonly Dictionary<Type,object> components=new Dictionary<Type,object>();
  public GameObject(string name=""){this.name=name;transform=new Transform(this);}
  public T GetComponent<T>() where T:class {object o;return components.TryGetValue(typeof(T),out o)?o as T:null;}
  public Component GetComponent(Type type){object o;return components.TryGetValue(type,out o)?o as Component:null;}
  public T AddComponent<T>() where T:class,new(){var x=new T();components[typeof(T)]=x;var c=x as Component;if(c!=null)c.gameObject=this;return x;}
  public void Put<T>(T o){components[typeof(T)]=o;}
  public void SetActive(bool active){activeInHierarchy=active;}
  internal GameObject Clone(){var copy=new GameObject(name);copy.transform.position=transform.position;copy.transform.rotation=transform.rotation;copy.transform.localScale=transform.lossyScale;
   foreach(var pair in components){var c=copy.AddByType(pair.Key);if(pair.Value is MeshRenderer r){((MeshRenderer)c).sharedMaterial=r.sharedMaterial;((MeshRenderer)c).enabled=r.enabled;}if(pair.Value is MeshFilter f)((MeshFilter)c).sharedMesh=f.sharedMesh;}
   return copy;}
  private Component AddByType(Type type){var c=(Component)Activator.CreateInstance(type);c.gameObject=this;components[type]=c;return c;}
  public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class {var list=new List<T>();foreach(Object o in All)if(o is Component c&&c&&c.gameObject!=null){for(Transform t=c.transform;t!=null;t=t.parent)if(t==transform){if(c is T value)list.Add(value);break;}}return list.ToArray();}
 }
 public class Component:Object { public GameObject gameObject; public Transform transform=>gameObject.transform; public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
  public T GetComponentInParent<T>() where T:class {for(Transform t=transform;t!=null;t=t.parent){T c=t.gameObject.GetComponent<T>();if(c!=null)return c;}return null;}}
 public static class ComponentChildren {public static T[] GetComponentsInChildren<T>(this Component c,bool includeInactive)where T:class=>c.gameObject.GetComponentsInChildren<T>(includeInactive);}
 public class MonoBehaviour:Component{}
 public class Transform:Object {
  public GameObject gameObject;public Transform parent;public Vector3 position,lossyScale=new Vector3(1,1,1),localScale=new Vector3(1,1,1);public Quaternion rotation;public float angle;
  public Transform(GameObject g){gameObject=g;}
  public Vector3 TransformPoint(Vector3 p){double r=angle*Math.PI/180;return position+new Vector3((float)(p.x*Math.Cos(r)-p.y*Math.Sin(r)),(float)(p.x*Math.Sin(r)+p.y*Math.Cos(r)),p.z);}
 }
 public struct Bounds {public Vector3 center,size;}
 public class Rigidbody:Component {public bool isKinematic;public Vector3 velocity;}
 public class Collider:Component {public bool enabled=true;public Rigidbody attachedRigidbody=>GetComponentInParent<Rigidbody>();public virtual Bounds bounds=>new Bounds{center=transform.position,size=new Vector3(1,1,1)};}
 public class BoxCollider:Collider {public Vector3 center,size=new Vector3(1,1,1);public override Bounds bounds=>new Bounds{center=transform.TransformPoint(center),size=size};}
 public class SphereCollider:Collider {public Vector3 center;public float radius=.5f;public override Bounds bounds=>new Bounds{center=transform.TransformPoint(center)};}
 public static class Physics {public static Collider[] Candidates=new Collider[0];public static int Searches;public static Collider[] OverlapSphere(Vector3 p,float r,int mask){Searches++;return Candidates;}}
 public class Camera:Object {public static Camera main=new Camera();public Vector3 ScreenToWorldPoint(Vector3 p)=>p;public Vector3 WorldToScreenPoint(Vector3 p)=>new Vector3(p.x,p.y,1);public Vector3 ViewportToWorldPoint(Vector3 p)=>new Vector3(p.x*40-20,p.y*20-10,0);}
 public static class Input {public static Vector3 mousePosition{get;set;}public static bool Click;public static bool GetMouseButtonDown(int n)=>Click;}
 public static class Time {public static int frameCount;public static float unscaledTime,unscaledDeltaTime=.02f;public static float time=>unscaledTime;}
 public class NetworkView:Component {public bool isMine=true;}
 public struct Quaternion{public float z;public static Quaternion Euler(float x,float y,float z)=>new Quaternion{z=z};}
 public struct Matrix4x4 {public Vector3 origin;public float angle;public static Matrix4x4 identity=>new Matrix4x4();public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new Matrix4x4{origin=p,angle=q.z};}
 public struct NetworkPlayer {public int Id;public static bool operator ==(NetworkPlayer a,NetworkPlayer b)=>a.Id==b.Id;public static bool operator !=(NetworkPlayer a,NetworkPlayer b)=>a.Id!=b.Id;public override bool Equals(object o)=>o is NetworkPlayer p&&p.Id==Id;public override int GetHashCode()=>Id;}
 public static class Network {public static NetworkPlayer player;public static Object Instantiate(Object o,Vector3 p,Quaternion q,int g)=>o;}
 public static class Resources {public static Dictionary<string,Object> Assets=new Dictionary<string,Object>();public static Object Load(string path){Object o;return Assets.TryGetValue(path,out o)?o:null;}}
 public class Texture:Object {public string name;}
 public class Shader:Object {public string name;public bool Color,Tint;public static Dictionary<string,Shader> Available=new Dictionary<string,Shader>();public static Shader Find(string name){Shader shader;return Available.TryGetValue(name,out shader)?shader:null;}}
 public class Material:Object {public Shader shader;public Texture mainTexture;public Color Tint;public Material(Shader shader){this.shader=shader;}public bool HasProperty(string p)=>(p=="_Color"&&shader.Color)||(p=="_TintColor"&&shader.Tint);public void SetColor(string p,Color color){Tint=color;}}
 public class MeshRenderer:Component {public Material sharedMaterial;public bool enabled=true;}
 public class MeshFilter:Component {public Object sharedMesh;}
 public class Animation:Component{}
 public static class Debug {public static List<string> Warnings=new List<string>();public static void LogWarning(object x){Warnings.Add(x.ToString());}}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color red=>new Color(1,0,0);public static Color yellow=>new Color(1,1,0);}
 public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;this.width=w;this.height=h;}}
 public class Texture2D:Texture {public static Texture2D whiteTexture=new Texture2D();}
 public static class GUI {public struct Draw {public Color Color;public Matrix4x4 Matrix;public Rect Rect;}public static List<Draw> Recorded=new List<Draw>();public static Color color;public static Matrix4x4 matrix;public static int Draws;public static void DrawTexture(Rect r,Texture2D t){Draws++;Recorded.Add(new Draw{Color=color,Matrix=matrix,Rect=r});}}
 public static class Screen {public static int height=1200;}
}
public class PlayerScript:UnityEngine.MonoBehaviour {public static bool beaming;public bool attacking;public UnityEngine.Transform shot;public IEnumerator Attack(){yield break;}public IEnumerator Repeater(){yield break;}public void Staff(){} }
public class Projectile:UnityEngine.MonoBehaviour {private bool isController=true;public void SetController(bool value){isController=value;}public float speed;public int special;public bool isStaff,onlyHorizontal,onlyVertical,isFlame,isPerceval,treant,moloch,tyrannog,isSwathe;public class UpdatePosToken{public Projectile Owner;public bool MoveNext(){return false;}}}
public class EnemyScript:UnityEngine.MonoBehaviour {public int hp=10;public bool dead,dying;}
public class GameScript {public static int[] equippedIDs=new[]{404};public static bool combatMode=true,dead,pausing,buildMode,inventoryOpen,talking;public void ActivateCombatChip(int id){}public void Atalanta(){}}
public static class Menuu {public static bool usingController;}
public static class MenuScript {public static UnityEngine.GameObject player;}
public static class ControllerCursor {public static float staticX,staticY;}
public class ActionCursor:UnityEngine.MonoBehaviour{}
namespace ControllerSupport {internal static class ControllerGameplayInput {internal static bool CloseAimHeld()=>false;} public static class MouseOnlyHelperAimPatch {public static IEnumerable<MethodBase> TargetMethods(){yield break;}}}
namespace GadgetCore.API {public static class GadgetCoreAPI {public static bool Frozen;public static bool IsInputFrozen()=>Frozen;}[AttributeUsage(AttributeTargets.Class)]public class HarmonyGadgetAttribute:Attribute{public HarmonyGadgetAttribute(string s){}}}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]public class HarmonyPatch:Attribute{public HarmonyPatch(){}public HarmonyPatch(Type t,string s){}}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyPrefix:Attribute{}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyPostfix:Attribute{}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyFinalizer:Attribute{}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyTranspiler:Attribute{}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyPriority:Attribute{public HarmonyPriority(int i){}}
 public static class Priority{public const int First=800;}
 public class CodeInstruction {public OpCode opcode;public object operand;public List<object> labels=new List<object>(),blocks=new List<object>();public CodeInstruction(OpCode o,object p=null){opcode=o;operand=p;}public CodeInstruction(CodeInstruction o){opcode=o.opcode;operand=o.operand;labels.AddRange(o.labels);blocks.AddRange(o.blocks);}}
 public static class AccessTools {public static MethodInfo Method(Type t,string n,Type[] p=null)=>p==null?t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance):t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance,null,p,null);}
}

public class WormScript:UnityEngine.MonoBehaviour {public bool isMainHead;public UnityEngine.GameObject mainHead;public int hp;public bool dying;}
public class Hivemind:UnityEngine.MonoBehaviour {public int hp;public bool dying,dead;}
public class HivemindBox:UnityEngine.MonoBehaviour {public UnityEngine.GameObject p;}
