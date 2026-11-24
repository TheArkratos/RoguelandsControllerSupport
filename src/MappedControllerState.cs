namespace ControllerSupport
{
    internal sealed class MappedControllerState
    {
        internal readonly JoystickInputState State = new JoystickInputState();
        // Canonical Xbox js button number -> SDL2 GameController button number.
        private static readonly int[] ButtonMap = { 0, 1, 2, 3, 9, 10, 4, 6, 5, 7, 8 };
        private readonly short[] previousAxes = new short[8];
        private readonly bool[] previousButtons = new bool[11];
        private bool initialized;

        // One complete SDL2 logical snapshot per frame; no native/device dependency.
        internal void Apply(short[] axes, bool[] buttons, uint timestamp)
        {
            State.BeginFrame();
            bool initial = !initialized;
            if (initial) State.Connect();

            ApplyAxis(0, axes[0], timestamp, initial);
            ApplyAxis(1, axes[1], timestamp, initial);
            ApplyAxis(2, TriggerAxis(axes[4]), timestamp, initial);
            ApplyAxis(3, axes[2], timestamp, initial);
            ApplyAxis(4, axes[3], timestamp, initial);
            ApplyAxis(5, TriggerAxis(axes[5]), timestamp, initial);
            short horizontal = DPadAxis(buttons[14], buttons[13]);
            short vertical = DPadAxis(buttons[12], buttons[11]);
            if ((buttons[14] && buttons[13]) || (buttons[12] && buttons[11]))
            {
                // Conflicting buttons are ambiguous, not neutral. Represent them
                // as a diagonal: preserve an existing latch, suppress new actions,
                // and retain the normal initial-snapshot neutral/rearm requirement.
                horizontal = short.MaxValue;
                vertical = short.MaxValue;
            }
            // These are atomic snapshots, not ordered js events. Update a nonzero
            // destination before clearing the old axis, avoiding transient neutral
            // on cardinal-to-cardinal changes. The shared timestamp still prevents
            // a simultaneous diagonal from establishing a cardinal order.
            if (horizontal != 0)
            {
                ApplyAxis(6, horizontal, timestamp, initial);
                ApplyAxis(7, vertical, timestamp, initial);
            }
            else
            {
                ApplyAxis(7, vertical, timestamp, initial);
                ApplyAxis(6, horizontal, timestamp, initial);
            }

            for (byte number = 0; number < ButtonMap.Length; number++)
            {
                bool held = buttons[ButtonMap[number]];
                if (initial || held != previousButtons[number])
                    State.Apply(new RawJoystickEvent {
                        Type = 1, Number = number, Value = (short)(held ? 1 : 0),
                        TimeMilliseconds = timestamp, Initial = initial
                    });
                previousButtons[number] = held;
            }
            initialized = true;
        }

        internal void Disconnect()
        {
            State.Clear();
            initialized = false;
        }

        private void ApplyAxis(byte number, short value, uint timestamp, bool initial)
        {
            if (initial || value != previousAxes[number])
                State.Apply(new RawJoystickEvent {
                    Type = 2, Number = number, Value = value,
                    TimeMilliseconds = timestamp, Initial = initial
                });
            previousAxes[number] = value;
        }

        private static short TriggerAxis(short value)
        {
            // SDL triggers use only the nonnegative half of a signed short.
            int clamped = value < 0 ? 0 : value;
            return (short)(clamped * 65535 / 32767 - 32768);
        }

        private static short DPadAxis(bool positive, bool negative)
        {
            return positive == negative ? (short)0 : positive ? short.MaxValue : short.MinValue;
        }
    }
}
