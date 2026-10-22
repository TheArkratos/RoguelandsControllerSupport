using System;
using ControllerSupport;
using UnityEngine;

// Production selection, bindings, raw-pad state and virtual-click adapters;
// native inventory mutation is a deterministic double, not a Unity substitute.
internal static class Program
{
    private static GameScript game;
    private static JoystickInputState pad;
    private static Collider[] slots;
    private static Collider page, upgrade;
    private static int checks;
    private static string warning;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static Collider Add(GameObject parent, string name, string tag, int x)
    {
        var obj = new GameObject(name) { tag=tag };
        parent.transform.Add(obj.transform);
        return new Collider(obj) { bounds=new Bounds { center=new Vector3(x,2,1), size=new Vector3(1,1,1) } };
    }
    private static void Setup(bool storage=false, string config=null, int openingButton=-1)
    {
        ControllerMenuSelection.Reset();
        MenuScript.player = new GameObject("player"); GadgetCore.API.GadgetCoreAPI.Frozen = false;
        warning=null; GadgetCore.API.GadgetCoreAPI.ThrowOnCopy=false;
        game=new GameScript { inventoryMain=new GameObject("inventory"), inventoryBar=new GameObject("bar"),
            menuStorage=new GameObject("storage"), holdingItemObj=new GameObject("held") };
        game.menuStorage.activeInHierarchy=storage;
        slots=new Collider[51];
        for(int i=6;i<45;i++) slots[i]=Add(game.inventoryMain,i.ToString(),"Untagged",i);
        for(int i=45;i<51;i++) slots[i]=Add(game.inventoryBar,(i-45).ToString(),"Untagged",i);
        for(int i=0;i<36;i++) Add(game.menuStorage,i.ToString(),"storage",60+i);
        page=Add(game.menuStorage,"page0","Untagged",100);
        game.storageButton[0]=page.gameObject;
        upgrade=Add(game.menuStorage,"bStorageU","Untagged",101);
        for(int i=0;i<game.inventory.Length;i++) game.inventory[i]=new Item();
        for(int i=0;i<game.storage.Length;i++) game.storage[i]=new Item();
        GameScript.inventoryOpen=true; GameScript.pausing=GameScript.dead=GameScript.buildMode=false;
        Input.left=Input.right=false; Input.mousePosition=Vector3.zero;
        Physics.Hit=slots[6]; pad=new JoystickInputState(); pad.Connect();
        if(openingButton>=0) pad.Apply(new RawJoystickEvent { Type=1,Number=(byte)openingButton,Value=1 });
        ControllerMenuSelection.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),
            message=>warning=message, ()=>true);
        Frame(); ControllerMenuSelection.EndNativeUpdate();
        pad.BeginFrame(); Frame(); ControllerMenuSelection.EndNativeUpdate();
    }
    private static void Frame(float elapsed=0.05f)
    { Time.frameCount++; Time.unscaledTime+=elapsed; ControllerMenuSelection.Prepare(game); }
    private static void Hover(Collider target)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame(); Physics.Hit=target;
        Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0); Frame();
        ControllerMenuSelection.EndNativeUpdate();
    }
    private static void Press(int button)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent {Type=1,Number=(byte)button,Value=1}); Frame();
    }
    private static void Release(int button)
    {
        ControllerMenuSelection.EndNativeUpdate(); pad.BeginFrame();
        pad.Apply(new RawJoystickEvent {Type=1,Number=(byte)button,Value=0}); Frame();
        ControllerMenuSelection.EndNativeUpdate();
    }
    private static bool Left { get { return ControllerMenuSelection.VirtualMouseButtonDown(0); } }
    private static bool Right { get { return ControllerMenuSelection.VirtualMouseButtonDown(1); } }
    private static void Native(int slot, bool reject=false)
    {
        if(!reject && (Left||Right))
        {
            Item target=game.inventory[slot], held=game.holdingItem;
            if(held.id==0)
            {
                int amount=Right ? (target.q+1)/2 : target.q;
                game.holdingItem=new Item{id=target.id,q=amount}; target.q-=amount;
                if(target.q==0) target.id=0;
            }
            else if(target.id==0 || target.id==held.id)
            {
                int amount=Right ? 1 : held.q;
                target.id=held.id; target.q+=amount; held.q-=amount;
                if(held.q==0) held.id=0;
            }
            else { game.inventory[slot]=held; game.holdingItem=target; }
        }
        ControllerMenuSelection.EndNativeUpdate(); ControllerMenuSelection.Update(game);
    }
    private static void Main()
    {
        StoragePaging();
        Setup(); game.inventory[6]=new Item{id=21,q=8}; Press(0);
        Check(Left&&!Right,"Ordinary inventory A left-click");
        Check(ControllerMenuSelection.VirtualMousePosition().x==6,"Same-frame selected projection");
        Native(6); Check(game.holdingItem.q==8 && game.inventory[6].id==0,"Native pickup result retained");
        Release(0);
        Hover(slots[36]); game.holdingItem=new Item{id=21,q=8}; Press(0);
        Check(!Left,"Wrong equipment type rejected before native click"); Release(0);
        game.holdingItem=new Item{id=500,q=1}; Press(0);
        Check(Left,"Eligible weapon uses ordinary primary click"); Native(36); Release(0);
        for(int i=45;i<51;i++)
        {
            game.holdingItem=new Item{id=21,q=2}; Hover(slots[i]); Press(0);
            Check(Left && ControllerMenuSelection.VirtualMousePosition().x==i,"Hotkey target "+i);
            Native(i-45); Release(0);
        }
        Setup(); game.inventory[6]=new Item{id=21,q=8}; Press(2);
        Check(Right&&!Left,"X native secondary"); Native(6); Check(game.holdingItem.q==4,"Native split once");
        pad.BeginFrame(); Frame(0.5f); Check(!Right,"Held X never resplits"); ControllerMenuSelection.EndNativeUpdate(); Release(2);
        Setup(); game.holdingItem=new Item{id=21,q=4}; Press(2); Check(Right,"Place-one starts"); Native(6);
        pad.BeginFrame(); Frame(0.2f); Check(!Right,"Repeat delay"); ControllerMenuSelection.EndNativeUpdate();
        pad.BeginFrame(); Frame(0.2f); Check(Right,"Place-one repeats after delay"); Native(6);
        Release(2); Frame(0.5f); Check(!Right,"Release stops repeats"); ControllerMenuSelection.EndNativeUpdate();
        Setup(); game.holdingItem=new Item{id=21,q=4}; Press(2); Native(6); Hover(slots[7]);
        pad.BeginFrame(); Frame(0.5f); Check(!Right,"Selection change stops repeats"); ControllerMenuSelection.EndNativeUpdate();
        Setup(); game.holdingItem=new Item{id=21,q=4}; Press(2); Native(6,true);
        pad.BeginFrame(); Frame(0.5f); Check(!Right,"Native rejection stops repeats"); ControllerMenuSelection.EndNativeUpdate();
        Setup(); game.holdingItem=new Item{id=21,q=1}; Press(2); Native(6);
        pad.BeginFrame(); Frame(0.5f); Check(!Right,"Empty hand stops repeats"); ControllerMenuSelection.EndNativeUpdate();
        Setup(); game.holdingItem=new Item{id=21,q=4}; game.inventory[6]=new Item{id=22,q=2}; Press(2);
        Check(Right,"Secondary swap once"); Native(6);
        pad.BeginFrame(); Frame(0.5f); Check(!Right,"Holding cannot swap repeatedly"); ControllerMenuSelection.EndNativeUpdate();
        Setup(true); game.inventory[6]=new Item{id=21,q=4}; Press(3);
        Check(!Left&&!Right&&!game.shifting,"Y bypasses virtual mouse and keyboard Shift latch");
        Check(game.TransferInventoryCalls==1&&game.CoroutineSteps==1&&game.inventory[6].id==0&&game.storage[0].q==4,
            "Y invokes native inventory-to-storage coroutine immediately");
        ControllerMenuSelection.EndNativeUpdate(); Release(3);
        game.shifting=true; game.inventory[6]=new Item{id=22,q=3}; Press(3);
        Check(game.shifting&&game.TransferInventoryCalls==2&&game.storage[1].id==22,"Physical Shift remains held during controller transfer");
        ControllerMenuSelection.EndNativeUpdate();
        Check(game.shifting,"EndNativeUpdate does not alter physical Shift");
        Release(3); game.shifting=false; game.inventory[6]=new Item{id=23,q=2}; Press(0);
        Check(Left&&!game.shifting,"Physical Shift release followed by A remains ordinary pickup"); Native(6);
        Check(game.holdingItem.id==23&&game.TransferInventoryCalls==2,"A after Y does not accidentally transfer");
        Setup(true); Collider chest=game.menuStorage.transform.GetChild(30).GetComponent<Collider>();
        game.currentStoragePage=2; Item chestItem=new Item{id=31,q=7,tier=4}; game.storage[372]=chestItem;
        Hover(chest); Press(3);
        Check(!Left&&game.TransferStorageCalls==1&&game.LastNativeSlot==30&&ReferenceEquals(game.inventory[6],chestItem)&&game.storage[372].id==0,
            "Sixth-row chest transfer calls native storage direction using current page");
        Setup(true); Item equipped=new Item{id=500,q=1,tier=3}; game.inventory[36]=equipped; Hover(slots[36]); Press(3);
        Check(game.TransferInventoryCalls==1&&game.LastNativeSlot==36&&ReferenceEquals(game.storage[0],equipped),"Equipped source transfers through native route");
        Setup(); game.menuCraft=new GameObject("craft"); game.inventory[6]=new Item{id=32,q=9}; Press(3);
        Check(game.TransferCraftCalls==1&&game.CraftReceived.q==9&&game.inventory[6].id==0&&!Left,"Craft transfer calls native coroutine");
        Setup(); game.menuCraft=new GameObject("craft"); game.CraftAllowed=false; game.inventory[6]=new Item{id=32,q=9}; Press(3);
        Check(game.TransferCraftCalls==0&&game.inventory[6].q==9&&!Left,"Native craft eligibility prevents transfer");
        Setup(true); game.holdingItem=new Item{id=21,q=5}; Hover(page); Press(0);
        Check(Left&&game.holdingItem.q==5,"Page activation keeps held stack"); Release(0);
        Setup(); game.holdingItem=new Item{id=21,q=4}; Press(5);
        Check(InventoryItemLifecycle.DropCalls==1 && !Left,"RB invokes dedicated drop");
        pad.BeginFrame(); Frame(); Check(InventoryItemLifecycle.DropCalls==1,"Held RB drops once");
        Setup(config:BindingConfig.DefaultText.Replace("MenuDrop = Joystick1Button5","MenuDrop = Joystick1Button12"));
        game.holdingItem=new Item{id=21,q=2}; Press(5); Check(InventoryItemLifecycle.DropCalls==0,"Old Drop binding inactive");
        Release(5); Press(12); Check(InventoryItemLifecycle.DropCalls==1,"Rebound Drop invokes action");
        Setup(); game.holdingItem=new Item{id=21,q=2}; Press(1);
        Check(InventoryItemLifecycle.ReturnCalls==1&&game.CloseCalls==0&&GameScript.inventoryOpen,"B returns and leaves open");
        Release(1); Press(1); Check(game.CloseCalls==1,"Second B closes");
        foreach(int button in new[]{6,7})
        {
            Setup(); game.holdingItem=new Item{id=21,q=2}; Press(button);
            Check(InventoryItemLifecycle.ReturnCalls==1&&game.CloseCalls==1,"View/Menu returns then closes");
            Check(game.PauseCalls==(button==7?1:0),"Only Menu pauses once");
            ControllerMenuSelection.Prepare(game); Check(game.CloseCalls==1,"Idempotent same-frame Prepare");
            Setup(); game.holdingItem=new Item{id=21,q=2}; InventoryItemLifecycle.ReturnSuccess=false; Press(button);
            Check(game.CloseCalls==0&&game.holdingItem.q==2,"Failed return preserves UI/item");
        }
        Setup(openingButton:0); Check(!Left,"Opening-held primary cannot click");
        pad.BeginFrame(); Frame(); Check(!Left,"Held opening action remains disarmed"); Release(0); Press(0); Check(Left,"Release rearms action");
        Setup(); Physics.Hit=new Collider(new GameObject("front blocker")); Press(0); Check(!Left,"Frontmost collider must match selection");
        Setup(); pad.BeginFrame(); pad.Apply(new RawJoystickEvent{Type=1,Number=0,Value=1}); Input.right=true; Frame();
        Check(!Left&&Right&&ControllerMenuSelection.VirtualMousePosition().x==Input.mousePosition.x,"Mouse click wins over controller");
        Input.right=false;
        Setup(); pad.Apply(new RawJoystickEvent{Type=1,Number=0,Value=1}); Input.mousePosition=new Vector3(100,0,0); Frame();
        Check(!Left,"Mouse motion suppresses simultaneous controller action");
        Setup(true); game.inventory[6]=new Item{id=21,q=3};
        GadgetCore.API.GadgetCoreAPI.ThrowOnCopy=true; Press(3);
        Check(warning!=null && !game.shifting&&game.TransferInventoryCalls==0,"Copy failure prevents transfer without touching keyboard Shift");
        Check(!Left,"Adapter exception clears virtual click");
        Setup(true); game.inventory[6]=new Item{id=21,q=3}; Press(3);
        try { throw new InvalidOperationException("native update failure"); }
        catch(InvalidOperationException) { }
        finally { ControllerMenuSelection.EndNativeUpdate(); }
        Check(!game.shifting&&!Left,"Native exception cleanup restores shift and click");
        string rebound=BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button11")
            .Replace("MenuSecondary = Joystick1Button2","MenuSecondary = Joystick1Button12")
            .Replace("MenuTransfer = Joystick1Button3","MenuTransfer = Joystick1Button13")
            .Replace("MenuCancel = Joystick1Button1","MenuCancel = Joystick1Button14");
        Setup(true,rebound); game.inventory[6]=new Item{id=21,q=8}; Press(0); Check(!Left,"Old primary binding inactive");
        Release(0); Press(11); Check(Left,"Rebound primary routes native click"); Release(11);
        Press(12); Check(Right,"Rebound secondary routes native click"); Release(12);
        Press(13); Check(!Left&&game.TransferInventoryCalls==1&&game.storage[0].q==8&&!game.shifting,"Rebound transfer routes native coroutine"); Release(13);
        Press(1); Check(game.CloseCalls==0,"Old Cancel binding inactive"); Release(1);
        Press(14); Check(game.CloseCalls==1,"Rebound Cancel closes");
        Setup(); game.inventory[6]=new Item{id=21,q=8}; ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Primary=="Pick up"&&InventoryFeedback.Secondary=="Split", "Empty-hand contextual native labels");
        Check(!InventoryFeedback.Transfer&&!InventoryFeedback.Held,"Transfer hidden without container");
        game.holdingItem=new Item{id=21,q=3}; ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Primary=="Combine"&&InventoryFeedback.Secondary=="Place one"&&InventoryFeedback.Held,"Compatible stack labels");
        Hover(slots[36]); ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Primary==null&&InventoryFeedback.Secondary==null,"Invalid equipment hides actions");
        Setup(true); game.inventory[6]=new Item{id=21,q=8}; ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Transfer,"Container allows native quick transfer hint");
        Hover(page); ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Primary=="Select page"&&InventoryFeedback.Secondary==null,"Page context hides item actions");
        foreach(bool storage in new[]{false,true})
        {
            Setup(storage); Item held=new Item{id=21,q=5,tier=1,exp=9,aspect=new[]{3}};
            Item target=new Item{id=21,q=2,tier=2,exp=7,aspect=new[]{4}};
            game.holdingItem=held;
            if(storage) { game.storage[0]=target; Hover(game.menuStorage.transform.GetChild(0).GetComponent<Collider>()); }
            else game.inventory[6]=target;
            Press(2);
            Check(!Left&&!Right&&ReferenceEquals(game.holdingItem,target)&&
                ReferenceEquals(storage?game.storage[0]:game.inventory[6],held),"Incompatible metadata secondary uses exact native swap "+storage);
            Check(storage?game.SwapStorageCalls==1:game.SwapCalls==1,"Correct swap direction "+storage);
            Native(6); // No virtual right-click may fall through into native PlaceOneItem.
            Check(ReferenceEquals(game.holdingItem,target)&&held.q==5&&target.q==2,"No second mutation after direct swap "+storage);
            pad.BeginFrame(); Frame(0.5f);
            Check(!Right&&(storage?game.SwapStorageCalls==1:game.SwapCalls==1),"Holding X never repeats metadata swap "+storage);
        }
        foreach(bool storage in new[]{false,true})
        {
            Setup(storage);
            Item held=new Item{id=21,q=5,extraData="held metadata"};
            Item target=new Item{id=21,q=5,extraData="target metadata"};
            game.holdingItem=held;
            if(storage) { game.storage[0]=target; Hover(game.menuStorage.transform.GetChild(0).GetComponent<Collider>()); }
            else game.inventory[6]=target;
            Press(2);
            ControllerMenuSelection.Update(game);
            Check(ReferenceEquals(game.holdingItem,target) &&
                ReferenceEquals(storage?game.storage[0]:game.inventory[6],held), "Extra-data-only difference swaps exactly " + storage);
            Check(InventoryFeedback.LastReason==null, "Successful extra-data-only swap has no false rejection " + storage);
        }
        foreach(string mode in new[]{"emblemForging","trashing","prisming"})
        {
            foreach(int action in new[]{0,2,3})
            {
                Setup(true); game.inventory[6]=new Item{id=21,q=8};
                typeof(GameScript).GetField(mode).SetValue(game,true); Press(action);
                Check(!Left&&!Right&&game.TransferInventoryCalls==0&&game.inventory[6].q==8,"Special mode blocks generic mutation "+mode+" "+action);
                ControllerMenuSelection.Update(game);
                Check(InventoryFeedback.Primary==null&&InventoryFeedback.Secondary==null&&!InventoryFeedback.Transfer,"Special mode hides generic hints "+mode);
            }
        }
        foreach(int action in new[]{0,2,3,5,1,6,7})
        {
            Setup(true); game.inventory[6]=new Item{id=21,q=8}; InventoryItemLifecycle.BlockMutations=true; Press(action);
            Check(!Left&&!Right&&game.TransferInventoryCalls==0&&InventoryItemLifecycle.ReturnCalls==0&&
                InventoryItemLifecycle.DropCalls==0&&game.CloseCalls==0,"Lifecycle quarantine blocks action "+action);
        }
        Setup(true); Hover(upgrade); ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Primary=="Upgrade","Upgrade hint uses sufficient native Credits/cost");
        game.Credits=game.UpgradeCost-1; ControllerMenuSelection.Update(game);
        Check(InventoryFeedback.Primary==null,"Upgrade hint hides when native Credits insufficient");
        Press(0); Check(!Left,"Insufficient Credits blocks upgrade dispatch"); Release(0);
        game.Credits=game.UpgradeCost; Press(0); Check(Left,"Exact native upgrade cost permits dispatch");
        Console.WriteLine("Production inventory action routing: "+checks+" checks passed.");
    }

    private static void Pages(int last)
    {
        game.SetStorageLevel(last);
        for (int i = 1; i < 12; i++)
            game.storageButton[i] = Add(game.menuStorage, "page" + i, "Untagged", 100 + i).gameObject;
        pad.BeginFrame(); Frame(); ControllerMenuSelection.EndNativeUpdate();
    }

    private static void StoragePaging()
    {
        Setup(true); Pages(11);
        Item held = new Item { id=21, q=7, extraData="source metadata" }; game.holdingItem=held;
        for (int i = 1; i <= 12; i++)
        {
            Press(5);
            Check(game.currentStoragePage == i % 12 && game.PageCalls == i, "RB ordered page cycle " + i);
            Check(ReferenceEquals(game.holdingItem,held) && held.q==7 && held.extraData=="source metadata" &&
                InventoryItemLifecycle.DropCalls==0, "paging preserves held stack/metadata without drop " + i);
            Check(!Left && !Right && ControllerMenuSelection.VirtualMousePosition().x==6,
                "paging preserves selected backpack cell " + i);
            pad.BeginFrame(); Frame(1f); Check(game.PageCalls==i, "held RB never repeats " + i);
            Release(5);
        }
        Press(4); Check(game.currentStoragePage==11, "LB wraps page 1 to page 12"); Release(4);
        Press(4); Check(game.currentStoragePage==10, "LB selects previous page"); Release(4);
        Collider chest=game.menuStorage.transform.GetChild(12).GetComponent<Collider>();
        Hover(chest); Press(5);
        Check(game.currentStoragePage==11 && ControllerMenuSelection.VirtualMousePosition().x==72,
            "paging retains selected storage cell");
        Release(5);
        Setup(true); Pages(2); Press(4); Check(game.currentStoragePage==2, "LB wraps across unlocked pages only");
        Release(4); Press(5); Check(game.currentStoragePage==0, "RB wraps last unlocked page to first"); Release(5);
        game.storageButton[1].activeInHierarchy=false;
        Press(5); Check(game.currentStoragePage==2, "hidden page skipped"); Release(5);
        game.SetStorageLevel(0); game.currentStoragePage=0; int calls=game.PageCalls;
        Press(5); Check(game.PageCalls==calls && InventoryItemLifecycle.DropCalls==0, "one page reserves RB without dropping"); Release(5);
        Setup(true, openingButton:5); Pages(11);
        Check(game.PageCalls==0 && InventoryItemLifecycle.DropCalls==0, "opening-held RB neither pages nor drops");
        Release(5); Press(5); Check(game.currentStoragePage==1, "release rearms opening bumper"); Release(5);

        foreach (string blocked in new[] { "freeze", "disconnect", "remote", "scene", "player" })
        {
            Setup(true); Pages(11); game.holdingItem=new Item {id=21,q=4};
            if(blocked=="freeze") GadgetCore.API.GadgetCoreAPI.Frozen=true;
            if(blocked=="disconnect") pad.Clear();
            if(blocked=="remote") MenuScript.player.view.isMine=false;
            if(blocked=="scene") Application.loadedLevel++;
            if(blocked=="player") MenuScript.player=new GameObject("replacement");
            Press(5); Check(game.PageCalls==0 && InventoryItemLifecycle.DropCalls==0 && game.holdingItem.q==4,
                "blocked paging/drop in " + blocked);
            GadgetCore.API.GadgetCoreAPI.Frozen=false; MenuScript.player.view.isMine=true;
            if(blocked=="disconnect") pad.Connect();
            pad.BeginFrame(); Frame(); Check(game.PageCalls==0, "held blocked press cannot replay after "+blocked);
            Release(5); Press(5); Check(game.currentStoragePage==1, "fresh edge recovers after "+blocked);
        }
        Setup(true); Pages(11);
        pad.BeginFrame(); pad.Apply(new RawJoystickEvent {Type=1,Number=4,Value=1});
        pad.Apply(new RawJoystickEvent {Type=1,Number=5,Value=1}); Frame();
        Check(game.PageCalls==0 && InventoryItemLifecycle.DropCalls==0, "simultaneous bumpers cancel without drop");
        Setup(true, BindingConfig.DefaultText.Replace("MenuDrop = Joystick1Button5","MenuDrop = Joystick1Button12"));
        Pages(11); game.holdingItem=new Item{id=21,q=3}; Press(5);
        Check(game.currentStoragePage==1 && InventoryItemLifecycle.DropCalls==0, "RB pages despite remapped drop"); Release(5);
        Press(12); Check(InventoryItemLifecycle.DropCalls==1, "non-bumper remapped drop remains usable in storage");
        Setup(true, BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button4"));
        Pages(11); Press(4);
        Check(game.currentStoragePage==11 && !Left, "bumper paging takes priority over mapped primary");
    }
}
