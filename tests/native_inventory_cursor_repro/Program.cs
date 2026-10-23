using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks, failures;

    private static void Check(bool condition, string description)
    {
        checks++;
        Console.WriteLine((condition ? "PASS " : "FAIL ") + description);
        if (!condition) failures++;
    }

    private static ActionCursor Cursor(string name)
    {
        var cursor = new ActionCursor { z = -3 };
        cursor.gameObject.name = name;
        cursor.Awake();
        return cursor;
    }

    private static void NativeUpdate(ActionCursor cursor)
    {
        if (ControllerVisibleCursor.Prefix(cursor)) cursor.Update();
    }

    private static bool AtMouse(ActionCursor cursor)
    {
        return cursor.transform.position.x == 25 && cursor.transform.position.y == 15 &&
            cursor.transform.position.z == -3;
    }

    private static void Main()
    {
        bool owns = false;
        ControllerInputOwnership.Configure(() => owns);
        foreach (string name in new[] { "holdingItem", "hoverItem" })
        {
            Menuu.usingController = true;
            ActionCursor cursor = Cursor(name);
            NativeUpdate(cursor);
            Check(AtMouse(cursor), name + " uses native mouse coordinates despite stale native controller flag");
        }

        var game = new GameScript();
        var trasher = new GameObject("trasher");
        var emblem = new GameObject("emblem");
        var prism = new GameObject("prism");
        game.cursorTrasher = trasher;
        game.cursorEmblemForge = emblem;
        game.cursorPrism = prism;
        var mesh = new Renderer();
        var particles = new Renderer();
        var hidden = new Renderer { enabled = false };
        var glow = new Light();
        var hiddenLight = new Light { enabled = false };
        var heldSibling = new Renderer();
        game.holdingItemObj = new GameObject("holdingItem");
        game.holdingItemObj.renderers.Add(heldSibling);
        trasher.renderers.AddRange(new[] { mesh, particles, hidden });
        trasher.lights.AddRange(new[] { glow, hiddenLight });
        emblem.activeSelf = prism.activeSelf = false;

        InventoryNativeCursor.Update(game);
        Check(mesh.enabled && particles.enabled && !hidden.enabled && glow.enabled,
            "mouse ownership preserves native station visuals");
        owns = true;
        InventoryNativeCursor.Update(game);
        Check(!mesh.enabled && !particles.enabled && !hidden.enabled,
            "controller hides native mesh and particle renderers");
        Check(!glow.enabled && !hiddenLight.enabled, "controller hides native station point lights");
        Check(trasher.activeSelf && !emblem.activeSelf && !prism.activeSelf,
            "render suppression preserves native root activation");
        Check(heldSibling.enabled, "ordinary held-item sibling renderer remains visible");

        // Handoff after the controller update, before a second GameScript update.
        owns = false;
        Menuu.usingController = true;
        ActionCursor held = Cursor("holdingItem");
        NativeUpdate(held);
        Check(mesh.enabled && particles.enabled && !hidden.enabled && glow.enabled && !hiddenLight.enabled,
            "same-frame mouse cursor prefix restores captured renderer and light states");
        Check(AtMouse(held), "same-frame active-mode handoff uses native mouse coordinates");
        Menuu.usingController = true;
        InventoryNativeCursor.Update(game);
        Check(!Menuu.usingController, "UI update clears residual native flag without combat aim ownership");
        GameScript.inventoryOpen = false;
        Menuu.usingController = true;
        InventoryNativeCursor.Update(game);
        Check(!Menuu.usingController, "closed UI handoff also clears residual native controller flag");
        GameScript.inventoryOpen = true;

        var emblemMesh = new Renderer();
        var prismParticles = new Renderer();
        emblem.renderers.Add(emblemMesh);
        prism.renderers.Add(prismParticles);
        owns = true;
        foreach (GameObject root in new[] { trasher, emblem, prism })
        {
            trasher.activeSelf = emblem.activeSelf = prism.activeSelf = false;
            root.activeSelf = true;
            InventoryNativeCursor.Update(game);
            Renderer active = root == trasher ? mesh : root == emblem ? emblemMesh : prismParticles;
            Check(!active.enabled, root.name + " active native effect is suppressed for controller");
            if (root != trasher) Check(mesh.enabled && glow.enabled, "station switch restores former trasher visuals");
        }
        Check(emblemMesh.enabled, "switching emblem to prism restores emblem rendering");
        prism.activeSelf = false;
        InventoryNativeCursor.Update(game);
        Check(prismParticles.enabled && !prism.activeSelf, "station close restores rendering without reopening root");

        prism.activeSelf = true;
        InventoryNativeCursor.Update(game);
        GameScript.inventoryOpen = false;
        InventoryNativeCursor.Update(game);
        Check(prismParticles.enabled, "closing inventory restores active native effect rendering");
        GameScript.inventoryOpen = true;
        InventoryNativeCursor.Update(game);
        InventoryNativeCursor.Reset();
        Check(prismParticles.enabled && !hidden.enabled && !hiddenLight.enabled,
            "unload restores original visibility without enabling originally disabled components");
        Check(prism.activeSelf, "unload does not change native conversion root activation");
        owns = true;
        Menuu.usingController = false;
        NativeUpdate(Cursor("holdingItem"));
        Check(!Menuu.usingController, "controller inventory prefix never forces legacy global ownership true");

        Console.WriteLine(checks + " native inventory cursor checks; " + failures + " failures");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
