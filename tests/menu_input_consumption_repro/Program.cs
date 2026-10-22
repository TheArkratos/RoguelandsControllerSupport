using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new Exception(label);
    }
    private static JoystickInputState Fresh(string text = null)
    {
        Input.Down.Clear();
        GameScript.inventoryOpen = true;
        GameScript.pausing = GameScript.dead = GameScript.buildMode = GameScript.talking = false;
        var input = new JoystickInputState();
        input.Connect();
        ControllerGameplayInput.Configure(() => input, BindingConfig.Parse((text ?? BindingConfig.DefaultText).Split('\n')));
        Time.frameCount++;
        return input;
    }
    private static void Main()
    {
        var player = new PlayerScript();
        player.SetCanInteract(true);
        MenuScript.player = player.gameObject;
        foreach (byte button in new byte[] { 0, 1, 2, 3, 5, 6, 7 })
        {
            var input = Fresh();
            input.Apply(new RawJoystickEvent { Type = 1, Number = button, Value = 1 });
            foreach (string name in new[] { "Cancel", "Inventory", "CombatMode", "Jump", "Up", "Dash Left", "Dash Right", "Droid" }) Input.Down.Add(name);
            Check(!ControllerGameplayInput.GameButtonDown("Cancel"), "Cancel consumed " + button);
            Check(!ControllerGameplayInput.GameButtonDown("Inventory"), "Inventory consumed " + button);
            Check(!ControllerGameplayInput.GameButtonDown("CombatMode"), "Combat consumed " + button);
            Check(!ControllerGameplayInput.GamePausePressed(), "Pause consumed " + button);
            Check(!ControllerGameplayInput.PlayerInteractionButtonDown("Up", player), "World interaction consumed " + button);
            Check(!ControllerGameplayInput.GameDroidButtonDown("Droid", new GameScript()), "Droid consumed " + button);
            foreach (string name in new[] { "Jump", "Up", "Dash Left", "Dash Right" })
                Check(!ControllerGameplayInput.PlayerButtonDown(name), name + " consumed " + button);
            input.BeginFrame();
            Time.frameCount++;
            Check(!ControllerGameplayInput.GameButtonDown("Cancel"), "Delayed native edge consumed " + button);
        }
        string remapped = BindingConfig.DefaultText.Replace("MenuCancel = Joystick1Button1", "MenuCancel = Joystick1Button12")
            .Replace("MenuDrop = Joystick1Button5", "MenuDrop = RightTrigger")
            .Replace("MenuSecondary = Joystick1Button2", "MenuSecondary = LeftTrigger");
        foreach (byte axis in new byte[] { 2, 5 })
        {
            var input = Fresh(remapped);
            input.Apply(new RawJoystickEvent { Type = 2, Number = axis, Value = 32767 });
            Input.Down.Add("CombatMode");
            Input.Down.Add("Droid");
            Check(!ControllerGameplayInput.GameButtonDown("CombatMode"), "Remapped trigger consumes combat");
            Check(!ControllerGameplayInput.GameDroidButtonDown("Droid", new GameScript()), "Remapped trigger consumes droid");
            Check(!ControllerGameplayInput.ExplorationPrimaryPressed(), "UI cannot dispatch exploration droid");
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "Remapped LT cannot jump");
        }
        foreach (byte button in new byte[] { 1, 6, 7 })
        {
            var input = Fresh();
            input.Apply(new RawJoystickEvent { Type = 1, Number = button, Value = 1 });
            ControllerGameplayInput.ConsumeInventoryInput();
            GameScript.inventoryOpen = false; // Prepare has completed requested close.
            Check(!ControllerGameplayInput.GameButtonDown("Cancel"), "No same-frame second cancel");
            Check(!ControllerGameplayInput.GameButtonDown("Inventory"), "No same-frame reopen");
            Check(!ControllerGameplayInput.GamePausePressed(), "No same-frame second pause");
        }
        var rebound = Fresh(remapped);
        GameScript.inventoryOpen = false;
        rebound.Apply(new RawJoystickEvent { Type = 1, Number = 1, Value = 1 });
        Input.Down.Add("Cancel");
        Check(!ControllerGameplayInput.GameButtonDown("Cancel"), "Old B does not cancel after remap");
        rebound.Apply(new RawJoystickEvent { Type = 1, Number = 12, Value = 1 });
        Check(!ControllerGameplayInput.GameButtonDown("Cancel"), "Configured UI cancel no longer synthesizes gameplay Cancel");
        var keyboard = Fresh();
        foreach (string name in new[] { "Cancel", "Inventory", "CombatMode", "Jump" }) Input.Down.Add(name);
        Check(ControllerGameplayInput.GameButtonDown("Cancel"), "Keyboard Cancel preserved");
        Check(ControllerGameplayInput.GameButtonDown("Inventory"), "Keyboard Inventory preserved");
        Check(ControllerGameplayInput.GameButtonDown("CombatMode"), "Keyboard secondary action preserved");
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "Keyboard Jump preserved");
        keyboard.Apply(new RawJoystickEvent { Type = 2, Number = 0, Value = 32767 });
        Time.frameCount++;
        Check(ControllerGameplayInput.PlayerButton("Right"), "Stick movement retained in inventory");
        Console.WriteLine("Inventory input ownership: " + checks + " checks passed.");
    }
}
