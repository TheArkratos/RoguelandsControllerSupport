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
        var state = new JoystickInputState();
        var source = new LastInputSource();
        state.Connect();
        state.Apply(Event(1, 0, 1, true));
        state.Apply(Event(2, 0, 28000, true));
        state.Apply(Event(2, 2, 20000, true));
        Check(!state.MeaningfulInputThisFrame, "Linux initialization is not deliberate input");
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(!source.UsingController, "initial state leaves keyboard prompt");

        state.BeginFrame();
        state.Apply(Event(1, 0, 1));
        Check(!state.MeaningfulInputThisFrame, "held repeated button is ignored");
        state.Apply(Event(2, 0, 28030));
        Check(!state.MeaningfulInputThisFrame, "stick noise is ignored");
        state.Apply(Event(2, 2, 20020));
        Check(!state.MeaningfulInputThisFrame, "trigger noise is ignored");

        state.BeginFrame();
        state.Apply(Event(1, 0, 0));
        Check(!state.MeaningfulInputThisFrame, "button release is ignored");
        state.Apply(Event(1, 0, 1));
        Check(state.MeaningfulInputThisFrame, "new button press is activity");
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(source.UsingController, "button switches to controller prompt");

        state.BeginFrame();
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(source.UsingController, "idle holds last source");
        state.Apply(Event(2, 0, 20000));
        Check(state.MeaningfulInputThisFrame, "deliberate stick movement counts");
        source.Observe(state.MeaningfulInputThisFrame, true);
        Check(!source.UsingController, "physical keyboard/mouse wins simultaneous input");

        state.BeginFrame();
        state.Apply(Event(2, 0, 0));
        Check(state.MeaningfulInputThisFrame, "return to center counts");
        source.Observe(state.MeaningfulInputThisFrame, false);
        Check(source.UsingController, "controller can retake prompt");
        source.Observe(false, true);
        Check(!source.UsingController, "keyboard/mouse can retake prompt");

        state.BeginFrame();
        state.Apply(Event(2, 6, 32767));
        Check(state.MeaningfulInputThisFrame, "D-pad axis counts");
        state.BeginFrame();
        state.Apply(Event(2, 5, short.MaxValue));
        Check(state.MeaningfulInputThisFrame, "trigger press counts");
        state.Clear();
        Check(!state.MeaningfulInputThisFrame, "disconnect clears activity");
        source.Reset();
        Check(!source.UsingController, "reset returns keyboard prompt");
        Console.WriteLine("Last input source: " + checks + " checks passed.");
    }
}
