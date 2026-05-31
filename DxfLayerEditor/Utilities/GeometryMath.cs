using System;
using System.Collections.Generic;
using DxfLayerEditor.Models;

namespace DxfLayerEditor.Utilities
{
    /// <summary>
    /// Provides geometric intersection calculations for LINE-LINE, LINE-ARC,
    /// and ARC-ARC entity pairs. Used by the Trim tool and chain-close algorithm.
    /// </summary>
    public static class GeometryMath
    {
        /// <summary>Default tolerance for floating-point comparisons.</summary>
        public const double Epsilon = 1e-9;

        #region LINE-LINE Intersection

        /// <summary>
        /// Computes the intersection point of two infinite lines defined by
        /// their respective start/end points.
        /// </summary>
        /// <param name="a1">Start of line A.</param>
        /// <param name="a2">End of line A.</param>
        /// <param name="b1">Start of line B.</param>
        /// <param name="b2">End of line B.</param>
        /// <param name="intersection">The intersection point, if found.</param>
        /// <returns>True if the lines intersect (are not parallel).</returns>
        public static bool LineLineIntersection(
            Point2D a1, Point2D a2,
            Point2D b1, Point2D b2,
            out Point2D intersection)
        {
            intersection = Point2D.Zero;

            double dx1 = a2.X - a1.X;
            double dy1 = a2.Y - a1.Y;
            double dx2 = b2.X - b1.X;
            double dy2 = b2.Y - b1.Y;

            double denom = dx1 * dy2 - dy1 * dx2;

            if (Math.Abs(denom) < Epsilon)
                return false; // Parallel or coincident

            double t = ((b1.X - a1.X) * dy2 - (b1.Y - a1.Y) * dx2) / denom;

            intersection = new Point2D(
                a1.X + t * dx1,
                a1.Y + t * dy1
            );

            return true;
        }

        /// <summary>
        /// Computes the intersection of two finite line segments.
        /// Returns the intersection point only if it lies on both segments.
        /// </summary>
        /// <param name="a1">Start of segment A.</param>
        /// <param name="a2">End of segment A.</param>
        /// <param name="b1">Start of segment B.</param>
        /// <param name="b2">End of segment B.</param>
        /// <param name="intersection">The intersection point, if found.</param>
        /// <returns>True if the segments intersect.</returns>
        public static bool SegmentSegmentIntersection(
            Point2D a1, Point2D a2,
            Point2D b1, Point2D b2,
            out Point2D intersection)
        {
            intersection = Point2D.Zero;

            double dx1 = a2.X - a1.X;
            double dy1 = a2.Y - a1.Y;
            double dx2 = b2.X - b1.X;
            double dy2 = b2.Y - b1.Y;

            double denom = dx1 * dy2 - dy1 * dx2;

            if (Math.Abs(denom) < Epsilon)
                return false;

            double t = ((b1.X - a1.X) * dy2 - (b1.Y - a1.Y) * dx2) / denom;
            double u = ((b1.X - a1.X) * dy1 - (b1.Y - a1.Y) * dx1) / denom;

            if (t < -Epsilon || t > 1.0 + Epsilon || u < -Epsilon || u > 1.0 + Epsilon)
                return false;

            intersection = new Point2D(
                a1.X + t * dx1,
                a1.Y + t * dy1
            );

            return true;
        }

        #endregion

        #region LINE-ARC Intersection

        /// <summary>
        /// Computes intersection points between an infinite line and a circle.
        /// Solves the quadratic equation for the line-circle intersection.
        /// </summary>
        /// <param name="lineStart">A point on the line.</param>
        /// <param name="lineEnd">Another point on the line.</param>
        /// <param name="center">Center of the circle/arc.</param>
        /// <param name="radius">Radius of the circle/arc.</param>
        /// <returns>List of intersection points (0, 1, or 2).</returns>
        public static List<Point2D> LineCircleIntersections(
            Point2D lineStart, Point2D lineEnd,
            Point2D center, double radius)
        {
            var results = new List<Point2D>();

            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double fx = lineStart.X - center.X;
            double fy = lineStart.Y - center.Y;

            // Quadratic coefficients: a*t^2 + b*t + c = 0
            double a = dx * dx + dy * dy;
            double b = 2.0 * (fx * dx + fy * dy);
            double c = fx * fx + fy * fy - radius * radius;

            double discriminant = b * b - 4.0 * a * c;

            if (discriminant < -Epsilon || a < Epsilon)
                return results;

            if (discriminant < Epsilon)
            {
                // Tangent — one intersection
                double t = -b / (2.0 * a);
                results.Add(new Point2D(lineStart.X + t * dx, lineStart.Y + t * dy));
            }
            else
            {
                double sqrtD = Math.Sqrt(discriminant);
                double t1 = (-b - sqrtD) / (2.0 * a);
                double t2 = (-b + sqrtD) / (2.0 * a);

                results.Add(new Point2D(lineStart.X + t1 * dx, lineStart.Y + t1 * dy));
                results.Add(new Point2D(lineStart.X + t2 * dx, lineStart.Y + t2 * dy));
            }

            return results;
        }

