using System;
using System.Collections.Generic;
using DxfLayerEditor.Models;

namespace DxfLayerEditor.Utilities
{
    /// <summary>
    /// Describes the type of a snap candidate point.
    /// </summary>
    public enum SnapType
    {
        /// <summary>Entity start or end point.</summary>
        Endpoint,
        /// <summary>Midpoint of a line segment.</summary>
        Midpoint,
        /// <summary>Center of an arc or circle.</summary>
        Center,
        /// <summary>Cardinal quadrant point on an arc/circle (0°, 90°, 180°, 270°).</summary>
        Quadrant,
        /// <summary>Tangent point from an external reference point.</summary>
        Tangent,
        /// <summary>Intersection of two entities.</summary>
        Intersection
    }

    /// <summary>
    /// A snap candidate with position and type metadata.
    /// </summary>
    public class SnapCandidate
    {
        /// <summary>World-space position of the snap point.</summary>
        public Point2D Position { get; set; }

        /// <summary>Type of snap.</summary>
        public SnapType Type { get; set; }

        /// <summary>Optional label for display.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>ID of the entity this snap belongs to.</summary>
        public string? EntityId { get; set; }

        public SnapCandidate(Point2D position, SnapType type, string? entityId = null)
        {
            Position = position;
            Type = type;
            EntityId = entityId;
            Label = type.ToString();
        }
    }

    /// <summary>
    /// Generates snap candidate points for the Move tool.
    /// Given a target entity (being hovered) and an anchor point (the endpoint being moved),
    /// produces all valid snap locations: endpoints, midpoints, arc center, quadrants, tangents.
    /// </summary>
    public static class SnapUtilities
    {
        /// <summary>
        /// Generates all snap candidates for a target entity, optionally filtered
        /// by the anchor (moved endpoint) position for tangent/projection snaps.
        /// </summary>
        /// <param name="target">The entity to generate snap points from.</param>
        /// <param name="anchor">Optional anchor point (the endpoint being moved).</param>
        /// <returns>List of snap candidates.</returns>
        public static List<SnapCandidate> GetSnapCandidates(EntityBase target, Point2D? anchor = null)
        {
            var candidates = new List<SnapCandidate>();

            switch (target)
            {
                case LineEntity line:
                    AddLineSnapCandidates(line, anchor, candidates);
                    break;
                case ArcEntity arc:
                    AddArcSnapCandidates(arc, anchor, candidates);
                    break;
                case CircleEntity circle:
                    AddCircleSnapCandidates(circle, anchor, candidates);
                    break;
            }

            return candidates;
        }

        /// <summary>
        /// Generates snap candidates for a LINE entity:
        /// endpoints and midpoint.
        /// </summary>
        private static void AddLineSnapCandidates(
            LineEntity line, Point2D? anchor, List<SnapCandidate> candidates)
        {
            // Endpoints
            candidates.Add(new SnapCandidate(line.Start, SnapType.Endpoint, line.Id)
                { Label = "Start" });
            candidates.Add(new SnapCandidate(line.End, SnapType.Endpoint, line.Id)
                { Label = "End" });

            // Midpoint
            candidates.Add(new SnapCandidate(line.Midpoint, SnapType.Midpoint, line.Id)
                { Label = "Midpoint" });
        }

        /// <summary>
        /// Generates snap candidates for an ARC entity:
        /// endpoints, center, quadrant points within sweep, and tangent points.
        /// </summary>
        private static void AddArcSnapCandidates(
            ArcEntity arc, Point2D? anchor, List<SnapCandidate> candidates)
        {
            // Endpoints
            candidates.Add(new SnapCandidate(arc.StartPoint, SnapType.Endpoint, arc.Id)
                { Label = "Arc Start" });
            candidates.Add(new SnapCandidate(arc.EndPoint, SnapType.Endpoint, arc.Id)
                { Label = "Arc End" });

            // Center
            candidates.Add(new SnapCandidate(arc.Center, SnapType.Center, arc.Id)
                { Label = "Center" });

            // Quadrant points (0°, 90°, 180°, 270°) — only if within the arc sweep
            AddQuadrantCandidates(arc.Center, arc.Radius, arc, candidates);

            // Tangent points from anchor
            if (anchor.HasValue)
            {
                AddTangentCandidates(arc.Center, arc.Radius, anchor.Value, arc, candidates);
            }
        }

        /// <summary>
        /// Generates snap candidates for a CIRCLE entity:
        /// center, all 4 quadrant points, and tangent points from anchor.
        /// </summary>
        private static void AddCircleSnapCandidates(
            CircleEntity circle, Point2D? anchor, List<SnapCandidate> candidates)
        {
            // Center
            candidates.Add(new SnapCandidate(circle.Center, SnapType.Center, circle.Id)
                { Label = "Center" });

            // All 4 quadrant points (circle has full 360° sweep)
            double[] quads = { 0, 90, 180, 270 };
            foreach (double angle in quads)
            {
                var pt = circle.PointAtAngle(angle);
                candidates.Add(new SnapCandidate(pt, SnapType.Quadrant, circle.Id)
                    { Label = $"Quad {angle}°" });
            }

            // Tangent points from anchor
            if (anchor.HasValue)
            {
                AddTangentCandidates(circle.Center, circle.Radius, anchor.Value, null, candidates);
            }
        }

