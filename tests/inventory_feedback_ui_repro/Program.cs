using System;
using System.Reflection;
using ControllerSupport;
using UnityEngine;
class Program {
 static int checks;
 static void Check(bool ok,string message) {checks++;if (!ok) throw new Exception(message);}
 static bool Near(float a,float b){return Math.Abs(a-b)<0.01f;}
 static void Draw() {GUI.Labels.Clear();GUI.Boxes.Clear();GUI.Draws.Clear();GameObject.Runner.GetType().GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(GameObject.Runner,null);}
 static Bounds Badge(float left,float right){return new Bounds{min=new Vector3(left,100,0),max=new Vector3(right,130,0)};}
 static void CardsBoundedAndCentered(){foreach(Rect r in GUI.Boxes){Check(r.x>=0&&r.y>=0&&r.xMax<=Screen.width&&r.yMax<=Screen.height,"card inside viewport");Check(Near(r.x+r.width/2,Screen.width/2f),"card horizontally centered");}}
 static void SnugQuantity(GameScript game,Collider slot,BindingConfig bindings,int width,int height,float left,float right) {
  Screen.width=width;Screen.height=height;game.Hold(7,23);slot.bounds=new Bounds{min=new Vector3(left,100,0),max=new Vector3(right+100,160,0)};
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true,Badge(left,right));Draw();
  LabelDraw number=GUI.Draws[GUI.Draws.Count-1];
  Check(Near(number.glyphX,right+1f),"quantity glyph starts one screen pixel beside badge at "+width+"x"+height+"; actual gap="+(number.glyphX-right));
 }
 static void EdgeQuantity(GameScript game,Collider slot,BindingConfig bindings,int width,int height) {
  Screen.width=width;Screen.height=height;game.Hold(7,9999);float left=width-30;slot.bounds=new Bounds{min=new Vector3(width-60,100,0),max=new Vector3(width,160,0)};
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true,Badge(left,width));Draw();
  LabelDraw number=GUI.Draws[GUI.Draws.Count-1];
  Check(number.rect.xMax<=width-1f&&number.rect.xMax>=width-2f,"right-edge quantity remains inside its selected cell at "+width);
  Check(number.glyphX>=0&&number.rect.xMax<=width,"right-edge quantity stays on screen at "+width);
 }
 static void Main() {
  BindingConfig bindings=BindingConfig.Parse(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button9").Split('\n'));
  GameScript game=new GameScript(); GameScript.inventoryOpen=true;
  Collider slot=new Collider {bounds = new Bounds {min=new Vector3(10,10,0),max=new Vector3(30,30,0)}};
  InventoryFeedback.SetContext(game,slot,true,bindings,"Pick up",null,false,false); Draw();
  Check(GUI.Labels.Count==1 && GUI.Labels[0]=="L3 Pick up   ·   B Close","mapped primary and no invalid actions");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Forge emblems",null,false,false,null,"Cancel forging"); Draw();
  Check(GUI.Labels[0].Contains("B Cancel forging")&&!GUI.Labels[0].Contains("B Close"),"native forge mode uses shared configured cancel hint");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Pick up",null,false,false); Draw();
  Check(GUI.Labels[0].Contains("B Close")&&!GUI.Labels[0].Contains("Cancel forging"),"leaving mode restores default cancel hint");
  Check(GUI.Boxes[0].width<Screen.width/2,"short hints are compact rather than full width");CardsBoundedAndCentered();
  game.Hold(7,23);
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true,Badge(10,30));Draw();
  Check(GUI.Labels[0].Contains("X Place one") && GUI.Labels[0].Contains("RB Drop") && GUI.Labels[0].Contains("B Return"),"held contextual hints");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true,Badge(10,30),storagePaging:true);Draw();
  Check(GUI.Labels[0].Contains("LB Previous page")&&GUI.Labels[0].Contains("RB Next page")&&!GUI.Labels[0].Contains("RB Drop"),
      "storage bottom hint card advertises bumpers and hides conflicting drop");
  Check(GUI.Labels[0].Contains("B Return")&&GUI.Labels[0].Contains("L3 Place"),"paging hints preserve other configured actions");
  BindingConfig remappedDrop=BindingConfig.Parse(BindingConfig.DefaultText.Replace("MenuDrop = Joystick1Button5","MenuDrop = Joystick1Button12").Split('\n'));
  InventoryFeedback.SetContext(game,slot,true,remappedDrop,"Place","Place one",false,true,Badge(10,30),storagePaging:true);Draw();
  Check(GUI.Labels[0].Contains("Button 12 Drop")&&GUI.Labels[0].Contains("RB Next page"),"non-bumper remapped drop retains accurate storage hint");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true,Badge(10,30));Draw();
  Check(GUI.Labels[0].Contains("RB Drop")&&!GUI.Labels[0].Contains("Next page"),"leaving storage restores ordinary drop hint");
  Check(GUI.Labels.Contains("23"),"held quantity");
  Rect smallBadgeQuantity=GUI.Draws[GUI.Draws.Count-1].rect;Check(smallBadgeQuantity.x>=10&&smallBadgeQuantity.xMax<=30,"quantity remains in selected cell even when badge fills it");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true,Badge(10,70));Draw();
  Check(Near(GUI.Draws[GUI.Draws.Count-1].rect.x,smallBadgeQuantity.x),"larger badge cannot push counter into next cell");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true,Badge(1250,1280));Draw();
  Rect edgeQuantity=GUI.Draws[GUI.Draws.Count-1].rect;Check(edgeQuantity.x>=10&&edgeQuantity.xMax<=30,"offscreen badge cannot move counter outside selected cell");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",false,true);Draw();
  Check(!GUI.Labels.Contains("23"),"no phantom quantity when badge art unavailable");
  InventoryFeedback.SetContext(game,slot,false,bindings,"Place","Place one",false,true,Badge(10,30)); Draw();
  Check(GUI.Labels.Count==0,"mouse hides controller hints and quantity");
  InventoryFeedback.Reject("Wrong item type"); Draw(); Check(GUI.Labels.Count==1 && GUI.Labels[0]=="Wrong item type","mouse rejection stays visible");CardsBoundedAndCentered();
  ControllerInputOwnership.Configure(() => false); Draw();
  Check(GUI.Labels.Count==0 && GUI.Boxes.Count==0,"same-frame ownership handoff clears pending rejection and quantity");
  InventoryFeedback.SetContext(game,slot,true,bindings,"Place","Place one",true,true,Badge(10,30));
  InventoryFeedback.Reject("Must stay hidden"); Draw();
  Check(GUI.Labels.Count==0 && GUI.Boxes.Count==0,"false shared ownership overrides controller context and rejection");
  ControllerInputOwnership.Configure(() => true); Draw();
  Check(GUI.Labels.Count==0,"reclaim never resurrects pending rejection");
  Time.realtimeSinceStartup=3; Draw(); Check(GUI.Labels.Count==0,"rejection expires");
  SnugQuantity(game,slot,bindings,1920,1200,100,145);
  SnugQuantity(game,slot,bindings,360,1200,100,130);
  EdgeQuantity(game,slot,bindings,1920,1200);
  EdgeQuantity(game,slot,bindings,360,1200);
  Check(GUI.skin.label.padding.left==4&&GUI.skin.label.padding.right==4,"quantity normalization does not mutate inherited GUI skin padding");
  Screen.width=1920;Screen.height=1200;
  game.Hold(0,0);InventoryFeedback.SetContext(game,slot,true,bindings,"Pick up","Split",true,false);
  Color tint=new Color(.2f,.3f,.4f,.5f),background=new Color(.6f,.7f,.8f,.9f);GUI.color=tint;GUI.backgroundColor=background;
  Draw();CardsBoundedAndCentered();Check(GUI.Draws[0].font==24,"1920x1200 uses limiting width scale");
  Check(GUI.color.Equals(tint)&&GUI.backgroundColor.Equals(background),"cards preserve inherited GUI tints");Check(GUI.Draws[0].tint.Equals(Color.white),"hint text keeps full contrast despite inherited tint");
  InventoryFeedback.Reject("Storage full");Draw();CardsBoundedAndCentered();Check(GUI.Boxes.Count==2,"error has separate card");Check(GUI.Boxes[1].yMax<GUI.Boxes[0].y,"error card above hints without overlap");Check(GUI.Boxes[1].width<GUI.Boxes[0].width,"error card independently sized");
  Screen.width=360;Screen.height=1200;
  BindingConfig longLabels=BindingConfig.Parse(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button19").Replace("MenuSecondary = Joystick1Button2","MenuSecondary = Joystick1Button18").Replace("MenuTransfer = Joystick1Button3","MenuTransfer = Joystick1Button17").Replace("MenuCancel = Joystick1Button1","MenuCancel = Joystick1Button16").Split('\n'));
  InventoryFeedback.SetContext(game,slot,true,longLabels,"Pick up","Split",true,false);Draw();CardsBoundedAndCentered();
  Check(GUI.Labels[0].Contains("Button 19 Pick up")&&GUI.Labels[0].Contains("Button 16 Close"),"long remapped labels unchanged");Check(GUI.Draws[0].font==12,"narrow tall window scales using width");Check(GUI.Boxes[0].height>30,"long labels wrap to multiple lines");Check(GUI.Boxes[1].yMax<GUI.Boxes[0].y,"wrapped hints and error remain separate");
  GameScript.inventoryOpen=false;InventoryFeedback.Reject("Storage full");Draw();Check(GUI.Labels.Count==0,"closed inventory hides feedback");
  InventoryFeedback.Reset();Console.WriteLine("PASS "+checks+" contextual feedback, compact cards, wrapping, scale, contrast, badge geometry, handoff, lifecycle checks");
 }
}
