using System;
using ControllerSupport;
using GadgetCore;
using UnityEngine;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void NextFrame()
    {
        Time.frameCount++;
        Time.unscaledTime = Time.frameCount / 60f;
        Input.KeysDown.Clear();
        Input.MouseDown.Clear();
    }

    private static void Main()
    {
        var api = new FakeApi();
        // An already-held button, trigger and stick must be visible but quiet.
        api.Buttons[0] = true;
        api.Axes[0] = short.MaxValue;
        api.Axes[4] = short.MaxValue;
        var backend = new SdlControllerBackend(api, message => { });
        var bindings = BindingConfig.Parse(BindingConfig.DefaultText.Split('\n'));
        using (var probe = new InputProbe(new GadgetLogger(), bindings, false, backend))
        {
            NextFrame();
            var state = probe.PollState(); // Game caller runs before ProbeRunner.Update.
            Check(state.Connected && state.ButtonHeld(0) && state.LeftTriggerHeld && state.MoveX == 1f,
                "initial mapped snapshot exposes held state");
            Check(!state.ButtonPressed(0) && !state.LeftTriggerPressed && !state.MeaningfulInputThisFrame,
                "initial snapshot does not generate edges or ownership activity");
            Check(!probe.UsingController, "initial held snapshot does not claim controller ownership");
            probe.Update();
            Check(api.Updates == 1 && api.Initializations == 1 && api.Opens == 1,
                "early game poll, ownership read and runner update share one snapshot");

            NextFrame();
            api.Buttons[0] = false;
            api.Axes[0] = 0;
            api.Axes[4] = 0;
            probe.Update();
            Check(state.ButtonReleased(0) && state.LeftTriggerReleased, "release edges reach real probe state");

            NextFrame();
            api.Buttons[0] = true;
            api.Axes[4] = short.MaxValue;
            state = probe.PollState();
            int polls = api.Updates;
            int reads = api.SnapshotReads;
            Check(state.ButtonPressed(0) && state.LeftTriggerPressed && bindings.Pressed("MenuActivate", state),
                "fresh mapped button and trigger reach configured action edges");
            Check(probe.UsingController, "meaningful mapped press takes ownership before runner update");
            probe.Update();
            Check(ReferenceEquals(state, probe.PollState()), "all callers receive the authoritative state object");
            Check(api.Updates == polls && api.SnapshotReads == reads,
                "same-frame polling and runner update neither poll nor reread snapshots");
            Check(state.ButtonPressed(0) && state.LeftTriggerPressed && state.MeaningfulInputThisFrame,
                "same-frame calls preserve edges and meaningful activity");

            NextFrame();
            probe.PollState();
            Check(api.Updates == polls + 1, "new frame polls exactly once");
            Check(!state.ButtonPressed(0) && !state.LeftTriggerPressed && !state.MeaningfulInputThisFrame,
                "new held snapshot clears previous frame edges and activity");
            Check(state.ButtonHeld(0) && state.LeftTriggerHeld && probe.UsingController,
                "held state and sticky ownership survive idle frame");

            NextFrame();
            Input.KeysDown.Add(KeyCode.Space);
            probe.PollState();
            probe.Update();
            Check(!probe.UsingController, "physical keyboard takes ownership while mapped controls remain held");
            NextFrame();
            probe.Update();
            Check(!probe.UsingController, "unchanged held controller does not retake ownership");

            NextFrame();
            api.Buttons[1] = true;
            Check(probe.UsingController && state.ButtonPressed(1), "fresh mapped press retakes keyboard ownership");
            NextFrame();
            Input.mousePosition = new Vector3(2f, 0f, 0f);
            probe.Update();
            Check(!probe.UsingController, "mouse movement takes ownership");
            NextFrame();
            api.Buttons[2] = true;
            Check(probe.UsingController, "fresh mapped press retakes mouse ownership");
            NextFrame();
            Input.MouseDown.Add(0);
            api.Buttons[3] = true;
            Check(!probe.UsingController && state.ButtonPressed(3), "simultaneous mouse click wins over mapped press");
            NextFrame();
            Input.KeysDown.Add(KeyCode.Joystick1Button0);
            Input.KeysDown.Add(KeyCode.Mouse0);
            api.Buttons[9] = true;
            Check(probe.UsingController && state.ButtonPressed(4),
                "SDL bumper uses canonical mapping; Unity joystick/mouse key aliases do not claim keyboard ownership");

            Check(Input.AxisReads == 0 && Input.KeyUpReads == 0 && Input.JoystickKeyReads == 0,
                "diagnostics disabled avoids Unity axis and joystick button probes");
            polls = api.Updates;
            reads = api.SnapshotReads;
            probe.Dispose();
            Check(!state.Connected && !state.ButtonHeld(0) && !probe.UsingController,
                "probe disposal clears state and ownership");
            NextFrame();
            probe.PollState();
            probe.Update();
            Check(api.Updates == polls && api.SnapshotReads == reads,
                "disposed probe does not restart backend or read snapshots");
            Check(api.Closes == 1 && api.Disposals == 1, "disposal closes fake controller and API once");
        }
        Check(api.Disposals == 1, "repeated probe disposal remains idempotent");
        Console.WriteLine("Input probe: " + checks + " checks passed.");
    }

    private sealed class FakeApi : ISdlControllerApi
    {
        internal readonly short[] Axes = new short[6];
        internal readonly bool[] Buttons = new bool[15];
        internal int Initializations, Updates, Opens, SnapshotReads, Closes, Disposals;
        public void Initialize() { Initializations++; }
        public void Update() { Updates++; }
        public int Count { get { return 1; } }
        public bool IsController(int index) { return index == 0; }
        public IntPtr Open(int index) { Opens++; return new IntPtr(1); }
        public bool Attached(IntPtr controller) { return true; }
        public short Axis(IntPtr controller, int axis) { SnapshotReads++; return Axes[axis]; }
        public bool Button(IntPtr controller, int button) { SnapshotReads++; return Buttons[button]; }
        public void Close(IntPtr controller) { Closes++; }
        public void Dispose() { Disposals++; }
    }
}
