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
    private static void Setup(bool mode=false,string config=null)
    {
        ControllerMenuSelection.Reset(); GadgetCoreAPI.Frozen=false; GadgetCoreAPI.emblemForgeRecipes.Clear();
        game=new GameScript { inventoryMain=new GameObject("inventory"), inventoryBar=new GameObject("bar"),
            menuStorage=new GameObject("storage"), menuEmblemForge=new GameObject("forge"),
            holdingItemObj=new GameObject("held") };
        game.menuStorage.activeInHierarchy=false;
        input=Add(game.inventoryMain,"6",0); other=Add(game.inventoryMain,"7",4);
        equipped=Add(game.inventoryMain,"36",8); hotkey=Add(game.inventoryBar,"0",-4);
        var wrapper=new GameObject("-"); game.menuEmblemForge.transform.Add(wrapper.transform);
        toggle=Add(wrapper,"bEmblem",20,0);
        for(int i=0;i<45;i++) game.inventory[i]=new Item();
        game.emblemForging=mode;
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
            if(game.holdingItem.id==0) { game.EmblemForging(); game.emblemAgain=false; }
        }
        else if(target!=null&&(Left||Right))
        {
            int slot=int.Parse(target.gameObject.name);
            game.slotID=slot; // Native Update writes this BEFORE loader ForgeEmblem.
            if(game.emblemForging&&Left&&game.holdingItem.id==0)
            {
                Item source=game.inventory[game.slotID];
                Tuple<int,int> recipe;
                bool custom=GadgetCoreAPI.emblemForgeRecipes.TryGetValue(source.id,out recipe);
                int count=custom?recipe.Item2:10;
                int quantity=game.inventory[slot].q/count;
                game.inventory[slot].q-=quantity*10;
                game.ForgeCalls++; game.EmblemForging(); game.emblemAgain=true;
                game.holdingItem=new Item { id=custom?recipe.Item1:source.id+100,q=quantity };
                InventoryItemLifecycle.ReturnSuccess=false; // Generated output has no pickup origin.
            }
            else if(!game.emblemForging&&game.holdingItem.id>0)
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
    private static void Main()
    {
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
        Console.WriteLine("Emblem forge integration: "+checks+" checks passed.");
    }
}
