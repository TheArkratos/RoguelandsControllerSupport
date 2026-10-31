using System;
using System.Collections.Generic;
using System.Reflection;
using ControllerSupport;
using GadgetCore.API;
using UnityEngine;
internal static class Program
{
    static int checks;static GameScript game;static JoystickInputState pad;static bool owned;
    static Collider[] upgrades,outputs;static Collider backpack;
    static InventorySelectionNavigator Nav => (InventorySelectionNavigator)typeof(ControllerMenuSelection).GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static Collider Add(GameObject root,string name,string tag,float x,float y)
    {var obj=new GameObject(name){tag=tag,layer=16};root.transform.Add(obj.transform);return new Collider(obj){bounds=new Bounds{center=new Vector3(x,y,1),size=new Vector3(2.2f,2.2f,1)}};}
    static void Frame(){Time.frameCount++;Time.unscaledTime+=.05f;ControllerMenuSelection.Prepare(game);}
    static void Next(){ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();Frame();}
    static void Button(int n,bool held=true){ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=(byte)n,Value=(short)(held?1:0)});Frame();}
    static void Hover(Collider target){Physics.Hit=target;Input.mousePosition=new Vector3(Input.mousePosition.x+3,0);Next();Next();}
    static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    static bool Right => ControllerMenuSelection.VirtualMouseButtonDown(1);
    static void Setup(string config=null)
    {
        ControllerMenuSelection.Reset();owned=true;ControllerInputOwnership.Configure(()=>owned);
        game=new GameScript{inventoryMain=new GameObject("inventory"),inventoryBar=new GameObject("bar"),menuStorage=new GameObject("storage"),menuGather=new GameObject("gather"),holdingItemObj=new GameObject("holding")};game.menuStorage.SetActive(false);
        for(int i=0;i<45;i++)game.inventory[i]=new Item();for(int i=0;i<9;i++)game.gatherStorage[i]=new Item();GameScript.shipDroid=new int[6];
        for(int i=6;i<36;i++){var c=Add(game.inventoryMain,i.ToString(),"Untagged",-12+((i-6)%6)*2,4-((i-6)/6)*2);if(i==6)backpack=c;}
        upgrades=new Collider[6];outputs=new Collider[9];for(int i=0;i<6;i++)upgrades[i]=Add(game.menuGather,i.ToString(),"sd",4.875f+(i%3)*3.125f,i<3?4.88f:2f);
        for(int i=0;i<9;i++)outputs[i]=Add(game.menuGather,i.ToString(),"gather",5.85f+(i%3)*2.15f,-3.25f-(i/3)*2.125f);
        GameScript.inventoryOpen=true;GameScript.pausing=GameScript.dead=GameScript.buildMode=false;GadgetCoreAPI.Frozen=false;Input.left=Input.right=false;Input.mousePosition=Vector3.zero;Physics.Hit=backpack;
        pad=new JoystickInputState();pad.Connect();ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),message=>throw new Exception(message),()=>owned);Frame();Next();
    }
    static void ContextAndNavigation()
    {
        Setup();Check(Nav.SelectedSlot==190,"Gather entry focuses first droid rather than stationary backpack mouse");
        var slots=new Collider[205];ShipDroidMenuContext.CollectTargets(game,slots);
        for(int i=0;i<6;i++)Check(slots[190+i]==upgrades[i],"upgrade tags map duplicate numeric names "+i);
        for(int i=0;i<9;i++)Check(slots[196+i]==outputs[i],"gather tags map duplicate numeric names "+i);
        var field=typeof(InventorySelectionNavigator).GetField("<SelectedSlot>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
        var move=typeof(InventorySelectionNavigator).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic);
        var reached=new HashSet<int>();var todo=new Queue<int>();todo.Enqueue(190);reached.Add(190);
        while(todo.Count>0){int from=todo.Dequeue();foreach(int d in new[]{-1,1,-2,2}){field.SetValue(Nav,from);move.Invoke(Nav,new object[]{d});if(reached.Add(Nav.SelectedSlot))todo.Enqueue(Nav.SelectedSlot);}}
        for(int i=190;i<205;i++)Check(reached.Contains(i),"actual-layout shared navigation reaches target "+i);
        Check(reached.Contains(6),"ship panel connects to player inventory");
        field.SetValue(Nav,190);move.Invoke(Nav,new object[]{1});Check(Nav.SelectedSlot==191,"droid row Right");move.Invoke(Nav,new object[]{2});Check(Nav.SelectedSlot==194,"droid grid Down");
        outputs[4].enabled=false;ShipDroidMenuContext.CollectTargets(game,slots);Check(slots[200]==null,"disabled output excluded");
        outputs[4].enabled=true;outputs[4].gameObject.tag="Untagged";ShipDroidMenuContext.CollectTargets(game,slots);Check(slots[200]==null,"non-gather tag cannot hijack duplicate slot name");
        outputs[4].gameObject.tag="gather";outputs[4].gameObject.layer=1;ShipDroidMenuContext.CollectTargets(game,slots);Check(slots[200]==null,"wrong-layer output excluded");
        outputs[4].gameObject.layer=16;outputs[4].gameObject.SetActive(false);ShipDroidMenuContext.CollectTargets(game,slots);Check(slots[200]==null,"hidden output excluded");
        game.menuGather.SetActive(false);Check(!ShipDroidMenuContext.IsOpen(game),"closed panel context unavailable");
    }
    static void Upgrades()
    {
        Setup();Hover(upgrades[0]);int credits=game.Credits;Button(0);Check(Left&&!Right,"A supplies one native upgrade click");Check(game.Credits==credits&&GameScript.shipDroid[0]==0,"controller adapter never spends Credits or upgrades itself");
        game.ReplayUpgrade(0);Check(GameScript.shipDroid[0]==1&&game.Credits==credits-100,"native replay upgrades and spends native cost");Next();Check(!Left,"held A does not repeat purchases");
        Button(0,false);GameScript.shipDroid[0]=5;Button(0);Check(!Left&&InventoryFeedback.LastReason.Contains("maximum"),"max level rejects without click");
        Button(0,false);GameScript.shipDroid[0]=4;game.Credits=9998;Button(0);Check(!Left&&InventoryFeedback.LastReason.Contains("Credits"),"insufficient Credits reject without mutation");
        Button(0,false);game.Credits=9999;Button(0);Check(Left,"exact native cost accepts upgrade");
        Button(0,false);Button(2);Check(!Left&&!Right&&InventoryFeedback.LastReason.Contains("primary"),"X cannot act as inventory secondary on upgrade");Button(2,false);Button(3);Check(!Left&&!Right&&InventoryFeedback.LastReason.Contains("primary"),"Y cannot transfer upgrade as item");
        Button(3,false);game.holdingItem=new Item{id=101,q=7};Button(0);Check(Left&&game.holdingItem.q==7,"native upgrade remains allowed with held item without mutating it");
        Button(0,false);GadgetCoreAPI.Frozen=true;Button(0);Check(!Left,"frozen input blocks upgrade");GadgetCoreAPI.Frozen=false;
    }
    static void GatherActions()
    {
        Setup();Hover(outputs[2]);game.gatherStorage[2]=new Item{id=101,q=17,extraData="gather"};Button(0);Check(Left&&!Right,"A projects native whole-stack pickup");
        game.ReplayTake(2);ControllerMenuSelection.EndNativeUpdate();ControllerMenuSelection.Update(game);Check(game.holdingItem.q==17&&game.holdingItem.extraData=="gather","native pickup preserves exact stack metadata");
        Button(0,false);Button(2);Check(!Right&&InventoryFeedback.LastReason.Contains("held item"),"held X cannot deposit/place-one into output");Button(2,false);Button(0);Check(!Left,"held A cannot deposit into output");Button(0,false);Button(3);Check(game.GatherTransferCalls==0,"held Y cannot transfer source while carrying");
        Button(3,false);Button(1);Check(InventoryItemLifecycle.ReturnCalls==1,"B reuses shared held-source return");Button(1,false);
        game.gatherStorage[2]=new Item{id=101,q=9};Button(2);Check(Right&&!Left,"X matches native whole-stack take, not split");Check(game.gatherStorage[2].q==9,"adapter does not pre-split");
        Button(2,false);Button(3);Check(game.GatherTransferCalls==1&&game.LastNativeSlot==2,"Y invokes native ShiftClickGather with output index");Check(game.inventory[0].q==9&&game.gatherStorage[2].id==0,"native transfer handles output to inventory");
        Button(3,false);Button(0);Check(!Left&&InventoryFeedback.LastReason.Contains("No gathered"),"empty output cannot take");
        game.gatherStorage[2]=new Item{id=101,q=20,extraData="a"};for(int i=0;i<36;i++)game.inventory[i]=new Item{id=102,q=9999};
        game.inventory[4]=new Item{id=101,q=9998,extraData="a"};string reason;Check(ShipDroidMenuContext.CanActivate(game,198,2,out reason),"partial matching stack room permits loader transfer without empty inventory cell");
        game.inventory[4].extraData="b";Check(!ShipDroidMenuContext.CanActivate(game,198,2,out reason)&&reason.Contains("full"),"incompatible metadata is not capacity");
        game.inventory[4]=new Item();Check(ShipDroidMenuContext.CanActivate(game,198,2,out reason),"empty slot permits exact metadata-preserving transfer");
        game.holdingItem=new Item();Hover(outputs[2]);ControllerMenuSelection.Update(game);Check(InventoryFeedback.Primary=="Take"&&InventoryFeedback.Secondary=="Take"&&InventoryFeedback.Transfer,"gather hints match native take/take/transfer semantics");
        Hover(upgrades[2]);ControllerMenuSelection.Update(game);Check(InventoryFeedback.Primary=="Upgrade"&&InventoryFeedback.Secondary==null&&!InventoryFeedback.Transfer,"upgrade hints never offer split/transfer");
    }
    static void OwnershipAndConfig()
    {
        Setup();Hover(upgrades[0]);owned=false;Input.left=true;Button(0);Check(Left&&ControllerMenuSelection.VirtualMousePosition().x==Input.mousePosition.x,"physical mouse exact on handoff");Input.left=false;owned=true;Button(0,false);Button(0);Check(Left,"controller can reacquire after mouse handoff");
        Setup(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12"));Hover(upgrades[0]);Button(0);Check(!Left,"default A disabled after remap");Button(0,false);Button(12);Check(Left,"configured replacement button activates upgrade");
        Setup();Hover(outputs[0]);Button(1);Check(!GameScript.inventoryOpen,"empty-handed B uses shared CloseAllMenus");
        Setup();GadgetCoreAPI.Frozen=true;Hover(outputs[1]);game.gatherStorage[1]=new Item{id=101,q=3};Button(0);Check(!Left,"frozen gather pickup rejected");GadgetCoreAPI.Frozen=false;
        Setup();game.gatherStorage[0]=new Item{id=101,q=3};Hover(outputs[0]);InventoryItemLifecycle.BlockMutations=true;Button(0);Check(!Left,"pending drop quarantine blocks gathered pickup");InventoryItemLifecycle.BlockMutations=false;
        Setup();Button(0);Next();GameScript.dead=true;Button(0,false);Button(0);Check(!Left,"dead player cannot activate visible droid panel");GameScript.dead=false;
        Setup();game.menuGather.SetActive(false);Frame();Button(0);game.menuGather.SetActive(true);ControllerMenuSelection.Prepare(game);
        Check(Nav.SelectedSlot==190&&!Left,"same-frame Gather entry resets context and cannot spend a held activation");Next();Check(!Left,"held entry activation remains disarmed");Button(0,false);Physics.Hit=upgrades[0];Button(0);Check(Left,"fresh activation works after entry release");
        Setup();game.menuGather.SetActive(false);Frame();pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=short.MaxValue});game.menuGather.SetActive(true);Next();Check(Nav.SelectedSlot==190,"entry navigation waits for held stick release");Next();Check(Nav.SelectedSlot==190,"held entry stick cannot repeatedly move focus");pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=0});Next();pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=short.MaxValue});Next();Check(Nav.SelectedSlot==191,"released/repressed stick navigates ship menu");
        Setup();Hover(outputs[0]);game.gatherStorage[0]=new Item{id=101,q=2};Physics.Hit=backpack;Button(0);Check(!Left&&game.gatherStorage[0].q==2,"occluded target ray cannot create pickup");
        Setup();game.menuGather.SetActive(false);Frame();Check(!ShipDroidMenuContext.IsOpen(game)&&!Left,"same-frame close invalidates panel action");
    }
    static void Main(){ContextAndNavigation();Upgrades();GatherActions();OwnershipAndConfig();Console.WriteLine("Ship droid inventory: "+checks+" checks passed.");}
}
