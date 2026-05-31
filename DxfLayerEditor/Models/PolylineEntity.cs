using System;
using System.Collections.Generic;
using System.Linq;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents a single vertex in a polyline, with an optional bulge value.
    /// </summary>
    public readonly struct PolylineVertex
    {
        /// <summary>Position of this vertex.</summary>
        public Point2D Position { get; }

        /// <summary>
        /// Bulge factor for the segment starting at this vertex.
        /// 0 = straight line, positive = CCW arc, negative = CW arc.
        /// bulge = tan(sweep_angle / 4).
        /// </summary>
        public double Bulge { get; }

        public PolylineVertex(Point2D position, double bulge = 0.0)
        {
            Position = position;
            Bulge = bulge;
        }

        public PolylineVertex(double x, double y, double bulge = 0.0)
        {
            Position = new Point2D(x, y);
            Bulge = bulge;
        }
    }

    /// <summary>
    /// Represents an LWPOLYLINE or POLYLINE entity — a sequence of vertices
    /// with optional bulge values producing LINE and ARC segments.
    /// </summary>
    public class PolylineEntity : EntityBase
    {
        /// <summary>Ordered vertices with bulge values.</summary>
        public List<PolylineVertex> Vertices { get; set; } = new();

        /// <summary>Whether this polyline is closed (last vertex connects to first).</summary>
        public bool IsClosed { get; set; }

        /// <inheritdoc />
        public override EntityType EntityType => EntityType.Polyline;

        /// <summary>
        /// Returns the start and end points of the polyline.
        /// If closed, returns empty (like a circle — no free endpoints).
        /// </summary>
        public override Point2D[] GetEndpoints()
        {
            if (IsClosed || Vertices.Count < 2)
                return Array.Empty<Point2D>();

            return new[]
            {
                Vertices[0].Position,
                Vertices[^1].Position
            };
        }

        /// <inheritdoc />
        public override (double MinX, double MinY, double MaxX, double MaxY) GetBounds()
        {
            if (Vertices.Count == 0)
                return (0, 0, 0, 0);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var v in Vertices)
            {
                minX = Math.Min(minX, v.Position.X);
                minY = Math.Min(minY, v.Position.Y);
                maxX = Math.Max(maxX, v.Position.X);
                maxY = Math.Max(maxY, v.Position.Y);
            }

            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Computes the arc parameters for a polyline segment defined by two consecutive
        /// vertices with a non-zero bulge value.
        /// </summary>
        /// <param name="p1">Start vertex position.</param>
        /// <param name="p2">End vertex position.</param>
        /// <param name="bulge">Bulge value of the segment.</param>
        /// <returns>Tuple of (center, radius, startAngleDeg, endAngleDeg).</returns>
        public static (Point2D Center, double Radius, double StartAngle, double EndAngle) BulgeToArc(
            Point2D p1, Point2D p2, double bulge)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            double s = d / 2.0;

            // bulge = tan(sweep / 4)
            double radius = Math.Abs(s * (bulge * bulge + 1) / (2 * bulge));
            double sweepAngle = 4.0 * Math.Atan(Math.Abs(bulge));

            // Midpoint of chord
            double mx = (p1.X + p2.X) / 2.0;
            double my = (p1.Y + p2.Y) / 2.0;

            // Distance from chord midpoint to center
            double h = radius - s * Math.Abs(bulge);

            // Normal to chord direction
            double nx = -dy / d;
            double ny = dx / d;

            // Center is on the side determined by bulge sign
            double sign = bulge > 0 ? 1.0 : -1.0;
            double cx = mx + sign * h * nx;
            double cy = my + sign * h * ny;

            var center = new Point2D(cx, cy);

            // Compute start and end angles
            double startAngle = Math.Atan2(p1.Y - cy, p1.X - cx) * 180.0 / Math.PI;
            double endAngle = Math.Atan2(p2.Y - cy, p2.X - cx) * 180.0 / Math.PI;

            // If bulge is negative (CW), swap start/end to maintain CCW convention
            if (bulge < 0)
            {
                (startAngle, endAngle) = (endAngle, startAngle);
            }

            // Normalize angles
            startAngle = ArcEntity.NormalizeAngle(startAngle);
            endAngle = ArcEntity.NormalizeAngle(endAngle);

            return (center, radius, startAngle, endAngle);
        }
    }
}
