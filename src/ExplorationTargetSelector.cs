using System;
using UnityEngine;

namespace ControllerSupport
{
    // Pure directional choice: strong alignment wins; distance breaks nearly
    // equal angles. An existing target gets a small angular hysteresis margin.
    internal static class ExplorationTargetSelector
    {
        internal const float Range = 18f;
        internal const float MinAlignment = 0.65f;
        internal const float Hysteresis = 0.08f;

        internal static int Choose(Vector2 origin, Vector2 direction, Vector2[] points,
            int current, bool centered, bool[] visible = null)
        {
            if (points == null || points.Length == 0) return -1;
            if (centered) return Valid(origin, points, current, visible) ? current : -1;
            if (direction.sqrMagnitude < 0.0001f) return -1;
            direction.Normalize();
            int best = -1;
            float bestAlignment = MinAlignment;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 offset = points[i] - origin;
                float distance = offset.magnitude;
                if (!Valid(origin, points, i, visible)) continue;
                float alignment = Vector2.Dot(offset / distance, direction);
                if (alignment < MinAlignment) continue;
                if (alignment > bestAlignment + 0.0005f ||
                    (Math.Abs(alignment - bestAlignment) <= 0.0005f && distance < bestDistance))
                {
                    best = i;
                    bestAlignment = alignment;
                    bestDistance = distance;
                }
            }
            if (Valid(origin, points, current, visible))
            {
                Vector2 offset = points[current] - origin;
                float alignment = Vector2.Dot(offset.normalized, direction);
                if (alignment >= MinAlignment &&
                    (best == -1 || bestAlignment - alignment <= Hysteresis))
                    return current;
            }
            return best;
        }

        private static bool Valid(Vector2 origin, Vector2[] points, int index,
            bool[] visible)
        {
            if (index < 0 || index >= points.Length) return false;
            float distanceSquared = (points[index] - origin).sqrMagnitude;
            return distanceSquared >= 0.01f &&
                (visible == null ? distanceSquared <= Range * Range :
                    index < visible.Length && visible[index]);
        }
    }
}
