using System;
using System.Reflection;
using ControllerSupport;
using GadgetCore.API;
using UnityEngine;

internal static class Program
{
    static int checks;
    static GameScript game;
    static JoystickInputState pad;
    static Collider backpack,input,other,output,craftButton,book,back;
    static bool active;
    static InventorySelectionNavigator Nav => (InventorySelectionNavigator)typeof(ControllerMenuSelection)
        .GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
    static Collider Add(GameObject root,string name,string tag,float x,float y,int layer=16)
    {
        var obj=new GameObject(name){tag=tag,layer=layer};root.transform.Add(obj.transform);
        return new Collider(obj){bounds=new Bounds{center=new Vector3(x,y,1),size=new Vector3(2,2,1)}};
    }
    static void Frame() { Time.frameCount++; Time.unscaledTime+=.05f; ControllerMenuSelection.Prepare(game); }
    static void Next() { ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();Frame(); }
    static void Hover(Collider target)
    {
        pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=0});pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=0});
        Physics.Hit=target;Input.mousePosition=new Vector3(Input.mousePosition.x+3,0);Next();Next();
    }
    static void Button(int number,bool held=true)
    { ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=(byte)number,Value=(short)(held?1:0)});Frame(); }
    static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    static bool Right => ControllerMenuSelection.VirtualMouseButtonDown(1);
    static void Setup(string config=null)
    {
        ControllerMenuSelection.Reset();active=true;ControllerInputOwnership.Configure(()=>active);
        game=new GameScript{inventoryMain=new GameObject("inventory"),inventoryBar=new GameObject("bar"),
            menuStorage=new GameObject("storage"),menuCraft=new GameObject("shared craft"),menuRecipe=new GameObject("shared recipes"),
            holdingItemObj=new GameObject("holdingItem"),craftType=0};
        game.menuStorage.SetActive(false);game.menuRecipe.SetActive(false);
        for(int i=0;i<game.inventory.Length;i++)game.inventory[i]=new Item();
        game.CraftItems=game.craft;
        backpack=Add(game.inventoryMain,"6","Untagged",0,0);
        input=Add(game.menuCraft,"0","craft",10,0);other=Add(game.menuCraft,"1","craft",14,0);
        Add(game.menuCraft,"2","craft",18,0);output=Add(game.menuCraft,"3","craft",22,0);
        craftButton=Add(game.menuCraft,"bCraft","Untagged",16,-4,0);book=Add(game.menuCraft,"bRecipe","Untagged",16,4,0);
        back=Add(game.menuRecipe,"bRecipe2","Untagged",22,9,0);
        Add(game.menuRecipe,"bRecUp","Untagged",18,7,0);Add(game.menuRecipe,"bRecDown","Untagged",10,7,0);
        for(int i=0;i<12;i++)Add(game.menuRecipe,i.ToString(),"recipe",10+(i/6)*8,3.5f-(i%6)*2.4f);
        game.recipeUnlocked0[0,0]=1;
        GameScript.inventoryOpen=true;GameScript.pausing=GameScript.dead=GameScript.buildMode=false;
        GadgetCoreAPI.Frozen=false;Input.left=Input.right=false;Input.mousePosition=Vector3.zero;Physics.Hit=backpack;
        pad=new JoystickInputState();pad.Connect();
        ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),
            message=>throw new Exception(message),()=>active);
        Frame();Next();
    }
    static void ReplayPrimary()
    {
        int index=CraftingMenuContext.NativeSlot(Nav.SelectedSlot);
        Check(Left&&index>=0,"native replay receives projected craft-cell primary");
        Item target=game.craft[index],held=game.holdingItem;
        if(held.id==0){game.holdingItem=target;game.craft[index]=new Item();}
        else if(target.id==0){game.craft[index]=held;game.holdingItem=new Item();}
        else if(GadgetCoreAPI.CanItemsStack(target,held)){target.q+=held.q;game.holdingItem=new Item();}
        else {game.craft[index]=held;game.holdingItem=target;}
        ControllerMenuSelection.EndNativeUpdate();ControllerMenuSelection.Update(game);
    }
    static void InputsAndActions()
    {
        Setup();Hover(input);Check(Nav.SelectedSlot==131,"Gear inputs share native crafting target IDs");
        game.craft[0]=new Item{id=101,q=9,extraData="emblem"};Button(0);ReplayPrimary();
        Check(game.holdingItem.q==9&&game.holdingItem.extraData=="emblem"&&game.craft[0].id==0,"primary pickup preserves exact held object metadata");
        Next();Check(!Left,"held pickup primary does not repeat");Button(0,false);Button(0);ReplayPrimary();
        Check(game.craft[0].q==9&&game.holdingItem.id==0,"primary placement uses native dispatch");
        Button(0,false);Button(2);Check(Right,"empty-handed secondary selects native split branch");
        Button(2,false);game.craft[0]=new Item();game.holdingItem=new Item{id=101,q=5,extraData="one"};Button(2);
        Check(Right,"held Gear emblem secondary projects native place-one into empty input");
        // Replay native PlaceOneItemCraft, whose loader keeps item metadata.
        game.craft[0]=GadgetCoreAPI.CopyItem(game.holdingItem);game.craft[0].q=1;game.holdingItem.q--;
        ControllerMenuSelection.EndNativeUpdate();ControllerMenuSelection.Update(game);
        Time.unscaledTime+=1;Next();
        Check(!Right&&game.craft[0].q==1&&game.holdingItem.q==4,"held secondary stops when native duplicate-input rule makes matching-stack repeat invalid");
        Button(2,false);int validations=game.CraftEligibilityCalls;Button(2);
        Check(!Right&&game.CraftEligibilityCalls==validations+1,"fresh secondary matching-stack attempt keeps native validation and rejection");
        Button(2,false);game.holdingItem=new Item{id=101,q=2,extraData="different"};Button(2);
        Check(!Right&&game.SwapCraftCalls==0,"native duplicate-ID secondary rejection cannot bypass into metadata swap");
        Button(2,false);game.holdingItem=new Item();Hover(output);game.craft[3]=new Item{id=500,q=1,extraData="weapon"};Button(3);
        Check(game.TransferCraftOutputCalls==1&&game.inventory[0].extraData=="weapon"&&game.craft[3].id==0,"native ShiftClickCraft moves exact Gear output to inventory");
        Button(3,false);game.holdingItem=new Item{id=101,q=1};Button(0);Check(!Left&&game.holdingItem.q==1,"held emblem cannot overwrite output");
        Setup();Hover(backpack);game.inventory[6]=new Item{id=101,q=4,extraData="inventory"};Button(3);
        Check(game.TransferCraftCalls==1&&game.CraftReceived.extraData=="inventory","native ShiftClick2 handles Gear inventory transfer");
        Setup();Hover(craftButton);game.canCraft=true;Button(0);Check(Left,"ready Gear Craft button uses shared native primary");
        Next();Check(!Left,"held Craft primary never auto-crafts");Button(0,false);Button(0);Check(Left,"fresh Craft presses remain available");
    }
    static void Navigation()
    {
        Setup();var targets=new Collider[152];CraftingMenuContext.CollectTargets(game,targets);
        foreach(int slot in new[]{131,132,133})
        {
            Hover(targets[slot]);
            pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=-25000});Next();
            Check(Nav.SelectedSlot==136&&!Left&&game.menuCraft.activeInHierarchy,"Gear input "+slot+" Up selects recipe book without opening it");
            Hover(targets[slot]);pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=25000});Next();
            Check(Nav.SelectedSlot==135&&!Left&&game.menuCraft.activeInHierarchy,"Gear input "+slot+" Down selects Craft without crafting");
        }
    }
    static void Stick(int direction)
    {
        pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=0});
        pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=0}); Next();
        pad.Apply(new RawJoystickEvent{Type=2,Number=(byte)(Math.Abs(direction)==1?3:4),Value=(short)(direction<0?-25000:25000)}); Next();
    }
    static void StationNavigationFollowup()
    {
        Setup(); Hover(book); Stick(-2);
        Check(Nav.SelectedSlot==136,"Gear Open Recipes Up holds");
        Hover(book); Stick(2); Check(Nav.SelectedSlot==134,"Gear Open Recipes Down selects crafted output");
        Hover(craftButton); Stick(1); Check(Nav.SelectedSlot==135,"Gear Craft Right holds");
        Hover(book); Button(0); Check(Left,"Gear open-book transition receives primary");
        Physics.Hit=backpack; game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        // Fixture GameObject.activeInHierarchy is flat: model the old parent's
        // inactive child explicitly, as real Unity hierarchy propagation does.
        book.gameObject.SetActive(false);
        // Real GameScript.Update postfix runs in the prepared frame, followed
        // by cursor/presentation queries before the next-frame context refresh.
        ControllerMenuSelection.Update(game); ControllerMenuSelection.EndNativeUpdate();
        Check(Nav.SelectedCollider==null,"Gear hidden Open Recipes collider is not exposed during transition");
        ControllerMenuSelection.VirtualMousePosition(); Next();
        Check(Nav.SelectedSlot==137&&!Left,"Gear open-book retains focus on corresponding Back button without repeat");
        Physics.Hit=back; Button(0,false); Button(0); Check(Left,"Gear Back receives fresh primary");
        game.menuRecipe.SetActive(false); game.menuCraft.SetActive(true);
        book.gameObject.SetActive(true); back.gameObject.SetActive(false);
        ControllerMenuSelection.Update(game); ControllerMenuSelection.EndNativeUpdate();
        Check(Nav.SelectedCollider==null,"Gear hidden Back collider is not exposed during transition");
        ControllerMenuSelection.VirtualMousePosition(); Next();
        Check(Nav.SelectedSlot==136&&!Left,"Gear Back retains focus on Open Recipes without repeat");
        back.gameObject.SetActive(true);
        Button(0,false); game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        Add(game.inventoryMain,"7","Untagged",10,-20);
        game.menuRecipe.SetActive(false); Next(); game.menuRecipe.SetActive(true); Next();
        var targets=new Collider[152]; CraftingMenuContext.CollectTargets(game,targets);
        foreach(int cell in new[]{5,11})
        {
            Hover(targets[140+cell]); Stick(2);
            Check(Nav.SelectedSlot==140+cell,"Gear bottom recipe "+cell+" Down holds despite lower inventory target");
        }
        // Only the bottom row blocks Down; the preceding row remains reachable.
        var followTargets=new Collider[152]; CraftingMenuContext.CollectTargets(game,followTargets);
        Hover(followTargets[144]); Stick(2);
        Check(Nav.SelectedSlot==145,"Gear penultimate recipe Down reaches bottom row");
        Setup(); Hover(book); active=false; Physics.Hit=backpack;
        game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        active=true; Next();
        Check(Nav.SelectedSlot==6,"Gear mouse-owned view change keeps native pointer selection");
        Setup(); Hover(book); Physics.Hit=backpack;
        game.craftType=1; game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        Check(Nav.SelectedSlot==6,"Gear station-mode change cannot inherit book focus");
        Setup(); Hover(book); Physics.Hit=backpack;
        game.menuCraft.SetActive(false); Next(); game.menuRecipe.SetActive(true); Next();
        Check(Nav.SelectedSlot==6,"Gear reopening after closed crafting context uses normal default");
        Setup(); Hover(book); active=false;
        book.gameObject.SetActive(false); game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Gear mouse-owned hidden selection clears immediately");
        Setup(); Hover(book); game.craftType=1;
        book.gameObject.SetActive(false); game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Gear cross-mode hidden selection clears immediately");
        Setup(); Hover(book); book.gameObject.SetActive(false); game.menuCraft.SetActive(false);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Gear closed crafting selection clears immediately");
        Setup(); Hover(craftButton); craftButton.gameObject.SetActive(false);
        game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Gear unrelated hidden control cannot inherit book focus");
    }
    static void ModeHandoff()
    {
        Setup();Hover(input);game.craft[0]=new Item{id=101,q=1};Button(0);Check(Left,"Gear prerequisite queues primary");
        game.craftType=1;
        Check(!Left,"same-frame Gear-to-Alchemy mode switch invalidates cached click");
        Next();Check(!Left,"held primary stays disarmed after same-root mode change");
        Button(0,false);game.craft[0]=new Item{id=11,q=2};Button(0);Check(Left,"fresh primary works in the new Alchemy mode");
        Setup();Hover(input);game.holdingItem=new Item{id=101,q=4};Button(2);Check(Right,"Gear prerequisite queues empty-input secondary");
        game.craftType=1;Check(!Right,"same-frame mode change clears queued secondary and repeat");
        Time.unscaledTime+=1;Next();Check(!Right&&game.holdingItem.q==4,"held secondary cannot resume across Gear/Alchemy mode change");
        Setup();Hover(input);game.craft[0]=new Item{id=101,q=1};
        ControllerMenuSelection.EndNativeUpdate();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=25000});Frame();
        game.craftType=1;ControllerMenuSelection.Prepare(game);int selected=Nav.SelectedSlot;Next();
        Check(Nav.SelectedSlot==selected,"held selection direction requires neutral across same-panel mode change");
        pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=0});Next();pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=25000});Next();
        Check(Nav.SelectedSlot!=selected,"fresh selection direction can navigate after neutral");
        Setup();game.menuCraft.SetActive(false);game.menuRecipe.SetActive(true);Next();Next();Button(5);
        Check(game.RecipeUpCalls==1,"Gear page action prerequisite");game.craftType=1;ControllerMenuSelection.Prepare(game);Next();
        Check(game.RecipeUpCalls==1,"held page action cannot repeat after recipe mode changes on same root");
        Button(5,false);Button(5);Check(game.RecipeUpCalls==2,"fresh page action works after recipe mode change");
        Setup();game.inventory[6]=new Item{id=11,q=2};
        pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=3,Value=1});game.craftType=1;
        ControllerMenuSelection.Prepare(game);
        Check(game.TransferCraftCalls==0&&game.inventory[6].q==2,"same-frame station mode change disarms a new transfer edge");
        Setup();game.holdingItem=new Item{id=11,q=2};
        pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=5,Value=1});game.craftType=1;
        ControllerMenuSelection.Prepare(game);
        Check(InventoryItemLifecycle.DropCalls==0&&game.holdingItem.q==2,"same-frame station mode change disarms a new Drop edge");
        Setup();game.holdingItem=new Item{id=11,q=2};
        pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=1,Value=1});game.craftType=1;
        ControllerMenuSelection.Prepare(game);
        Check(InventoryItemLifecycle.ReturnCalls==0&&game.holdingItem.q==2,"same-frame station mode change disarms a new Cancel edge");
    }
    static void RecipesOwnershipAndLimits()
    {
        Setup();game.menuCraft.SetActive(false);game.menuRecipe.SetActive(true);Next();Next();
        for(int i=0;i<6;i++){Button(5);Button(5,false);}
        Check(game.RecipeUpCalls==6&&game.curRecipePage==0,"Gear next-page binding wraps six native recipe pages");
        Button(4);Check(game.curRecipePage==5&&game.RecipeDownCalls==1,"Gear previous-page binding wraps to sixth page");
        Button(4,false);
        var targets=new Collider[152];CraftingMenuContext.CollectTargets(game,targets);Hover(targets[140]);Button(0);
        Check(!Left&&game.GearRecipeLookupCalls>0,"Gear recipe lock checks use native GetRecipeUnlocked0 mapping");
        Button(0,false);game.recipeUnlocked0[8,0]=1;Button(0);Check(Left,"unlocked Gear recipe projects native quick craft");
        active=false;Check(!Left,"mouse handoff cancels same-frame Gear recipe click");Input.left=true;Check(Left,"mouse-owned native click remains available");Input.left=false;
        active=true;Next();Check(!Left,"controller reclaim cannot replay held recipe activation");
        Setup();Hover(input);game.craft[0]=new Item{id=101,q=2};int calls=game.CraftEligibilityCalls;
        for(int i=0;i<100;i++)Next();Check(game.CraftEligibilityCalls==calls,"idle Gear selection never runs native error-producing validation");
        InventoryItemLifecycle.BlockMutations=true;Button(0);Check(!Left&&game.craft[0].q==2,"pending-drop quarantine blocks Gear mutation");
        Setup();Hover(craftButton);game.canCraft=true;GadgetCoreAPI.Frozen=true;Button(0);Check(!Left,"modal freeze blocks Gear Craft");
        GadgetCoreAPI.Frozen=false;Next();Check(!Left,"unfreeze cannot replay held Gear Craft action");
        Setup(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12"));
        Hover(input);game.craft[0]=new Item{id=101,q=1};Button(0);Check(!Left,"old primary is inactive after Gear mapping change");
        Button(0,false);Button(12);Check(Left,"configured Gear primary uses shared native click dispatch");
    }
    static void Main()
    { InputsAndActions();Navigation();StationNavigationFollowup();ModeHandoff();RecipesOwnershipAndLimits();ControllerMenuSelection.Reset();Console.WriteLine("Gear Forge shared inventory integration: "+checks+" checks passed."); }
}
