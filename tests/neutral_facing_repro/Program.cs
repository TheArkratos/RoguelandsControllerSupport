using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ControllerSupport;
using HarmonyLib;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static bool owned=true,modal;
    private static JoystickInputState pad;
    private static void Check(bool value,string why) { checks++;if(!value)throw new Exception(why); }
    private static void Axis(int n,short v) { pad.Apply(new RawJoystickEvent { Type=2,Number=(byte)n,Value=v }); }
    private static List<CodeInstruction> Raw(int mice=1,int xs=1)
    {
        var code=new List<CodeInstruction>();
        for(int i=0;i<mice;i++)code.Add(new CodeInstruction(OpCodes.Call,typeof(Input).GetField("mousePosition")!=null?typeof(Program).GetMethod("Mouse",BindingFlags.Static|BindingFlags.NonPublic):typeof(Input).GetProperty("mousePosition").GetGetMethod()));
        for(int i=0;i<xs;i++)code.Add(new CodeInstruction(OpCodes.Ldsfld,typeof(ControllerCursor).GetField("staticX")));
        return code;
    }
    private static Vector3 Mouse() { return Input.mousePosition; }
    private static List<CodeInstruction> Rewrite(List<CodeInstruction> code)
    {
        Type patch=typeof(ControllerAim).Assembly.GetType("ControllerSupport.ControllerFacingPatch");
        if(patch==null)return code;
        return ((IEnumerable<CodeInstruction>)patch.GetMethod("Transpiler",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(null,new object[]{code})).ToList();
    }
    // Native PlayerScript.Update753–770: input source branch, X comparison,
    // then Y180 for right or Y0 for left. Read adapters come from production IL patch.
    private static bool NativeFacesRight(List<CodeInstruction> instructions,bool controller)
    {
        CodeInstruction read=instructions[controller?1:0];
        float x=controller?(read.operand is MethodInfo m?(float)m.Invoke(null,null):ControllerCursor.staticX):Camera.main.ScreenToWorldPoint((Vector3)((MethodInfo)read.operand).Invoke(null,null)).x;
        return x>(MenuScript.player!=null?MenuScript.player.transform.position.x:0f);
    }
    private static void Setup()
    {
        owned=true;modal=false;ControllerInputOwnership.Configure(()=>owned,()=>modal);
        GameScript.combatMode=true;GameScript.dead=GameScript.pausing=GameScript.buildMode=GameScript.inventoryOpen=false;
        GameScript.equippedIDs=new[]{327};
        MenuScript.player=new GameObject();Input.mousePosition=new Vector3(50,5,0);Menuu.usingController=true;
        pad=new JoystickInputState();pad.Connect();ControllerAim.Configure(()=>pad);ControllerLookDirection.Configure(()=>pad);
        Axis(3,short.MaxValue);Time.frameCount++;Time.unscaledTime+=1;
        Check(ControllerAim.TryWorldPoint(out var aim),"explicit right aim acquired");
        ControllerCursor.staticX=aim.x;ControllerCursor.staticY=aim.y;
        Axis(3,0);Axis(0,short.MinValue);Time.frameCount++;Time.unscaledTime+=.016f;
    }
    private static Vector3 AimFrame()
    {
        Time.frameCount++;Time.unscaledTime+=.016f;
        Check(ControllerAim.TryWorldPoint(out var point),"eligible controller owns white crosshair");
        return point;
    }
    private static void ExplorationFacing()
    {
        Setup(); GameScript.combatMode=false;
        var native=Rewrite(Raw()); ControllerCursor.staticX=50; Input.mousePosition=new Vector3(50,5,0);
        Check(!NativeFacesRight(native,true),"exploration LEFT-stick LEFT overrides stale right cursor");
        Check(!NativeFacesRight(native,false),"exploration LEFT-stick LEFT overrides physical mouse branch");
        Axis(0,0);Axis(1,short.MinValue);Axis(3,short.MaxValue);
        Check(!NativeFacesRight(native,true),"exploration neutral/up movement and right stick retain last LEFT facing");
        Axis(0,short.MaxValue);Check(NativeFacesRight(native,true),"exploration LEFT-stick RIGHT reverses facing");
        Axis(0,0);Axis(1,0);Axis(3,short.MinValue);
        Check(NativeFacesRight(native,false),"exploration RIGHT persists with neutral move despite opposing right stick/mouse");
        Axis(0,-4000);Check(NativeFacesRight(native,true),"exploration horizontal noise retains RIGHT");
        Axis(0,short.MinValue);GameScript.inventoryOpen=true;
        Check(NativeFacesRight(native,true),"inventory navigation cannot change remembered RIGHT");
        GameScript.inventoryOpen=false;modal=true;
        Check(NativeFacesRight(native,true),"modal input cannot change remembered RIGHT");
        modal=false;GameScript.pausing=true;
        Check(NativeFacesRight(native,true),"pause input cannot change remembered RIGHT");
        GameScript.pausing=false;Axis(3,0);
        Check(!NativeFacesRight(native,true),"returning to gameplay resumes LEFT-stick facing");
        GameScript.combatMode=true;Axis(0,short.MaxValue);
        ControllerFacing.CursorX();GameScript.combatMode=false;Axis(0,0);
        Check(NativeFacesRight(native,true),"combat walking remembers RIGHT for exploration return without changing combat arbitration");
        Axis(0,short.MinValue);ControllerFacing.CursorX();Axis(0,0);
        MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,180,0);
        owned=false;Input.mousePosition=new Vector3(123,0,0);
        Check(ControllerFacing.MousePosition().x==123,"exploration ownership handoff restores exact physical mouse");
        owned=true;
        Check(NativeFacesRight(native,true),"controller reacquisition seeds native RIGHT instead of old remembered LEFT");
        Axis(0,short.MinValue);ControllerFacing.CursorX();Axis(0,0);pad.Clear();
        Check(ControllerFacing.CursorX()==ControllerCursor.staticX,"disconnect restores native cursor read");
        pad.Connect();Check(NativeFacesRight(native,true),"reconnection seeds current native facing");
        Axis(0,short.MinValue);ControllerFacing.CursorX();Axis(0,0);
        MenuScript.player=new GameObject();MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,180,0);
        Check(NativeFacesRight(native,true),"replacement player does not inherit old LEFT memory");
        Axis(0,short.MinValue);ControllerFacing.CursorX();Axis(0,0);Application.loadedLevel++;
        Check(NativeFacesRight(native,true),"scene change does not inherit old LEFT memory");
        Axis(0,short.MinValue);ControllerFacing.CursorX();Axis(0,0);NetworkView.Owned=false;
        Check(ControllerFacing.CursorX()==ControllerCursor.staticX,"remote player cannot own exploration facing");
        NetworkView.Owned=true;Check(NativeFacesRight(native,true),"local ownership recovery seeds native facing");
        Axis(0,short.MinValue);ControllerLookDirection.Configure(()=>{owned=false;return pad;});
        Check(ControllerFacing.CursorX()==ControllerCursor.staticX,"ownership loss during polling cannot expose exploration override");
        owned=true;ControllerLookDirection.Configure(()=>pad);
        GameScript.dead=true;Check(ControllerFacing.CursorX()==ControllerCursor.staticX,"death resets exploration memory");
        GameScript.dead=false;Axis(0,0);Check(NativeFacesRight(native,true),"revival seeds native facing");
        Axis(0,short.MinValue);ControllerFacingPatch.Prefix(MenuScript.player.Player);Axis(0,0);
        GameScript.dead=true;ControllerFacingPatch.Prefix(MenuScript.player.Player);
        GameScript.dead=false;ControllerFacingPatch.Prefix(MenuScript.player.Player);
        Check(NativeFacesRight(native,true),"local Update prefix resets death memory even when native facing reads are skipped");
        int polls=0;ControllerLookDirection.Configure(()=>{polls++;return pad;});
        ControllerFacingPatch.Prefix(new PlayerScript());
        Check(polls==0,"remote PlayerScript Update prefix cannot sample local walking memory");
        ControllerFacingPatch.Prefix(MenuScript.player.Player);
        Check(polls==1,"local PlayerScript Update prefix samples connected gameplay intent");


    }
    private static void WalkingCrosshair()
    {
        Setup();Vector3 point=AimFrame();
        Check(point.x < -13.9f && Math.Abs(point.y)<.01f,
            "neutral right stick + left walking immediately places white crosshair fully LEFT");
        Axis(0,short.MaxValue);point=AimFrame();
        Check(point.x>13.9f&&Math.Abs(point.y)<.01f,"walk reversal immediately places crosshair fully RIGHT without sweeping");
        Axis(0,short.MinValue);point=AimFrame();
        Check(point.x < -13.9f && Math.Abs(point.y)<.01f,"second reversal immediately returns white crosshair fully LEFT");
        Axis(0,0);Vector3 idle=AimFrame();
        Check((idle-point).sqrMagnitude<.0001f,"idle with both sticks neutral retains last horizontal aim");
        Axis(0,short.MaxValue);Axis(3,short.MinValue);point=AimFrame();
        Check(point.x < -13.9f,"explicit right-stick LEFT beats walking RIGHT");
        Axis(3,0);Axis(4,short.MinValue);
        Vector3 first=AimFrame();
        Check(first.x < -13f&&first.y>0,"explicit right-stick takeover keeps existing angular smoothing");
        for(int frame=0;frame<20;frame++)point=AimFrame();
        Check(Math.Abs(point.x)<.01f&&point.y>13.9f,"vertical explicit right stick beats horizontal walking");
        Axis(0,0);Axis(4,0);Axis(1,short.MinValue);point=AimFrame();
        Check(Math.Abs(point.x)<.01f&&point.y>13.9f,"vertical movement alone retains previous aim direction");
        Axis(0,short.MinValue);Axis(3,4000);point=AimFrame();
        Check(point.x < -13.9f&&Math.Abs(point.y)<.01f,"right-stick neutral noise still allows cardinal walking aim");
        MenuScript.player.transform.position=new Vector3(100,7,0);Axis(0,short.MaxValue);point=AimFrame();
        Check(Math.Abs(point.x-114)<.01f&&Math.Abs(point.y-7)<.01f,"walking aim follows current player location at full configured radius");
        ControllerCursor.staticX=point.x;ControllerCursor.staticY=point.y;
        owned=false;Input.mousePosition=new Vector3(-123,456,7);
        Check(!ControllerAim.TryWorldPoint(out var unowned),"same-frame mouse ownership revokes cached walking white aim");
        Vector3 attack=ControllerAim.AttackMousePosition();
        Check(attack.x==-123&&attack.y==456&&attack.z==7&&!Menuu.usingController,"neutral held left stick cannot alter physical mouse target or native ownership flag");
        Check(ControllerCursor.staticX==114&&ControllerCursor.staticY==7,"walking aim relinquishment does not rewrite native cursor coordinates");
        Setup();ControllerAim.Reset();ControllerAim.Configure(()=>pad);
        Axis(0,short.MinValue);point=AimFrame();
        Check(point.x < -13.9f&&Math.Abs(point.y)<.01f,"walking acquires aim without any prior right-stick deflection");
        modal=true;
        Check(!ControllerAim.TryWorldPoint(out _),"same-frame modal opening revokes cached walking aim");
        foreach(string guard in new[]{"mouse","modal","inventory","pause","death","build","combat","disconnect","remote","player"})
        {
            Setup();
            if(guard=="mouse")owned=false;if(guard=="modal")modal=true;if(guard=="inventory")GameScript.inventoryOpen=true;
            if(guard=="pause")GameScript.pausing=true;if(guard=="death")GameScript.dead=true;if(guard=="build")GameScript.buildMode=true;
            if(guard=="combat")GameScript.combatMode=false;if(guard=="disconnect")pad.Clear();
            if(guard=="remote")NetworkView.Owned=false;if(guard=="player")MenuScript.player=null;
            Check(!ControllerAim.TryWorldPoint(out _),"walking cannot acquire crosshair in excluded state: "+guard);
            NetworkView.Owned=true;
        }
    }
    private static void SwordSwingLock()
    {
        Setup();MenuScript.player.Player.Attacking=true;Axis(5,short.MaxValue);
        var point=AimFrame();
        Check(point.x>13.9f&&Math.Abs(point.y)<.01f,
            "active sword attacking flag holds white crosshair RIGHT despite neutral-stick walking LEFT");
        var native=Rewrite(Raw());ControllerCursor.staticX=-14;Input.mousePosition=new Vector3(-50,5,0);
        // Physical mouse changes would normally hand off ownership before this
        // query. Keep the sampled pointer stable to model stale native reads.
        Input.mousePosition=new Vector3(50,5,0);
        Check(NativeFacesRight(native,true),"locked sword facing uses held white aim instead of stale opposite controller cursor");
        Input.mousePosition=new Vector3(-50,5,0);
        Check(NativeFacesRight(native,false),"locked sword facing uses held white aim instead of opposite physical pointer");
        Input.mousePosition=new Vector3(50,5,0);
        for(int i=0;i<3;i++)
        {
            Time.unscaledTime+=10;point=AimFrame();
            Check(point.x>13.9f&&Math.Abs(point.y)<.01f,"sword lock follows attacking state rather than an elapsed timer");
        }
        Check(pad.MoveX<-.5f&&pad.RightTriggerHeld,"aim lock does not mutate walking input or held primary trigger");
        MenuScript.player.Player.Attacking=false;MenuScript.player.Player.canAttack=false;
        Check(ControllerAim.TryWorldPoint(out point)&&point.x < -13.9f&&Math.Abs(point.y)<.01f,
            "same-frame attacking=false applies held LEFT walking during canAttack=false recovery");
        ControllerCursor.staticX=14;
        Check(!NativeFacesRight(native,true)&&!NativeFacesRight(native,false),"recovery walking changes both facing branches immediately despite stale native targets");
        Check(pad.RightTriggerHeld,"release of primary trigger is not required to update recovery aim");
        MenuScript.player.Player.Attacking=true;Axis(0,short.MaxValue);point=AimFrame();
        Check(point.x < -13.9f&&Math.Abs(point.y)<.01f,"next automatic sword swing locks the newly established LEFT aim");
        Axis(3,short.MaxValue);Vector3 start=AimFrame();
        Check((start-point).sqrMagnitude>.01f,"explicit right stick can steer during a locked sword swing");
        for(int i=0;i<20;i++)point=AimFrame();
        Check(point.x>13.9f&&Math.Abs(point.y)<.01f,"explicit right stick reaches RIGHT during attacking=true");
        Axis(3,0);Axis(4,short.MinValue);
        for(int i=0;i<20;i++)point=AimFrame();
        Check(Math.Abs(point.x)<.01f&&point.y>13.9f,"explicit vertical right-stick aim remains active during sword swing");
        Axis(4,0);point=AimFrame();
        Check(Math.Abs(point.x)<.01f&&point.y>13.9f,"releasing right stick mid-swing retains explicit aim instead of walking snap");
        MenuScript.player.Player.Attacking=false;
        Check(ControllerAim.TryWorldPoint(out point)&&point.x>13.9f&&Math.Abs(point.y)<.01f,
            "held RIGHT walking immediately replaces retained vertical aim when actual swing ends");
        foreach(int weapon in new[]{300,327,349,0,299,350,399,400,450,500,550,599,600})
        {
            Setup();GameScript.equippedIDs=new[]{weapon};MenuScript.player.Player.Attacking=true;
            point=AimFrame();bool sword=weapon>=300&&weapon<350;
            Check(sword?point.x>13.9f:point.x < -13.9f,"movement aim lock respects native sword category boundary: "+weapon);
        }
        foreach(bool empty in new[]{false,true})
        {
            Setup();GameScript.equippedIDs=empty?new int[0]:null;MenuScript.player.Player.Attacking=true;point=AimFrame();
            Check(point.x < -13.9f,"missing/empty equipment cannot invent a sword swing lock");
        }
        Setup();MenuScript.player.Player=null;point=AimFrame();
        Check(point.x < -13.9f,"missing local PlayerScript does not invent a sword swing lock");
        Setup();MenuScript.player.Player.Attacking=true;point=AimFrame();owned=false;Input.mousePosition=new Vector3(-123,456,7);
        Check(!ControllerAim.TryWorldPoint(out _)&&!NativeFacesRight(native,false),"mouse ownership immediately bypasses cached sword lock and native facing override");
        Vector3 physical=ControllerAim.AttackMousePosition();
        Check(physical.x==-123&&physical.y==456&&physical.z==7,"sword lock leaves native mouse attack position exact");
        foreach(string guard in new[]{"modal","inventory","pause","death","build","combat","remote"})
        {
            Setup();MenuScript.player.Player.Attacking=true;
            if(guard=="modal")modal=true;if(guard=="inventory")GameScript.inventoryOpen=true;if(guard=="pause")GameScript.pausing=true;
            if(guard=="death")GameScript.dead=true;if(guard=="build")GameScript.buildMode=true;if(guard=="combat")GameScript.combatMode=false;
            if(guard=="remote")NetworkView.Owned=false;
            Check(!ControllerAim.TryWorldPoint(out _),"actual sword attacking cannot bypass excluded aim context: "+guard);NetworkView.Owned=true;
        }
    }
    private static void NeutralStart(bool right=false)
    {
        Setup();Axis(0,0);Axis(1,0);Axis(3,0);Axis(4,0);
        ControllerAim.Reset();ControllerAim.Configure(()=>pad);
        MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,right?180:0,0);
        Menuu.usingController=false;Input.MouseButtonDown0=false;
        Input.mousePosition=new Vector3(right?-10000:10000,-10000,0);
    }
    private static void Gate(string gate,bool enabled)
    {
        if(gate=="pause")GameScript.pausing=enabled;if(gate=="inventory")GameScript.inventoryOpen=enabled;
        if(gate=="dead")GameScript.dead=enabled;if(gate=="build")GameScript.buildMode=enabled;
        if(gate=="combat")GameScript.combatMode=!enabled;if(gate=="modal")modal=enabled;
    }
    private static Vector3 OwnedUp()
    {
        Setup();Axis(0,0);Axis(3,0);Axis(4,short.MinValue);Vector3 point=Vector3.zero;
        for(int i=0;i<20;i++)point=AimFrame();
        Axis(4,0);return point;
    }
    private static void NeutralControllerEntry()
    {
        foreach(bool right in new[]{false,true})
        {
            NeutralStart(right);
            Check(ControllerAim.TryWorldPoint(out var seeded)&&Math.Abs(seeded.x-(right?14:-14))<.01f&&Math.Abs(seeded.y)<.01f,
                "neutral controller combat entry seeds native sprite-facing white crosshair instead of opposite off-window mouse: "+right);
            Check(Menuu.usingController,"neutral acquisition claims native controller aim branch");
            Vector3 attack=ControllerAim.AttackMousePosition();
            Check((attack-seeded).sqrMagnitude<.0001f,"first neutral attack uses seeded controller aim instead of mouse");
        }
        NeutralStart();GameScript.combatMode=false;
        Check(!ControllerAim.TryWorldPoint(out _)&&!Menuu.usingController,"stationary exploration does not acquire combat aim");
        pad.Apply(new RawJoystickEvent { Type=1,Number=3,Value=1 });GameScript.combatMode=true;
        Check(ControllerAim.TryWorldPoint(out var point)&&point.x < -13.9f,
            "stationary Y combat entry with neutral sticks seeds current LEFT facing in same frame");
        Vector3 up=OwnedUp();
        Check(Math.Abs(up.x)<.01f&&up.y>13.9f,"owned up-aim fixture established");
        MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,0,0);point=AimFrame();
        Check((point-up).sqrMagnitude<.0001f,"already-owned neutral idle preserves actual UP aim instead of reseeding sprite facing");
        GameScript.talking=true;point=AimFrame();
        Check((point-up).sqrMagnitude<.0001f,"ordinary dialogue retains owned neutral UP aim");GameScript.talking=false;
        owned=false;Input.mousePosition=new Vector3(-50000,25000,0);
        Check(!ControllerAim.TryWorldPoint(out _)&&!Menuu.usingController,"mouse handoff revokes owned neutral angle");
        MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,180,0);
        owned=true;pad.Apply(new RawJoystickEvent { Type=1,Number=2,Value=1 });
        Check(ControllerAim.TryWorldPoint(out point)&&point.x>13.9f&&Math.Abs(point.y)<.01f,
            "fresh controller button with neutral sticks reseeds current RIGHT facing rather than old UP or off-window mouse");
        foreach(string gate in new[]{"pause","inventory","dead","build","combat","modal"})
        {
            up=OwnedUp();Gate(gate,true);
            Check(!ControllerAim.TryWorldPoint(out _),"same-frame unavailable context releases aim: "+gate);
            MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,0,0);Gate(gate,false);
            Check(ControllerAim.TryWorldPoint(out point)&&point.x < -13.9f&&Math.Abs(point.y)<.01f,
                "neutral return seeds current facing without right-stick deflection: "+gate);
        }
        up=OwnedUp();MenuScript.player=new GameObject();
        MenuScript.player.transform.position=new Vector3(100,7,0);
        MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,0,0);Input.mousePosition=new Vector3(10000,10000,0);
        Check(ControllerAim.TryWorldPoint(out point)&&Math.Abs(point.x-86)<.01f&&Math.Abs(point.y-7)<.01f,
            "same-frame player replacement clears old UP angle and mouse history before seeding new LEFT body");
        foreach(string unavailable in new[]{"mouse","remote","player","disconnect"})
        {
            NeutralStart();
            if(unavailable=="mouse")owned=false;if(unavailable=="remote")NetworkView.Owned=false;
            if(unavailable=="player")MenuScript.player=null;if(unavailable=="disconnect")pad.Clear();
            Check(!ControllerAim.TryWorldPoint(out _)&&!Menuu.usingController,"neutral acquisition respects unavailable ownership/body/pad: "+unavailable);
            NetworkView.Owned=true;
        }
        foreach(string missing in new[]{"component","sprite","yaw"})
        {
            NeutralStart();
            if(missing=="component")MenuScript.player.Player=null;
            if(missing=="sprite")MenuScript.player.Player.e=null;
            if(missing=="yaw")MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,float.NaN,0);
            Check(ControllerAim.TryWorldPoint(out point)&&point.x>13.9f&&Math.Abs(point.y)<.01f,
                "unavailable native facing gets deterministic full-radius safe aim, never mouse: "+missing);
        }
        NeutralStart();ControllerAim.Configure(()=>{owned=false;return pad;});
        Check(!ControllerAim.TryWorldPoint(out _)&&!Menuu.usingController,
            "state polling cannot seed or claim native controller aim after revoking UsingController");
        NeutralStart();bool revoke=false;ControllerAim.Configure(()=>{if(revoke)owned=false;return pad;});
        Check(ControllerAim.TryWorldPoint(out point)&&Menuu.usingController,"owned neutral aim acquired before live polling handoff");
        revoke=true;Time.frameCount++;
        Check(!ControllerAim.TryWorldPoint(out _)&&!Menuu.usingController,"live state-poll handoff releases existing native controller flag before returning aim");
        NeutralStart();ControllerAim.Configure(()=>pad,7f);
        Check(ControllerAim.TryWorldPoint(out point)&&Math.Abs(point.x+7)<.01f,"neutral seed respects configured aim radius");
        NeutralStart();Axis(3,short.MaxValue);
        Check(ControllerAim.TryWorldPoint(out point)&&point.x>13.9f,"explicit right-stick input outranks initial LEFT sprite facing");
        NeutralStart();Axis(0,short.MaxValue);
        Check(ControllerAim.TryWorldPoint(out point)&&point.x>13.9f,"allowed walking outranks initial LEFT sprite facing");
        NeutralStart(true);MenuScript.player.Player.Attacking=true;Axis(0,short.MinValue);
        Check(ControllerAim.TryWorldPoint(out point)&&point.x>13.9f,"first neutral acquisition during sword swing seeds RIGHT facing instead of blocked LEFT walking");
        var native=Rewrite(Raw());ControllerCursor.staticX=-1000;Input.mousePosition=new Vector3(-1000,0,0);
        Check(NativeFacesRight(native,true)&&NativeFacesRight(native,false),"first-use sword lock faces its seeded white aim despite dirty native positions");
        Input.mousePosition=new Vector3(-10000,-10000,0);MenuScript.player.Player.Attacking=false;
        Check(ControllerAim.TryWorldPoint(out point)&&point.x < -13.9f,"walking takes priority immediately after initialized sword swing ends");
    }
    private static void NeutralNativeFacingEntry()
    {
        Setup();Axis(0,0);Axis(3,0);ControllerAim.Reset();ControllerAim.Configure(()=>pad);
        MenuScript.player.Player.e.transform.eulerAngles=new Vector3(0,0,0);
        Menuu.usingController=false;Input.mousePosition=new Vector3(10000,-10000,0);
        ControllerCursor.staticX=10000;
        var native=Rewrite(Raw());
        Check(!NativeFacesRight(native,false)&&!NativeFacesRight(native,true),
            "native facing reads seed LEFT sprite before stale mouse or controller cursor can flip it on neutral entry");
    }
    private static void Main(string[] args)
    {
        if(args.Length>0&&args[0]=="native-entry") { NeutralNativeFacingEntry();Console.WriteLine("Native neutral entry checks passed.");return; }
        NeutralNativeFacingEntry();
        NeutralControllerEntry();
        ExplorationFacing();
        WalkingCrosshair();
        SwordSwingLock();
        Setup();var instructions=Rewrite(Raw());
        Check(!NativeFacesRight(instructions,true),"neutral right stick + left walk faces LEFT even though retained controller cursor is RIGHT");
        Check(!NativeFacesRight(instructions,false),"neutral right stick + left walk faces LEFT before native usingController flag updates");
        Axis(0,short.MaxValue);Check(NativeFacesRight(instructions,true)&&NativeFacesRight(instructions,false),"right walk faces right in both native input branches");
        Axis(0,0);Check(NativeFacesRight(instructions,true),"idle retains native cursor-facing behavior");
        Axis(0,short.MinValue);Axis(3,short.MaxValue);Check(NativeFacesRight(instructions,true),"explicit right aim beats left walking");
        Axis(3,0);Axis(4,short.MinValue);Check(NativeFacesRight(instructions,true),"vertical right-stick aim also beats walking facing");
        Axis(4,0);Axis(0,-4000);Check(NativeFacesRight(instructions,true),"movement noise does not change facing");
        Axis(0,short.MinValue);Axis(3,4000);Check(!NativeFacesRight(instructions,true),"right-stick noise within aim deadzone still permits walking facing");
        Axis(3,0);
        foreach(string guard in new[]{"mouse","modal","inventory","pause","death","build","disconnect","remote","player"})
        {
            Setup();instructions=Rewrite(Raw());
            if(guard=="mouse")owned=false;if(guard=="modal")modal=true;if(guard=="inventory")GameScript.inventoryOpen=true;
            if(guard=="pause")GameScript.pausing=true;if(guard=="death")GameScript.dead=true;if(guard=="build")GameScript.buildMode=true;
            if(guard=="combat")GameScript.combatMode=false;if(guard=="disconnect")pad.Clear();
            if(guard=="remote")NetworkView.Owned=false;if(guard=="player")MenuScript.player=null;
            Check(NativeFacesRight(instructions,true),"native cursor read retained outside eligible controller walking: "+guard);
            NetworkView.Owned=true;
        }
        Setup();instructions=Rewrite(Raw());owned=false;Input.mousePosition=new Vector3(-50,0,0);
        Check(!NativeFacesRight(instructions,false),"mouse handoff keeps exact native mouse-facing X");
        foreach(var counts in new[]{new[]{0,1},new[]{1,0},new[]{2,1},new[]{1,2}})
        {
            var raw=Raw(counts[0],counts[1]);var changed=Rewrite(raw);
            Check(changed.Count==raw.Count&&changed.Zip(raw,(a,b)=>a.opcode==b.opcode&&Equals(a.operand,b.operand)).All(x=>x),"unknown native read counts left unchanged "+counts[0]+"/"+counts[1]);
        }
        Setup();instructions=Rewrite(Raw());
        MenuScript.player.transform.position=new Vector3(100,7,0);ControllerCursor.staticX=114;
        Check(!NativeFacesRight(instructions,true)&&!NativeFacesRight(instructions,false),"facing offset follows moved player, not world origin");
        Check(ControllerCursor.staticX==114,"facing never writes the shared controller cursor target");
        Axis(3,short.MaxValue);Axis(0,short.MinValue);
        Check(NativeFacesRight(instructions,true),"right-stick priority is live even after neutral-facing query in same frame");
        Axis(3,0);Check(!NativeFacesRight(instructions,true),"neutral walking regains facing in same frame without aim cache refresh");
        owned=false;Input.mousePosition=new Vector3(123,456,7);ControllerCursor.staticX=789;
        var mouseRead=(MethodInfo)instructions[0].operand;var cursorRead=(MethodInfo)instructions[1].operand;
        var nativeMouse=(Vector3)mouseRead.Invoke(null,null);
        Check(nativeMouse.x==123&&nativeMouse.y==456&&nativeMouse.z==7&&(float)cursorRead.Invoke(null,null)==789,"mouse ownership preserves exact coordinates and native controller field");
        Setup();var original=Raw();
        var method=new DynamicMethod("labels",typeof(void),Type.EmptyTypes);
        Label label=method.GetILGenerator().DefineLabel();original[0].labels.Add(label);
        original[1].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var patched=Rewrite(original);
        Check(original[0].operand is MethodInfo native&&native.DeclaringType==typeof(Input)&&original[1].opcode==OpCodes.Ldsfld,"transpiler does not mutate original instructions");
        Check(patched.Count==original.Count&&patched[0].labels.Contains(label)&&patched[1].blocks.Count==1,"native labels and exception boundaries preserved");
        Console.WriteLine("Neutral walking facing: "+checks+" checks passed.");
    }
}
public class PlayerScript { public GameObject e=new GameObject(false); private bool attacking; public bool canAttack; public bool Attacking { get { return attacking; } set { attacking=value; } } }
namespace UnityEngine { public static class Debug { public static void LogWarning(string text){} } }
namespace GadgetCore.API { [AttributeUsage(AttributeTargets.Class)]public class HarmonyGadget:Attribute { public HarmonyGadget(string name){} } }
