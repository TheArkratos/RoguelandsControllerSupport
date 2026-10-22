using System;
using UnityEngine;

namespace ControllerSupport
{
    // Deliberately partial prediction: native projectile physics still owns motion.
    // The caller must apply the player's cone limit after prediction.
    internal static class AimLeadPrediction
    {
        internal static float LeadStrength = .5f;
        internal static float MaxLeadTime = .2f;
        internal static float StaffLookAheadDistance = 30f;

        internal static Vector2 Predict(Vector2 projectileOrigin, Vector2 targetCenter,
            Vector2 targetVelocity, float projectileSpeed)
        {
            if (!Finite(targetCenter)) return Safe(projectileOrigin);
            if (!Finite(projectileOrigin) || !Finite(targetVelocity) ||
                !Finite(projectileSpeed) || projectileSpeed <= 0 ||
                !Finite(LeadStrength) || LeadStrength <= 0 ||
                !Finite(MaxLeadTime) || MaxLeadTime <= 0) return targetCenter;

            double x = (double)targetCenter.x - projectileOrigin.x;
            double y = (double)targetCenter.y - projectileOrigin.y;
            double time = Math.Min(Math.Sqrt(x * x + y * y) / projectileSpeed * LeadStrength,
                MaxLeadTime);
            Vector2 predicted = new Vector2((float)(targetCenter.x + targetVelocity.x * time),
                (float)(targetCenter.y + targetVelocity.y * time));
            return Finite(predicted) ? predicted : targetCenter;
        }

        internal static Vector2 StaffFallback(Vector2 projectilePosition, Vector2 aimDirection)
        {
            Vector2 origin = Safe(projectilePosition);
            if (!Finite(aimDirection) || !Finite(StaffLookAheadDistance) ||
                StaffLookAheadDistance <= 0) return origin;
            double x = aimDirection.x, y = aimDirection.y;
            double length = Math.Sqrt(x * x + y * y);
            if (length == 0) return origin;
            Vector2 ahead = new Vector2((float)(origin.x + x / length * StaffLookAheadDistance),
                (float)(origin.y + y / length * StaffLookAheadDistance));
            return Finite(ahead) ? ahead : origin;
        }

        private static bool Finite(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Finite(Vector2 value) { return Finite(value.x) && Finite(value.y); }
        private static Vector2 Safe(Vector2 value) { return Finite(value) ? value : new Vector2(0, 0); }
    }
}
