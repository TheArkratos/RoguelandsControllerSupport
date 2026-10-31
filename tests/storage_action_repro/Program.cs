using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int failures, checks;
    private static GameScript game;
    private static JoystickInputState pad;
    private static Collider[] inventory, chest, hotkey;
    private static Collider page, stat;

    private static Collider Add(GameObject root, string name, string tag, int x, int y,
        int layer = 16)
    {
        var obj = new GameObject(name) { tag = tag, layer = layer };
        root.transform.Add(obj.transform);
        return new Collider(obj) { bounds = new Bounds {
            center = new Vector3(x, y, 1), size = new Vector3(1, 1, 1) } };
    }

    private static void NewScene()
    {
        ControllerMenuSelection.Reset();
        game = new GameScript {
            inventoryMain = new GameObject("inventory"),
            inventoryBar = new GameObject("bar"),
            menuStorage = new GameObject("storage"),
            holdingItemObj = new GameObject("held") };
        inventory = new Collider[45];
        chest = new Collider[36];
        hotkey = new Collider[6];
        stat = Add(game.inventoryMain, "1", "stat", -2, 2);
        for (int i = 6; i < 36; i++)
            inventory[i] = Add(game.inventoryMain, i.ToString(), "Untagged", i % 6, -i / 6);
        for (int i = 36; i < 45; i++)
            inventory[i] = Add(game.inventoryMain, i.ToString(), "Untagged", i - 36, 3);
        for (int i = 0; i < hotkey.Length; i++)
            hotkey[i] = Add(game.inventoryBar, i.ToString(), "Untagged", i, -10);
        for (int i = 0; i < 36; i++)
            chest[i] = Add(game.menuStorage, i.ToString(), "storage", i % 6 + 10, -i / 6);
        page = Add(game.menuStorage, "page0", "Untagged", 10, 2, 0);
        game.storageButton[0] = page.gameObject;
        pad = new JoystickInputState();
        Input.mousePosition = new Vector3(0, 0);
        Input.left = Input.right = false;
        Physics.Hit = inventory[6];
        Time.frameCount++;
        GameScript.inventoryOpen = true;
        GameScript.pausing = false;
        ControllerMenuSelection.Configure(() => pad, BindingConfig.Parse(
            BindingConfig.DefaultText.Split('\n')), message => throw new Exception(message));
        ControllerMenuSelection.Prepare(game);
        ControllerMenuSelection.EndNativeUpdate();
        NextFrame(); // opening frame cannot activate, subsequent release arms.
    }

    private static void NextFrame()
    {
        pad.Pressed = false;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        ControllerMenuSelection.EndNativeUpdate();
    }

    private static void Hover(Collider collider)
    {
        pad.Pressed = false;
        Physics.Hit = collider;
        Input.mousePosition = new Vector3(Input.mousePosition.x + 3, 0);
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        ControllerMenuSelection.EndNativeUpdate();
    }

    private static void Press(bool physical = false)
    {
        pad.Held = true;
        pad.Pressed = true;
        Input.left = physical;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
    }

    private static void Release()
    {
        ControllerMenuSelection.EndNativeUpdate();
        pad.Held = pad.Pressed = false;
        Input.left = Input.right = false;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        ControllerMenuSelection.EndNativeUpdate();
    }

    private static void Expect(string label, bool success)
    {
        checks++;
        if (!success) { failures++; Console.WriteLine("FAIL " + label); }
        else Console.WriteLine("PASS " + label);
    }

    private static void MainCases()
    {
        NewScene();
        Press();
        Expect("A picks inventory item via native left-click", ControllerMenuSelection.VirtualMouseButtonDown(0));
        Expect("virtual mouse points at selected inventory slot",
            ControllerMenuSelection.VirtualMousePosition().x == inventory[6].bounds.center.x);
        Expect("inventory press not extra row", ControllerMenuSelection.ExtraRowVirtualClickSlot(game) == -1);
        ControllerMenuSelection.Prepare(game);
        Expect("same frame remains one click", ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();
        Expect("release not another click", !ControllerMenuSelection.VirtualMouseButtonDown(0));
        Hover(chest[4]);
        Press();
        Expect("second A places via native left-click", ControllerMenuSelection.VirtualMouseButtonDown(0));
        Expect("virtual mouse points at selected chest slot",
            ControllerMenuSelection.VirtualMousePosition().x == chest[4].bounds.center.x);
        Release();

        Hover(chest[31]);
        Press();
        Expect("extra-row bridge uses exact visual slot 31",
            ControllerMenuSelection.ExtraRowVirtualClickSlot(game) == 31);
        Release();
        Hover(page);
        Press();
        Expect("page A activates through native left click", ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();
        Hover(stat);
        Press();
        Expect("stat A remains disabled", !ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();

        Hover(chest[2]);
        Press(true);
        Expect("simultaneous physical left click keeps physical mouse ownership",
            ControllerMenuSelection.VirtualMouseButtonDown(0) &&
            ControllerMenuSelection.VirtualMousePosition().x == Input.mousePosition.x);
        Release();

        Hover(chest[31]);
        Press(true);
        Expect("simultaneous physical click bypasses controller extra-row dispatch",
            ControllerMenuSelection.ExtraRowVirtualClickSlot(game) == -1 &&
            ControllerMenuSelection.VirtualMousePosition().x == Input.mousePosition.x);
        Release();

        Hover(chest[2]);
        Input.left = true;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        Expect("physical left click alone keeps actual cursor",
            ControllerMenuSelection.VirtualMouseButtonDown(0) &&
            ControllerMenuSelection.VirtualMousePosition().x == Input.mousePosition.x);
        Input.left = false;
        ControllerMenuSelection.EndNativeUpdate();
        Release();

        Hover(chest[2]);
        pad.Held = pad.Pressed = true;
        Input.right = true;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        Expect("physical right click wins over A",
            !ControllerMenuSelection.VirtualMouseButtonDown(0) &&
            ControllerMenuSelection.VirtualMouseButtonDown(1) &&
            ControllerMenuSelection.VirtualMousePosition().x == Input.mousePosition.x);
        Release();

        Hover(chest[3]);
        pad.Held = pad.Pressed = true;
        Input.mousePosition = new Vector3(Input.mousePosition.x + 3, 0);
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        Expect("same-frame mouse movement suppresses A click",
            !ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();

        Hover(chest[3]);
        pad.Held = pad.Pressed = true;
        Input.left = true;
        Input.mousePosition = new Vector3(Input.mousePosition.x + 3, 0);
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        Expect("mouse movement with simultaneous left click keeps actual cursor",
            ControllerMenuSelection.VirtualMouseButtonDown(0) &&
            ControllerMenuSelection.VirtualMousePosition().x == Input.mousePosition.x);
        Release();
    }

    private static void ArmingCases()
    {
        NewScene();
        game.menuStorage.activeInHierarchy = false;
        GameScript.inventoryOpen = false;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        ControllerMenuSelection.EndNativeUpdate();
        pad.Held = pad.Pressed = true;
        GameScript.inventoryOpen = true;
        game.menuStorage.activeInHierarchy = true;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        Expect("A held while opening cannot click", !ControllerMenuSelection.VirtualMouseButtonDown(0));
        ControllerMenuSelection.EndNativeUpdate();
        pad.Pressed = false;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        Expect("A still held cannot click", !ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();
        Press();
        Expect("A rearmed after release clicks", ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();
        game.menuStorage.activeInHierarchy = false;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        ControllerMenuSelection.EndNativeUpdate();
        Press();
        Expect("ordinary inventory retains primary action after chest closes", ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();
        GameScript.inventoryOpen = false;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        Expect("closed inventory cannot click", !ControllerMenuSelection.VirtualMouseButtonDown(0));
    }

    private static void HotkeyPlacementCases()
    {
        NewScene();
        game.SetHeldItem(21);
        for (int i = 0; i < hotkey.Length; i++)
        {
            // Mouse hover is deliberately used only to seed the production
            // navigator's selected hotkey; A must then use its virtual click.
            Hover(hotkey[i]);
            Press();
            Expect("A places held item via native left-click on hotkey " + i,
                ControllerMenuSelection.VirtualMouseButtonDown(0));
            Vector3 cursor = ControllerMenuSelection.VirtualMousePosition();
            Expect("virtual cursor targets hotkey " + i,
                cursor.x == hotkey[i].bounds.center.x &&
                cursor.y == hotkey[i].bounds.center.y &&
                cursor.z == hotkey[i].bounds.center.z);
            Expect("hotkey " + i + " does not route through chest extra-row bridge",
                ControllerMenuSelection.ExtraRowVirtualClickSlot(game) == -1);
            Release();
            Expect("hotkey " + i + " A release has no second click",
                !ControllerMenuSelection.VirtualMouseButtonDown(0));
        }


        // Start on the bottom inventory row and navigate to the bar using
        // the right stick, so this case does not depend on mouse hotkey hover.
        Hover(inventory[30]);
        pad.AimY = 1f;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        ControllerMenuSelection.EndNativeUpdate();
        pad.AimY = 0f;
        // The stub raycaster has one explicit hit rather than spatial lookup.
        Physics.Hit = hotkey[0];
        Press();
        Expect("stick-selected hotkey accepts A placement",
            ControllerMenuSelection.VirtualMouseButtonDown(0) &&
            ControllerMenuSelection.VirtualMousePosition().x == hotkey[0].bounds.center.x &&
            ControllerMenuSelection.VirtualMousePosition().y == hotkey[0].bounds.center.y);
        Release();
    }

    private static void EquipmentCases()
    {
        NewScene();
        game.menuStorage.activeInHierarchy = false;
        Hover(inventory[36]);
        Press();
        Expect("ordinary inventory equipment pickup routes native primary action",
            ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();
        game.SetHeldItem(21);
        Press();
        Expect("wrong equipment type is rejected without virtual click",
            !ControllerMenuSelection.VirtualMouseButtonDown(0) &&
            InventoryFeedback.LastReason == "Wrong item type" && game.holdingItem.id == 21);
        Release();
        game.SetHeldItem(500); // Registry fixture defines a weapon.
        Press();
        Expect("matching weapon target routes native equip action",
            ControllerMenuSelection.VirtualMouseButtonDown(0));
        Release();
        Hover(inventory[37]);
        Press();
        Expect("weapon cannot be routed into offhand target",
            !ControllerMenuSelection.VirtualMouseButtonDown(0) && game.holdingItem.id == 500);
        Release();
    }

    private static void HeldStorageCloseCases()
    {
        NewScene();
        game.SetHeldItem(21);
        Expect("open storage with held item reports drop hazard",
            ControllerMenuSelection.ClosingStorageWouldDropHeldItem(game));
        game.SetHeldItem(0);
        Expect("placed item removes native-close drop hazard",
            !ControllerMenuSelection.ClosingStorageWouldDropHeldItem(game));
        game.SetHeldItem(21);
        game.menuStorage.activeInHierarchy = false;
        Expect("held item outside storage does not block controller close",
            !ControllerMenuSelection.ClosingStorageWouldDropHeldItem(game));
        game.menuStorage.activeInHierarchy = true;
        GameScript.inventoryOpen = false;
        Expect("closed inventory does not block controller close",
            !ControllerMenuSelection.ClosingStorageWouldDropHeldItem(game));
    }

    private static void Bumpers(bool left, bool right)
    {
        ControllerMenuSelection.EndNativeUpdate();
        pad.PressedButtons[4] = left && !pad.HeldButtons[4];
        pad.PressedButtons[5] = right && !pad.HeldButtons[5];
        pad.HeldButtons[4] = left; pad.HeldButtons[5] = right;
        Time.frameCount++; ControllerMenuSelection.Prepare(game);
    }

    private static void Trigger(bool held)
    {
        ControllerMenuSelection.EndNativeUpdate();
        pad.RightTriggerPressed = held && !pad.RightTriggerHeld;
        pad.RightTriggerHeld = held;
        Time.frameCount++; ControllerMenuSelection.Prepare(game);
    }

    private static void StorageDropTriggerCases()
    {
        NewScene(); game.SetHeldItem(21);
        game.storageButton[1] = Add(game.menuStorage, "page1", "Untagged", 11, 2, 0).gameObject;
        game.storageButton[2] = Add(game.menuStorage, "page2", "Untagged", 12, 2, 0).gameObject;
        Trigger(true);
        Expect("RT makes storage held-stack drop reachable", InventoryItemLifecycle.DropCalls == 1);
        Expect("storage RT drop does not change page", game.currentStoragePage == 0);
        Expect("rejected RT drop preserves held item and UI", game.holdingItem.id == 21 && GameScript.inventoryOpen);
        Trigger(true); Expect("held RT cannot repeat drop", InventoryItemLifecycle.DropCalls == 1);
        Trigger(false); Trigger(true); Expect("release and fresh RT drops again", InventoryItemLifecycle.DropCalls == 2);
        Trigger(false); Bumpers(true, true);
        Expect("old bumper chord cannot drop", InventoryItemLifecycle.DropCalls == 2 && game.currentStoragePage == 0);
        Bumpers(false, false); Bumpers(false, true);
        Expect("single RB pages without drop", game.currentStoragePage == 1 && InventoryItemLifecycle.DropCalls == 2);

        NewScene(); Trigger(true);
        Expect("empty handed RT does not drop", InventoryItemLifecycle.DropCalls == 0);

        NewScene(); game.SetHeldItem(21);
        GameScript.inventoryOpen = false; Time.frameCount++; ControllerMenuSelection.Prepare(game);
        GameScript.inventoryOpen = true; Trigger(true); Trigger(true);
        Expect("held entry RT cannot drop", InventoryItemLifecycle.DropCalls == 0);
        Trigger(false); Trigger(true); Expect("entry RT rearms after release", InventoryItemLifecycle.DropCalls == 1);

        NewScene(); game.SetHeldItem(21); GadgetCore.API.GadgetCoreAPI.Frozen = true;
        Trigger(true); Expect("modal blocks RT drop", InventoryItemLifecycle.DropCalls == 0);
        GadgetCore.API.GadgetCoreAPI.Frozen = false; Trigger(true);
        Expect("modal dismissal cannot reuse held RT", InventoryItemLifecycle.DropCalls == 0);
        Trigger(false); Trigger(true); Expect("modal recovery permits fresh RT", InventoryItemLifecycle.DropCalls == 1);

        NewScene(); game.SetHeldItem(21); InventoryItemLifecycle.BlockMutations = true;
        Trigger(true); Expect("quarantine blocks RT", InventoryItemLifecycle.DropCalls == 0);
        InventoryItemLifecycle.BlockMutations = false; Trigger(true);
        Expect("quarantine release cannot replay held RT", InventoryItemLifecycle.DropCalls == 0);

        NewScene(); game.SetHeldItem(21); ControllerInputOwnership.Configure(() => false);
        Trigger(true); Expect("unowned RT cannot drop", InventoryItemLifecycle.DropCalls == 0);
        ControllerInputOwnership.Configure(() => true); Trigger(true);
        Expect("ownership handoff cannot replay held RT", InventoryItemLifecycle.DropCalls == 0);
        Trigger(false); Trigger(true); Expect("ownership handoff rearms on release", InventoryItemLifecycle.DropCalls == 1);

        NewScene(); game.SetHeldItem(21); Input.left = true; Trigger(true);
        Expect("physical click blocks RT", InventoryItemLifecycle.DropCalls == 0);
        Input.left = false; Trigger(true); Expect("physical click release cannot replay RT", InventoryItemLifecycle.DropCalls == 0);

        NewScene(); game.SetHeldItem(21); MenuScript.player.view.isMine = false; Trigger(true);
        Expect("nonlocal player cannot RT drop", InventoryItemLifecycle.DropCalls == 0);
        MenuScript.player.view.isMine = true;
        NewScene(); game.SetHeldItem(21); pad.Connected = false; Trigger(true);
        Expect("disconnected RT cannot drop", InventoryItemLifecycle.DropCalls == 0);
    }

    private static int Main()
    {
        StorageDropTriggerCases();
        MainCases();
        HotkeyPlacementCases();
        ArmingCases();
        EquipmentCases();
        HeldStorageCloseCases();
        Console.WriteLine(checks + " checks; " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
