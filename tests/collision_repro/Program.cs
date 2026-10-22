using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static void Main()
    {
        CheckVanillaConflict(0, "Jump", false);       // A is vanilla Jump, not configured.
        CheckVanillaConflict(4, "Dash Left", false);  // LB alone must not dash.
        CheckVanillaConflict(5, "Dash Right", false); // RB is configured for close aim.
        CheckVanillaConflict(3, "Up", false);         // Y toggles mode, not interact.
        CheckDelayedVanillaConflict(4, "Dash Left");
        CheckDelayedVanillaConflict(5, "Dash Right");
        CheckDelayedVanillaConflict(0, "Jump");
        CheckDelayedVanillaConflict(3, "Up");

        PlayerScript player = new PlayerScript();
        MenuScript.player = player.gameObject;
        JoystickInputState interactInput = Fresh();
        interactInput.Apply(new RawJoystickEvent { Type = 1, Number = 2, Value = 1 });
        Input.Down.Add("Up"); // Even if X is also a vanilla Up binding.
        Time.frameCount++;
        if (ControllerGameplayInput.PlayerInteractionButtonDown("Up", player))
            throw new Exception("X invoked Up branch without an interactable NPC.");
        player.SetCanInteract(true);
        interactInput.BeginFrame();
        Time.frameCount++;
        if (ControllerGameplayInput.PlayerInteractionButtonDown("Up", player))
            throw new Exception("Held X retriggered interaction.");
        interactInput.BeginFrame();
        interactInput.Apply(new RawJoystickEvent { Type = 1, Number = 2, Value = 0 });
        interactInput.BeginFrame();
        interactInput.Apply(new RawJoystickEvent { Type = 1, Number = 2, Value = 1 });
        Time.frameCount++;
        if (!ControllerGameplayInput.PlayerInteractionButtonDown("Up", player))
            throw new Exception("Fresh X did not interact with available NPC.");
        player.SetInteracting(true);
        interactInput.BeginFrame();
        interactInput.Apply(new RawJoystickEvent { Type = 1, Number = 2, Value = 0 });
        interactInput.BeginFrame();
        interactInput.Apply(new RawJoystickEvent { Type = 1, Number = 2, Value = 1 });
        Time.frameCount++;
        if (ControllerGameplayInput.PlayerInteractionButtonDown("Up", player))
            throw new Exception("X invoked the Up augment branch while already interacting.");
        Input.Down.Clear();

        JoystickInputState droidInput = Fresh();
        droidInput.Apply(new RawJoystickEvent { Type = 2, Number = 5, Value = 32767 });
        Time.frameCount++;
        if (!ControllerGameplayInput.ExplorationPrimaryPressed())
            throw new Exception("Fresh RT did not request one droid.");
        droidInput.BeginFrame();
        Time.frameCount++;
        if (ControllerGameplayInput.ExplorationPrimaryPressed() ||
            !ControllerGameplayInput.ExplorationPrimaryHeld())
            throw new Exception("Held RT retriggered droid dispatch.");
        droidInput.Apply(new RawJoystickEvent { Type = 2, Number = 5, Value = -32768 });
        droidInput.BeginFrame();
        droidInput.Apply(new RawJoystickEvent { Type = 2, Number = 5, Value = 32767 });
        Time.frameCount++;
        if (!ControllerGameplayInput.ExplorationPrimaryPressed())
            throw new Exception("Released/repressed RT did not request a new droid.");

        JoystickInputState switchingInput = Fresh();
        switchingInput.Apply(new RawJoystickEvent { Type = 1, Number = 3, Value = 1 });
        switchingInput.Apply(new RawJoystickEvent { Type = 2, Number = 5, Value = 32767 });
        Time.frameCount++;
        if (!ControllerGameplayInput.ExplorationContextSwitchPressed())
            throw new Exception("Combat-mode switch did not block same-frame droid dispatch.");

        JoystickInputState menuInput = Fresh();
        menuInput.Apply(new RawJoystickEvent { Type = 1, Number = 7, Value = 1 });
        Input.Down.Add("Inventory"); // Vanilla Menu button opens inventory.
        Time.frameCount++;
        if (ControllerGameplayInput.GameButtonDown("Inventory"))
            throw new Exception("Menu still opens vanilla inventory.");
        if (!ControllerGameplayInput.GamePausePressed())
            throw new Exception("Menu did not request pause.");
        Input.Down.Clear();
        menuInput.BeginFrame();
        Time.frameCount++;
        Input.Down.Add("Inventory"); // Delayed Unity inventory edge.
        if (ControllerGameplayInput.GameButtonDown("Inventory"))
            throw new Exception("Delayed Menu inventory edge escaped.");
        Input.Down.Clear();

        JoystickInputState viewInput = Fresh();
        viewInput.Apply(new RawJoystickEvent { Type = 1, Number = 6, Value = 1 });
        Time.frameCount++;
        if (!ControllerGameplayInput.GameButtonDown("Inventory") ||
            ControllerGameplayInput.GamePausePressed())
            throw new Exception("View did not open inventory exclusively.");

        GameScript.inventoryOpen = true;
        JoystickInputState heldViewInput = Fresh();
        heldViewInput.Apply(new RawJoystickEvent { Type = 1, Number = 6, Value = 1 });
        Input.Down.Add("Inventory"); // Legacy View binding may also fire.
        Time.frameCount++;
        ControllerGameplayInput.SetGameUpdateContext(new GameScript());
        if (ControllerGameplayInput.GameButtonDown("Inventory"))
            throw new Exception("View escaped inventory lifecycle ownership.");
        Input.Down.Clear();

        JoystickInputState heldBInput = Fresh();
        heldBInput.Apply(new RawJoystickEvent { Type = 1, Number = 1, Value = 1 });
        Input.Down.Add("Cancel"); // Native B=Cancel mapping.
        Time.frameCount++;
        ControllerGameplayInput.SetGameUpdateContext(new GameScript());
        if (ControllerGameplayInput.GameButtonDown("Cancel"))
            throw new Exception("B escaped inventory lifecycle ownership.");
        Input.Down.Clear();

        JoystickInputState keyboardInput = Fresh();
        Input.Down.Add("Cancel");
        Time.frameCount++;
        ControllerGameplayInput.SetGameUpdateContext(new GameScript());
        if (!ControllerGameplayInput.GameButtonDown("Cancel"))
            throw new Exception("Keyboard-only Cancel was suppressed by held-item guard.");
        Input.Down.Clear();
        Input.Down.Add("Inventory");
        Time.frameCount++;
        ControllerGameplayInput.SetGameUpdateContext(new GameScript());
        if (!ControllerGameplayInput.GameButtonDown("Inventory"))
            throw new Exception("Keyboard-only Inventory was suppressed by held-item guard.");
        Input.Down.Clear();
        GameScript.inventoryOpen = false;

        JoystickInputState closeAimInput = Fresh();
        GameScript.combatMode = true;
        closeAimInput.Apply(new RawJoystickEvent { Type = 1, Number = 10, Value = 1 });
        Time.frameCount++;
        if (!ControllerGameplayInput.CloseAimHeld())
            throw new Exception("R3 did not request half-distance aim.");
        closeAimInput.Apply(new RawJoystickEvent { Type = 1, Number = 10, Value = 0 });
        closeAimInput.Apply(new RawJoystickEvent { Type = 1, Number = 5, Value = 1 });
        Time.frameCount++;
        if (ControllerGameplayInput.CloseAimHeld())
            throw new Exception("RB still requested half-distance aim.");
        GameScript.combatMode = false;

        JoystickInputState input = Fresh();
        Time.frameCount++;
        Input.Down.Add("Jump"); // Keyboard-only vanilla action remains intact.
        if (!ControllerGameplayInput.PlayerButtonDown("Jump"))
            throw new Exception("Keyboard jump was suppressed.");
        Input.Down.Clear();
        input.Apply(new RawJoystickEvent { Type = 2, Number = 2, Value = 32767 });
        Time.frameCount++;
        if (!ControllerGameplayInput.PlayerButtonDown("Jump"))
            throw new Exception("Configured LT jump was lost.");

        input = Fresh();
        input.Apply(new RawJoystickEvent { Type = 2, Number = 0, Value = -32768 });
        input.Apply(new RawJoystickEvent { Type = 1, Number = 4, Value = 1 });
        Time.frameCount++;
        if (!ControllerGameplayInput.PlayerButtonDown("Dash Left"))
            throw new Exception("Configured LB+left dash was lost.");

        input = Fresh();
        input.Apply(new RawJoystickEvent { Type = 2, Number = 0, Value = 32767 });
        input.Apply(new RawJoystickEvent { Type = 1, Number = 4, Value = 1 });
        Input.Down.Add("Dash Left"); // Vanilla LB must not force left.
        Time.frameCount++;
        if (ControllerGameplayInput.PlayerButtonDown("Dash Left") ||
            !ControllerGameplayInput.PlayerButtonDown("Dash Right"))
            throw new Exception("LB+right did not dash right exclusively.");
        Input.Down.Clear();

        input = Fresh();
        input.Apply(new RawJoystickEvent { Type = 2, Number = 0, Value = -32768 });
        input.Apply(new RawJoystickEvent { Type = 1, Number = 5, Value = 1 });
        Input.Down.Add("Dash Right"); // Vanilla RB must not dash right.
        Time.frameCount++;
        if (ControllerGameplayInput.PlayerButtonDown("Dash Left") ||
            ControllerGameplayInput.PlayerButtonDown("Dash Right"))
            throw new Exception("RB triggered a dash.");
        Input.Down.Clear();

        input = Fresh();
        input.Apply(new RawJoystickEvent { Type = 1, Number = 3, Value = 1 });
        Time.frameCount++;
        if (!ControllerGameplayInput.GameButtonDown("CombatMode"))
            throw new Exception("Configured Y mode toggle was lost.");
        Console.WriteLine("Vanilla joystick conflicts suppressed; keyboard and configured actions preserved.");
    }

    private static void CheckVanillaConflict(int button, string namedInput, bool expected)
    {
        JoystickInputState input = Fresh();
        input.Apply(new RawJoystickEvent { Type = 1, Number = (byte)button, Value = 1 });
        Input.Down.Add(namedInput); // Simulate the game's legacy joystick mapping.
        Time.frameCount++;
        bool actual = ControllerGameplayInput.PlayerButtonDown(namedInput);
        Input.Down.Clear();
        if (actual != expected)
            throw new Exception("Button " + button + " still invokes vanilla " + namedInput + ".");
    }

    private static JoystickInputState Fresh()
    {
        JoystickInputState input = new JoystickInputState();
        input.Connect();
        ControllerGameplayInput.Configure(() => input,
            BindingConfig.Parse(BindingConfig.DefaultText.Split('\n')));
        return input;
    }

    private static void CheckDelayedVanillaConflict(int button, string namedInput)
    {
        JoystickInputState input = Fresh();
        input.Apply(new RawJoystickEvent { Type = 1, Number = (byte)button, Value = 1 });
        Time.frameCount++;
        ControllerGameplayInput.PlayerButtonDown(namedInput); // Raw edge seen first.
        input.BeginFrame();
        Time.frameCount++;
        Input.Down.Add(namedInput); // Unity legacy button edge arrives one frame later.
        bool actual = ControllerGameplayInput.PlayerButtonDown(namedInput);
        Input.Down.Clear();
        if (actual)
            throw new Exception("Delayed vanilla " + namedInput + " escaped while button " + button + " remained held.");
    }
}
