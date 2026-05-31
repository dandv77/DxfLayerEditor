using System;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents an ARC entity defined by center, radius, and angular sweep.
    /// Angles are in degrees, measured counter-clockwise from the positive X-axis.
    /// </summary>
    public class ArcEntity : EntityBase
    {
        /// <summary>Center point of the arc.</summary>
        public Point2D Center { get; set; }

        /// <summary>Radius of the arc.</summary>
        public double Radius { get; set; }

        /// <summary>Start angle in degrees (CCW from +X).</summary>
        public double StartAngle { get; set; }

        /// <summary>End angle in degrees (CCW from +X).</summary>
        public double EndAngle { get; set; }

        /// <inheritdoc />
        public override EntityType EntityType => EntityType.Arc;

        /// <summary>
        /// Initializes a new ARC entity.
        /// </summary>
        public ArcEntity(Point2D center, double radius, double startAngle, double endAngle)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }

        /// <summary>
        /// Returns the point on the arc at the given angle (degrees).
        /// </summary>
        public Point2D PointAtAngle(double angleDegrees)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new Point2D(
                Center.X + Radius * Math.Cos(rad),
                Center.Y + Radius * Math.Sin(rad)
            );
        }

        /// <summary>Start point of the arc.</summary>
        public Point2D StartPoint => PointAtAngle(StartAngle);

        /// <summary>End point of the arc.</summary>
        public Point2D EndPoint => PointAtAngle(EndAngle);

        /// <summary>
        /// Returns the sweep angle in degrees (always positive, CCW).
        /// </summary>
        public double SweepAngle
        {
            get
            {
                double sweep = EndAngle - StartAngle;
                while (sweep < 0) sweep += 360.0;
                while (sweep > 360) sweep -= 360.0;
                return sweep == 0 ? 360.0 : sweep;
            }
        }

        /// <summary>
        /// Determines whether the given angle (degrees) lies within the arc's sweep.
        /// </summary>
        public bool ContainsAngle(double angleDegrees)
        {
            double a = NormalizeAngle(angleDegrees);
            double s = NormalizeAngle(StartAngle);
            double e = NormalizeAngle(EndAngle);

            if (s <= e)
                return a >= s && a <= e;
            else
                return a >= s || a <= e;
        }

        /// <summary>
        /// Normalizes an angle to the range [0, 360).
        /// </summary>
        public static double NormalizeAngle(double angle)
        {
            angle %= 360.0;
            if (angle < 0) angle += 360.0;
            return angle;
        }

        /// <inheritdoc />
        public override Point2D[] GetEndpoints() => new[] { StartPoint, EndPoint };

        /// <inheritdoc />
        public override (double MinX, double MinY, double MaxX, double MaxY) GetBounds()
        {
            double minX = Math.Min(StartPoint.X, EndPoint.X);
            double minY = Math.Min(StartPoint.Y, EndPoint.Y);
            double maxX = Math.Max(StartPoint.X, EndPoint.X);
            double maxY = Math.Max(StartPoint.Y, EndPoint.Y);

            // Check if cardinal directions (0°, 90°, 180°, 270°) are within the sweep
            double[] cardinals = { 0, 90, 180, 270 };
            foreach (double c in cardinals)
            {
                if (ContainsAngle(c))
                {
                    var p = PointAtAngle(c);
                    minX = Math.Min(minX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X);
                    maxY = Math.Max(maxY, p.Y);
                }
            }

            return (minX, minY, maxX, maxY);
        }
    }
}
