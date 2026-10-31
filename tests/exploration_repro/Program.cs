using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int count;
    private static void Equal(int expected, int actual)
    {
        count++;
        if (expected != actual) throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Check(bool condition, string message)
    {
        count++;
        if (!condition) throw new Exception(message);
    }

    private static void Point(float expectedX)
    {
        UnityEngine.Vector3 point;
        count++;
        if (!ControllerExploration.TryWorldPoint(out point) || point.x != expectedX)
            throw new Exception("Expected controller target x=" + expectedX);
    }

    private static void NoPoint()
    {
        UnityEngine.Vector3 point;
        count++;
        if (ControllerExploration.TryWorldPoint(out point))
            throw new Exception("Expected no controller target");
    }

    private static void Main()
    {
        Vector2 origin = new Vector2(0, 0), east = new Vector2(1, 0);
        // Alignment wins over proximity; equal angles use proximity.
        Vector2[] points = { new Vector2(8, 1), new Vector2(14, 0),
            new Vector2(5, 6), new Vector2(-2, 0), new Vector2(19, 0) };
        Equal(1, ExplorationTargetSelector.Choose(origin, east, points, -1, false));
        Equal(1, ExplorationTargetSelector.Choose(origin, east, points, 1, true));
        Equal(-1, ExplorationTargetSelector.Choose(origin, east, points, -1, true));
        Equal(0, ExplorationTargetSelector.Choose(origin, new Vector2(1, 0.11f),
            points, 0, false)); // existing target resists small stick drift
        Equal(-1, ExplorationTargetSelector.Choose(origin, new Vector2(0, -1),
            points, -1, false));
        Equal(-1, ExplorationTargetSelector.Choose(origin, east,
            new[] { new Vector2(19, 0) }, 0, true));

        GameScript game = new GameScript();
        MenuScript.player = new GameObject();
        ObjectScript first = new ObjectScript(), second = new ObjectScript();
        first.transform.position = new Vector3(8, 0, 0);
        second.transform.position = new Vector3(12, 0, 0);
        ExplorationFixture.Objects = new[] { first, second };
        JoystickInputState input = new JoystickInputState { AimX = 1f, AimY = 0f };
        ControllerExploration.Configure(() => input);
        ControllerExploration.UpdateAndDispatch(game);
        Point(8);

        game.SetDroidItem(0);
        ControllerGameplayInput.Pressed = true;
        ControllerExploration.UpdateAndDispatch(game);
        Point(8);
        Equal(0, game.activateCalls);
        game.SetDroidItem(1000);

        // A failed vanilla activation must leave the resource selectable.
        game.activateSucceeds = false;
        ControllerGameplayInput.Pressed = true;
        ControllerExploration.UpdateAndDispatch(game);
        Point(8);

        Equal(1, game.activateCalls);

        // A successful dispatch immediately advances to the next target.
        game.activateSucceeds = true;
        ControllerExploration.UpdateAndDispatch(game);
        Point(12);
        Equal(2, game.activateCalls);
        ControllerGameplayInput.Pressed = false;

        // Temporary menu and mode changes must preserve dispatch history.
        GameScript.inventoryOpen = true;
        ControllerExploration.UpdateAndDispatch(game);
        GameScript.inventoryOpen = false;
        ControllerExploration.UpdateAndDispatch(game);
        Point(12);
        GameScript.combatMode = true;
        ControllerExploration.UpdateAndDispatch(game);
        GameScript.combatMode = false;
        ControllerExploration.UpdateAndDispatch(game);
        Point(12);

        // Dispatching the last target leaves no cursor selection.
        game.activated[0] = false;
        ControllerGameplayInput.Pressed = true;
        ControllerExploration.UpdateAndDispatch(game);
        NoPoint();
        ControllerGameplayInput.Pressed = false;

        game.SetInShip(true);
        ControllerExploration.UpdateAndDispatch(game);
        game.SetInShip(false);
        ControllerExploration.UpdateAndDispatch(game);
        Point(8);

        game.activated[0] = false;
        ControllerGameplayInput.Pressed = true;
        ControllerExploration.UpdateAndDispatch(game);
        Point(12);
        ControllerGameplayInput.Pressed = false;

        // Leaving and re-entering a level re-enables its resource objects.
        GameScript.inInstance = false;
        ControllerExploration.UpdateAndDispatch(game);
        GameScript.inInstance = true;
        ControllerExploration.UpdateAndDispatch(game);
        Point(8);

        // A centered stick clears selection after dispatch rather than
        // retaining the just-dispatched object.
        input.AimX = 0f;
        game.activated[0] = false;
        ControllerGameplayInput.Pressed = true;
        ControllerExploration.UpdateAndDispatch(game);
        NoPoint();
        ControllerGameplayInput.Pressed = false;

        ControllerExploration.Reset();
        input.AimX = 1f;
        ControllerExploration.Configure(() => input);
        ControllerExploration.UpdateAndDispatch(game);
        Point(8);

        // Native GameScript.CameraZoom uses orthographic half-heights 13/15
        // (or 15/17). At 15 and a 16:9 viewport, x=20 is visibly on-screen
        // while x=30 is not. Exercise the real scan/update path, not only the
        // selector: the old 18-unit radius rejects the visible resource.
        Camera.main = new Camera();
        Camera.main.transform.position = new Vector3(0, 0, -10);
        Vector3 visiblePoint = new Vector3(20, 0, 0);
        Vector3 offscreenPoint = new Vector3(30, 0, 0);
        Vector3 visibleViewport = Camera.main.WorldToViewportPoint(visiblePoint);
        Vector3 offscreenViewport = Camera.main.WorldToViewportPoint(offscreenPoint);
        Check(visibleViewport.x > 0f && visibleViewport.x < 1f,
            "fixture error: x=20 must be inside camera viewport");
        Check(offscreenViewport.x > 1f,
            "fixture error: x=30 must be beyond right screen edge");
        ControllerExploration.Reset();
        GameScript viewportGame = new GameScript();
        ObjectScript visible = new ObjectScript(), offscreen = new ObjectScript();
        visible.transform.position = visiblePoint;
        offscreen.transform.position = offscreenPoint;
        ExplorationFixture.Objects = new[] { visible, offscreen };
        ControllerExploration.Configure(() => input);
        ControllerExploration.UpdateAndDispatch(viewportGame);
        Point(20);
        visible.exists = false;
        ControllerExploration.UpdateAndDispatch(viewportGame);
        NoPoint();

        // Camera motion changes eligibility without changing player distance.
        Camera.main.transform.position = new Vector3(25, 0, -10);
        ObjectScript movingView = new ObjectScript();
        movingView.transform.position = new Vector3(45, 0, 0);
        ExplorationFixture.Objects = new[] { movingView };
        ControllerExploration.Reset();
        ControllerExploration.Configure(() => input);
        ControllerExploration.UpdateAndDispatch(viewportGame);
        Point(45);
        Camera.main.transform.position = new Vector3(80, 0, -10);
        input.AimX = 0f;
        ControllerExploration.UpdateAndDispatch(viewportGame);
        NoPoint(); // centered stick must not retain an offscreen target

        // At the screen edge, include in-frame objects and reject adjacent
        // offscreen ones, even when the latter are better aligned.
        input.AimX = 1f;
        Camera.main.transform.position = new Vector3(60, 0, -10);
        ObjectScript atEdge = new ObjectScript(), pastEdge = new ObjectScript();
        atEdge.transform.position = new Vector3(86.5f, 0, 0);
        pastEdge.transform.position = new Vector3(87, 0, 0);
        ExplorationFixture.Objects = new[] { atEdge, pastEdge };
        ControllerExploration.Reset();
        ControllerExploration.Configure(() => input);
        ControllerExploration.UpdateAndDispatch(viewportGame);
        Point(86.5f);

        // Without Camera.main, keep the previous 18-unit safety fallback.
        Camera.main = null;
        ObjectScript fallback = new ObjectScript(), tooFar = new ObjectScript();
        fallback.transform.position = new Vector3(17, 0, 0);
        tooFar.transform.position = new Vector3(19, 0, 0);
        ExplorationFixture.Objects = new[] { fallback, tooFar };
        ControllerExploration.Reset();
        ControllerExploration.Configure(() => input);
        ControllerExploration.UpdateAndDispatch(viewportGame);
        Point(17);
        fallback.exists = false;
        ControllerExploration.UpdateAndDispatch(viewportGame);
        NoPoint();
        fallback.exists=true;
        viewportGame.actionCursor.SetActive(false);
        bool owns=true;ControllerInputOwnership.Configure(()=>owns);
        ControllerExploration.UpdateAndDispatch(viewportGame);Point(17);
        owns=false;NoPoint();
        ControllerGameplayInput.Pressed=true;
        int activations=viewportGame.activateCalls;
        ControllerExploration.UpdateAndDispatch(viewportGame);
        NoPoint();
        Check(viewportGame.activateCalls==activations,"keyboard ownership cannot dispatch controller droid");
        Check(!viewportGame.actionCursor.activeSelf,"keyboard handoff clears controller resource cursor");
        ControllerGameplayInput.Pressed=false;
        owns=true;NoPoint(); // no stale target reappears before a new controller update
        ControllerExploration.UpdateAndDispatch(viewportGame);Point(17);
        ControllerInputOwnership.Configure(()=>true);
        bool story=true;ControllerInputOwnership.Configure(()=>true,()=>story);
        ControllerGameplayInput.Pressed=true;activations=viewportGame.activateCalls;
        ControllerExploration.UpdateAndDispatch(viewportGame);NoPoint();
        Check(viewportGame.activateCalls==activations,"story modal blocks exploration dispatch outside inventory/talking");
        ControllerGameplayInput.Pressed=false;story=false;ControllerExploration.UpdateAndDispatch(viewportGame);Point(17);
        ControllerInputOwnership.Configure(()=>true);
        Console.WriteLine("Passed " + count + " exploration selection checks.");
    }
}
