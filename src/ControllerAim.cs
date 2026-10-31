using System;
using System.Reflection;
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
        private static readonly FieldInfo AttackingField = typeof(PlayerScript).GetField("attacking",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
        private static bool lastSwordSwingActive;
        private static bool lastCombatAvailable;
        private static GameObject aimPlayer;

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
            lastSwordSwingActive = false;
            lastCombatAvailable = false;
            aimPlayer = null;
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
            lastSwordSwingActive = false;
            lastCombatAvailable = false;
            aimPlayer = null;
        }

        private static void ReleaseOwnership()
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
        }

        private static bool TryPoint(out Vector3 point)
        {
            point = Vector3.zero;
            // Check before the per-frame cache: keyboard/mouse can reclaim input
            // after a controller query earlier in this same frame.
            if (!ControllerInputOwnership.UsingController || ControllerInputOwnership.GameplayBlocked)
            {
                ReleaseOwnership();
                return false;
            }
            if (aimPlayer != MenuScript.player)
            {
                // A replaced character cannot inherit another body's aim angle
                // or an off-window mouse sample from its previous session.
                aimPlayer = MenuScript.player;
                ownsAim = hasExplicitAimOwner = hasRenderedAngle = hasMouseSample = false;
                lastFrame = -1;
                mouseReclaimUntil = 0f;
                lastCombatAvailable = false;
            }
            NetworkView localView = MenuScript.player ? MenuScript.player.GetComponent<NetworkView>() : null;
            bool combatAvailable = GameScript.combatMode && !GameScript.dead &&
                !GameScript.pausing && !GameScript.buildMode && !GameScript.inventoryOpen &&
                localView && localView.isMine;
            bool swordSwingActive = SwordSwingActive();
            // Native coroutine state can change after an earlier aim query in
            // this frame. Ending the swing must apply held walking immediately,
            // including the recovery window before the next automatic attack.
            if (lastFrame != Time.frameCount || lastSwordSwingActive != swordSwingActive ||
                lastCombatAvailable != combatAvailable)
            {
                lastFrame = Time.frameCount;
                lastSwordSwingActive = swordSwingActive;
                lastCombatAvailable = combatAvailable;
                Vector3 mouse = Input.mousePosition;
                bool mouseActive = ownsAim && hasMouseSample &&
                    (mouse - previousMouse).sqrMagnitude > 0.25f;
                mouseActive |= Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                previousMouse = mouse;
                hasMouseSample = true;

                JoystickInputState state = stateProvider == null ? null : stateProvider();
                // Polling can update InputProbe's last-input decision. Do not
                // seed or claim native controller aim after that same call has
                // handed ownership back to physical mouse/keyboard input.
                if (!ControllerInputOwnership.UsingController || ControllerInputOwnership.GameplayBlocked)
                {
                    ReleaseOwnership();
                    return false;
                }
                // Dialogue text does not end an active combat aiming session.
                // Retain the last direction while idle; walking supplies a
                // horizontal aim fallback only while the right stick is neutral.
                if (state == null || !state.Connected || !combatAvailable)
                {
                    ownsAim = false;
                    hasRenderedAngle = false;
                    hasMouseSample = false;
                    mouseReclaimUntil = 0f;
                }
                else if (mouseActive)
                {
                    ownsAim = false;
                    hasExplicitAimOwner = true;
                    hasRenderedAngle = false;
                    mouseReclaimUntil = Time.unscaledTime + 0.35f;
                }
                else if (Time.unscaledTime >= mouseReclaimUntil)
                {
                    Vector2 intent;
                    bool walking, movementLocked;
                    if (TryIntentDirection(state, out intent, out walking, out movementLocked))
                    {
                        direction = intent;
                        if (walking || !hasRenderedAngle)
                        {
                            // A walk-direction change must put the crosshair on
                            // that side immediately, not sweep through the old
                            // hemisphere while the character already faces away.
                            // Explicit right-stick aim retains its usual smoothing.
                            renderedAngle = (float)Math.Atan2(direction.y, direction.x);
                            hasRenderedAngle = true;
                        }
                        ownsAim = true;
                        hasExplicitAimOwner = true;
                    }
                    else if (!ownsAim || !hasRenderedAngle)
                    {
                        // Fresh controller ownership with neutral sticks still
                        // needs an aim point. Seed from this character's native
                        // facing, never the physical mouse or stale cursor field.
                        direction = FacingSeed();
                        renderedAngle = (float)Math.Atan2(direction.y, direction.x);
                        hasRenderedAngle = ownsAim = hasExplicitAimOwner = true;
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
                !GameScript.inventoryOpen && MenuScript.player)
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

        // Read current intent before the native cursor refresh. A sword swing
        // holds the white aim position rather than falling through to stale
        // cursor fields or the physical pointer while walking is deferred.
        internal static bool TryWalkingFacingX(out float x)
        {
            x = 0f;
            if (!ControllerInputOwnership.UsingController || ControllerInputOwnership.GameplayBlocked ||
                !GameScript.combatMode || GameScript.dead || GameScript.pausing ||
                GameScript.buildMode || GameScript.inventoryOpen || !MenuScript.player) return false;
            NetworkView view = MenuScript.player.GetComponent<NetworkView>();
            if (!view || !view.isMine) return false;
            JoystickInputState state = stateProvider == null ? null : stateProvider();
            if (state == null || !state.Connected) return false;
            Vector2 intent;
            bool walking, movementLocked;
            bool hasIntent = TryIntentDirection(state, out intent, out walking, out movementLocked);
            if (hasIntent && walking)
            {
                x = MenuScript.player.transform.position.x + intent.x;
                return true;
            }
            Vector3 held;
            // This also seeds first-use neutral controller aim before native
            // facing can turn the sprite toward stale mouse/cursor coordinates.
            if (TryWorldPoint(out held))
            {
                x = held.x;
                return true;
            }
            return false;
        }

        // One arbitration policy for the visible white aim point and native
        // facing reads. Explicit right-stick aim always outranks walking.
        private static bool TryIntentDirection(JoystickInputState state, out Vector2 intent, out bool walking,
            out bool movementLocked)
        {
            walking = movementLocked = false;
            intent = new Vector2(state.AimX, -state.AimY);
            if (intent.sqrMagnitude >= AimDeadzone * AimDeadzone)
            {
                intent = intent.normalized;
                return true;
            }
            // Only movement-derived aim waits for the native sword swing.
            // Explicit right-stick input above has already taken priority.
            movementLocked = SwordSwingActive();
            if (movementLocked || Math.Abs(state.MoveX) <= 0.5f) return false;
            intent = new Vector2(state.MoveX < 0f ? -1f : 1f, 0f);
            walking = true;
            return true;
        }

        private static Vector2 FacingSeed()
        {
            PlayerScript player = MenuScript.player.GetComponent<PlayerScript>();
            if (player == null || !player.e) return Vector2.right;
            float yaw = player.e.transform.eulerAngles.y;
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) return Vector2.right;
            // Native PlayerScript.Update writes yaw 0 for left, 180 for right.
            return new Vector2(Math.Cos(yaw * Math.PI / 180d) < 0d ? 1f : -1f, 0f);
        }

        private static bool SwordSwingActive()
        {
            int[] gear = GameScript.equippedIDs;
            if (!MenuScript.player || AttackingField == null || gear == null || gear.Length == 0 ||
                gear[0] < 300 || gear[0] >= 350) return false;
            NetworkView view = MenuScript.player.GetComponent<NetworkView>();
            if (!view || !view.isMine) return false;
            PlayerScript player = MenuScript.player.GetComponent<PlayerScript>();
            return player != null && (bool)AttackingField.GetValue(player);
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
