using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ControllerSupport
{
    // Stable logical actions shared by the diagnostic and gameplay modules.
    internal sealed class BindingConfig
    {
        internal static readonly string[] Actions = {
            "Move", "AimTargetOrMenuSelection", "Jump", "PrimaryAction", "Dash",
            "CloseAim", "CombatMode", "Interact", "Inventory", "Pause",
            "MenuActivate", "MenuSecondary", "MenuTransfer", "MenuDrop", "MenuCancel",
            "AimAssist", "Hotkey1", "Hotkey2", "Hotkey3", "Hotkey4", "Hotkey5", "Hotkey6"
        };

        private static readonly string[] MenuActions = {
            "MenuActivate", "MenuSecondary", "MenuTransfer", "MenuDrop", "MenuCancel", "Inventory", "Pause"
        };

        private static readonly string[] GameplayActions = {
            "Jump", "PrimaryAction", "Dash", "CloseAim", "CombatMode", "Interact", "Inventory", "Pause",
            "AimAssist", "Hotkey1", "Hotkey2", "Hotkey3", "Hotkey4", "Hotkey5", "Hotkey6"
        };

        private static readonly string[] DPadTokens = { "DPadUp", "DPadRight", "DPadDown", "DPadLeft" };

        internal const string DefaultText =
            "; Controller Support bindings. Edit while the game is closed.\n" +
            "; Joystick1Button0 through Joystick1Button19 identify physical buttons.\n" +
            "; LeftTrigger and RightTrigger are analog trigger tokens on the tested USB pad.\n" +
            "; DPadUp, DPadRight, DPadDown, and DPadLeft use tested raw axes 6/7.\n" +
            "; Use the probe log to calibrate a different controller.\n" +
            "; Sticks are fixed aliases in this first version.\n" +
            "[Bindings]\n" +
            "Move = LeftStick\n" +
            "AimTargetOrMenuSelection = RightStick\n" +
            "Jump = LeftTrigger\n" +
            "PrimaryAction = RightTrigger\n" +
            "Dash = Joystick1Button4\n" +
            "CloseAim = Joystick1Button10\n" +
            "CombatMode = Joystick1Button3\n" +
            "Interact = Joystick1Button2\n" +
            "Inventory = Joystick1Button6\n" +
            "Pause = Joystick1Button7\n" +
            "MenuActivate = Joystick1Button0\n" +
            "MenuSecondary = Joystick1Button2\n" +
            "MenuTransfer = Joystick1Button3\n" +
            "MenuDrop = Joystick1Button5\n" +
            "MenuCancel = Joystick1Button1\n" +
            "AimAssist = Joystick1Button9\n" +
            "Hotkey1 = DPadUp\n" +
            "Hotkey2 = DPadRight\n" +
            "Hotkey3 = DPadDown\n" +
            "Hotkey4 = DPadLeft\n" +
            "Hotkey5 = Joystick1Button0\n" +
            "Hotkey6 = Joystick1Button1\n";

        private readonly Dictionary<string, string> values;

        private BindingConfig(Dictionary<string, string> values)
        {
            this.values = values;
        }

        internal string this[string action] { get { return values[action]; } }

        internal bool Held(string action, JoystickInputState input)
        {
            if (input == null || !input.Connected) return false;
            string token = values[action];
            if (token == "LeftTrigger") return input.LeftTriggerHeld;
            if (token == "RightTrigger") return input.RightTriggerHeld;
            if (Array.IndexOf(DPadTokens, token) >= 0) return input.DPadHeld(token);
            int button;
            return TryButton(token, out button) && input.ButtonHeld(button);
        }

        internal bool Pressed(string action, JoystickInputState input)
        {
            if (input == null || !input.Connected) return false;
            string token = values[action];
            if (token == "LeftTrigger") return input.LeftTriggerPressed;
            if (token == "RightTrigger") return input.RightTriggerPressed;
            if (Array.IndexOf(DPadTokens, token) >= 0) return input.DPadPressed(token);
            int button;
            return TryButton(token, out button) && input.ButtonPressed(button);
        }

        internal string Label(string action)
        {
            string token = values[action];
            if (token == "LeftTrigger") return "LT";
            if (token == "RightTrigger") return "RT";
            if (Array.IndexOf(DPadTokens, token) >= 0) return "D-pad " + token.Substring(4);
            int button;
            if (!TryButton(token, out button)) return token;
            switch (button)
            {
                case 0: return "A";
                case 1: return "B";
                case 2: return "X";
                case 3: return "Y";
                case 4: return "LB";
                case 5: return "RB";
                case 6: return "View";
                case 7: return "Menu";
                case 9: return "L3";
                case 10: return "R3";
                default: return "Button " + button.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static bool TryButton(string token, out int button)
        {
            const string prefix = "Joystick1Button";
            button = -1;
            return token.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(token.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out button);
        }

        internal static BindingConfig Load(string path)
        {
            // This dedicated file differs from GadgetCore's assembly-named config.
            // Existing files, including empty or invalid ones, are never rewritten.
            if (!File.Exists(path))
            {
                using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(file)) writer.Write(DefaultText);
            }
            return Parse(File.ReadAllLines(path));
        }

        internal static BindingConfig SafeDefaults()
        {
            // Invalid user configuration must never silently enable controls.
            Dictionary<string, string> defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string action in Actions)
                defaults.Add(action, action == "Move" ? "LeftStick" :
                    action == "AimTargetOrMenuSelection" ? "RightStick" : "Unbound");
            return new BindingConfig(defaults);
        }

        internal static BindingConfig Parse(string[] lines)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool inBindings = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    inBindings = line.Equals("[Bindings]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inBindings) continue; // GadgetCore may add its own section.
                int equals = line.IndexOf('=');
                if (equals <= 0) throw new FormatException("Line " + (i + 1) + ": expected Action = Input.");
                string action = line.Substring(0, equals).Trim();
                string token = line.Substring(equals + 1).Trim();
                if (Array.Find(Actions, name => name.Equals(action, StringComparison.OrdinalIgnoreCase)) == null)
                    throw new FormatException("Line " + (i + 1) + ": unknown action '" + action + "'.");
                if (values.ContainsKey(action)) throw new FormatException("Line " + (i + 1) + ": duplicate action '" + action + "'.");
                if (action.Equals("Move", StringComparison.OrdinalIgnoreCase) || action.Equals("AimTargetOrMenuSelection", StringComparison.OrdinalIgnoreCase))
                {
                    string required = action.Equals("Move", StringComparison.OrdinalIgnoreCase) ? "LeftStick" : "RightStick";
                    if (!token.Equals(required, StringComparison.OrdinalIgnoreCase))
                        throw new FormatException("Line " + (i + 1) + ": " + action + " must be " + required + " in this version.");
                    token = required;
                }
                else if (token.Equals("LeftTrigger", StringComparison.OrdinalIgnoreCase) ||
                         token.Equals("RightTrigger", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Equals("LeftTrigger", StringComparison.OrdinalIgnoreCase) ? "LeftTrigger" : "RightTrigger";
                }
                else if (Array.Find(DPadTokens, name => name.Equals(token, StringComparison.OrdinalIgnoreCase)) != null)
                {
                    token = Array.Find(DPadTokens, name => name.Equals(token, StringComparison.OrdinalIgnoreCase));
                }
                else if (!token.Equals("Unbound", StringComparison.OrdinalIgnoreCase))
                {
                    const string prefix = "Joystick1Button";
                    int button;
                    if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        !int.TryParse(token.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out button) ||
                        button < 0 || button > 19)
                        throw new FormatException("Line " + (i + 1) + ": '" + token + "' must be Unbound, LeftTrigger, RightTrigger, DPadUp/Right/Down/Left, or Joystick1Button0..19.");
                    token = prefix + button.ToString(CultureInfo.InvariantCulture);
                }
                else token = "Unbound";
                values.Add(action, token);
            }

            // Legacy files predate these actions. Add defaults only in memory;
            // explicitly configured Unbound and existing file bytes are preserved.
            AddLegacyAction(values, "MenuTransfer", "Joystick1Button3", MenuActions);
            AddLegacyAction(values, "MenuDrop", "Joystick1Button5", MenuActions);
            AddLegacyAction(values, "MenuCancel", "Joystick1Button1", MenuActions);
            AddLegacyAction(values, "AimAssist", "Joystick1Button9", GameplayActions);
            AddLegacyAction(values, "Hotkey1", "DPadUp", GameplayActions);
            AddLegacyAction(values, "Hotkey2", "DPadRight", GameplayActions);
            AddLegacyAction(values, "Hotkey3", "DPadDown", GameplayActions);
            AddLegacyAction(values, "Hotkey4", "DPadLeft", GameplayActions);
            AddLegacyAction(values, "Hotkey5", "Joystick1Button0", GameplayActions);
            AddLegacyAction(values, "Hotkey6", "Joystick1Button1", GameplayActions);
            foreach (string action in Actions)
                if (!values.ContainsKey(action)) throw new FormatException("Missing [Bindings] action '" + action + "'.");

            // Same-context conflicts are unsafe. Reuse across contexts remains legal.
            CheckContext(values, "gameplay", GameplayActions);
            CheckContext(values, "menu", MenuActions);
            return new BindingConfig(values);
        }

        private static void AddLegacyAction(Dictionary<string, string> values, string action, string defaultToken, string[] contextActions)
        {
            if (values.ContainsKey(action)) return;
            // A new default must not invalidate a previously valid custom mapping.
            // Explicit values (including Unbound) always take precedence.
            foreach (string contextAction in contextActions)
            {
                string token;
                if (values.TryGetValue(contextAction, out token) && token == defaultToken)
                {
                    values.Add(action, "Unbound");
                    return;
                }
            }
            values.Add(action, defaultToken);
        }

        private static void CheckContext(Dictionary<string, string> values, string context, params string[] actions)
        {
            Dictionary<string, string> used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string action in actions)
            {
                string token = values[action];
                if (token == "Unbound") continue;
                string other;
                if (used.TryGetValue(token, out other))
                    throw new FormatException("Same-" + context + " collision: " + other + " and " + action + " both use " + token + ".");
                used.Add(token, action);
            }
        }
    }
}
