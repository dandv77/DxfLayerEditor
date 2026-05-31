using System;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents a 2D point with double-precision coordinates.
    /// Immutable value type used throughout the geometry pipeline.
    /// </summary>
    public readonly struct Point2D : IEquatable<Point2D>
    {
        /// <summary>Gets the X coordinate.</summary>
        public double X { get; }

        /// <summary>Gets the Y coordinate.</summary>
        public double Y { get; }

        /// <summary>The origin point (0, 0).</summary>
        public static readonly Point2D Zero = new(0, 0);

        /// <summary>
        /// Initializes a new <see cref="Point2D"/> with the given coordinates.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Returns the Euclidean distance to another point.
        /// </summary>
        public double DistanceTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Returns the squared distance to another point (avoids sqrt for comparisons).
        /// </summary>
        public double DistanceSquaredTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Returns the midpoint between this point and another.
        /// </summary>
        public Point2D MidpointTo(Point2D other)
        {
            return new Point2D((X + other.X) / 2.0, (Y + other.Y) / 2.0);
        }

        /// <summary>
        /// Rounds coordinates to the given number of decimal places.
        /// Used for endpoint clustering in chain detection.
        /// </summary>
        /// <param name="decimals">Number of decimal places.</param>
        public Point2D Round(int decimals)
        {
            return new Point2D(
                Math.Round(X, decimals),
                Math.Round(Y, decimals)
            );
        }

        /// <summary>
        /// Rounds coordinates to a grid defined by the given tolerance.
        /// Each coordinate is snapped to the nearest multiple of <paramref name="tolerance"/>.
        /// </summary>
        /// <param name="tolerance">Grid cell size.</param>
        public Point2D RoundToGrid(double tolerance)
        {
            if (tolerance <= 0) return this;
            return new Point2D(
                Math.Round(X / tolerance) * tolerance,
                Math.Round(Y / tolerance) * tolerance
            );
        }

        /// <summary>
        /// Returns a string key suitable for dictionary lookups after rounding.
        /// </summary>
        public string ToKey(int decimals = 6)
        {
            var rounded = Round(decimals);
            return $"{rounded.X},{rounded.Y}";
        }

        public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
        public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
        public static Point2D operator *(Point2D p, double s) => new(p.X * s, p.Y * s);
        public static Point2D operator *(double s, Point2D p) => new(p.X * s, p.Y * s);
        public static bool operator ==(Point2D a, Point2D b) => a.Equals(b);
        public static bool operator !=(Point2D a, Point2D b) => !a.Equals(b);

        /// <summary>
        /// Returns the dot product of two point-vectors.
        /// </summary>
        public static double Dot(Point2D a, Point2D b) => a.X * b.X + a.Y * b.Y;

        /// <summary>
        /// Returns the 2D cross product (z-component of 3D cross).
        /// </summary>
        public static double Cross(Point2D a, Point2D b) => a.X * b.Y - a.Y * b.X;

        /// <summary>
        /// Returns the length (magnitude) of this vector.
        /// </summary>
        public double Length() => Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// Returns a unit vector in the same direction.
        /// </summary>
        public Point2D Normalized()
        {
            double len = Length();
            return len < 1e-15 ? Zero : new Point2D(X / len, Y / len);
        }

        public bool Equals(Point2D other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        /// <summary>
        /// Checks approximate equality within a tolerance.
        /// </summary>
        public bool ApproxEquals(Point2D other, double tolerance = 1e-9)
        {
            return Math.Abs(X - other.X) < tolerance && Math.Abs(Y - other.Y) < tolerance;
        }

        public override bool Equals(object? obj) => obj is Point2D p && Equals(p);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:G}, {Y:G})";
    }
}
