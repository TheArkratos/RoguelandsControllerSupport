using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static Vector2 P(float x, float y) { return new Vector2(x, y); }
    private static void Point(Vector2 actual, float x, float y, string message)
    {
        checks++;
        if (float.IsNaN(actual.x) || float.IsNaN(actual.y) ||
            float.IsInfinity(actual.x) || float.IsInfinity(actual.y) ||
            Math.Abs(actual.x - x) > .0001 || Math.Abs(actual.y - y) > .0001)
            throw new Exception(message + ": (" + actual.x + ", " + actual.y + ")");
    }
    private static Vector2 Lead(Vector2 origin, Vector2 center, Vector2 velocity, float speed)
    { return AimLeadPrediction.Predict(origin, center, velocity, speed); }
    private static void Main()
    {
        Vector2 zero = P(0, 0), center = P(10, 0), velocity = P(0, 10);
        Point(Lead(zero, center, velocity, 100), 10, .5f, "half of travel-time displacement");
        Point(Lead(zero, center, velocity, 200), 10, .25f, "faster shot needs less lead");
        Point(Lead(zero, P(20, 0), velocity, 100), 20, 1, "farther target needs more lead");
        Point(Lead(P(5, 0), center, velocity, 100), 10, .25f, "distance measured from projectile");
        Point(Lead(zero, P(6, 8), P(-10, 10), 100), 5.5f, 8.5f, "Euclidean distance and signed motion");
        Point(Lead(zero, center, velocity, 1), 10, 2, "long travel time capped at point two seconds");
        Point(Lead(zero, center, zero, 100), 10, 0, "stationary target stays centered");
        Point(Lead(center, center, velocity, 100), 10, 0, "zero travel distance adds no lead");
        foreach (float speed in new[] { 0f, -10f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Point(Lead(zero, center, velocity, speed), 10, 0, "missing or invalid speed falls back");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Point(Lead(zero, center, P(invalid, 1), 100), 10, 0, "invalid velocity x falls back");
            Point(Lead(zero, center, P(1, invalid), 100), 10, 0, "invalid velocity y falls back");
            Point(Lead(P(invalid, 0), center, velocity, 100), 10, 0, "invalid origin keeps valid center");
            Point(Lead(P(4, 5), P(invalid, 0), velocity, 100), 4, 5, "invalid target keeps valid origin");
            Point(Lead(P(invalid, 0), P(0, invalid), velocity, 100), 0, 0, "invalid target and origin yield finite zero");
        }
        Point(Lead(zero, P(float.MaxValue, 0), P(float.MaxValue, 0), 1), float.MaxValue, 0,
            "overflow prediction keeps valid center");
        Point(Lead(P(-float.MaxValue, 0), P(float.MaxValue, 0), P(0, 10), 1), float.MaxValue, 2,
            "large finite distance avoids intermediate float overflow");

        // This helper predicts freely. Runtime callers enforce the player's cone.
        Point(Lead(zero, P(1, 0), P(0, 100), 1), 1, 20, "prediction does not clamp to cone itself");
        Point(AimLeadPrediction.StaffFallback(P(20, 3), P(1, 0)), 50, 3,
            "staff fallback remains ahead after passing visible cursor at x ten");
        Point(AimLeadPrediction.StaffFallback(P(20, 3), P(100, 0)), 50, 3, "staff direction normalized");
        Point(AimLeadPrediction.StaffFallback(P(20, 3), P(0, -5)), 20, -27, "rotated fallback");
        Point(AimLeadPrediction.StaffFallback(zero, P(3, 4)), 18, 24, "diagonal fallback");
        Point(AimLeadPrediction.StaffFallback(P(20, 3), zero), 20, 3, "zero direction stays finite");
        Point(AimLeadPrediction.StaffFallback(P(20, 3), P(float.NaN, 1)), 20, 3, "invalid direction stays finite");
        Point(AimLeadPrediction.StaffFallback(P(float.NaN, 3), P(1, 0)), 30, 0, "invalid staff position uses finite zero");
        Point(AimLeadPrediction.StaffFallback(zero, P(float.MaxValue, float.MaxValue)),
            (float)(30 / Math.Sqrt(2)), (float)(30 / Math.Sqrt(2)), "large axis normalizes without overflow");

        AimLeadPrediction.LeadStrength = .25f;
        Point(Lead(zero, center, velocity, 100), 10, .25f, "strength remains easy to tune");
        AimLeadPrediction.LeadStrength = .5f;
        AimLeadPrediction.MaxLeadTime = .1f;
        Point(Lead(zero, center, velocity, 1), 10, 1, "time cap remains easy to tune");
        AimLeadPrediction.MaxLeadTime = .2f;
        AimLeadPrediction.StaffLookAheadDistance = 10;
        Point(AimLeadPrediction.StaffFallback(P(20, 3), P(1, 0)), 30, 3, "lookahead remains easy to tune");
        AimLeadPrediction.StaffLookAheadDistance = 30;
        Console.WriteLine("PASS: " + checks + " aim lead prediction checks");
    }
}
