using System;
using ControllerSupport;
using UnityEngine;

// Replays the two source-backed branches in GameScript.Update at ~5404 and
// ~5957: click in inventory selects an item; a click over layer 17 drops it.
// The input reads are the production transpiler replacements, not fake clicks.
internal static class Program
{
    private static GameScript game;
    private static JoystickInputState pad;
    private static Collider slot;
    private static bool holdingItem;
    private static int drops;

    private static void PrepareScene()
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
        slot = new Collider(target) { bounds = new Bounds {
            center = new Vector3(0, 0, 1), size = new Vector3(1, 1, 1) } };
        Physics.InventorySlot = slot;
        Physics.DropZone = new Collider(new GameObject("drop zone") { layer = 17 });
        Input.mousePosition = new Vector3(100, 0, 0); // stale physical cursor over drop zone
        Input.left = Input.right = false;
        GameScript.inventoryOpen = true;
        GameScript.pausing = false;
        pad = new JoystickInputState();
        holdingItem = false;
        drops = 0;
        ControllerMenuSelection.Configure(() => pad,
            BindingConfig.Parse(BindingConfig.DefaultText.Split('\n')),
            message => throw new Exception(message));
        Frame(false, false);
        Frame(false, false); // opening frame then release arm A
    }

    private static void NativeStorageClick()
    {
        // Exact GameScript.Update dispatch shape, including the second raycast
        // and layer-17 DropItem path. No item array methods are stubbed.
        if (!ControllerFrontEndMenus.MouseButtonDown(0) || !GameScript.inventoryOpen)
            return;
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(ControllerFrontEndMenus.MousePosition());
        if (!Physics.Raycast(ray, out hit, 7f)) return;
        if (hit.collider.gameObject.layer == 16)
        {
            if (!holdingItem) holdingItem = true; // SelectItem(slotID)
            else holdingItem = false;              // PlaceItem(slotID)
        }
        else if (hit.collider.gameObject.layer == 17 && holdingItem)
        {
            drops++;
            holdingItem = false;                    // DropItem()
        }
    }

    private static void Frame(bool a, bool physicalLeft)
    {
        pad.Held = pad.Pressed = a;
        Input.left = physicalLeft;
        Time.frameCount++;
        ControllerMenuSelection.Prepare(game);
        NativeStorageClick();
        ControllerMenuSelection.EndNativeUpdate();
    }

    private static int Main()
    {
        PrepareScene();
        Frame(true, false);
        if (!holdingItem || drops != 0)
        {
            Console.WriteLine("SETUP FAIL: first A did not pick up the inventory item");
            return 2;
        }
        Frame(false, false);
        Frame(true, false); // controller-only placement ignores stale physical drop-zone position
        if (drops != 0 || holdingItem)
        {
            Console.WriteLine("FAIL: second A dropped the held item into the level " +
                "instead of placing it in the selected slot (drops=" + drops + ")");
            return 1;
        }
        PrepareScene();
        Frame(true, false);
        Frame(false, false);
        Frame(false, true); // real mouse-only click at the drop zone remains native
        if (drops != 1 || holdingItem)
        {
            Console.WriteLine("FAIL: physical left click alone no longer drops at level cursor");
            return 1;
        }
        PrepareScene();
        Frame(true, false);
        Frame(false, false);
        Frame(true, true); // physical click explicitly takes ownership, even with A
        if (drops != 1 || holdingItem)
        {
            Console.WriteLine("FAIL: simultaneous physical click did not retain native mouse behavior");
            return 1;
        }
        Console.WriteLine("PASS: simultaneous physical click retains native mouse ownership");
        Console.WriteLine("PASS: A placed held storage item without dropping it");
        Console.WriteLine("PASS: physical mouse-only drop remains native");
        return 0;
    }
}
