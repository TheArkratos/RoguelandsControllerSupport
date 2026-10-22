using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static void Main()
    {
        JoystickInputState input = new JoystickInputState();
        input.Connect();
        ControllerAim.Configure(() => input);

        // A full stick rotation is sampled across adjacent 60-Hz frames.
        input.Apply(new RawJoystickEvent { Type = 2, Number = 3, Value = 32767 });
        Time.frameCount = 1;
        Time.unscaledTime = 1f / 60f;
        Vector3 first;
        if (!ControllerAim.TryWorldPoint(out first)) throw new Exception("Aim was not acquired.");

        input.BeginFrame();
        input.Apply(new RawJoystickEvent { Type = 2, Number = 3, Value = 0 });
        input.Apply(new RawJoystickEvent { Type = 2, Number = 4, Value = -32768 });
        Time.frameCount = 2;
        Time.unscaledTime = 2f / 60f;
        Vector3 second;
        if (!ControllerAim.TryWorldPoint(out second)) throw new Exception("Aim was lost.");

        float degrees = (float)(Math.Atan2(second.y, second.x) - Math.Atan2(first.y, first.x)) *
            180f / (float)Math.PI;
        Console.WriteLine("Crosshair angle change in one 60-Hz frame: " + degrees.ToString("0.0") + " degrees");
        if (Math.Abs(degrees) > 30f)
            throw new Exception("Angular jump exceeds 30 degrees per frame.");

        for (int frame = 3; frame <= 9; frame++)
        {
            Time.frameCount = frame;
            Time.unscaledTime = frame / 60f;
            if (!ControllerAim.TryWorldPoint(out second)) throw new Exception("Aim was lost while rotating.");
        }
        if (Math.Abs(second.x) > 0.01f || Math.Abs(second.y - 14f) > 0.01f)
            throw new Exception("Crosshair never reached the new direction.");

        input.Apply(new RawJoystickEvent { Type = 2, Number = 4, Value = 0 });
        Time.frameCount = 10;
        Time.unscaledTime = 10f / 60f;
        if (!ControllerAim.TryWorldPoint(out second) || Math.Abs(second.y - 14f) > 0.01f)
            throw new Exception("Neutral stick did not retain last direction.");
        ControllerGameplayInput.HalfRadius = true;
        if (!ControllerAim.TryWorldPoint(out second) || Math.Abs(second.y - 7f) > 0.01f)
            throw new Exception("Half-radius aim did not update immediately.");
        Console.WriteLine("Neutral-direction hold and half-radius checks passed.");

        // The actual visible crosshair uses ActionCursor, not ControllerCursor.
        // The game's usingController flag can remain false while firing in place.
        ControllerGameplayInput.HalfRadius = false;
        Menuu.usingController = false;
        ActionCursor visible = new ActionCursor();
        visible.gameObject.name = "actionCursor";
        visible.Awake();
        input.Apply(new RawJoystickEvent { Type = 2, Number = 5, Value = 32767 });
        input.Apply(new RawJoystickEvent { Type = 2, Number = 3, Value = 32767 });
        Time.frameCount = 11;
        Time.unscaledTime = 11f / 60f;
        if (ControllerVisibleCursor.Prefix(visible)) visible.Update();
        Vector3 before = visible.transform.position;
        input.Apply(new RawJoystickEvent { Type = 2, Number = 3, Value = 0 });
        input.Apply(new RawJoystickEvent { Type = 2, Number = 4, Value = -32768 });
        Time.frameCount = 12;
        Time.unscaledTime = 12f / 60f;
        if (ControllerVisibleCursor.Prefix(visible)) visible.Update();
        Vector3 after = visible.transform.position;
        if ((after - before).sqrMagnitude < 0.01f)
            throw new Exception("Visible crosshair froze while RT was held and right stick moved.");
        Console.WriteLine("Visible ActionCursor follows stick while firing in place.");

        // PlayerScript can leave this flag true after gamepad movement. Mouse
        // motion must reclaim the actual visible combat cursor in that state.
        Menuu.usingController = true;
        ControllerCursor.staticX = 777f;
        ControllerCursor.staticY = 888f;
        Input.mousePosition = new Vector3(-20f, 5f, 0f);
        Time.frameCount = 13;
        Time.unscaledTime = 13f / 60f;
        if (ControllerVisibleCursor.Prefix(visible)) visible.Update();
        if (ControllerAim.TryWorldPoint(out _))
            throw new Exception("Mouse movement did not reclaim virtual aim.");
        if (Menuu.usingController)
            throw new Exception("Mouse movement did not switch PlayerScript attack branch back to mouse.");
        if (Math.Abs(AttackBranchX() + 20f) > 0.01f)
            throw new Exception("PlayerScript attack branch still follows ControllerCursor.staticX after mouse motion.");
        if (Math.Abs(visible.transform.position.x + 20f) > 0.01f ||
            Math.Abs(visible.transform.position.y - 5f) > 0.01f)
            throw new Exception("Mouse reclaimed virtual aim but visible crosshair stayed on ControllerCursor.staticX/Y: " +
                visible.transform.position.x + ", " + visible.transform.position.y);

        // A held stick should reclaim controller aiming after the short mouse
        // grace period; clicking the mouse should take it back without motion.
        Time.frameCount = 14;
        Time.unscaledTime = 0.3f;
        if (ControllerAim.TryWorldPoint(out _))
            throw new Exception("Stick reclaimed aim during mouse grace period.");
        Time.frameCount = 15;
        Time.unscaledTime = 0.6f;
        if (!ControllerAim.TryWorldPoint(out Vector3 reclaimed))
            throw new Exception("Stick failed to reclaim aim after mouse grace period.");
        if (ControllerVisibleCursor.Prefix(visible)) visible.Update();
        if (Math.Abs(visible.transform.position.x - reclaimed.x) > 0.01f ||
            Math.Abs(visible.transform.position.y - reclaimed.y) > 0.01f)
            throw new Exception("Visible crosshair did not follow reclaimed controller aim.");
        if (!Menuu.usingController)
            throw new Exception("Stick reclaim did not switch PlayerScript attack branch to controller.");
        ControllerCursor.staticX = reclaimed.x;
        if (Math.Abs(AttackBranchX() - reclaimed.x) > 0.01f)
            throw new Exception("PlayerScript attack branch did not use controller aim after reclaim.");
        Input.MouseButtonDown0 = true;
        Time.frameCount = 16;
        Time.unscaledTime = 0.62f;
        if (ControllerAim.TryWorldPoint(out _))
            throw new Exception("Mouse click did not reclaim aim without motion.");
        Input.MouseButtonDown0 = false;
        if (Menuu.usingController || Math.Abs(AttackBranchX() + 20f) > 0.01f)
            throw new Exception("Mouse click did not switch PlayerScript attack branch back to mouse.");
        if (ControllerVisibleCursor.Prefix(visible)) visible.Update();
        if (Math.Abs(visible.transform.position.x + 20f) > 0.01f ||
            Math.Abs(visible.transform.position.y - 5f) > 0.01f)
            throw new Exception("Mouse click reclaimed virtual aim but visible crosshair did not follow mouse.");

        ActionCursor inventoryHover = new ActionCursor();
        inventoryHover.gameObject.name = "hoverItem";
        inventoryHover.Awake();
        if (!ControllerVisibleCursor.Prefix(inventoryHover))
            throw new Exception("Non-combat ActionCursor was intercepted.");
    }

    // Same source-selection branch as PlayerScript.Attack's controller path.
    private static float AttackBranchX()
    {
        return Menuu.usingController ? ControllerCursor.staticX :
            Camera.main.ScreenToWorldPoint(Input.mousePosition).x;
    }
}
