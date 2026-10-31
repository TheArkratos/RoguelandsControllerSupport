using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ControllerSupport;

internal static class Program
{
    private static string ModDirectory([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source), ".."));
    }

    private static int checks;

    private static void Main()
    {
        BindingConfig defaults = BindingConfig.Parse(BindingConfig.DefaultText.Split('\n'));
        CheckExportedGameplayPatches();
        CheckMenuBindings(defaults);
        CheckRecipePagingBindings(defaults);
        CheckGameplayBindings(defaults);
        Equal("LeftStick", defaults["Move"]);
        Equal("LeftTrigger", defaults["Jump"]);
        Equal("RightTrigger", defaults["PrimaryAction"]);
        Equal("Joystick1Button4", defaults["Dash"]);
        Equal("Joystick1Button3", defaults["CombatMode"]);
        CheckObsoleteCloseAim(defaults);
        Equal("Joystick1Button6", defaults["Inventory"]);
        Equal("Joystick1Button7", defaults["Pause"]);
        Equal("Joystick1Button2", defaults["Interact"]);
        Equal("Joystick1Button0", defaults["MenuActivate"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["Jump"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["PrimaryAction"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["Dash"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["CombatMode"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["Inventory"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["Pause"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["Interact"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["MenuActivate"]);
        Equal("RightTrigger", BindingConfig.Load(Path.Combine(ModDirectory(), "Phase1Bindings.ini"))["PrimaryAction"]);
        Equal("Joystick1Button0", BindingConfig.Load(Path.Combine(ModDirectory(), "Phase1Bindings.ini"))["MenuActivate"]);

        string bound = BindingConfig.DefaultText.Replace("Jump = LeftTrigger", "Jump = Joystick1Button11");
        Equal("Joystick1Button11", BindingConfig.Parse(bound.Split('\n'))["Jump"]);
        Equal("RightTrigger", BindingConfig.Parse(bound.Replace("PrimaryAction = RightTrigger", "PrimaryAction = righttrigger").Split('\n'))["PrimaryAction"]);

        Reject(bound.Replace("PrimaryAction = RightTrigger", "PrimaryAction = Joystick1Button11"), "button collision");
        Reject(BindingConfig.DefaultText.Replace("PrimaryAction = RightTrigger", "PrimaryAction = LeftTrigger"), "trigger collision");
        Reject(bound.Replace("Jump = Joystick1Button11", "Jump = Joystick1Button20"), "button outside supported range");
        Reject(bound.Replace("Jump = Joystick1Button11", "Jump = UnknownInput"), "unknown input");
        Reject(bound.Replace("Jump = Joystick1Button11", "Jump = Joystick1Button11\nJump = Joystick1Button12"), "duplicate action");
        Reject(bound.Replace("Jump = Joystick1Button11\n", ""), "missing action");
        Reject(bound.Replace("Jump = Joystick1Button11", "Surprise = Joystick1Button11"), "unknown action");
        Reject(bound.Replace("Move = LeftStick", "Move = RightStick"), "invalid stick role");

        string file = Path.Combine(Path.GetTempPath(), "ControllerSupportBindings-test-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            File.WriteAllText(file, "[Bindings]\nJump = BadToken\n");
            string before = File.ReadAllText(file);
            RejectLoad(file, "existing invalid file");
            Equal(before, File.ReadAllText(file));
        }
        finally { if (File.Exists(file)) File.Delete(file); }

        string fresh = Path.Combine(Path.GetTempPath(), "ControllerSupportBindings-test-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            Equal("LeftStick", BindingConfig.Load(fresh)["Move"]);
            Equal(BindingConfig.DefaultText, File.ReadAllText(fresh));
        }
        finally { if (File.Exists(fresh)) File.Delete(fresh); }

        RawJoystickEvent raw;
        True(RawJoystickEvent.TryParse(new byte[] { 0x78, 0x56, 0x34, 0x12, 0x00, 0x80, 0x82, 0x03 }, out raw));
        Equal("305419896", raw.TimeMilliseconds.ToString());
        Equal("-32768", raw.Value.ToString());
        Equal("2", raw.Type.ToString());
        Equal("3", raw.Number.ToString());
        True(raw.Initial);
        True(RawJoystickEvent.TryParse(new byte[] { 0, 0, 0, 0, 1, 0, 1, 7 }, out raw));
        Equal("1", raw.Type.ToString());
        False(raw.Initial);
        False(RawJoystickEvent.TryParse(new byte[] { 0, 0, 0, 0, 0, 0, 3, 0 }, out raw));
        False(RawJoystickEvent.TryParse(new byte[7], out raw));
        True(RawJoystickEvent.ShouldLogAxis(int.MinValue, 0));
        False(RawJoystickEvent.ShouldLogAxis(0, 2999));
        True(RawJoystickEvent.ShouldLogAxis(0, 3001));
        True(RawJoystickEvent.ShouldLogAxis(-32767, 32767));

        JoystickInputState input = new JoystickInputState();
        False(input.Connected);
        Near(0f, input.LeftTrigger);
        input.Connect();
        True(input.Connected);
        Near(0f, input.LeftTrigger);
        Near(0f, input.RightTrigger);
        input.Apply(Event(2, 0, 3000, true));
        Near(0f, input.MoveX); // centered stick deadzone
        input.Apply(Event(2, 0, -4000));
        Near(0f, input.MoveX);
        input.Apply(Event(2, 0, 32767));
        Near(1f, input.MoveX);
        input.Apply(Event(2, 1, -32768));
        Near(-1f, input.MoveY);
        input.Apply(Event(2, 3, -32768));
        Near(-1f, input.AimX);
        input.Apply(Event(2, 4, 32767));
        Near(1f, input.AimY);
        input.Apply(Event(1, 4, 1, true));
        True(input.ButtonHeld(4));
        False(input.ButtonPressed(4)); // initial snapshot is not a new press
        input.Apply(Event(1, 4, 0));
        True(input.ButtonReleased(4));
        input.BeginFrame();
        False(input.ButtonReleased(4));
        input.Apply(Event(1, 4, 1));
        True(input.ButtonPressed(4));
        True(input.ButtonHeld(4));
        input.Apply(Event(1, 4, 1));
        input.BeginFrame();
        False(input.ButtonPressed(4));
        True(input.ButtonHeld(4));
        False(input.ButtonHeld(20));
        input.Apply(Event(2, 2, 32767, true));
        True(input.LeftTriggerHeld);
        False(input.LeftTriggerPressed);
        Near(1f, input.LeftTrigger);
        input.Apply(Event(2, 5, 32767));
        True(input.RightTriggerHeld);
        True(input.RightTriggerPressed);
        input.BeginFrame();
        False(input.RightTriggerPressed);
        True(input.RightTriggerHeld);
        input.Apply(Event(2, 5, -32768));
        True(input.RightTriggerReleased);
        False(input.RightTriggerHeld);
        input.BeginFrame();
        input.Apply(Event(2, 5, -1));
        False(input.RightTriggerHeld); // below midpoint threshold
        input.Apply(Event(2, 5, 0));
        True(input.RightTriggerHeld); // at midpoint threshold
        True(input.RightTriggerPressed);
        input.Clear();
        False(input.Connected);
        False(input.ButtonHeld(4));
        False(input.ButtonPressed(4));
        False(input.LeftTriggerHeld);
        Near(0f, input.MoveX);
        Near(0f, input.LeftTrigger);
        input.Apply(Event(1, 4, 1)); // disconnected events cannot reactivate it
        False(input.ButtonHeld(4));
        input.Connect();
        False(input.ButtonHeld(4));
        False(input.RightTriggerHeld);
        False(input.RightTriggerPressed);

        Console.WriteLine("Controller Support: " + checks + " checks passed.");
    }

    private static void CheckMenuBindings(BindingConfig defaults)
    {
        foreach (string action in BindingConfig.Actions)
        {
            if (action == "Move" || action == "AimTargetOrMenuSelection") continue;
            Equal("Unbound", BindingConfig.SafeDefaults()[action]);
            Equal("Unbound", BindingConfig.SafeDefaults().Label(action));
        }
        Equal("Joystick1Button2", defaults["MenuSecondary"]);
        Equal("Joystick1Button3", defaults["MenuTransfer"]);
        Equal("RightTrigger", defaults["MenuDrop"]);
        Equal("Joystick1Button1", defaults["MenuCancel"]);
        Equal("A", defaults.Label("MenuActivate"));
        Equal("B", defaults.Label("MenuCancel"));
        Equal("X", defaults.Label("MenuSecondary"));
        Equal("Y", defaults.Label("MenuTransfer"));
        Equal("RT", defaults.Label("MenuDrop"));
        Equal("LB", defaults.Label("Dash"));
        Equal("View", defaults.Label("Inventory"));
        Equal("Menu", defaults.Label("Pause"));
        Equal("LT", defaults.Label("Jump"));
        Equal("RT", defaults.Label("PrimaryAction"));
        string legacy = BindingConfig.DefaultText
            .Replace("MenuSecondary = Joystick1Button2", "MenuSecondary = Unbound")
            .Replace("MenuTransfer = Joystick1Button3\n", "")
            .Replace("MenuDrop = RightTrigger\n", "")
            .Replace("MenuCancel = Joystick1Button1\n", "");
        string path = Path.Combine(Path.GetTempPath(), "ControllerSupportBindings-legacy-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            byte[] original = System.Text.Encoding.UTF8.GetBytes(legacy.Replace("\n", "\r\n"));
            File.WriteAllBytes(path, original);
            BindingConfig loaded = BindingConfig.Load(path);
            Equal("Unbound", loaded["MenuSecondary"]);
            Equal("Joystick1Button3", loaded["MenuTransfer"]);
            Equal("RightTrigger", loaded["MenuDrop"]);
            Equal("Joystick1Button1", loaded["MenuCancel"]);
            Equal(Convert.ToBase64String(original), Convert.ToBase64String(File.ReadAllBytes(path)));
            Reject(legacy.Replace("MenuSecondary = Unbound\n", ""), "legacy missing original action");
            foreach (string action in new[] { "MenuTransfer", "MenuDrop", "MenuCancel" })
            {
                string custom = legacy.Replace("MenuActivate = Joystick1Button0", "MenuActivate = " + defaults[action]);
                BindingConfig parsed = BindingConfig.Parse(custom.Split('\n'));
                Equal(defaults[action], parsed["MenuActivate"]);
                Equal("Unbound", parsed[action]);
                Equal("Unbound", parsed["MenuSecondary"]);
                byte[] customBytes = System.Text.Encoding.UTF8.GetBytes(custom.Replace("\n", "\r\n"));
                File.WriteAllBytes(path, customBytes);
                BindingConfig customLoaded = BindingConfig.Load(path);
                Equal(defaults[action], customLoaded["MenuActivate"]);
                Equal("Unbound", customLoaded[action]);
                Equal(Convert.ToBase64String(customBytes), Convert.ToBase64String(File.ReadAllBytes(path)));
                Reject(custom + action + " = " + defaults[action] + "\n", "explicit new action collision " + action);
                BindingConfig explicitUnbound = BindingConfig.Parse((legacy + action + " = Unbound\n").Split('\n'));
                Equal("Unbound", explicitUnbound[action]);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        foreach (string action in new[] { "MenuSecondary", "MenuTransfer", "MenuDrop", "MenuCancel" })
        {
            string current = action + " = " + defaults[action];
            Reject(BindingConfig.DefaultText.Replace(current, action + " = Joystick1Button0"), "menu collision " + action);
            Reject(BindingConfig.DefaultText.Replace(current, action + " = Joystick1Button6"), "inventory collision " + action);
            Reject(BindingConfig.DefaultText.Replace(current, action + " = UnknownInput"), "bad menu token " + action);
        }
        // Cross-context reuse is intentional: X, Y and RT retain their gameplay actions.
        Equal(defaults["PrimaryAction"], defaults["MenuDrop"]);
        Equal(defaults["Interact"], defaults["MenuSecondary"]);
        Equal(defaults["CombatMode"], defaults["MenuTransfer"]);
        string remapped = BindingConfig.DefaultText
            .Replace("MenuCancel = Joystick1Button1", "MenuCancel = Joystick1Button9")
            .Replace("MenuDrop = RightTrigger", "MenuDrop = Joystick1Button12");
        BindingConfig rebound = BindingConfig.Parse(remapped.Split('\n'));
        Equal("L3", rebound.Label("MenuCancel"));
        Equal("Button 12", rebound.Label("MenuDrop"));
        JoystickInputState input = new JoystickInputState();
        False(rebound.Pressed("MenuCancel", input));
        False(rebound.Held("MenuCancel", null));
        input.Connect();
        input.Apply(Event(1, 1, 1));
        False(rebound.Held("MenuCancel", input));
        False(rebound.Pressed("MenuCancel", input));
        input.Apply(Event(1, 9, 1));
        True(rebound.Held("MenuCancel", input));
        True(rebound.Pressed("MenuCancel", input));
        input.Apply(Event(1, 12, 1));
        True(rebound.Held("MenuDrop", input));
        True(rebound.Pressed("MenuDrop", input));
        input.BeginFrame();
        True(rebound.Held("MenuDrop", input));
        False(rebound.Pressed("MenuDrop", input));
        input.Apply(Event(1, 12, 0));
        False(rebound.Held("MenuDrop", input));
        BindingConfig triggers = BindingConfig.Parse(BindingConfig.DefaultText
            .Replace("MenuSecondary = Joystick1Button2", "MenuSecondary = LeftTrigger")
            .Replace("MenuActivate = Joystick1Button0", "MenuActivate = RightTrigger")
            .Replace("MenuDrop = RightTrigger", "MenuDrop = Unbound").Split('\n'));
        Equal("LT", triggers.Label("MenuSecondary"));
        Equal("RT", triggers.Label("MenuActivate"));
        input.Apply(Event(2, 2, 32767));
        input.Apply(Event(2, 5, 32767));
        True(triggers.Held("MenuSecondary", input));
        True(triggers.Pressed("MenuSecondary", input));
        True(triggers.Held("MenuActivate", input));
        True(triggers.Pressed("MenuActivate", input));
        False(BindingConfig.SafeDefaults().Held("MenuDrop", input));
        False(BindingConfig.SafeDefaults().Pressed("MenuDrop", input));
        input.Clear();
        False(triggers.Held("MenuActivate", input));
        False(triggers.Pressed("MenuActivate", input));
    }

    private static void CheckRecipePagingBindings(BindingConfig defaults)
    {
        Equal("Joystick1Button4", defaults["MenuPreviousPage"]);
        Equal("Joystick1Button5", defaults["MenuNextPage"]);
        Equal("LB", defaults.Label("MenuPreviousPage"));
        Equal("RB", defaults.Label("MenuNextPage"));
        Equal("Unbound", BindingConfig.SafeDefaults()["MenuPreviousPage"]);
        Equal("Unbound", BindingConfig.SafeDefaults()["MenuNextPage"]);
        // Paging deliberately wins over these actions inside the recipe panel.
        BindingConfig shared = BindingConfig.Parse(BindingConfig.DefaultText
            .Replace("MenuNextPage = Joystick1Button5", "MenuNextPage = Joystick1Button0").Split('\n'));
        Equal("Joystick1Button0", shared["MenuActivate"]);
        Equal("Joystick1Button0", shared["MenuNextPage"]);
        Reject(BindingConfig.DefaultText.Replace("MenuNextPage = Joystick1Button5",
            "MenuNextPage = Joystick1Button4"), "recipe page directions cannot share one binding");
        string legacy = BindingConfig.DefaultText.Replace("MenuPreviousPage = Joystick1Button4\n", "")
            .Replace("MenuNextPage = Joystick1Button5\n", "");
        string file = Path.Combine(Path.GetTempPath(), "ControllerSupportBindings-recipe-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            File.WriteAllText(file, legacy);
            BindingConfig loaded = BindingConfig.Load(file);
            Equal("Joystick1Button4", loaded["MenuPreviousPage"]);
            Equal("Joystick1Button5", loaded["MenuNextPage"]);
            Equal(legacy, File.ReadAllText(file));
        }
        finally { if (File.Exists(file)) File.Delete(file); }
        Equal("Unbound", BindingConfig.Parse(BindingConfig.DefaultText.Replace("MenuNextPage = Joystick1Button5",
            "MenuNextPage = Unbound").Split('\n'))["MenuNextPage"]);
    }

    private static void CheckObsoleteCloseAim(BindingConfig defaults)
    {
        False(Array.Exists(BindingConfig.Actions, action => action == "CloseAim"));
        foreach (string action in BindingConfig.Actions)
            False(defaults[action] == "Joystick1Button10");
        False(BindingConfig.DefaultText.Contains("CloseAim"));
        foreach (string token in new[] { "Joystick1Button10", "Joystick1Button4", "InvalidOldToken", "Joystick1Button999", "" })
        {
            string legacy = BindingConfig.DefaultText + "CloseAim = " + token + "\n";
            Equal("Joystick1Button4", BindingConfig.Parse(legacy.Split('\n'))["Dash"]);
            BindingConfig rebound = BindingConfig.Parse(legacy.Replace("Dash = Joystick1Button4", "Dash = Joystick1Button10").Split('\n'));
            Equal("Joystick1Button10", rebound["Dash"]);
            Equal("R3", rebound.Label("Dash"));
        }
        Equal("Joystick1Button4", BindingConfig.Parse((BindingConfig.DefaultText + "closeaim = BadToken\nCloseAim = Joystick1Button4\n").Split('\n'))["Dash"]);
        Reject(BindingConfig.DefaultText + "CloseAims = Joystick1Button10\n", "only exact obsolete key is ignored");
        Reject(BindingConfig.DefaultText.Replace("Dash = Joystick1Button4", "Dash = Joystick1Button10")
            .Replace("CombatMode = Joystick1Button3", "CombatMode = Joystick1Button10")
            + "CloseAim = BadToken\n", "active collisions still reject with obsolete key present");
        string file = Path.Combine(Path.GetTempPath(), "ControllerSupportBindings-obsolete-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            string legacy = BindingConfig.DefaultText.Replace("Dash = Joystick1Button4", "Dash = Joystick1Button10")
                + "CloseAim = InvalidOldToken\n";
            byte[] original = System.Text.Encoding.UTF8.GetBytes(legacy.Replace("\n", "\r\n"));
            File.WriteAllBytes(file, original);
            Equal("Joystick1Button10", BindingConfig.Load(file)["Dash"]);
            Equal(Convert.ToBase64String(original), Convert.ToBase64String(File.ReadAllBytes(file)));
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    private static void CheckGameplayBindings(BindingConfig defaults)
    {
        string[] added = { "AimAssist", "Hotkey1", "Hotkey2", "Hotkey3", "Hotkey4", "Hotkey5", "Hotkey6" };
        string[] tokens = { "Joystick1Button9", "DPadUp", "DPadRight", "DPadDown", "DPadLeft", "Joystick1Button0", "Joystick1Button1" };
        string[] labels = { "L3", "D-pad Up", "D-pad Right", "D-pad Down", "D-pad Left", "A", "B" };
        string legacy = BindingConfig.DefaultText;
        for (int i = 0; i < added.Length; i++)
        {
            Equal(tokens[i], defaults[added[i]]);
            Equal(labels[i], defaults.Label(added[i]));
            legacy = legacy.Replace(added[i] + " = " + tokens[i] + "\n", "");
        }
        string path = Path.Combine(Path.GetTempPath(), "ControllerSupportBindings-gameplay-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            byte[] original = System.Text.Encoding.UTF8.GetBytes(legacy.Replace("\n", "\r\n"));
            File.WriteAllBytes(path, original);
            BindingConfig loaded = BindingConfig.Load(path);
            for (int i = 0; i < added.Length; i++) Equal(tokens[i], loaded[added[i]]);
            Equal(Convert.ToBase64String(original), Convert.ToBase64String(File.ReadAllBytes(path)));
            for (int i = 0; i < added.Length; i++)
            {
                BindingConfig custom = BindingConfig.Parse(legacy.Replace("Dash = Joystick1Button4", "Dash = " + tokens[i]).Split('\n'));
                Equal(tokens[i], custom["Dash"]);
                Equal("Unbound", custom[added[i]]);
                Reject(legacy.Replace("Dash = Joystick1Button4", "Dash = " + tokens[i]) + added[i] + " = " + tokens[i] + "\n", "explicit gameplay collision " + added[i]);
                Equal("Unbound", BindingConfig.Parse((legacy + added[i] + " = Unbound\n").Split('\n'))[added[i]]);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        Equal("DPadLeft", BindingConfig.Parse(BindingConfig.DefaultText.Replace("Hotkey1 = DPadUp", "Hotkey1 = dpadleft").Replace("Hotkey4 = DPadLeft", "Hotkey4 = Unbound").Split('\n'))["Hotkey1"]);
        Equal(defaults["MenuActivate"], defaults["Hotkey5"]);
        Equal(defaults["MenuCancel"], defaults["Hotkey6"]);
        JoystickInputState input = new JoystickInputState();
        input.Connect();
        input.Apply(Event(1, 9, 1));
        True(defaults.Pressed("AimAssist", input));
        True(defaults.Held("AimAssist", input));
        input.Apply(Event(1, 0, 1));
        True(defaults.Pressed("Hotkey5", input));
        input.BeginFrame();
        False(defaults.Pressed("Hotkey5", input));
        True(defaults.Held("Hotkey5", input));
        input.Apply(Event(2, 7, -32767));
        True(defaults.Pressed("Hotkey1", input));
        True(defaults.Held("Hotkey1", input));
        input.BeginFrame();
        False(defaults.Pressed("Hotkey1", input));
        input.Clear();
        False(defaults.Held("Hotkey1", input));
        False(defaults.Pressed("Hotkey1", input));
    }

    private static RawJoystickEvent Event(byte type, byte number, short value, bool initial = false)
    {
        return new RawJoystickEvent { Type = type, Number = number, Value = value, Initial = initial };
    }

    private static void CheckExportedGameplayPatches()
    {
        string path = Path.Combine(ModDirectory(), "build", "ControllerSupport.dll");
        using (FileStream file = File.OpenRead(path))
        using (PEReader image = new PEReader(file))
        {
            MetadataReader metadata = image.GetMetadataReader();
            foreach (string patch in new[] { "PlayerGameplayInputPatch", "GameGameplayInputPatch" })
            {
                bool exported = false;
                foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
                {
                    TypeDefinition type = metadata.GetTypeDefinition(handle);
                    if (metadata.GetString(type.Name) != patch) continue;
                    exported = (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
                    break;
                }
                True(exported); // GadgetLoader uses Assembly.GetExportedTypes().
            }
        }
    }

    private static void Near(float expected, float actual)
    {
        if (Math.Abs(expected - actual) > 0.0001f)
            throw new Exception("Expected " + expected + ", got " + actual + ".");
        checks++;
    }

    private static void Reject(string text, string label)
    {
        try { BindingConfig.Parse(text.Split('\n')); }
        catch (FormatException) { checks++; return; }
        throw new Exception("Expected rejection: " + label);
    }

    private static void RejectLoad(string path, string label)
    {
        try { BindingConfig.Load(path); }
        catch (FormatException) { checks++; return; }
        throw new Exception("Expected rejection: " + label);
    }

    private static void Equal(string expected, string actual)
    {
        if (expected != actual) throw new Exception("Expected '" + expected + "', got '" + actual + "'.");
        checks++;
    }

    private static void True(bool value)
    {
        if (!value) throw new Exception("Expected true.");
        checks++;
    }

    private static void False(bool value)
    {
        if (value) throw new Exception("Expected false.");
        checks++;
    }
}
