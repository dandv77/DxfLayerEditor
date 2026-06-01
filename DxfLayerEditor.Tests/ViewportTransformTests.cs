using System;
using DxfLayerEditor.Models;
using DxfLayerEditor.Utilities;
using Xunit;

namespace DxfLayerEditor.Tests
{
    /// <summary>
    /// Unit tests for viewport coordinate transformations.
    /// </summary>
    public class ViewportTransformTests
    {
        private const double Tol = 1e-6;

        [Fact]
        public void WorldToScreen_Identity_FlipsY()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(0, 0, 800, 600, 0); // scale=1, centered

            // After fit, origin should map to bottom-left of viewport
            var origin = vt.WorldToScreen(new Point2D(0, 0));
            // y should be at viewport height (bottom in screen coords)
            Assert.True(origin.Y > 500); // Near bottom
        }

        [Fact]
        public void WorldToScreen_ScreenToWorld_Roundtrip()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(-10, -10, 10, 10, 0.05);

            var worldPt = new Point2D(3.5, -2.7);
            var screenPt = vt.WorldToScreen(worldPt);
            var roundtrip = vt.ScreenToWorld(screenPt);

            Assert.True(Math.Abs(roundtrip.X - worldPt.X) < Tol);
            Assert.True(Math.Abs(roundtrip.Y - worldPt.Y) < Tol);
        }

        [Fact]
        public void FitToBounds_CentersContent()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(0, 0, 100, 100, 0);

            // Center of world bounds (50, 50) should map to center of screen (400, 300)
            var center = vt.WorldToScreen(new Point2D(50, 50));
            Assert.True(Math.Abs(center.X - 400) < 1.0);
            Assert.True(Math.Abs(center.Y - 300) < 1.0);
        }

        [Fact]
        public void Pan_ShiftsCoordinates()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(0, 0, 100, 100, 0);

            var before = vt.WorldToScreen(new Point2D(50, 50));
            vt.Pan(10, 0);
            var after = vt.WorldToScreen(new Point2D(50, 50));

            Assert.True(Math.Abs(after.X - before.X - 10) < Tol);
        }

        [Fact]
        public void PickTolerance_InversePropToScale()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(0, 0, 100, 100, 0); // scale = 6

            double worldTol = vt.PickTolerance(5);

            // At scale=6, 5 pixels = 5/6 ≈ 0.833 world units
            Assert.True(worldTol > 0);
            Assert.True(worldTol < 5); // Must be less than screen pixels
        }

        [Fact]
        public void WorldToScreenDistance_ScalesCorrectly()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(0, 0, 100, 100, 0);

            double screenDist = vt.WorldToScreenDistance(10);
            double worldDist = vt.ScreenToWorldDistance(screenDist);

            Assert.True(Math.Abs(worldDist - 10) < Tol);
        }

        [Fact]
        public void Zoom_PreservesPivotPoint()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(0, 0, 100, 100, 0);

            // Pick a screen point (e.g., the center)
            double pivotSX = 400, pivotSY = 300;

            // Get world point at pivot before zoom
            var worldPivot = vt.ScreenToWorld(new Point2D(pivotSX, pivotSY));

            // Zoom in 2x at pivot
            vt.Zoom(2.0, pivotSX, pivotSY);

            // World point should still map to same screen position
            var afterScreen = vt.WorldToScreen(worldPivot);
            Assert.True(Math.Abs(afterScreen.X - pivotSX) < Tol,
                $"X: expected {pivotSX}, got {afterScreen.X}");
            Assert.True(Math.Abs(afterScreen.Y - pivotSY) < Tol,
                $"Y: expected {pivotSY}, got {afterScreen.Y}");
        }

        [Fact]
        public void Zoom_PreservesPivotPoint_OffCenter()
        {
            var vt = new ViewportTransform(800, 600);
            vt.FitToBounds(-20, -10, 80, 50, 0.05);

            // Pick an off-center screen point
            double pivotSX = 200, pivotSY = 150;

            var worldPivot = vt.ScreenToWorld(new Point2D(pivotSX, pivotSY));

            // Zoom out 0.5x at pivot
            vt.Zoom(0.5, pivotSX, pivotSY);

            var afterScreen = vt.WorldToScreen(worldPivot);
            Assert.True(Math.Abs(afterScreen.X - pivotSX) < Tol,
                $"X: expected {pivotSX}, got {afterScreen.X}");
            Assert.True(Math.Abs(afterScreen.Y - pivotSY) < Tol,
                $"Y: expected {pivotSY}, got {afterScreen.Y}");
        }

        [Fact]
        public void Zoom_MultipleSteps_PreservesPivot()
        {
            var vt = new ViewportTransform(1024, 768);
            vt.FitToBounds(0, 0, 50, 30, 0.08);

            double pivotSX = 600, pivotSY = 400;
            var worldPivot = vt.ScreenToWorld(new Point2D(pivotSX, pivotSY));

            // Simulate 10 scroll-zoom steps
            for (int i = 0; i < 10; i++)
            {
                vt.Zoom(1.15, pivotSX, pivotSY);
            }

            var afterScreen = vt.WorldToScreen(worldPivot);
            Assert.True(Math.Abs(afterScreen.X - pivotSX) < 0.01,
                $"X after 10 zooms: expected {pivotSX}, got {afterScreen.X}");
            Assert.True(Math.Abs(afterScreen.Y - pivotSY) < 0.01,
                $"Y after 10 zooms: expected {pivotSY}, got {afterScreen.Y}");
        }
    }
}