        /// <summary>
        /// Computes intersection points between a finite line segment and an arc.
        /// Filters results to only include points on the segment and within the arc sweep.
        /// </summary>
        /// <param name="line">The line entity.</param>
        /// <param name="arc">The arc entity.</param>
        /// <returns>List of valid intersection points.</returns>
        public static List<Point2D> LineArcIntersections(LineEntity line, ArcEntity arc)
        {
            var circleHits = LineCircleIntersections(line.Start, line.End, arc.Center, arc.Radius);
            var results = new List<Point2D>();

            foreach (var pt in circleHits)
            {
                if (!IsPointOnSegment(pt, line.Start, line.End))
                    continue;

                double angle = Math.Atan2(pt.Y - arc.Center.Y, pt.X - arc.Center.X) * 180.0 / Math.PI;
                angle = ArcEntity.NormalizeAngle(angle);

                if (arc.ContainsAngle(angle))
                    results.Add(pt);
            }

            return results;
        }

        #endregion

        #region ARC-ARC Intersection

        /// <summary>
        /// Computes intersection points between two circles.
        /// </summary>
        /// <param name="c1">Center of circle 1.</param>
        /// <param name="r1">Radius of circle 1.</param>
        /// <param name="c2">Center of circle 2.</param>
        /// <param name="r2">Radius of circle 2.</param>
        /// <returns>List of intersection points (0, 1, or 2).</returns>
        public static List<Point2D> CircleCircleIntersections(
            Point2D c1, double r1,
            Point2D c2, double r2)
        {
            var results = new List<Point2D>();

            double d = c1.DistanceTo(c2);

            // No intersection cases
            if (d > r1 + r2 + Epsilon)       // Too far apart
                return results;
            if (d < Math.Abs(r1 - r2) - Epsilon) // One inside the other
                return results;
            if (d < Epsilon && Math.Abs(r1 - r2) < Epsilon) // Coincident
                return results;

            double a = (r1 * r1 - r2 * r2 + d * d) / (2.0 * d);
            double hSq = r1 * r1 - a * a;

            if (hSq < 0) hSq = 0; // Clamp numerical noise
            double h = Math.Sqrt(hSq);

            // Point on line between centers, distance 'a' from c1
            double px = c1.X + a * (c2.X - c1.X) / d;
            double py = c1.Y + a * (c2.Y - c1.Y) / d;

            if (h < Epsilon)
            {
                // Tangent — one point
                results.Add(new Point2D(px, py));
            }
            else
            {
                // Two intersection points
                double offsetX = h * (c2.Y - c1.Y) / d;
                double offsetY = h * (c2.X - c1.X) / d;

                results.Add(new Point2D(px + offsetX, py - offsetY));
                results.Add(new Point2D(px - offsetX, py + offsetY));
            }

            return results;
        }

        /// <summary>
        /// Computes intersection points between two arcs, filtered to
        /// only include points within both arcs' angular sweeps.
        /// </summary>
        /// <param name="arc1">First arc entity.</param>
        /// <param name="arc2">Second arc entity.</param>
        /// <returns>List of valid intersection points.</returns>
        public static List<Point2D> ArcArcIntersections(ArcEntity arc1, ArcEntity arc2)
        {
            var circleHits = CircleCircleIntersections(
                arc1.Center, arc1.Radius,
                arc2.Center, arc2.Radius);

            var results = new List<Point2D>();

            foreach (var pt in circleHits)
            {
                double angle1 = Math.Atan2(pt.Y - arc1.Center.Y, pt.X - arc1.Center.X) * 180.0 / Math.PI;
                double angle2 = Math.Atan2(pt.Y - arc2.Center.Y, pt.X - arc2.Center.X) * 180.0 / Math.PI;

                angle1 = ArcEntity.NormalizeAngle(angle1);
                angle2 = ArcEntity.NormalizeAngle(angle2);

                if (arc1.ContainsAngle(angle1) && arc2.ContainsAngle(angle2))
                    results.Add(pt);
            }

            return results;
        }

