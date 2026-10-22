using System;
using ControllerSupport;

internal static class Program
{
    private static int checks;
    private static JoystickInputState pad;
    private static void Main()
    {
        Fresh();
        Axis(7, -32767, 10);
        Check(pad.DPadPressed("DPadUp"), "up press");
        Check(pad.DPadHeld("DPadUp"), "up held");
        pad.BeginFrame();
        Check(!pad.DPadPressed("DPadUp"), "no held repeat");
        Axis(6, 32767, 20);
        Check(!pad.DPadPressed("DPadRight"), "rolling into diagonal cannot activate right");
        Axis(7, 0, 30);
        Check(!pad.DPadPressed("DPadRight"), "rolling into right cannot activate right");
        Axis(6, -32767, 40);
        Check(!pad.DPadPressed("DPadLeft"), "switch axis without neutral cannot activate left");
        Axis(6, 0, 50);
        Check(!pad.DPadHeld("DPadUp"), "neutral releases first direction");
        Axis(6, -32767, 60);
        Check(pad.DPadPressed("DPadLeft"), "neutral rearms left");
        Fresh();
        Axis(6, 32767, 10);
        Axis(7, -32767, 11);
        Check(pad.DPadPressed("DPadRight"), "ordered raw events preserve first right");
        Check(!pad.DPadPressed("DPadUp"), "ordered diagonal does not add up");
        Fresh();
        Axis(6, 32767, 10);
        Axis(7, -32767, 10);
        Check(!pad.DPadPressed("DPadRight") && !pad.DPadPressed("DPadUp"), "simultaneous diagonal waits");
        Axis(6, 0, 20);
        Check(pad.DPadPressed("DPadUp"), "ambiguous diagonal resolves to cardinal");
        Fresh();
        Axis(7, -32767, 10, true);
        Check(!pad.DPadPressed("DPadUp") && !pad.DPadHeld("DPadUp"), "initial held direction suppressed");
        Axis(6, 32767, 20);
        Axis(7, 0, 30);
        Check(!pad.DPadPressed("DPadRight"), "initial hold cannot roll to activation");
        Axis(6, 0, 40);
        Axis(7, 32767, 50);
        Check(pad.DPadPressed("DPadDown"), "initial hold rearmed after complete neutral");
        pad.Clear();
        Check(!pad.DPadHeld("DPadDown") && !pad.DPadPressed("DPadDown"), "disconnect clears latch and edges");
        Axis(7, -32767, 60);
        Check(!pad.DPadPressed("DPadUp"), "disconnected events ignored");
        pad.Connect();
        Axis(7, 32767, 70, true);
        Check(!pad.DPadPressed("DPadDown"), "reconnect initial hold suppressed");
        Axis(7, 0, 80);
        Axis(7, -32767, 90);
        Check(pad.DPadPressed("DPadUp"), "reconnect neutral rearms");
        Fresh();
        Axis(6, 3000, 10);
        Check(!pad.DPadPressed("DPadRight"), "axis noise not a direction");
        Axis(6, 32767, 20);
        Check(pad.MeaningfulInputThisFrame && pad.DPadPressed("DPadRight"), "fresh D-pad input claims prompts");
        pad.BeginFrame();
        Check(!pad.MeaningfulInputThisFrame, "held D-pad not fresh input");
        BindingConfig defaults = BindingConfig.Parse(BindingConfig.DefaultText.Split('\n'));
        Check(defaults.Held("Hotkey2", pad), "binding held uses latch");
        Check(!defaults.Pressed("Hotkey2", pad), "binding press does not repeat");
        Console.WriteLine("D-pad raw input: " + checks + " checks passed.");
    }
    private static void Fresh() { pad = new JoystickInputState(); pad.Connect(); pad.BeginFrame(); }
    private static void Axis(byte axis, short value, uint time, bool initial = false)
    {
        pad.Apply(new RawJoystickEvent { Type = 2, Number = axis, Value = value, TimeMilliseconds = time, Initial = initial });
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
