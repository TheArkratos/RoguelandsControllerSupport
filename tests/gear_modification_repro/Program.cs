using System;
using System.Collections.Generic;
using System.Reflection;
using ControllerSupport;
using GadgetCore.API;
using UnityEngine;
internal static class Program
{
    static int checks; static GameScript game;static JoystickInputState pad;static bool owned;
    static Collider slot,backpack;static Collider[] buttons;
    static InventorySelectionNavigator Nav => (InventorySelectionNavigator)typeof(ControllerMenuSelection).GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static Item Gear(int id=500) => new Item{id=id,q=1,aspect=new int[3],aspectLvl=new int[3],extraData="metadata"};
    static Collider Add(GameObject root,string name,string tag,float x,float y)
    {var obj=new GameObject(name){tag=tag,layer=16};root.transform.Add(obj.transform);return new Collider(obj){bounds=new Bounds{center=new Vector3(x,y,1),size=new Vector3(2,2,1)}};}
    static void Frame(){Time.frameCount++;Time.unscaledTime+=.05f;ControllerMenuSelection.Prepare(game);}
    static void Next(){ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();Frame();}
    static void Button(int n,bool held=true){ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=(byte)n,Value=(short)(held?1:0)});Frame();}
    static void Hover(Collider c){Physics.Hit=c;Input.mousePosition=new Vector3(Input.mousePosition.x+3,0);Next();Next();}
    static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    static bool Right => ControllerMenuSelection.VirtualMouseButtonDown(1);
    static void Setup()
    {
        ControllerMenuSelection.Reset();owned=true;ControllerInputOwnership.Configure(()=>owned);InventoryItemLifecycle.Reset();
        game=new GameScript{inventoryMain=new GameObject("inventory"),inventoryBar=new GameObject("bar"),menuStorage=new GameObject("storage"),menuGearMods=new GameObject("menuMods"),holdingItemObj=new GameObject("holding")};game.menuStorage.SetActive(false);
        for(int i=0;i<45;i++)game.inventory[i]=new Item();
        for(int i=6;i<36;i++){var c=Add(game.inventoryMain,i.ToString(),"Untagged",-12+((i-6)%6)*2,4-((i-6)/6)*2);if(i==6)backpack=c;}
        slot=Add(game.menuGearMods,"0","mod",2.51f,2.6f);buttons=new Collider[3];for(int i=0;i<3;i++){ buttons[i]=Add(game.menuGearMods,"bAddMod"+i,"Untagged",9.496f,4.735f-i*2.125f);buttons[i].gameObject.layer=0;buttons[i].gameObject.extraComponent=new ButtonMenu(); }
        GameScript.inventoryOpen=true;GameScript.pausing=GameScript.dead=GameScript.buildMode=false;GadgetCoreAPI.Frozen=false;Input.left=Input.right=false;Input.mousePosition=Vector3.zero;Physics.Hit=backpack;
        pad=new JoystickInputState();pad.Connect();ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse(BindingConfig.DefaultText.Split('\n')),m=>throw new Exception(m),()=>owned);Frame();Next();
    }
    static void Main()
    {
        Setup();Check(Nav.SelectedSlot==205,"station entry focuses gear slot");
        var targets=new Collider[209];GearModificationMenuContext.CollectTargets(game,targets);Check(targets[205]==slot,"qualified mod slot discovered");
        for(int i=0;i<3;i++)Check(targets[206+i]==buttons[i],"aspect button discovered "+i);
        var selected=typeof(InventorySelectionNavigator).GetField("<SelectedSlot>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);var move=typeof(InventorySelectionNavigator).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic);
        var reached=new HashSet<int>{205};var todo=new Queue<int>();todo.Enqueue(205);
        while(todo.Count>0){int from=todo.Dequeue();foreach(int d in new[]{-1,1,-2,2}){selected.SetValue(Nav,from);move.Invoke(Nav,new object[]{d});if(reached.Add(Nav.SelectedSlot))todo.Enqueue(Nav.SelectedSlot);}}
        foreach(int id in new[]{6,205,206,207,208})Check(reached.Contains(id),"shared navigation reaches "+id);
        Setup();game.modSlot[0]=Gear();Hover(slot);Button(0);Check(Left&&!Right,"A routes native primary take");if(Left)game.SelectItemMod(0);Check(game.holdingItem.extraData=="metadata"&&game.modSlot[0].id==0,"whole native gear metadata retained");
        Button(0,false);Hover(slot);Button(2);Check(Left&&!Right,"X uses whole native primary placement");if(Left)game.PlaceItemMod(0);Check(game.modSlot[0].id==500&&game.holdingItem.id==0,"whole gear placement replay");
        Button(2,false);game.modSlot[0]=Gear(10001);Hover(slot);Button(2);Check(!Left&&!Right&&game.SelectModCalls==2&&game.holdingItem.id==10001,"X custom modable gear uses loader native method despite no physical click");
        Setup();game.holdingItem=new Item{id=11,q=2};Hover(slot);Button(0);Check(!Left&&InventoryFeedback.LastReason!=null,"nonmodifiable held item rejected");
        Setup();game.modSlot[0]=Gear();game.holdingItem=new Item{id=201,q=3};Hover(buttons[0]);Button(0);Check(Left,"eligible mod routes native primary click");if(Left)game.AddMod(0);Check(game.modSlot[0].aspect[0]==201&&game.modSlot[0].aspectLvl[0]==1&&game.holdingItem.q==2,"native mod consumes one and increments aspect");
        Next();Check(!Left,"held A cannot repeat modification");Button(0,false);Hover(buttons[1]);Button(0);Check(!Left&&InventoryFeedback.LastReason.Contains("another"),"duplicate modification across aspects rejected");
        Setup();game.modSlot[0]=Gear();game.modSlot[0].aspect[0]=201;game.modSlot[0].aspectLvl[0]=5;game.holdingItem=new Item{id=201,q=3};Hover(buttons[0]);Button(0);Check(!Left&&game.holdingItem.q==3,"max aspect does not consume item");
        Setup();game.modSlot[0]=Gear();game.modSlot[0].aspect[0]=202;game.modSlot[0].aspectLvl[0]=1;game.holdingItem=new Item{id=201,q=3};Hover(buttons[0]);Button(0);Check(!Left,"different existing aspect cannot be overwritten");
        Setup();game.modSlot[0]=Gear();game.holdingItem=new Item{id=201,q=3};Hover(buttons[0]);Button(2);Check(!Left&&!Right,"secondary does not apply modification");Button(2,false);Button(3);Check(!Left&&!Right,"transfer does not apply modification");
        Setup();game.modSlot[0]=Gear();Button(1);Check(game.CloseCalls==0&&InventoryFeedback.LastReason.Contains("Take the gear"),"close refuses native uncaptured slotted-gear drop");
        Setup();Button(1);Check(game.CloseCalls==1,"empty station closes normally");
        Setup();game.modSlot[0]=Gear();Hover(slot);GadgetCoreAPI.Frozen=true;Button(0);Check(!Left,"frozen input cannot mutate gear");
        Setup();game.modSlot[0]=Gear();Hover(slot);InventoryItemLifecycle.BlockMutations=true;Button(0);Check(!Left,"quarantine prevents modification actions");
        Setup();game.modSlot[0]=Gear();Hover(slot);owned=false;Button(0);Check(!Left&&!Right,"mouse ownership prevents gear controller click");
        Setup();game.holdingItem=Gear(10001);Hover(slot);Button(0);Check(game.PlaceModCalls==1&&game.modSlot[0].id==10001&&game.holdingItem.id==0&&!Left,"custom gear native placement works without physical loader postfix");
        Setup();game.modSlot[0]=Gear(500);game.holdingItem=Gear(10001);Hover(slot);Button(0);Check(game.SwapModCalls==1&&game.modSlot[0].id==10001&&game.holdingItem.id==500,"custom gear native swap preserves both items");
        Setup();game.modSlot[0]=Gear();Button(6);Check(game.CloseCalls==0,"View cannot close with slotted gear");
        Setup();game.modSlot[0]=Gear();Button(7);Check(game.CloseCalls==0&&game.PauseCalls==0,"Menu cannot pause via uncaptured gear drop");
        Setup();buttons[1].enabled=false;GearModificationMenuContext.CollectTargets(game,targets);Check(targets[207]==null,"disabled control excluded");game.menuGearMods.SetActive(false);Next();Check(!GearModificationMenuContext.IsOpen(game)&&Nav.SelectedSlot<205,"closed root releases stale gear selection");
        ControllerMenuSelection.Reset();ControllerInputOwnership.Reset();Console.WriteLine("Gear Modification: "+checks+" checks passed.");
    }
}
