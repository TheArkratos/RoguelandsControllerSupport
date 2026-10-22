using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static readonly Vector2 Origin = P(0, 0), Right = P(1, 0);
    private static readonly Vector2 Min = P(-20, -20), Max = P(20, 20);
    private static Vector2 P(double x, double y) { return new Vector2((float)x, (float)y); }
    private static Vector2[] Box(double left, double bottom, double right, double top)
    {
        // Deliberately unordered, with duplicates as in an XY projection of eight
        // 3D box corners. A perimeter-only implementation would not be sufficient.
        return new[] { P(right, top), P(left, bottom), P(left, top), P(right, bottom),
            P(left, top), P(right, bottom), P(right, top), P(left, bottom) };
    }
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static bool Hit(Vector2[] box)
    { return AimAssistGeometry.Intersects(Origin, Right, box, Min, Max); }
    private static bool Circle(Vector2 center, float radius)
    { return AimAssistGeometry.IntersectsCircle(Origin, Right, center, radius, Min, Max); }
    private static double Distance(Vector2 a, Vector2 b)
    { double x = (double)a.x - b.x, y = (double)a.y - b.y; return Math.Sqrt(x * x + y * y); }
    private static double Angle(Vector2 origin, Vector2 axis, Vector2 point)
    {
        double x = (double)point.x - origin.x, y = (double)point.y - origin.y;
        return Math.Atan2(axis.x * y - axis.y * x, axis.x * x + axis.y * y) * 180 / Math.PI;
    }
    private static void Bounded(Vector2 origin, Vector2 axis, Vector2 target)
    {
        Vector2 aim = AimAssistGeometry.ClampAim(origin, axis, target);
        Check(Math.Abs(Angle(origin, axis, aim)) <= 8.00001, "assisted direction stays within eight degrees");
        Check(Math.Abs(Distance(origin, aim) - Distance(origin, target)) < 0.0001,
            "direction clamp preserves target radial distance");
    }

    public static void Main()
    {
        double radians = 8 * Math.PI / 180;
        double tangent = Math.Tan(radians);
        Check(AimAssistGeometry.HalfAngleDegrees == 8, "configured cone has eight-degree half-angle");
        Vector2 seven = P(10, 10 * Math.Tan(7 * Math.PI / 180));
        Check(Hit(new[] { seven }), "positive seven-degree point qualifies in expanded five-to-eight band");
        Check(Hit(new[] { P(seven.x, -seven.y) }), "negative seven-degree point qualifies in expanded band");
        Vector2 sevenAim = AimAssistGeometry.ClampAim(Origin, Right, seven);
        Check(sevenAim.x == seven.x && sevenAim.y == seven.y,
            "seven-degree center remains unchanged rather than clamping to prior five-degree boundary");
        Check(Hit(Box(8, -.2, 10, .2)), "forward box intersects");
        Check(!Hit(Box(-10, -.2, -8, .2)), "behind box rejected");
        Check(!Hit(Box(8, 2, 10, 3)), "above cone rejected");
        Check(!Hit(Box(8, -3, 10, -2)), "below cone rejected");
        Check(Hit(Box(8, .9, 10, 2.2)), "center outside cone accepted when hitbox edge crosses");
        Check(Hit(new[] { P(10, 10 * tangent) }), "upper ray tangent included");
        Check(Hit(new[] { P(10, -10 * tangent) }), "lower ray tangent included");
        Check(!Hit(new[] { P(10, 10 * Math.Tan(8.0001 * Math.PI / 180)) }), "point just outside cone rejected");
        Check(!Hit(new[] { P(10, -10 * Math.Tan(8.0001 * Math.PI / 180)) }), "point just below negative eight-degree boundary rejected");
        Check(Hit(new[] { Origin }), "cone origin tangent included");
        Check(Hit(Box(-2, -1, 0, 1)), "otherwise behind hitbox touching origin included");
        Check(Hit(new[] { P(5, -.8), P(5, .8) }), "degenerate line crossing cone included");
        Check(!Hit(new[] { P(5, 1), P(6, 2), P(5, 1) }), "degenerate line outside cone rejected");
        Check(Hit(new[] { P(5, 0), P(5, 0), P(5, 0) }), "duplicate point footprint included");
        Check(!Hit(Box(25, -.2, 30, .2)), "offscreen forward hitbox rejected");
        Check(Hit(Box(20, -.2, 25, .2)), "viewport-edge tangent included");
        Check(!AimAssistGeometry.Intersects(Origin, Right,
            new[] { P(4, 2), P(10, .5), P(10, 2) }, P(-5, -5), P(5, 5)),
            "cone overlap outside visible part does not satisfy clipped intersection");
        Check(!Hit(new[] { P(3, 2), P(5, .9), P(7, 2), P(5, 3.4) }),
            "actual rotated box rejected despite AABB crossing cone");
        Check(Hit(new[] { P(7, 2), P(5, 3.4), P(5, .9), P(3, 2), P(8, 0) }),
            "unordered hull really reaching cone included");
        Check(AimAssistGeometry.Intersects(Origin, P(0, 4), Box(-.3, 8, .3, 10), Min, Max), "vertical scaled axis supported");
        Check(AimAssistGeometry.Intersects(Origin, P(-1, .001), Box(-10, -.1, -8, .1), Min, Max), "left axis across angle wrap supported");
        Check(AimAssistGeometry.Intersects(P(-10, -10), Right, Box(-5, -10.2, -4, -9.8), Min, Max), "negative world coordinates supported");
        Check(AimAssistGeometry.Intersects(P(30, 0), P(-1, 0), Box(10, -.2, 12, .2), Min, Max), "origin outside viewport can aim into visible viewport");
        Check(!AimAssistGeometry.Intersects(Origin, Right, Box(1, 1, 2, 2), P(3, 3), P(2, 2)), "inverted viewport rejected");
        Check(!AimAssistGeometry.Intersects(Origin, Right, null, Min, Max), "null footprint rejected");
        Check(!Hit(new Vector2[0]), "empty footprint rejected");
        Check(!AimAssistGeometry.Intersects(Origin, P(0, 0), Box(1, -1, 2, 1), Min, Max), "zero axis rejected");
        Check(!AimAssistGeometry.Intersects(P(double.NaN, 0), Right, Box(1, -1, 2, 1), Min, Max), "nonfinite origin rejected");
        Check(!AimAssistGeometry.Intersects(Origin, P(double.PositiveInfinity, 0), Box(1, -1, 2, 1), Min, Max), "nonfinite axis rejected");
        Check(!Hit(new[] { P(1, 0), P(double.NaN, 1), P(2, 0) }), "one invalid corner rejects entire footprint");
        Check(!AimAssistGeometry.Intersects(Origin, Right, Box(1, -1, 2, 1), Min, P(double.PositiveInfinity, 20)), "nonfinite viewport rejected");
        Check(AimAssistGeometry.Intersects(Origin, Right, new[] { P(5, 0) }, P(5, 0), P(5, 0)), "zero-area viewport tangent supported");
        Vector2 unchanged = AimAssistGeometry.ClampAim(Origin, Right, P(10, .1));
        Check(unchanged.x == 10 && unchanged.y == (float).1, "center within cone is unchanged");
        Bounded(Origin, Right, P(10, 4));
        Bounded(Origin, Right, P(10, -4));
        Bounded(Origin, Right, P(-10, 0));
        Bounded(P(-10, -10), P(0, 1), P(-8, -4));
        Bounded(Origin, P(-1, .001), P(-10, -.1));
        Vector2 clamped = AimAssistGeometry.ClampAim(Origin, Right, P(10, 4));
        Check(Math.Abs(Angle(Origin, Right, clamped) - 8) < .00001,
            "outside center clamps exactly to expanded positive eight-degree boundary");
        clamped = AimAssistGeometry.ClampAim(Origin, Right, P(10, -4));
        Check(Math.Abs(Angle(Origin, Right, clamped) + 8) < .00001,
            "outside center clamps exactly to expanded negative eight-degree boundary");
        Vector2 invalid = AimAssistGeometry.ClampAim(P(3, 4), P(0, 0), P(10, 0));
        Check(invalid.x == 3 && invalid.y == 4, "invalid axis returns finite origin");
        invalid = AimAssistGeometry.ClampAim(P(3, 4), Right, P(double.NaN, 0));
        Check(invalid.x == 3 && invalid.y == 4, "invalid target returns finite origin");
        invalid = AimAssistGeometry.ClampAim(P(double.NaN, 0), Right, P(10, 0));
        Check(invalid.x == 0 && invalid.y == 0, "invalid origin returns zero");
        unchanged = AimAssistGeometry.ClampAim(P(3, 4), Right, P(3, 4));
        Check(unchanged.x == 3 && unchanged.y == 4, "coincident target remains origin");
        Check(AimAssistGeometry.Intersects(Origin, P(float.MaxValue, 0), Box(8, -.2, 10, .2), Min, Max),
            "large finite axis normalizes without float overflow");
        Check(Circle(P(5, 0), .5f), "circle centered in cone included");
        Check(Circle(P(10, 1.8), .5f), "off-cone center with intersecting circle edge included");
        Check(!Circle(P(5, 1.8), 1), "circle rejected despite its AABB intersecting cone");
        Check(Circle(P(10, 10 * tangent + 1 / Math.Cos(radians)), 1), "circle upper-ray tangent included");
        Check(Circle(P(10, -10 * tangent - 1 / Math.Cos(radians)), 1), "circle lower-ray tangent included");
        Check(!Circle(P(10, 10 * tangent + 1 / Math.Cos(radians) + .0001), 1), "circle just outside upper-ray tangent rejected");
        Check(Circle(P(-1, 0), 1), "circle behind player touching cone origin included");
        Check(!Circle(P(-1, 0), .99f), "circle wholly behind origin rejected");
        Check(!Circle(P(25, 0), 1), "offscreen circle rejected");
        Check(Circle(P(21, 0), 1), "circle tangent to viewport included");
        Check(!Circle(P(21.001, 0), 1), "circle outside viewport tangent rejected");
        Check(!AimAssistGeometry.IntersectsCircle(Origin, Right, P(5, 2), 1.6f, P(-5, -5), P(4, 5)),
            "circle viewport overlap and cone overlap in separate regions rejected");
        Check(Circle(P(5, 0), 0), "zero-radius circle point inside cone included");
        Check(!Circle(P(5, 1), 0), "zero-radius circle point outside cone rejected");
        Check(AimAssistGeometry.IntersectsCircle(Origin, Right, P(5, 1), 1, P(5, 0), P(5, 0)),
            "circle tangent to degenerate viewport point included");
        Check(AimAssistGeometry.IntersectsCircle(Origin, P(0, 1), P(.7, 10), .1f, Min, Max),
            "vertical circle intersection supported");
        Check(AimAssistGeometry.IntersectsCircle(P(-10, -10), P(-1, .001), P(-15, -10), .1f, Min, Max),
            "negative world and left-wrap circle supported");
        Check(!Circle(P(5, 0), -1), "negative radius rejected");
        Check(!Circle(P(5, 0), float.NaN), "nonfinite radius rejected");
        Check(!Circle(P(double.PositiveInfinity, 0), 1), "nonfinite circle center rejected");
        Check(!AimAssistGeometry.IntersectsCircle(Origin, P(0, 0), P(5, 0), 1, Min, Max), "circle zero axis rejected");
        Check(!AimAssistGeometry.IntersectsCircle(Origin, Right, P(5, 0), 1, P(5, 5), P(-5, -5)),
            "circle inverted viewport rejected");
        Check(AimAssistGeometry.IntersectsCircle(Origin, P(float.MaxValue, 0), P(5, 0), float.MaxValue, Min, Max),
            "large finite circle radius and axis avoid arithmetic overflow");
        Check(AimAssistGeometry.CenterlineDistance(Origin,Right,Box(8,-.2,10,2))==0,"Hitbox crossing forward centerline has zero gap despite off-line center");
        Check(Math.Abs(AimAssistGeometry.CenterlineDistance(Origin,Right,Box(8,1,10,2))-1)<.00001,"Box gap uses actual nearest edge rather than center");
        Check(Math.Abs(AimAssistGeometry.CenterlineDistance(Origin,Right,new[]{P(-3,2),P(-1,.2),P(1,2),P(-1,3.8)})-2.2/Math.Sqrt(7.24))<.00001,"Rotated box crossing ray origin uses real footprint instead of closer empty AABB corner");
        Check(Math.Abs(AimAssistGeometry.CenterlineDistance(Origin,Right,Box(-4,-1,-2,1))-2)<.00001,"Box behind origin does not overlap forward ray despite crossing infinite line");
        Check(Math.Abs(AimAssistGeometry.CenterlineDistance(Origin,P(0,5),Box(1,4,3,6))-1)<.00001,"Scaled vertical axis measures perpendicular world distance");
        Check(AimAssistGeometry.CenterlineDistance(Origin,Right,Box(-1,-1,1,1))==0,"Footprint containing ray origin has zero gap");
        Check(AimAssistGeometry.CenterlineDistance(Origin,Right,new[]{P(5,2)})==2,"Point hitbox has exact ray distance");
        Check(Math.Abs(AimAssistGeometry.CenterlineDistanceCircle(Origin,Right,P(10,1.8),.5f)-1.3)<.00001,"Sphere gap subtracts actual projected radius");
        Check(AimAssistGeometry.CenterlineDistanceCircle(Origin,Right,P(10,.5),1)==0,"Circle touching centerline has zero gap despite off-line center");
        Check(AimAssistGeometry.CenterlineDistanceCircle(Origin,Right,P(-3,0),1)==2,"Circle behind origin measures distance to ray endpoint");
        Check(float.IsPositiveInfinity(AimAssistGeometry.CenterlineDistance(Origin,P(0,0),Box(1,1,2,2))),"Invalid scoring axis fails closed");
        Check(float.IsPositiveInfinity(AimAssistGeometry.CenterlineDistance(Origin,Right,new[]{P(double.NaN,0)})),"Invalid scoring corner fails closed");
        Check(float.IsPositiveInfinity(AimAssistGeometry.CenterlineDistanceCircle(Origin,Right,P(3,1),-1)),"Invalid scoring radius fails closed");
        Console.WriteLine("Aim assist geometry: " + checks + " checks passed.");
    }
}
