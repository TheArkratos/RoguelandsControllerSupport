using System;
using ControllerSupport;
using UnityEngine;
using GadgetCore.API;
class Program {
 static int checks,toggles;static uint timestamp;
 static JoystickInputState pad;static GameScript game;
 static void Check(bool v,string why){checks++;if(!v)throw new Exception(why);}
 static void Button(int n,bool held){pad.Apply(new RawJoystickEvent{Type=1,Number=(byte)n,Value=(short)(held?1:0),TimeMilliseconds=++timestamp});}
 static void Axis(int n,short value){pad.Apply(new RawJoystickEvent{Type=2,Number=(byte)n,Value=value,TimeMilliseconds=++timestamp});}
 static void Tick(Action emit=null){Time.frameCount++;pad.BeginFrame();Input.Down.Clear();if(emit!=null)emit();ControllerGameplayActions.Prepare(game);}
 static void Setup(bool combat=false,string text=null){
  game=new GameScript();pad=new JoystickInputState();pad.Connect();toggles=0;
  GameScript.inventoryOpen=GameScript.pausing=GameScript.dead=GameScript.talking=GameScript.buildMode=PlayerScript.beaming=GadgetCoreAPI.Frozen=false;
  GameScript.combatMode=combat;MenuScript.player=new GameObject();
  var config=BindingConfig.Parse((text??BindingConfig.DefaultText).Split('\n'));
  ControllerGameplayInput.Configure(()=>pad,config);ControllerGameplayActions.Configure(()=>pad,config,()=>toggles++);Tick();Tick();
 }
 static bool Hotkey(int n){return ControllerGameplayInput.GameButtonDown("UseItem"+n);}
 static void Main(){
  foreach(bool combat in new[]{false,true}){
   Setup(combat);Tick(()=>Button(0,true));Check(Hotkey(5),"A activates native slot 5 in mode "+combat);Check(!Hotkey(5),"slot edge consumed once");
   Input.Down.Add("Jump");Check(!ControllerGameplayInput.PlayerButtonDown("Jump"),"A cannot also native-jump");Tick();Check(!Hotkey(5),"held A does not repeat");Tick(()=>Button(0,false));Tick(()=>Button(0,true));Check(Hotkey(5),"fresh A repeats");
   Setup(combat);Tick(()=>{Button(1,true);Input.Down.Add("Cancel");});Check(Hotkey(6),"B activates slot6");Check(!ControllerGameplayInput.GameButtonDown("Cancel"),"B cannot exit combat or pause");Tick(()=>Button(1,false));Input.Down.Add("Cancel");Check(ControllerGameplayInput.GameButtonDown("Cancel"),"keyboard Escape retains native route");
   Setup(combat);Tick(()=>Axis(7,short.MinValue));Check(Hotkey(1),"up activates slot1");Tick(()=>Axis(6,short.MaxValue));Check(!Hotkey(2),"diagonal roll cannot activate right");Tick(()=>Axis(7,0));Check(!Hotkey(2),"first-direction latch survives partial release");Tick(()=>Axis(6,0));Tick(()=>Axis(6,short.MaxValue));Check(Hotkey(2),"neutral then right activates slot2");
   Tick(()=>Axis(6,0));Tick(()=>Axis(7,short.MaxValue));Check(Hotkey(3),"down activates slot3");Tick(()=>Axis(7,0));Tick(()=>Axis(6,short.MinValue));Check(Hotkey(4),"left activates slot4");
  }
  foreach(string context in new[]{"inventory","pause","dead","talking","build","beaming","freeze","remote","missingplayer"}){
   Setup(true);
   if(context=="inventory")GameScript.inventoryOpen=true;
   if(context=="pause")GameScript.pausing=true;
   if(context=="dead")GameScript.dead=true;
   if(context=="talking")GameScript.talking=true;
   if(context=="build")GameScript.buildMode=true;
   if(context=="beaming")PlayerScript.beaming=true;
   if(context=="freeze")GadgetCoreAPI.Frozen=true;
   if(context=="remote")MenuScript.player.view.isMine=false;
   if(context=="missingplayer")MenuScript.player=null;
   Tick(()=>{Button(0,true);Button(9,true);});Check(!Hotkey(5)&&toggles==0,"hotkeys and assist toggle blocked in "+context);
  }
  Setup();GameScript.inventoryOpen=true;Tick(()=>Button(0,true));GameScript.inventoryOpen=false;Tick();Check(!Hotkey(5),"closing UI with A held requires release");Tick(()=>Button(0,false));Tick(()=>Button(0,true));Check(Hotkey(5),"released UI A can activate new gameplay press");
  Setup();Tick(()=>Button(0,true));ControllerGameplayInput.ConsumeInventoryInput();Check(!Hotkey(5),"same-frame UI consumption invalidates queued action");
  Setup();Tick(()=>{Button(0,true);Button(3,true);});Check(!Hotkey(5),"mode-toggle press suppresses simultaneous hotkey");GameScript.combatMode=true;Tick();Check(!Hotkey(5),"held hotkey cannot carry through mode switch");
  Setup();Tick(()=>{Button(0,true);Button(6,true);});Check(!Hotkey(5),"inventory-open press suppresses simultaneous hotkey");
  Setup();Tick(()=>Button(0,true));GadgetCoreAPI.Frozen=true;Check(!Hotkey(5),"modal opened after Prepare invalidates queued action");GadgetCoreAPI.Frozen=false;Tick();Check(!Hotkey(5),"held edge not replayed after modal");
  Setup(true);Tick(()=>Button(9,true));Check(toggles==1,"L3 toggles once in combat");Tick();Check(toggles==1,"held L3 does not repeat");Tick(()=>Button(9,false));Tick(()=>Button(9,true));Check(toggles==2,"second fresh L3 toggles");
  Setup(false);Tick(()=>Button(9,true));Check(toggles==0,"L3 cannot invisibly toggle assist in exploration");
  Setup(true,BindingConfig.DefaultText.Replace("Hotkey5 = Joystick1Button0","Hotkey5 = Joystick1Button12").Replace("AimAssist = Joystick1Button9","AimAssist = Joystick1Button13"));
  Tick(()=>{Button(0,true);Button(9,true);});Check(!Hotkey(5)&&toggles==0,"old defaults do not survive remapping");Tick(()=>{Button(12,true);Button(13,true);});Check(Hotkey(5)&&toggles==1,"remapped actions work");
  Setup();Tick(()=>Button(0,true));GameScript.combatMode=true;Check(!Hotkey(5),"late mode change invalidates queued action");
  Setup();Tick(()=>Button(0,true));MenuScript.player=new GameObject();Tick();Check(!Hotkey(5),"new local player requires fresh press");
  Setup();Tick(()=>Button(0,true));Application.loadedLevel++;Tick();Check(!Hotkey(5),"scene transition disarms held hotkey");
  Setup();pad.Clear();Tick();pad.Connect();Button(0,true);Tick();Check(!Hotkey(5),"reconnection-held action cannot activate");
  Setup();Tick(()=>{Button(0,true);Button(0,false);Input.Down.Add("Jump");});Check(Hotkey(5)&&!ControllerGameplayInput.PlayerButtonDown("Jump"),"same-frame A tap fires hotkey without native jump");
  Setup();Tick(()=>{Button(1,true);Button(1,false);Input.Down.Add("Cancel");});Check(Hotkey(6)&&!ControllerGameplayInput.GameButtonDown("Cancel"),"same-frame B tap fires hotkey without native Cancel");
  Setup();Tick();Input.Down.Add("UseItem2");Check(Hotkey(2),"physical keyboard hotkey unchanged");
  Console.WriteLine("Gameplay actions: "+checks+" checks passed.");
 }
}
