using System;
using System.Reflection;
using ControllerSupport;
using UnityEngine;

// Production navigator, input configuration and inventory action adapter.
// Portal snapshot transport and native item mutations are deterministic doubles.
internal static class Program
{
    private static GameScript game;
    private static JoystickInputState pad;
    private static Collider inventory, edge, planet, travel, hotkey;
    private static int checks;
    private static string tooltipContent;
    private static bool controllerActive;
    private static InventorySelectionNavigator Navigator => (InventorySelectionNavigator)typeof(ControllerMenuSelection)
        .GetField("navigator",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    private static void Check(bool condition,string reason)
    { checks++; if(!condition) throw new Exception(reason); }
    private static Collider Add(GameObject root,string name,float x,float y,int layer=16)
    {
        var obj=new GameObject(name) { layer=layer }; root.transform.Add(obj.transform);
        return new Collider(obj) { bounds=new Bounds { center=new Vector3(x,y,1),size=new Vector3(2,2,1) } };
    }
    private static void Frame() { Time.frameCount++; Time.unscaledTime+=.05f; ControllerMenuSelection.Prepare(game); }
    private static void Next() { ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame(); Frame(); }
    private static void Button(int number,bool held=true)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent { Type=1,Number=(byte)number,Value=(short)(held?1:0) }); Frame();
    }
    private static void Axis(byte number,float value)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent { Type=2,Number=number,Value=(short)(value*32767) }); Frame();
    }
    private static void Hover(Collider target)
    {
        Physics.Hit=target; Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0); Next(); Next();
    }
    private static bool Left => ControllerMenuSelection.VirtualMouseButtonDown(0);
    private static bool Right => ControllerMenuSelection.VirtualMouseButtonDown(1);
    private static void Setup(bool portal=true,string config=null)
    {
        ControllerMenuSelection.Reset(); GadgetCore.API.GadgetCoreAPI.Frozen=false; controllerActive=true;
        game=new GameScript { inventoryMain=new GameObject("inventory"), inventoryBar=new GameObject("bar"),
            menuStorage=new GameObject("storage"), holdingItemObj=new GameObject("held") };
        game.menuStorage.activeInHierarchy=false;
        game.hoverItem=new GameObject("hoverItem") { activeInHierarchy=false }; tooltipContent=null;
        inventory=Add(game.inventoryMain,"6",0,0); edge=Add(game.inventoryMain,"11",10,0);
        hotkey=Add(game.inventoryBar,"0",0,-10);
        var pane=new GameObject("planets"); planet=Add(pane,"0",20,0,22); travel=Add(pane,"bPlanetTele",20,-5,0);
        planet.gameObject.tag="planet";
        for(int i=0;i<45;i++) game.inventory[i]=new Item();
        GameScript.inventoryOpen=true; GameScript.pausing=GameScript.dead=GameScript.buildMode=false;
        Input.left=Input.right=false; Input.mousePosition=Vector3.zero; Physics.Hit=inventory;
        pad=new JoystickInputState(); pad.Connect();
        ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),
            message=>throw new Exception(message),()=>controllerActive);
        PortalMenuContext.Open=portal; PortalMenuContext.Targets[0]=planet; PortalMenuContext.Targets[26]=travel;
        Frame(); Next();
    }
    // Replay the verified native hover dispatch in GameScript.Update: items
    // and difficulty populate/activate hoverItem, while HoverPlanet is empty.
    // This runs AFTER production Prepare, exactly where native hover executes.
    private static void NativeHover()
    {
        Collider target=Navigator.SelectedCollider;
        if(target==null) return;
        if(target.gameObject.tag=="planet") return;
        if(target.gameObject.tag=="difficulty") tooltipContent="difficulty";
        else tooltipContent="item:"+target.gameObject.name;
        game.hoverItem.SetActive(true);
    }
    private static void TooltipTransitions()
    {
        Setup(); Hover(edge); NativeHover();
        Check(game.hoverItem.activeInHierarchy,"Native item branch opens tooltip");
        Physics.Hit=planet; Axis(3,1); NativeHover();
        Check(!game.hoverItem.activeInHierarchy,"Moving Right from inventory to planet clears stale item tooltip");
        Axis(3,0); Physics.Hit=edge; Axis(3,-1);
        Check(!game.hoverItem.activeInHierarchy,"Planet to item transition waits for native hover");
        NativeHover();
        Check(game.hoverItem.activeInHierarchy&&tooltipContent=="item:11","Native item hover repopulates after planet");
        Axis(3,0); int activations=game.hoverItem.SetActiveCalls; Next();
        Check(game.hoverItem.activeInHierarchy&&game.hoverItem.SetActiveCalls==activations,
            "Stable controller item does not clear tooltip each frame");

        Setup(); travel.gameObject.tag="difficulty"; Hover(travel); NativeHover();
        Check(game.hoverItem.activeInHierarchy&&tooltipContent=="difficulty","Native difficulty hover populates tooltip");
        Physics.Hit=planet; Axis(4,-1); NativeHover();
        Check(Navigator.SelectedSlot==100&&!game.hoverItem.activeInHierarchy,"Difficulty to planet clears shared tooltip");

        Setup(); travel.gameObject.tag="difficulty"; planet.enabled=false; Hover(edge); NativeHover();
        Physics.Hit=travel; Axis(3,1);
        Check(Navigator.SelectedSlot==126&&!game.hoverItem.activeInHierarchy,"Item to difficulty clears old content before native dispatch");
        NativeHover();
        Check(game.hoverItem.activeInHierarchy&&tooltipContent=="difficulty","Native difficulty content remains visible after transition");

        Setup(); Hover(edge); NativeHover();
        controllerActive=false; Physics.Hit=planet; Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0); Next(); NativeHover();
        Check(game.hoverItem.activeInHierarchy&&tooltipContent=="item:11","Physical mouse handoff keeps native hover behavior");
        Next();
        Check(game.hoverItem.activeInHierarchy,"Stable mouse ownership keeps native tooltip handling untouched");
        controllerActive=true; Next();
        Check(!game.hoverItem.activeInHierarchy,"Controller takeover on same collider clears old mouse tooltip first");

        Setup(); Hover(planet); controllerActive=false; Next();
        game.hoverItem.SetActive(true); Button(0);
        Check(!game.hoverItem.activeInHierarchy,"Primary activation taking controller ownership clears before native dispatch");

        Setup(); var stat=Add(game.inventoryMain,"1",10,5); stat.gameObject.tag="stat";
        Hover(stat); NativeHover(); Physics.Hit=planet; Axis(3,1); NativeHover();
        Check(Navigator.SelectedSlot==100&&!game.hoverItem.activeInHierarchy,"Stat to planet uses shared tooltip transition clearing");

        Setup(); Hover(edge); NativeHover(); GameScript.inventoryOpen=false; Next();
        Check(game.hoverItem.activeInHierarchy,"Closing inventory leaves native tooltip close handling alone");
        GameScript.inventoryOpen=true; Physics.Hit=planet; Next();
        Check(!game.hoverItem.activeInHierarchy,"Reopened controller context clears stale native tooltip");
    }
    private static void PortalOpeningSelection()
    {
        Setup();
        Check(Navigator.SelectedSlot==100,"Portal opens highlighted on Desolate Canyon despite stationary inventory hover");
        Check(PortalMenuContext.Prepared==-1&&!Left&&!Right,"Initial planet highlight does not activate or consume native action");
        var nav=new InventorySelectionNavigator(); Physics.Hit=inventory; nav.Update(game,pad);
        Check(nav.SelectedSlot==100,"Fresh portal navigator gives planet priority over initial mouse hover");
        Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0); nav.Update(game,pad);
        Check(nav.SelectedSlot==6,"Actual mouse movement after opening takes selection back");
        PortalMenuContext.Open=false; nav.Update(game,pad);
        PortalMenuContext.Open=true; nav.Update(game,pad);
        Check(nav.SelectedSlot==100,"Reopening portal while inventory remains open resets default highlight");
        GameScript.inventoryOpen=false; nav.Update(game,pad); GameScript.inventoryOpen=true;
        nav.Update(game,pad);
        Check(nav.SelectedSlot==100,"Reopening inventory and portal restores initial planet highlight");
        var second=Add(new GameObject("secondPlanetPane"),"1",24,0,22);
        PortalMenuContext.Targets[1]=second; planet.enabled=false;
        nav=new InventorySelectionNavigator(); nav.Update(game,pad);
        Check(nav.SelectedSlot==101,"Unavailable Desolate Canyon falls back to first eligible planet cell");
        second.enabled=false; nav=new InventorySelectionNavigator(); nav.Update(game,pad);
        Check(nav.SelectedSlot==126,"No eligible planet falls back to eligible portal control");
        travel.enabled=false; nav=new InventorySelectionNavigator(); nav.Update(game,pad);
        Check(nav.SelectedSlot==6,"No eligible portal target falls back to existing inventory target");
        Setup(false); Physics.Hit=edge; nav=new InventorySelectionNavigator(); nav.Update(game,pad);
        Check(nav.SelectedSlot==11,"Ordinary inventory still respects initial mouse hover");
        Setup(); Physics.Hit=inventory; Input.left=true; nav=new InventorySelectionNavigator(); nav.Update(game,pad);
        Check(nav.SelectedSlot==6,"Deliberate physical click takes priority on portal opening frame");
        Input.left=false;
    }
    private static void NativePortalControlNavigation()
    {
        Setup();
        // level1 menuPlanets611: position(7.75,-.125), scale16, rotation180X.
        // Native controls636/688 are direct layer-zero children, not planet cells.
        var pane=planet.transform.parent.gameObject;
        planet.bounds=new Bounds{center=new Vector3(8,6.25f,1),size=new Vector3(1,1,1)};
        travel.bounds=new Bounds{center=new Vector3(8.15f,-9.277f,1),size=new Vector3(6,2,1)};
        var challenge=Add(pane,"bChallenge",8.0652f,-6.4418f,0);
        PortalMenuContext.Targets[27]=challenge;
        foreach(int number in new[]{4,8,16})
        {
            float y=number==4?4.5f:number==8?2.75f:-.7522f;
            PortalMenuContext.Targets[number]=Add(pane,number.ToString(),8,y);
        }
        foreach(int expected in new[]{104,108,116,127,126})
        {
            Axis(4,0);Axis(4,1);
            Check(Navigator.SelectedSlot==expected,"Native portal geometry Down reaches "+expected);
        }
        Axis(4,0);Axis(4,-1);
        Check(Navigator.SelectedSlot==127,"Bottom Set portal Up returns to challenge control");
        Axis(4,0);
        Physics.Hit=challenge;
        PortalMenuContext.Allowed=false;Button(0);
        Check(!Left&&InventoryFeedback.LastReason=="Portal unavailable",
            "Unavailable focused challenge explains rejection without native action");
        Button(0,false);PortalMenuContext.Allowed=true;Button(0);
        Check(Left&&PortalMenuContext.Prepared==127,"Challenge control uses existing primary route");
    }
    private static void Main()
    {
        NativePortalControlNavigation();
        PortalOpeningSelection();
        TooltipTransitions();
        Setup(); Hover(edge); Physics.Hit=planet; Axis(3,1);
        Check(Navigator.SelectedSlot==100,"Inventory right edge enters portal pane");
        Axis(3,0); Axis(3,-1);
        Check(Navigator.SelectedSlot==11,"Portal Left returns to inventory pane");
        Axis(3,0); Axis(0,1);
        Check(Navigator.SelectedSlot==11,"Left movement stick does not navigate portal");
        Axis(0,0); Hover(planet);
        Check(Navigator.SelectedSlot==100,"Mouse selects portal collider on noninventory layer");
        game.holdingItem=new Item { id=21,q=7 };
        Button(0);
        Check(Left&&!Right&&PortalMenuContext.Prepared==100,"A prepares portal snapshot then emits native primary");
        Check(game.holdingItem.q==7&&InventoryItemLifecycle.ReturnCalls==0,"Planet selection preserves held stack");
        ControllerMenuSelection.EndNativeUpdate(); ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.LastReason==null,"Portal selection skips item nochange rejection");
        Check(InventoryFeedback.Primary=="Select planet"&&InventoryFeedback.Secondary==null,"Portal hints describe control");
        Button(0,false); Button(2);
        Check(!Left&&!Right&&PortalMenuContext.Prepared==100,"X cannot split/place on portal controls");
        Button(2,false); Button(3);
        Check(!Left&&!Right&&game.TransferInventoryCalls==0&&game.TransferCraftCalls==0,"Y cannot transfer into portal controls");
        Button(3,false); PortalMenuContext.Allowed=false; Button(0);
        Check(!Left&&InventoryFeedback.LastReason=="Portal unavailable","Locked portal activation reports eligibility reason");
        ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Primary==null,"Unavailable portal activation omitted from hints");
        Button(0,false); PortalMenuContext.Allowed=true; PortalMenuContext.NativeAllowed=false; Button(0);
        Check(!Left&&PortalMenuContext.Prepared==100,"GadgetCore-routed snapshot suppresses duplicate native click");
        Button(0,false); PortalMenuContext.NativeAllowed=true; Hover(travel); Button(0);
        Check(Left&&PortalMenuContext.Prepared==126,"Travel uses native primary without forced item return");
        Check(game.holdingItem.q==7&&InventoryItemLifecycle.ReturnCalls==0,"Travel preserves held item because native menu stays open");

        Setup(false); PortalMenuContext.Open=true; Physics.Hit=planet; Button(0);
        Check(!Left,"Portal opening while inventory already open rearms primary");
        Button(0,false); Hover(planet); Button(0);
        Check(Left,"Released primary can activate after pane transition");
        Button(0,false);
        var replacement=Add(new GameObject("replacementPane"),"new0",30,0,22);
        PortalMenuContext.Targets[0]=replacement; Physics.Hit=replacement; Next();
        Check(Navigator.SelectedCollider==replacement,"Page snapshot refresh replaces still-active old collider");
        Button(0);
        Check(Left&&PortalMenuContext.Prepared==100,"Page refresh does not rearm unchanged portal context");
        Button(0,false); replacement.enabled=false; Next();
        Check(Navigator.SelectedCollider==null,"Disabled target cannot retain selection");

        foreach(int frozenAction in new[]{0,1,2,3,5,6,7})
        {
            Setup(); Hover(planet); game.holdingItem=new Item{id=21,q=9};
            GadgetCore.API.GadgetCoreAPI.Frozen=true; Button(frozenAction);
            Check(!Left&&!Right&&game.holdingItem.q==9&&GameScript.inventoryOpen&&!GameScript.pausing&&
                InventoryItemLifecycle.ReturnCalls==0&&InventoryItemLifecycle.DropCalls==0&&PortalMenuContext.Prepared==-1,
                "Frozen portal suppresses controller action "+frozenAction);
            GadgetCore.API.GadgetCoreAPI.Frozen=false; Next();
            Check(!Left&&game.holdingItem.q==9&&GameScript.inventoryOpen&&!GameScript.pausing,
                "Unfreezing held action does not replay "+frozenAction);
        }
        Setup(); Hover(planet); GadgetCore.API.GadgetCoreAPI.Frozen=true; Button(0);
        GadgetCore.API.GadgetCoreAPI.Frozen=false; Button(0,false); Button(0);
        Check(Left,"Primary resumes after unfreeze and fresh press");

        Setup(true,BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12"));
        Hover(planet); Button(0); Check(!Left,"Legacy A disabled after primary remap");
        Button(0,false); Button(12); Check(Left,"Remapped primary activates portal");
        Button(12,false); Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0); Button(12);
        Check(!Left,"Physical mouse movement wins simultaneous controller primary");
        Button(12,false); int preparedBeforeMouse=PortalMenuContext.PrepareCalls; Input.left=true; Button(12);
        Check(Left&&PortalMenuContext.PrepareCalls==preparedBeforeMouse,"Physical click stays native and does not create a new portal snapshot");
        Input.left=false;
        Setup(); Hover(inventory); game.inventory[6]=new Item{id=21,q=8}; Button(0);
        Check(Left,"Inventory item remains actionable beside portal pane");
        Button(0,false); Hover(hotkey); game.holdingItem=new Item{id=21,q=5}; Button(0);
        Check(Left,"Hotkey assignment remains actionable beside portal pane");
        Button(0,false); Hover(planet); game.holdingItem=new Item{id=21,q=5}; Button(1);
        Check(InventoryItemLifecycle.ReturnCalls==1&&GameScript.inventoryOpen,"B returns held item while keeping combined menu open");
        Button(1,false); Button(1);
        Check(!GameScript.inventoryOpen,"Empty-handed B closes combined menu");
        Check(ControllerGameplayInput.Consumed>0,"Shared inventory context consumes UI frame input");
        Console.WriteLine("Portal inventory integration: "+checks+" checks passed.");
    }
}
