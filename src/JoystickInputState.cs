using System;

namespace ControllerSupport
{
    // Canonical frame state using the original Xbox/js binding positions.
    // Both Linux events and mapped SDL snapshots feed this same action model.
    // No Unity or device dependency here.
    internal sealed class JoystickInputState
    {
        private const float StickDeadzone = 0.15f;
        private const float TriggerThreshold = 0.5f;
        private readonly short[] axes = new short[8];
        private readonly bool[] buttons = new bool[20];
        private readonly bool[] pressed = new bool[20];
        private readonly bool[] released = new bool[20];
        private bool leftTriggerHeld;
        private bool rightTriggerHeld;
        private const int DPadThreshold = 16384;
        private int dpadDirection = -1;
        private int dpadPressedMask;
        private bool dpadBlockedUntilNeutral;
        private bool dpadPending;
        private uint dpadPendingTime;


        internal bool Connected { get; private set; }
        internal float MoveX { get { return Connected ? Stick(axes[0]) : 0f; } }
        internal float MoveY { get { return Connected ? Stick(axes[1]) : 0f; } }
        internal float AimX { get { return Connected ? Stick(axes[3]) : 0f; } }
        internal float AimY { get { return Connected ? Stick(axes[4]) : 0f; } }
        internal float LeftTrigger { get { return Connected ? Trigger(axes[2]) : 0f; } }
        internal float RightTrigger { get { return Connected ? Trigger(axes[5]) : 0f; } }
        internal bool LeftTriggerHeld { get { return leftTriggerHeld; } }
        internal bool RightTriggerHeld { get { return rightTriggerHeld; } }
        internal bool LeftTriggerPressed { get; private set; }
        internal bool RightTriggerPressed { get; private set; }
        internal bool LeftTriggerReleased { get; private set; }
        internal bool RightTriggerReleased { get; private set; }
        // Only fresh, deliberate activity counts for prompt ownership.
        // Held buttons and initialization snapshots do not count.
        internal bool MeaningfulInputThisFrame { get; private set; }

        internal void Connect()
        {
            Clear();
            Connected = true;
            // Linux js triggers rest at their negative endpoint. Before their
            // initialization events arrive, expose a neutral trigger state.
            axes[2] = short.MinValue;
            axes[5] = short.MinValue;
        }

        internal void BeginFrame()
        {
            ResolveDPad();
            dpadPressedMask = 0;
            Array.Clear(pressed, 0, pressed.Length);
            Array.Clear(released, 0, released.Length);
            LeftTriggerPressed = false;
            RightTriggerPressed = false;
            LeftTriggerReleased = false;
            RightTriggerReleased = false;
            MeaningfulInputThisFrame = false;
        }

        internal void Apply(RawJoystickEvent input)
        {
            if (!Connected) return;
            if (input.Type == 1 && input.Number < buttons.Length)
            {
                int number = input.Number;
                bool held = input.Value != 0;
                if (!input.Initial && held != buttons[number])
                {
                    if (held)
                    {
                        pressed[number] = true;
                        MeaningfulInputThisFrame = true;
                    }
                    else released[number] = true;
                }
                buttons[number] = held;
            }
            else if (input.Type == 2 && input.Number < axes.Length)
            {
                // Resolve a preceding timestamp before consuming this one. Events
                // sharing a Linux timestamp cannot establish diagonal order.
                if ((input.Number == 6 || input.Number == 7) && dpadPending &&
                    input.TimeMilliseconds != dpadPendingTime) ResolveDPad();
                short previous = axes[input.Number];
                if (!input.Initial && MeaningfulAxisMovement(input.Number, previous, input.Value))
                    MeaningfulInputThisFrame = true;
                axes[input.Number] = input.Value;
                if (input.Number == 2)
                    UpdateTrigger(Trigger(input.Value) >= TriggerThreshold, input.Initial, true);
                else if (input.Number == 5)
                    UpdateTrigger(Trigger(input.Value) >= TriggerThreshold, input.Initial, false);
                else if (input.Number == 6 || input.Number == 7)
                    UpdateDPad(input.Initial, input.TimeMilliseconds);
            }
        }

        internal bool ButtonHeld(int number) { return ValidButton(number) && buttons[number]; }
        internal bool ButtonPressed(int number) { return ValidButton(number) && pressed[number]; }
        internal bool ButtonReleased(int number) { return ValidButton(number) && released[number]; }

