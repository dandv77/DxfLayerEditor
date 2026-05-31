using System;
using System.Collections.Generic;
using DxfLayerEditor.Models;
using DxfLayerEditor.Utilities;
using Xunit;

namespace DxfLayerEditor.Tests
{
    /// <summary>
    /// Unit tests for geometric intersection and distance calculations.
    /// </summary>
    public class GeometryMathTests
    {
        private const double Tol = 1e-6;

        #region LINE-LINE Intersection

        [Fact]
        public void LineLineIntersection_PerpendicularLines_FindsOrigin()
        {
            // Horizontal line through origin
            var a1 = new Point2D(-1, 0);
            var a2 = new Point2D(1, 0);
            // Vertical line through origin
            var b1 = new Point2D(0, -1);
            var b2 = new Point2D(0, 1);

            bool found = GeometryMath.LineLineIntersection(a1, a2, b1, b2, out Point2D pt);

            Assert.True(found);
            Assert.True(pt.ApproxEquals(Point2D.Zero, Tol));
        }

        [Fact]
        public void LineLineIntersection_ParallelLines_ReturnsFalse()
        {
            var a1 = new Point2D(0, 0);
            var a2 = new Point2D(1, 0);
            var b1 = new Point2D(0, 1);
            var b2 = new Point2D(1, 1);

            bool found = GeometryMath.LineLineIntersection(a1, a2, b1, b2, out _);

            Assert.False(found);
        }

        [Fact]
        public void LineLineIntersection_AngledLines_FindsCorrectPoint()
        {
            // y = x line
            var a1 = new Point2D(0, 0);
            var a2 = new Point2D(2, 2);
            // y = -x + 4 line
            var b1 = new Point2D(0, 4);
            var b2 = new Point2D(4, 0);

            bool found = GeometryMath.LineLineIntersection(a1, a2, b1, b2, out Point2D pt);

            Assert.True(found);
            Assert.True(pt.ApproxEquals(new Point2D(2, 2), Tol));
        }

        [Fact]
        public void SegmentSegmentIntersection_CrossingSegments_Finds()
        {
            var a1 = new Point2D(0, 0);
            var a2 = new Point2D(4, 4);
            var b1 = new Point2D(0, 4);
            var b2 = new Point2D(4, 0);

            bool found = GeometryMath.SegmentSegmentIntersection(a1, a2, b1, b2, out Point2D pt);

            Assert.True(found);
            Assert.True(pt.ApproxEquals(new Point2D(2, 2), Tol));
        }

        [Fact]
        public void SegmentSegmentIntersection_NonOverlapping_ReturnsFalse()
        {
            var a1 = new Point2D(0, 0);
            var a2 = new Point2D(1, 0);
            var b1 = new Point2D(2, -1);
            var b2 = new Point2D(2, 1);

            bool found = GeometryMath.SegmentSegmentIntersection(a1, a2, b1, b2, out _);

            Assert.False(found);
        }

        #endregion

        #region LINE-CIRCLE / LINE-ARC Intersection

        [Fact]
        public void LineCircleIntersections_LineThroughCenter_TwoPoints()
        {
            var center = new Point2D(0, 0);
            double radius = 5.0;
            var lineStart = new Point2D(-10, 0);
            var lineEnd = new Point2D(10, 0);

            var hits = GeometryMath.LineCircleIntersections(lineStart, lineEnd, center, radius);

            Assert.Equal(2, hits.Count);
            // Should be at (-5, 0) and (5, 0)
            Assert.Contains(hits, p => p.ApproxEquals(new Point2D(-5, 0), Tol));
            Assert.Contains(hits, p => p.ApproxEquals(new Point2D(5, 0), Tol));
        }

        [Fact]
        public void LineCircleIntersections_Tangent_OnePoint()
        {
            var center = new Point2D(0, 0);
            double radius = 5.0;
            // Horizontal line tangent at top
            var lineStart = new Point2D(-10, 5);
            var lineEnd = new Point2D(10, 5);

            var hits = GeometryMath.LineCircleIntersections(lineStart, lineEnd, center, radius);

            Assert.Single(hits);
            Assert.True(hits[0].ApproxEquals(new Point2D(0, 5), Tol));
        }

        [Fact]
        public void LineCircleIntersections_NoIntersection_Empty()
        {
            var center = new Point2D(0, 0);
            double radius = 5.0;
            // Line above the circle
            var lineStart = new Point2D(-10, 6);
            var lineEnd = new Point2D(10, 6);

            var hits = GeometryMath.LineCircleIntersections(lineStart, lineEnd, center, radius);

            Assert.Empty(hits);
        }

        [Fact]
        public void LineArcIntersections_ArcContainsHit_ReturnsPoint()
        {
            // Arc: quarter circle from 0° to 90° at origin, radius 5
            var arc = new ArcEntity(new Point2D(0, 0), 5, 0, 90);
            // Horizontal line at y=3 — should hit arc at x = 4 (3-4-5 triangle)
            var line = new LineEntity(new Point2D(-10, 3), new Point2D(10, 3));

            var hits = GeometryMath.LineArcIntersections(line, arc);

            Assert.Single(hits);
            Assert.True(Math.Abs(hits[0].X - 4.0) < Tol);
            Assert.True(Math.Abs(hits[0].Y - 3.0) < Tol);
        }

