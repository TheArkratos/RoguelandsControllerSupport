using System;
using ControllerSupport;
using UnityEngine;

// Compile the production selection code; observe only the corner renderer.
// Native GameScript's private holdingItem is represented by the test stub.
internal static class Program
{
    private static int failures, checks;
    private static GameScript game;
    private static Collider inventorySlot, chestSlot, hotkeySlot, stat, page;
    private static JoystickInputState pad;
    private static bool controllerSource;
    private static readonly Vector3 NativeScale = new Vector3(1.3f, 0.9f, 1f);

    private static Collider Add(GameObject root, string name, string tag,
        int x, int y, int layer = 16)
    {
        var obj = new GameObject(name) { tag = tag, layer = layer };
        root.transform.Add(obj.transform);
        return new Collider(obj) { bounds = new Bounds {
            center = new Vector3(x, y, 1), size = new Vector3(1, 1, 1) } };
    }

    private static void Expect(string label, bool success)
    {
        checks++;
        Console.WriteLine((success ? "PASS " : "FAIL ") + label);
        if (!success) failures++;
    }

    private static void Frame(Collider hover)
    {
        Physics.Hit = hover;
        Input.mousePosition = new Vector3(Input.mousePosition.x + 2, 0);
        Time.frameCount++;
        ControllerMenuSelection.Update(game);
        ControllerMenuSelection.EndNativeUpdate();
    }

    private static void StableFrame()
    {
        Time.frameCount++;
        ControllerMenuSelection.Update(game);
        ControllerMenuSelection.EndNativeUpdate();
    }

    private static bool ScaleIs(Vector3 expected)
    {
        Vector3 actual = game.holdingItemObj.transform.localScale;
        return actual.x == expected.x && actual.y == expected.y && actual.z == expected.z;
    }

    private static void Setup()
    {
        ControllerMenuSelection.Reset();
        SelectionCornerRenderer.Latest = null;
        SelectionCornerRenderer.CreateCount = 0;
        game = new GameScript {
            inventoryMain = new GameObject("inventory"),
            inventoryBar = new GameObject("bar"),
            menuStorage = new GameObject("storage"),
            holdingItemObj = new GameObject("held"),
            holdingItemIcon = new GameObject("held icon") };
        game.holdingItemObj.transform.localScale = NativeScale;
        game.holdingItemIcon.renderer = new Renderer { bounds = new Bounds {
            size = new Vector3(0.8f, 0.6f, 0f) } };
        inventorySlot = Add(game.inventoryMain, "6", "Untagged", 1, 0);
        chestSlot = Add(game.menuStorage, "4", "storage", 10, 0);
        hotkeySlot = Add(game.inventoryBar, "0", "Untagged", 1, -8);
        stat = Add(game.inventoryMain, "1", "stat", 1, 2);
        page = Add(game.menuStorage, "page0", "Untagged", 10, 2, 0);
        game.storageButton[0] = page.gameObject;
        pad = new JoystickInputState();
        Input.mousePosition = new Vector3(0, 0);
        Input.left = Input.right = false;
        GameScript.inventoryOpen = true;
        GameScript.pausing = false;
        controllerSource = false;
        ControllerMenuSelection.Configure(() => pad, BindingConfig.Parse(
            BindingConfig.DefaultText.Split('\n')), message => throw new Exception(message),
            () => controllerSource);
    }

    private static int Main()
    {
        Setup();
        Frame(inventorySlot);
        var renderer = SelectionCornerRenderer.Latest;
        Expect("ordinary selection draws corners", renderer != null &&
            renderer.ShowTarget == inventorySlot);
        Expect("empty hand has no icon", renderer != null && renderer.HeldIconId == 0);

        game.SetHeldItem(21);
        Frame(inventorySlot);
        Expect("mouse-owned held item stays on native cursor",
            renderer.HeldIconId == 0 && ScaleIs(NativeScale));
        controllerSource = true;
        StableFrame();
        Expect("controller-owned pickup shows held item's own icon", renderer.HeldIconId == 21);
        Expect("controller-owned pickup hides native held graphic", ScaleIs(Vector3.zero));
        Expect("corner receives native icon dimensions for 75% rendering",
            renderer.HeldIconWidth == 0.8f && renderer.HeldIconHeight == 0.6f);
        Frame(chestSlot);
        StableFrame();
        Expect("held icon follows selection into chest", renderer.ShowTarget == chestSlot &&
            renderer.HeldIconId == 21);
        int creationCount = SelectionCornerRenderer.CreateCount;
        for (int i = 0; i < 10; i++) Frame(chestSlot);
        Expect("steady frames reuse corner renderer", SelectionCornerRenderer.CreateCount == creationCount);

        game.SetHeldItem(53);
        StableFrame();
        Expect("swap changes icon identity", renderer.HeldIconId == 53);
        game.SetHeldItem(0);
        StableFrame();
        Expect("placing item hides icon", renderer.HeldIconId == 0);
        Expect("placing item restores native held graphic exactly", ScaleIs(NativeScale));

        game.SetHeldItem(21);
        StableFrame();
        controllerSource = false;
        StableFrame();
        Expect("switching to mouse input hides controller corner icon",
            renderer.HeldIconId == 0);
        Expect("switching to mouse input restores exact native scale", ScaleIs(NativeScale));
        controllerSource = true;
        StableFrame();
        Expect("switching back to controller returns held icon to corner",
            renderer.HeldIconId == 21 && ScaleIs(Vector3.zero));

        Frame(stat);
        StableFrame();
        Expect("stat selection keeps held icon visible over selection",
            renderer.ShowTarget == stat && renderer.HeldIconId == 21);
        Frame(page);
        StableFrame();
        Expect("page selection keeps held icon after renderer rebuild",
            SelectionCornerRenderer.Latest != null &&
            SelectionCornerRenderer.Latest.ShowTarget == page &&
            SelectionCornerRenderer.Latest.HeldIconId == 21);
        Frame(chestSlot);
        StableFrame();
        Expect("returning to actionable cell shows held icon",
            SelectionCornerRenderer.Latest != null && SelectionCornerRenderer.Latest.HeldIconId == 21);
        Frame(hotkeySlot);
        StableFrame();
        Expect("held icon follows selection onto hotkey slot",
            SelectionCornerRenderer.Latest != null &&
            SelectionCornerRenderer.Latest.ShowTarget == hotkeySlot &&
            SelectionCornerRenderer.Latest.HeldIconId == 21);

        game.menuStorage.activeInHierarchy = false;
        Frame(chestSlot);
        Expect("closing chest hides held icon",
            SelectionCornerRenderer.Latest == null ||
            SelectionCornerRenderer.Latest.HeldIconId == 0);
        Expect("closing chest restores native held graphic", ScaleIs(NativeScale));

        Console.WriteLine(checks + " checks; " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
