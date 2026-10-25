using System;
using System.IO;
using GadgetCore;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    [Gadget("Controller Support", true, true, null, null, 0, null, false, false)]
    public sealed class ControllerSupportGadget : Gadget
    {
        private BindingConfig bindings;
        private InputProbe probe;
        private GameObject probeObject;

        protected override void LoadConfig()
        {
            string path = Path.Combine(GadgetPaths.ConfigsPath, "ControllerSupportBindings.ini");
            try
            {
                bindings = BindingConfig.Load(path);
                Logger.Log("Bindings validated from " + path + ". Restart after editing.");
            }
            catch (Exception error)
            {
                // Invalid user files remain untouched. The probe still runs without gameplay changes.
                bindings = BindingConfig.SafeDefaults();
                Logger.LogWarning("Bindings invalid at " + path + ": " + error.Message +
                    ". File preserved; using safe, unbound defaults for this session.", false);
            }
        }

        protected override void Initialize()
        {
            probe = new InputProbe(Logger, bindings ?? BindingConfig.SafeDefaults());
            ControllerInputOwnership.Configure(() => probe != null && probe.UsingController);
            ControllerGameplayInput.Configure(probe.PollState, bindings ?? BindingConfig.SafeDefaults());
            ControllerDialogueInput.Configure(probe.PollState, bindings ?? BindingConfig.SafeDefaults());
            ControllerAim.Configure(probe.PollState);
            ControllerAimAssist.Trace = message => Logger.Log(message);
            ControllerAimAssist.StartSession();
            ControllerGameplayActions.Configure(probe.PollState,
                bindings ?? BindingConfig.SafeDefaults(), ControllerAimAssist.Toggle);
            ControllerExploration.Configure(probe.PollState);
            ControllerMenuSelection.Configure(probe.PollState,
                bindings ?? BindingConfig.SafeDefaults(),
                message => Logger.LogWarning(message, false),
                () => probe.UsingController);
            ControllerFrontEndMenus.Configure(probe.PollState,
                bindings ?? BindingConfig.SafeDefaults(),
                message => Logger.LogWarning(message, false));
            ControllerInteractPrompt.Configure((bindings ?? BindingConfig.SafeDefaults())["Interact"],
                message => Logger.LogWarning(message, false));
            probeObject = new GameObject("Controller Support Input Probe");
            probeObject.AddComponent<ProbeRunner>().Attach(probe);
            UnityEngine.Object.DontDestroyOnLoad(probeObject);
            Logger.Log("Controller Support loaded: configurable combat aim-assist and six gameplay hotkeys, exploration, shared inventory and planet-selector actions, Emblem Forge, feedback, safe item returns, and front-end/pause menu navigation.");
        }

        protected override void Unload()
        {
            ControllerInputOwnership.Reset();
            ControllerGameplayInput.Reset();
            ControllerDialogueInput.Reset();
            ControllerGameplayActions.Reset();
            ControllerAimAssist.Reset();
            ControllerAim.Reset();
            ControllerCombatGuide.Reset();
            ControllerExploration.Reset();
            ControllerMenuSelection.Reset();
            ControllerFrontEndMenus.Reset();
            ControllerInteractPrompt.Reset();
            if (probeObject != null) UnityEngine.Object.Destroy(probeObject);
            probeObject = null;
            probe = null;
        }

        public override string GetModDescription()
        {
            return "Direct Linux controller input with combat aim-assist, configurable hotkeys, exploration, inventory, and menu navigation.";
        }
    }
}
