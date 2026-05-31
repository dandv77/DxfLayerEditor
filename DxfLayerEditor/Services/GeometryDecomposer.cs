using System;
using System.Collections.Generic;
using DxfLayerEditor.Models;

namespace DxfLayerEditor.Services
{
    /// <summary>
    /// Decomposes complex DXF entities (Polylines, Splines, Ellipses) into
    /// LINE and ARC primitives suitable for Thermwood CNC export.
    /// </summary>
    public static class GeometryDecomposer
    {
        /// <summary>
        /// Decomposes a polyline into LINE and ARC primitives.
        /// Segments with zero bulge become LINEs; non-zero bulge become ARCs.
        /// </summary>
        /// <param name="polyline">The polyline entity to decompose.</param>
        /// <returns>List of LINE and ARC primitives.</returns>
        public static List<EntityBase> DecomposePolyline(PolylineEntity polyline)
        {
            var primitives = new List<EntityBase>();
            var verts = polyline.Vertices;

            if (verts.Count < 2)
                return primitives;

            int segmentCount = polyline.IsClosed ? verts.Count : verts.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                int nextIdx = (i + 1) % verts.Count;
                var v1 = verts[i];
                var v2 = verts[nextIdx];

                if (Math.Abs(v1.Bulge) < 1e-10)
                {
                    // Straight segment → LINE
                    var line = new LineEntity(v1.Position, v2.Position)
                    {
                        Layer = polyline.Layer,
                        Color = polyline.Color,
                        ChainId = polyline.ChainId
                    };
                    primitives.Add(line);
                }
                else
                {
                    // Curved segment → ARC
                    var (center, radius, startAngle, endAngle) =
                        PolylineEntity.BulgeToArc(v1.Position, v2.Position, v1.Bulge);

                    var arc = new ArcEntity(center, radius, startAngle, endAngle)
                    {
                        Layer = polyline.Layer,
                        Color = polyline.Color,
                        ChainId = polyline.ChainId
                    };
                    primitives.Add(arc);
                }
            }

            return primitives;
        }