        #endregion

        #region Point-on-Segment Validation

        /// <summary>
        /// Determines whether a point lies on the line segment from
        /// <paramref name="segStart"/> to <paramref name="segEnd"/>.
        /// </summary>
        /// <param name="point">The point to test.</param>
        /// <param name="segStart">Segment start.</param>
        /// <param name="segEnd">Segment end.</param>
        /// <param name="tolerance">Distance tolerance for "on segment" test.</param>
        /// <returns>True if the point is on the segment within tolerance.</returns>
        public static bool IsPointOnSegment(Point2D point, Point2D segStart, Point2D segEnd, double tolerance = 1e-6)
        {
            double segLen = segStart.DistanceTo(segEnd);
            if (segLen < Epsilon)
                return point.DistanceTo(segStart) < tolerance;

            // Project point onto the line and check parameter t ∈ [0, 1]
            double dx = segEnd.X - segStart.X;
            double dy = segEnd.Y - segStart.Y;

            double t = ((point.X - segStart.X) * dx + (point.Y - segStart.Y) * dy) / (dx * dx + dy * dy);

            if (t < -tolerance / segLen || t > 1.0 + tolerance / segLen)
                return false;

            // Check perpendicular distance
            double projX = segStart.X + t * dx;
            double projY = segStart.Y + t * dy;
            double dist = Math.Sqrt((point.X - projX) * (point.X - projX) +
                                    (point.Y - projY) * (point.Y - projY));

            return dist < tolerance;
        }

        #endregion

        #region Distance Calculations

        /// <summary>
        /// Returns the minimum distance from a point to a line segment.
        /// </summary>
        public static double PointToSegmentDistance(Point2D point, Point2D segStart, Point2D segEnd)
        {
            double dx = segEnd.X - segStart.X;
            double dy = segEnd.Y - segStart.Y;
            double lenSq = dx * dx + dy * dy;

            if (lenSq < Epsilon)
                return point.DistanceTo(segStart);

            double t = ((point.X - segStart.X) * dx + (point.Y - segStart.Y) * dy) / lenSq;
            t = Math.Clamp(t, 0.0, 1.0);

            var closest = new Point2D(segStart.X + t * dx, segStart.Y + t * dy);
            return point.DistanceTo(closest);
        }

        /// <summary>
        /// Returns the minimum distance from a point to an arc.
        /// </summary>
        public static double PointToArcDistance(Point2D point, ArcEntity arc)
        {
            double angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X) * 180.0 / Math.PI;
            angle = ArcEntity.NormalizeAngle(angle);

            if (arc.ContainsAngle(angle))
            {
                // Closest point is on the arc — distance to the arc curve
                double distToCenter = point.DistanceTo(arc.Center);
                return Math.Abs(distToCenter - arc.Radius);
            }
            else
            {
                // Closest point is one of the arc endpoints
                return Math.Min(
                    point.DistanceTo(arc.StartPoint),
                    point.DistanceTo(arc.EndPoint));
            }
        }

        /// <summary>
        /// Returns the minimum distance from a point to a circle perimeter.
        /// </summary>
        public static double PointToCircleDistance(Point2D point, CircleEntity circle)
        {
            return Math.Abs(point.DistanceTo(circle.Center) - circle.Radius);
        }

        /// <summary>
        /// Returns the closest point on a segment to the given point.
        /// </summary>
        public static Point2D ClosestPointOnSegment(Point2D point, Point2D segStart, Point2D segEnd)
        {
            double dx = segEnd.X - segStart.X;
            double dy = segEnd.Y - segStart.Y;
            double lenSq = dx * dx + dy * dy;

            if (lenSq < Epsilon)
                return segStart;

            double t = ((point.X - segStart.X) * dx + (point.Y - segStart.Y) * dy) / lenSq;
            t = Math.Clamp(t, 0.0, 1.0);

            return new Point2D(segStart.X + t * dx, segStart.Y + t * dy);
        }

        #endregion

        #region Angle Utilities

        /// <summary>
        /// Returns the angle in degrees from <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        public static double AngleBetween(Point2D from, Point2D to)
        {
            return Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI;
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        public static double DegToRad(double degrees) => degrees * Math.PI / 180.0;

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        public static double RadToDeg(double radians) => radians * 180.0 / Math.PI;

        #endregion
    }
}
