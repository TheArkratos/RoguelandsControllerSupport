using System;
using System.Collections.Generic;
namespace UnityEngine
{
 public class Object { public static void Destroy(Object o) {} public static void DontDestroyOnLoad(Object o) {} }
 public class GameObject : Object {
 public bool activeSelf=true; public bool activeInHierarchy {get{return activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy); }set{activeSelf=value;}} public static object Runner; public Transform transform;
 public GameObject(string name) {transform=new Transform{gameObject=this};} public void SetActive(bool value){activeSelf=value;} public T AddComponent<T>() where T:new() { T runner = new T(); if(runner is SpriteRenderer) ((SpriteRenderer)(object)runner).transform=transform; else Runner=runner; return runner; }
 }
 public class Transform : Object {
 public GameObject gameObject;private Transform ancestor;public Vector3 localPosition;public Vector3 localScale=new Vector3(1,1,1);
 public Transform parent {get{return ancestor;}set{Vector3 world=position,scale=lossyScale;ancestor=value;position=world;Vector3 parentScale=ancestor==null?new Vector3(1,1,1):ancestor.lossyScale;localScale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);}}
 public Vector3 lossyScale {get{Vector3 scale=ancestor==null?new Vector3(1,1,1):ancestor.lossyScale;return new Vector3(scale.x*localScale.x,scale.y*localScale.y,scale.z*localScale.z);}}
 public Vector3 position {get{if(ancestor==null)return localPosition;Vector3 pos=ancestor.position,scale=ancestor.lossyScale;return new Vector3(pos.x+localPosition.x*scale.x,pos.y+localPosition.y*scale.y,pos.z+localPosition.z*scale.z);}set{if(ancestor==null){localPosition=value;return;}Vector3 pos=ancestor.position,scale=ancestor.lossyScale;localPosition=new Vector3((value.x-pos.x)/scale.x,(value.y-pos.y)/scale.y,(value.z-pos.z)/scale.z);}}
 }
 public class Material : Object {public Texture2D mainTexture;}
 public class Texture2D : Object {public int width,height;public FilterMode filterMode;public TextureWrapMode wrapMode;public Texture2D(int w,int h,TextureFormat format,bool mipmap){width=w;height=h;}public bool LoadImage(byte[] bytes){width=6;height=6;return true;}}
 public enum TextureFormat{ARGB32}public enum FilterMode{Point}public enum TextureWrapMode{Clamp}
 public class Sprite : Object {public Bounds bounds;public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot,float ppu){return new Sprite{bounds=new Bounds{min=new Vector3(-pivot.x*rect.width/ppu,-pivot.y*rect.height/ppu,0),max=new Vector3((1-pivot.x)*rect.width/ppu,(1-pivot.y)*rect.height/ppu,0)}};}}
 public class SpriteRenderer : Object {public Color color=Color.white;public bool enabled=true;public int sortingOrder;public Sprite sprite;public Transform transform;public Bounds bounds{get{Vector3 p=transform.position,s=transform.lossyScale;return new Bounds{min=new Vector3(p.x+sprite.bounds.min.x*s.x,p.y+sprite.bounds.min.y*s.y,p.z),max=new Vector3(p.x+sprite.bounds.max.x*s.x,p.y+sprite.bounds.max.y*s.y,p.z)};}}}
 public static class Resources{public static Object Load(string name){return new Material{mainTexture=new Texture2D(32,32,TextureFormat.ARGB32,false)};}}public static class Debug{public static void LogWarning(string message){throw new Exception(message);}}

 public class MonoBehaviour : Object {}
 public struct Vector2 {public float x,y; public Vector2(float a,float b){x=a;y=b;}}
 public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) {x=a;y=b;z=c;} }
 public struct Bounds { public Vector3 min,max;public Vector3 center{get{return new Vector3((min.x+max.x)/2,(min.y+max.y)/2,(min.z+max.z)/2);}} public Vector3 size{get{return new Vector3(max.x-min.x,max.y-min.y,max.z-min.z);}} }
 public class Collider : Object { public Transform transform=new GameObject("slot").transform;private Bounds localBounds;public Bounds bounds{get{Vector3 p=transform.position;return new Bounds{min=new Vector3(localBounds.min.x+p.x,localBounds.min.y+p.y,localBounds.min.z+p.z),max=new Vector3(localBounds.max.x+p.x,localBounds.max.y+p.y,localBounds.max.z+p.z)};}set{Vector3 p=transform.position;localBounds=new Bounds{min=new Vector3(value.min.x-p.x,value.min.y-p.y,value.min.z-p.z),max=new Vector3(value.max.x-p.x,value.max.y-p.y,value.max.z-p.z)};}} }
 public class Camera : Object { public static Camera main = new Camera();public Transform transform=new GameObject("camera").transform; public Vector3 WorldToScreenPoint(Vector3 v) { return new Vector3(v.x-transform.position.x,v.y-transform.position.y,1); } }
 public static class Screen { public static int height=720,width=1280; }
 public static class Time { public static float realtimeSinceStartup; }
 public static class Mathf { public static float Clamp(float n,float a,float b) { return Math.Max(a,Math.Min(n,b)); } public static float Min(float a,float b) {return Math.Min(a,b);} public static float Max(float a,float b) {return Math.Max(a,b);} public static float Abs(float a) {return Math.Abs(a);} public static int RoundToInt(float a) {return (int)Math.Round(a);} }
 public struct Color {public float r,g,b,a; public Color(float red,float green,float blue,float alpha){r=red;g=green;b=blue;a=alpha;} public static Color white=new Color(1,1,1,1),black=new Color(0,0,0,1);}
 public struct Rect { public float x,y,width,height; public Rect(float left,float top,float w,float h) {x=left;y=top;width=w;height=h;} public float xMax {get{return x+width;}} public float yMax {get{return y+height;}} }
 public enum TextAnchor {MiddleCenter,UpperLeft}
 public class RectOffset {public int left,right,top,bottom; public RectOffset() {} public RectOffset(int l,int r,int t,int b){left=l;right=r;top=t;bottom=b;} }
 public class GUIStyleState {public Color textColor;}
 public class GUIStyle {
  public GUIStyle(GUIStyle other) {if(other!=null){alignment=other.alignment;wordWrap=other.wordWrap;fontSize=other.fontSize;padding=new RectOffset(other.padding.left,other.padding.right,other.padding.top,other.padding.bottom);}} public TextAnchor alignment; public bool wordWrap; public int fontSize; public RectOffset padding=new RectOffset(); public GUIStyleState normal=new GUIStyleState();
  public Vector2 CalcSize(GUIContent c) {return new Vector2(c.text.Length*fontSize*0.55f+padding.left+padding.right,fontSize*1.25f+padding.top+padding.bottom);}
  public float CalcHeight(GUIContent c,float width) {
   if(!wordWrap) return fontSize*1.25f+padding.top+padding.bottom;
   width=Math.Max(1,width-padding.left-padding.right);
   // Greedy word wrapping, including a break for a label wider than one line.
   float character=fontSize*0.55f, used=0; int lines=1;
   foreach(string word in c.text.Split(' ')) {
    float size=word.Length*character;
    if(used>0 && used+character+size>width) {lines++;used=0;}
    if(used>0)used+=character;
    if(size>width) {int pieces=(int)Math.Ceiling(size/width);lines+=pieces-1;used=size-(pieces-1)*width;}
    else used+=size;
   }
   return lines*fontSize*1.25f+padding.top+padding.bottom;
  }
 }
 public class GUIContent {public string text; public GUIContent(string value) {text=value;} }
 public class GUISkin { public GUIStyle label=new GUIStyle(null){padding=new RectOffset(4,4,2,2)}; }
 public sealed class LabelDraw {public Rect rect;public string text;public int font;public Color tint;public float glyphX;}
 public static class GUI {
  public static Color color=Color.white,backgroundColor=Color.white; public static GUISkin skin=new GUISkin(); public static List<string> Labels=new List<string>(); public static List<Rect> Boxes=new List<Rect>();public static List<LabelDraw> Draws=new List<LabelDraw>();
  public static void Box(Rect r,string s) {Boxes.Add(r);} public static void Label(Rect r,string s,GUIStyle style) {Labels.Add(s);Draws.Add(new LabelDraw{rect=r,text=s,font=style.fontSize,tint=color,glyphX=r.x+style.padding.left});}
 }
}
public class Item {public int id,q;}
public class GameScript : UnityEngine.Object {public static bool inventoryOpen,pausing; public UnityEngine.GameObject inventoryMain=new UnityEngine.GameObject("inventory"); private Item holdingItem; public void Hold(int id,int q) {holdingItem = new Item {id=id,q=q};} }