        // A D-pad actuation owns one direction until both axes return to
        // neutral. This applies to Held as well as Pressed, so remapped actions
        // cannot be activated by rolling through a diagonal.
        internal bool DPadHeld(string token)
        {
            ResolveDPad();
            int direction = DPadDirection(token);
            return Connected && direction >= 0 && dpadDirection == direction;
        }

        internal bool DPadPressed(string token)
        {
            ResolveDPad();
            int direction = DPadDirection(token);
            return Connected && direction >= 0 && (dpadPressedMask & (1 << direction)) != 0;
        }

        private void UpdateDPad(bool initial, uint time)
        {
            int horizontal = DPadAxis(axes[6]);
            int vertical = DPadAxis(axes[7]);
            if (horizontal == 0 && vertical == 0)
            {
                dpadDirection = -1;
                dpadBlockedUntilNeutral = false;
                dpadPending = false;
                return;
            }
            if (initial)
            {
                // Initialization/reconnection reports current position, not a
                // fresh press. Require a complete neutral before activation.
                dpadDirection = -1;
                dpadBlockedUntilNeutral = true;
                dpadPending = false;
                return;
            }
            if (dpadBlockedUntilNeutral || dpadDirection >= 0) return;
            dpadPending = true;
            dpadPendingTime = time;
        }

        private void ResolveDPad()
        {
            if (!dpadPending) return;
            dpadPending = false;
            if (!Connected || dpadBlockedUntilNeutral || dpadDirection >= 0) return;
            int horizontal = DPadAxis(axes[6]);
            int vertical = DPadAxis(axes[7]);
            // Simultaneous diagonal: wait for an unambiguous cardinal. Ordered
            // events are resolved before their next timestamp instead.
            if ((horizontal == 0) == (vertical == 0)) return;
            dpadDirection = horizontal > 0 ? 1 : horizontal < 0 ? 3 : vertical > 0 ? 2 : 0;
            dpadPressedMask |= 1 << dpadDirection;
        }

        private static int DPadAxis(short value)
        {
            return value >= DPadThreshold ? 1 : value <= -DPadThreshold ? -1 : 0;
        }

        private static int DPadDirection(string token)
        {
            switch (token)
            {
                case "DPadUp": return 0;
                case "DPadRight": return 1;
                case "DPadDown": return 2;
                case "DPadLeft": return 3;
                default: return -1;
            }
        }

        internal void Clear()
        {
            Connected = false;
            dpadDirection = -1;
            dpadPressedMask = 0;
            dpadBlockedUntilNeutral = false;
            dpadPending = false;
            Array.Clear(axes, 0, axes.Length);
            Array.Clear(buttons, 0, buttons.Length);
            BeginFrame();
            leftTriggerHeld = false;
            rightTriggerHeld = false;
        }

        private void UpdateTrigger(bool held, bool initial, bool left)
        {
            if (left)
            {
                if (!initial && held != leftTriggerHeld)
                {
                    if (held) LeftTriggerPressed = true;
                    else LeftTriggerReleased = true;
                }
                leftTriggerHeld = held;
            }
            else
            {
                if (!initial && held != rightTriggerHeld)
                {
                    if (held) RightTriggerPressed = true;
                    else RightTriggerReleased = true;
                }
                rightTriggerHeld = held;
            }
        }

        private static bool ValidButton(int number) { return number >= 0 && number < 20; }

        private static bool MeaningfulAxisMovement(int number, short previous, short current)
        {
            float before = number == 2 || number == 5 ? Trigger(previous) : Stick(previous);
            float after = number == 2 || number == 5 ? Trigger(current) : Stick(current);
            // Stick() removes center noise. A deliberate change must clear a
            // further travel threshold, including when returning to center.
            return Math.Abs(after - before) >= 0.10f &&
                (Math.Abs(after) >= 0.10f || Math.Abs(before) >= 0.10f);
        }

        private static float Stick(short raw)
        {
            float value = raw < 0 ? raw / 32768f : raw / 32767f;
            float absolute = Math.Abs(value);
            if (absolute <= StickDeadzone) return 0f;
            return Math.Sign(value) * (absolute - StickDeadzone) / (1f - StickDeadzone);
        }

        private static float Trigger(short raw)
        {
            return (raw + 32768f) / 65535f;
        }
    }
}
