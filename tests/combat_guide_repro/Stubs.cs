using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public bool destroyed; public static implicit operator bool(Object o)=>o!=null&&!o.destroyed;public static void Destroy(Object o){if(o!=null)o.destroyed=true;} }
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();public T[] GetComponentsInChildren<T>(bool all=false) where T:Component=>gameObject.GetComponentsInChildren<T>(all);}
 public class MonoBehaviour:Component{}
 public class GameObject:Object {
  public string name;public int layer;public bool activeInHierarchy=true;public Transform transform;public static bool ThrowOnAdd;
  readonly List<Component> components=new List<Component>();
  public GameObject(string n=""){name=n;transform=new Transform{gameObject=this};}
  public T AddComponent<T>() where T:Component,new(){if(ThrowOnAdd)throw new Exception("modeled renderer allocation failure");var c=new T{gameObject=this};components.Add(c);return c;}
  public T GetComponent<T>() where T:Component {foreach(var c in components)if(c is T)return(T)c;return null;}
  public T[] GetComponentsInChildren<T>(bool all=false) where T:Component {var found=new List<T>();if(all||activeInHierarchy){var own=GetComponent<T>();if(own!=null)found.Add(own);foreach(var child in transform.children)found.AddRange(child.gameObject.GetComponentsInChildren<T>(all));}return found.ToArray();}
 }
 public class Transform:Component {public Vector3 position,localScale=new Vector3(1,1,1);public Quaternion rotation=Quaternion.Euler(0,0,0);public Vector3 TransformDirection(Vector3 p)=>rotation*p;public Vector3 TransformPoint(Vector3 p)=>position+rotation*new Vector3(p.x*localScale.x,p.y*localScale.y,p.z*localScale.z);public readonly List<Transform> children=new List<Transform>();}
 public class MeshFilter:Component {public Mesh sharedMesh;}
 public class MeshRenderer:Component {public Bounds bounds;public Material sharedMaterial;public bool enabled=true;public int sortingLayerID,sortingOrder;}
 public class Mesh:Object {public Bounds bounds;public Vector3[] vertices;public int[] triangles;public Vector2[] uv;public void Clear() {vertices=null;triangles=null;uv=null;} public void RecalculateBounds(){} public void RecalculateNormals(){} }
 public class Material:Object{public int culling=2;}
 public struct Bounds {public Vector3 size;}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator*(Vector3 a,float s)=>new Vector3(a.x*s,a.y*s,a.z*s);}
 public struct Quaternion {
  public float x,y,z,w;public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}
  public static Quaternion Euler(float x,float y,float z){double k=Math.PI/360;var a=new Quaternion((float)Math.Sin(x*k),0,0,(float)Math.Cos(x*k));var b=new Quaternion(0,(float)Math.Sin(y*k),0,(float)Math.Cos(y*k));var c=new Quaternion(0,0,(float)Math.Sin(z*k),(float)Math.Cos(z*k));return b*a*c;}
  public static Quaternion operator*(Quaternion a,Quaternion b)=>new Quaternion(a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w,a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z);
  public static Vector3 operator*(Quaternion q,Vector3 p){var v=q*new Quaternion(p.x,p.y,p.z,0)*new Quaternion(-q.x,-q.y,-q.z,q.w);return new Vector3(v.x,v.y,v.z);}
 }
 public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}public float xMin=>x;public float xMax=>x+width;public float yMin=>y;public float yMax=>y+height;}
 public class Camera:Object {
  public static Camera main;public Rect pixelRect=new Rect(0,0,1920,1200);public float scale=10;public Vector3 offset;public float cameraZ=-5;public bool Behind;public double RollDegrees;
  static Vector3 Rotate(Vector3 p,double degrees){double r=degrees*Math.PI/180,c=Math.Cos(r),s=Math.Sin(r);return new Vector3((float)(p.x*c-p.y*s),(float)(p.x*s+p.y*c),p.z);}
  public Vector3 WorldToScreenPoint(Vector3 p){var local=Rotate(new Vector3(p.x-offset.x,p.y-offset.y,0),-RollDegrees);return new Vector3(pixelRect.x+pixelRect.width/2+local.x*scale,pixelRect.y+pixelRect.height/2+local.y*scale,Behind?-1:p.z-cameraZ);}
  public Vector3 ScreenToWorldPoint(Vector3 p){var world=Rotate(new Vector3((p.x-pixelRect.x-pixelRect.width/2)/scale,(p.y-pixelRect.y-pixelRect.height/2)/scale,p.z+cameraZ),RollDegrees);return new Vector3(world.x+offset.x,world.y+offset.y,world.z);}
 }
 public class NetworkView:Component {public bool isMine=true;}
}
public class ActionCursor:UnityEngine.MonoBehaviour{}
public static class MenuScript {public static UnityEngine.GameObject player;}
public static class GameScript {public static int[] equippedIDs;public static bool combatMode,dead,pausing,buildMode,inventoryOpen,talking;}
namespace ControllerSupport {
 internal static class ControllerAimAssist {internal static UnityEngine.Vector3 Center;internal static UnityEngine.Vector3 PlayerCenter()=>Center;}
 internal static class ControllerAim {internal static bool Success=true;internal static bool TryWorldPoint(out UnityEngine.Vector3 p){p=new UnityEngine.Vector3(-20,0,0);return Success;}}
}
