using System;
using System.Collections.Generic;
using UnityEngine;

namespace ControllerSupport
{
    // Geometry only: callers supply the actual projected collider vertices,
    // viewport, target center and stick axis. Target discovery/ranking live outside.
    internal static class AimAssistGeometry
    {
        internal const float HalfAngleDegrees = 8f;
        private const double HalfAngle = HalfAngleDegrees * Math.PI / 180.0;
        // Allow only float projection rounding at a boundary, not extra aim range.
        private const double BoundaryTolerance = 1e-7;

        private struct Point
        {
            internal double X, Y;
            internal Point(double x, double y) { X = x; Y = y; }
        }

        // Shortest distance between the actual convex footprint and the forward
        // centerline ray. The negative continuation of the line is not a target.
        internal static float CenterlineDistance(Vector2 origin,Vector2 axis,Vector2[] hitbox)
        {
            double ax,ay;
            if(!Finite(origin) || !Normalize(axis,out ax,out ay) || hitbox==null || hitbox.Length==0) return float.PositiveInfinity;
            var points=new List<Point>(hitbox.Length);
            foreach(Vector2 vertex in hitbox)
            {
                if(!Finite(vertex)) return float.PositiveInfinity;
                double dx=(double)vertex.x-origin.x,dy=(double)vertex.y-origin.y;
                points.Add(new Point(dx*ax+dy*ay,ax*dy-ay*dx));
            }
            List<Point> hull=Hull(points),forward=Clip(hull,1,0,0,0);
            double low=double.PositiveInfinity,high=double.NegativeInfinity;
            foreach(Point point in forward) { low=Math.Min(low,point.Y); high=Math.Max(high,point.Y); }
            if(low<=0 && high>=0) return 0;
            double nearest=double.PositiveInfinity;
            Point previous=hull[hull.Count-1],zero=new Point(0,0);
            foreach(Point point in hull)
            {
                double vertexDistance=point.X>=0 ? point.Y*point.Y : point.X*point.X+point.Y*point.Y;
                nearest=Math.Min(nearest,Math.Min(vertexDistance,SegmentDistanceSquared(zero,previous,point)));
                previous=point;
            }
            return (float)Math.Sqrt(nearest);
        }

        internal static float CenterlineDistanceCircle(Vector2 origin,Vector2 axis,Vector2 center,float radius)
        {
            double ax,ay;
            if(!Finite(origin) || !Finite(center) || !Finite(radius) || radius<0 || !Normalize(axis,out ax,out ay)) return float.PositiveInfinity;
            double dx=(double)center.x-origin.x,dy=(double)center.y-origin.y;
            double along=dx*ax+dy*ay;
            double distance=along>=0 ? Math.Abs(ax*dy-ay*dx) : Math.Sqrt(dx*dx+dy*dy);
            return (float)Math.Max(0,distance-radius);
        }

        // hitbox may be an unordered cloud of the projected box's eight corners.
        // Its convex hull represents the actual box footprint, not its screen AABB.
        // Point/line footprints and tangencies count as intersections.
        internal static bool Intersects(Vector2 origin, Vector2 axis, Vector2[] hitbox,
            Vector2 minViewport, Vector2 maxViewport)
        {
            double ax, ay;
            if (!ValidView(origin, axis, minViewport, maxViewport, out ax, out ay)
                || hitbox == null || hitbox.Length == 0) return false;

            var points = new List<Point>(hitbox.Length);
            foreach (Vector2 vertex in hitbox)
            {
                if (!Finite(vertex)) return false;
                points.Add(new Point(vertex.x, vertex.y));
            }
            return ClipVisibleCone(Hull(points), origin, ax, ay, minViewport, maxViewport).Count != 0;
        }

        // Exact circle/convex-region intersection: no bounding box or polygonal
        // circle approximation. The caller supplies the native sphere's world
        // radius (local radius times the largest absolute lossy-scale component).
        internal static bool IntersectsCircle(Vector2 origin, Vector2 axis, Vector2 center, float radius,
            Vector2 minViewport, Vector2 maxViewport)
        {
            double ax, ay;
            if (!ValidView(origin, axis, minViewport, maxViewport, out ax, out ay)
                || !Finite(center) || !Finite(radius) || radius < 0) return false;
            var viewport = new List<Point> {
                new Point(minViewport.x, minViewport.y), new Point(maxViewport.x, minViewport.y),
                new Point(maxViewport.x, maxViewport.y), new Point(minViewport.x, maxViewport.y)
            };
            List<Point> region = ClipVisibleCone(viewport, origin, ax, ay, minViewport, maxViewport);
            if (region.Count == 0) return false;
            Point c = new Point(center.x, center.y);
            if (ClipVisibleCone(new List<Point> { c }, origin, ax, ay, minViewport, maxViewport).Count != 0)
                return true;
            double acceptedRadius = radius + BoundaryTolerance * Math.Max(1, radius);
            double limit = acceptedRadius * acceptedRadius;
            Point previous = region[region.Count - 1];
            foreach (Point current in region)
            {
                if (SegmentDistanceSquared(c, previous, current) <= limit) return true;
                previous = current;
            }
            return false;
        }

        private static double SegmentDistanceSquared(Point point, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared == 0 ? 0 : ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            double x = point.X - (a.X + t * dx), y = point.Y - (a.Y + t * dy);
            return x * x + y * y;
        }