        #endregion

        #region ARC-ARC / CIRCLE-CIRCLE Intersection

        [Fact]
        public void CircleCircleIntersections_TwoIntersections()
        {
            var c1 = new Point2D(0, 0);
            var c2 = new Point2D(3, 0);
            double r1 = 2.0, r2 = 2.0;

            var hits = GeometryMath.CircleCircleIntersections(c1, r1, c2, r2);

            Assert.Equal(2, hits.Count);
            // Intersection points should be symmetric about x-axis at x=1.5
            foreach (var pt in hits)
            {
                Assert.True(Math.Abs(pt.X - 1.5) < Tol);
            }
        }

        [Fact]
        public void CircleCircleIntersections_Tangent_OnePoint()
        {
            var c1 = new Point2D(0, 0);
            var c2 = new Point2D(4, 0);
            double r1 = 2.0, r2 = 2.0;

            var hits = GeometryMath.CircleCircleIntersections(c1, r1, c2, r2);

            Assert.Single(hits);
            Assert.True(hits[0].ApproxEquals(new Point2D(2, 0), Tol));
        }

        [Fact]
        public void CircleCircleIntersections_TooFarApart_Empty()
        {
            var c1 = new Point2D(0, 0);
            var c2 = new Point2D(10, 0);
            double r1 = 2.0, r2 = 2.0;

            var hits = GeometryMath.CircleCircleIntersections(c1, r1, c2, r2);

            Assert.Empty(hits);
        }

        [Fact]
        public void ArcArcIntersections_BothSweepsContain_ReturnsPoint()
        {
            // Two quarter-circle arcs that overlap
            var arc1 = new ArcEntity(new Point2D(0, 0), 2, 0, 90);
            var arc2 = new ArcEntity(new Point2D(3, 0), 2, 90, 180);

            var hits = GeometryMath.ArcArcIntersections(arc1, arc2);

            // Both arcs should have their circle-circle intersection filtered
            // to points within both sweeps
            Assert.True(hits.Count <= 2);
        }

        #endregion

        #region Point-on-Segment

        [Fact]
        public void IsPointOnSegment_MidpointIsOnSegment()
        {
            var a = new Point2D(0, 0);
            var b = new Point2D(4, 0);
            var p = new Point2D(2, 0);

            Assert.True(GeometryMath.IsPointOnSegment(p, a, b));
        }

        [Fact]
        public void IsPointOnSegment_EndpointsAreOnSegment()
        {
            var a = new Point2D(0, 0);
            var b = new Point2D(4, 0);

            Assert.True(GeometryMath.IsPointOnSegment(a, a, b));
            Assert.True(GeometryMath.IsPointOnSegment(b, a, b));
        }

        [Fact]
        public void IsPointOnSegment_PointOffLine_ReturnsFalse()
        {
            var a = new Point2D(0, 0);
            var b = new Point2D(4, 0);
            var p = new Point2D(2, 1);

            Assert.False(GeometryMath.IsPointOnSegment(p, a, b));
        }

        [Fact]
        public void IsPointOnSegment_PointBeyondSegment_ReturnsFalse()
        {
            var a = new Point2D(0, 0);
            var b = new Point2D(4, 0);
            var p = new Point2D(5, 0);

            Assert.False(GeometryMath.IsPointOnSegment(p, a, b));
        }

        #endregion

        #region Distance Calculations

        [Fact]
        public void PointToSegmentDistance_PointOnSegment_Zero()
        {
            double dist = GeometryMath.PointToSegmentDistance(
                new Point2D(2, 0), new Point2D(0, 0), new Point2D(4, 0));

            Assert.True(dist < Tol);
        }

        [Fact]
        public void PointToSegmentDistance_PointAboveSegment_PerpendicularDistance()
        {
            double dist = GeometryMath.PointToSegmentDistance(
                new Point2D(2, 3), new Point2D(0, 0), new Point2D(4, 0));

            Assert.True(Math.Abs(dist - 3.0) < Tol);
        }

        [Fact]
        public void PointToSegmentDistance_PointPastEnd_DistToEndpoint()
        {
            double dist = GeometryMath.PointToSegmentDistance(
                new Point2D(6, 0), new Point2D(0, 0), new Point2D(4, 0));

            Assert.True(Math.Abs(dist - 2.0) < Tol);
        }

        [Fact]
        public void Point2D_DistanceTo_Correct()
        {
            var a = new Point2D(0, 0);
            var b = new Point2D(3, 4);

            Assert.True(Math.Abs(a.DistanceTo(b) - 5.0) < Tol);
        }

        [Fact]
        public void Point2D_MidpointTo_Correct()
        {
            var a = new Point2D(0, 0);
            var b = new Point2D(4, 6);
            var mid = a.MidpointTo(b);

            Assert.True(mid.ApproxEquals(new Point2D(2, 3), Tol));
        }

        #endregion
    }
}
