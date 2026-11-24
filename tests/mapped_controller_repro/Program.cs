using System;
using ControllerSupport;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void Main()
    {
        var mapped = new MappedControllerState();
        Check(!mapped.State.Connected, "Starts disconnected");
        mapped.Apply(new short[6], new bool[15], 1);
        Check(mapped.State.Connected, "First mapped snapshot connects");
        Check(mapped.State.LeftTrigger == 0f && mapped.State.RightTrigger == 0f,
            "SDL neutral triggers become canonical negative endpoints");
        Check(!mapped.State.MeaningfulInputThisFrame, "Initial snapshot is not activity");
        ButtonsAndDefaults();
        SticksAndTriggers();
        DPadAndRearm();
        DPadConflictingButtons();
        DPadSnapshotTransitions();
        InitialHeldAndReconnect();
        Ownership();
        Console.WriteLine("Mapped controller contract: " + checks + " checks passed.");
    }

    private static void ButtonsAndDefaults()
    {
        var bindings = BindingConfig.Parse(BindingConfig.DefaultText.Split('\n'));
        // SDL order: A B X Y Back Guide Start L3 R3 LB RB.
        int[] expectedCanonical = { 0, 1, 2, 3, 6, 8, 7, 9, 10, 4, 5 };
        string[][] expectedActions = {
            new[] { "MenuActivate", "Hotkey5" }, new[] { "MenuCancel", "Hotkey6" },
            new[] { "Interact", "MenuSecondary" }, new[] { "CombatMode", "MenuTransfer" },
            new[] { "Inventory" }, new string[0], new[] { "Pause" },
            new[] { "AimAssist" }, new string[0], new[] { "Dash", "MenuPreviousPage" },
            new[] { "MenuNextPage" }
        };
        var mapped = new MappedControllerState();
        var axes = new short[6];
        var buttons = new bool[15];
        mapped.Apply(axes, buttons, 0);
        for (int sdl = 0; sdl < 11; sdl++)
        {
            buttons[sdl] = true;
            mapped.Apply(axes, buttons, 10);
            for (int canonical = 0; canonical < 20; canonical++)
            {
                bool expected = canonical == expectedCanonical[sdl];
                Check(mapped.State.ButtonHeld(canonical) == expected, "Canonical held mapping " + sdl + "/" + canonical);
                Check(mapped.State.ButtonPressed(canonical) == expected, "Canonical edge mapping " + sdl + "/" + canonical);
            }
            foreach (string action in BindingConfig.Actions)
            {
                bool expected = Array.IndexOf(expectedActions[sdl], action) >= 0;
                Check(bindings.Held(action, mapped.State) == expected, "Default held " + sdl + "/" + action);
                Check(bindings.Pressed(action, mapped.State) == expected, "Default press " + sdl + "/" + action);
            }
            Check(mapped.State.MeaningfulInputThisFrame, "Fresh button is activity");
            mapped.Apply(axes, buttons, 11);
            Check(!mapped.State.ButtonPressed(expectedCanonical[sdl]) && !mapped.State.MeaningfulInputThisFrame,
                "Held snapshot neither repeats edge nor reports activity");
            buttons[sdl] = false;
            mapped.Apply(axes, buttons, 12);
            Check(mapped.State.ButtonReleased(expectedCanonical[sdl]), "Release maps to canonical button");
            mapped.Apply(axes, buttons, 13);
            Check(!mapped.State.ButtonReleased(expectedCanonical[sdl]), "Release edge clears next frame");
        }
    }

    private static void SticksAndTriggers()
    {
        var mapped = new MappedControllerState();
        var axes = new short[6];
        var buttons = new bool[15];
        mapped.Apply(axes, buttons, 0);
        for (int axis = 0; axis < 4; axis++)
        {
            axes[axis] = short.MinValue;
            mapped.Apply(axes, buttons, 1);
            float[] actual = { mapped.State.MoveX, mapped.State.MoveY, mapped.State.AimX, mapped.State.AimY };
            for (int field = 0; field < 4; field++)
                Check(actual[field] == (field == axis ? -1f : 0f), "Independent negative stick axis " + axis + "/" + field);
            axes[axis] = short.MaxValue;
            mapped.Apply(axes, buttons, 2);
            actual = new[] { mapped.State.MoveX, mapped.State.MoveY, mapped.State.AimX, mapped.State.AimY };
            for (int field = 0; field < 4; field++)
                Check(actual[field] == (field == axis ? 1f : 0f), "Independent positive stick axis " + axis + "/" + field);
            axes[axis] = 1000;
            mapped.Apply(axes, buttons, 3);
            Check(mapped.State.MoveX == 0f && mapped.State.MoveY == 0f && mapped.State.AimX == 0f && mapped.State.AimY == 0f,
                "Stick center noise retains existing deadzone");
            axes[axis] = 0;
            mapped.Apply(axes, buttons, 4);
        }
        var bindings = BindingConfig.Parse(BindingConfig.DefaultText.Split('\n'));
        axes[4] = 16383;
        mapped.Apply(axes, buttons, 5);
        Check(!mapped.State.LeftTriggerHeld && mapped.State.RightTrigger == 0f, "LT below half travel; RT independent");
        axes[4] = 16384;
        mapped.Apply(axes, buttons, 6);
        Check(mapped.State.LeftTriggerPressed && bindings.Pressed("Jump", mapped.State), "LT threshold drives default Jump");
        mapped.Apply(axes, buttons, 7);
        Check(mapped.State.LeftTriggerHeld && !mapped.State.LeftTriggerPressed && !mapped.State.MeaningfulInputThisFrame,
            "Held trigger does not repeat");
        axes[4] = short.MaxValue;
        axes[5] = short.MaxValue;
        mapped.Apply(axes, buttons, 8);
        Check(mapped.State.LeftTrigger == 1f && mapped.State.RightTrigger == 1f, "Both trigger positive endpoints");
        Check(bindings.Held("Jump", mapped.State) && bindings.Pressed("PrimaryAction", mapped.State) &&
            bindings.Pressed("MenuDrop", mapped.State), "RT defaults and independent LT held");
        axes[4] = -1;
        axes[5] = short.MinValue;
        mapped.Apply(axes, buttons, 9);
        Check(mapped.State.LeftTrigger == 0f && mapped.State.RightTrigger == 0f, "Negative triggers clamp to neutral");
        Check(mapped.State.LeftTriggerReleased && mapped.State.RightTriggerReleased, "Trigger releases cross threshold");
        mapped.Apply(axes, buttons, 10);
        Check(!mapped.State.LeftTriggerReleased && !mapped.State.RightTriggerReleased, "Trigger release edges clear");
    }

    private static void DPadAndRearm()
    {
        var mapped = new MappedControllerState();
        var axes = new short[6];
        var buttons = new bool[15];
        var bindings = BindingConfig.Parse(BindingConfig.DefaultText.Split('\n'));
        string[] tokens = { "DPadUp", "DPadDown", "DPadLeft", "DPadRight" };
        string[] actions = { "Hotkey1", "Hotkey3", "Hotkey4", "Hotkey2" };
        mapped.Apply(axes, buttons, 0);
        for (int direction = 0; direction < 4; direction++)
        {
            buttons[11 + direction] = true;
            mapped.Apply(axes, buttons, 1);
            for (int other = 0; other < 4; other++)
            {
                Check(mapped.State.DPadHeld(tokens[other]) == (other == direction), "D-pad held mapping");
                Check(mapped.State.DPadPressed(tokens[other]) == (other == direction), "D-pad edge mapping");
                Check(bindings.Pressed(actions[other], mapped.State) == (other == direction), "D-pad hotkey mapping");
            }
            mapped.Apply(axes, buttons, 2);
            Check(!mapped.State.DPadPressed(tokens[direction]) && !mapped.State.MeaningfulInputThisFrame, "D-pad held snapshot no repeat");
            buttons[11 + direction] = false;
            mapped.Apply(axes, buttons, 3);
            Check(!mapped.State.DPadHeld(tokens[direction]), "D-pad neutral releases");
        }
        buttons[11] = buttons[14] = true;
        mapped.Apply(axes, buttons, 100);
        Check(!mapped.State.DPadHeld("DPadUp") && !mapped.State.DPadHeld("DPadRight") &&
            !mapped.State.DPadPressed("DPadUp") && !mapped.State.DPadPressed("DPadRight"), "Simultaneous diagonal has no ordering");
        mapped.Apply(axes, buttons, 101);
        Check(!mapped.State.DPadHeld("DPadUp") && !mapped.State.DPadHeld("DPadRight"), "Held diagonal remains suppressed");
        buttons[14] = false;
        mapped.Apply(axes, buttons, 102);
        Check(mapped.State.DPadPressed("DPadUp"), "Diagonal resolves only on unambiguous cardinal");
        buttons[14] = true;
        mapped.Apply(axes, buttons, 103);
        buttons[11] = false;
        mapped.Apply(axes, buttons, 104);
        Check(mapped.State.DPadHeld("DPadUp") && !mapped.State.DPadPressed("DPadRight"), "Rolling cannot replace latched cardinal");
        buttons[14] = false;
        mapped.Apply(axes, buttons, 105);
        buttons[14] = true;
        mapped.Apply(axes, buttons, 106);
        Check(mapped.State.DPadPressed("DPadRight"), "Full neutral rearms D-pad");
    }

    private static void DPadSnapshotTransitions()
    {
        string[] tokens = { "DPadUp", "DPadDown", "DPadLeft", "DPadRight" };
        for (int from = 0; from < 4; from++)
        for (int to = 0; to < 4; to++)
        {
            if (from == to) continue;
            var mapped = new MappedControllerState();
            var axes = new short[6];
            var buttons = new bool[15];
            mapped.Apply(axes, buttons, 0);
            buttons[11 + from] = true;
            mapped.Apply(axes, buttons, 1);
            Check(mapped.State.DPadPressed(tokens[from]), "Transition starts with fresh cardinal");
            buttons[11 + from] = false;
            buttons[11 + to] = true;
            mapped.Apply(axes, buttons, 2);
            for (int direction = 0; direction < 4; direction++)
            {
                Check(mapped.State.DPadHeld(tokens[direction]) == (direction == from),
                    "Atomic cardinal transition retains old latch " + tokens[from] + " -> " + tokens[to]);
                Check(!mapped.State.DPadPressed(tokens[direction]), "Cardinal transition never generates another action");
            }
            mapped.Apply(axes, buttons, 3);
            Check(mapped.State.DPadHeld(tokens[from]) && !mapped.State.DPadPressed(tokens[to]),
                "Held destination cannot bypass full-neutral requirement");
            buttons[11 + to] = false;
            mapped.Apply(axes, buttons, 4);
            Check(!mapped.State.DPadHeld(tokens[from]), "Complete neutral snapshot clears latch");
            buttons[11 + to] = true;
            mapped.Apply(axes, buttons, 5);
            Check(mapped.State.DPadPressed(tokens[to]), "Destination rearms after complete neutral snapshot");
        }
        for (int vertical = 11; vertical <= 12; vertical++)
        for (int horizontal = 13; horizontal <= 14; horizontal++)
        {
            var mapped = new MappedControllerState();
            var axes = new short[6];
            var buttons = new bool[15];
            mapped.Apply(axes, buttons, 0);
            buttons[vertical] = buttons[horizontal] = true;
            mapped.Apply(axes, buttons, 1);
            foreach (string token in tokens)
                Check(!mapped.State.DPadHeld(token) && !mapped.State.DPadPressed(token),
                    "Every simultaneous diagonal remains ambiguous");
        }
    }

    private static void DPadConflictingButtons()
    {
        string[] tokens = { "DPadUp", "DPadDown", "DPadLeft", "DPadRight" };
        for (int from = -1; from < 4; from++)
        for (int pair = 0; pair < 2; pair++)
        {
            var mapped = new MappedControllerState();
            var axes = new short[6];
            var buttons = new bool[15];
            mapped.Apply(axes, buttons, 0);
            if (from >= 0)
            {
                buttons[11 + from] = true;
                mapped.Apply(axes, buttons, 1);
                Check(mapped.State.DPadPressed(tokens[from]), "Conflict starts with known latch");
                buttons[11 + from] = false;
            }
            // Vertical or horizontal opposing pair: neither is a neutral snapshot.
            buttons[pair == 0 ? 11 : 13] = true;
            buttons[pair == 0 ? 12 : 14] = true;
            mapped.Apply(axes, buttons, 2);
            for (int direction = 0; direction < 4; direction++)
            {
                Check(mapped.State.DPadHeld(tokens[direction]) == (direction == from),
                    "Opposing buttons suppress new directions and preserve existing latch");
                Check(!mapped.State.DPadPressed(tokens[direction]), "Opposing buttons never generate cardinal action");
            }
            mapped.Apply(axes, buttons, 3);
            for (int direction = 0; direction < 4; direction++)
                Check(!mapped.State.DPadPressed(tokens[direction]), "Held conflict has no stale additional action");
            Array.Clear(buttons, 0, buttons.Length);
            buttons[11] = true;
            mapped.Apply(axes, buttons, 4);
            Check(mapped.State.DPadHeld(from < 0 ? "DPadUp" : tokens[from]),
                "Conflict cannot manufacture neutral and rearm a previous latch");
            Check(mapped.State.DPadPressed("DPadUp") == (from < 0),
                "Only an unlatched conflict may resolve to fresh cardinal");
            Array.Clear(buttons, 0, buttons.Length);
            mapped.Apply(axes, buttons, 5);
            buttons[14] = true;
            mapped.Apply(axes, buttons, 6);
            Check(mapped.State.DPadPressed("DPadRight"), "Real neutral rearms after conflict");
        }
        for (int pair = 0; pair < 3; pair++)
        {
            var mapped = new MappedControllerState();
            var axes = new short[6];
            var buttons = new bool[15];
            buttons[11] = buttons[12] = pair != 1;
            buttons[13] = buttons[14] = pair != 0;
            mapped.Apply(axes, buttons, 0);
            mapped.Apply(axes, buttons, 1);
            Check(!mapped.State.MeaningfulInputThisFrame, "Initial or held conflict is not fresh activity");
            foreach (string token in tokens)
                Check(!mapped.State.DPadHeld(token) && !mapped.State.DPadPressed(token), "Initial conflict has no action");
            Array.Clear(buttons, 0, buttons.Length);
            buttons[11] = true;
            mapped.Apply(axes, buttons, 2);
            Check(!mapped.State.DPadHeld("DPadUp") && !mapped.State.DPadPressed("DPadUp"),
                "Initial conflict requires neutral before cardinal activation");
            buttons[11] = false;
            mapped.Apply(axes, buttons, 3);
            buttons[11] = true;
            mapped.Apply(axes, buttons, 4);
            Check(mapped.State.DPadPressed("DPadUp"), "Initial conflict rearms after real neutral");
        }
    }

    private static void InitialHeldAndReconnect()
    {
        var mapped = new MappedControllerState();
        var state = mapped.State;
        var axes = new short[] { short.MaxValue, short.MinValue, short.MinValue, short.MaxValue, short.MaxValue, short.MaxValue };
        var buttons = new bool[15];
        for (int i = 0; i < 11; i++) buttons[i] = true;
        buttons[11] = true;
        for (int connection = 0; connection < 2; connection++)
        {
            mapped.Apply(axes, buttons, 0);
            Check(ReferenceEquals(state, mapped.State) && state.Connected, "Reconnect preserves state object");
            Check(state.MoveX == 1f && state.AimY == 1f && state.LeftTriggerHeld && state.RightTriggerHeld,
                "Initial held axes remain available");
            for (int i = 0; i < 11; i++)
                Check(state.ButtonHeld(i) && !state.ButtonPressed(i), "Initial button held without edge");
            Check(!state.LeftTriggerPressed && !state.RightTriggerPressed && !state.MeaningfulInputThisFrame,
                "Initial axes suppress edges and activity");
            mapped.Apply(axes, buttons, 1);
            Check(!state.DPadHeld("DPadUp") && !state.DPadPressed("DPadUp") && !state.MeaningfulInputThisFrame,
                "Unchanged initial D-pad stays blocked rather than replaying neutral horizontal axis");
            buttons[14] = true;
            mapped.Apply(axes, buttons, 2);
            buttons[11] = false;
            mapped.Apply(axes, buttons, 3);
            Check(!state.DPadHeld("DPadRight"), "Initial D-pad block survives rolling");
            buttons[14] = false;
            mapped.Apply(axes, buttons, 4);
            buttons[11] = true;
            mapped.Apply(axes, buttons, 5);
            Check(state.DPadPressed("DPadUp"), "Initial D-pad rearms after full neutral");
            mapped.Disconnect();
            mapped.Disconnect();
            Check(!state.Connected && state.MoveX == 0f && state.AimY == 0f && state.LeftTrigger == 0f && state.RightTrigger == 0f,
                "Disconnect clears analog state and is idempotent");
            Check(!state.LeftTriggerHeld && !state.RightTriggerHeld && !state.LeftTriggerPressed &&
                !state.DPadHeld("DPadUp") && !state.DPadPressed("DPadUp") && !state.MeaningfulInputThisFrame,
                "Disconnect clears held state, edges and activity");
            for (int i = 0; i < 20; i++)
                Check(!state.ButtonHeld(i) && !state.ButtonPressed(i) && !state.ButtonReleased(i), "Disconnect clears button state");
        }
    }

    private static void Ownership()
    {
        var mapped = new MappedControllerState();
        var owner = new LastInputSource();
        var axes = new short[6];
        var buttons = new bool[15];
        buttons[0] = true;
        mapped.Apply(axes, buttons, 0);
        owner.Observe(mapped.State.MeaningfulInputThisFrame, false);
        Check(!owner.UsingController, "Initial held snapshot does not take keyboard ownership");
        buttons[0] = false;
        mapped.Apply(axes, buttons, 1);
        buttons[0] = true;
        mapped.Apply(axes, buttons, 2);
        owner.Observe(mapped.State.MeaningfulInputThisFrame, false);
        Check(owner.UsingController, "Fresh mapped button takes controller ownership");
        owner.Observe(mapped.State.MeaningfulInputThisFrame, true);
        Check(!owner.UsingController, "Simultaneous keyboard input wins");
        mapped.Apply(axes, buttons, 3);
        owner.Observe(mapped.State.MeaningfulInputThisFrame, false);
        Check(!owner.UsingController, "Held mapped snapshot cannot steal ownership back");
        axes[2] = short.MaxValue;
        mapped.Apply(axes, buttons, 4);
        owner.Observe(mapped.State.MeaningfulInputThisFrame, false);
        Check(owner.UsingController, "Fresh mapped aim movement takes ownership");
        mapped.Disconnect();
        mapped.Apply(axes, buttons, 5);
        owner.Observe(mapped.State.MeaningfulInputThisFrame, true);
        mapped.Apply(axes, buttons, 6);
        owner.Observe(mapped.State.MeaningfulInputThisFrame, false);
        Check(!owner.UsingController, "Reconnect with held input preserves keyboard takeover");
    }
}