        /// <summary>
        /// Approximates a spline as a series of LINE segments using chord-tolerance sampling.
        /// Since full B-spline evaluation requires the control points and knot vector,
        /// this method accepts pre-sampled points (from the DXF library's flattening method).
        /// </summary>
        /// <param name="samplePoints">
        /// Ordered points along the spline, typically obtained from
        /// a DXF library's <c>Spline.Flatten(chordTolerance)</c> method.
        /// </param>
        /// <param name="layer">Layer name for generated entities.</param>
        /// <param name="color">ACI color for generated entities.</param>
        /// <param name="chainId">Optional chain ID.</param>
        /// <returns>List of LINE primitives approximating the spline.</returns>
        public static List<LineEntity> ApproximateSpline(
            IList<Point2D> samplePoints,
            string layer = "0",
            int color = 7,
            string? chainId = null)
        {
            var lines = new List<LineEntity>();

            if (samplePoints.Count < 2)
                return lines;

            for (int i = 0; i < samplePoints.Count - 1; i++)
            {
                var line = new LineEntity(samplePoints[i], samplePoints[i + 1])
                {
                    Layer = layer,
                    Color = color,
                    ChainId = chainId
                };
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>
        /// Approximates an ellipse as a series of LINE segments.
        /// Samples points along the ellipse and connects them with lines.
        /// </summary>
        /// <param name="center">Ellipse center.</param>
        /// <param name="majorAxisEndpoint">
        /// Endpoint of the major axis (relative to center, defines direction and length).
        /// </param>
        /// <param name="axisRatio">Ratio of minor axis to major axis (0 &lt; ratio ≤ 1).</param>
        /// <param name="startParam">Start parameter (radians, 0 = start of major axis).</param>
        /// <param name="endParam">End parameter (radians, 2π = full ellipse).</param>
        /// <param name="chordTolerance">
        /// Maximum chord-to-curve deviation. Smaller values produce more segments.
        /// </param>
        /// <param name="layer">Layer name for generated entities.</param>
        /// <param name="color">ACI color for generated entities.</param>
        /// <param name="chainId">Optional chain ID.</param>
        /// <returns>List of LINE primitives approximating the ellipse.</returns>
        public static List<LineEntity> ApproximateEllipse(
            Point2D center,
            Point2D majorAxisEndpoint,
            double axisRatio,
            double startParam,
            double endParam,
            double chordTolerance = 1e-3,
            string layer = "0",
            int color = 7,
            string? chainId = null)
        {
            double majorLength = majorAxisEndpoint.Length();
            double minorLength = majorLength * axisRatio;

            if (majorLength < GeometryMath.Epsilon)
                return new List<LineEntity>();

            // Rotation angle of the major axis
            double rotation = Math.Atan2(majorAxisEndpoint.Y, majorAxisEndpoint.X);

            // Adaptive segment count based on chord tolerance
            double avgRadius = (majorLength + minorLength) / 2.0;
            int segmentCount = EstimateSegmentCount(avgRadius, startParam, endParam, chordTolerance);
            segmentCount = Math.Max(segmentCount, 8); // Minimum 8 segments

            double paramStep = (endParam - startParam) / segmentCount;

            var points = new List<Point2D>();
            for (int i = 0; i <= segmentCount; i++)
            {
                double param = startParam + i * paramStep;
                double ex = majorLength * Math.Cos(param);
                double ey = minorLength * Math.Sin(param);

                // Rotate to major axis orientation
                double rx = ex * Math.Cos(rotation) - ey * Math.Sin(rotation);
                double ry = ex * Math.Sin(rotation) + ey * Math.Cos(rotation);

                points.Add(new Point2D(center.X + rx, center.Y + ry));
            }

            return ApproximateSpline(points, layer, color, chainId);
        }

        /// <summary>
        /// Approximates an elliptical arc as ARC primitives (circular arcs).
        /// For near-circular ellipses (axis ratio close to 1), returns a single arc.
        /// For non-circular ellipses, falls back to LINE approximation.
        /// </summary>
        /// <param name="center">Ellipse center.</param>
        /// <param name="majorAxisEndpoint">Endpoint of the major axis relative to center.</param>
        /// <param name="axisRatio">Ratio of minor to major axis.</param>
        /// <param name="startParam">Start parameter in radians.</param>
        /// <param name="endParam">End parameter in radians.</param>
        /// <param name="layer">Layer name.</param>
        /// <param name="color">ACI color.</param>
        /// <param name="chainId">Optional chain ID.</param>
        /// <returns>List of ARC or LINE primitives.</returns>
        public static List<EntityBase> ApproximateEllipseAsArcs(
            Point2D center,
            Point2D majorAxisEndpoint,
            double axisRatio,
            double startParam,
            double endParam,
            string layer = "0",
            int color = 7,
            string? chainId = null)
        {
            var result = new List<EntityBase>();

            // If nearly circular, produce a single arc
            if (Math.Abs(axisRatio - 1.0) < 0.01)
            {
                double radius = majorAxisEndpoint.Length();
                double rotation = Math.Atan2(majorAxisEndpoint.Y, majorAxisEndpoint.X);

                double startDeg = ArcEntity.NormalizeAngle((startParam + rotation) * 180.0 / Math.PI);
                double endDeg = ArcEntity.NormalizeAngle((endParam + rotation) * 180.0 / Math.PI);

                bool isFullEllipse = Math.Abs(endParam - startParam - 2.0 * Math.PI) < 1e-6;
                if (isFullEllipse)
                {
                    result.Add(new CircleEntity(center, radius)
                    {
                        Layer = layer,
                        Color = color,
                        ChainId = chainId
                    });
                }
                else
                {
                    result.Add(new ArcEntity(center, radius, startDeg, endDeg)
                    {
                        Layer = layer,
                        Color = color,
                        ChainId = chainId
                    });
                }

                return result;
            }

            // Non-circular: decompose to lines
            var lines = ApproximateEllipse(center, majorAxisEndpoint, axisRatio,
                startParam, endParam, 1e-3, layer, color, chainId);
            result.AddRange(lines);
            return result;
        }

        /// <summary>
        /// Decomposes any entity into LINE/ARC/CIRCLE primitives.
        /// Passes through LINE, ARC, and CIRCLE unchanged.
        /// </summary>
        /// <param name="entity">The entity to decompose.</param>
        /// <returns>List of primitive entities.</returns>
        public static List<EntityBase> Decompose(EntityBase entity)
        {
            switch (entity)
            {
                case LineEntity line:
                    return new List<EntityBase> { line };

                case ArcEntity arc:
                    return new List<EntityBase> { arc };

                case CircleEntity circle:
                    return new List<EntityBase> { circle };

                case PointEntity point:
                    return new List<EntityBase> { point };

                case PolylineEntity polyline:
                    return DecomposePolyline(polyline);

                default:
                    // Unknown type — return as-is
                    return new List<EntityBase> { entity };
            }
        }

        /// <summary>
        /// Estimates the number of line segments needed to approximate a curve
        /// with the given chord tolerance.
        /// </summary>
        /// <param name="radius">Average radius of curvature.</param>
        /// <param name="startParam">Start parameter.</param>
        /// <param name="endParam">End parameter.</param>
        /// <param name="chordTolerance">Maximum chord deviation.</param>
        /// <returns>Estimated segment count.</returns>
        private static int EstimateSegmentCount(
            double radius, double startParam, double endParam, double chordTolerance)
        {
            if (radius < GeometryMath.Epsilon || chordTolerance < GeometryMath.Epsilon)
                return 64;

            double totalAngle = Math.Abs(endParam - startParam);

            // For a circle segment, chord error ≈ r(1 - cos(θ/2))
            // Solving for θ: θ = 2·arccos(1 - tol/r)
            double cosArg = 1.0 - chordTolerance / radius;
            cosArg = Math.Clamp(cosArg, -1.0, 1.0);
            double maxAnglePerSegment = 2.0 * Math.Acos(cosArg);

            if (maxAnglePerSegment < GeometryMath.Epsilon)
                return 256;

            return (int)Math.Ceiling(totalAngle / maxAnglePerSegment);
        }
    }
}