        /// <summary>
        /// Adds quadrant snap candidates (0°, 90°, 180°, 270°) for an arc/circle,
        /// filtered to only include angles within the arc's sweep.
        /// </summary>
        /// <param name="center">Circle/arc center.</param>
        /// <param name="radius">Radius.</param>
        /// <param name="arc">Optional arc for sweep filtering (null = full circle).</param>
        /// <param name="candidates">Target list to add candidates to.</param>
        private static void AddQuadrantCandidates(
            Point2D center, double radius, ArcEntity? arc, List<SnapCandidate> candidates)
        {
            double[] quads = { 0, 90, 180, 270 };
            foreach (double angle in quads)
            {
                if (arc != null && !arc.ContainsAngle(angle))
                    continue;

                double rad = angle * Math.PI / 180.0;
                var pt = new Point2D(
                    center.X + radius * Math.Cos(rad),
                    center.Y + radius * Math.Sin(rad));

                candidates.Add(new SnapCandidate(pt, SnapType.Quadrant, arc?.Id)
                    { Label = $"Quad {angle}°" });
            }
        }

        /// <summary>
        /// Computes tangent points on a circle from an external anchor point.
        /// Tangent points are where a line from the anchor touches the circle at exactly one point.
        /// </summary>
        /// <param name="center">Circle center.</param>
        /// <param name="radius">Circle radius.</param>
        /// <param name="anchor">External point.</param>
        /// <param name="arc">Optional arc for sweep filtering.</param>
        /// <param name="candidates">Target list to add candidates to.</param>
        private static void AddTangentCandidates(
            Point2D center, double radius, Point2D anchor, ArcEntity? arc, List<SnapCandidate> candidates)
        {
            double dx = anchor.X - center.X;
            double dy = anchor.Y - center.Y;
            double distSq = dx * dx + dy * dy;
            double dist = Math.Sqrt(distSq);

            // Anchor must be outside the circle
            if (dist <= radius + GeometryMath.Epsilon)
                return;

            // Tangent length from anchor to tangent point
            double tangentLength = Math.Sqrt(distSq - radius * radius);

            // Angle from center to anchor
            double angleToAnchor = Math.Atan2(dy, dx);

            // Half-angle of the tangent cone
            double halfAngle = Math.Asin(radius / dist);

            // Two tangent directions
            double angle1 = angleToAnchor + halfAngle + Math.PI / 2.0;
            double angle2 = angleToAnchor - halfAngle - Math.PI / 2.0;

            // Tangent points on the circle
            var tp1 = new Point2D(
                center.X + radius * Math.Cos(angle1),
                center.Y + radius * Math.Sin(angle1));
            var tp2 = new Point2D(
                center.X + radius * Math.Cos(angle2),
                center.Y + radius * Math.Sin(angle2));

            // Apply arc sweep filter
            if (arc != null)
            {
                double deg1 = ArcEntity.NormalizeAngle(angle1 * 180.0 / Math.PI);
                double deg2 = ArcEntity.NormalizeAngle(angle2 * 180.0 / Math.PI);

                if (arc.ContainsAngle(deg1))
                    candidates.Add(new SnapCandidate(tp1, SnapType.Tangent, arc.Id) { Label = "Tangent" });
                if (arc.ContainsAngle(deg2))
                    candidates.Add(new SnapCandidate(tp2, SnapType.Tangent, arc.Id) { Label = "Tangent" });
            }
            else
            {
                candidates.Add(new SnapCandidate(tp1, SnapType.Tangent) { Label = "Tangent" });
                candidates.Add(new SnapCandidate(tp2, SnapType.Tangent) { Label = "Tangent" });
            }
        }

        /// <summary>
        /// Finds the nearest snap candidate to the given world-space position
        /// within the specified pick radius.
        /// </summary>
        /// <param name="position">Mouse/cursor world position.</param>
        /// <param name="candidates">Available snap candidates.</param>
        /// <param name="pickRadius">Maximum snap distance in world units.</param>
        /// <returns>The nearest snap candidate, or null if none within range.</returns>
        public static SnapCandidate? FindNearest(
            Point2D position, IEnumerable<SnapCandidate> candidates, double pickRadius)
        {
            SnapCandidate? best = null;
            double bestDist = double.MaxValue;

            foreach (var c in candidates)
            {
                double d = position.DistanceTo(c.Position);
                if (d <= pickRadius && d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            return best;
        }
    }
}
