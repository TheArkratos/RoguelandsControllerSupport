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
  foreach(string context in new[]{"inventory","pause","dead","build","beaming","freeze","remote","missingplayer"}){
   Setup(true);
   if(context=="inventory")GameScript.inventoryOpen=true;
   if(context=="pause")GameScript.pausing=true;
   if(context=="dead")GameScript.dead=true;
   if(context=="build")GameScript.buildMode=true;
   if(context=="beaming")PlayerScript.beaming=true;
   if(context=="freeze")GadgetCoreAPI.Frozen=true;
   if(context=="remote")MenuScript.player.view.isMine=false;
   if(context=="missingplayer")MenuScript.player=null;
   Tick(()=>{Button(0,true);Button(9,true);});Check(!Hotkey(5)&&toggles==0,"hotkeys and assist toggle blocked in "+context);
  }
  Setup(true);GameScript.talking=true;Tick(()=>{Button(0,true);Button(9,true);});
  Check(Hotkey(5)&&toggles==1,"native NPC dialogue does not blanket-block hotkeys or assist toggle");
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
  // The actual shared policy is live, including after per-frame input was cached.
  bool owns=true;ControllerInputOwnership.Configure(()=>owns);Setup(true);
  Tick(()=>{Axis(0,short.MaxValue);Button(0,true);Button(9,true);});
  Check(ControllerGameplayInput.PlayerButton("Right"),"controller caches movement while owned");
  int before=toggles;owns=false;Input.Down.Add("Jump");Input.Down.Add("Cancel");
  Check(!ControllerGameplayInput.PlayerButton("Right"),"same-frame handoff removes cached movement");
  Check(ControllerGameplayInput.PlayerButtonDown("Jump"),"keyboard Jump unchanged despite held raw A");
  Check(ControllerGameplayInput.GameButtonDown("Cancel"),"keyboard Cancel unchanged despite raw pad");
  Check(!Hotkey(5),"same-frame handoff clears queued hotkey");
  ControllerGameplayActions.Prepare(game);Check(toggles==before,"L3 cannot toggle while keyboard owns input");
  Tick(()=>{Button(9,false);Button(9,true);});Check(toggles==before,"fresh raw L3 cannot toggle while source false");
  Check(!Hotkey(5),"raw hotkey cannot inject while source false");
  Input.Down.Add("Inventory");Input.Down.Add("CombatMode");
  Check(ControllerGameplayInput.GameButtonDown("Inventory")&&ControllerGameplayInput.GameButtonDown("CombatMode"),"native menu/mode retain exact route while source false");
  owns=true;Tick();Check(!Hotkey(5)&&toggles==before,"held hotkey and L3 do not rearm at handoff");
  Tick(()=>{Button(0,false);Button(9,false);});Tick(()=>{Button(0,true);Button(9,true);});
  Check(Hotkey(5)&&toggles==before+1,"release and fresh press rearm hotkey and L3");
  // Primary trigger must release before controller ownership can fire again.
  Tick(()=>Axis(5,short.MaxValue));Check(ControllerGameplayInput.PlayerAxis("Attack")<0,"owned primary trigger fires");
  owns=false;Input.Axes["Attack"]=0.25f;Input.Axes["Horizontal"]=-0.75f;
  Check(ControllerGameplayInput.PlayerAxis("Attack")==0.25f&&ControllerGameplayInput.PlayerAxis("Horizontal")==-0.75f,"handoff preserves exact native axes despite held controller stick/trigger");
  Input.Axes.Clear();owns=true;Tick();Check(ControllerGameplayInput.PlayerAxis("Attack")==0,"held primary cannot fire after ownership handoff");
  Tick(()=>Axis(5,short.MinValue));ControllerGameplayInput.PlayerAxis("Attack");
  Tick(()=>Axis(5,short.MaxValue));Check(ControllerGameplayInput.PlayerAxis("Attack")<0,"released primary trigger rearms");
  owns=false;Input.Held.Add("Up");Input.Up.Add("Right");
  Check(ControllerGameplayInput.PlayerButton("Up")&&ControllerGameplayInput.PlayerButtonUp("Right"),"native held/up reads stay intact while source false");
  Input.Held.Clear();Input.Up.Clear();
  ControllerInputOwnership.Reset();Tick(()=>Axis(0,short.MaxValue));
  Check(!ControllerGameplayInput.PlayerButton("Right"),"unconfigured policy defaults to native input");
  ControllerInputOwnership.Configure(()=>true);
  bool story=false, storyOwner=true;ControllerInputOwnership.Configure(()=>storyOwner,()=>story);
  Setup(true);Tick(()=>Button(0,true));story=true;
  Check(!Hotkey(5),"live story modal blocks a hotkey queued earlier in the same frame");
  foreach(string name in new[]{"Left","Right","Up","Down","Jump","Dash Left","Dash Right"}){
   Input.Down.Add(name);Input.Held.Add(name);Input.Up.Add(name);
   Check(!ControllerGameplayInput.PlayerButton(name)&&!ControllerGameplayInput.PlayerButtonDown(name),"story blocks controller movement/action press adapters "+name);
   Check(ControllerGameplayInput.PlayerButtonUp(name),"story preserves native release cleanup "+name);
  }
  Input.Up.Clear();Axis(0,short.MaxValue);Time.frameCount++;
  ControllerGameplayInput.PlayerButtonUp("Right"); // Sample held stick while modal owns input.
  Axis(0,0);Time.frameCount++;
  Check(ControllerGameplayInput.PlayerButtonUp("Right"),"stick release during story reaches native movement cleanup");
  foreach(string name in new[]{"Cancel","Inventory","CombatMode","UseItem1","UseItem5","UseItem6"}){
   Input.Down.Add(name);Check(!ControllerGameplayInput.GameButtonDown(name),"story blocks named gameplay adapter "+name);
  }
  Input.Axes["Attack"]=-1;Input.Axes["Horizontal"]=1;
  Check(ControllerGameplayInput.PlayerAxis("Attack")==0 && ControllerGameplayInput.PlayerAxis("Horizontal")==0,"story blocks native joystick axes while controller owned");
  Check(!ControllerGameplayInput.GamePausePressed()&&!ControllerGameplayInput.GameDroidButtonDown("Droid",game)&&!ControllerGameplayInput.ExplorationPrimaryHeld()&&!ControllerGameplayInput.ExplorationContextSwitchPressed(),"story prevents pause/droid/exploration conflicts");
  Tick(()=>Button(9,true));Check(toggles==0,"story blocks aim-assist toggle");
  ControllerGameplayInput.ConsumeInventoryInput();story=false;
  Check(!Hotkey(5)&&ControllerGameplayInput.PlayerAxis("Attack")==0,"story close frame remains consumed after modal hides");
  Input.Axes.Clear();Tick(()=>Axis(5,short.MaxValue));
  Check(ControllerGameplayInput.PlayerAxis("Attack")==0,"primary held during story must release before gameplay resumes");
  Tick(()=>Axis(5,short.MinValue));ControllerGameplayInput.PlayerAxis("Attack");Tick(()=>Axis(5,short.MaxValue));
  Check(ControllerGameplayInput.PlayerAxis("Attack")<0,"primary re-arms after fresh release following story");
  Input.Axes["Attack"]=-1;
  story=true;storyOwner=false;Input.Down.Add("Inventory");Input.Down.Add("Jump");
  Check(ControllerGameplayInput.GameButtonDown("Inventory")&&ControllerGameplayInput.PlayerButtonDown("Jump")&&ControllerGameplayInput.PlayerAxis("Attack")==-1,"mouse/keyboard ownership retains exact native values with story still open");
  Input.Down.Clear();Input.Held.Clear();Input.Up.Clear();Input.Axes.Clear();ControllerInputOwnership.Configure(()=>true);
  Console.WriteLine("Gameplay actions: "+checks+" checks passed.");
 }
}
