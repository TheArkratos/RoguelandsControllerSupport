using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ControllerSupport
{
    internal static class ControllerExploration
    {
        private const float AimDeadzone = 0.22f;
        private static readonly FieldInfo InShipField = typeof(GameScript).GetField("inShip",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo InventoryField = typeof(GameScript).GetField("inventory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static Func<JoystickInputState> stateProvider;
        private static ObjectScript target;
        private static bool ownsCursor;
        private static Vector3 mouseSample;
        private static bool sampledMouse;
        private static float mouseReclaimUntil;
        private static float nextScanAt;
        private static ObjectScript[] cachedObjects = new ObjectScript[0];
        private static readonly HashSet<ObjectScript> dispatchedObjects = new HashSet<ObjectScript>();
        private static int levelOwnerId;

        internal static void Configure(Func<JoystickInputState> provider)
        {
            stateProvider = provider;
            target = null;
            ownsCursor = false;
            sampledMouse = false;
            mouseReclaimUntil = 0f;
            nextScanAt = 0f;
            cachedObjects = new ObjectScript[0];
            dispatchedObjects.Clear();
            levelOwnerId = 0;
        }

        internal static void Reset()
        {
            stateProvider = null;
            target = null;
            ownsCursor = false;
            sampledMouse = false;
            cachedObjects = new ObjectScript[0];
            dispatchedObjects.Clear();
            levelOwnerId = 0;
            nextScanAt = 0f;
        }

        private static bool Eligible(GameScript game)
        {
            if (!ControllerInputOwnership.UsingController || ControllerInputOwnership.GameplayBlocked || game == null || InShipField == null || InventoryField == null ||
                (bool)InShipField.GetValue(game) || !GameScript.inInstance || GameScript.isTown ||
                GameScript.combatMode || GameScript.buildMode || GameScript.dead ||
                GameScript.pausing || GameScript.inventoryOpen || GameScript.talking ||
                !MenuScript.player) return false;
            NetworkView view = MenuScript.player.GetComponent<NetworkView>();
            return view && view.isMine;
        }

        internal static bool SuppressControllerDroidButton(GameScript game)
        {
            return Eligible(game) && ControllerGameplayInput.ExplorationPrimaryHeld();
        }

        internal static void UpdateAndDispatch(GameScript game)
        {
            TrackLevel(game);
            if (!Eligible(game)) { Clear(game); return; }
            JoystickInputState input = stateProvider == null ? null : stateProvider();
            if (input == null || !input.Connected) { Clear(game); return; }

            Vector3 mouse = Input.mousePosition;
            bool mouseActive = sampledMouse && (mouse - mouseSample).sqrMagnitude > 0.25f;
            mouseActive |= Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            mouseSample = mouse;
            sampledMouse = true;
            if (mouseActive)
            {
                mouseReclaimUntil = Time.unscaledTime + 0.35f;
                Clear(game);
                return;
            }

            Vector2 stick = new Vector2(input.AimX, -input.AimY);
            bool centered = stick.sqrMagnitude < AimDeadzone * AimDeadzone;
            if (Time.unscaledTime >= mouseReclaimUntil && (!centered || target))
                RefreshTarget(game, stick, centered);

            if (target && !ControllerGameplayInput.ExplorationContextSwitchPressed() &&
                ControllerGameplayInput.ExplorationPrimaryPressed())
            {
                int slot = -1;
                Item[] inventory = (Item[])InventoryField.GetValue(game);
                for (int i = 0; i < 3; i++)
                    if (!game.activated[i] && inventory[42 + i].id >= 1000 &&
                        game.droid[i]) { slot = i; break; }
                if (slot >= 0)
                {
                    // ActivateDroid is synchronous. Its vanilla controller
                    // branch already accepts an exact world point; restore
                    // shared aim state before the rest of Update executes.
                    ObjectScript dispatched = target;
                    Vector3 point = dispatched.transform.position;
                    bool oldMode = Menuu.usingController;
                    float oldX = ControllerCursor.staticX, oldY = ControllerCursor.staticY;
                    bool activated = false;
                    try
                    {
                        Menuu.usingController = true;
                        ControllerCursor.staticX = point.x;
                        ControllerCursor.staticY = point.y;
                        game.ActivateDroid(slot);
                        activated = game.activated[slot];
                    }
                    finally
                    {
                        Menuu.usingController = oldMode;
                        ControllerCursor.staticX = oldX;
                        ControllerCursor.staticY = oldY;
                    }
                    if (activated)
                    {
                        dispatchedObjects.Add(dispatched);
                        // Reuse the current stick direction immediately. A
                        // centered stick has no direction, so clear selection.
                        target = null;
                        RefreshTarget(game, stick, centered);
                    }
                }
            }
        }

        private static void TrackLevel(GameScript game)
        {
            bool inLevel = game != null && InShipField != null &&
                !(bool)InShipField.GetValue(game) && GameScript.inInstance && !GameScript.isTown;
            int ownerId = inLevel ? game.GetInstanceID() : 0;
            if (ownerId == levelOwnerId) return;
            levelOwnerId = ownerId;
            dispatchedObjects.Clear();
            cachedObjects = new ObjectScript[0];
            nextScanAt = 0f;
        }

        private static void RefreshTarget(GameScript game, Vector2 stick, bool centered)
        {
            if (Time.unscaledTime >= nextScanAt)
            {
                cachedObjects = UnityEngine.Object.FindObjectsOfType<ObjectScript>();
                nextScanAt = Time.unscaledTime + 0.1f;
                // Unity objects may have been destroyed while a level runs;
                // don't keep their managed wrappers alive until level exit.
                dispatchedObjects.RemoveWhere(item => !item);
            }
            ObjectScript[] all = cachedObjects;
            List<ObjectScript> objects = new List<ObjectScript>();
            for (int i = 0; i < all.Length; i++)
            {
                ObjectScript item = all[i];
                if (!item || dispatchedObjects.Contains(item) ||
                    !item.gameObject.activeInHierarchy || item.id < 0 || item.id >= 200)
                    continue;
                objects.Add(item);
            }
            objects.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            List<Vector2> points = new List<Vector2>(objects.Count);
            Camera camera = Camera.main;
            bool[] visible = camera == null ? null : new bool[objects.Count];
            int current = -1;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] == target) current = i;
                Vector3 worldPoint = objects[i].transform.position;
                points.Add(worldPoint);
                if (visible != null)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
                    visible[i] = viewport.z > 0f && viewport.x >= 0f &&
                        viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
                }
            }
            Vector2 origin = MenuScript.player.transform.position;
            int selected = ExplorationTargetSelector.Choose(origin, stick,
                points.ToArray(), current, centered, visible);
            target = selected >= 0 ? objects[selected] : null;
            if (target && game.actionCursor)
            {
                if (!game.actionCursor.activeSelf)
                {
                    game.actionCursor.SetActive(true);
                    ownsCursor = true;
                }
                Vector3 point = target.transform.position;
                game.actionCursor.transform.position = new Vector3(point.x, point.y, 0f);
            }
            else Clear(game);
        }

        internal static bool TryWorldPoint(out Vector3 point)
        {
            point = Vector3.zero;
            if (!ControllerInputOwnership.UsingController) { target = null; return false; }
            if (!target || GameScript.combatMode) return false;
            point = target.transform.position;
            return true;
        }

        private static void Clear(GameScript game)
        {
            target = null;
            if (ownsCursor && game != null && game.actionCursor && !GameScript.combatMode)
                game.actionCursor.SetActive(false);
            ownsCursor = false;
        }
    }
}
