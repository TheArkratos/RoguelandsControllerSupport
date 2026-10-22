namespace ControllerSupport
{
    // Pure arbitration for prompt glyphs. Once a device takes ownership,
    // inactivity does not switch it back; simultaneous input favors KB/mouse.
    internal sealed class LastInputSource
    {
        internal bool UsingController { get; private set; }

        internal void Observe(bool controllerActivity, bool keyboardMouseActivity)
        {
            if (keyboardMouseActivity) UsingController = false;
            else if (controllerActivity) UsingController = true;
        }

        internal void Reset() { UsingController = false; }
    }
}
