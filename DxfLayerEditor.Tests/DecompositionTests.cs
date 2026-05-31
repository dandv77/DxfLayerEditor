using System;
using System.Collections.Generic;
using System.Linq;
using DxfLayerEditor.Models;
using DxfLayerEditor.Services;
using Xunit;

namespace DxfLayerEditor.Tests
{
    /// <summary>
    /// Unit tests for geometry decomposition (Polyline → LINE/ARC).
    /// </summary>
    public class DecompositionTests
    {
        [Fact]
        public void DecomposePolyline_AllStraight_ProducesLines()
        {
            var poly = new PolylineEntity
            {
                Vertices = new List<PolylineVertex>
                {
                    new(0, 0),
                    new(1, 0),
                    new(1, 1),
                    new(0, 1)
                },
                IsClosed = true
            };

            var primitives = GeometryDecomposer.DecomposePolyline(poly);

            Assert.Equal(4, primitives.Count);
            Assert.All(primitives, p => Assert.IsType<LineEntity>(p));
        }

        [Fact]
        public void DecomposePolyline_OpenPolyline_ProducesNMinus1Segments()
        {
            var poly = new PolylineEntity
            {
                Vertices = new List<PolylineVertex>
                {
                    new(0, 0),
                    new(1, 0),
                    new(2, 1)
                },
                IsClosed = false
            };

            var primitives = GeometryDecomposer.DecomposePolyline(poly);

            Assert.Equal(2, primitives.Count);
        }

        [Fact]
        public void DecomposePolyline_WithBulge_ProducesArc()
        {
            var poly = new PolylineEntity
            {
                Vertices = new List<PolylineVertex>
                {
                    new(0, 0, 1.0),  // bulge = 1 → semicircle
                    new(2, 0, 0)
                },
                IsClosed = false
            };

            var primitives = GeometryDecomposer.DecomposePolyline(poly);

            Assert.Single(primitives);
            Assert.IsType<ArcEntity>(primitives[0]);

            var arc = (ArcEntity)primitives[0];
            Assert.True(arc.Radius > 0);
        }

        [Fact]
        public void ApproximateSpline_FourPoints_ThreeLines()
        {
            var points = new List<Point2D>
            {
                new(0, 0), new(1, 1), new(2, 0), new(3, 1)
            };

            var lines = GeometryDecomposer.ApproximateSpline(points);

            Assert.Equal(3, lines.Count);
            Assert.True(lines[0].Start.ApproxEquals(new Point2D(0, 0)));
            Assert.True(lines[0].End.ApproxEquals(new Point2D(1, 1)));
        }

        [Fact]
        public void Decompose_Line_PassesThrough()
        {
            var line = new LineEntity(new Point2D(0, 0), new Point2D(1, 1));

            var result = GeometryDecomposer.Decompose(line);

            Assert.Single(result);
            Assert.Same(line, result[0]);
        }

        [Fact]
        public void Decompose_Circle_PassesThrough()
        {
            var circle = new CircleEntity(new Point2D(0, 0), 5);

            var result = GeometryDecomposer.Decompose(circle);

            Assert.Single(result);
            Assert.Same(circle, result[0]);
        }

        [Fact]
        public void ApproximateEllipse_ProducesMultipleLines()
        {
            var lines = GeometryDecomposer.ApproximateEllipse(
                center: new Point2D(0, 0),
                majorAxisEndpoint: new Point2D(5, 0),
                axisRatio: 0.5,
                startParam: 0,
                endParam: 2 * Math.PI);

            Assert.True(lines.Count >= 8);

            // First and last points should be close (full ellipse)
            var firstStart = lines[0].Start;
            var lastEnd = lines[^1].End;
            Assert.True(firstStart.DistanceTo(lastEnd) < 0.01);
        }

        [Fact]
        public void BulgeToArc_UnitBulge_Semicircle()
        {
            var p1 = new Point2D(0, 0);
            var p2 = new Point2D(2, 0);
            double bulge = 1.0; // tan(90°/4) = tan(22.5°) ≠ 1; bulge=1 → sweep=180°

            var (center, radius, startAngle, endAngle) =
                PolylineEntity.BulgeToArc(p1, p2, bulge);

            // For bulge=1 (semicircle), center should be at midpoint (1,0) offset by radius
            Assert.True(radius > 0);
            // Sweep should be ~180°
            double sweep = endAngle - startAngle;
            if (sweep < 0) sweep += 360;
            Assert.True(Math.Abs(sweep - 180) < 1.0 || Math.Abs(sweep - 180) > 359);
        }
    }
}
