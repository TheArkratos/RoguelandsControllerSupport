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
    private static int assistToggles;

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
    private static void Axis(int axis, short value)
    {
        pad.Apply(new RawJoystickEvent { Type = 2, Number = (byte)axis,
            Value = value, TimeMilliseconds = ++timestamp });
    }
    private static void Tick(Action input = null)
    {
        Time.frameCount++;
        pad.BeginFrame(); Input.Down.Clear(); Input.MouseDown.Clear(); Input.Axes.Clear();
        if (input != null) input();
        ControllerDialogueInput.Prepare(game);
        ControllerGameplayActions.Prepare(game);
        ControllerGameplayInput.PlayerAxis("Attack"); // Native player reads this each Update.
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
        assistToggles = 0;
        ControllerGameplayActions.Configure(() => pad, config, () => assistToggles++);
        ControllerDialogueInput.Configure(() => pad, config);
        Tick();
    }
    private static void Main()
    {
        // Native EnterTalking2 uses this panel without setting talking or immobilizing
        // the player. Exercise the real shared gameplay adapter, not fake flags.
        Setup(); GameScript.talking = false; Tick();
        Tick(() => { Axis(0, short.MaxValue); Axis(2, short.MaxValue); Button(4, true); });
        Check(ControllerGameplayInput.PlayerButton("Right"), "timed popup preserves horizontal movement");
        Check(ControllerGameplayInput.PlayerButtonDown("Jump"), "timed popup preserves controller jump");
        Check(ControllerGameplayInput.PlayerButtonDown("Dash Right"), "timed popup preserves controller dash");
        Setup(); GameScript.talking = false; GameScript.combatMode = true; Tick();
        Tick(() => Axis(5, short.MaxValue));
        Check(ControllerGameplayInput.PlayerAxis("Attack") < 0, "timed popup preserves controller attack");
        Setup(); GameScript.talking = false; Tick(); Tick(() => Button(0, true));
        Check(ControllerGameplayInput.GameButtonDown("UseItem5"), "timed popup preserves controller hotkey");
        Setup(); GameScript.talking = false; Tick(); Tick(() => Button(3, true));
        Check(ControllerGameplayInput.GameButtonDown("CombatMode"), "timed popup preserves combat-mode switch");
        Setup(); GameScript.talking = false; Tick(); Tick(() => Button(7, true));
        Check(ControllerGameplayInput.GamePausePressed(), "timed popup preserves controller pause");

        foreach (bool nativeConversation in new[] { false, true })
        {
            Setup(); GameScript.talking = nativeConversation; GameScript.combatMode = true; Tick();
            Tick(() => { Button(2, true); Button(0, true); Button(9, true); Axis(0, short.MaxValue);
                Axis(2, short.MaxValue); Axis(5, short.MaxValue); Button(4, true); });
            Check(game.menuTalking.Clicks == 1, "Interact still reveals with simultaneous gameplay inputs");
            Check(ControllerGameplayInput.PlayerButtonDown("Jump") &&
                ControllerGameplayInput.PlayerButtonDown("Dash Right"), "dialogue preserves jump/dash, talking=" + nativeConversation);
            Check(ControllerGameplayInput.PlayerAxis("Attack") < 0, "dialogue preserves attack, talking=" + nativeConversation);
            Check(ControllerGameplayInput.GameButtonDown("UseItem5") && assistToggles == 1,
                "dialogue preserves independent hotkey/assist, talking=" + nativeConversation);
            Tick(); Check(ControllerGameplayInput.PlayerAxis("Attack") < 0,
                "dialogue does not interrupt held attack after reveal");
            Check(!ControllerGameplayInput.PlayerInteractionButtonDown("Up", localPlayer),
                "revealing dialogue never also interacts with NPC");
        }
        Setup(); Tick(() => Button(3, true));
        Check(!ControllerGameplayInput.GameButtonDown("CombatMode"), "native NPC conversation still gates combat switching");
        Setup(); Tick(() => { Input.Down.Add("Jump"); Input.Down.Add("CombatMode"); });
        Check(ControllerGameplayInput.PlayerButtonDown("Jump") && ControllerGameplayInput.GameButtonDown("CombatMode"),
            "physical keyboard keeps native reads for caller-owned restrictions");
        Setup(); Tick(() => { Axis(0, short.MaxValue); Axis(2, short.MaxValue); });
        GameScript.pausing = true;
        Check(!ControllerGameplayInput.PlayerButtonDown("Jump"), "real pause still gates controller jump");

        // Configuration forbids same-context duplicates. Reassign the old
        // owner when moving Interact onto a native legacy controller input.
        Setup(BindingConfig.DefaultText.Replace("Interact = Joystick1Button2", "Interact = Joystick1Button3")
            .Replace("CombatMode = Joystick1Button3", "CombatMode = Joystick1Button12"));
        GameScript.talking = false; Tick();
        Tick(() => { Button(3, true); Button(0, true); Input.Down.Add("CombatMode"); });
        Check(game.menuTalking.Clicks == 1 && !ControllerGameplayInput.GameButtonDown("CombatMode"),
            "remapped Y reveals without legacy mode-switch leak");
        Check(ControllerGameplayInput.GameButtonDown("UseItem5"), "consumed legacy Y does not block unrelated A hotkey");

        Setup(BindingConfig.DefaultText.Replace("Interact = Joystick1Button2", "Interact = RightTrigger")
            .Replace("PrimaryAction = RightTrigger", "PrimaryAction = Joystick1Button12"));
        GameScript.talking = false; GameScript.combatMode = true; Tick();
        Tick(() => { Axis(5, short.MaxValue); Input.Axes["Attack"] = -1; });
        Check(game.menuTalking.Clicks == 1 && ControllerGameplayInput.PlayerAxis("Attack") == 0,
            "remapped RT reveals without native attack-axis leak");
        MenuTalking.isWriting = false; game.CloseOnTalk = true; Tick(() => Axis(5, short.MinValue));
        Tick(() => Axis(5, short.MaxValue)); Tick(() => { Input.Axes["Attack"] = -1; Axis(2, short.MaxValue); });
        Check(ControllerGameplayInput.PlayerAxis("Attack") == 0 && ControllerGameplayInput.PlayerButtonDown("Jump"),
            "held closing RT remains consumed while independent jump works");
        Tick(() => Axis(5, short.MinValue)); Tick(() => Button(12, true));
        Check(ControllerGameplayInput.PlayerAxis("Attack") < 0, "new configured attack works after close release");

        Setup(BindingConfig.DefaultText.Replace("Interact = Joystick1Button2", "Interact = Joystick1Button5"));
        GameScript.talking = false; Tick();
        Tick(() => { Button(5, true); Input.Down.Add("Droid"); Button(0, true); });
        Check(game.menuTalking.Clicks == 1 && !ControllerGameplayInput.GameDroidButtonDown("Droid", game),
            "remapped RB reveals without native droid activation");
        Check(ControllerGameplayInput.GameButtonDown("UseItem5"), "remapped RB leaves unrelated hotkey active");
        Setup(BindingConfig.DefaultText.Replace("Interact = Joystick1Button2", "Interact = Joystick1Button1")
            .Replace("Hotkey6 = Joystick1Button1", "Hotkey6 = Joystick1Button12"));
        GameScript.talking = false; Tick();
        Tick(() => { Button(1, true); Input.Down.Add("Cancel"); });
        Check(game.menuTalking.Clicks == 1 && !ControllerGameplayInput.GameButtonDown("Cancel"),
            "remapped B reveals without legacy cancel activation");
        Setup(); GameScript.talking = false; Tick();
        MenuTalking.isWriting = false; game.CloseOnTalk = true;
        Tick(() => { Button(2, true); Button(0, true); });
        Check(game.Talks == 1 && ControllerGameplayInput.GameButtonDown("UseItem5"),
            "closing X consumes only Interact and permits simultaneous independent A");
        Tick(() => { Axis(2, short.MaxValue); Button(3, true); });
        Check(ControllerGameplayInput.PlayerButtonDown("Jump") && ControllerGameplayInput.GameButtonDown("CombatMode"),
            "held closing X does not hold unrelated movement or mode-switch hostage");

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
        Check(!ControllerGameplayInput.GameButtonDown("UseItem5"), "simultaneous mode-switch preserves existing hotkey gate");
        Check(ControllerGameplayInput.GameButtonDown("CombatMode"), "close frame preserves independent combat-mode press");
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
