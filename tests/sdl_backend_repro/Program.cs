using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ControllerSupport;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    private static void Main(string[] args)
    {
        var api = new FakeApi();
        var logs = new List<string>();
        var backend = new SdlControllerBackend(api, logs.Add);
        backend.Update(0);
        backend.Update(1);
        Check(!backend.State.Connected, "no pad stays disconnected");
        Check(api.Initializations == 1 && logs.Count == 2, "initialize once, deduplicate no-device logs");
        api.Devices = 3;
        backend.Update(2);
        Check(api.OpenedIndex == 1 && backend.State.Connected, "skip unmapped device and open first mapped pad");
        Check(!backend.State.MeaningfulInputThisFrame, "first snapshot is not ownership activity");
        api.Buttons[0] = true;
        backend.Update(3);
        Check(backend.State.ButtonPressed(0), "poll yields fresh mapped button edge");
        backend.Update(4);
        Check(!backend.State.ButtonPressed(0) && backend.State.ButtonHeld(0), "held snapshots do not repeat edges");
        Check(api.Opens == 1, "do not switch devices while selected pad remains attached");
        api.IsAttached = false;
        api.Devices = 0;
        backend.Update(5);
        Check(!backend.State.Connected && !backend.State.ButtonHeld(0), "disconnect clears held state");
        Check(api.Closes == 1, "close detached pad once");
        api.IsAttached = true;
        api.Devices = 2;
        backend.Update(6);
        Check(backend.State.Connected && !backend.State.ButtonPressed(0), "reconnect held button is not a new edge");
        backend.Dispose();
        backend.Dispose();
        backend.Update(7);
        Check(api.Closes == 2 && api.Disposals == 1 && !backend.State.Connected, "disposal is idempotent and stops polling");

        api = new FakeApi { FailInit = true };
        logs.Clear();
        backend = new SdlControllerBackend(api, logs.Add);
        backend.Update(0);
        backend.Update(1);
        Check(!backend.State.Connected && api.Initializations == 1 && api.Disposals == 1, "initialization error fails closed once");
        Check(logs.Count == 1 && logs[0].Contains("unavailable"), "actionable initialization failure logged once");

        api = new FakeApi { Devices = 2 };
        backend = new SdlControllerBackend(api, logs.Add);
        backend.Update(0);
        api.FailPoll = true;
        backend.Update(1);
        Check(!backend.State.Connected && api.Closes == 1 && api.Disposals == 1, "poll failure releases device and clears state");

        string oldPath = Environment.GetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY");
        try
        {
            Environment.SetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY", "/nonexistent/controller-support-test/SDL2");
            logs.Clear();
            backend = new SdlControllerBackend(new Sdl2NativeControllerApi(), logs.Add);
            backend.Update(0);
            backend.Update(1);
            Check(!backend.State.Connected && logs.Count == 1 && logs[0].Contains("DllNotFoundException"), "real native loader missing-library failure is contained");
        }
        finally { Environment.SetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY", oldPath); }

        // Explicit developer smoke only: loads SDL in this test process, not Unity.
        if (Array.IndexOf(args, "--native-smoke") >= 0)
        {
            string cache = Path.Combine(Path.GetTempPath(), "controller-sdl-smoke-" + Guid.NewGuid().ToString("N"));
            using (var native = new Sdl2NativeControllerApi(cache))
            {
                native.Initialize();
                native.Update();
                Check(native.Count >= 0, "native SDL initializes and enumerates without a Unity event loop");
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    string path = BundledSdl2.Available ? BundledSdl2.Extract(cache) :
                        Environment.GetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY") ?? "libSDL2-2.0.so.0";
                    using (var hooks = new NativeVirtualApi(path)) VirtualControllerSmoke(native, hooks);
                    if (BundledSdl2.Available) HostCoexistenceSmoke(native, path);
                }
                Console.WriteLine("Native SDL2 smoke passed; not a legacy Mono or hardware acceptance test.");
            }
        }
        Console.WriteLine("SDL backend: " + checks + " checks passed.");
    }

    private static void VirtualControllerSmoke(Sdl2NativeControllerApi native, NativeVirtualApi hooks)
    {
        int device = hooks.Attach(1, 6, 15, 0);
        Check(device >= 0, "attach process-local SDL virtual gamepad");
        IntPtr joystick = IntPtr.Zero;
        var backend = new SdlControllerBackend(new VirtualOnlyApi(native, device), Console.WriteLine);
        try
        {
            Check(native.IsController(device), "SDL recognizes virtual device as mapped gamepad");
            joystick = hooks.Open(device);
            Check(joystick != IntPtr.Zero, "open process-local virtual joystick");
            Check(hooks.Axis(joystick, 4, short.MinValue) == 0 &&
                hooks.Axis(joystick, 5, short.MinValue) == 0, "initialize virtual trigger rest positions");
            {
                backend.Update(0);
                Check(backend.State.Connected && !backend.State.MeaningfulInputThisFrame, "real SDL initial snapshot stays quiet");
                Check(hooks.Button(joystick, 0, 1) == 0, "set virtual A button");
                backend.Update(1);
                Check(backend.State.ButtonPressed(0), "real SDL virtual A reaches canonical fresh edge");
                backend.Update(2);
                Check(!backend.State.ButtonPressed(0), "real SDL held A does not repeat");
                Check(hooks.Axis(joystick, 4, short.MaxValue) == 0, "set virtual LT");
                backend.Update(3);
                Check(backend.State.LeftTriggerPressed && backend.State.LeftTrigger == 1f, "real SDL LT normalization reaches action state");
                if (BundledSdl2.Available)
                {
                    Check(hooks.ControllerEvents(-1) == 0 && hooks.JoystickEvents(-1) == 0, "private SDL input events disabled, state polling remains live");
                    for (uint frame = 5; frame < 1005; frame++)
                    {
                        hooks.Button(joystick, 0, (byte)(frame % 2));
                        backend.Update(frame);
                    }
                    Check(backend.State.ButtonHeld(0) == false, "private SDL continues updating with events disabled");
                    Check(hooks.Poll(IntPtr.Zero) == 0, "private input polling does not accumulate an event queue");
                }
                Check(hooks.Detach(device) == 0, "detach virtual gamepad");
                device = -1;
                backend.Update(4);
                Check(!backend.State.Connected && !backend.State.ButtonHeld(0), "real SDL hot-unplug clears action state");
                hooks.Close(joystick);
                joystick = IntPtr.Zero;
            }
        }
        finally
        {
            if (joystick != IntPtr.Zero) hooks.Close(joystick);
            if (device >= 0) hooks.Detach(device);
            backend.Dispose();
        }
    }

    private static void HostCoexistenceSmoke(Sdl2NativeControllerApi privateApi, string privatePath)
    {
        string old = Environment.GetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY");
        try
        {
            Environment.SetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY", "libSDL2-2.0.so.0");
            using (var host = new Sdl2NativeControllerApi())
            using (var hooks = new NativeVirtualApi("libSDL2-2.0.so.0"))
            using (var privateHooks = new NativeVirtualApi(privatePath))
            {
                host.Initialize();
                hooks.ControllerEvents(1);
                hooks.JoystickEvents(1);
                Check(privateHooks.ControllerEvents(-1) == 0 && hooks.ControllerEvents(-1) == 1,
                    "host event settings are independent of private SDL");
                uint initialized = hooks.WasInit(0);
                privateApi.Dispose();
                Check(initialized != 0 && hooks.WasInit(0) == initialized,
                    "private SDL disposal preserves host subsystem references");
                Check(hooks.ControllerEvents(-1) == 1 && hooks.JoystickEvents(-1) == 1,
                    "private SDL disposal preserves host event settings");
                Environment.SetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY", null);
                string cache = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(privatePath)));
                using (var reloaded = new Sdl2NativeControllerApi(cache))
                {
                    reloaded.Initialize();
                    reloaded.Update();
                    Check(reloaded.Count >= 0 && hooks.ControllerEvents(-1) == 1,
                        "private SDL also initializes after the host without changing host events");
                    Check(privateHooks.ControllerEvents(-1) == 0,
                        "host-first initialization keeps private event settings isolated");
                }
                Check(hooks.WasInit(0) == initialized, "host-first private unload preserves host references");
            }
        }
        finally { Environment.SetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY", old); }
    }

    // Test selection is restricted to our process-local virtual device, never a
    // physical controller. The outer using owns the real SDL subsystem reference.
    private sealed class VirtualOnlyApi : ISdlControllerApi
    {
        private readonly Sdl2NativeControllerApi native;
        private readonly int index;
        internal VirtualOnlyApi(Sdl2NativeControllerApi native, int index) { this.native = native; this.index = index; }
        public void Initialize() { }
        public void Update() { native.Update(); }
        public int Count { get { return native.Count; } }
        public bool IsController(int value) { return value == index && native.IsController(value); }
        public IntPtr Open(int value) { return native.Open(value); }
        public bool Attached(IntPtr pad) { return native.Attached(pad); }
        public short Axis(IntPtr pad, int value) { return native.Axis(pad, value); }
        public bool Button(IntPtr pad, int value) { return native.Button(pad, value); }
        public void Close(IntPtr pad) { native.Close(pad); }
        public void Dispose() { }
    }

    private sealed class FakeApi : ISdlControllerApi
    {
        internal int Devices, Initializations, Opens, Closes, Disposals, OpenedIndex = -1;
        internal bool IsAttached = true, FailInit, FailPoll;
        internal readonly bool[] Buttons = new bool[15];
        public void Initialize() { Initializations++; if (FailInit) throw new InvalidOperationException("fixture init failure"); }
        public void Update() { if (FailPoll) throw new InvalidOperationException("fixture poll failure"); }
        public int Count { get { return Devices; } }
        public bool IsController(int index) { return index != 0; }
        public IntPtr Open(int index) { Opens++; OpenedIndex = index; return new IntPtr(index + 1); }
        public bool Attached(IntPtr controller) { return IsAttached; }
        public short Axis(IntPtr controller, int index) { return 0; }
        public bool Button(IntPtr controller, int index) { return Buttons[index]; }
        public void Close(IntPtr controller) { Closes++; }
        public void Dispose() { Disposals++; }
    }
}
