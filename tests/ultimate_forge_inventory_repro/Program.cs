using System;
using System.Reflection;
using ControllerSupport;
using GadgetCore.API;
using UnityEngine;

// The level getter is source-backed from native GetItemLevel. Mutation replay
// below models native Update; production lifecycle is exercised separately.
public partial class GameScript
{
    public int GetItemLevel(int exp)
    {
        int level=1, threshold=100, count=0;
        while(exp>threshold)
        {
            exp-=threshold; count++; threshold+=75+count*100; level++;
            if(level==10)break;
        }
        return level;
    }
}

internal static class Program
{
    static int checks;
    static GameScript game;
    static JoystickInputState pad;
    static Collider gear, middle, prism, output, craft, book, backpack;
    static bool active;
    static InventorySelectionNavigator Nav => (InventorySelectionNavigator)typeof(ControllerMenuSelection)
        .GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool value,string message) { checks++; if(!value)throw new Exception(message); }
    static Collider Add(GameObject root,string name,string tag,float x,float y,int layer=16)
    {
        var obj=new GameObject(name){tag=tag,layer=layer};root.transform.Add(obj.transform);
        return new Collider(obj){bounds=new Bounds{center=new Vector3(x,y,1),size=new Vector3(2,2,1)}};
    }
    static void Frame() { Time.frameCount++;Time.unscaledTime+=.05f;ControllerMenuSelection.Prepare(game); }
    static void Next() { ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();Frame(); }
    static void Hover(Collider target)
    { Axis(3,0);Axis(4,0);Physics.Hit=target;Input.mousePosition=new Vector3(Input.mousePosition.x+3,0);Next();Next(); }
    static void Axis(byte number,short value) { pad.Apply(new RawJoystickEvent{Type=2,Number=number,Value=value}); }
    static void Button(byte number,bool held=true)
    { ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=number,Value=(short)(held?1:0)});Frame(); }
    static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    static bool Right => ControllerMenuSelection.VirtualMouseButtonDown(1);
    static bool Allowed(int index,int action)
    { string reason;return CraftingMenuContext.CanActivate(game,index,action,out reason); }
    static Item Gear(int id=300,int exp=100000) => new Item{id=id,q=1,exp=exp,extraData="full gear data",tier=3,corrupted=1};
    static void Setup()
    {
        ControllerMenuSelection.Reset();active=true;ControllerInputOwnership.Configure(()=>active);
        game=new GameScript{inventoryMain=new GameObject("inventory"),inventoryBar=new GameObject("bar"),
            menuStorage=new GameObject("storage"),menuCraft=new GameObject("craft"),menuRecipe=new GameObject("recipe"),
            holdingItemObj=new GameObject("held"),craftType=2};
        game.menuStorage.SetActive(false);game.menuRecipe.SetActive(false);
        for(int i=0;i<game.inventory.Length;i++)game.inventory[i]=new Item();
        backpack=Add(game.inventoryMain,"6","Untagged",0,0);
        gear=Add(game.menuCraft,"0","craft",10,0);middle=Add(game.menuCraft,"1","craft",14,0);
        prism=Add(game.menuCraft,"2","craft",18,0);output=Add(game.menuCraft,"3","craft",22,0);
        craft=Add(game.menuCraft,"bCraft","Untagged",16,-4,0);book=Add(game.menuCraft,"bRecipe","Untagged",16,4,0);
        Add(game.menuRecipe,"bRecipe2","Untagged",22,9,0);
        Add(game.menuRecipe,"bRecUp","Untagged",18,7,0);Add(game.menuRecipe,"bRecDown","Untagged",10,7,0);
        // Deliberately leave recipe hitboxes active in this model: the context
        // must still refuse quick-craft even if an asset/mod exposes one.
        for(int i=0;i<12;i++)Add(game.menuRecipe,i.ToString(),"recipe",10+(i/6)*8,3.5f-(i%6)*2.4f);
        GameScript.inventoryOpen=true;GameScript.pausing=GameScript.dead=GameScript.buildMode=false;
        Input.left=Input.right=false;Input.mousePosition=Vector3.zero;Physics.Hit=backpack;GadgetCoreAPI.Frozen=false;
        pad=new JoystickInputState();pad.Connect();
        ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse(BindingConfig.DefaultText.Split('\n')),
            reason=>throw new Exception(reason),()=>active);
        Frame();Next();
    }
    static void ContextAndEligibility()
    {
        Setup();Check(CraftingMenuContext.IsUltimate(game)&&CraftingMenuContext.ModeIdentity(game)==2,"type2 recognized");
        var targets=new Collider[152];CraftingMenuContext.CollectTargets(game,targets);
        foreach(int index in new[]{131,133,134,135,136})Check(targets[index]!=null,"ultimate target collected "+index);
        Check(targets[132]==null&&!Allowed(132,0),"unused middle ingredient never actionable");
        foreach(int id in new[]{300,311,350,361,400,411,450,455,500,505,550,561,600,611,700,711,800,811,1000,1005})
        {
            game.holdingItem=Gear(id);Check(Allowed(131,0),"native Lv10 gear allowed "+id);
            Check(CraftingMenuContext.ActionLabel(game,131)=="Place ingredient","valid gear hint "+id);
        }
        foreach(int id in new[]{299,312,347,362,412,448,449,456,506,562,598,612,648,712,713,714,725,726,750,799,812,850,899,1006,9999})
        { game.holdingItem=Gear(id);Check(!Allowed(131,0)&&CraftingMenuContext.ActionLabel(game,131)==null,"native excluded gear rejected "+id); }
        game.holdingItem=Gear(300,0);Check(!Allowed(131,0),"underleveled gear rejected");
        foreach(int id in new[]{89,90,91})
        { game.holdingItem=new Item{id=id,q=9};Check(Allowed(133,0),"native prism allowed "+id);Check(!Allowed(131,0),"prism cannot enter gear slot "+id); }
        foreach(int id in new[]{88,92,101,300})
        { game.holdingItem=Gear(id);Check(!Allowed(133,0),"nonprism rejected "+id); }
        game.holdingItem=Gear();game.craft[0]=Gear(301);
        Check(!Allowed(131,0),"occupied gear input cannot swap");
        game.holdingItem=new Item{id=89,q=5};game.craft[2]=new Item{id=89,q=2};
        Check(!Allowed(133,0),"occupied prism input cannot merge");
        game.craft[2]=new Item();Check(!Allowed(133,1)&&!CraftingMenuContext.CanPlaceOne(game,133,game.holdingItem,game.craft[2]),"held secondary cannot place or repeat");
        Check(!Allowed(134,0),"held ingredient cannot overwrite output");
        game.holdingItem=new Item();game.craft[2]=new Item{id=89,q=6};
        Check(Allowed(133,1)&&CraftingMenuContext.SecondaryLabel(game,133)=="Split stack","empty secondary splits prism");
        Check(Allowed(131,1)&&CraftingMenuContext.SecondaryLabel(game,131)=="Pick up ingredient","empty secondary takes gear");
        game.craft[3]=Gear(312);Check(Allowed(134,1)&&CraftingMenuContext.SecondaryLabel(game,134)=="Take output","empty secondary takes output");
        Check(!CraftingMenuContext.CanTransferInventory(game,Gear()),"native unsupported inventory quick-transfer rejected");
        game.holdingItem=Gear();Hover(gear);Button(0);Check(!Left,"occupied gear rejection blocks virtual click");
        game.craft[0]=new Item();Button(0,false);Button(0);Check(Left,"eligible gear receives native primary click");
        var exact=game.holdingItem;game.craft[0]=exact;game.holdingItem=new Item();ControllerMenuSelection.EndNativeUpdate();ControllerMenuSelection.Update(game);
        Check(ReferenceEquals(game.craft[0],exact)&&game.craft[0].extraData=="full gear data","native replay retains exact metadata-bearing gear object");
    }
    static void NavigationAndActions()
    {
        Setup();Hover(gear);Axis(3,25000);Next();Check(Nav.SelectedSlot==133,"right from gear skips unusable middle slot");
        Hover(prism);Axis(3,-25000);Next();Check(Nav.SelectedSlot==131,"left from prism skips unusable middle slot");
        Hover(gear);Axis(4,-25000);Next();Check(Nav.SelectedSlot==136&&!Left,"Up selects book without activation");
        Hover(prism);Axis(4,25000);Next();Check(Nav.SelectedSlot==135&&!Left,"Down selects Craft without activation");
        Hover(craft);game.canCraft=false;Button(0);Check(!Left,"unready craft button rejected");
        game.canCraft=true;Button(0,false);Button(0);Check(Left,"ready Craft dispatches native primary");
        Next();Check(!Left,"holding A never repeats crafting");
        Button(0,false);Button(0);Check(Left,"fresh A advances native progress again");
        Setup();Hover(prism);game.craft[2]=new Item{id=89,q=8,extraData="prism data"};Button(2);
        Check(Right,"empty secondary reaches native prism split branch");
        Button(2,false);game.holdingItem=new Item{id=89,q=4};Button(2);Check(!Right,"held secondary cannot overwrite prism input");
        Setup();Hover(output);var item=Gear(312);game.craft[3]=item;Button(3);
        Check(game.TransferCraftOutputCalls==1&&ReferenceEquals(game.inventory[0],item)&&game.craft[3].id==0,"output Y uses effective native ShiftClickCraft with exact metadata");
        Setup();Hover(backpack);game.inventory[6]=Gear();Button(3);Check(game.TransferCraftCalls==0,"inventory Y never enters unsupported ShiftClick2");
        Setup();Hover(prism);game.craft[2]=new Item{id=90,q=3};InventoryItemLifecycle.BlockMutations=true;Button(0);Check(!Left,"quarantined drop blocks Ultimate mutations");
        InventoryItemLifecycle.BlockMutations=false;Button(0,false);active=false;Button(0);Check(!Left&&game.craft[2].q==3,"mouse ownership disables controller mutation");
    }
    static void RecipeDisplay()
    {
        Setup();game.menuCraft.SetActive(false);game.menuRecipe.SetActive(true);Next();
        var targets=new Collider[152];CraftingMenuContext.CollectTargets(game,targets);
        foreach(int index in new[]{137,138,139})Check(targets[index]!=null,"book control retained "+index);
        for(int i=0;i<12;i++)Check(targets[140+i]==null&&!Allowed(140+i,0)&&CraftingMenuContext.ActionLabel(game,140+i)==null,"ultimate recipe remains display-only "+i);
        Check(CraftingMenuContext.ActionLabel(game,137)=="Back to Ultimate Forge","book return identifies station");
        string reason;Check(CraftingMenuContext.TryChangeRecipePage(game,1,out reason)&&game.RecipeUpCalls==1,"book next delegates native recipe page method once");
        Check(CraftingMenuContext.TryChangeRecipePage(game,-1,out reason)&&game.RecipeDownCalls==1,"book previous delegates native recipe page method once");
        game.craftType=3;Check(!CraftingMenuContext.IsOpen(game),"creation machine remains outside supported crafting modes");
    }
    static void Main()
    { ContextAndEligibility();NavigationAndActions();RecipeDisplay();Console.WriteLine("PASS "+checks+" Ultimate Forge checks"); }
}
