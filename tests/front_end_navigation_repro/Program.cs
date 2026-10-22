using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static readonly MethodInfo step = typeof(ControllerFrontEndMenus).GetMethod("Step",
        BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo selected = typeof(ControllerFrontEndMenus).GetField("selected",
        BindingFlags.Static | BindingFlags.NonPublic);
    private static int failures;

    private static void AssertSelected(string label, Collider expected)
    {
        var actual = ControllerFrontEndMenus.SelectedCollider;
        if (actual == expected) Console.WriteLine("PASS " + label);
        else
        {
            failures++;
            Console.WriteLine("FAIL " + label + ": got " +
                (actual == null ? "none" : actual.gameObject.name) +
                ", expected " + expected.gameObject.name);
        }
    }

    private static Collider Add(GameObject root, string name, float x, float y)
    {
        var button = new GameObject(name);
        root.AddChild(button);
        return new Collider(button) { bounds = new Bounds
            { center = new Vector3(x, y, 1f), size = new Vector3(1f, 1f, 0f) } };
    }

    private static void Check(string label, Collider from, int direction, Collider expected)
    {
        selected.SetValue(null, from);
        step.Invoke(null, new object[] { direction });
        var actual = ControllerFrontEndMenus.SelectedCollider;
        if (actual == expected) Console.WriteLine("PASS " + label);
        else
        {
            failures++;
            Console.WriteLine("FAIL " + label + ": " + from.gameObject.name + " -> " +
                (actual == null ? "none" : actual.gameObject.name) +
                ", expected " + expected.gameObject.name);
        }
    }

    private static void Title()
    {
        var root = new GameObject("menuMain");
        // These centers model the observed spatial-ranking ambiguity: Play
        // beats Options from Data, and Play beats Data from Quit. Explicit
        // title-menu routes should not depend on collider spacing.
        var play = Add(root, "bPlay", .5f, 2f);
        var data = Add(root, "bData", -2f, 1f);
        var options = Add(root, "bOptions", 2.5f, 1f);
        var quit = Add(root, "bQuit", 1f, 0f);
        var menu = new Menuu { menuMain = root };
        ControllerFrontEndMenus.Configure(() => new JoystickInputState(),
            Config(), null);
        ControllerFrontEndMenus.Begin(menu);

        Check("title Data Right -> Options", data, 1, options);
        Check("title Quit Up -> Data", quit, -2, data);
        Check("title Play Right -> Options", play, 1, options);
        Check("title Options Up -> Play", options, -2, play);
        ControllerFrontEndMenus.Reset();
    }

    private static void Characters()
    {
        var root = new GameObject("menuCharSelect");
        var slots = new Collider[6];
        // The tested two-column slot order is row-major: 0/1 top,
        // 2/3 middle, 4/5 bottom. Middle-row collider centers are offset
        // just enough that spatial scoring incorrectly prefers row three.
        slots[0] = Add(root, "bs0", 0f, 3f);
        slots[1] = Add(root, "bs1", 2f, 3f);
        slots[2] = Add(root, "bs2", .8f, 2f);
        slots[3] = Add(root, "bs3", 2.8f, 2f);
        slots[4] = Add(root, "bs4", 0f, 1f);
        slots[5] = Add(root, "bs5", 2f, 1f);
        var back = Add(root, "bBack", 1f, 0f);
        var menu = new Menuu { menuCharSelect = root };
        ControllerFrontEndMenus.Configure(() => new JoystickInputState(),
            Config(), null);
        ControllerFrontEndMenus.Begin(menu);

        for (int column = 0; column < 2; column++)
        {
            Check("characters top " + column + " Down -> middle",
                slots[column], 2, slots[2 + column]);
            Check("characters middle " + column + " Down -> bottom",
                slots[2 + column], 2, slots[4 + column]);
            Check("characters bottom " + column + " Up -> middle",
                slots[4 + column], -2, slots[2 + column]);
            Check("characters middle " + column + " Up -> top",
                slots[2 + column], -2, slots[column]);
            Check("characters bottom " + column + " Down -> Back",
                slots[4 + column], 2, back);
        }
        Check("characters Back Up -> bottom row", back, -2, slots[4]);
        Check("characters left top Right -> right top", slots[0], 1, slots[1]);
        Check("characters right middle Left -> left middle", slots[3], -1, slots[2]);
        ControllerFrontEndMenus.Reset();
    }

    private static void BothSticks()
    {
        var titleRoot = new GameObject("menuMain");
        var play = Add(titleRoot, "bPlay", 0f, 2f);
        var data = Add(titleRoot, "bData", -2f, 1f);
        var options = Add(titleRoot, "bOptions", 2f, 1f);
        Add(titleRoot, "bQuit", 0f, 0f);
        var title = new Menuu { menuMain = titleRoot };
        var state = ConnectedState();
        ControllerFrontEndMenus.Configure(() => state, Config(), null);
        ControllerFrontEndMenus.Begin(title);
        selected.SetValue(null, data);
        Axis(state, 0, 1f);
        ControllerFrontEndMenus.Begin(title);
        AssertSelected("title left stick Data Right -> Options", options);
        Axis(state, 0, 0f);
        ControllerFrontEndMenus.Begin(title);
        Axis(state, 3, 1f);
        selected.SetValue(null, data);
        ControllerFrontEndMenus.Begin(title);
        AssertSelected("title right stick Data Right -> Options", options);
        ControllerFrontEndMenus.Reset();

        var charRoot = new GameObject("menuCharSelect");
        var slots = new Collider[6];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = Add(charRoot, "bs" + i, (i % 2) * 2f, 3f - (i / 2));
        Add(charRoot, "bBack", 1f, 0f);
        var characters = new Menuu { menuCharSelect = charRoot };
        state = ConnectedState();
        ControllerFrontEndMenus.Configure(() => state, Config(), null);
        ControllerFrontEndMenus.Begin(characters);
        Axis(state, 1, 1f);
        ControllerFrontEndMenus.Begin(characters);
        AssertSelected("characters left stick top Down -> middle", slots[2]);
        Axis(state, 1, 0f);
        ControllerFrontEndMenus.Begin(characters);
        Axis(state, 4, 1f);
        selected.SetValue(null, slots[0]);
        ControllerFrontEndMenus.Begin(characters);
        AssertSelected("characters right stick top Down -> middle", slots[2]);
        Axis(state, 4, 0f);
        ControllerFrontEndMenus.Begin(characters);
        Axis(state, 4, 1f);
        selected.SetValue(null, slots[0]);
        Axis(state, 1, 1f);
        // Both held Down must make one move, not skip the middle row.
        ControllerFrontEndMenus.Begin(characters);
        AssertSelected("characters both sticks Down -> middle once", slots[2]);
        Axis(state, 4, 0f);
        Axis(state, 3, 1f);
        selected.SetValue(null, slots[0]);
        ControllerFrontEndMenus.Begin(characters);
        AssertSelected("characters opposed sticks prefer right", slots[1]);
        ControllerFrontEndMenus.Reset();

        var modeRoot = new GameObject("menu");
        var single = Add(modeRoot, "bSingleplayer", 0f, 2f);
        var multi = Add(modeRoot, "bMultiplayer", 0f, 1f);
        var modes = new MenuScript { menu = modeRoot };
        state = ConnectedState();
        ControllerFrontEndMenus.Configure(() => state, Config(), null);
        ControllerFrontEndMenus.Begin(modes);
        Axis(state, 1, 1f);
        ControllerFrontEndMenus.Begin(modes);
        AssertSelected("single/multiplayer left stick Down", multi);
        Axis(state, 1, 0f);
        ControllerFrontEndMenus.Begin(modes);
        Axis(state, 4, 1f);
        selected.SetValue(null, single);
        ControllerFrontEndMenus.Begin(modes);
        AssertSelected("single/multiplayer right stick Down", multi);
        ControllerFrontEndMenus.Reset();

        var pauseRoot = new GameObject("menuPause");
        var resume = Add(pauseRoot, "bResume", 0f, 2f);
        var quit = Add(pauseRoot, "bQuit", 0f, 1f);
        var pause = new GameScript { menuPause = pauseRoot };
        GameScript.pausing = true;
        state = ConnectedState();
        ControllerFrontEndMenus.Configure(() => state, Config(), null);
        ControllerFrontEndMenus.Begin(pause);
        Axis(state, 1, 1f);
        ControllerFrontEndMenus.Begin(pause);
        AssertSelected("pause left stick Down", quit);
        Axis(state, 1, 0f);
        ControllerFrontEndMenus.Begin(pause);
        Axis(state, 4, 1f);
        selected.SetValue(null, resume);
        ControllerFrontEndMenus.Begin(pause);
        AssertSelected("pause right stick Down", quit);
        ControllerFrontEndMenus.Reset();
        GameScript.pausing = false;
    }

    private static BindingConfig Config(string cancel="Joystick1Button1")
    { return BindingConfig.Parse(BindingConfig.DefaultText.Replace("MenuCancel = Joystick1Button1", "MenuCancel = "+cancel).Split('\n')); }
    private static JoystickInputState ConnectedState()
    { var state=new JoystickInputState(); state.Connect(); return state; }
    private static void Axis(JoystickInputState state, byte axis, float value)
    { state.Apply(new RawJoystickEvent { Type=2, Number=axis, Value=(short)(value*32767) }); }
    private static void Button(JoystickInputState state, byte button)
    { state.BeginFrame(); state.Apply(new RawJoystickEvent { Type=1, Number=button, Value=1 }); }
    private static void Assert(bool condition, string label)
    { if(!condition) failures++; Console.WriteLine((condition?"PASS ":"FAIL ")+label); }

    private static void CancelBindings()
    {
        foreach(bool shipChoice in new[]{false,true})
        foreach(string binding in new[]{"Joystick1Button1","Joystick1Button12","Unbound"})
        {
            var state=ConnectedState();
            var root=new GameObject("menu"); Add(root,"bBack",0,1);
            Action begin=shipChoice ? (Action)(()=>ControllerFrontEndMenus.Begin(new MenuScript{menu=root})) :
                ()=>ControllerFrontEndMenus.Begin(new Menuu{menuOptions=root});
            ControllerFrontEndMenus.Configure(()=>state,Config(binding),null); begin();
            Input.NativeCancel=true; Input.EscapeDown=false; Button(state,1);
            Assert(ControllerFrontEndMenus.ButtonDown("Cancel")== (binding=="Joystick1Button1"),
                "Native B follows binding in "+(shipChoice?"ship":"title")+" "+binding);
            Input.EscapeDown=true;
            Assert(ControllerFrontEndMenus.ButtonDown("Cancel"),"Keyboard Escape survives simultaneous B "+binding);
            Input.EscapeDown=false; Input.NativeCancel=false;
            state.Apply(new RawJoystickEvent{Type=1,Number=1,Value=0}); Button(state,12);
            Assert(ControllerFrontEndMenus.ButtonDown("Cancel")== (binding=="Joystick1Button12"),"Remapped button follows config "+binding);
            state.BeginFrame(); Assert(!ControllerFrontEndMenus.ButtonDown("Cancel"),"Held Cancel does not repeat "+binding);
            Input.NativeCancel=true;
            Assert(ControllerFrontEndMenus.ButtonDown("Cancel"),"Native keyboard Cancel retained "+binding);
            state.Clear(); Input.NativeCancel=false;
            Assert(!ControllerFrontEndMenus.ButtonDown("Cancel"),"Disconnect clears controller Cancel "+binding);
            Input.NativeCancel=true;
            Assert(ControllerFrontEndMenus.ButtonDown("Cancel"),"Disconnect keeps native keyboard "+binding);
            ControllerFrontEndMenus.End();
            Assert(ControllerFrontEndMenus.ButtonDown("Cancel"),"Outside active supported menu native input unchanged "+binding);
            ControllerFrontEndMenus.Reset();
        }
        Input.NativeCancel=true;
        Assert(ControllerFrontEndMenus.ButtonDown("Other"),"Unrelated native named inputs unchanged");
        Input.NativeCancel=Input.EscapeDown=false;
        // The production transpiler expects a property, just as Unity provides.
        foreach(string target in new[]{"Menuu.Update","MenuScript.Update","GameScript.Update"})
        {
            var native=typeof(Input).GetMethod("GetButtonDown");
            var instructions=new[]{new CodeInstruction(typeof(Input).GetProperty("mousePosition").GetGetMethod()),
                new CodeInstruction(typeof(Input).GetMethod("GetMouseButtonDown")), new CodeInstruction(native)};
            var patched=FrontEndMenuTranspiler.Replace(instructions,target).ToArray();
            Assert(Equals(patched[2].operand,native)==(target=="GameScript.Update"),"Cancel transpiler ownership "+target);
        }
    }

    private static int Main()
    {
        Title();
        Characters();
        BothSticks();
        CancelBindings();
        Console.WriteLine("RESULT " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
