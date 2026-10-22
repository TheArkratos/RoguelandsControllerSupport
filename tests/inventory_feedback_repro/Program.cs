using System;
using ControllerSupport;
using UnityEngine;

// This exercises the production navigator, not a duplicate selection algorithm.
// Test objects model the indexed native hierarchy; exact screen geometry is intentionally absent.
internal static class Program
{
    private static int failures;

    private static Collider Add(GameObject root, string name, float x, float y)
    {
        var item = new GameObject(name);
        root.transform.Add(item.transform);
        return new Collider(item) { bounds = new Bounds { center = new Vector3(x, y), size = new Vector3(1, 1) } };
    }

    private static void Check(string label, Collider actual, Collider expected)
    {
        if (actual == expected) Console.WriteLine("PASS " + label);
        else { failures++; Console.WriteLine("FAIL " + label + ": selected=" +
            (actual == null ? "none" : actual.gameObject.name) + ", expected=" + expected.gameObject.name); }
    }

    private static void Hover(InventorySelectionNavigator navigator, GameScript game,
        Collider item, float mouseX)
    {
        Physics.Hit = item;
        Input.mousePosition = new Vector3(mouseX, 1f);
        navigator.Update(game, new JoystickInputState());
    }

    private static int Main()
    {
        var root = new GameObject("inventoryMain");
        var bar = new GameObject("inventoryBar");
        var game = new GameScript { inventoryMain = root, inventoryBar = bar };
        var regular = Add(root, "6", 0, 0);
        var droid = Add(root, "42", 1, 0);
        var hotkeys = new Collider[6];
        for (int i = 0; i < hotkeys.Length; i++)
            hotkeys[i] = Add(bar, i.ToString(), -2, i);
        var hotkey = hotkeys[0];
        var navigator = new InventorySelectionNavigator();

        Hover(navigator, game, regular, 1);
        Check("ordinary inventory slot", navigator.SelectedCollider, regular);
        Hover(navigator, game, droid, 2);
        Check("equipped droid slot is selected", navigator.SelectedCollider, droid);
        for (int i = 0; i < hotkeys.Length; i++)
        {
            Hover(navigator, game, hotkeys[i], 3 + i);
            Check("hotkey " + i + " is selected", navigator.SelectedCollider, hotkeys[i]);
        }
        Hover(navigator, game, hotkey, 10);
        Physics.Hit = null;
        Input.mousePosition = new Vector3(10, 1f);
        navigator.Update(game, new JoystickInputState { AimX = 1f });
        Check("right stick leaves hotkey toward inventory", navigator.SelectedCollider, regular);
        navigator.Update(game, new JoystickInputState { AimX = -1f });
        Check("right stick enters hotkey from inventory", navigator.SelectedCollider, hotkey);
        GameScript.inventoryOpen = false;
        navigator.Update(game, new JoystickInputState { AimX = -1f });
        if (navigator.SelectedCollider == null) Console.WriteLine("PASS closed inventory clears selection");
        else { failures++; Console.WriteLine("FAIL closed inventory retains selection"); }
        Console.WriteLine("RESULT " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
