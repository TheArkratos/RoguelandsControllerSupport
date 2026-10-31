using System;
using System.Reflection;
using UnityEngine;
using ControllerSupport;
static class Program {
 // Authored synthetic inputs: no asset exports, native source or local reports.
 static readonly Vector3[] QuadVertices = {
  new Vector3(-1,1,0),new Vector3(-1,-1,0),
  new Vector3(1,1,0),new Vector3(1,-1,0)
 };
 static readonly int[] QuadTriangles = {0,1,2,2,1,3};
 // Exact half-turn about X; the vertical plane additionally rotates 90 degrees
 // about Z. These ideal transforms exercise facing flips without asset samples.
 static readonly Quaternion HorizontalFacing = new Quaternion(1,0,0,0);
 static readonly Quaternion VerticalFacing = new Quaternion((float)Math.Sqrt(.5),(float)Math.Sqrt(.5),0,0);
 static readonly double[] CameraRolls = {0,.25,-.25,2,-2,7.5,-7.5,30,-30,90,-90,180};
 const float OrthographicHalfHeight = 60f;
 static bool Owned;static int checks;static ActionCursor cursor;static MeshRenderer h,v,glyph;static Mesh mesh;static Material material;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.0005;
 static T Field<T>(string n)=>(T)typeof(ControllerCombatGuide).GetField(n,BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
 static MeshRenderer Add(GameObject parent,string name,Vector3 size,Mesh m,Material mat){var o=new GameObject(name);parent.transform.children.Add(o.transform);o.transform.position=new Vector3(14,3,-.29f);var f=o.AddComponent<MeshFilter>();f.sharedMesh=m;var r=o.AddComponent<MeshRenderer>();r.bounds=new Bounds{size=size};r.sharedMaterial=mat;r.sortingLayerID=4;r.sortingOrder=7;return r;}
 static void Setup(){ControllerCombatGuide.Reset();GameScript.equippedIDs=new[]{549};ControllerAimAssist.Center=new Vector3(0,0,0);Owned=ControllerAim.Success=true;ControllerInputOwnership.Configure(()=>Owned);GameScript.combatMode=true;GameScript.dead=GameScript.pausing=GameScript.buildMode=GameScript.inventoryOpen=GameScript.talking=false;Camera.main=new Camera();MenuScript.player=new GameObject("player");MenuScript.player.AddComponent<NetworkView>();var root=new GameObject("actionCursor");root.transform.position=new Vector3(14,3,0);cursor=root.AddComponent<ActionCursor>();mesh=new Mesh{bounds=new Bounds{size=new Vector3(2,2,0)},vertices=(Vector3[])QuadVertices.Clone(),triangles=(int[])QuadTriangles.Clone()};material=new Material();h=Add(root,"-1",new Vector3(200,.2f,0),mesh,material);v=Add(root,"-1",new Vector3(.2f,200,0),mesh,material);glyph=Add(root,"-1",new Vector3(1,1,0),mesh,new Material());h.transform.rotation=HorizontalFacing;v.transform.rotation=VerticalFacing;h.transform.localScale=v.transform.localScale=new Vector3(100,.1f,1);material.culling=2;Camera.main.cameraZ=-10;ControllerCombatGuide.Observe(cursor);}
 static void Hidden(){ControllerCombatGuide.UpdateVisual();Check(!h.enabled&&!v.enabled&&Field<MeshRenderer>("guideRenderer").enabled,"native bars replaced by active controller guide");}
 static void Restored(string reason){ControllerCombatGuide.UpdateVisual();Check(h.enabled&&v.enabled&&!Field<MeshRenderer>("guideRenderer").enabled,reason);}
 static void Geometry(){
  var rect=new Rect(10,20,200,100);var o=new Vector2(110,70);Vector2 e;
  foreach(var p in new[]{new Vector2(111,70),new Vector2(109,70),new Vector2(110,71),new Vector2(110,69),new Vector2(111,71),new Vector2(109,71),new Vector2(111,69),new Vector2(109,69)}){
   Check(ControllerCombatGuide.TryScreenEndpoint(o,p,rect,out e),"cardinal/diagonal ray clips");
   Check(e.x>=10&&e.x<=210&&e.y>=20&&e.y<=120,"endpoint remains inside offset viewport");
   Check(Near(e.x,10)||Near(e.x,210)||Near(e.y,20)||Near(e.y,120),"endpoint touches viewport edge");
   Check(Math.Abs((e.x-o.x)*(p.y-o.y)-(e.y-o.y)*(p.x-o.x))<.001,"endpoint collinear with crosshair ray");
  }
  Check(!ControllerCombatGuide.TryScreenEndpoint(o,o,rect,out e),"zero axis rejected");
  Check(!ControllerCombatGuide.TryScreenEndpoint(new Vector2(0,0),o,rect,out e),"offscreen player rejected");
  Check(!ControllerCombatGuide.TryScreenEndpoint(o,new Vector2(float.NaN,1),rect,out e),"NaN aim rejected");
  Check(!ControllerCombatGuide.TryScreenEndpoint(o,new Vector2(float.PositiveInfinity,1),rect,out e),"infinite aim rejected");
  Check(!ControllerCombatGuide.TryScreenEndpoint(o,new Vector2(120,70),new Rect(0,0,0,100),out e),"zero viewport rejected");
  Check(!ControllerCombatGuide.TryScreenEndpoint(new Vector2(10,70),new Vector2(9,70),rect,out e),"outward edge ray rejected");
 }
 static double Area(Vector3 a,Vector3 b,Vector3 c)=>((double)b.x-a.x)*((double)c.y-a.y)-((double)b.y-a.y)*((double)c.x-a.x);
 static void CheckCone(Mesh cone,Vector3 origin,Vector3 white,Rect worldView){
  Check(cone.vertices!=null&&cone.vertices.Length>=3,"staff cone has filled polygon vertices");
  Check(cone.triangles.Length==(cone.vertices.Length-2)*3,"single fan uses exactly n-2 triangles without duplicate back faces");
  double polygonArea=0,triangleArea=0;bool apex=false;double maxAngle=0;
  for(int i=0;i<cone.vertices.Length;i++){
   Vector3 a=cone.vertices[i],b=cone.vertices[(i+1)%cone.vertices.Length];polygonArea+=(double)a.x*b.y-(double)a.y*b.x;
   Vector3 w=a+origin;Check(w.x>=worldView.xMin-.001&&w.x<=worldView.xMax+.001&&w.y>=worldView.yMin-.001&&w.y<=worldView.yMax+.001,"filled vertex clips to viewport");
   double radius=Math.Sqrt((double)a.x*a.x+(double)a.y*a.y);if(radius<.0001)apex=true;else{
    double dx=white.x-origin.x,dy=white.y-origin.y,angle=Math.Abs(Math.Atan2(dx*a.y-dy*a.x,dx*a.x+dy*a.y)*180/Math.PI);maxAngle=Math.Max(maxAngle,angle);Check(angle<=AimAssistGeometry.HalfAngleDegrees+.001,"filled vertex remains inside existing eight-degree assist cone");}
  }
  Check(apex,"cone apex uses player center");Check(Math.Abs(maxAngle-8)<.001,"cone shares exact eight-degree half angle");
  for(int i=0;i<cone.triangles.Length;i+=3){double area=Area(cone.vertices[cone.triangles[i]],cone.vertices[cone.triangles[i+1]],cone.vertices[cone.triangles[i+2]]);Check(area<=.001,"cone winding matches synthetic clockwise source plane");triangleArea+=Math.Abs(area);}
  Check(Math.Abs(triangleArea-Math.Abs(polygonArea))<.01,"fan covers polygon once without alpha overdraw");
 }
 static void Staff(){
  Setup();GameScript.equippedIDs[0]=550;cursor.transform.position=new Vector3(14,0,0);Hidden();var obj=Field<GameObject>("guide");var cone=obj.GetComponent<MeshFilter>().sharedMesh;
  Check(cone!=mesh,"staff 550 renders filled cone mesh instead of thin ray");Check(Field<MeshRenderer>("guideRenderer").sharedMaterial==material,"staff uses fixture source material without opacity change");
  CheckCone(cone,obj.transform.position,cursor.transform.position,new Rect(-96,-60,192,120));
  GameScript.talking=true;Hidden();Check(obj.GetComponent<MeshFilter>().sharedMesh==cone,"dialogue popup retains filled staff cone instead of native bars");
  GameScript.pausing=true;Restored("pause during dialogue still restores native bars");GameScript.pausing=false;Hidden();
  Owned=false;ControllerCombatGuide.Observe(cursor);Check(h.enabled&&v.enabled&&!Field<MeshRenderer>("guideRenderer").enabled,"mouse takeover during dialogue still restores native bars");Owned=true;Hidden();GameScript.talking=false;
  Check(Near(obj.transform.localScale.x,1)&&Near(obj.transform.localScale.y,1),"cone mesh is not stretched by old ray scale");
  GameScript.equippedIDs[0]=599;ControllerCombatGuide.UpdateVisual();Check(obj.GetComponent<MeshFilter>().sharedMesh==cone,"last staff category ID uses same reusable owned cone mesh");
  foreach(int id in new[]{549,600,478}){GameScript.equippedIDs[0]=id;ControllerCombatGuide.UpdateVisual();Check(obj.GetComponent<MeshFilter>().sharedMesh==mesh&&Near(obj.transform.localScale.y*2,.2f),"nonstaff boundary/special cannon "+id+" immediately restores source-width ray");}
  GameScript.equippedIDs=null;ControllerCombatGuide.UpdateVisual();Check(obj.GetComponent<MeshFilter>().sharedMesh==mesh,"missing equipped weapon array uses ray");GameScript.equippedIDs=new int[0];ControllerCombatGuide.UpdateVisual();Check(obj.GetComponent<MeshFilter>().sharedMesh==mesh,"empty equipped weapon array uses ray");
  GameScript.equippedIDs=new[]{550};cursor.transform.position=new Vector3(96,60,0);ControllerCombatGuide.UpdateVisual();Check(obj.GetComponent<MeshFilter>().sharedMesh==cone&&cone.vertices.Length>=4,"corner-spanning cone clips to polygon rather than overshooting triangular viewport");CheckCone(cone,obj.transform.position,cursor.transform.position,new Rect(-96,-60,192,120));
  ControllerAimAssist.Center=new Vector3(3,2,1);cursor.transform.position=new Vector3(17,2,0);Camera.main.scale=20;Camera.main.offset=new Vector3(2,1,0);Camera.main.pixelRect=new Rect(100,50,1200,800);ControllerCombatGuide.UpdateVisual();
  Check(Near(obj.transform.position.x,3)&&Near(obj.transform.position.y,2),"staff reuses actual assist player collider center rather than transform position");CheckCone(cone,obj.transform.position,cursor.transform.position,new Rect(-28,-19,60,40));
  GameScript.pausing=true;Restored("staff cone restores bars on pause");GameScript.pausing=false;Hidden();Owned=false;ControllerCombatGuide.Observe(cursor);Check(h.enabled&&v.enabled&&!Field<MeshRenderer>("guideRenderer").enabled,"staff cone restores mouse bars immediately");
  ControllerCombatGuide.Reset();Check(cone.destroyed&&!mesh.destroyed&&!material.destroyed,"reset destroys only owned cone mesh, not native assets");
  Setup();GameScript.equippedIDs[0]=550;v.enabled=false;Hidden();Owned=false;ControllerCombatGuide.UpdateVisual();Check(h.enabled&&!v.enabled,"staff cone preserves originally disabled native bar");ControllerCombatGuide.Reset();
  Vector2[] empty=AimAssistGeometry.VisibleConePolygon(new Vector2(0,0),new Vector2(0,0),new Vector2(-1,-1),new Vector2(1,1));Check(empty.Length==0,"shared cone polygon rejects zero axis");
  empty=AimAssistGeometry.VisibleConePolygon(new Vector2(float.NaN,0),new Vector2(1,0),new Vector2(-1,-1),new Vector2(1,1));Check(empty.Length==0,"shared cone polygon rejects nonfinite origin");
  empty=AimAssistGeometry.VisibleConePolygon(new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(-1,-1));Check(empty.Length==0,"shared cone polygon rejects reversed viewport");
  ControllerCombatGuide.Reset();
 }
 static void Facing(bool staff,bool verticalFirst=false){
  Setup();if(verticalFirst)cursor.transform.children.Reverse();GameScript.equippedIDs[0]=staff?550:549;Hidden();var obj=Field<GameObject>("guide");var m=obj.GetComponent<MeshFilter>().sharedMesh;
  Vector3 a=h.transform.TransformPoint(mesh.vertices[0]),b=h.transform.TransformPoint(mesh.vertices[1]),c=h.transform.TransformPoint(mesh.vertices[2]);Check(Area(a,b,c)<0&&material.culling==2,"synthetic half-turn plane faces camera under CullBack");
  for(int i=0;i<m.triangles.Length;i+=3){a=obj.transform.TransformPoint(m.vertices[m.triangles[i]]);b=obj.transform.TransformPoint(m.vertices[m.triangles[i+1]]);c=obj.transform.TransformPoint(m.vertices[m.triangles[i+2]]);Check(Area(a,b,c)<-.00001,(staff?"staff cone":"aim ray")+" is front-facing with synthetic quad, ideal transform and CullBack material");}
  if(!staff){var start=(obj.transform.TransformPoint(m.vertices[0])+obj.transform.TransformPoint(m.vertices[1]))*.5f;var end=(obj.transform.TransformPoint(m.vertices[2])+obj.transform.TransformPoint(m.vertices[3]))*.5f;Check(Near(start.x,0)&&Near(start.y,0),"rotated ray starts at player in world coordinates");Check(Math.Abs(end.x*cursor.transform.position.y-end.y*cursor.transform.position.x)<.01,"diagonal ray remains collinear after synthetic facing flip");}
  ControllerCombatGuide.Reset();
 }
 static void ShakeGuides(){
  foreach(bool staff in new[]{true,false}){
   Setup();Camera.main.scale=Camera.main.pixelRect.height/(2*OrthographicHalfHeight);GameScript.equippedIDs[0]=staff?550:549;Hidden();var obj=Field<GameObject>("guide");
   // Model a text-popup state with combat active and talking false. This checks
   // guide behavior for those flags, not the game's actual dialogue transitions.
   Check(!GameScript.talking&&GameScript.combatMode,"synthetic text-popup state retains requested combat/talking flags");
   foreach(double roll in CameraRolls){
    Camera.main.RollDegrees=roll;ControllerCombatGuide.UpdateVisual();
    Check(!h.enabled&&!v.enabled&&Field<MeshRenderer>("guideRenderer").enabled,
      "synthetic camera roll retains "+(staff?"filled staff cone":"normal aim ray")+" at representative roll "+Camera.main.RollDegrees);
    var displayed=obj.GetComponent<MeshFilter>().sharedMesh;
    bool reachesEdge=false;
    foreach(var vertex in displayed.vertices){var world=obj.transform.TransformPoint(vertex);var screen=Camera.main.WorldToScreenPoint(world);
     // The ray has finite thickness, so its far corners may straddle the edge;
     // the filled cone's actual polygon vertices must lie inside the real quad.
     if(staff){
      Check(screen.x>=Camera.main.pixelRect.xMin-.01&&screen.x<=Camera.main.pixelRect.xMax+.01&&screen.y>=Camera.main.pixelRect.yMin-.01&&screen.y<=Camera.main.pixelRect.yMax+.01,"staff cone clips to rotated screen viewport, not its larger world AABB");
      reachesEdge|=Math.Abs(screen.x-Camera.main.pixelRect.xMin)<.01||Math.Abs(screen.x-Camera.main.pixelRect.xMax)<.01||Math.Abs(screen.y-Camera.main.pixelRect.yMin)<.01||Math.Abs(screen.y-Camera.main.pixelRect.yMax)<.01;
      double radius=Math.Sqrt((double)vertex.x*vertex.x+(double)vertex.y*vertex.y);
      if(radius>.001){double dx=cursor.transform.position.x-obj.transform.position.x,dy=cursor.transform.position.y-obj.transform.position.y;double angle=Math.Abs(Math.Atan2(dx*vertex.y-dy*vertex.x,dx*vertex.x+dy*vertex.y)*180/Math.PI);Check(angle<=AimAssistGeometry.HalfAngleDegrees+.001,"rolled viewport preserves world-space eight-degree cone");}
     }
    }
    if(staff){Check(reachesEdge,"filled cone still reaches actual screen edge during shake");Check(displayed.triangles.Length==(displayed.vertices.Length-2)*3,"rolled cone retains single nonoverlapping fan");}
   }
   Camera.main.RollDegrees=0;ControllerCombatGuide.UpdateVisual();Check(!h.enabled&&!v.enabled&&Field<MeshRenderer>("guideRenderer").enabled,"synthetic camera roll reset keeps controller guide");
   ControllerCombatGuide.Reset();
  }
  var invalid=AimAssistGeometry.VisibleConePolygon(new Vector2(0,0),new Vector2(1,0),new[]{new Vector2(-1,-1),new Vector2(float.NaN,1),new Vector2(1,1)});Check(invalid.Length==0,"convex viewport overload rejects nonfinite corners");
  invalid=AimAssistGeometry.VisibleConePolygon(new Vector2(0,0),new Vector2(1,0),new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(2,0)});Check(invalid.Length==0,"convex viewport overload rejects collapsed viewport");
 }
 static void Main(string[] args){ShakeGuides();if(args.Length>0){Facing(args[0]=="--facing-staff");Console.WriteLine("Synthetic guide facing: "+checks+" checks passed.");return;}Facing(false);Facing(true);Facing(false,true);Facing(true,true);Staff();Geometry();Setup();Hidden();var guide=Field<GameObject>("guide");var r=Field<MeshRenderer>("guideRenderer");Check(glyph.enabled,"center glyph untouched");Check(r.sharedMaterial==material&&guide.GetComponent<MeshFilter>().sharedMesh==mesh,"fixture source mesh/material reused without copies");Check(Near(guide.transform.localScale.y*2,.2f),"fixture .2 world thickness retained");Check(r.sortingOrder==7&&r.sortingLayerID==4,"fixture source sorting retained");Check(Near(guide.transform.position.x,48)&&Near(guide.transform.position.y,48*3f/14f),"ray starts at player and follows visible cursor rather than resolved attack aim");
  var before=guide;ControllerCombatGuide.UpdateVisual();Check(Field<GameObject>("guide")==before,"guide reused across frames");
  Camera.main.scale=20;Camera.main.pixelRect=new Rect(100,50,1200,800);Camera.main.offset=new Vector3(2,1,0);ControllerCombatGuide.UpdateVisual();Check(Near(guide.transform.position.x,16),"zoom/offset viewport recomputes edge position");Check(Near(guide.transform.localScale.y*2,.2f),"zoom does not alter fixture source world thickness");
  Owned=false;ControllerCombatGuide.Observe(cursor);Check(h.enabled&&v.enabled&&!r.enabled,"mouse takeover restores bars in same-frame Observe");Owned=true;Hidden();
  GameScript.inventoryOpen=true;Restored("inventory hides custom guide and restores native bars");GameScript.inventoryOpen=false;Hidden();GameScript.pausing=true;Restored("pause cleans custom guide");GameScript.pausing=false;Hidden();GameScript.dead=true;Restored("death cleans custom guide");GameScript.dead=false;Hidden();GameScript.talking=true;Hidden();Check(Field<MeshRenderer>("guideRenderer").enabled,"dialogue popup retains controller combat guide");GameScript.talking=false;Hidden();GameScript.buildMode=true;Restored("build mode cleans custom guide");GameScript.buildMode=false;Hidden();GameScript.combatMode=false;Restored("exploration cleans custom guide");GameScript.combatMode=true;Hidden();
  ControllerAim.Success=false;Restored("missing aim ownership cleans custom guide");ControllerAim.Success=true;Hidden();MenuScript.player.GetComponent<NetworkView>().isMine=false;Restored("remote player excluded");MenuScript.player.GetComponent<NetworkView>().isMine=true;Hidden();var camera=Camera.main;Camera.main=null;Restored("missing camera restores bars");Camera.main=camera;Hidden();var player=MenuScript.player;MenuScript.player=null;Restored("missing player restores bars");MenuScript.player=player;Hidden();cursor.gameObject.activeInHierarchy=false;Restored("hidden cursor restores bars");cursor.gameObject.activeInHierarchy=true;Hidden();
  var view=MenuScript.player.GetComponent<NetworkView>();view.destroyed=true;Restored("missing local network ownership restores bars");view.destroyed=false;Hidden();
  cursor.transform.position=MenuScript.player.transform.position;Restored("zero-length visible aim axis restores bars");cursor.transform.position=new Vector3(14,3,0);Hidden();
  Camera.main.Behind=true;Restored("behind-camera projection restores bars");Camera.main.Behind=false;Hidden();
  ControllerCombatGuide.Reset();Check(h.enabled&&v.enabled&&guide.destroyed&&!mesh.destroyed&&!material.destroyed,"reset restores native bars and destroys only owned object");
  Setup();v.enabled=false;Hidden();Owned=false;ControllerCombatGuide.UpdateVisual();Check(h.enabled&&!v.enabled,"previously disabled native bar remains disabled");
  Setup();GameObject.ThrowOnAdd=true;ControllerCombatGuide.UpdateVisual();GameObject.ThrowOnAdd=false;Check(h.enabled&&v.enabled&&glyph.enabled,"renderer allocation failure preserves native visuals");ControllerCombatGuide.Reset();
  Setup();Hidden();var oldH=h;var oldV=v;var oldGuide=Field<GameObject>("guide");var newCursor=new GameObject("actionCursor").AddComponent<ActionCursor>();ControllerCombatGuide.Observe(newCursor);Check(oldH.enabled&&oldV.enabled&&oldGuide.destroyed,"new scene cursor restores old renderers before rebinding");ControllerCombatGuide.UpdateVisual();Check(Field<GameObject>("guide")==null,"missing guide planes fail closed");ControllerCombatGuide.Reset();
  Console.WriteLine("Combat guide: "+checks+" checks passed.");
 }
}
