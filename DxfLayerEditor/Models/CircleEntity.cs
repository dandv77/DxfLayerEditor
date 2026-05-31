using System;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents a CIRCLE entity defined by center and radius.
    /// Circles are closed entities with no endpoints for chain detection.
    /// </summary>
    public class CircleEntity : EntityBase
    {
        /// <summary>Center point of the circle.</summary>
        public Point2D Center { get; set; }

        /// <summary>Radius of the circle.</summary>
        public double Radius { get; set; }

        /// <inheritdoc />
        public override EntityType EntityType => EntityType.Circle;

        /// <summary>
        /// Initializes a new CIRCLE entity.
        /// </summary>
        public CircleEntity(Point2D center, double radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>Diameter of the circle.</summary>
        public double Diameter => Radius * 2.0;

        /// <summary>Circumference of the circle.</summary>
        public double Circumference => 2.0 * Math.PI * Radius;

        /// <summary>Area of the circle.</summary>
        public double Area => Math.PI * Radius * Radius;

        /// <summary>
        /// Returns the point on the circle at the given angle (degrees, CCW from +X).
        /// </summary>
        public Point2D PointAtAngle(double angleDegrees)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new Point2D(
                Center.X + Radius * Math.Cos(rad),
                Center.Y + Radius * Math.Sin(rad)
            );
        }

        /// <summary>
        /// Circles are closed — they have no endpoints for chain detection.
        /// </summary>
        public override Point2D[] GetEndpoints() => Array.Empty<Point2D>();

        /// <inheritdoc />
        public override (double MinX, double MinY, double MaxX, double MaxY) GetBounds()
        {
            return (
                Center.X - Radius,
                Center.Y - Radius,
                Center.X + Radius,
                Center.Y + Radius
            );
        }
    }
}
