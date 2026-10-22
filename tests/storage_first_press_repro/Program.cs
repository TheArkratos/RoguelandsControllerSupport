using System;
using ControllerSupport;
using UnityEngine;

// One manually launched, raw-gamepad A press. The original cursor is over a
// layer-17 level/drop collider; no physical/Steam mouse click is supplied.
// This mirrors GameScript.Update's single storage-click dispatch at 5404-5963.
internal static class Program
{
    private static GameScript game;
    private static JoystickInputState pad;
    private static Collider inventorySlot;
    private static int picks, drops, dispatches;
    private static bool holdingItem;

    private static void Setup()
    {
        ControllerFrontEndMenus.Reset();
        ControllerMenuSelection.Reset();
        game = new GameScript {
            inventoryMain = new GameObject("inventory"),
            inventoryBar = new GameObject("bar"),
            menuStorage = new GameObject("storage"),
            holdingItemObj = new GameObject("held") };
        var target = new GameObject("6") { layer = 16 };
        game.inventoryMain.transform.Add(target.transform);
        inventorySlot = new Collider(target) { bounds = new Bounds {
            center = new Vector3(0, 0, 1), size = new Vector3(1, 1, 1) } };
        Physics.InventorySlot = inventorySlot;
        Physics.DropZone = new Collider(new GameObject("drop zone") { layer = 17 });
        Input.mousePosition = new Vector3(100, 0, 0);
        Input.left = Input.right = false;
        GameScript.inventoryOpen = true;
        GameScript.pausing = false;
        pad = new JoystickInputState();
        ControllerMenuSelection.Configure(() => pad,
            BindingConfig.Parse(BindingConfig.DefaultText.Split('\n')),
            warning => throw new Exception(warning));
    }

    private static void NativeStorageClick()
    {
        // Both expressions are the real production wrappers installed by
        // FrontEndMenuTranspiler.Replace for GameScript.Update.
        if (!ControllerFrontEndMenus.MouseButtonDown(0) || !GameScript.inventoryOpen)
            return;
        dispatches++;
        RaycastHit hit;
        var ray = Camera.main.ScreenPointToRay(ControllerFrontEndMenus.MousePosition());
        if (!Physics.Raycast(ray, out hit, 7f)) return;
        if (hit.collider.gameObject.layer == 16)
        {
            if (!holdingItem) { picks++; holdingItem = true; } // SelectItem(6)
        }
        else if (hit.collider.gameObject.layer == 17 && holdingItem)
        {
            drops++; holdingItem = false;                     // DropItem()
        }
    }

    private static void Frame(bool a)
    {
        pad.Held = pad.Pressed = a;
        Time.frameCount++;
        // In storage, PauseControllerMenuPatch.Begin has no active menuRoot,
        // but its wrapper still delegates to ControllerMenuSelection.
        ControllerFrontEndMenus.Begin(game);
        ControllerMenuSelection.Prepare(game);
        NativeStorageClick();
        ControllerMenuSelection.EndNativeUpdate();
        ControllerFrontEndMenus.End();
    }

    private static int Main()
    {
        Setup();
        Frame(false); // opening frame
        Frame(false); // released, so first A can arm
        Frame(true);  // the user's FIRST A press; no second press or mouse click
        if (Input.left || Input.right || picks != 1 || drops != 0 ||
            dispatches != 1 || !holdingItem)
        {
            Console.WriteLine("FAIL: first A did not exclusively select the slot " +
                "(picks=" + picks + ", drops=" + drops + ", dispatches=" + dispatches + ")");
            return 1;
        }
        Console.WriteLine("PASS: first raw A selects once; no same-frame DropItem " +
            "(picks=1, drops=0, dispatches=1)");
        return 0;
    }
}