        private static bool ValidView(Vector2 origin, Vector2 axis, Vector2 minViewport, Vector2 maxViewport,
            out double ax, out double ay)
        {
            ax = ay = 0;
            return Finite(origin) && Normalize(axis, out ax, out ay)
                && Finite(minViewport) && Finite(maxViewport)
                && minViewport.x <= maxViewport.x && minViewport.y <= maxViewport.y;
        }

        private static List<Point> ClipVisibleCone(List<Point> polygon, Vector2 origin, double ax, double ay,
            Vector2 minViewport, Vector2 maxViewport)
        {
            polygon = Clip(polygon, 1, 0, minViewport.x, 0);
            polygon = Clip(polygon, -1, 0, maxViewport.x, 0);
            polygon = Clip(polygon, 0, 1, 0, minViewport.y);
            polygon = Clip(polygon, 0, -1, 0, maxViewport.y);
            polygon = Clip(polygon, ax, ay, origin.x, origin.y);
            double tangent = Math.Tan(HalfAngle);
            polygon = Clip(polygon, tangent * ax + ay, tangent * ay - ax, origin.x, origin.y);
            return Clip(polygon, tangent * ax - ay, tangent * ay + ax, origin.x, origin.y);
        }

        // Return a world aim point at the center's radial distance. Even when a
        // large hitbox intersects the cone, assistance never turns beyond +/-8°.
        // Invalid data fails closed to a finite origin (or zero for invalid origin).
        internal static Vector2 ClampAim(Vector2 origin, Vector2 axis, Vector2 targetCenter)
        {
            if (!Finite(origin)) return new Vector2(0, 0);
            double ax, ay;
            if (!Finite(targetCenter) || !Normalize(axis, out ax, out ay)) return origin;
            double dx = (double)targetCenter.x - origin.x;
            double dy = (double)targetCenter.y - origin.y;
            if (dx == 0 && dy == 0) return origin;
            double angle = Math.Atan2(ax * dy - ay * dx, ax * dx + ay * dy);
            if (angle >= -HalfAngle && angle <= HalfAngle) return targetCenter;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            double along = radius * Math.Cos(HalfAngle);
            double side = radius * Math.Sin(HalfAngle) * (angle < 0 ? -1 : 1);
            float x = (float)(origin.x + along * ax - side * ay);
            float y = (float)(origin.y + along * ay + side * ax);
            return Finite(x) && Finite(y) ? new Vector2(x, y) : origin;
        }

        private static bool Normalize(Vector2 axis, out double x, out double y)
        {
            x = y = 0;
            if (!Finite(axis)) return false;
            double length = Math.Sqrt((double)axis.x * axis.x + (double)axis.y * axis.y);
            if (length == 0) return false;
            x = axis.x / length;
            y = axis.y / length;
            return true;
        }

        private static bool Finite(Vector2 point) { return Finite(point.x) && Finite(point.y); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private static double Cross(Point a, Point b, Point c)
        { return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X); }

        private static List<Point> Hull(List<Point> points)
        {
            points.Sort(delegate(Point a, Point b)
            { int order = a.X.CompareTo(b.X); return order == 0 ? a.Y.CompareTo(b.Y) : order; });
            var unique = new List<Point>();
            foreach (Point point in points)
                if (unique.Count == 0 || unique[unique.Count - 1].X != point.X || unique[unique.Count - 1].Y != point.Y)
                    unique.Add(point);
            if (unique.Count <= 2) return unique;
            var hull = new List<Point>();
            foreach (Point point in unique)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lower = hull.Count;
            for (int i = unique.Count - 2; i >= 0; i--)
            {
                Point point = unique[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        // Intersect a convex footprint with n dot (point - anchor) >= 0.
        // Successive clipping is essential: separate viewport/cone overlap tests
        // could accept disjoint portions of the same enemy footprint.
        private static List<Point> Clip(List<Point> polygon, double nx, double ny, double anchorX, double anchorY)
        {
            var result = new List<Point>();
            if (polygon.Count == 0) return result;
            Point previous = polygon[polygon.Count - 1];
            double previousDistance = nx * (previous.X - anchorX) + ny * (previous.Y - anchorY);
            bool previousInside = Inside(previous, nx, ny, anchorX, anchorY, previousDistance);
            foreach (Point current in polygon)
            {
                double currentDistance = nx * (current.X - anchorX) + ny * (current.Y - anchorY);
                bool currentInside = Inside(current, nx, ny, anchorX, anchorY, currentDistance);
                if (currentInside != previousInside)
                {
                    double t = previousDistance / (previousDistance - currentDistance);
                    t = Math.Max(0, Math.Min(1, t));
                    result.Add(new Point(previous.X + (current.X - previous.X) * t,
                        previous.Y + (current.Y - previous.Y) * t));
                }
                if (currentInside) result.Add(current);
                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }
            return result;
        }

        private static bool Inside(Point point, double nx, double ny, double anchorX, double anchorY, double distance)
        {
            double scale = Math.Max(1, Math.Abs(nx * (point.X - anchorX)) + Math.Abs(ny * (point.Y - anchorY)));
            return distance >= -BoundaryTolerance * scale;
        }
    }
}
