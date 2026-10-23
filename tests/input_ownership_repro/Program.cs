using System;
using ControllerSupport;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new Exception(description);
    }

    private static RawJoystickEvent Event(byte type, byte number, short value, bool initial = false)
    {
        return new RawJoystickEvent { Type = type, Number = number, Value = value, Initial = initial };
    }

    private static void Main()
    {
        // No positive-fixture initializer: this exercises production's actual
        // default, then the same live delegate shape used by Gadget/InputProbe.
        Check(!ControllerInputOwnership.UsingController, "unconfigured production policy defaults to keyboard/mouse");
        ControllerInputOwnership.Reset();
        Check(!ControllerInputOwnership.UsingController, "explicit reset defaults to keyboard/mouse");
        ControllerInputOwnership.Configure(null);
        Check(!ControllerInputOwnership.UsingController, "null provider is safe and grants no controller effects");

        var state = new JoystickInputState();
        var source = new LastInputSource();
        ControllerInputOwnership.Configure(() => source.UsingController);
        state.Connect();
        state.Apply(Event(1, 0, 1, true));
        state.Apply(Event(2, 0, 24000, true));
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(!ControllerInputOwnership.UsingController, "connected held controls/initialization do not acquire ownership");

        state.BeginFrame();
        state.Apply(Event(1, 1, 1));
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(ControllerInputOwnership.UsingController, "fresh controller press reaches the shared policy");
        state.BeginFrame();
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(ControllerInputOwnership.UsingController, "idle preserves last deliberate controller input");

        // There is deliberately no policy BeginFrame/Update call here: every
        // consumer must see a keyboard/mouse handoff within this same frame.
        source.Observe(false, true);
        Check(!ControllerInputOwnership.UsingController, "same-frame keyboard/mouse input immediately revokes controller effects");
        state.Apply(Event(1, 1, 1));
        state.Apply(Event(2, 0, 24020));
        Check(!state.MeaningfulInputThisFrame, "held button and small axis noise are not fresh intent");
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(!ControllerInputOwnership.UsingController, "raw held/noisy controller state cannot reclaim keyboard/mouse ownership");

        state.BeginFrame();
        state.Apply(Event(1, 2, 1));
        source.Observe(state.MeaningfulInputThisFrame, true);
        Check(!ControllerInputOwnership.UsingController, "simultaneous physical input favors keyboard/mouse");
        state.BeginFrame();
        state.Apply(Event(1, 2, 0));
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(!ControllerInputOwnership.UsingController, "button release alone cannot reclaim controller ownership");
        state.BeginFrame();
        state.Apply(Event(1, 2, 1));
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(ControllerInputOwnership.UsingController, "new press after handoff reacquires controller effects");

        source.Reset();
        Check(!ControllerInputOwnership.UsingController, "source reset is visible without a policy refresh");
        source.Observe(true, false);
        Check(ControllerInputOwnership.UsingController, "live source can acquire again");
        ControllerInputOwnership.Reset();
        Check(!ControllerInputOwnership.UsingController, "unload reset removes a previously true source");
        source.Observe(true, false);
        Check(!ControllerInputOwnership.UsingController, "old source cannot re-enable effects after policy reset");

        var replacement = new LastInputSource();
        ControllerInputOwnership.Configure(() => replacement.UsingController);
        Check(!ControllerInputOwnership.UsingController, "replacement source has no stale previous ownership");
        replacement.Observe(true, false);
        Check(ControllerInputOwnership.UsingController, "replacement provider remains live");
        ControllerInputOwnership.Reset();
        Console.WriteLine("Input ownership: " + checks + " checks passed.");
    }
}
