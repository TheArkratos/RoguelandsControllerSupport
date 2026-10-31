using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using ControllerSupport;
using UnityEngine;
using HarmonyLib;
static class Program {
 static int checks;static JoystickInputState pad;static BoxCollider body;static bool owned;
 const BindingFlags All=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.0005;
 static CodeInstruction[] Adapt(params CodeInstruction[] code)=>( (IEnumerable<CodeInstruction>)typeof(AimAssistAtalantaPatch).GetMethod("Transpiler",All).Invoke(null,new object[]{code})).ToArray();
 static MethodInfo Adapter(){var mouse=typeof(Input).GetProperty("mousePosition").GetGetMethod();return(MethodInfo)Adapt(new CodeInstruction(OpCodes.Call,mouse),new CodeInstruction(OpCodes.Call,mouse))[0].operand;}
 static void Setup(short x=32767,short y=0){
  ControllerAimAssist.Reset();owned=true;ControllerInputOwnership.Configure(()=>owned);
  Input.mousePosition=new Vector3(4,5,6);Input.Click=false;Time.frameCount++;Time.unscaledTime+=1;
  GameScript.combatMode=true;GameScript.dead=GameScript.pausing=GameScript.inventoryOpen=GameScript.buildMode=GameScript.talking=false;
  Camera.main=new Camera();var player=new GameObject("player");player.transform.position=new Vector3(10,20,0);player.AddComponent<NetworkView>();body=player.AddComponent<BoxCollider>();body.size=new Vector3(2.25f,3,1);player.Put<Collider>(body);MenuScript.player=player;
  pad=new JoystickInputState();pad.Connect();pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=x});pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=y});ControllerAim.Configure(()=>pad);
 }
 static Vector3 Place(){
  var mouse=typeof(Input).GetProperty("mousePosition").GetGetMethod();var patched=Adapt(new CodeInstruction(OpCodes.Call,mouse),new CodeInstruction(OpCodes.Call,mouse));
  var x=(Vector3)((MethodInfo)patched[0].operand).Invoke(null,null);var y=(Vector3)((MethodInfo)patched[1].operand).Invoke(null,null);
  return new Vector3(Camera.main.ScreenToWorldPoint(x).x,Camera.main.ScreenToWorldPoint(y).y,0);
 }
 static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((double)(a.x-b.x)*(a.x-b.x)+(double)(a.y-b.y)*(a.y-b.y));
 static void CheckPlacement(string label){
  Check(ControllerAim.TryWorldPoint(out var white),label+": fixture owns white controller aim");
  Vector3 center=ControllerAimAssist.PlayerCenter(),eye=Place();double dx=white.x-center.x,dy=white.y-center.y;
  Check(Near(Distance(eye,center),2*body.bounds.size.x),label+": eye center remains exactly two body world widths away");
  Check(Math.Abs((eye.x-center.x)*dy-(eye.y-center.y)*dx)<.001,label+": placement follows visible white aim direction");
  Check((eye.x-center.x)*dx+(eye.y-center.y)*dy>0,label+": placement stays forward rather than behind the player");
  Check(ControllerAim.TryWorldPoint(out var after)&&Near(after.x,white.x)&&Near(after.y,white.y),label+": placement does not move visible aim");
 }
 static void GuardShape(){
  var mouse=typeof(Input).GetProperty("mousePosition").GetGetMethod();
  for(int count=0;count<=3;count++){
   var code=new List<CodeInstruction>{new CodeInstruction(OpCodes.Nop)};object label=new object(),block=new object();
   for(int i=0;i<count;i++){var instruction=new CodeInstruction(OpCodes.Call,mouse);instruction.labels.Add(label);instruction.blocks.Add(block);code.Add(instruction);}
   code.Add(new CodeInstruction(OpCodes.Ret));var result=Adapt(code.ToArray());
   Check(result.Length==code.Count,"Atalanta read-count "+count+" preserves instruction count");
   if(count==2){
    Check(result.Count(i=>i.operand is MethodInfo m&&m.DeclaringType==typeof(ControllerAtalantaPlacement)&&m.Name=="MousePosition")==2,"exact two reads use only dedicated Atalanta placement adapter");
    Check(result[1].labels.Single()==label&&result[1].blocks.Single()==block&&result[2].labels.Single()==label,"replacement retains native branch labels and exception boundaries");
    Check(Equals(code[1].operand,mouse)&&!ReferenceEquals(code[1],result[1]),"transpiler copies instead of mutating input instructions");
   }else Check(result.SequenceEqual(code),"unexpected Atalanta mouse-read shape "+count+" remains unchanged");
  }
 }
 static void Main(){
  Setup();body.center=new Vector3(.5f,1,0);var placed=Place();var center=ControllerAimAssist.PlayerCenter();
  Check(Near(Distance(placed,center),2*body.bounds.size.x),"controller Atalanta Eye center is two world body widths from body center instead of the general crosshair radius");
  Check(ControllerAtalantaPlacement.BodyWidthMultiplier==2,"placement multiplier matches accepted two-width design");
  foreach(var direction in new[]{new short[]{32767,0},new short[]{-32767,0},new short[]{0,-32767},new short[]{0,32767},new short[]{32767,-32767},new short[]{-32767,-32767},new short[]{32767,32767},new short[]{-32767,32767}}){Setup(direction[0],direction[1]);CheckPlacement("direction "+direction[0]+","+direction[1]);}
  Setup();body.center=new Vector3(.5f,1,0);CheckPlacement("body center differs from player transform");
  body.size=new Vector3(4,3,1);body.transform.lossyScale=new Vector3(7,2,1);CheckPlacement("world collider width is used exactly once without multiplying transform scale");
  Setup();body.center=new Vector3(.5f,1,0);Camera.main.WorldProjection=p=>new Vector3((p.x+3)*17,(p.y-2)*17,8);Camera.main.ScreenProjection=p=>new Vector3(p.x/17-3,p.y/17+2,0);CheckPlacement("camera zoom and offset preserve world-distance placement");
  Setup();int redirected=0;ControllerAim.AttackResolver=p=>{redirected++;return new Vector3(90,-50,0);};int searches=Physics.Searches,loads=Resources.Loads;CheckPlacement("Atalanta ignores enemy-target lead adapter");Check(redirected==0&&Physics.Searches==searches&&Resources.Loads==loads,"placement never invokes attack resolution, target searches or resource loading");
  var first=(Vector3)Adapter().Invoke(null,null);var second=(Vector3)Adapter().Invoke(null,null);Check(Near(first.x,second.x)&&Near(first.y,second.y),"both native X/Y reads receive coherent same-frame placement");
  Setup();ControllerAim.TryWorldPoint(out var fallbackWhite);
  foreach(string invalid in new[]{"missing","disabled","zero-width","negative-width","infinite-width","invalid-center","zero-direction"}){
   body.enabled=true;body.size=new Vector3(2.25f,3,1);body.center=Vector3.zero;MenuScript.player.Put<Collider>(body);
   if(invalid=="missing")MenuScript.player.Put<Collider>(null);if(invalid=="disabled")body.enabled=false;
   if(invalid=="zero-width")body.size=new Vector3(0,3,1);if(invalid=="negative-width")body.size=new Vector3(-1,3,1);if(invalid=="infinite-width")body.size=new Vector3(float.PositiveInfinity,3,1);
   if(invalid=="invalid-center")body.center=new Vector3(float.NaN,0,0);if(invalid=="zero-direction")body.center=fallbackWhite-MenuScript.player.transform.position;
   var fallback=Place();Check(Near(fallback.x,fallbackWhite.x)&&Near(fallback.y,fallbackWhite.y),invalid+" body geometry falls back to unassisted white cursor point");
  }
  foreach(string gate in new[]{"ownership","combat","pause","inventory","dead","build","remote","no-network","disconnected"}){
   Setup();ControllerAim.TryWorldPoint(out _); // All context changes occur after this same-frame cache read.
   if(gate=="ownership")owned=false;if(gate=="combat")GameScript.combatMode=false;if(gate=="pause")GameScript.pausing=true;if(gate=="inventory")GameScript.inventoryOpen=true;if(gate=="dead")GameScript.dead=true;if(gate=="build")GameScript.buildMode=true;
   if(gate=="remote")MenuScript.player.GetComponent<NetworkView>().isMine=false;if(gate=="no-network")MenuScript.player.Put<NetworkView>(null);if(gate=="disconnected"){pad.Clear();Time.frameCount++;}
   Vector3 mouse=(Vector3)Adapter().Invoke(null,null);Check(mouse.x==Input.mousePosition.x&&mouse.y==Input.mousePosition.y&&mouse.z==Input.mousePosition.z,gate+" preserves exact native physical mouse coordinates");
  }
  Setup();Camera.main=null;var original=(Vector3)Adapter().Invoke(null,null);Check(original.x==4&&original.y==5&&original.z==6,"missing camera preserves mouse safely");
  Setup();MenuScript.player=null;original=(Vector3)Adapter().Invoke(null,null);Check(original.x==4&&original.y==5&&original.z==6,"missing player preserves mouse safely");
  Setup();Camera.main.WorldProjection=p=>new Vector3(float.NaN,0,1);original=(Vector3)Adapter().Invoke(null,null);Check(original.x==4&&original.y==5&&original.z==6,"nonfinite camera projection preserves mouse safely");
  Setup();Camera.main.WorldProjection=p=>new Vector3(p.x,p.y,-1);original=(Vector3)Adapter().Invoke(null,null);Check(original.x==4&&original.y==5&&original.z==6,"behind-camera projection preserves mouse safely");
  Setup();owned=false;Input.mousePosition=new Vector3(77,-9,123);ControllerAim.AttackResolver=p=>throw new Exception("mouse adapter touched controller aim resolver");original=(Vector3)Adapter().Invoke(null,null);Check(original.x==77&&original.y==-9&&original.z==123,"mouse ownership remains exact even while controller is connected and stick held");
  GuardShape();ControllerAimAssist.Reset();
  Console.WriteLine("Atalanta placement: "+checks+" checks passed.");
 }
}
