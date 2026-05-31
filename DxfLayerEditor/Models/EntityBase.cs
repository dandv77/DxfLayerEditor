using System;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Enumerates the supported DXF entity types.
    /// </summary>
    public enum EntityType
    {
        Line,
        Arc,
        Circle,
        Point,
        Polyline,
        Ellipse,
        Spline,
        Text
    }

    /// <summary>
    /// Abstract base class for all DXF geometry entities.
    /// Provides common properties: ID, layer, color, and chain membership.
    /// </summary>
    public abstract class EntityBase
    {
        /// <summary>Unique entity identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Source DXF layer name.</summary>
        public string Layer { get; set; } = "0";

        /// <summary>AutoCAD Color Index (0–255).</summary>
        public int Color { get; set; } = 7;

        /// <summary>Chain group identifier assigned by Union-Find chain detection.</summary>
        public string? ChainId { get; set; }

        /// <summary>The type of this entity.</summary>
        public abstract EntityType EntityType { get; }

        /// <summary>
        /// Returns the endpoints of this entity for chain detection.
        /// Closed entities (Circle) return an empty array.
        /// </summary>
        public abstract Point2D[] GetEndpoints();

        /// <summary>
        /// Returns the axis-aligned bounding box as (minX, minY, maxX, maxY).
        /// </summary>
        public abstract (double MinX, double MinY, double MaxX, double MaxY) GetBounds();
    }
}
