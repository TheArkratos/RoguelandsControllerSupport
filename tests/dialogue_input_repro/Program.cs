using System;
using ControllerSupport;
using UnityEngine;
using GadgetCore.API;

internal static class Program
{
    private static int checks;
    private static uint timestamp;
    private static JoystickInputState pad;
    private static GameScript game;
    private static PlayerScript localPlayer;

    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    private static void Button(int button, bool held)
    {
        pad.Apply(new RawJoystickEvent { Type = 1, Number = (byte)button,
            Value = (short)(held ? 1 : 0), TimeMilliseconds = ++timestamp });
    }
    private static void Tick(Action input = null)
    {
        Time.frameCount++;
        pad.BeginFrame(); Input.Down.Clear(); Input.MouseDown.Clear();
        if (input != null) input();
        ControllerDialogueInput.Prepare(game);
        ControllerGameplayActions.Prepare(game);
    }
    private static void Setup(string text = null)
    {
        game = new GameScript(); pad = new JoystickInputState(); pad.Connect();
        localPlayer = new PlayerScript(); MenuScript.player = localPlayer.gameObject;
        GameScript.inventoryOpen = GameScript.pausing = GameScript.dead =
            GameScript.buildMode = GameScript.combatMode = PlayerScript.beaming = GadgetCoreAPI.Frozen = false;
        GameScript.talking = true; MenuTalking.isWriting = true;
        var config = BindingConfig.Parse((text ?? BindingConfig.DefaultText).Split('\n'));
        ControllerGameplayInput.Configure(() => pad, config);
        ControllerGameplayActions.Configure(() => pad, config);
        ControllerDialogueInput.Configure(() => pad, config);
        Tick();
    }
    private static void Main()
    {
        Setup(); Tick(() => Button(2, true));
        Check(game.menuTalking.Clicks == 1 && game.Talks == 0, "X reveals writing without advancing");
        ControllerDialogueInput.Prepare(game);
        Check(game.menuTalking.Clicks == 1 && game.Talks == 0, "multiple calls in a frame cannot double-dispatch");
        Tick(); Check(game.Talks == 0, "held X cannot advance revealed text");
        Tick(() => Button(2, false)); Tick(() => Button(2, true));
        Check(game.Talks == 1 && game.menuTalking.Clicks == 1, "fresh X advances through native Talk");
        Check(game.LastArgs[0] == 99 && game.LastArgs[1] == 1 && game.LastArgs[2] == 2,
            "native dialogue coordinates retained");
        Tick(); Check(game.Talks == 1 && game.menuTalking.Clicks == 1, "held advance cannot reveal next line");
        Tick(() => Button(2, false)); Tick(() => Button(2, true));
        Check(game.menuTalking.Clicks == 2 && game.Talks == 1, "new line reveals on next fresh edge");

        Setup(); game.menuTalking.activeInHierarchy = false; GameScript.talking = false; Tick();
        Tick(() => { Button(2, true); game.menuTalking.activeInHierarchy = true; GameScript.talking = true; });
        Tick(); Check(game.menuTalking.Clicks == 0, "opening X is not reused for reveal");
        Tick(() => Button(2, false)); Tick(() => Button(2, true));
        Check(game.menuTalking.Clicks == 1, "release arms dialogue after entry");

        Setup(); Tick(() => { Button(2, true); Input.MouseDown.Add(0); });
        Check(game.menuTalking.Clicks == 0 && game.Talks == 0, "physical click retains sole native dispatch");
        game.menuTalking.SendMessage("Click"); // Native click branch replay.
        Tick(); Check(game.menuTalking.Clicks == 1 && game.Talks == 0, "simultaneous click and X advance only once");

        Setup(); GameScript.talking = false; Tick(() => Button(2, true));
        Check(game.menuTalking.Clicks == 1, "timed native popup works without talking flag");
        Check(!ControllerGameplayInput.PlayerInteractionButtonDown("Up", localPlayer), "timed popup consumes interaction");
        Tick(() => Button(2, false)); MenuTalking.isWriting = false; game.CloseOnTalk = true;
        Tick(() => { Button(2, true); Button(0, true); Button(3, true); Input.Down.Add("CombatMode"); });
        Check(game.Talks == 1 && !game.menuTalking.activeInHierarchy, "last advance uses native close");
        Check(!ControllerGameplayInput.GameButtonDown("UseItem5"), "close frame cannot trigger hotkey");
        Check(!ControllerGameplayInput.GameButtonDown("CombatMode"), "close frame cannot toggle combat");
        Tick(); Check(!ControllerGameplayInput.PlayerInteractionButtonDown("Up", localPlayer),
            "held close press remains consumed after popup disappears");
        Tick(() => Button(2, false)); Tick(() => Button(2, true));
        Check(ControllerGameplayInput.PlayerInteractionButtonDown("Up", localPlayer),
            "fresh Interact resumes native NPC interaction after close");

        Setup(); game.menuTalking.activeInHierarchy = false; GameScript.talking = false; Tick();
        Tick(() => Button(2, true));
        Check(game.Talks == 0 && game.menuTalking.Clicks == 0, "inactive popup cannot consume dialogue advance");
        Check(ControllerGameplayInput.PlayerInteractionButtonDown("Up", localPlayer),
            "ordinary interaction unaffected outside popup");

        foreach (string blocked in new[] { "freeze", "pause", "dead", "build", "inventory", "beam", "remote", "missing", "exit" })
        {
            Setup();
            if (blocked == "freeze") GadgetCoreAPI.Frozen = true;
            if (blocked == "pause") GameScript.pausing = true;
            if (blocked == "dead") GameScript.dead = true;
            if (blocked == "build") GameScript.buildMode = true;
            if (blocked == "inventory") GameScript.inventoryOpen = true;
            if (blocked == "beam") PlayerScript.beaming = true;
            if (blocked == "remote") MenuScript.player.view.isMine = false;
            if (blocked == "missing") MenuScript.player = null;
            if (blocked == "exit") game.SetExiting(true);
            Tick(() => Button(2, true));
            Check(game.menuTalking.Clicks == 0 && game.Talks == 0, "blocked context " + blocked);
            GadgetCoreAPI.Frozen = GameScript.pausing = GameScript.dead = GameScript.buildMode =
                GameScript.inventoryOpen = PlayerScript.beaming = false;
            MenuScript.player = new GameObject(); game.SetExiting(false);
            Tick(); Check(game.menuTalking.Clicks == 0, "blocked held press not replayed after " + blocked);
            Tick(() => Button(2, false)); Tick(() => Button(2, true));
            Check(game.menuTalking.Clicks == 1, "fresh edge recovers after " + blocked);
        }

        foreach (string change in new[] { "scene", "owner", "player", "popup", "disconnect" })
        {
            Setup();
            if (change == "scene") Application.loadedLevel++;
            if (change == "owner") game = new GameScript();
            if (change == "player") MenuScript.player = new GameObject();
            if (change == "popup") game.menuTalking = new GameObject();
            if (change == "disconnect") pad.Clear();
            Tick(() => Button(2, true));
            Check(game.menuTalking.Clicks == 0, "context change disarms press: " + change);
            if (change == "disconnect") pad.Connect();
            Tick(); Check(game.menuTalking.Clicks == 0, "context change cannot replay held edge: " + change);
        }

        Setup(BindingConfig.DefaultText.Replace("Interact = Joystick1Button2", "Interact = Joystick1Button12"));
        Tick(() => Button(2, true)); Check(game.menuTalking.Clicks == 0, "old X disabled when remapped");
        Tick(() => Button(12, true)); Check(game.menuTalking.Clicks == 1, "configured Interact advances dialogue");
        Setup(BindingConfig.DefaultText.Replace("Interact = Joystick1Button2", "Interact = Unbound"));
        Tick(() => Button(2, true)); Check(game.menuTalking.Clicks == 0, "unbound Interact remains disabled");
        Setup(); ControllerDialogueInput.Reset(); Tick(() => Button(2, true));
        Check(game.menuTalking.Clicks == 0, "unload resets input provider");
        Console.WriteLine("PASS: " + checks + " dialogue input checks");
    }
}
