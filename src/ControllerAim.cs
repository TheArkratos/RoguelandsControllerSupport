using System;
using UnityEngine;

namespace ControllerSupport
{
    // One world aim point for the visible cursor and the game's attack paths.
    // The radius is provisional until measured against the base Aethercannon.
    internal static class ControllerAim
    {
        internal const float ProvisionalRadius = 14f;
        private const float AimDeadzone = 0.22f;
        private const float MaxDegreesPerSecond = 720f;
        private const float MaxDegreesPerFrame = 30f;
        private static Func<JoystickInputState> stateProvider;
        internal static Func<Vector3,Vector3> AttackResolver;
        internal static Action AttackOwnershipLost;
        private static float radius = ProvisionalRadius;
        private static Vector2 direction = Vector2.right;
        private static float renderedAngle;
        private static bool hasRenderedAngle;
        private static Vector3 previousMouse;
        private static bool hasMouseSample;
        private static bool ownsAim;
        private static bool hasExplicitAimOwner;
        private static int lastFrame = -1;
        private static float mouseReclaimUntil;

        internal static void Configure(Func<JoystickInputState> provider, float aimRadius = ProvisionalRadius)
        {
            stateProvider = provider;
            radius = aimRadius > 0f && !float.IsNaN(aimRadius) && !float.IsInfinity(aimRadius)
                ? aimRadius : ProvisionalRadius;
            direction = Vector2.right;
            renderedAngle = 0f;
            hasRenderedAngle = false;
            hasMouseSample = false;
            ownsAim = false;
            hasExplicitAimOwner = false;
            lastFrame = -1;
            mouseReclaimUntil = 0f;
        }

        internal static void Reset()
        {
            stateProvider = null;
            ownsAim = false;
            hasExplicitAimOwner = false;
            hasRenderedAngle = false;
            hasMouseSample = false;
            lastFrame = -1;
            mouseReclaimUntil = 0f;
        }

        private static bool TryPoint(out Vector3 point)
        {
            point = Vector3.zero;
            // Check before the per-frame cache: keyboard/mouse can reclaim input
            // after a controller query earlier in this same frame.
            if (!ControllerInputOwnership.UsingController)
            {
                if (hasExplicitAimOwner && MenuScript.player)
                {
                    NetworkView previousOwner = MenuScript.player.GetComponent<NetworkView>();
                    if (previousOwner && previousOwner.isMine) Menuu.usingController = false;
                }
                ownsAim = false;
                hasExplicitAimOwner = false;
                hasRenderedAngle = false;
                hasMouseSample = false;
                lastFrame = -1;
                mouseReclaimUntil = 0f;
                if (AttackOwnershipLost != null) AttackOwnershipLost();
                return false;
            }
            if (lastFrame != Time.frameCount)
            {
                lastFrame = Time.frameCount;
                Vector3 mouse = Input.mousePosition;
                bool mouseActive = hasMouseSample &&
                    (mouse - previousMouse).sqrMagnitude > 0.25f;
                mouseActive |= Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                previousMouse = mouse;
                hasMouseSample = true;

                JoystickInputState state = stateProvider == null ? null : stateProvider();
                bool combatAvailable = GameScript.combatMode && !GameScript.dead &&
                    !GameScript.pausing && !GameScript.buildMode &&
                    !GameScript.inventoryOpen && !GameScript.talking;
                if (mouseActive)
                {
                    ownsAim = false;
                    hasExplicitAimOwner = true;
                    hasRenderedAngle = false;
                    mouseReclaimUntil = Time.unscaledTime + 0.35f;
                }
                else if (state == null || !state.Connected || !combatAvailable)
                {
                    ownsAim = false;
                    hasRenderedAngle = false;
                }
                else if (Time.unscaledTime >= mouseReclaimUntil)
                {
                    Vector2 stick = new Vector2(state.AimX, -state.AimY);
                    if (stick.sqrMagnitude >= AimDeadzone * AimDeadzone)
                    {
                        direction = stick.normalized;
                        if (!hasRenderedAngle)
                        {
                            renderedAngle = (float)Math.Atan2(direction.y, direction.x);
                            hasRenderedAngle = true;
                        }
                        ownsAim = true;
                        hasExplicitAimOwner = true;
                    }
                }

                if (ownsAim && hasRenderedAngle)
                {
                    float target = (float)Math.Atan2(direction.y, direction.x);
                    float delta = WrapRadians(target - renderedAngle);
                    float radiansPerFrame = Math.Min(MaxDegreesPerFrame,
                        MaxDegreesPerSecond * Math.Max(0f, Time.unscaledDeltaTime)) *
                        (float)Math.PI / 180f;
                    renderedAngle += Math.Sign(delta) * Math.Min(Math.Abs(delta), radiansPerFrame);
                }
            }

            // Vanilla movement can leave this flag true after the mouse takes
            // aim back. Its combat cursor and several attack paths read the
            // flag directly, so keep it aligned with the most recent explicit
            // aim device while gameplay is active.
            if (hasExplicitAimOwner && GameScript.combatMode && !GameScript.dead &&
                !GameScript.pausing && !GameScript.buildMode &&
                !GameScript.inventoryOpen && !GameScript.talking && MenuScript.player)
            {
                NetworkView ownerView = MenuScript.player.GetComponent<NetworkView>();
                if (ownerView && ownerView.isMine)
                    Menuu.usingController = ownsAim;
            }

            if (!ownsAim || !MenuScript.player) return false;
            NetworkView view = MenuScript.player.GetComponent<NetworkView>();
            if (!view || !view.isMine) return false;
            float distance = radius;
            Vector3 player = MenuScript.player.transform.position;
            point = new Vector3(player.x + (float)Math.Cos(renderedAngle) * distance,
                player.y + (float)Math.Sin(renderedAngle) * distance, 0f);
            return true;
        }

        private static float WrapRadians(float radians)
        {
            float full = (float)(2d * Math.PI);
            radians = (radians + (float)Math.PI) % full;
            if (radians < 0f) radians += full;
            return radians - (float)Math.PI;
        }

        internal static bool TryWorldPoint(out Vector3 point)
        {
            return TryPoint(out point);
        }

        // Replaces only Input.mousePosition reads in the verified Attack iterator.
        // The game's own Camera.ScreenToWorldPoint then receives this screen point.
        internal static Vector3 AttackMousePosition()
        {
            Vector3 point;
            Camera camera = Camera.main;
            if(camera != null && TryPoint(out point))
                return camera.WorldToScreenPoint(AttackResolver != null ? AttackResolver(point) : point);
            if(AttackOwnershipLost != null) AttackOwnershipLost();
            return Input.mousePosition;
        }
    }
}
