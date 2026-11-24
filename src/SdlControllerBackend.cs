using System;

namespace ControllerSupport
{
    // Only this adapter produces controller state when SDL is selected. Never
    // merge its snapshots with Unity joystick input or the legacy Linux reader.
    internal interface ISdlControllerApi : IDisposable
    {
        void Initialize();
        void Update();
        int Count { get; }
        bool IsController(int index);
        IntPtr Open(int index);
        bool Attached(IntPtr controller);
        short Axis(IntPtr controller, int axis);
        bool Button(IntPtr controller, int button);
        void Close(IntPtr controller);
    }

    internal sealed class SdlControllerBackend : IDisposable
    {
        private readonly ISdlControllerApi api;
        private readonly Action<string> log;
        private readonly MappedControllerState mapped = new MappedControllerState();
        private readonly short[] axes = new short[6];
        private readonly bool[] buttons = new bool[15];
        private IntPtr controller;
        private bool initialized;
        private bool stopped;
        private bool observedActivity;
        private string status;

        internal SdlControllerBackend(ISdlControllerApi api, Action<string> log)
        {
            this.api = api;
            this.log = log;
        }

        internal JoystickInputState State { get { return mapped.State; } }

        // Caller polls once per Unity frame; no Unity or Steam event loop is run.
        internal void Update(uint timestamp)
        {
            if (stopped) return;
            try
            {
                if (!initialized)
                {
                    api.Initialize();
                    initialized = true;
                    Status("SDL2 mapped controller backend initialized; Steam device filters unchanged.");
                }
                api.Update();
                if (controller != IntPtr.Zero && !api.Attached(controller))
                {
                    api.Close(controller);
                    controller = IntPtr.Zero;
                    mapped.Disconnect();
                    observedActivity = false;
                    Status("SDL2 controller disconnected; waiting for a mapped gamepad.");
                }
                if (controller == IntPtr.Zero)
                {
                    int count = api.Count;
                    for (int i = 0; i < count && controller == IntPtr.Zero; i++)
                        if (api.IsController(i)) controller = api.Open(i);
                    if (controller == IntPtr.Zero)
                    {
                        Status("SDL2 sees " + count + " joystick(s), no open mapped gamepad; waiting. Check Steam Input gamepad layout and device visibility.");
                        return;
                    }
                    Status("SDL2 mapped gamepad opened; other controllers ignored until disconnect.");
                }
                for (int i = 0; i < axes.Length; i++) axes[i] = api.Axis(controller, i);
                for (int i = 0; i < buttons.Length; i++) buttons[i] = api.Button(controller, i);
                mapped.Apply(axes, buttons, timestamp);
                if (!observedActivity && mapped.State.MeaningfulInputThisFrame)
                {
                    observedActivity = true;
                    Status("SDL2 mapped input activity observed.");
                }
            }
            catch (Exception error)
            {
                // Fail closed, once. A missing/wrong-architecture native library
                // must not prevent the gadget or keyboard/mouse from loading.
                Dispose();
                Status("SDL2 backend unavailable (" + error.GetType().Name + "): " + error.Message +
                    ". No raw/Unity controller fallback is merged; keyboard/mouse remain available.");
            }
        }

        private void Status(string message)
        {
            if (message == status) return;
            status = message;
            log(message);
        }

        public void Dispose()
        {
            if (stopped) return;
            stopped = true;
            mapped.Disconnect();
            try
            {
                if (controller != IntPtr.Zero) api.Close(controller);
            }
            finally
            {
                controller = IntPtr.Zero;
                api.Dispose();
            }
        }
    }
}
