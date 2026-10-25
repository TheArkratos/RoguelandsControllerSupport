using System;
using System.Reflection;
using System.Collections.Generic;
using ControllerSupport;
using UnityEngine;

// Production context, navigator and click adapter; native item dispatch is
// replayed below, while native IL parity is checked in phase51_native_il_repro.
internal static class Program
{
    static int checks;
    static GameScript game;
    static JoystickInputState pad;
    static Collider backpack, input, output, craftButton, recipeButton, recipeBack;
    static bool active;
    static InventorySelectionNavigator Nav => (InventorySelectionNavigator)typeof(ControllerMenuSelection)
        .GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
    static Collider Add(GameObject root,string name,string tag,float x,float y,int layer=16)
    {
        var obj=new GameObject(name){tag=tag,layer=layer}; root.transform.Add(obj.transform);
        return new Collider(obj){bounds=new Bounds{center=new Vector3(x,y,1),size=new Vector3(2,2,1)}};
    }
    static void Frame() { Time.frameCount++; Time.unscaledTime+=.05f; ControllerMenuSelection.Update(game); }
    static void Next() { ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame(); Frame(); }
    static void Hover(Collider target)
    { Physics.Hit=target; Input.mousePosition=new Vector3(Input.mousePosition.x+3,0); Next(); Next(); }
    static void Button(int number,bool held=true)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent{Type=1,Number=(byte)number,Value=(short)(held?1:0)}); Frame();
    }
    static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    static bool Right => ControllerMenuSelection.VirtualMouseButtonDown(1);
    static void Setup(string config = null)
    {
        ControllerMenuSelection.Reset(); active=true; ControllerInputOwnership.Configure(()=>active);
        game=new GameScript{inventoryMain=new GameObject("inventory"),inventoryBar=new GameObject("bar"),
            menuStorage=new GameObject("storage"),menuCraft=new GameObject("alchemy"),menuRecipe=new GameObject("recipes"),
            craftType=1,holdingItemObj=new GameObject("holdingItem")};
        game.menuStorage.SetActive(false); game.menuRecipe.SetActive(false);
        for(int i=0;i<45;i++) game.inventory[i]=new Item();
        backpack=Add(game.inventoryMain,"6","Untagged",0,0);
        input=Add(game.menuCraft,"0","craft",10,0); Add(game.menuCraft,"1","craft",14,0); Add(game.menuCraft,"2","craft",18,0);
        output=Add(game.menuCraft,"3","craft",22,0);
        craftButton=Add(game.menuCraft,"bCraftAlch","Untagged",16,-4,0);
        recipeButton=Add(game.menuCraft,"bRecipe","Untagged",16,4,0);
        recipeBack=Add(game.menuRecipe,"bRecipe2","Untagged",22,9,0);
        // Native menuRecipe is rotated180 degrees around X: serialized negative
        // Y is above the cells. Native cells are column-major, six per column.
        for(int i=0;i<12;i++) Add(game.menuRecipe,i.ToString(),"recipe",10+(i/6)*8,3.5f-(i%6)*2.4f);
        Add(game.menuRecipe,"bRecUp","Untagged",18,7,0); Add(game.menuRecipe,"bRecDown","Untagged",10,7,0);
        game.recipeUnlocked1[0,0]=1;
        GameScript.inventoryOpen=true; GameScript.pausing=GameScript.dead=GameScript.buildMode=false;
        GadgetCore.API.GadgetCoreAPI.Frozen=false; Input.left=Input.right=false; Input.mousePosition=Vector3.zero;
        Physics.Hit=backpack; pad=new JoystickInputState(); pad.Connect();
        ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),
            reason=>throw new Exception(reason),()=>active);
        Frame(); Next();
    }
    static Collider RecipeTarget(int index)
    {
        var targets=new Collider[CraftingMenuContext.FirstSlot+CraftingMenuContext.TargetCount];
        CraftingMenuContext.CollectTargets(game,targets); return targets[index];
    }
    static void Stick(int direction)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=0});
        pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=0}); Frame();
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent{Type=2,Number=(byte)(Math.Abs(direction)==1?3:4),Value=(short)(direction<0?-25000:25000)}); Frame();
    }
    static void RecipeHover(Collider target)
    {
        pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=0});
        pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=0});
        Hover(target);
    }
    static void RecipeNavigation()
    {
        Setup();
        for(int ingredient=0;ingredient<3;ingredient++)
        {
            var target=RecipeTarget(CraftingMenuContext.FirstSlot+ingredient);
            RecipeHover(target); Stick(-2);
            Check(Nav.SelectedSlot==CraftingMenuContext.RecipeToggle,"Up from ingredient "+ingredient+" selects visible recipe-book button");
            Check(game.menuCraft.activeInHierarchy&&!Left,"ingredient Up does not open book "+ingredient);
            RecipeHover(target); Stick(2);
            Check(Nav.SelectedSlot==CraftingMenuContext.CraftButton,"Down from ingredient "+ingredient+" selects actual craft button");
            Check(game.menuCraft.activeInHierarchy&&!Left,"ingredient Down does not craft "+ingredient);
        }
        recipeButton.enabled=false; craftButton.enabled=false; Next();
        RecipeHover(input); Stick(-2);
        Check(Nav.SelectedSlot==CraftingMenuContext.FirstSlot,"ingredient Up holds when recipe-book control is unavailable");
        Stick(2); Check(Nav.SelectedSlot==CraftingMenuContext.FirstSlot,"ingredient Down holds when Craft control is unavailable");
        recipeButton.enabled=craftButton.enabled=true; Next();
        // Output remains governed by the existing spatial navigator.
        var nearest=typeof(InventorySelectionNavigator).GetMethod("NearestInDirection",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(int direction in new[]{-2,2})
        {
            RecipeHover(output);
            int expected=(int)nearest.Invoke(Nav,new object[]{direction,false});
            Stick(direction);
            Check(Nav.SelectedSlot==expected,"output keeps normal spatial navigation in direction "+direction);
        }
        game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        Add(game.inventoryMain,"1","stat",0,20);
        game.menuRecipe.SetActive(false); Next(); game.menuRecipe.SetActive(true); Next();
        RecipeHover(recipeBack); Stick(-2);
        Check(Nav.SelectedSlot==CraftingMenuContext.RecipeBack,"Up from top crafting-return button holds selection");
        RecipeHover(RecipeTarget(CraftingMenuContext.PageDown)); Stick(1);
        Check(Nav.SelectedSlot==CraftingMenuContext.PageUp,"Right from physical left page button selects right page button");
        Stick(-1); Check(Nav.SelectedSlot==CraftingMenuContext.PageDown,"Left from physical right page button selects left page button");
        var right=RecipeTarget(CraftingMenuContext.PageUp); right.enabled=false; Next();
        RecipeHover(RecipeTarget(CraftingMenuContext.PageDown)); Stick(1);
        Check(Nav.SelectedSlot==CraftingMenuContext.PageDown,"missing paired page button holds selection rather than searching another target");
        right.enabled=true; Next();
        var selected=typeof(InventorySelectionNavigator).GetProperty("SelectedSlot",BindingFlags.Instance|BindingFlags.NonPublic);
        var move=typeof(InventorySelectionNavigator).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic);
        var seen=new HashSet<int>{CraftingMenuContext.PageDown}; var pending=new Queue<int>(); pending.Enqueue(CraftingMenuContext.PageDown);
        while(pending.Count>0)
        {
            int current=pending.Dequeue();
            foreach(int direction in new[]{-1,1,-2,2})
            {
                selected.SetValue(Nav,current); move.Invoke(Nav,new object[]{direction}); int next=Nav.SelectedSlot;
                if(next>=CraftingMenuContext.RecipeBack&&seen.Add(next)) pending.Enqueue(next);
            }
        }
        for(int cell=0;cell<12;cell++)
            Check(seen.Contains(CraftingMenuContext.FirstRecipe+cell),"recipe cell "+cell+" reachable within book from page controls");
        Check(game.menuRecipe.activeInHierarchy&&!Left,"book reachability navigation never activates a control");
        ControllerMenuSelection.Reset();
    }
    static void StationNavigationFollowup()
    {
        Setup();
        RecipeHover(recipeButton); Stick(-2);
        Check(Nav.SelectedSlot==136,"Alchemy Open Recipes Up holds");
        RecipeHover(recipeButton); Stick(2);
        Check(Nav.SelectedSlot==134,"Alchemy Open Recipes Down selects crafted output");
        RecipeHover(craftButton); Stick(1);
        Check(Nav.SelectedSlot==135,"Alchemy Craft Right holds");
        RecipeHover(recipeButton); Button(0); Check(Left,"Alchemy open-book transition receives primary");
        // Replay the native view change while the physical pointer stays over inventory.
        Physics.Hit=backpack; game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        // Fixture GameObject.activeInHierarchy is flat: model the old parent's
        // inactive child explicitly, as real Unity hierarchy propagation does.
        recipeButton.gameObject.SetActive(false);
        // Real GameScript.Update postfix runs in the prepared frame, followed
        // by cursor/presentation queries before the next-frame context refresh.
        ControllerMenuSelection.Update(game); ControllerMenuSelection.EndNativeUpdate();
        Check(Nav.SelectedCollider==null,"Alchemy hidden Open Recipes collider is not exposed during transition");
        ControllerMenuSelection.VirtualMousePosition(); Next();
        Check(Nav.SelectedSlot==137&&!Left,"Alchemy open-book retains focus on corresponding Back button without repeat");
        Physics.Hit=recipeBack; Button(0,false); Button(0); Check(Left,"Alchemy Back receives fresh primary");
        game.menuRecipe.SetActive(false); game.menuCraft.SetActive(true);
        recipeButton.gameObject.SetActive(true); recipeBack.gameObject.SetActive(false);
        ControllerMenuSelection.Update(game); ControllerMenuSelection.EndNativeUpdate();
        Check(Nav.SelectedCollider==null,"Alchemy hidden Back collider is not exposed during transition");
        ControllerMenuSelection.VirtualMousePosition(); Next();
        Check(Nav.SelectedSlot==136&&!Left,"Alchemy Back retains focus on Open Recipes without repeat");
        recipeBack.gameObject.SetActive(true);
        Button(0,false); game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        Add(game.inventoryMain,"7","Untagged",10,-20);
        game.menuRecipe.SetActive(false); Next(); game.menuRecipe.SetActive(true); Next();
        foreach(int cell in new[]{5,11})
        {
            RecipeHover(RecipeTarget(140+cell)); Stick(2);
            Check(Nav.SelectedSlot==140+cell,"Alchemy bottom recipe "+cell+" Down holds despite lower inventory target");
        }
        // Only the bottom row blocks Down; the preceding row remains reachable.
        var followTargets=new Collider[152]; CraftingMenuContext.CollectTargets(game,followTargets);
        RecipeHover(followTargets[144]); Stick(2);
        Check(Nav.SelectedSlot==145,"Alchemy penultimate recipe Down reaches bottom row");
        Setup(); RecipeHover(recipeButton); active=false; Physics.Hit=backpack;
        game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        active=true; Next();
        Check(Nav.SelectedSlot==6,"Alchemy mouse-owned view change keeps native pointer selection");
        Setup(); RecipeHover(recipeButton); Physics.Hit=backpack;
        game.craftType=0; game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        Check(Nav.SelectedSlot==6,"Alchemy station-mode change cannot inherit book focus");
        Setup(); RecipeHover(recipeButton); Physics.Hit=backpack;
        game.menuCraft.SetActive(false); Next(); game.menuRecipe.SetActive(true); Next();
        Check(Nav.SelectedSlot==6,"Alchemy reopening after closed crafting context uses normal default");
        Setup(); RecipeHover(recipeButton); active=false;
        recipeButton.gameObject.SetActive(false); game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Alchemy mouse-owned hidden selection clears immediately");
        Setup(); RecipeHover(recipeButton); game.craftType=0;
        recipeButton.gameObject.SetActive(false); game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Alchemy cross-mode hidden selection clears immediately");
        Setup(); RecipeHover(recipeButton); recipeButton.gameObject.SetActive(false); game.menuCraft.SetActive(false);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Alchemy closed crafting selection clears immediately");
        Setup(); RecipeHover(craftButton); craftButton.gameObject.SetActive(false);
        game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true);
        Check(Nav.SelectedCollider==null&&Nav.SelectedSlot==-1,"Alchemy unrelated hidden control cannot inherit book focus");
    }
    static void OpenRecipes()
    { game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next(); Next(); }
    static void RecipePaging()
    {
        Setup(); OpenRecipes(); game.holdingItem=new Item{id=11,q=4};
        Button(5);
        Check(game.RecipeUpCalls==1&&game.curRecipePage==1,"default RB selects next recipe page through native RecipeUp");
        Check(InventoryItemLifecycle.DropCalls==0&&game.holdingItem.q==4,"recipe paging owns RB before default held-item Drop");
        Next(); Check(game.RecipeUpCalls==1,"held next-page binding cannot repeat");
        Button(5,false); Button(5);
        Check(game.RecipeUpCalls==2&&game.curRecipePage==0,"fresh next-page press wraps native Alchemy pages");
        Button(5,false); Button(4);
        Check(game.RecipeDownCalls==1&&game.curRecipePage==1,"default LB uses native previous-page wrap");
        Button(4,false); int total=game.RecipeUpCalls+game.RecipeDownCalls;
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent{Type=1,Number=4,Value=1}); pad.Apply(new RawJoystickEvent{Type=1,Number=5,Value=1}); Frame();
        Check(game.RecipeUpCalls+game.RecipeDownCalls==total&&InventoryItemLifecycle.DropCalls==0,
            "simultaneous previous and next requests do not page or drop");
        Setup(); Button(5); game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        Check(game.RecipeUpCalls==0,"page action held while recipe pane opens is disarmed");
        Button(5,false); Button(5); Check(game.RecipeUpCalls==1,"fresh press after recipe entry can change page");
        Setup(); OpenRecipes(); GadgetCore.API.GadgetCoreAPI.Frozen=true; Button(5);
        Check(game.RecipeUpCalls==0,"modal freeze blocks recipe paging");
        GadgetCore.API.GadgetCoreAPI.Frozen=false; Next(); Check(game.RecipeUpCalls==0,"unfreeze cannot replay held page action");
        Button(5,false); Button(5); Check(game.RecipeUpCalls==1,"page action rearms after release following modal freeze");
        Setup(); OpenRecipes(); active=false; Button(5);
        Check(game.RecipeUpCalls==0,"mouse ownership disables recipe paging");
        active=true; Next(); Check(game.RecipeUpCalls==0,"controller reclaim cannot replay held page action");
        Button(5,false); Button(5); Check(game.RecipeUpCalls==1,"fresh page press works after controller reclaim");
        Setup(BindingConfig.DefaultText.Replace("MenuPreviousPage = Joystick1Button4","MenuPreviousPage = Joystick1Button12")
            .Replace("MenuNextPage = Joystick1Button5","MenuNextPage = Joystick1Button13")); OpenRecipes();
        Button(13); Check(game.RecipeUpCalls==1,"configured next-page button replaces default RB");
        Button(13,false); Button(12); Check(game.RecipeDownCalls==1,"configured previous-page button replaces default LB");
        Button(12,false); Button(4); Check(game.RecipeDownCalls==1,"old previous-page button no longer changes recipe page after remap");
        ControllerMenuSelection.Reset();
    }
    static void Main()
    {
        RecipeNavigation(); StationNavigationFollowup(); RecipePaging();
        Setup(); Hover(input); Check(Nav.SelectedSlot==131,"Alchemy input joins shared inventory selection");
        game.craft[0]=new Item{id=11,q=9,extraData="native"}; Button(0);
        Check(Left&&!Right,"configured primary projects native craft pickup");
        // Native Update consumes the shared virtual ray/click and calls its own craft operation.
        game.holdingItem=game.craft[0]; game.craft[0]=new Item(); ControllerMenuSelection.EndNativeUpdate();
        Next(); Check(!Left,"held primary cannot repeat pickup");
        Button(0,false); Button(0); Check(Left,"primary projects native ingredient placement");
        game.craft[0]=game.holdingItem; game.holdingItem=new Item(); ControllerMenuSelection.EndNativeUpdate();
        Button(0,false); Button(2); Check(Right,"empty-handed secondary uses native split branch");
        ControllerMenuSelection.EndNativeUpdate(); Button(2,false);
        game.holdingItem=new Item{id=12,q=5}; Button(2);
        Check(!Left&&!Right&&game.holdingItem.q==5,"held ingredient secondary preserves native no-op instead of inventing placement");
        Button(2,false); game.holdingItem=new Item(); Hover(output);
        game.craft[3]=new Item{id=51,q=2,extraData="output"}; Button(3);
        Check(game.TransferCraftOutputCalls==1&&game.inventory[0].extraData=="output"&&game.craft[3].id==0,
            "transfer reuses ShiftClickCraft and preserves exact output object metadata");
        Button(3,false); game.holdingItem=new Item{id=11,q=1}; Button(0);
        Check(!Left&&game.holdingItem.q==1,"held item cannot overwrite crafted output cell");
        game.holdingItem=new Item(); Button(0,false); Hover(craftButton);
        game.canCraft=false; Button(0); Check(!Left,"unready craft button rejects controller activation");
        Button(0,false); game.canCraft=true; Button(0); Check(Left,"ready craft button uses native primary dispatch");
        Next(); Check(!Left,"crafting requires fresh primary presses rather than held auto-crafting");
        Button(0,false); Button(0); Check(Left,"successive primary presses can advance native crafting");
        ControllerMenuSelection.EndNativeUpdate(); Button(0,false); Hover(recipeButton); Button(0);
        Check(Left,"recipe book opens through native control click");
        game.menuCraft.SetActive(false); game.menuRecipe.SetActive(true); Next();
        Check(!Left,"pane switch disarms held recipe-open action");
        Button(0,false); Hover(recipeBack); Button(0); Check(Left,"recipe back button uses shared primary");
        active=false;
        Check(!Left&&ControllerMenuSelection.VirtualMousePosition().x==Input.mousePosition.x,
            "same-frame mouse handoff invalidates Alchemy virtual click");
        Input.left=true; Check(Left,"mouse-owned native click remains available"); Input.left=false;
        Next(); Check(game.menuRecipe.activeInHierarchy,"mouse handoff does not mutate native recipe menu");
        active=true; Next(); Check(!Left,"controller reclaim requires release before new Alchemy action");
        Setup(); Hover(input); game.craft[0]=new Item{id=11,q=2};
        int validations=game.CraftEligibilityCalls;
        for(int i=0;i<100;i++) Next();
        Check(game.CraftEligibilityCalls==validations,"idle Alchemy selection and hints never call native error-producing validation");
        InventoryItemLifecycle.BlockMutations=true; Button(0); Check(!Left&&game.craft[0].q==2,"pending drop quarantine blocks Alchemy action");
        Setup();
        pad.BeginFrame(); pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=25000}); Frame();
        Check(Nav.SelectedSlot==131,"right stick moves from backpack into Alchemy input panel");
        Setup(); Hover(backpack); game.inventory[6]=new Item{id=11,q=4,extraData="ingredient"}; Button(3);
        Check(game.TransferCraftCalls==1&&game.CraftReceived.extraData=="ingredient"&&game.inventory[6].id==0,
            "backpack transfer reuses native ShiftClick2 without inventing movement");
        Setup(); Hover(backpack); game.inventory[6]=new Item{id=51,q=4}; Button(3);
        Check(game.TransferCraftCalls==0&&game.CraftEligibilityCalls==0&&game.inventory[6].q==4,
            "read-only effective ingredient eligibility rejects invalid quick transfer before native validation");
        Setup(); Hover(input); game.craft[0]=new Item{id=12,q=1}; game.CraftItems=game.craft;
        game.holdingItem=new Item{id=11,q=3}; int sounds=InventoryItemLifecycle.SoundCalls; Button(0);
        Check(!Left&&game.CraftEligibilityCalls==1&&game.NativeSameTierErrors==1,"native ingredient rejection runs once per deliberate attempt");
        Check(InventoryItemLifecycle.SoundCalls==sounds+1,"native rejection feedback is not replayed by adapter");
        Setup(); Hover(craftButton); game.canCraft=true; GadgetCore.API.GadgetCoreAPI.Frozen=true; Button(0);
        Check(!Left,"modal input freeze blocks Alchemy activation");
        GadgetCore.API.GadgetCoreAPI.Frozen=false; Next(); Check(!Left,"unfreeze cannot replay held Alchemy primary");
        Setup(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12"));
        Hover(input); game.craft[0]=new Item{id=11,q=1}; Button(0); Check(!Left,"default primary is inactive after configuration remap");
        Button(0,false); Button(12); Check(Left,"remapped primary reaches same native Alchemy dispatch");
        ControllerMenuSelection.Reset(); Console.WriteLine("Alchemy shared inventory integration: "+checks+" checks passed.");
    }
}
