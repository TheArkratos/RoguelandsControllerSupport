using System;
using System.Collections.Generic;
using System.Reflection;
using ControllerSupport;
using GadgetCore.API;
using UnityEngine;
internal static class Program
{
    static int checks; static GameScript game;static JoystickInputState pad;static bool owned;
    static Collider slot,backpack,shuffle;static Collider[] buttons;
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
        game=new GameScript{inventoryMain=new GameObject("inventory"),inventoryBar=new GameObject("bar"),menuStorage=new GameObject("storage"),holdingItemObj=new GameObject("holding")};game.menuStorage.SetActive(false);
        for(int i=0;i<45;i++)game.inventory[i]=new Item();
        for(int i=6;i<36;i++){var c=Add(game.inventoryMain,i.ToString(),"Untagged",-12+((i-6)%6)*2,4-((i-6)/6)*2);if(i==6)backpack=c;}
        game.menuQuest=new GameObject("menuQuest");
        buttons=new Collider[3];for(int i=0;i<3;i++){buttons[i]=Add(game.menuQuest,i.ToString(),"quest",13.766f,5.35f-i*3.232f);game.inventoryQuest[i]=new Item();GameScript.quest[i]=new Quest();}
        slot=buttons[0];shuffle=Add(game.menuQuest,"bShuffle","Untagged",7.75f,-5.66f);shuffle.gameObject.layer=0;shuffle.gameObject.extraComponent=new ButtonMenu();
        GameScript.inventoryOpen=true;GameScript.pausing=GameScript.dead=GameScript.buildMode=false;GadgetCoreAPI.Frozen=false;Input.left=Input.right=false;Input.mousePosition=Vector3.zero;Physics.Hit=backpack;
        pad=new JoystickInputState();pad.Connect();ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse(BindingConfig.DefaultText.Split('\n')),m=>throw new Exception(m),()=>owned);Frame();Next();
    }
    static void Complete(int i=0,int type=1){GameScript.quest[i].type=type;GameScript.quest[i].numberOf2=5;game.inventoryQuest[i]=new Item{id=59,q=2,extraData="quest reward"};}
    static void Replay(){if(Left){if(Nav.SelectedSlot==212)game.Shuffle();else if(Nav.SelectedSlot>=209)game.QuestClick(Nav.SelectedSlot-209);}ControllerMenuSelection.EndNativeUpdate();}
    static void Main()
    {
        Setup();Check(Nav.SelectedSlot==209,"Questbot entry focuses first native quest");
        var targets=new Collider[213];QuestMenuContext.CollectTargets(game,targets);for(int i=0;i<3;i++)Check(targets[209+i]==buttons[i],"qualified quest card "+i);Check(targets[212]==shuffle,"layer0 shuffle control discovered");
        var selected=typeof(InventorySelectionNavigator).GetField("<SelectedSlot>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);var move=typeof(InventorySelectionNavigator).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic);
        var seen=new HashSet<int>{209};var todo=new Queue<int>();todo.Enqueue(209);while(todo.Count>0){int from=todo.Dequeue();foreach(int d in new[]{-1,1,-2,2}){selected.SetValue(Nav,from);move.Invoke(Nav,new object[]{d});if(seen.Add(Nav.SelectedSlot))todo.Enqueue(Nav.SelectedSlot);}}
        foreach(int id in new[]{6,209,210,211,212})Check(seen.Contains(id),"shared navigation reaches "+id);
        Setup();Hover(slot);Button(0);Check(!Left&&game.QuestClaims==0,"incomplete quest cannot claim");
        Setup();game.holdingItem=new Item{id=10,q=2,extraData="delivery remainder"};Hover(slot);Button(0);Check(Left,"matching partial delivery uses native primary");Replay();Check(GameScript.quest[0].numberOf2==2&&game.holdingItem.id==0&&game.DeliveryCalls==1,"partial delivery consumes exact quantity");Next();Check(!Left,"held A cannot repeat delivery");
        Button(0,false);game.holdingItem=new Item{id=10,q=8,extraData="remainder"};Button(0);Replay();Check(GameScript.quest[0].numberOf2==5&&game.holdingItem.q==5&&game.holdingItem.extraData=="remainder","full delivery consumes only missing amount and preserves remainder metadata");
        Button(0,false);Button(0);Check(!Left&&game.QuestClaims==0,"completed delivery with held matching remainder must be placed before claim");
        game.holdingItem=new Item();Button(0,false);Button(0);Check(Left,"complete quest claims empty-handed");Replay();Check(game.QuestClaims==1&&game.QuestXp==1&&game.holdingItem.q==2&&game.holdingItem.extraData=="quest reward","one native claim awards XP/completion and exact reward object");Next();Check(!Left&&game.QuestClaims==1,"held claim cannot complete replacement quest");
        Button(0,false);Button(0);Check(!Left&&game.QuestClaims==1,"fresh claim cannot reclaim while reward held/new quest incomplete");
        Setup();Complete(1,2);Hover(buttons[1]);Button(0);Replay();Check(game.QuestClaims==1&&game.QuestXp==1&&GameScript.quest[1].numberOf2==0,"kill completion uses same native claim and regenerates quest");
        Setup();Complete();game.holdingItem=new Item{id=99,q=1};Hover(slot);Button(0);Check(!Left&&game.QuestClaims==0,"wrong held item cannot claim or deliver");
        Setup();Complete();Hover(slot);Button(2);Check(!Left&&!Right,"no invented secondary quest action");Button(2,false);Button(3);Check(!Left&&!Right,"no invented quick transfer");
        Setup();game.Credits=1499;Hover(shuffle);Button(0);Check(!Left&&game.Credits==1499,"insufficient shuffle credits reject");
        Setup();game.Credits=4000;game.holdingItem=new Item{id=59,q=2,extraData="claimed"};Hover(shuffle);Button(0);Replay();Check(game.Credits==2500&&game.ShuffleCalls==1&&game.holdingItem.extraData=="claimed","native shuffle charges1500 once and preserves held reward");Next();Check(!Left&&game.ShuffleCalls==1,"held A cannot repeatedly shuffle");
        Setup();Complete();Hover(slot);GadgetCoreAPI.Frozen=true;Button(0);Check(!Left,"modal blocks quest mutation");
        Setup();Complete();Hover(slot);InventoryItemLifecycle.BlockMutations=true;Button(0);Check(!Left,"uncertain drop blocks claims");
        Setup();Complete();Hover(slot);owned=false;Button(0);Check(!Left,"mouse handoff blocks controller claim");
        Setup();Complete();buttons[0].enabled=false;QuestMenuContext.CollectTargets(game,targets);Check(targets[209]==null,"disabled quest target excluded");game.menuQuest.SetActive(false);Next();Check(!QuestMenuContext.Root(game)&&Nav.SelectedSlot<209,"closed Questbot clears stale target");
        Setup();Complete();Button(0);Check(!Left,"invalid physical ray cannot issue a synthetic quest claim");
        Setup();game.menuQuest.SetActive(false);Frame();Button(0);game.menuQuest.SetActive(true);Complete();Physics.Hit=slot;Frame();Check(!Left,"held A opening Questbot cannot claim");Next();Check(!Left,"held opening A stays disarmed");Button(0,false);Button(0);Check(Left,"release and fresh A can claim after entry");
        Setup();Complete();ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12").Split('\n')),m=>throw new Exception(m),()=>owned);Frame();Next();Hover(slot);Button(0);Check(!Left,"old A does not activate after remap");Button(0,false);Button(12);Check(Left,"remapped activation retains native claim path");
        Setup();game.holdingItem=new Item{id=10,q=2};Hover(slot);InventoryItemLifecycle.BlockMutations=true;owned=false;Input.left=true;Physics.Hit=slot;Frame();
        if(Left)game.QuestClick(0);Check(!Left&&GameScript.quest[0].numberOf2==0&&game.holdingItem.q==2,"pending drop plus mouse handoff blocks inline physical quest delivery");
        Physics.Hit=shuffle;game.Credits=5000;Frame();if(Left)game.Shuffle();Check(!Left&&game.Credits==5000&&game.ShuffleCalls==0,"quarantine blocks physical shuffle without charge");
        Physics.Hit=slot;Complete();game.holdingItem=new Item();Frame();if(Left)game.QuestClick(0);Check(!Left&&game.QuestClaims==0,"quarantine blocks physical reward claim");
        Physics.Hit=backpack;Frame();Check(Left,"quest inline guard does not replace unrelated native mouse input");
        Physics.Hit=slot;InventoryItemLifecycle.BlockMutations=false;Frame();Check(Left,"normal native quest mouse click resumes when quarantine absent");
        if(Left)game.QuestClick(0);Check(game.QuestClaims==1&&game.QuestXp==1,"normal mouse retains native reward completion");
        InventoryItemLifecycle.BlockMutations=true;game.menuQuest.SetActive(false);Frame();Check(Left,"closed quest root cannot impose stale physical guard");
        ControllerMenuSelection.Reset();ControllerInputOwnership.Reset();Console.WriteLine("Questbot menus: "+checks+" checks passed.");
    }
}
