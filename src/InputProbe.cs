using System;
using System.Collections.Generic;
using GadgetCore;
using UnityEngine;

namespace ControllerSupport
{
    internal sealed class InputProbe : IDisposable
    {
        private static readonly string[] AxisNames = { "Horizontal", "Vertical", "Horizontal1", "Vertical1", "Attack" };
        private readonly GadgetLogger logger;
        private readonly BindingConfig bindings;
        private readonly RawLinuxJoystickProbe rawLinuxProbe;
        private readonly bool diagnostics;
        private readonly LastInputSource lastInputSource = new LastInputSource();
        private readonly KeyCode[] physicalKeyboardKeys;
        private readonly KeyCode[] buttons = new KeyCode[20];
        private readonly HashSet<string> missingAxes = new HashSet<string>();
        private readonly Dictionary<string, float> lastAxis = new Dictionary<string, float>();
        private readonly Dictionary<string, float> lastAxisLogTime = new Dictionary<string, float>();
        private string joystickName = null;
        private float nextNamePoll;
        private int lastSourceFrame = -1;
        private Vector3 previousMousePosition;
        private bool hasMousePosition;

        internal InputProbe(GadgetLogger logger, BindingConfig bindings, bool diagnostics = false)
        {
            this.logger = logger;
            this.bindings = bindings;
            this.diagnostics = diagnostics;
            rawLinuxProbe = new RawLinuxJoystickProbe(logger, diagnostics);
            if (diagnostics)
                for (int i = 0; i < buttons.Length; i++)
                    buttons[i] = (KeyCode)Enum.Parse(typeof(KeyCode), "Joystick1Button" + i);
            List<KeyCode> keys = new List<KeyCode>();
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                string name = key.ToString();
                if (key == KeyCode.None || name.StartsWith("Joystick", StringComparison.Ordinal) ||
                    name.StartsWith("Mouse", StringComparison.Ordinal)) continue;
                keys.Add(key);
            }
            physicalKeyboardKeys = keys.ToArray();
        }

        internal bool UsingController
        {
            get { PollState(); return lastInputSource.UsingController; }
        }

        internal JoystickInputState PollState()
        {
            // Game methods may update before ProbeRunner in a Unity frame.
            rawLinuxProbe.Update();
            if (lastSourceFrame != Time.frameCount)
            {
                lastSourceFrame = Time.frameCount;
                lastInputSource.Observe(rawLinuxProbe.State.MeaningfulInputThisFrame,
                    PhysicalKeyboardOrMouseActivity());
            }
            return rawLinuxProbe.State;
        }

        private bool PhysicalKeyboardOrMouseActivity()
        {
            Vector3 mouse = Input.mousePosition;
            bool active = hasMousePosition && (mouse - previousMousePosition).sqrMagnitude > 0.25f;
            previousMousePosition = mouse;
            hasMousePosition = true;
            for (int button = 0; button < 3; button++)
                if (Input.GetMouseButtonDown(button)) active = true;
            for (int i = 0; i < physicalKeyboardKeys.Length; i++)
                if (Input.GetKeyDown(physicalKeyboardKeys[i])) active = true;
            return active;
        }

        internal void Update()
        {
            PollState();
            if (Time.unscaledTime >= nextNamePoll)
            {
                nextNamePoll = Time.unscaledTime + 5f;
                try
                {
                    string[] names = Input.GetJoystickNames();
                    string name = names != null && names.Length > 0 && !string.IsNullOrEmpty(names[0]) ? names[0] : "(none on joystick 1)";
                    if (name != joystickName)
                    {
                        joystickName = name;
                        logger.Log("Probe joystick 1: " + name + ". Other controllers ignored.");
                    }
                }
                catch (Exception error)
                {
                    logger.LogWarning("Probe cannot read joystick name: " + error.Message, false);
                    nextNamePoll = float.PositiveInfinity;
                }
            }

            // Raw polling and device ownership above are functional. Legacy-axis
            // and button enumeration below are calibration diagnostics only.
            if (!diagnostics) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool pressed = Input.GetKeyDown(buttons[i]);
                bool released = Input.GetKeyUp(buttons[i]);
                if (!pressed && !released) continue;
                string token = "Joystick1Button" + i;
                List<string> actions = new List<string>();
                foreach (string action in BindingConfig.Actions)
                    if (bindings[action] == token) actions.Add(action);
                logger.Log("Probe " + token + " " + (pressed ? "down" : "up") +
                    (actions.Count == 0 ? " (unbound)" : " -> " + string.Join(", ", actions.ToArray())));
            }

            // Probe candidate InputManager names, not guaranteed raw joystick-only signals.
            foreach (string axis in AxisNames)
            {
                if (missingAxes.Contains(axis)) continue;
                float value;
                try { value = Input.GetAxisRaw(axis); }
                catch (Exception error)
                {
                    missingAxes.Add(axis);
                    logger.LogWarning("Probe axis '" + axis + "' unavailable: " + error.Message, false);
                    continue;
                }
                float old;
                if (!lastAxis.TryGetValue(axis, out old)) { lastAxis[axis] = value; continue; }
                float oldLogTime;
                if (!lastAxisLogTime.TryGetValue(axis, out oldLogTime)) oldLogTime = -10f;
                bool changed = Mathf.Abs(value - old) >= 0.25f;
                bool crossedDeadzone = (Mathf.Abs(old) <= 0.15f) != (Mathf.Abs(value) <= 0.15f);
                if ((changed || crossedDeadzone) && Time.unscaledTime - oldLogTime >= 0.35f)
                {
                    logger.Log("Probe axis " + axis + " = " + value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                    lastAxisLogTime[axis] = Time.unscaledTime;
                    lastAxis[axis] = value;
                }
            }
        }

        public void Dispose()
        {
            rawLinuxProbe.Dispose();
            lastInputSource.Reset();
        }
    }
}
