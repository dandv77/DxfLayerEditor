using System;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents a POINT entity at a specific location.
    /// Points can be converted to circles for drill hole representation.
    /// </summary>
    public class PointEntity : EntityBase
    {
        /// <summary>Position of the point.</summary>
        public Point2D Position { get; set; }

        /// <inheritdoc />
        public override EntityType EntityType => EntityType.Point;

        public PointEntity(Point2D position)
        {
            Position = position;
        }

        /// <inheritdoc />
        public override Point2D[] GetEndpoints() => new[] { Position };

        /// <inheritdoc />
        public override (double MinX, double MinY, double MaxX, double MaxY) GetBounds()
        {
            return (Position.X, Position.Y, Position.X, Position.Y);
        }

        /// <summary>
        /// Converts this point to a circle entity with the given diameter.
        /// </summary>
        /// <param name="diameter">Diameter of the resulting circle.</param>
        /// <returns>A new <see cref="CircleEntity"/>.</returns>
        public CircleEntity ToCircle(double diameter)
        {
            return new CircleEntity(Position, diameter / 2.0)
            {
                Id = Id,
                Layer = Layer,
                Color = Color,
                ChainId = ChainId
            };
        }
    }
}
