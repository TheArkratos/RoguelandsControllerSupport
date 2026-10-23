using System;
using UnityEngine;

namespace ControllerSupport
{
    // Anchor controller inventory tooltips after native cursor movement.
    // Mouse ownership leaves native cursor positioning entirely intact.
    internal static class InventoryCursorPosition
    {
        internal static void Update(ActionCursor cursor, GameScript game,
            Collider selected, bool controllerOwns)
        {
            if (!ControllerInputOwnership.UsingController || !controllerOwns) return;
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

        }
    }
}
