using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks, consumed;
    private static JoystickInputState pad;
    private static GameScript game;
    private static Collider[] choices;
    private static ButtonMenu[] highlights;
    private static bool owned;
    private static void Check(bool value, string why)
    { checks++; if (!value) throw new Exception(why); }
    private static void Button(int n, bool held)
    { pad.Apply(new RawJoystickEvent { Type=1, Number=(byte)n, Value=(short)(held?1:0) }); }
    private static void Axis(int n, short v)
    { pad.Apply(new RawJoystickEvent { Type=2, Number=(byte)n, Value=v }); }
    private static void Tick(Action action=null)
    {
        pad.BeginFrame(); if(action!=null) action();
        Time.unscaledTime+=.01f; ControllerFrontEndMenus.Begin(game);
    }
    private static void Setup(string config=null)
    {
        ControllerFrontEndMenus.Reset(); owned=true; consumed=0;
        ControllerInputOwnership.Configure(()=>owned,()=>game!=null && game.menuStory.activeInHierarchy);
        GameScript.pausing=GameScript.dead=false; Input.Left=Input.Right=Input.NativeCancel=Input.EscapeDown=false;
        Input.mousePosition=new Vector3(100,100,1); Physics.Hit=null; SelectionCornerRenderer.Visible=null;
        pad=new JoystickInputState(); pad.Connect();
        game=new GameScript { menuStory=new GameObject("menuStory"), menuPause=new GameObject("pause") };
        choices=new Collider[3]; highlights=new ButtonMenu[3];
        // Deliberately reverse scene child order: initial choice uses native choice index.
        for(int i=2;i>=0;i--)
        {
            var go=new GameObject("bChoice"+i); game.menuStory.AddChild(go);
            choices[i]=new Collider(go) { bounds=new Bounds { center=new Vector3(0,3-i,1),size=new Vector3(4,1,0) } };
            go.AddComponent<Renderer>(); go.AddComponent<AudioSource>();
            highlights[i]=go.AddComponent<ButtonMenu>(); highlights[i].button=new Material(); highlights[i].buttonSelect=new Material();
            highlights[i].txt[0]=new GameObject("text").AddComponent<TextMesh>(); highlights[i].txt[0].gameObject.AddComponent<Animation>();
        }
        ControllerFrontEndMenus.Configure(()=>pad,BindingConfig.Parse((config??BindingConfig.DefaultText).Split('\n')),null,()=>consumed++);
    }
    private static void Main()
    {
        Setup(); Physics.Hit=choices[2]; Tick();
        Check(ControllerFrontEndMenus.SelectedCollider==choices[0],"story entry starts first native choice, not child order/physical pointer");
        Check(choices[0].gameObject.GetComponent<Renderer>().material==highlights[0].buttonSelect,"story focus uses native highlight");
        Check(SelectionCornerRenderer.Visible==null,"no corners when native highlight available");
        Check(consumed==1 && ControllerInputOwnership.GameplayBlocked,"story consumes gameplay while native inventory/talking flags are absent");
        Check(!ControllerFrontEndMenus.MouseButtonDown(0),"entry never activates");
        Tick(()=>Axis(7,short.MaxValue)); Check(ControllerFrontEndMenus.SelectedCollider==choices[1],"Dpad down moves one option");
        Check(choices[0].gameObject.GetComponent<Renderer>().material==highlights[0].button,"old highlight restores native material");
        Check(choices[1].gameObject.GetComponent<Renderer>().material==highlights[1].buttonSelect,"new choice gets native material");
        Tick(()=>Axis(7,0)); Tick(()=>Axis(1,short.MaxValue)); Check(ControllerFrontEndMenus.SelectedCollider==choices[2],"left stick selects bottom option");
        Tick(()=>Axis(1,0)); Tick(()=>Axis(4,short.MinValue)); Check(ControllerFrontEndMenus.SelectedCollider==choices[1],"right stick selects upper option");
        Tick(()=>Axis(4,0)); Physics.Hit=choices[1]; Tick(()=>Button(0,true));
        Check(ControllerFrontEndMenus.MouseButtonDown(0),"fresh configurable Activate supplies native click");
        Vector3 mouse=ControllerFrontEndMenus.MousePosition(); Check(mouse.y==choices[1].bounds.center.y,"native raycast uses selected choice center");
        ControllerFrontEndMenus.End(); Check(!ControllerFrontEndMenus.MouseButtonDown(0),"virtual click ends with native Update");
        Tick(); Check(!ControllerFrontEndMenus.MouseButtonDown(0),"held Activate cannot repeat");
        Tick(()=>Button(0,false)); Tick(()=>Button(0,true)); Check(ControllerFrontEndMenus.MouseButtonDown(0),"release/repress can activate again if native did not close");
        game.menuStory.SetActive(false); ControllerFrontEndMenus.End(); Tick();
        Check(ControllerFrontEndMenus.SelectedCollider==null && !ControllerFrontEndMenus.MouseButtonDown(0),"native close clears selection and virtual click");
        Check(choices[1].gameObject.GetComponent<Renderer>().material==highlights[1].button,"native close restores highlight");

        foreach(int heldButton in new[]{0,1})
        {
            Setup(); Button(heldButton,true); Tick(); Tick();
            Check(game.LeaveCalls==0 && !ControllerFrontEndMenus.MouseButtonDown(0),"held entry button cannot activate/cancel "+heldButton);
            Tick(()=>Button(heldButton,false)); Tick(()=>Button(heldButton,true));
            Check(heldButton==0 ? ControllerFrontEndMenus.MouseButtonDown(0)==false : game.LeaveCalls==1,"fresh edge works after release (activation ray must validate) "+heldButton);
        }
        Setup(); Tick(); Tick(()=>Button(1,true));
        Check(game.LeaveCalls==1 && !game.menuStory.activeInHierarchy,"Back uses native LeaveNPC once and dismisses");
        Check(!ControllerFrontEndMenus.MouseButtonDown(0) && ControllerFrontEndMenus.SelectedCollider==null,"Back cannot select an outcome");
        Check(consumed==2,"Back frame consumed before root is hidden");
        Tick(); Check(game.LeaveCalls==1,"held Back never repeats");
        game.menuStory.SetActive(true); Tick(); Check(game.LeaveCalls==1,"reopen while Back held cannot dismiss");
        Tick(()=>Button(1,false)); Tick(()=>Button(1,true)); Check(game.LeaveCalls==2,"reopened Back re-arms on release");

        foreach(string bind in new[]{"Joystick1Button12","Unbound"})
        {
            Setup(BindingConfig.DefaultText.Replace("MenuCancel = Joystick1Button1","MenuCancel = "+bind)); Tick();
            Tick(()=>Button(1,true)); Check(game.LeaveCalls==0,"default B follows remap/unbound "+bind);
            Tick(()=>Button(1,false)); Tick(()=>Button(12,true)); Check(game.LeaveCalls==(bind=="Unbound"?0:1),"custom Back follows config "+bind);
        }
        Setup(BindingConfig.DefaultText.Replace("MenuActivate = Joystick1Button0","MenuActivate = Joystick1Button12")); Tick(); Physics.Hit=choices[0];
        Tick(()=>Button(0,true)); Check(!ControllerFrontEndMenus.MouseButtonDown(0),"default A disabled by Activate remap");
        Tick(()=>Button(12,true)); Check(ControllerFrontEndMenus.MouseButtonDown(0),"remapped Activate clicks native choice");

        for(int hidden=0;hidden<3;hidden++)
        {
            Setup(); choices[hidden].gameObject.SetActive(false); Tick();
            Check(ControllerFrontEndMenus.SelectedCollider==choices[hidden==0?1:0],"entry skips hidden choice "+hidden);
            for(int i=0;i<5;i++) { Tick(()=>Axis(7,0)); Tick(()=>Axis(7,short.MaxValue)); Check(ControllerFrontEndMenus.SelectedCollider!=choices[hidden],"navigation skips hidden option "+hidden); }
        }
        Setup(); foreach(var c in choices)c.gameObject.SetActive(false); Tick(); Tick(()=>Button(0,true));
        Check(ControllerFrontEndMenus.SelectedCollider==null&&!ControllerFrontEndMenus.MouseButtonDown(0),"no visible options cannot create synthetic choice");
        Tick(()=>Button(1,true)); Check(game.LeaveCalls==1,"empty custom choice screen can still cancel");

        Setup(); Tick(); Physics.Hit=choices[2]; owned=false; Input.Left=true;
        Check(ControllerFrontEndMenus.MouseButtonDown(0),"physical click survives ownership handoff");
        Check(ControllerFrontEndMenus.MousePosition().x==Input.mousePosition.x,"mouse position exact on handoff");
        Check(!ControllerInputOwnership.GameplayBlocked,"modal policy does not change native mouse mode");
        Check(ControllerFrontEndMenus.SelectedCollider==null,"mouse handoff clears controller focus");
        Check(choices[2].gameObject.GetComponent<Renderer>().material==highlights[2].buttonSelect,"stationary physical hover restored on handoff");
        Input.Left=false; owned=true; Tick(); Check(ControllerFrontEndMenus.SelectedCollider==choices[0],"controller re-entry selects first visible option");
        Input.mousePosition=new Vector3(101,100,1); Tick(); Check(ControllerFrontEndMenus.SelectedCollider==choices[2],"physical movement follows physical hover");

        Setup(); Tick(); GameScript.dead=true; Tick(); Check(ControllerFrontEndMenus.SelectedCollider==null,"death hides choice focus");
        GameScript.dead=false; Tick(); Check(ControllerFrontEndMenus.SelectedCollider==choices[0],"context recovery resets focus");
        game.menuStory.SetActive(false); Tick(); game.menuStory=new GameObject("replacement"); Tick(); Check(ControllerFrontEndMenus.SelectedCollider==null,"scene/root replacement releases old collider");
        Setup(); Tick(); pad.Clear(); Tick(); Check(!ControllerFrontEndMenus.MouseButtonDown(0),"disconnect cannot select");
        Setup(); Tick(); game.menuEmblemForge=new GameObject("contradictory station"); Tick(()=>Button(1,true));
        Check(game.LeaveCalls==0 && game.menuStory.activeInHierarchy,"conflicting station root cannot invoke a different LeaveNPC branch");
        Setup(); Tick(); GameScript.pausing=true; Tick(); Check(ControllerFrontEndMenus.SelectedCollider==null,"pause context takes priority over story");
        GameScript.pausing=false; Tick(); Check(ControllerFrontEndMenus.SelectedCollider==choices[0],"resume restores first story choice");
        ControllerFrontEndMenus.Reset(); ControllerInputOwnership.Reset();
        Check(!ControllerInputOwnership.GameplayBlocked,"unload clears modal provider");
        Console.WriteLine("Story choice menus: "+checks+" checks passed.");
    }
}
