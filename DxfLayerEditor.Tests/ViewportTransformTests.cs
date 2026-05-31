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
    }
}
