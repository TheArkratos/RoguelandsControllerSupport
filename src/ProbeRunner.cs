using UnityEngine;

namespace ControllerSupport
{
    // GadgetCore's ScriptUpdate hook is created only when GameScript awakens.
    // This component also runs on the title screen and survives scene changes.
    public sealed class ProbeRunner : MonoBehaviour
    {
        private InputProbe probe;

        internal void Attach(InputProbe inputProbe)
        {
            probe = inputProbe;
        }

        private void Update()
        {
            if (probe == null) return;
            probe.Update();
            ControllerInteractPrompt.Update(probe.UsingController);
        }

        private void OnDestroy()
        {
            if (probe != null) probe.Dispose();
            ControllerInteractPrompt.Reset();
            probe = null;
        }
    }
}
