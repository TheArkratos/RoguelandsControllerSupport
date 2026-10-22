using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ControllerSupport;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;
class Program {
 static int checks; static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static Collider Target(GameScript g,string name,int layer=0,string tag="Untagged"){var o=new GameObject(name){layer=layer,tag=tag};g.menuPlanets.Child(o);o.Add<Renderer>();var c=o.Add<Collider>();c.bounds=new Bounds{center=new Vector3(12,4,0)};o.Add<ButtonMenu>();return c;}
 static bool Can(GameScript g,int n){string r;return PortalMenuContext.CanActivate(g,n,out r);}
 static void Main(){
  var mouse=typeof(Input).GetProperty("mousePosition").GetGetMethod();var click=typeof(Input).GetMethod("GetMouseButtonDown");
  var il=new List<CodeInstruction>{new CodeInstruction(OpCodes.Call,mouse),new CodeInstruction(OpCodes.Call,mouse),new CodeInstruction(OpCodes.Call,click),new CodeInstruction(OpCodes.Call,click),new CodeInstruction(OpCodes.Call,click)};il[0].labels.Add("branch");
  var bad=PortalGadgetCoreInputPatch.Transpiler(il.Take(4)).ToList();Check(Equals(bad[0].operand,mouse),"wrong counts leave entire IL untouched");
  var patched=PortalGadgetCoreInputPatch.Transpiler(il).ToList();Check(!Equals(patched[0].operand,mouse)&&!Equals(patched[2].operand,click),"verified reads adapted");Check(patched[0].labels.Count==1&&Equals(il[0].operand,mouse),"labels preserved and originals not mutated");
  var g=new GameScript();var p=Target(g,"0",16,"planet");g.planetGuard[0]=p.gameObject;g.Uses(0,-1);var tele=Target(g,"bPlanetTele");Target(g,"bChallenge");Target(g,"bPlanetPageBack");Target(g,"bPlanetPageForward");
  var slots=new Collider[130];PortalMenuContext.CollectTargets(g,slots);Check(slots[100]==p&&slots[126]==tele&&slots[127]!=null&&slots[128]==null,"collect eligible roots only");
  g.Uses(0,0);Check(!Can(g,100)&&!Can(g,126),"zero uses rejects choosing and teleport");g.Uses(0,2);p.GetComponent<ButtonMenu>().isPlanet=true;Check(!Can(g,100),"native lock state rejects");p.GetComponent<ButtonMenu>().isPlanet=false;
  GameScript.curPlanetTrue=0;Check(!Can(g,126),"same destination rejects");GameScript.curPlanetTrue=-1;g.Cooling(true);Check(!Can(g,126),"cooldown rejects");g.Cooling(false);Check(Can(g,126),"destination available");
  g.credits=4999;Check(!Can(g,127),"cost rejection");g.credits=5000;Check(Can(g,127),"exact credits accepted");GameScript.challengeLevel=3;Check(!Can(g,127),"max level rejects");GameScript.challengeLevel=0;
  GadgetCoreAPI.Frozen=true;Check(!Can(g,100)&&!Can(g,127),"frozen input blocks");GadgetCoreAPI.Frozen=false;
  PortalMenuContext.PrepareClick(g,100,p);Check(PortalMenuContext.NativeClickAllowed(g),"vanilla planet native route");PortalGadgetCoreInputPatch.Prefix(g);Check(!PortalMenuContext.GadgetMouseButtonDown(0),"vanilla planet not duplicated by loader");PortalGadgetCoreInputPatch.Finalizer(null);
  PlanetRegistry.PlanetSelectorPages=2;PlanetRegistry.PlanetSelectorPage=2;PlanetRegistry.Custom(new PlanetInfo{PortalUses=3});Check(Can(g,100)&&Can(g,128)&&Can(g,129),"custom page choices and arrows supported");
  PortalMenuContext.PrepareClick(g,100,p);Check(!PortalMenuContext.NativeClickAllowed(g),"custom choice bypasses native ID branch");Check(!PortalMenuContext.GadgetMouseButtonDown(0),"no loader scope no synthetic action");PortalGadgetCoreInputPatch.Prefix(g);Check(PortalMenuContext.GadgetMouseButtonDown(0)&&PortalMenuContext.GadgetMousePosition().x==120,"loader scope receives retained click point");
  var ex=new Exception("native failure");Check(PortalGadgetCoreInputPatch.Finalizer(ex)==ex&&!PortalMenuContext.GadgetMouseButtonDown(0),"exception cleanup preserves exception");
  PortalGadgetCoreInputPatch.Prefix(new GameScript());Check(!PortalMenuContext.GadgetMouseButtonDown(0),"foreign game cannot borrow snapshot");PortalGadgetCoreInputPatch.Finalizer(null);
  Time.frameCount++;PortalGadgetCoreInputPatch.Prefix(g);Check(!PortalMenuContext.GadgetMouseButtonDown(0),"stale frame cannot replay");Check(PortalMenuContext.NativeClickAllowed(g),"next frame restores ordinary native routing");PortalGadgetCoreInputPatch.Finalizer(null);
  PortalMenuContext.PrepareClick(g,100,p);PortalGadgetCoreInputPatch.Prefix(g);Input.mousePosition=new Vector3(3,0,0);Check(!PortalMenuContext.GadgetMouseButtonDown(0)&&PortalMenuContext.GadgetMousePosition().x==3,"mouse movement wins");Input.Left=true;Check(PortalMenuContext.GadgetMouseButtonDown(0),"physical click preserved");Input.Left=false;PortalGadgetCoreInputPatch.Finalizer(null);
  PortalMenuContext.PrepareClick(g,128,g.menuPlanets.children.First(o=>o.name=="bPlanetPageBack").GetComponent<Collider>());PortalGadgetCoreInputPatch.Prefix(g);Check(!PortalMenuContext.NativeClickAllowed(g)&&PortalMenuContext.GadgetMouseButtonDown(0),"page arrows loader only");PortalGadgetCoreInputPatch.Finalizer(null);
  PortalMenuContext.PrepareClick(g,100,p);PortalGadgetCoreInputPatch.Prefix(g);GadgetCoreAPI.Frozen=true;Check(!PortalMenuContext.GadgetMouseButtonDown(0),"freeze after prepare cancels dispatch");GadgetCoreAPI.Frozen=false;Input.Right=true;Check(!PortalMenuContext.GadgetMouseButtonDown(0)&&PortalMenuContext.GadgetMouseButtonDown(1),"physical right click wins without synthetic left");Input.Right=false;PortalGadgetCoreInputPatch.Finalizer(null);
  var savedRoot=g.menuPlanets;PortalMenuContext.PrepareClick(g,100,p);g.menuPlanets=new GameObject();PortalGadgetCoreInputPatch.Prefix(g);Check(!PortalMenuContext.GadgetMouseButtonDown(0)&&!PortalMenuContext.NativeClickAllowed(g),"replaced root cannot replay or fall through into native action");PortalGadgetCoreInputPatch.Finalizer(null);g.menuPlanets=savedRoot;
  PortalMenuContext.PrepareClick(g,100,p);p.enabled=false;PortalGadgetCoreInputPatch.Prefix(g);Check(!PortalMenuContext.NativeClickAllowed(g)&&!PortalMenuContext.GadgetMouseButtonDown(0),"disabled captured target cannot fall through into native action");PortalGadgetCoreInputPatch.Finalizer(null);p.enabled=true;
  Check(!Can(g,99)&&!Can(g,130),"out of range slots rejected");
  PortalMenuContext.Reset();Check(PortalMenuContext.NativeClickAllowed(g)&&Can(g,100),"reset clears snapshot but retains installed bridge readiness");
  p.gameObject.GetComponent<Renderer>().enabled=false;Check(!Can(g,100),"hidden planet rejected");p.gameObject.GetComponent<Renderer>().enabled=true;g.menuPlanets=new GameObject();Check(!Can(g,100),"foreign root rejected");
  Console.WriteLine("Portal context: "+checks+" checks passed.");
 }
}
