namespace ControllerSupport
{
    // Linux joystick events are eight little-endian bytes (linux/joystick.h).
    internal struct RawJoystickEvent
    {
        internal uint TimeMilliseconds;
        internal short Value;
        internal byte Type;
        internal byte Number;
        internal bool Initial;

        internal static bool TryParse(byte[] bytes, out RawJoystickEvent result)
        {
            result = new RawJoystickEvent();
            if (bytes == null || bytes.Length != 8) return false;
            byte type = (byte)(bytes[6] & 0x7f);
            if (type != 1 && type != 2) return false;
            result.TimeMilliseconds = (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24);
            result.Value = unchecked((short)(bytes[4] | bytes[5] << 8));
            result.Type = type;
            result.Number = bytes[7];
            result.Initial = (bytes[6] & 0x80) != 0;
            return true;
        }

        internal static bool ShouldLogAxis(int previousLoggedValue, short value)
        {
            if (previousLoggedValue == int.MinValue) return true;
            // ~12.5% of travel, or entering/leaving the centered deadzone.
            if (System.Math.Abs(value - previousLoggedValue) >= 4096) return true;
            return (System.Math.Abs(previousLoggedValue) <= 3000) != (System.Math.Abs((int)value) <= 3000);
        }
    }
}
