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
        Input.Down.Clear(); Input.KeysDown.Clear();
        GameScript.inventoryOpen = true;
        GameScript.pausing = GameScript.dead = GameScript.buildMode = GameScript.talking = false;
        var input = new JoystickInputState();
        input.Connect();
        ControllerGameplayInput.Configure(() => input, BindingConfig.Parse((text ?? BindingConfig.DefaultText).Split('\n')));
        Time.frameCount++;
        return input;
    }
    private static void CheckLegacyPadProvenance()
    {
        foreach (string alias in new[] { "Jump", "Dash Left", "Dash Right" })
        foreach (bool owns in new[] { true, false })
        foreach (bool held in new[] { true, false })
        foreach (int delay in new[] { 0, 1, 3 })
        {
            ControllerInputOwnership.Configure(() => owns);
            var raw = Fresh(); GameScript.inventoryOpen = false;
            ControllerGameplayInput.PlayerButtonDown(alias);
            byte button = alias == "Jump" ? (byte)0 : alias == "Dash Left" ? (byte)4 : (byte)5;
            raw.BeginFrame(); Time.frameCount++;
            raw.Apply(new RawJoystickEvent { Type = 1, Number = button, Value = 1 });
            ControllerGameplayInput.PlayerButtonDown(alias);
            if (!held) raw.Apply(new RawJoystickEvent { Type = 1, Number = button, Value = 0 });
            for (int frame = 0; frame <= delay; frame++)
            {
                raw.BeginFrame(); Time.frameCount++;
                Check(!ControllerGameplayInput.PlayerButtonDown(alias), "neutral frame does not synthesize " + alias);
            }
            Input.Down.Add(alias);
            Check(!ControllerGameplayInput.PlayerButtonDown(alias),
                "merged legacy alias suppressed: " + alias + ", owns=" + owns + ", held=" + held + ", delay=" + delay);
            string key = alias == "Jump" ? "space" : alias == "Dash Left" ? "q" : "e";
            Input.Down.Clear(); Input.KeysDown.Add(key);
            Check(ControllerGameplayInput.PlayerButtonDown(alias),
                "actual keyboard works independently of merged alias and held raw pad: " + key + ", owns=" + owns);
            Input.KeysDown.Clear();
            raw.Clear(); Input.Down.Add(alias);
            Check(ControllerGameplayInput.PlayerButtonDown(alias), "disconnected controller preserves native fallback for " + alias);
        }
        ControllerInputOwnership.Configure(() => true);
        foreach (bool menuFirst in new[] { true, false })
        foreach (string alias in new[] { "Jump", "Dash Right" })
        {
            var raw = Fresh(); ControllerGameplayInput.PlayerButtonDown(alias);
            raw.BeginFrame(); Time.frameCount++;
            raw.Apply(new RawJoystickEvent { Type = 1, Number = alias == "Jump" ? (byte)0 : (byte)5, Value = 1 });
            if (menuFirst) ControllerGameplayInput.ConsumeInventoryInput();
            Check(!ControllerGameplayInput.PlayerButtonDown(alias), "menu blocks pad alias in either update order");
            if (!menuFirst) ControllerGameplayInput.ConsumeInventoryInput();
            GameScript.inventoryOpen = false;
            raw.Apply(new RawJoystickEvent { Type = 1, Number = alias == "Jump" ? (byte)0 : (byte)5, Value = 0 });
            for (int frame = 0; frame < 4; frame++)
            {
                raw.BeginFrame(); Time.frameCount++;
                Input.Down.Add(alias);
                Check(!ControllerGameplayInput.PlayerButtonDown(alias), "menu close cannot replay delayed legacy alias " + alias);
            }
        }
        var remappedJump = Fresh(BindingConfig.DefaultText.Replace("Jump = LeftTrigger", "Jump = Joystick1Button0")
            .Replace("Hotkey5 = Joystick1Button0", "Hotkey5 = Unbound"));
        GameScript.inventoryOpen = false; ControllerGameplayInput.PlayerButtonDown("Jump");
        remappedJump.BeginFrame(); Time.frameCount++;
        remappedJump.Apply(new RawJoystickEvent { Type = 1, Number = 0, Value = 1 }); Input.Down.Add("Jump");
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "explicit A-to-Jump remap remains a configured controller action");
        foreach (short direction in new[] { short.MinValue, short.MaxValue })
        {
            var remappedDash = Fresh(BindingConfig.DefaultText.Replace("Dash = Joystick1Button4", "Dash = Joystick1Button5"));
            GameScript.inventoryOpen = false; ControllerGameplayInput.PlayerButtonDown("Dash Right");
            remappedDash.BeginFrame(); Time.frameCount++;
            remappedDash.Apply(new RawJoystickEvent { Type = 2, Number = 0, Value = direction });
            remappedDash.Apply(new RawJoystickEvent { Type = 1, Number = 5, Value = 1 }); Input.Down.Add("Dash Right");
            Check(ControllerGameplayInput.PlayerButtonDown(direction < 0 ? "Dash Left" : "Dash Right"), "explicit RB Dash follows configured direction");
            Check(!ControllerGameplayInput.PlayerButtonDown(direction < 0 ? "Dash Right" : "Dash Left"), "explicit RB Dash does not also execute its legacy direction");
        }
        Fresh(); ControllerGameplayInput.Configure(null, BindingConfig.Parse(BindingConfig.DefaultText.Split('\n')));
        Input.Down.Add("Jump");
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "unavailable raw provider preserves native fallback");
    }
    private static void Main()
    {
        var player = new PlayerScript();
        player.SetCanInteract(true);
        MenuScript.player = player.gameObject;
        foreach (byte button in new byte[] { 0, 1, 2, 3, 6, 7 })
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
        var delayedConfirm = Fresh();
        delayedConfirm.Apply(new RawJoystickEvent { Type = 1, Number = 0, Value = 1 });
        Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "raw confirm does not jump");
        delayedConfirm.BeginFrame(); Time.frameCount++;
        delayedConfirm.Apply(new RawJoystickEvent { Type = 1, Number = 0, Value = 0 });
        Input.Down.Add("Jump"); // Unity's legacy edge arrives after the raw A release.
        Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "delayed native confirm edge cannot jump in a menu");
        foreach (bool playerFirst in new[] { true, false })
        foreach (bool remapJumpToConfirm in new[] { false, true })
        {
            string config = remapJumpToConfirm ? BindingConfig.DefaultText
                .Replace("Jump = LeftTrigger", "Jump = Joystick1Button0")
                .Replace("Hotkey5 = Joystick1Button0", "Hotkey5 = Unbound") : null;
            var confirm = Fresh(config);
            ControllerGameplayInput.PlayerButtonDown("Jump"); // observe neutral open menu
            confirm.BeginFrame(); Time.frameCount++;
            confirm.Apply(new RawJoystickEvent { Type = 1, Number = 0, Value = 1 });
            Input.Down.Add("Jump");
            if (!playerFirst) ControllerGameplayInput.ConsumeInventoryInput();
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "confirm blocked in either menu update order");
            if (playerFirst) ControllerGameplayInput.ConsumeInventoryInput();
            GameScript.inventoryOpen = false;
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "confirm cannot jump on native close frame");
            confirm.BeginFrame(); Time.frameCount++;
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "held confirm cannot jump after close");
            confirm.BeginFrame(); Time.frameCount++;
            confirm.Apply(new RawJoystickEvent { Type = 1, Number = 0, Value = 0 });
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "late Unity confirm edge cannot jump on release frame");
            confirm.BeginFrame(); Time.frameCount++; Input.Down.Clear(); Input.KeysDown.Clear();
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "neutral release rearms without jumping");
            confirm.BeginFrame(); Time.frameCount++;
            confirm.Apply(new RawJoystickEvent { Type = remapJumpToConfirm ? (byte)1 : (byte)2,
                Number = remapJumpToConfirm ? (byte)0 : (byte)2, Value = remapJumpToConfirm ? (short)1 : short.MaxValue });
            Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "fresh configured jump resumes after confirm release");
        }
        var heldJump = Fresh(BindingConfig.DefaultText.Replace(
            "MenuActivate = Joystick1Button0", "MenuActivate = LeftTrigger"));
        ControllerGameplayInput.PlayerButtonDown("Jump");
        heldJump.BeginFrame(); Time.frameCount++;
        heldJump.Apply(new RawJoystickEvent { Type = 2, Number = 2, Value = short.MaxValue });
        ControllerGameplayInput.ConsumeInventoryInput();
        ControllerGameplayInput.ConsumeActionForFrame("MenuActivate");
        GameScript.inventoryOpen = false;
        heldJump.BeginFrame(); Time.frameCount++;
        Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "consumed held Jump trigger does not count as release");
        heldJump.Apply(new RawJoystickEvent { Type = 2, Number = 2, Value = short.MinValue });
        heldJump.BeginFrame(); Time.frameCount++; ControllerGameplayInput.PlayerButtonDown("Jump");
        heldJump.BeginFrame(); Time.frameCount++;
        heldJump.Apply(new RawJoystickEvent { Type = 2, Number = 2, Value = short.MaxValue });
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "LT gameplay jump resumes only after real release");
        bool modal = false;
        ControllerInputOwnership.Configure(() => true, () => modal);
        var modalConfirm = Fresh(); GameScript.inventoryOpen = false;
        ControllerGameplayInput.PlayerButtonDown("Jump");
        modalConfirm.BeginFrame(); Time.frameCount++; modal = true;
        modalConfirm.Apply(new RawJoystickEvent { Type = 1, Number = 0, Value = 1 });
        Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "live story modal blocks confirm before menu prefix");
        modal = false; modalConfirm.BeginFrame(); Time.frameCount++; Input.Down.Add("Jump");
        Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "story close retains confirm release gate");
        ControllerInputOwnership.Configure(() => false);
        Input.KeysDown.Add("space");
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "keyboard handoff is immediately native despite retained controller confirm");
        ControllerInputOwnership.Configure(() => true);
        modalConfirm.Clear(); Input.KeysDown.Clear(); Time.frameCount++;
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "disconnected controller leaves native Jump intact");
        foreach (bool pause in new[] { false, true })
        {
            var cached = Fresh(); GameScript.inventoryOpen = false;
            ControllerGameplayInput.PlayerButtonDown("Jump"); // cache gameplay-neutral this frame
            GameScript.inventoryOpen = !pause; GameScript.pausing = pause;
            Input.Down.Add("Jump");
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "live menu blocks late native jump despite cached gameplay refresh");
            GameScript.inventoryOpen = GameScript.pausing = false;
            cached.BeginFrame(); Time.frameCount++;
            Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "live menu latch survives cached-frame close");
        }
        var dropping = Fresh();
        ControllerGameplayInput.ExplorationPrimaryHeld(); // neutral frame arms gameplay before dropping
        Time.frameCount++;
        dropping.Apply(new RawJoystickEvent { Type=2, Number=5, Value=short.MaxValue });
        Check(!ControllerGameplayInput.ExplorationPrimaryHeld(), "RT drop is not exploration primary while inventory open");
        ControllerGameplayInput.ConsumeInventoryInput();
        GameScript.inventoryOpen = false;
        Check(!ControllerGameplayInput.ExplorationPrimaryHeld(), "RT drop cannot dispatch on same-frame menu close");
        dropping.BeginFrame(); Time.frameCount++;
        Check(!ControllerGameplayInput.ExplorationPrimaryHeld(), "held RT cannot dispatch after menu close until release");
        Input.Axes["Attack"] = -0.4f;
        Check(ControllerGameplayInput.PlayerAxis("Attack") == 0f, "held UI RT cannot leak native attack axis after close");
        Input.Axes.Clear();
        dropping.Apply(new RawJoystickEvent { Type=2, Number=5, Value=short.MinValue });
        Time.frameCount++; ControllerGameplayInput.ExplorationPrimaryHeld();
        dropping.BeginFrame(); dropping.Apply(new RawJoystickEvent { Type=2, Number=5, Value=short.MaxValue }); Time.frameCount++;
        Check(ControllerGameplayInput.ExplorationPrimaryHeld(), "fresh RT after release can resume exploration primary");
        string remapped = BindingConfig.DefaultText.Replace("MenuCancel = Joystick1Button1", "MenuCancel = Joystick1Button12")
            .Replace("MenuDrop = RightTrigger", "MenuDrop = RightTrigger")
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
        Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "owned menu blocks jump independent of raw timing");
        ControllerInputOwnership.Configure(() => false);
        Input.KeysDown.Add("space");
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "keyboard ownership preserves native Jump in inventory");
        ControllerInputOwnership.Configure(() => true);
        keyboard.Apply(new RawJoystickEvent { Type = 2, Number = 0, Value = 32767 });
        Time.frameCount++;
        Check(ControllerGameplayInput.PlayerButton("Right"), "Stick movement retained in inventory");
        string recipeTriggers = BindingConfig.DefaultText
            .Replace("MenuPreviousPage = Joystick1Button4", "MenuPreviousPage = RightTrigger")
            .Replace("MenuNextPage = Joystick1Button5", "MenuNextPage = LeftTrigger");
        var recipeInput = Fresh(recipeTriggers);
        recipeInput.Apply(new RawJoystickEvent { Type = 2, Number = 5, Value = 32767 });
        recipeInput.Apply(new RawJoystickEvent { Type = 2, Number = 2, Value = 32767 });
        ControllerGameplayInput.ConsumeInventoryInput();
        ControllerGameplayInput.ConsumeActionForFrame("MenuPreviousPage");
        ControllerGameplayInput.ConsumeActionForFrame("MenuNextPage");
        Check(ControllerGameplayInput.IsActionConsumed("PrimaryAction") && ControllerGameplayInput.IsActionConsumed("Jump"),
            "both conflicting recipe-page bindings remain consumed in the same frame");
        Input.Axes["Attack"] = -0.4f;
        Input.Down.Add("Jump");
        Check(ControllerGameplayInput.PlayerAxis("Attack") == 0f && !ControllerGameplayInput.PlayerButtonDown("Jump"),
            "recipe paging consumes both native trigger aliases without attacking or jumping");
        ControllerInputOwnership.Configure(() => false);
        Input.KeysDown.Add("space");
        Check(ControllerGameplayInput.PlayerAxis("Attack") == -0.4f && ControllerGameplayInput.PlayerButtonDown("Jump"),
            "mouse ownership retains native axes and button values after recipe consumption");
        ControllerInputOwnership.Configure(() => true);
        GameScript.inventoryOpen = false;
        recipeInput.BeginFrame(); Time.frameCount++;
        Check(!ControllerGameplayInput.IsActionConsumed("PrimaryAction") && !ControllerGameplayInput.IsActionConsumed("Jump"),
            "recipe consumption expires outside its inventory frame");
        Input.Axes.Clear(); Input.Down.Clear(); Input.KeysDown.Clear();
        var leakedAliases = new System.Collections.Generic.List<string>();
        foreach (string alias in new[] { "Jump", "Dash Right" })
        {
            byte button = alias == "Jump" ? (byte)0 : (byte)5;
            var raw = Fresh(); GameScript.inventoryOpen = false;
            ControllerGameplayInput.PlayerButtonDown(alias); // arm a neutral gameplay frame
            raw.BeginFrame(); Time.frameCount++;
            raw.Apply(new RawJoystickEvent { Type = 1, Number = button, Value = 1 });
            ControllerGameplayInput.PlayerButtonDown(alias);
            raw.BeginFrame(); Time.frameCount++;
            raw.Apply(new RawJoystickEvent { Type = 1, Number = button, Value = 0 });
            Input.Down.Add(alias); // independent Unity edge follows raw release
            if (ControllerGameplayInput.PlayerButtonDown(alias)) leakedAliases.Add(alias);
        }
        Check(leakedAliases.Count == 0, "delayed legacy pad aliases leak after raw release: " + string.Join(", ", leakedAliases));
        CheckLegacyPadProvenance();
        Console.WriteLine("Inventory input ownership: " + checks + " checks passed.");
    }
}
