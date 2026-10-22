using System;
using UnityEngine;

namespace ControllerSupport
{
    // ActionCursor is the visible combat crosshair, but the same component is
    // also used by hoverItem and holdingItem. Only replace the combat cursor.
    internal static class ControllerVisibleCursor
    {
        internal static bool Prefix(ActionCursor cursor)
        {
            if (cursor == null || cursor.gameObject == null ||
                !string.Equals(cursor.gameObject.name, "actionCursor", StringComparison.Ordinal))
                return true;
            Vector3 point;
            if (ControllerExploration.TryWorldPoint(out point))
            {
                cursor.transform.position = new Vector3(point.x, point.y, 0f);
                return false;
            }
            if (!ControllerAim.TryWorldPoint(out point)) return true;
            cursor.transform.position = new Vector3(point.x, point.y, 0f);
            return false;
        }
    }
}
