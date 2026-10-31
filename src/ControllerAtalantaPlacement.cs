using System;
using UnityEngine;

namespace ControllerSupport
{
    // Atalanta is a nearby placement, not a weapon-target resolver. Native
    // spawning, chip cost, networking and the Eye's effect remain authoritative.
    internal static class ControllerAtalantaPlacement
    {
        internal const float BodyWidthMultiplier = 2f;

        internal static Vector3 MousePosition()
        {
            Vector3 mouse = Input.mousePosition;
            Camera camera = Camera.main;
            // Check context before reading the aim cache: a menu or pause can
            // open after another caller sampled controller aim this same frame.
            if (!ControllerInputOwnership.UsingController || !GameScript.combatMode ||
                GameScript.dead || GameScript.pausing || GameScript.inventoryOpen ||
                GameScript.buildMode || !camera || !MenuScript.player) return mouse;
            Vector3 whiteAim;
            if (!ControllerAim.TryWorldPoint(out whiteAim) || !Finite(whiteAim)) return mouse;
            Vector3 placement = whiteAim;
            Collider body = MenuScript.player.GetComponent<Collider>();
            if (body && body.enabled)
            {
                float width = body.bounds.size.x;
                Vector3 center = ControllerAimAssist.PlayerCenter();
                if (Finite(center) && Finite(width) && width > 0f)
                {
                    double dx = (double)whiteAim.x - center.x, dy = (double)whiteAim.y - center.y;
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    if (length > 0d)
                    {
                        double distance = (double)width * BodyWidthMultiplier;
                        Vector3 nearby = new Vector3((float)(center.x + dx / length * distance),
                            (float)(center.y + dy / length * distance), 0f);
                        if (Finite(nearby)) placement = nearby;
                    }
                }
            }
            // Missing/invalid body geometry retains the ordinary WHITE cursor
            // point. Never fall back through AttackMousePosition, which may
            // redirect toward an assisted/predicted enemy target.
            Vector3 screen = camera.WorldToScreenPoint(placement);
            return Finite(screen) && screen.z > 0f ? screen : mouse;
        }

        private static bool Finite(Vector3 value)
        { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
        private static bool Finite(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
