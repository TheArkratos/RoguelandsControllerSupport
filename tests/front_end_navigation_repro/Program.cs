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

    private static void DPadAndInitialCharacterRed()
    {
        var root=new GameObject("menuMain");var play=Add(root,"bPlay",0,2);var data=Add(root,"bData",0,1);
        var title=new Menuu{menuMain=root};var pad=ConnectedState();
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        Axis(pad,7,1);ControllerFrontEndMenus.Begin(title);
        AssertSelected("D-pad Down navigates native title menu",data);
        ControllerFrontEndMenus.Reset();
        root=new GameObject("menuCharSelect");Add(root,"bBack",1,0);var old=Add(root,"bs4",0,1);var first=Add(root,"bs0",0,3);
        var characters=new Menuu{menuCharSelect=root};pad=ConnectedState();
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(characters);
        AssertSelected("Character entry defaults top-left bs0 despite collider collection order",first);
        ControllerFrontEndMenus.Reset();
    }
    private static void FrontEndDPadCoverage()
    {
        foreach(string context in new[]{"main","options","character","create","choices","lobby","server"})
        {
            ControllerFrontEndMenus.Reset(); Physics.Hit=null; Input.Left=Input.Right=false; Input.mousePosition=new Vector3();
            Time.unscaledTime=100; var pad=ConnectedState();var root=new GameObject("menu");
            string[] names=context=="main" ? new[]{"bPlay","bData","bQuit"} :
                context=="options" ? new[]{"bHigherM","bLowerM","bBack"} :
                context=="character" ? new[]{"bs0","bs2","bs4"} :
                context=="create" ? new[]{"bRace","bVariant","bCreate"} :
                context=="choices" ? new[]{"0","1","2"} :
                context=="lobby" ? new[]{"bHost","bConnect","hostPort"} : new[]{"enterIP","enterPort","bBackMulti"};
            var top=Add(root,names[0],0,3);var middle=Add(root,names[1],0,2);var bottom=Add(root,names[2],0,1);
            if(context=="choices") foreach(var c in new[]{top,middle,bottom}) c.gameObject.layer=22;
            Action begin;
            if(context=="lobby"||context=="server")
            {
                var ship=new MenuScript();if(context=="lobby")ship.menu=root;else ship.menuServer=root;
                begin=()=>ControllerFrontEndMenus.Begin(ship);
            }
            else
            {
                var title=new Menuu();if(context=="main")title.menuMain=root;if(context=="options")title.menuOptions=root;
                if(context=="character")title.menuCharSelect=root;if(context=="create")title.menuCreateCharacter=root;if(context=="choices")title.menuStuffSelect=root;
                begin=()=>ControllerFrontEndMenus.Begin(title);
            }
            ControllerFrontEndMenus.Configure(()=>pad,Config(),null);begin();Axis(pad,7,1);begin();
            AssertSelected(context+" D-pad Down first step",middle);
            Assert(!ControllerFrontEndMenus.MouseButtonDown(0),context+" navigation never activates native click");
            Time.unscaledTime+=.15f;begin();AssertSelected(context+" repeat waits initial delay",middle);
            Time.unscaledTime+=.16f;begin();AssertSelected(context+" held direction repeats",bottom);
            Axis(pad,7,0);begin();Axis(pad,7,-1);begin();AssertSelected(context+" release and Up reverses direction",middle);
            Axis(pad,7,0);begin();Axis(pad,6,1);Axis(pad,7,1);begin();
            AssertSelected(context+" simultaneous diagonal does not move",middle);
            Axis(pad,6,0);Axis(pad,7,0);begin();Axis(pad,7,-1);begin();
            AssertSelected(context+" direction resumes after neutral",top);
            Axis(pad,6,1);Time.unscaledTime+=.31f;begin();
            AssertSelected(context+" rolling diagonal retains first Up",top);
        }
        ControllerFrontEndMenus.Reset();
    }
    private static void CharacterEntryAndHandoff()
    {
        Physics.Hit=null;Input.Left=Input.Right=false;var pad=ConnectedState();
        var chars=new GameObject("characters");var back=Add(chars,"bBack",1,0);var bottom=Add(chars,"bs4",0,1);
        var second=Add(chars,"bs1",2,3);var first=Add(chars,"bs0",0,3);var title=new Menuu{menuCharSelect=chars};
        Physics.Hit=bottom;ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        AssertSelected("character opening ignores stationary bottom-slot mouse hover",first);
        Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0);ControllerFrontEndMenus.Begin(title);
        AssertSelected("actual mouse movement takes character selection back",bottom);
        chars.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);chars.activeInHierarchy=true;
        ControllerFrontEndMenus.Begin(title);AssertSelected("character reentry resets previous selection to bs0",first);
        chars.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);first.enabled=false;chars.activeInHierarchy=true;
        ControllerFrontEndMenus.Begin(title);AssertSelected("disabled bs0 falls back to first visible top-row character",second);
        chars.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);second.gameObject.activeInHierarchy=false;
        chars.activeInHierarchy=true;ControllerFrontEndMenus.Begin(title);AssertSelected("hidden top-row falls back to next native character slot",bottom);
        chars.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);bottom.enabled=false;chars.activeInHierarchy=true;
        ControllerFrontEndMenus.Begin(title);AssertSelected("no characters falls back to safe existing choice",back);
        ControllerFrontEndMenus.Reset();
        first.enabled=bottom.enabled=true;second.gameObject.activeInHierarchy=true;Physics.Hit=bottom;
        var middle=Add(chars,"bs2",0,2);
        Axis(pad,7,1);ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        AssertSelected("held D-pad through root entry cannot skip bs0",first);
        Time.unscaledTime+=1;ControllerFrontEndMenus.Begin(title);AssertSelected("held D-pad stays blocked until neutral",first);
        Axis(pad,7,0);ControllerFrontEndMenus.Begin(title);Axis(pad,7,1);ControllerFrontEndMenus.Begin(title);
        AssertSelected("fresh D-pad after root entry can navigate",middle);
        ControllerFrontEndMenus.Reset();Axis(pad,7,0);Axis(pad,4,1);ControllerFrontEndMenus.Configure(()=>pad,Config(),null);
        ControllerFrontEndMenus.Begin(title);AssertSelected("held stick through root entry cannot skip bs0",first);
        Axis(pad,4,0);ControllerFrontEndMenus.Begin(title);Axis(pad,4,1);ControllerFrontEndMenus.Begin(title);
        AssertSelected("fresh stick after neutral navigates character entry",middle);
        Physics.Hit=second;Input.Left=true;chars.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);chars.activeInHierarchy=true;
        ControllerFrontEndMenus.Begin(title);AssertSelected("deliberate physical click can override character opening default",second);
        Input.Left=false;Physics.Hit=null;ControllerFrontEndMenus.Reset();
    }
    private static void NativeClicksAndExclusions()
    {
        var root=new GameObject("servers");var entry=Add(root,"bServer0",0,3);var deletion=Add(root,"ipdel0",0,2);
        var back=Add(root,"bBackMulti",0,1);var menu=new MenuScript{menuServer=root};var pad=ConnectedState();
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);Physics.Hit=entry;ControllerFrontEndMenus.Begin(menu);
        ControllerFrontEndMenus.Begin(menu);Button(pad,0);ControllerFrontEndMenus.Begin(menu);
        Assert(ControllerFrontEndMenus.MouseButtonDown(0),"Server entry uses native primary click adapter");
        ControllerFrontEndMenus.End();pad.BeginFrame();ControllerFrontEndMenus.Begin(menu);
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0),"Held primary never repeats server connection");
        Axis(pad,7,1);ControllerFrontEndMenus.Begin(menu);AssertSelected("Server delete button excluded from directional navigation",back);
        Physics.Hit=deletion;Input.mousePosition=new Vector3(Input.mousePosition.x+3,0,0);ControllerFrontEndMenus.Begin(menu);
        AssertSelected("Mouse hovering excluded delete cannot grant controller selection",back);
        ControllerFrontEndMenus.Reset();Input.Left=false;Physics.Hit=null;
        root=new GameObject("delete");Add(root,"delYes",0,2);var no=Add(root,"delNo",0,1);var title=new Menuu{menuDeleteChar=root};
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        AssertSelected("Character deletion confirmation defaults safe No",no);
        ControllerFrontEndMenus.Reset();root=new GameObject("data");Add(root,"bResetShip",0,2);back=Add(root,"bBack",0,1);title=new Menuu{menuData=root};
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        AssertSelected("Data destructive controls remain excluded",back);
        ControllerFrontEndMenus.Reset();
    }
    private static BoxCollider AddBox(GameObject root, string name, float x, float y,
        float width = 1.4f, float height = .2f)
    {
        var button = new GameObject(name); root.AddChild(button);
        return new BoxCollider(button) { size = new Vector3(width, height, 1f),
            position = new Vector3(x, y, 1f) };
    }
    private static bool Same(Vector3 a, Vector3 b)
    { return Math.Abs(a.x-b.x)<.0001f && Math.Abs(a.y-b.y)<.0001f && Math.Abs(a.z-b.z)<.0001f; }

    private static void CharacterHitboxes()
    {
        Physics.Hit=null; Input.Left=Input.Right=false;
        var root=new GameObject("characters");
        var boxes=new BoxCollider[6];
        for(int i=0;i<6;i++) boxes[i]=AddBox(root,"bs"+i,(i%2)*14,30-(i/2)*8,
            i==5?1.7f:1.4f,i==5?.3f:.2f);
        var last=boxes[5]; var original=last.size; var center=last.center;
        var pad=ConnectedState();var title=new Menuu{menuCharSelect=root};
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,boxes[0].size),"character bs5 native local hitbox matches peer");
        Assert(Same(last.bounds.size,boxes[0].bounds.size),"character bs5 scaled bounds match peer");
        Assert(Same(last.center,center),"character normalization preserves local center");
        for(int i=0;i<5;i++) Assert(Same(boxes[i].size,new Vector3(1.4f,.2f,1)),"character peer "+i+" is unchanged");
        Check("resized character left neighbor Right -> bs5",boxes[4],1,last);
        Physics.Hit=last;ControllerFrontEndMenus.Begin(title);Button(pad,0);ControllerFrontEndMenus.Begin(title);
        Assert(ControllerFrontEndMenus.MouseButtonDown(0),"resized character uses native click adapter");
        ControllerFrontEndMenus.End();
        for(int i=0;i<3;i++) ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,boxes[0].size),"repeated character Begin is idempotent");
        root.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,original),"leaving character menu restores original collider");
        root.activeInHierarchy=true;ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,boxes[0].size),"character reentry normalizes again");
        ControllerFrontEndMenus.Reset();Assert(Same(last.size,original),"Reset restores original collider");
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);
        Assert(Same(last.size,original),"reconfigure restores original collider");
        ControllerFrontEndMenus.Begin(title);
        var options=new GameObject("options");var option=AddBox(options,"bs5",0,0,1.7f,.3f);
        title.menuOptions=options;root.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,original),"root switch restores character collider");
        Assert(Same(option.size,original),"same named box outside character menu is untouched");
        ControllerFrontEndMenus.Reset();
        root=new GameObject("characters");var plain=Add(root,"bs5",0,0);AddBox(root,"bs0",0,10);
        var bounds=plain.bounds;title=new Menuu{menuCharSelect=root};
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        Assert(Same(plain.bounds.size,bounds.size),"non-box character collider is untouched");
        ControllerFrontEndMenus.Reset();
        root=new GameObject("characters");last=AddBox(root,"bs5",0,0,1.7f,.3f);
        title=new Menuu{menuCharSelect=root};ControllerFrontEndMenus.Configure(()=>pad,Config(),null);
        ControllerFrontEndMenus.Begin(title);Assert(Same(last.size,original),"missing peer leaves box untouched");
        var bad=AddBox(root,"bs0",0,10,0,.2f);var peer=AddBox(root,"bs1",14,10,1.4f,.2f);
        last.size=new Vector3(1.7f,.3f,2f);last.center=new Vector3(.1f,.2f,.3f);
        ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,new Vector3(peer.size.x,peer.size.y,2f)),"valid peer fallback preserves target depth");
        Assert(Same(last.center,new Vector3(.1f,.2f,.3f)),"nonzero center is preserved");
        ControllerFrontEndMenus.Reset();
        var restored=last.size;peer.enabled=false;
        ControllerFrontEndMenus.Configure(()=>new JoystickInputState(),Config(),null);
        ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,restored),"disabled peer is ignored");
        peer.enabled=true;peer.gameObject.activeInHierarchy=false;ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,restored),"inactive peer is ignored");
        peer.gameObject.activeInHierarchy=true;ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size,new Vector3(1.4f,.2f,2f)),"controller ownership normalizes real hitbox even if pad disconnected");
        ControllerFrontEndMenus.Reset();Physics.Hit=null;
    }
    private static void OwnershipHandoff()
    {
        bool active = false;
        ControllerInputOwnership.Configure(() => active);
        var root = new GameObject("characters");
        var first = AddBox(root, "bs0", 0, 10);
        var last = AddBox(root, "bs5", 10, 0, 1.7f, .3f);
        var original = last.size;
        var pad = ConnectedState();
        var title = new Menuu { menuCharSelect = root };
        ControllerFrontEndMenus.Configure(() => pad, Config(), null);
        ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size, original), "false ownership preserves native character hitbox");
        Assert(ControllerFrontEndMenus.SelectedCollider == null, "false ownership does not choose a default");
        active = true;
        ControllerFrontEndMenus.Begin(title);
        AssertSelected("controller ownership chooses top-left character", first);
        Assert(Same(last.size, first.size), "controller ownership normalizes hitbox");
        active = false;
        ControllerFrontEndMenus.Begin(title);
        Assert(Same(last.size, original), "mouse handoff restores native hitbox");
        Assert(ControllerFrontEndMenus.SelectedCollider == null, "mouse handoff clears controller selection");
        active = true;
        ControllerFrontEndMenus.Begin(title);
        Physics.Hit = first;
        Button(pad, 0);
        ControllerFrontEndMenus.Begin(title);
        Assert(ControllerFrontEndMenus.MouseButtonDown(0), "active ownership creates virtual click");
        active = false;
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0), "same-frame ownership loss blocks cached virtual click");
        Assert(Same(last.size, original), "same-frame input read restores native geometry");
        active = true; ControllerFrontEndMenus.Begin(title);
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0), "held activation cannot replay on reclaim");
        pad.BeginFrame(); pad.Apply(new RawJoystickEvent { Type=1,Number=0,Value=0 }); ControllerFrontEndMenus.Begin(title);
        Button(pad,0); ControllerFrontEndMenus.Begin(title);
        Assert(ControllerFrontEndMenus.MouseButtonDown(0), "fresh activation works after ownership reclaim");
        ControllerFrontEndMenus.Reset(); Physics.Hit = null;
        ControllerInputOwnership.Configure(() => true);
    }
    private static ButtonMenu Highlight(Collider target)
    {
        var obj=target.gameObject;
        var button=obj.AddComponent<ButtonMenu>(); button.button=new Material(); button.buttonSelect=new Material();
        obj.AddComponent<Renderer>().material=button.button; obj.AddComponent<AudioSource>();
        button.txt[0]=new GameObject("text").AddComponent<TextMesh>(); button.txt[0].gameObject.AddComponent<Animation>();
        return button;
    }
    private static void NativeHighlights()
    {
        bool active=true; ControllerInputOwnership.Configure(()=>active); Physics.Hit=null; Input.Left=Input.Right=false;
        var root=new GameObject("main"); var play=Add(root,"bPlay",0,2); var data=Add(root,"bData",0,1);
        var playButton=Highlight(play); var dataButton=Highlight(data); var title=new Menuu{menuMain=root}; var pad=ConnectedState();
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null); ControllerFrontEndMenus.Begin(title);
        Assert(play.gameObject.GetComponent<Renderer>().material==playButton.buttonSelect,"main uses actual native ButtonMenu highlight on default");
        Assert(SelectionCornerRenderer.Visible==null,"native highlighted target has no corners");
        Assert(!FrontEndNativeHoverPatch.Prefix(playButton)&&!FrontEndNativeHoverPatch.Prefix(dataButton),
            "Unity physical hover callbacks cannot clear or compete with controller selection");
        int hoverSounds=play.gameObject.GetComponent<AudioSource>().Plays;
        ControllerFrontEndMenus.Begin(title);
        Assert(play.gameObject.GetComponent<AudioSource>().Plays==hoverSounds,"idle selection does not replay native hover sound");
        Axis(pad,7,1); ControllerFrontEndMenus.Begin(title);
        Assert(play.gameObject.GetComponent<Renderer>().material==playButton.button&&data.gameObject.GetComponent<Renderer>().material==dataButton.buttonSelect,
            "navigation exits old and enters new native material");
        active=false; Physics.Hit=play; ControllerFrontEndMenus.MousePosition();
        Assert(data.gameObject.GetComponent<Renderer>().material==dataButton.button&&play.gameObject.GetComponent<Renderer>().material==playButton.buttonSelect,
            "same-frame handoff restores native physical hover");
        Assert(FrontEndNativeHoverPatch.Prefix(playButton),"native Unity hover callbacks resume on mouse handoff");
        active=true; Physics.Hit=null; Axis(pad,7,0); ControllerFrontEndMenus.Begin(title);
        active=false; Physics.Hit=data;
        int mouseSound=data.gameObject.GetComponent<AudioSource>().Plays;
        if(FrontEndNativeHoverPatch.Prefix(dataButton)) dataButton.OnMouseEnter();
        Assert(data.gameObject.GetComponent<AudioSource>().Plays==mouseSound+1,
            "Unity mouse enter before menu update plays native sound only once");
        Assert(play.gameObject.GetComponent<Renderer>().material==playButton.button&&data.gameObject.GetComponent<Renderer>().material==dataButton.buttonSelect,
            "Unity mouse callback restores physical highlight and clears controller material");
        ControllerFrontEndMenus.Reset(); Physics.Hit=null; ControllerInputOwnership.Configure(()=>true);

        root=new GameObject("characters");
        var last=Add(root,"bs5",10,0); var first=Add(root,"bs0",0,10);
        var lastButton=Highlight(last); var firstButton=Highlight(first);
        lastButton.OnMouseEnter(); Physics.Hit=last;
        title=new Menuu{menuCharSelect=root}; pad=ConnectedState();
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null); ControllerFrontEndMenus.Begin(title);
        AssertSelected("native highlight preserves top-left character default",first);
        Assert(first.gameObject.GetComponent<Renderer>().material==firstButton.buttonSelect&&last.gameObject.GetComponent<Renderer>().material==lastButton.button,
            "character default uses native highlight and clears preexisting physical hover");
        Assert(SelectionCornerRenderer.Visible==null,"character native highlight replaces corners");
        int animations=firstButton.txt[0].gameObject.GetComponent<Animation>().Plays;
        Assert(animations==1,"native character text animation runs once on enter");
        var options=new GameObject("options"); var back=Add(options,"bBack",0,0); var backButton=Highlight(back);
        root.activeInHierarchy=false; title.menuOptions=options; Physics.Hit=null; ControllerFrontEndMenus.Begin(title);
        Assert(first.gameObject.GetComponent<Renderer>().material==firstButton.button,"context switch exits previous native highlight");
        Assert(SelectionCornerRenderer.Visible==back&&back.gameObject.GetComponent<Renderer>().material==backButton.button,
            "outside requested screens existing corners remain authoritative");
        Assert(FrontEndNativeHoverPatch.Prefix(backButton),"outside requested screens native callbacks remain untouched");
        ControllerFrontEndMenus.Reset();

        root=new GameObject("modeSelect"); var single=Add(root,"bSingleplayer",0,2); var multi=Add(root,"bMultiplayer",0,1);
        var singleButton=Highlight(single); var multiButton=Highlight(multi); var modes=new MenuScript{menu=root};
        pad=ConnectedState(); ControllerFrontEndMenus.Configure(()=>pad,Config(),null); ControllerFrontEndMenus.Begin(modes);
        Assert(single.gameObject.GetComponent<Renderer>().material==singleButton.buttonSelect,"singleplayer selection uses native highlight");
        Axis(pad,7,1); ControllerFrontEndMenus.Begin(modes);
        Assert(multi.gameObject.GetComponent<Renderer>().material==multiButton.buttonSelect&&SelectionCornerRenderer.Visible==null,
            "multiplayer selection uses native highlight without corners");
        var noHighlight=Add(root,"bBack",0,0); Axis(pad,7,0); ControllerFrontEndMenus.Begin(modes);
        Axis(pad,7,1); ControllerFrontEndMenus.Begin(modes);
        Assert(SelectionCornerRenderer.Visible==noHighlight&&multi.gameObject.GetComponent<Renderer>().material==multiButton.button,
            "target without native highlight falls back to corners and exits old material");
        ControllerFrontEndMenus.Reset(); Physics.Hit=null;
    }
    private static void PauseNativeHighlights()
    {
        bool active = true;
        ControllerInputOwnership.Configure(() => active);
        Physics.Hit = null; Input.Left = Input.Right = false;
        var root = new GameObject("menuPause");
        var resume = Add(root, "bResume", 0, 3);
        var quit = Add(root, "bQuit", 0, 2);
        var resumeButton = Highlight(resume); var quitButton = Highlight(quit);
        var game = new GameScript { menuPause = root };
        var pad = ConnectedState(); GameScript.pausing = true;
        ControllerFrontEndMenus.Configure(() => pad, Config(), null);
        ControllerFrontEndMenus.Begin(game);
        Assert(resume.gameObject.GetComponent<Renderer>().material == resumeButton.buttonSelect,
            "pause Resume uses native selected material");
        Assert(SelectionCornerRenderer.Visible == null, "pause native highlight hides corners");
        Assert(!FrontEndNativeHoverPatch.Prefix(resumeButton) && !FrontEndNativeHoverPatch.Prefix(quitButton),
            "pause physical callbacks cannot compete with controller focus");
        int sounds = resume.gameObject.GetComponent<AudioSource>().Plays;
        ControllerFrontEndMenus.Begin(game);
        Assert(resume.gameObject.GetComponent<AudioSource>().Plays == sounds,
            "pause idle focus does not repeat hover sound");
        Axis(pad, 7, 1); ControllerFrontEndMenus.Begin(game);
        AssertSelected("pause navigation selects Quit", quit);
        Assert(resume.gameObject.GetComponent<Renderer>().material == resumeButton.button &&
            quit.gameObject.GetComponent<Renderer>().material == quitButton.buttonSelect,
            "pause navigation exits Resume and enters Quit native materials");
        Assert(quit.gameObject.GetComponent<AudioSource>().Plays == 1,
            "pause navigation plays native hover sound once");
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0), "pause focus does not activate Quit");
        Physics.Hit = quit; Button(pad, 0); ControllerFrontEndMenus.Begin(game);
        Assert(ControllerFrontEndMenus.MouseButtonDown(0), "pause fresh A retains native click dispatch");
        ControllerFrontEndMenus.End(); pad.BeginFrame(); ControllerFrontEndMenus.Begin(game);
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0), "pause held A cannot replay activation");

        active = false; Physics.Hit = resume; ControllerFrontEndMenus.MousePosition();
        Assert(quit.gameObject.GetComponent<Renderer>().material == quitButton.button &&
            resume.gameObject.GetComponent<Renderer>().material == resumeButton.buttonSelect,
            "pause ownership handoff restores stationary physical hover");
        Assert(FrontEndNativeHoverPatch.Prefix(resumeButton), "pause native mouse callback resumes on handoff");
        active = true; Physics.Hit = null; Axis(pad, 7, 0);
        ControllerFrontEndMenus.Begin(game);
        active = false; Physics.Hit = quit; sounds = quit.gameObject.GetComponent<AudioSource>().Plays;
        if (FrontEndNativeHoverPatch.Prefix(quitButton)) quitButton.OnMouseEnter();
        Assert(quit.gameObject.GetComponent<AudioSource>().Plays == sounds + 1,
            "pause Unity callback handoff plays hover once");
        Assert(resume.gameObject.GetComponent<Renderer>().material == resumeButton.button,
            "pause Unity callback handoff clears controller highlight");

        active = true; Physics.Hit = null;
        ControllerFrontEndMenus.Begin(game);
        GameScript.pausing = false; ControllerFrontEndMenus.Begin(game);
        Assert(resume.gameObject.GetComponent<Renderer>().material == resumeButton.button,
            "unpausing clears prior controller material");
        Assert(FrontEndNativeHoverPatch.Prefix(quitButton), "unpaused buttons are not guarded");
        ControllerFrontEndMenus.Reset();

        // Pause options share the same native root and native click adapter.
        root = new GameObject("menuPause");
        var higher = Add(root, "bHigherM", 0, 3); var lower = Add(root, "bLowerM", 0, 2);
        var fullscreen = Add(root, "bFullscreen", 0, 1);
        var higherButton = Highlight(higher); var lowerButton = Highlight(lower);
        var fullscreenButton = Highlight(fullscreen); fullscreenButton.minorButton = true;
        game = new GameScript { menuPause = root }; pad = ConnectedState(); GameScript.pausing = true;
        ControllerFrontEndMenus.Configure(() => pad, Config(), null); ControllerFrontEndMenus.Begin(game);
        Assert(higher.gameObject.GetComponent<Renderer>().material == higherButton.buttonSelect,
            "pause options uses native highlight");
        Axis(pad, 7, 1); ControllerFrontEndMenus.Begin(game);
        Assert(lower.gameObject.GetComponent<Renderer>().material == lowerButton.buttonSelect,
            "pause options navigation highlights next control");
        Axis(pad, 7, 0); ControllerFrontEndMenus.Begin(game); Axis(pad, 7, 1); ControllerFrontEndMenus.Begin(game);
        Assert(fullscreen.gameObject.GetComponent<Renderer>().material == fullscreenButton.buttonSelect &&
            SelectionCornerRenderer.Visible == null, "pause minor button uses native material without corners");
        var fallback = Add(root, "bLowerS", 0, 0);
        Axis(pad, 7, 0); ControllerFrontEndMenus.Begin(game); Axis(pad, 7, 1); ControllerFrontEndMenus.Begin(game);
        Assert(SelectionCornerRenderer.Visible == fallback &&
            fullscreen.gameObject.GetComponent<Renderer>().material == fullscreenButton.button,
            "pause unsupported target retains corners and clears prior native highlight");
        var outside = Highlight(Add(new GameObject("unrelated"), "bResume", 0, 0));
        Assert(FrontEndNativeHoverPatch.Prefix(outside), "pause guard leaves unrelated native buttons alone");
        selected.SetValue(null, higher); Axis(pad, 7, 0); ControllerFrontEndMenus.Begin(game);
        root.SetActive(false); ControllerFrontEndMenus.Begin(game);
        Assert(higher.gameObject.GetComponent<Renderer>().material == higherButton.button,
            "hidden pause root clears controller highlight");
        root.SetActive(true); ControllerFrontEndMenus.Begin(game); ControllerFrontEndMenus.Reset();
        Assert(higher.gameObject.GetComponent<Renderer>().material == higherButton.button,
            "pause reset clears native controller material");
        GameScript.pausing = false; Physics.Hit = null; ControllerInputOwnership.Configure(() => true);
    }

    private static void CharacterDeletion()
    {
        bool owned = true;
        ControllerInputOwnership.Configure(() => owned); Physics.Hit = null;
        Input.Left = Input.Right = Input.NativeCancel = Input.EscapeDown = false;
        var root = new GameObject("menuCharSelect");
        var cards = new Collider[6]; var deletes = new Collider[6];
        for (int i = 0; i < 6; i++)
        {
            float x = i % 2 == 0 ? -4 : 4, y = 6 - i / 2 * 2;
            cards[i] = Add(root,"bs" + i,x,y);
            deletes[i] = Add(root,"del" + i,x + (i % 2 == 0 ? -2 : 2),y);
        }
        Add(root,"bBack",0,-2);
        var prompt = new GameObject("menuDelete");
        var yes = Add(prompt,"delYes",-2,0); var no = Add(prompt,"delNo",2,0);
        var yesButton = Highlight(yes); var noButton = Highlight(no);
        prompt.SetActive(false); var title = new Menuu { menuCharSelect = root, menuDeleteChar = prompt };
        var pad = ConnectedState(); ControllerFrontEndMenus.Configure(() => pad,Config(),null);
        ControllerFrontEndMenus.Begin(title);
        for (int i = 0; i < 6; i++)
        {
            int outward = i % 2 == 0 ? -1 : 1;
            Check("character card outward reaches native Delete " + i,cards[i],outward,deletes[i]);
            Check("native Delete inward returns own card " + i,deletes[i],-outward,cards[i]);
            if (i < 4) Check("native Delete Down reaches same-column next row " + i,deletes[i],2,deletes[i+2]);
        }
        // Replay only the native branch dispatch in memory; no saves or real
        // Menuu.EraseCharacter invocation is involved in this fixture.
        int pending = -1, erased = -1, confirms = 0;
        Action replay = () =>
        {
            if (!ControllerFrontEndMenus.MouseButtonDown(0)) return;
            string name = ControllerFrontEndMenus.SelectedCollider.gameObject.name;
            if (name == "delYes") { erased = pending; confirms++; prompt.SetActive(false); }
            else if (name == "delNo") { pending = 0; prompt.SetActive(false); }
            else if (name.StartsWith("del")) { pending = int.Parse(name.Substring(3)); prompt.SetActive(true); }
        };
        selected.SetValue(null,deletes[3]); Physics.Hit = deletes[3];
        Button(pad,0); ControllerFrontEndMenus.Begin(title); replay();
        Assert(pending == 3 && prompt.activeInHierarchy && confirms == 0,
            "A on native Delete opens confirmation for its exact character, without erasing");
        Physics.Hit = yes; pad.BeginFrame(); ControllerFrontEndMenus.Begin(title);
        AssertSelected("delete confirmation defaults No despite stationary pointer over Yes",no);
        Assert(no.gameObject.GetComponent<Renderer>().material == noButton.buttonSelect,
            "confirmation default No uses native highlight");
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0),"held A opening prompt cannot confirm");
        Check("confirmation can deliberately navigate Left to native Yes",no,-1,yes);
        ControllerFrontEndMenus.Begin(title);
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0),"moving focus to Yes while A held cannot confirm");
        pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=0,Value=0});ControllerFrontEndMenus.Begin(title);
        Button(pad,0);ControllerFrontEndMenus.Begin(title);replay();
        Assert(erased == 3 && confirms == 1,"fresh A on selected Yes routes exactly the pending character once");
        ControllerFrontEndMenus.End();pad.BeginFrame();ControllerFrontEndMenus.Begin(title);
        AssertSelected("after native completion character selection returns to first card",cards[0]);
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0),"held confirmation A cannot launch character after returning");
        deletes[3].gameObject.SetActive(false);ControllerFrontEndMenus.Begin(title);
        Check("empty character hides its delete control",cards[3],1,cards[3]);
        // Native B cancellation should dispatch delNo, not the generic native
        // Cancel branch (which goes to Main Menu).
        pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=0,Value=0});
        Button(pad,1);prompt.SetActive(true);Physics.Hit = no;ControllerFrontEndMenus.Begin(title);
        Assert(!ControllerFrontEndMenus.ButtonDown("Cancel") && !ControllerFrontEndMenus.MouseButtonDown(0),
            "held entry B cannot cancel or activate confirmation");
        pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=1,Value=0});ControllerFrontEndMenus.Begin(title);
        selected.SetValue(null,yes);Button(pad,1);ControllerFrontEndMenus.Begin(title);
        Assert(!ControllerFrontEndMenus.ButtonDown("Cancel"),"controller B does not run native Back to Main Menu");
        AssertSelected("controller B selects native No even while Yes focused",no);replay();
        Assert(!prompt.activeInHierarchy && confirms == 1,"B dismisses prompt without another deletion");
        ControllerFrontEndMenus.End();pad.BeginFrame();pad.Apply(new RawJoystickEvent{Type=1,Number=1,Value=0});
        prompt.SetActive(true);Physics.Hit=no;ControllerFrontEndMenus.Begin(title);
        selected.SetValue(null,yes);pad.BeginFrame();
        pad.Apply(new RawJoystickEvent{Type=1,Number=0,Value=1});pad.Apply(new RawJoystickEvent{Type=1,Number=1,Value=1});
        Physics.Hit=no;ControllerFrontEndMenus.Begin(title);
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0),"simultaneous A+B cannot confirm Yes before cancellation");
        Assert(!ControllerFrontEndMenus.ButtonDown("Cancel"),"simultaneous A+B stays in native dialog click workflow");
        AssertSelected("simultaneous A+B cancellation wins",no);replay();
        Assert(confirms == 1,"simultaneous A+B never erases");
        ControllerFrontEndMenus.Reset();
        // Held navigation on modal entry must be released before selecting Yes.
        pad=ConnectedState();Axis(pad,7,-1);prompt.SetActive(true);Physics.Hit=yes;
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        AssertSelected("held direction on confirmation entry retains default No",no);
        ControllerFrontEndMenus.Begin(title);AssertSelected("held entry direction cannot later slide to Yes",no);
        // Mouse/controller handoff preserves actual physical native input.
        owned=false;Input.Left=true;Physics.Hit=yes;
        Assert(ControllerFrontEndMenus.MouseButtonDown(0),"physical Yes click remains native on mouse handoff");
        Assert(ControllerFrontEndMenus.SelectedCollider == null,"mouse handoff clears controller confirmation focus");
        Input.Left=false;owned=true;ControllerFrontEndMenus.Begin(title);
        AssertSelected("controller reacquisition defaults No over stationary Yes pointer",no);
        ControllerFrontEndMenus.Reset();
        // A provider may detect mouse ownership while polling: never expose a
        // stale controller Yes activation afterward.
        pad=ConnectedState();ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        selected.SetValue(null,yes);Button(pad,0);
        ControllerFrontEndMenus.Configure(()=>{owned=false;return pad;},Config(),null);
        ControllerFrontEndMenus.Begin(title);
        Assert(!ControllerFrontEndMenus.MouseButtonDown(0) && ControllerFrontEndMenus.SelectedCollider==null,
            "poll-time ownership loss cannot expose a delete confirmation click");
        owned=true;Physics.Hit=null;ControllerFrontEndMenus.Reset();
        // Fail closed if a malformed/custom dialog has lost its safe No control.
        no.gameObject.SetActive(false);pad=ConnectedState();
        ControllerFrontEndMenus.Configure(()=>pad,Config(),null);ControllerFrontEndMenus.Begin(title);
        Assert(ControllerFrontEndMenus.SelectedCollider==null,"missing No control never defaults to Yes");
        Axis(pad,7,0);ControllerFrontEndMenus.Begin(title);Axis(pad,7,-1);ControllerFrontEndMenus.Begin(title);
        Assert(ControllerFrontEndMenus.SelectedCollider==null,"missing No cannot be bypassed by initial directional focus");
        ControllerFrontEndMenus.Reset();Physics.Hit=null;
    }

    private static void QuestQuarantineMouseRouting()
    {
        foreach (bool owned in new[] { true, false })
        {
            ControllerFrontEndMenus.Reset();
            ControllerInputOwnership.Configure(() => owned);
            Input.Left = Input.Right = true;
            ControllerMenuSelection.QuestInputBlocker = button => button == 0;
            Assert(!ControllerFrontEndMenus.MouseButtonDown(0),
                "quest quarantine blocks physical primary before ownership bypass: " + owned);
            Assert(ControllerFrontEndMenus.MouseButtonDown(1),
                "quest quarantine preserves unrelated physical secondary: " + owned);
            ControllerMenuSelection.QuestInputBlocker = null;
            Assert(ControllerFrontEndMenus.MouseButtonDown(0),
                "ordinary physical primary remains native without quest quarantine: " + owned);
        }
        Input.Left = Input.Right = false;
        ControllerInputOwnership.Configure(() => true);
    }

    private static int Main()
    {
        QuestQuarantineMouseRouting();
        CharacterDeletion();
        PauseNativeHighlights();
        NativeHighlights();
        OwnershipHandoff();
        CharacterHitboxes();
        FrontEndDPadCoverage();CharacterEntryAndHandoff();NativeClicksAndExclusions();
        DPadAndInitialCharacterRed();
        Title();
        Characters();
        BothSticks();
        CancelBindings();
        Console.WriteLine("RESULT " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
