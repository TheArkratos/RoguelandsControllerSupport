using System;
using UnityEngine;

namespace ControllerSupport
{
    // Native ActionCursor follows Menuu.usingController, which can disagree with
    // the mod's deliberate-device arbiter during mouse takeover. Correct only
    // inventory presentation after the native update; leave combat cursors alone.
    internal static class InventoryCursorPosition
    {
        internal static void Update(ActionCursor cursor, GameScript game,
            Collider selected, bool controllerOwns)
        {
            if (cursor == null || game == null || cursor.gameObject == null) return;
            bool held = cursor.gameObject == game.holdingItemObj;
            bool hover = string.Equals(cursor.gameObject.name, "hoverItem", StringComparison.Ordinal);
            if (!held && !hover) return;
            if (controllerOwns)
            {
                // The held object is hidden in favor of the controller badge.
                // The native hover panel still needs its selected-target anchor.
                if (hover && selected != null)
                {
                    Vector3 point = selected.bounds.center;
                    cursor.transform.position = new Vector3(point.x, point.y, cursor.z);
                }
                return;
            }
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 mouse = camera.ScreenToWorldPoint(Input.mousePosition);
            cursor.transform.position = new Vector3(mouse.x, mouse.y, cursor.z);
        }
    }
}
