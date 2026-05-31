using System;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents a LINE entity defined by a start and end point.
    /// </summary>
    public class LineEntity : EntityBase
    {
        /// <summary>Start point of the line.</summary>
        public Point2D Start { get; set; }

        /// <summary>End point of the line.</summary>
        public Point2D End { get; set; }

        /// <inheritdoc />
        public override EntityType EntityType => EntityType.Line;

        /// <summary>
        /// Initializes a new LINE from two points.
        /// </summary>
        public LineEntity(Point2D start, Point2D end)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// Returns the length of this line segment.
        /// </summary>
        public double Length => Start.DistanceTo(End);

        /// <summary>
        /// Returns the midpoint of this line segment.
        /// </summary>
        public Point2D Midpoint => Start.MidpointTo(End);

        /// <summary>
        /// Returns the unit direction vector from Start to End.
        /// </summary>
        public Point2D Direction => (End - Start).Normalized();

        /// <inheritdoc />
        public override Point2D[] GetEndpoints() => new[] { Start, End };

        /// <inheritdoc />
        public override (double MinX, double MinY, double MaxX, double MaxY) GetBounds()
        {
            return (
                Math.Min(Start.X, End.X),
                Math.Min(Start.Y, End.Y),
                Math.Max(Start.X, End.X),
                Math.Max(Start.Y, End.Y)
            );
        }
    }
}
