using System;
using System.Reflection;
using ControllerSupport;
using GadgetCore.API;
using UnityEngine;

// Real forge eligibility, shared navigator/config/action adapter and native-click
// projection. Source-backed native/loader conversion replay is a test double.
internal static class Program
{
    private static GameScript game;
    private static JoystickInputState pad;
    private static Collider input, other, toggle, hotkey, equipped;
    private static int checks;
    private static InventorySelectionNavigator Nav => (InventorySelectionNavigator)typeof(ControllerMenuSelection)
        .GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    private static void Check(bool condition,string reason)
    { checks++; if(!condition) throw new Exception(reason); }
    private static Collider Add(GameObject root,string name,float x,int layer=16)
    {
        var obj=new GameObject(name) { layer=layer }; root.transform.Add(obj.transform);
        return new Collider(obj) { bounds=new Bounds { center=new Vector3(x,0,1),size=new Vector3(2,2,1) } };
    }
    private static void Frame() { Time.frameCount++; Time.unscaledTime+=.05f; ControllerMenuSelection.Prepare(game); }
    private static void Next() { ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame(); Frame(); }
    private static void Button(int button,bool held=true)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent { Type=1,Number=(byte)button,Value=(short)(held?1:0) }); Frame();
    }
    private static void Hover(Collider target)
    { Physics.Hit=target; Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0); Next(); Next(); }
    private static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    private static bool Right => ControllerMenuSelection.VirtualMouseButtonDown(1);
    private static void Setup(bool mode=false,string config=null,bool trasher=false,bool prism=false)
    {
        ControllerMenuSelection.Reset(); GadgetCoreAPI.Frozen=false; GadgetCoreAPI.emblemForgeRecipes.Clear(); GadgetCoreAPI.prismForgeRecipes.Clear();
        game=new GameScript { inventoryMain=new GameObject("inventory"), inventoryBar=new GameObject("bar"),
            menuStorage=new GameObject("storage"), menuEmblemForge=new GameObject("forge"),
            holdingItemObj=new GameObject("held") };
        game.menuStorage.activeInHierarchy=false;
        game.menuTrasher=new GameObject("trasher");
        game.menuTrasher.activeInHierarchy=trasher; game.menuEmblemForge.activeInHierarchy=!trasher&&!prism;
        game.menuPrism=new GameObject("prism"); game.menuPrism.activeInHierarchy=prism;
        input=Add(game.inventoryMain,"6",0); other=Add(game.inventoryMain,"7",4);
        equipped=Add(game.inventoryMain,"36",8); hotkey=Add(game.inventoryBar,"0",-4);
        var wrapper=new GameObject("-"); (prism?game.menuPrism:trasher?game.menuTrasher:game.menuEmblemForge).transform.Add(wrapper.transform);
        toggle=Add(wrapper,prism?"bPrism":trasher?"bTrasher":"bEmblem",20,0);
        for(int i=0;i<45;i++) game.inventory[i]=new Item();
        game.emblemForging=!trasher&&!prism&&mode; game.trashing=trasher&&mode; game.prisming=prism&&mode;
        GameScript.inventoryOpen=true; GameScript.pausing=GameScript.dead=GameScript.buildMode=false;
        Input.left=Input.right=false; Input.mousePosition=Vector3.zero; Physics.Hit=input;
        pad=new JoystickInputState(); pad.Connect();
        ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),
            message=>throw new Exception(message),()=>true);
        Frame(); Next();
    }
    private static void Native()
    {
        Collider target=Nav.SelectedCollider;
        if(Left&&target==toggle)
        {
            if(game.holdingItem.id==0)
            {
                if(game.menuPrism.activeInHierarchy) { game.Prismer(); game.prismAgain=false; }
                else if(game.menuTrasher.activeInHierarchy) { game.Trasher(); game.trashAgain=false; }
                else { game.EmblemForging(); game.emblemAgain=false; }
            }
        }
        else if(target!=null&&(Left||Right))
        {
            int slot=int.Parse(target.gameObject.name);
            game.slotID=slot; // Native Update writes this BEFORE loader ForgeEmblem.
            if(game.trashing&&Left&&game.holdingItem.id==0)
            {
                // Native TrashItem replaces the complete input in-place, using
                // the effective native/loader GetItemWorth rather than mod pricing.
                Item source=game.inventory[slot];
                game.inventory[slot]=new Item { id=52,q=game.GetItemWorth(source.id)*source.q };
                game.TrashCalls++; game.trashAgain=true; game.trashing=false; game.Trasher();
            }
            else if((game.emblemForging||game.prisming)&&Left&&game.holdingItem.id==0)
            {
                Item source=game.inventory[game.slotID];
                Tuple<int,int> recipe;
                bool isPrism=game.prisming;
                bool custom=(isPrism?GadgetCoreAPI.prismForgeRecipes:GadgetCoreAPI.emblemForgeRecipes).TryGetValue(source.id,out recipe);
                int count=custom?recipe.Item2:10;
                int quantity=game.inventory[slot].q/count;
                game.inventory[slot].q-=quantity*10;
                if(isPrism) { game.PrismCalls++; game.Prismer(); game.prismAgain=false; }
                else { game.ForgeCalls++; game.EmblemForging(); game.emblemAgain=true; }
                game.holdingItem=new Item { id=custom?recipe.Item1:source.id+(isPrism?3:100),q=quantity };
                InventoryItemLifecycle.ReturnSuccess=false; // Generated output has no pickup origin.
            }
            else if(!game.emblemForging&&!game.trashing&&!game.prisming&&game.holdingItem.id>0)
            {
                if(game.emblemAgain) game.EmblemForging();
                if(Right)
                {
                    game.inventory[slot].id=game.holdingItem.id; game.inventory[slot].q++;
                    game.holdingItem.q--; if(game.holdingItem.q==0) game.holdingItem.id=0;
                }
                else { game.inventory[slot]=game.holdingItem; game.holdingItem=new Item(); }
            }
        }
        ControllerMenuSelection.EndNativeUpdate(); ControllerMenuSelection.Update(game);
    }
    private static void TrashScenarios()
    {
        Setup(trasher:true);
        for(int step=0;step<3;step++)
        {
            ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
            pad.Apply(new RawJoystickEvent { Type=2,Number=3,Value=25000 }); Frame();
            ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
            pad.Apply(new RawJoystickEvent { Type=2,Number=3,Value=0 }); Frame();
        }
        Check(Nav.SelectedSlot==130,"Right-stick navigation reaches nested Trasher toggle from inventory");
        Check(ItemConversionContext.ActionLabel(game,130)=="Enable trashing","Toggle hint names the native action");
        toggle.enabled=false; Next(); Check(Nav.SelectedSlot!=130,"Disabled native Trasher button is not selectable");
        toggle.enabled=true; Next(); Hover(toggle);
        Check(Nav.SelectedSlot==130,"Nested native Trasher toggle is selectable");
        Button(0); Check(Left&&!Right,"Primary projects a native Trasher toggle click"); Native();
        Check(game.trashing&&game.ToggleCalls==1,"Native toggle owns Trasher mode");
        Check(ItemConversionContext.ActionLabel(game,130)=="Cancel trashing"&&ItemConversionContext.CancelLabel(game)=="Cancel trashing",
            "Mode and B hints both explain cancellation");
        Next(); Check(!Left,"Held toggle primary cannot convert on mode entry");
        Button(0,false); Hover(input); game.inventory[6]=new Item{id=21,q=25};
        game.inventory[7]=new Item{id=22,q=7}; game.slotID=7; Button(0);
        Check(Left,"Eligible backpack conversion reaches native click"); Native();
        Check(game.slotID==6&&game.TrashCalls==1&&game.inventory[6].id==52&&game.inventory[6].q==50,
            "Native selected stack becomes exact Credits in the original slot");
        Check(game.inventory[7].q==7&&game.holdingItem.id==0&&game.trashing&&game.trashAgain,
            "Trasher leaves neighbors and held item intact and retains native mode");
        Next(); Check(!Left&&game.TrashCalls==1,"Held primary cannot convert twice");
        Button(0,false); Button(0); Check(!Left&&game.TrashCalls==1,"Credits cannot be converted again");
        Button(0,false); Button(1);
        Check(!game.trashing&&!game.trashAgain&&GameScript.inventoryOpen,"B cancels mode before closing");
        Button(1,false); Button(1); Check(!GameScript.inventoryOpen,"Next B closes cancelled Trasher");

        foreach(var item in new[]{new Item{id=0,q=25},new Item{id=21,q=0},new Item{id=51,q=10},new Item{id=52,q=10}})
        {
            Setup(true,trasher:true); game.inventory[6]=item; Button(0);
            Check(!Left&&game.TrashCalls==0,"Empty/protected stack never converts: "+item.id+"/"+item.q);
        }
        Setup(true,trasher:true); Hover(hotkey); game.inventory[0]=new Item{id=21,q=10}; Button(0);
        Check(!Left,"Hotkey conversion remains outside first Trasher slice");
        Setup(true,trasher:true); Hover(equipped); game.inventory[36]=new Item{id=21,q=10}; Button(0);
        Check(!Left,"Equipped conversion remains protected");
        foreach(int action in new[]{2,3})
        {
            Setup(true,trasher:true); game.inventory[6]=new Item{id=21,q=25}; Button(action);
            Check(!Left&&!Right&&game.inventory[6].q==25&&game.TransferInventoryCalls==0,
                "Secondary/transfer cannot mutate active Trasher input: "+action);
        }
        Setup(true,trasher:true); game.inventory[6]=new Item{id=5000,q=3}; game.ItemValue=id=>123;
        Button(0); Native(); Check(game.inventory[6].id==52&&game.inventory[6].q==369&&game.WorthCalls==1,
            "Custom registered value remains native and evaluated only on conversion");
        Setup(true,trasher:true); game.inventory[6]=new Item{id=21,q=25};
        for(int i=0;i<1000;i++) { Next(); ControllerMenuSelection.Update(game); }
        Check(game.WorthCalls==0&&game.TrashCalls==0&&game.inventory[6].q==25,"Idle hints never price or convert items");
        Check(InventoryFeedback.Primary=="Convert to Credits","Hint describes destructive Credits conversion");
        Setup(true,trasher:true); game.holdingItem=new Item{id=21,q=3}; game.trashAgain=true; Button(0);
        Check(!Left&&!game.trashing&&!game.trashAgain&&game.holdingItem.q==3,
            "Primary with held item cancels conversion without consuming/placing it");
        Next(); Button(0,false); Button(0); Check(Left,"Fresh primary resumes ordinary held-item placement"); Native();
        Check(game.inventory[6].q==3&&game.holdingItem.id==0,"Mode-off placement reuses shared inventory action");
        Setup(trasher:true); Hover(toggle); game.holdingItem=new Item{id=21,q=3}; Button(0);
        Check(!Left&&!game.trashing,"Toggle requires empty hands");
        Setup(true,trasher:true); game.inventory[6]=new Item{id=21,q=25}; InventoryItemLifecycle.BlockMutations=true; Button(0);
        Check(!Left&&game.TrashCalls==0,"Drop quarantine prevents conversion");
        foreach(int action in new[]{0,1,2,3,5,6,7})
        {
            Setup(true,trasher:true); game.inventory[6]=new Item{id=21,q=25}; GadgetCoreAPI.Frozen=true; Button(action);
            Check(!Left&&!Right&&game.trashing&&GameScript.inventoryOpen&&game.inventory[6].q==25,
                "Frozen input preserves Trasher state: "+action);
            GadgetCoreAPI.Frozen=false; Next(); Check(!Left&&game.trashing,"Unfreeze requires fresh Trasher action: "+action);
        }
        Setup(true,trasher:true); game.inventory[6]=new Item{id=21,q=25}; Input.mousePosition=new Vector3(10,0,0); Button(0);
        Check(!Left,"Deliberate physical mouse wins simultaneous conversion");
        Setup(false,BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12"),true);
        Hover(toggle); Button(0); Check(!Left,"Default A inactive after remap for Trasher");
        Button(0,false); Button(12); Check(Left,"Remapped primary toggles Trasher");
        Setup(true,trasher:true); game.menuTrasher.activeInHierarchy=false; Next(); Button(0);
        Check(!Left,"Closed Trasher pane cannot convert");
    }
    private static SelectionCornerRenderer Present()
    { ControllerMenuSelection.Update(game); return SelectionCornerRenderer.Latest; }
    private static void TrashingVisualScenarios()
    {
        Setup(trasher:true);
        Check(Present().Style==SelectionCornerStyle.Normal,"Inactive Trasher uses normal corners");
        Hover(toggle); Button(0); Native();
        Check(Present().Style==SelectionCornerStyle.Red,"Native Trasher toggle activates alert corners");
        Next(); Button(0,false); Hover(input);
        var active=Present();
        for(int frame=0;frame<100;frame++) { Next(); Present(); }
        Check(ReferenceEquals(active,SelectionCornerRenderer.Latest)&&active.Style==SelectionCornerStyle.Red,
            "Steady active frames reuse one styled renderer");
        Button(1); Check(Present().Style==SelectionCornerStyle.Normal&&GameScript.inventoryOpen,
            "B cancellation immediately restores normal corners while menu remains open");
        Button(1,false); Button(1); Present();
        Check(SelectionCornerRenderer.Latest.ShowTarget==null,"Closing Trasher hides the selection renderer");

        Setup(true,trasher:true); Check(Present().Style==SelectionCornerStyle.Red,"Already-active native Trasher starts with alert corners");
        Input.mousePosition=new Vector3(10,0,0); Next();
        Check(Present().Style==SelectionCornerStyle.Red&&game.trashing,
            "Mouse handoff preserves visible native active-mode indication");
        game.menuTrasher.activeInHierarchy=false; game.menuEmblemForge.activeInHierarchy=true; game.emblemForging=true;
        Next(); Check(Present().Style==SelectionCornerStyle.Blue,"Switching directly from Trasher to active Emblem Forge uses blue despite stale trashing flag");
        game.menuEmblemForge.activeInHierarchy=false; Next();
        Check(Present().Style==SelectionCornerStyle.Normal,"Ordinary inventory uses normal corners despite stale trashing flag");
        Setup(true); Check(Present().Style==SelectionCornerStyle.Blue,"Active Emblem Forge has blue active-mode corners");
        Setup(true,trasher:true); Present(); game.trashing=false; Next();
        Check(Present().Style==SelectionCornerStyle.Normal,"Native mouse-mode cancellation immediately restores normal corners");
        Setup(); Check(Present().Style==SelectionCornerStyle.Normal,"Inactive Emblem Forge uses normal corners");
        Hover(toggle); Button(0); Native();
        Check(Present().Style==SelectionCornerStyle.Blue,"Native forge activation immediately uses blue corners");
        Next(); Button(0,false); Hover(input); var blue=Present();
        for(int frame=0;frame<100;frame++) { Next(); Present(); }
        Check(ReferenceEquals(blue,SelectionCornerRenderer.Latest)&&blue.Style==SelectionCornerStyle.Blue,
            "Repeated active forge frames reuse the blue renderer");
        Input.mousePosition=new Vector3(20,0,0); Next();
        Check(Present().Style==SelectionCornerStyle.Blue,"Forge mouse handoff retains native active-mode blue");
        Next(); Button(1); Check(Present().Style==SelectionCornerStyle.Normal&&GameScript.inventoryOpen,
            "B cancels forging and restores normal corners before closing");
        Button(1,false); Button(1); Present();
        Check(SelectionCornerRenderer.Latest.ShowTarget==null,"Closing Emblem Forge hides selection corners");
        Setup(true); Present(); game.menuEmblemForge.activeInHierarchy=false; game.menuTrasher.activeInHierarchy=true; game.trashing=true;
        Next(); Check(Present().Style==SelectionCornerStyle.Red,"Switching directly from active forge to active Trasher restores red");
    }
    private static void PrismScenarios()
    {
        Setup(prism:true);
        for(int step=0;step<3;step++)
        {
            ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
            pad.Apply(new RawJoystickEvent { Type=2,Number=3,Value=25000 }); Frame();
            ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
            pad.Apply(new RawJoystickEvent { Type=2,Number=3,Value=0 }); Frame();
        }
        Check(Nav.SelectedSlot==130,"Right stick reaches nested native Prism toggle");
        Hover(toggle); Check(Nav.SelectedSlot==130,"Nested native Prism toggle is selectable");
        Check(ItemConversionContext.ActionLabel(game,130)=="Enable prism forging","Prism toggle hint names mode activation");
        Button(0); Check(Left,"Primary projects native Prismer toggle click"); Native();
        Check(game.prisming&&game.ToggleCalls==1,"Native toggle owns Prism mode side effects");
        Check(Present().Style==SelectionCornerStyle.Purple,"Active Prism uses purple corners");
        Next(); Check(!Left,"Holding activation across Prism mode entry never converts");
        foreach(int id in new[]{86,87,88}) foreach(int quantity in new[]{9,10,25})
        {
            Setup(true,prism:true); game.inventory[6]=new Item{id=id,q=quantity};
            game.inventory[7]=new Item{id=88,q=70}; game.slotID=7; Button(0);
            if(quantity<10)
            {
                Check(!Left&&game.PrismCalls==0&&game.inventory[6].q==quantity,"Under-ten shard stack rejected: "+id);
                continue;
            }
            Check(Left,"Eligible shard stack reaches native dispatch: "+id+"/"+quantity); Native();
            Check(game.slotID==6&&game.PrismCalls==1&&game.inventory[6].q==quantity%10&&game.inventory[7].q==70,
                "Native raycast updates private slotID and consumes only complete selected batches: "+id+"/"+quantity);
            Check(game.holdingItem.id==id+3&&game.holdingItem.q==quantity/10&&!game.prisming&&!game.prismAgain,
                "Native Prism produces correct held output and leaves mode off: "+id+"/"+quantity);
            Check(Present().Style==SelectionCornerStyle.Normal,"Native conversion immediately restores normal corners");
            Next(); Check(!Left&&game.PrismCalls==1,"Held primary cannot repeat Prism conversion");
        }
        foreach(var item in new[]{new Item{id=0,q=20},new Item{id=85,q=20},new Item{id=89,q=20},new Item{id=86,q=0}})
        {
            Setup(true,prism:true); game.inventory[6]=item; Button(0);
            Check(!Left&&game.PrismCalls==0,"Invalid/non-shard input rejected: "+item.id+"/"+item.q);
        }
        foreach(int count in new[]{-1,0,1,5,11,20})
        {
            Setup(true,prism:true); game.inventory[6]=new Item{id=5000,q=100};
            GadgetCoreAPI.prismForgeRecipes[5000]=Tuple.Create(7000,count); Button(0);
            Check(!Left&&game.inventory[6].q==100&&game.PrismCalls==0,"Unsafe Prism recipe count blocked: "+count);
        }
        Setup(true,prism:true); game.inventory[6]=new Item{id=5000,q=25};
        GadgetCoreAPI.prismForgeRecipes[5000]=Tuple.Create(7000,10); Button(0); Check(Left,"Safe custom Prism recipe reaches native click"); Native();
        Check(game.holdingItem.id==7000&&game.holdingItem.q==2&&game.inventory[6].q==5,
            "Native/loader owns safe custom output and exact remainder");
        foreach(int output in new[]{0,-1})
        {
            Setup(true,prism:true); game.inventory[6]=new Item{id=5000,q=20};
            GadgetCoreAPI.prismForgeRecipes[5000]=Tuple.Create(output,10); Button(0);
            Check(!Left,"Invalid custom Prism output blocked: "+output);
        }
        Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=20};
        GadgetCoreAPI.prismForgeRecipes[86]=Tuple.Create(7000,5); Button(0);
        Check(!Left,"Unsafe custom override cannot fall back to default shard recipe");
        Setup(true,prism:true); game.inventory[6]=new Item{id=21,q=20}; GadgetCoreAPI.emblemForgeRecipes[21]=Tuple.Create(9000,10); Button(0);
        Check(!Left,"Emblem recipes do not leak into Prism context");
        foreach(int action in new[]{2,3})
        {
            Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25}; Button(action);
            Check(!Left&&!Right&&game.inventory[6].q==25&&game.TransferInventoryCalls==0,"Secondary/transfer blocked in Prism mode: "+action);
        }
        foreach(bool bar in new[]{true,false})
        {
            Setup(true,prism:true); Hover(bar?hotkey:equipped); game.inventory[bar?0:36]=new Item{id=86,q=20}; Button(0);
            Check(!Left,"Hotkey/equipped targets cannot convert through Prism: "+bar);
        }
        Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25}; Button(0); Native(); Next(); Button(0,false); Button(1);
        Check(game.holdingItem.id==89&&game.holdingItem.q==2&&GameScript.inventoryOpen&&InventoryFeedback.LastReason=="No pickup source",
            "Generated Prism output has no invented safe-return source");
        Button(1,false); Hover(other); Button(0); Native();
        Check(game.inventory[7].id==89&&game.inventory[7].q==2&&game.holdingItem.id==0&&!game.prisming&&!game.prismAgain,
            "Ordinary Prism output placement does not invent Emblem-style rearming");
        Setup(true,prism:true); game.holdingItem=new Item{id=89,q=3}; game.prismAgain=true; Button(0);
        Check(!Left&&!game.prisming&&!game.prismAgain&&game.holdingItem.q==3,
            "Primary recovers held-item mode by cancelling without placing or consuming");
        Setup(prism:true); Hover(toggle); game.holdingItem=new Item{id=89,q=3}; Button(0);
        Check(!Left&&!game.prisming,"Prism toggle requires empty hands");
        Setup(true,prism:true); Present(); Button(1);
        Check(!game.prisming&&GameScript.inventoryOpen&&Present().Style==SelectionCornerStyle.Normal,
            "B cancels Prism before closing and clears purple indication");
        Button(1,false); Button(1); Present(); Check(!GameScript.inventoryOpen&&SelectionCornerRenderer.Latest.ShowTarget==null,"Next B closes Prism");
        Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25}; InventoryItemLifecycle.BlockMutations=true; Button(0);
        Check(!Left&&game.PrismCalls==0&&game.inventory[6].q==25,"Drop quarantine blocks Prism conversion");
        foreach(int action in new[]{0,1,2,3,5,6,7})
        {
            Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25}; GadgetCoreAPI.Frozen=true; Button(action);
            Check(!Left&&!Right&&game.prisming&&GameScript.inventoryOpen&&game.inventory[6].q==25,"Frozen input preserves Prism: "+action);
            GadgetCoreAPI.Frozen=false; Next(); Check(!Left&&game.prisming,"Unfreeze requires fresh Prism press: "+action);
        }
        Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25}; Input.mousePosition=new Vector3(10,0,0); Button(0);
        Check(!Left,"Mouse movement wins simultaneous Prism controller action");
        Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25};
        for(int frame=0;frame<1000;frame++) { Next(); Present(); }
        Check(game.PrismCalls==0&&game.inventory[6].q==25&&game.holdingItem.id==0,"Idle Prism hints never convert or consume input");
        Check(Present().Style==SelectionCornerStyle.Purple,"Repeated idle active Prism frames retain purple");
        game.menuPrism.activeInHierarchy=false; Next();
        Check(Present().Style==SelectionCornerStyle.Normal,"Closed Prism pane cannot leave stale purple corners");
        Button(0); Check(!Left,"Closed Prism pane cannot execute stale mode conversion");
        Setup(false,BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12"),prism:true);
        Hover(toggle); Button(0); Check(!Left,"Original primary inactive after Prism remap");
        Button(0,false); Button(12); Check(Left,"Configured primary activates native Prism toggle"); Native();
        Check(Present().Style==SelectionCornerStyle.Purple,"Remapped native Prism activation uses purple style");
        game.menuPrism.activeInHierarchy=false; game.menuTrasher.activeInHierarchy=true; game.trashing=true; Next();
        Check(Present().Style==SelectionCornerStyle.Red,"Direct Prism-to-Trasher transition restores red");
        game.menuTrasher.activeInHierarchy=false; game.menuPrism.activeInHierarchy=true; Next();
        Check(Present().Style==SelectionCornerStyle.Purple,"Returning to still-active Prism restores purple");
        game.menuPrism.activeInHierarchy=false; game.menuEmblemForge.activeInHierarchy=true; game.emblemForging=true; Next();
        Check(Present().Style==SelectionCornerStyle.Blue,"Direct Prism-to-Emblem transition uses blue");
    }
    private static void OwnershipScenarios()
    {
        Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25};
        game.holdingItemObj.transform.localScale=new Vector3(2,3,1);
        game.holdingItem=new Item{id=89,q=3}; Present();
        ControllerInputOwnership.Configure(() => false);
        Check(!ControllerMenuSelection.InventoryActionsOwnInput,"false ownership releases inventory input immediately");
        Check(SelectionCornerRenderer.Latest.ShowTarget==null,"false ownership hides cached colored corners");
        Check(game.holdingItemObj.transform.localScale.x==2,"false ownership restores exact native held scale");
        Check(InventoryFeedback.LastReason==null,"false ownership clears controller feedback");
        int consumed=ControllerGameplayInput.Consumed;
        foreach(int action in new[]{0,1,2,3,5,6,7}) Button(action);
        ControllerMenuSelection.Update(game);
        Check(!Left&&!Right&&ControllerMenuSelection.ExtraRowVirtualClickSlot(game)==-1,"false ownership cannot synthesize item clicks");
        Check(game.prisming&&game.holdingItem.q==3&&game.inventory[6].q==25&&GameScript.inventoryOpen,"false ownership preserves item/mode/menu state");
        Check(ControllerGameplayInput.Consumed==consumed,"false ownership never consumes gameplay input");
        Input.left=true; Check(Left,"false ownership preserves native mouse click"); Input.left=false;
        ControllerInputOwnership.Configure(() => true); Next();
        Check(!Left&&!Right&&GameScript.inventoryOpen,"ownership reclaim does not replay held actions");
        ControllerMenuSelection.Reset(); ControllerInputOwnership.Configure(() => true);
        Setup(true,prism:true); game.inventory[6]=new Item{id=86,q=25}; Button(0);
        Check(Left,"controller ownership queues native conversion click");
        ControllerInputOwnership.Configure(() => false);
        Check(!Left,"same-frame ownership loss invalidates queued inventory click");
        Check(ControllerMenuSelection.VirtualMousePosition().x==Input.mousePosition.x,"same-frame ownership loss returns native mouse position");
        ControllerInputOwnership.Configure(() => true); Next();
        Check(!Left,"held primary stays disarmed on ownership reclaim");
        Button(0,false); Button(0); Check(Left,"fresh primary after neutral regains controller conversion");
        ControllerMenuSelection.Reset();
    }
    private static void Main()
    {
        OwnershipScenarios();
        Setup(); Hover(toggle);
        Check(Nav.SelectedSlot==130,"Nested native forge toggle is selectable");
        Button(0); Check(Left&&!Right,"Configured primary routes native toggle"); Native();
        Check(game.emblemForging&&game.ToggleCalls==1,"Native toggle owns mode side effects");
        Next(); Check(!Left,"Holding activation after entering mode does not leak");
        Button(0,false); Hover(input); game.inventory[6]=new Item{id=21,q=25}; game.slotID=7;
        game.inventory[7]=new Item{id=39,q=100}; Button(0);
        Check(Left,"Eligible backpack stack queues native primary"); Native();
        Check(game.slotID==6&&game.ForgeCalls==1&&game.inventory[6].q==5&&game.inventory[7].q==100,
            "Native raycast sets correct slotID and consumes only selected input");
        Check(game.holdingItem.id==121&&game.holdingItem.q==2&&!game.emblemForging&&game.emblemAgain,
            "Native forge output, remainder and rearm marker retained");
        Check(InventoryFeedback.LastReason==null,"Conversion is not misreported as an item nochange");
        Next(); Check(!Left&&game.ForgeCalls==1,"Held primary cannot forge twice");
        Button(0,false); Button(1);
        Check(game.holdingItem.q==2&&GameScript.inventoryOpen&&InventoryFeedback.LastReason=="No pickup source",
            "Generated output cannot be returned to fabricated input provenance");
        Button(1,false); Hover(other); Button(0); Check(Left,"Mode-off output placement uses shared primary"); Native();
        Check(game.inventory[7].id==121&&game.inventory[7].q==2&&game.holdingItem.id==0&&game.emblemForging,
            "Native output placement rearms forging");
        Next(); Button(0,false); Button(1);
        Check(!game.emblemForging&&GameScript.inventoryOpen&&!game.emblemAgain,"Empty-handed B cancels forging before closing");
        Next(); Button(1,false); Button(1); Check(!GameScript.inventoryOpen,"Fresh empty-handed B closes after mode cancelled");

        foreach(var pair in new[]{new Item{id=0,q=20},new Item{id=21,q=9},new Item{id=41,q=20}})
        {
            Setup(true); game.inventory[6]=pair; Button(0);
            Check(!Left&&game.ForgeCalls==0,"Ineligible input never reaches native forge: "+pair.id+"/"+pair.q);
            ControllerMenuSelection.Update(game); Check(InventoryFeedback.Primary==null,"Invalid forge action omitted from hints");
        }
        foreach(int count in new[]{-1,0,1,5,11,20})
        {
            Setup(true); game.inventory[6]=new Item{id=5000,q=100}; GadgetCoreAPI.emblemForgeRecipes[5000]=Tuple.Create(7000,count);
            Button(0); Check(!Left&&game.inventory[6].q==100&&game.ForgeCalls==0,"Unsafe loader subtraction recipe count rejected: "+count);
        }
        Setup(true); game.inventory[6]=new Item{id=5000,q=20}; GadgetCoreAPI.emblemForgeRecipes[5000]=Tuple.Create(7000,10);
        Button(0); Check(Left,"Safe custom ten-item recipe accepted"); Native();
        Check(game.holdingItem.id==7000&&game.holdingItem.q==2&&game.inventory[6].q==0,"Custom output remains loader-owned");
        Setup(true); game.inventory[6]=new Item{id=5000,q=20}; GadgetCoreAPI.emblemForgeRecipes[5000]=Tuple.Create(0,10);
        Button(0); Check(!Left,"Invalid custom output fails closed");

        foreach(int action in new[]{2,3})
        {
            Setup(true); game.inventory[6]=new Item{id=21,q=20}; Button(action);
            Check(!Left&&!Right&&game.inventory[6].q==20&&game.TransferInventoryCalls==0,"Secondary/transfer reject forge mode: "+action);
        }
        Setup(true); Hover(hotkey); game.inventory[0]=new Item{id=21,q=20}; Button(0);
        Check(!Left,"Hotkey targets cannot masquerade as native forge input");
        Setup(true); Hover(equipped); game.inventory[36]=new Item{id=21,q=20}; Button(0);
        Check(!Left,"Equipped target cannot bypass equipment side effects via forging");

        Setup(true); game.holdingItem=new Item{id=121,q=3}; game.emblemAgain=true; Button(0);
        Check(!Left&&!game.emblemForging&&game.holdingItem.q==3&&!game.emblemAgain,"Primary cancels rearmed mode with held output without placing");
        Next(); Button(0,false); Button(2); Check(Right,"Secondary placement resumes after mode cancellation");
        Setup(); game.holdingItem=new Item{id=121,q=3}; game.emblemAgain=true; Button(2); Native();
        Check(game.emblemForging&&game.holdingItem.q==2,"Native place-one rearming with remainder preserved");
        Next(); Check(!Right,"Held secondary does not continue after native mode rearm");

        foreach(int action in new[]{0,1,2,3,5,6,7})
        {
            Setup(true); game.inventory[6]=new Item{id=21,q=20}; GadgetCoreAPI.Frozen=true; Button(action);
            Check(!Left&&!Right&&game.emblemForging&&GameScript.inventoryOpen&&game.inventory[6].q==20,
                "Frozen modal blocks forge action "+action);
            GadgetCoreAPI.Frozen=false; Next(); Check(!Left&&game.emblemForging,"Unfreeze requires fresh press "+action);
        }
        Setup(true); game.inventory[6]=new Item{id=21,q=20}; InventoryItemLifecycle.BlockMutations=true; Button(0);
        Check(!Left&&game.inventory[6].q==20,"Drop quarantine blocks forge mutation");

        Setup(true); game.inventory[6]=new Item{id=21,q=20}; Input.mousePosition=new Vector3(10,0,0); Button(0);
        Check(!Left,"Mouse movement wins simultaneous forge activation");
        Setup(false,BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12"));
        Hover(toggle); Button(0); Check(!Left,"Default A inactive after primary remap");
        Button(0,false); Button(12); Check(Left,"Remapped primary activates forge toggle");
        Setup(true); game.menuEmblemForge.activeInHierarchy=false; Next(); Button(0);
        Check(!Left,"Closing forge pane blocks mode operation");
        TrashScenarios(); TrashingVisualScenarios(); PrismScenarios();
        Console.WriteLine("Item conversion integration (Emblem Forge + Item Trasher + Prism Forge): "+checks+" checks passed.");
    }
}
