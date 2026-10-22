using System;
using ControllerSupport;
using UnityEngine;

// Minimal replay of the captured Phase 3 inventory hierarchy. This compiles
// the real navigator; only Unity input, raycast and scene objects are stubs.
internal static class Program
{
    private static int failures;
    private static int mouseStep;

    private static Collider Add(GameObject root, string name, string tag, float x, float y)
    {
        var item = new GameObject(name) { tag = tag };
        root.transform.Add(item.transform);
        return new Collider(item)
        {
            bounds = new Bounds { center = new Vector3(x, y), size = new Vector3(1f, 1f) }
        };
    }

    private static void Hover(InventorySelectionNavigator navigator, GameScript game, Collider hit)
    {
        Physics.Hit = hit;
        Input.mousePosition = new Vector3(++mouseStep, 0f);
        navigator.Update(game, new JoystickInputState());
    }

    private static void Step(InventorySelectionNavigator navigator, GameScript game,
        float x, float y)
    {
        Physics.Hit = null;
        navigator.Update(game, new JoystickInputState { AimX = x, AimY = y });
    }

    private static void Expect(string label, Collider actual, Collider expected)
    {
        if (actual == expected) Console.WriteLine("PASS " + label);
        else
        {
            failures++;
            Console.WriteLine("FAIL " + label + ": selected=" +
                (actual == null ? "none" : actual.gameObject.name + "/" + actual.gameObject.tag) +
                ", expected=" + expected.gameObject.name + "/" + expected.gameObject.tag);
        }
    }

    private static void ReproEquippedRowNavigation()
    {
        var root = new GameObject("inventoryMain");
        var game = new GameScript { inventoryMain = root };
        // Collider centers from the preserved [DEBUG-P3GEOM] run, in screen
        // pixels. Slot 11 is the ordinary grid row at y=555.0, slot 41 is
        // the equipped-item row at y=655.1, and Faith is above it at y=755.3.
        // Slot 35's y=155 is inferred from the regular 100-pixel grid spacing;
        // its exact position is immaterial to the numbered-grid transition.
        var inventory = Add(root, "11", "Untagged", 850.3f, 555.0f);
        var equipped = Add(root, "41", "Untagged", 850.3f, 655.1f);
        var faith = Add(root, "6", "stat", 802.4f, 755.3f);
        Add(root, "35", "Untagged", 850.3f, 155f);
        var navigator = new InventorySelectionNavigator();

        Hover(navigator, game, inventory);
        Step(navigator, game, 0f, -1f);
        Expect("up from inventory 11 reaches equipped 41", navigator.SelectedCollider, equipped);

        Hover(navigator, game, faith);
        Step(navigator, game, 0f, 1f);
        Expect("down from Faith reaches equipped 41", navigator.SelectedCollider, equipped);
        Step(navigator, game, 0f, 0f);
        Step(navigator, game, 0f, 1f);
        Expect("down from equipped 41 enters inventory 11", navigator.SelectedCollider, inventory);

        Hover(navigator, game, equipped);
        Step(navigator, game, 0f, -1f);
        Expect("up from equipped 41 does not jump to inventory 35", navigator.SelectedCollider, faith);
    }

    private static int Main()
    {
        var root = new GameObject("inventoryMain");
        var bar = new GameObject("inventoryBar");
        var game = new GameScript { inventoryMain = root, inventoryBar = bar };
        // Captured trace distinguishes inventoryMain/6{tag=stat} from the
        // grid's inventoryMain/6{tag=Untagged}. Native Transform.Find("6")
        // resolves only one of them, so both must exist in this replay.
        var stat4 = Add(root, "4", "stat", 0f, 2f);
        var grid6 = Add(root, "6", "Untagged", 1f, 0f);
        var faith = Add(root, "6", "stat", 2f, 2f);
        var grid7 = Add(root, "7", "Untagged", 2f, 0f);
        var grid12 = Add(root, "12", "Untagged", 1f, -1f);
        var droid = Add(root, "42", "Untagged", 3f, 0f);
        var hotkey = Add(bar, "0", "Untagged", -2f, 0f);
        var main = new Collider[45];
        main[3] = stat4; main[5] = faith;
        main[6] = grid6; main[7] = grid7; main[12] = grid12;
        main[42] = droid;
        for (int i = 0; i < 6; i++)
            if (main[i] == null)
                main[i] = Add(root, (i + 1).ToString(), "stat", 100f + i, 100f);
        for (int i = 6; i < main.Length; i++)
            if (main[i] == null)
                main[i] = Add(root, i.ToString(), "Untagged", 100f + i, 0f);
        var navigator = new InventorySelectionNavigator();

        Hover(navigator, game, stat4);
        Expect("ordinary stat hover", navigator.SelectedCollider, stat4);
        Hover(navigator, game, faith);
        Expect("Faith hover selects its own stat collider, not prior stat", navigator.SelectedCollider, faith);
        Hover(navigator, game, grid6);
        Expect("separate grid slot 6 remains selectable", navigator.SelectedCollider, grid6);
        Step(navigator, game, 1f, 0f);
        Expect("grid slot 6 right goes to 7", navigator.SelectedCollider, grid7);
        Hover(navigator, game, grid6);
        Step(navigator, game, 0f, 1f);
        Expect("grid slot 6 down goes to 12", navigator.SelectedCollider, grid12);
        Hover(navigator, game, faith);
        Step(navigator, game, 0f, 1f);
        Expect("Faith down enters grid", navigator.SelectedCollider, grid7);
        Hover(navigator, game, droid);
        Expect("equipped droid remains selectable", navigator.SelectedCollider, droid);
        Hover(navigator, game, hotkey);
        Expect("hotkey remains selectable", navigator.SelectedCollider, hotkey);
        for (int i = 0; i < main.Length; i++)
        {
            Hover(navigator, game, main[i]);
            Expect("all main slots hoverable: " + i,
                navigator.SelectedCollider, main[i]);
        }

        ReproEquippedRowNavigation();

        Console.WriteLine("RESULT " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
