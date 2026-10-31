using System;
using System.Reflection;
using System.Collections.Generic;
using ControllerSupport;
using UnityEngine;
using GadgetCore.API;
internal static class Program
{
    static int checks; static GameScript game; static JoystickInputState pad; static bool active;
    static Collider[] cells;
    static InventorySelectionNavigator Nav => (InventorySelectionNavigator)typeof(ControllerMenuSelection).GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
    static Collider Add(GameObject root,int i,float x,float y,string tag="combat")
    {var o=new GameObject(i.ToString()){layer=16,tag=tag};root.transform.Add(o.transform);return new Collider(o){bounds=new Bounds{center=new Vector3(x,y,1),size=new Vector3(2,2,1)}};}
    static void Frame(){Time.frameCount++;Time.unscaledTime+=.05f;ControllerMenuSelection.Prepare(game);}
    static void Next(){ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();Frame();}
    static void Button(int n,bool held=true){ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=(byte)n,Value=(short)(held?1:0)});Frame();}
    static void Trigger(){ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=2,Number=5,Value=short.MaxValue});Frame();}
    static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    static void Hover(int i){Physics.Hit=cells[i];Input.mousePosition=new Vector3(Input.mousePosition.x+3,0);Next();Next();}
    static void Setup(string config=null)
    {
        ControllerMenuSelection.Reset();active=true;ControllerInputOwnership.Configure(()=>active);
        game=new GameScript{inventoryMain=new GameObject("inventory"),inventoryBar=new GameObject("bar"),menuStorage=new GameObject("storage"),menuCraft=new GameObject("craft"),menuRecipe=new GameObject("recipe"),menuCombat=new GameObject("combat"),skillBar=new GameObject("skill"),holdingItemObj=new GameObject("holding")};
        game.inventoryMain.SetActive(false);game.menuStorage.SetActive(false);game.menuCraft.SetActive(false);game.menuRecipe.SetActive(false);
        cells=new Collider[38];for(int i=0;i<38;i++)cells[i]=Add(i<6?game.inventoryBar:game.menuCombat,i,i<6?i*3:(i-6)%8*3,i<6?-3:(i-6)/8*3);
        Add(game.inventoryBar,0,100,100,"Untagged");
        game.combatChips[0]=1;game.combatChips[6]=2;
        GameScript.inventoryOpen=true;GameScript.pausing=GameScript.dead=GameScript.buildMode=false;
        GadgetCoreAPI.Frozen=false;Input.left=Input.right=false;Input.mousePosition=Vector3.zero;Physics.Hit=cells[0];
        pad=new JoystickInputState();pad.Connect();ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),m=>throw new Exception(m),()=>active);Frame();Next();
    }
    static void Replay()
    {
        Check(Left,"native primary click projected");int i=Nav.SelectedSlot-152;
        if(game.holdingItem.id==0)game.SelectCombatChip(i);else if(game.combatChips[i]==0)game.PlaceCombatChip(i);else game.SwapCombatChip(i);
        ControllerMenuSelection.Update(game);ControllerMenuSelection.EndNativeUpdate();
    }
    public static int Main()
    {
        Setup();Check(ControllerMenuSelection.InventoryOpen(game),"chip menu owns UI while ordinary inventory is hidden");
        Check(Nav.SelectedSlot==152,"chip bar selection uses distinct native chip target");
        Check(ControllerMenuSelection.InventoryActionsOwnInput,"chip menu consumes shared UI inputs");
        var targets=new Collider[190];CombatChipMenuContext.CollectTargets(game,targets);
        for(int i=0;i<38;i++)Check(targets[152+i]==cells[i],"native chip slot "+i+" discovered");
        var selected=typeof(InventorySelectionNavigator).GetProperty("SelectedSlot",BindingFlags.Instance|BindingFlags.NonPublic);
        var move=typeof(InventorySelectionNavigator).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic);
        var seen=new HashSet<int>{152};var queue=new Queue<int>();queue.Enqueue(152);
        while(queue.Count>0){int current=queue.Dequeue();foreach(int direction in new[]{-1,1,-2,2}){selected.SetValue(Nav,current);move.Invoke(Nav,new object[]{direction});if(seen.Add(Nav.SelectedSlot))queue.Enqueue(Nav.SelectedSlot);}}
        for(int i=0;i<38;i++)Check(seen.Contains(152+i),"chip slot "+i+" reachable from equipped bar");
        Hover(0);Button(0);Replay();Check(game.holdingCombatChip&&game.holdingItem.id==1&&game.holdingItem.q==0,"native chip pickup retains quantity-zero representation");
        Next();Check(!Left,"held primary cannot repeatedly activate");Button(0,false);Hover(7);Button(0);Replay();Check(game.combatChips[7]==1&&!game.holdingCombatChip,"native placement updates chip slot");
        Button(0,false);Hover(6);Button(0);Replay();Button(0,false);Hover(7);Button(0);Replay();Check(game.combatChips[7]==2&&game.holdingItem.id==1&&game.holdingCombatChip,"native swap keeps held chip identity");
        Button(0,false);Button(1);Check(InventoryItemLifecycle.ReturnCalls==1,"cancel recognizes held quantity-zero chip");
        game.holdingItem=new Item{id=3};game.holdingCombatChip=true;Button(1,false);Trigger();Check(InventoryItemLifecycle.DropCalls==1,"dedicated RT Drop recognizes chip");
        Setup();Hover(0);Button(2);Check(!ControllerMenuSelection.VirtualMouseButtonDown(1),"secondary cannot split native chips");Button(2,false);Button(3);Check(!Left,"transfer does not invent a chip transfer");
        Setup();Hover(0);game.exitingcombatmode=true;Button(0);Check(!Left,"native exit cooldown blocks chip selection");
        Setup();Hover(0);GadgetCoreAPI.Frozen=true;Button(0);Check(!Left,"modal freeze blocks chip action");GadgetCoreAPI.Frozen=false;Next();Check(!Left,"unfreeze does not replay held action");Button(0,false);Button(0);Check(Left,"fresh primary after freeze is allowed");
        Setup();Hover(0);active=false;Button(0);Check(!Left,"mouse ownership leaves native mouse path");Input.left=true;Check(Left,"physical mouse click remains native");Input.left=false;active=true;Next();Check(!Left,"controller reclaim cannot replay held button");
        Setup();Hover(0);InventoryItemLifecycle.BlockMutations=true;Button(0);Check(!Left,"drop quarantine prevents chip mutations");
        Setup();Hover(0);game.holdingItem=new Item{id=11,q=2};Button(0);Check(!Left,"ordinary held item cannot be placed as a chip");
        Setup();Hover(0);game.holdingItem=new Item{id=1};game.holdingCombatChip=true;Button(0);Check(!Left,"same ID cannot trigger invented chip mutation");
        Setup();Hover(0);ControllerMenuSelection.Update(game);Check(InventoryFeedback.Primary=="Pick up chip","chip hint uses native chip action");
        int count=game.ChipPickups+game.ChipPlaces+game.ChipSwaps;for(int i=0;i<100;i++)CombatChipMenuContext.ActionLabel(game,152);Check(count==game.ChipPickups+game.ChipPlaces+game.ChipSwaps,"hint polling cannot move chips");
        Setup(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12").Replace("MenuDrop = RightTrigger","MenuDrop = Joystick1Button13"));
        Hover(0);Button(0);Check(!Left,"old activate binding no longer moves chips after remap");Button(0,false);Button(12);Replay();
        Button(12,false);Button(13);Check(InventoryItemLifecycle.DropCalls==1,"remapped dedicated drop works on held chip");
        Setup();Hover(0);game.holdingItem=new Item{id=2};game.holdingCombatChip=true;ControllerMenuSelection.Update(game);
        Check(SelectionCornerRenderer.Latest.HeldIconId==2&&SelectionCornerRenderer.Latest.HeldIconIsChip,"held chip uses chip art variant despite quantity zero");
        game.holdingItem=new Item{id=0};game.holdingCombatChip=false;Button(1);Check(game.CloseCalls==1,"empty-handed cancel closes chip menu through native close");
        Console.WriteLine("Combat Chip inventory integration: "+checks+" checks passed.");return 0;
    }
}
