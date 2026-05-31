using System;
using DxfLayerEditor.Models;

namespace DxfLayerEditor.Utilities
{
    /// <summary>
    /// Handles world-to-screen and screen-to-world coordinate transformations
    /// for the 2D canvas viewport. Supports pan, zoom, and fit-to-bounds operations.
    /// </summary>
    public class ViewportTransform
    {
        /// <summary>Current zoom scale factor (pixels per world unit).</summary>
        public double Scale { get; private set; } = 1.0;

        /// <summary>Pan offset in screen coordinates (pixels).</summary>
        public double OffsetX { get; private set; } = 0.0;

        /// <summary>Pan offset in screen coordinates (pixels).</summary>
        public double OffsetY { get; private set; } = 0.0;

        /// <summary>Width of the viewport in pixels.</summary>
        public double ViewportWidth { get; set; } = 800;

        /// <summary>Height of the viewport in pixels.</summary>
        public double ViewportHeight { get; set; } = 600;

        /// <summary>Minimum allowed zoom scale.</summary>
        public double MinScale { get; set; } = 0.001;

        /// <summary>Maximum allowed zoom scale.</summary>
        public double MaxScale { get; set; } = 100000.0;

        /// <summary>
        /// Initializes a new viewport transform with default settings.
        /// </summary>
        public ViewportTransform() { }

        /// <summary>
        /// Initializes a viewport transform with specified viewport size.
        /// </summary>
        /// <param name="viewportWidth">Viewport width in pixels.</param>
        /// <param name="viewportHeight">Viewport height in pixels.</param>
        public ViewportTransform(double viewportWidth, double viewportHeight)
        {
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }

        #region World ↔ Screen Conversions

        /// <summary>
        /// Transforms a world-space point to screen-space (pixel) coordinates.
        /// Y-axis is flipped (screen Y increases downward, world Y increases upward).
        /// </summary>
        /// <param name="world">World-space point.</param>
        /// <returns>Screen-space point in pixels.</returns>
        public Point2D WorldToScreen(Point2D world)
        {
            double sx = world.X * Scale + OffsetX;
            double sy = ViewportHeight - (world.Y * Scale + OffsetY);
            return new Point2D(sx, sy);
        }

        /// <summary>
        /// Transforms a screen-space (pixel) point to world-space coordinates.
        /// Inverse of <see cref="WorldToScreen"/>.
        /// </summary>
        /// <param name="screen">Screen-space point in pixels.</param>
        /// <returns>World-space point.</returns>
        public Point2D ScreenToWorld(Point2D screen)
        {
            double wx = (screen.X - OffsetX) / Scale;
            double wy = (ViewportHeight - screen.Y - OffsetY) / Scale;

            // Clamp to avoid NaN/Infinity from zero scale
            if (double.IsNaN(wx) || double.IsInfinity(wx)) wx = 0;
            if (double.IsNaN(wy) || double.IsInfinity(wy)) wy = 0;

            return new Point2D(wx, wy);
        }

        /// <summary>
        /// Transforms a world-space distance to screen-space pixels.
        /// </summary>
        public double WorldToScreenDistance(double worldDistance)
        {
            return worldDistance * Scale;
        }

        /// <summary>
        /// Transforms a screen-space pixel distance to world-space units.
        /// </summary>
        public double ScreenToWorldDistance(double screenDistance)
        {
            return Scale > 0 ? screenDistance / Scale : 0;
        }

        #endregion

        #region Pan

        /// <summary>
        /// Pans the viewport by the given screen-space pixel delta.
        /// </summary>
        /// <param name="deltaScreenX">Horizontal pan in pixels.</param>
        /// <param name="deltaScreenY">Vertical pan in pixels (positive = pan up in world).</param>
        public void Pan(double deltaScreenX, double deltaScreenY)
        {
            OffsetX += deltaScreenX;
            OffsetY += deltaScreenY;
        }

        /// <summary>
        /// Pans to center the viewport on a specific world-space point.
        /// </summary>
        /// <param name="worldPoint">The point to center on.</param>
        public void CenterOn(Point2D worldPoint)
        {
            OffsetX = ViewportWidth / 2.0 - worldPoint.X * Scale;
            OffsetY = worldPoint.Y * Scale - ViewportHeight / 2.0 + ViewportHeight;
        }

        #endregion

        #region Zoom

        /// <summary>
        /// Zooms the viewport by a factor, centered on a screen-space point.
        /// The world point under the cursor stays fixed.
        /// </summary>
        /// <param name="factor">Zoom factor (>1 = zoom in, &lt;1 = zoom out).</param>
        /// <param name="screenPivotX">Screen X coordinate of the zoom center.</param>
        /// <param name="screenPivotY">Screen Y coordinate of the zoom center.</param>
        public void Zoom(double factor, double screenPivotX, double screenPivotY)
        {
            // Convert pivot to world space before zoom
            var worldPivot = ScreenToWorld(new Point2D(screenPivotX, screenPivotY));

            double newScale = Scale * factor;
            newScale = Math.Clamp(newScale, MinScale, MaxScale);

            Scale = newScale;

            // Recompute offset so worldPivot maps back to the same screen position
            OffsetX = screenPivotX - worldPivot.X * Scale;
            OffsetY = (ViewportHeight - screenPivotY) - worldPivot.Y * Scale + ViewportHeight - (ViewportHeight - screenPivotY);
            // Simplified:
            OffsetX = screenPivotX - worldPivot.X * Scale;
            OffsetY = worldPivot.Y * Scale - (ViewportHeight - screenPivotY) + ViewportHeight - (ViewportHeight - screenPivotY);

            // Correct formula preserving pivot invariant:
            // WorldToScreen(worldPivot) should equal (screenPivotX, screenPivotY)
            // sx = wx * Scale + OffsetX  =>  OffsetX = sx - wx * Scale
            // sy = H - (wy * Scale + OffsetY)  =>  OffsetY = wy * Scale - (H - sy)
            //    = wy * Scale - H + sy
            OffsetX = screenPivotX - worldPivot.X * Scale;
            OffsetY = worldPivot.Y * Scale - ViewportHeight + screenPivotY;
        }

        /// <summary>
        /// Zooms using mouse scroll increments.
        /// </summary>
        /// <param name="scrollDelta">Scroll wheel delta (positive = zoom in).</param>
        /// <param name="screenX">Screen X of mouse position.</param>
        /// <param name="screenY">Screen Y of mouse position.</param>
        /// <param name="zoomSpeed">Zoom sensitivity multiplier (default 1.1).</param>
        public void ZoomScroll(int scrollDelta, double screenX, double screenY, double zoomSpeed = 1.1)
        {
            double factor = scrollDelta > 0 ? zoomSpeed : 1.0 / zoomSpeed;
            Zoom(factor, screenX, screenY);
        }

        #endregion

        #region Fit to Bounds

        /// <summary>
        /// Adjusts scale and offset to fit the given world-space bounding box
        /// within the viewport, with optional padding.
        /// </summary>
        /// <param name="minX">Minimum X of the bounding box.</param>
        /// <param name="minY">Minimum Y of the bounding box.</param>
        /// <param name="maxX">Maximum X of the bounding box.</param>
        /// <param name="maxY">Maximum Y of the bounding box.</param>
        /// <param name="paddingPercent">Padding as a fraction of viewport (0.05 = 5%).</param>
        public void FitToBounds(double minX, double minY, double maxX, double maxY, double paddingPercent = 0.05)
        {
            double worldWidth = maxX - minX;
            double worldHeight = maxY - minY;

            if (worldWidth < GeometryMath.Epsilon || worldHeight < GeometryMath.Epsilon)
            {
                // Degenerate bounds — center on the point
                Scale = 1.0;
                CenterOn(new Point2D((minX + maxX) / 2.0, (minY + maxY) / 2.0));
                return;
            }

            double padW = ViewportWidth * paddingPercent;
            double padH = ViewportHeight * paddingPercent;
            double availW = ViewportWidth - 2 * padW;
            double availH = ViewportHeight - 2 * padH;

            double scaleX = availW / worldWidth;
            double scaleY = availH / worldHeight;
            Scale = Math.Min(scaleX, scaleY);
            Scale = Math.Clamp(Scale, MinScale, MaxScale);

            // Center the bounding box
            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;

            OffsetX = ViewportWidth / 2.0 - centerX * Scale;
            OffsetY = centerY * Scale - ViewportHeight / 2.0 + ViewportHeight;
            // Correction: we need WorldToScreen(center) = (VP/2, VP/2)
            // sx = cx * S + Ox => Ox = VPw/2 - cx*S
            // sy = H - (cy * S + Oy) => Oy = cy*S - H + H/2 = cy*S - H/2
            OffsetX = ViewportWidth / 2.0 - centerX * Scale;
            OffsetY = centerY * Scale - ViewportHeight / 2.0;
        }

        /// <summary>
        /// Fits the viewport to show all entities.
        /// </summary>
        /// <param name="entities">The entities to fit.</param>
        /// <param name="paddingPercent">Padding as a fraction of viewport.</param>
        public void FitToEntities(System.Collections.Generic.IEnumerable<EntityBase> entities, double paddingPercent = 0.05)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            bool hasEntities = false;

            foreach (var entity in entities)
            {
                hasEntities = true;
                var (eMinX, eMinY, eMaxX, eMaxY) = entity.GetBounds();
                minX = Math.Min(minX, eMinX);
                minY = Math.Min(minY, eMinY);
                maxX = Math.Max(maxX, eMaxX);
                maxY = Math.Max(maxY, eMaxY);
            }

            if (!hasEntities) return;

            FitToBounds(minX, minY, maxX, maxY, paddingPercent);
        }

        #endregion

        #region Hit Testing

        /// <summary>
        /// Returns the pick tolerance in world units for a given screen pixel tolerance.
        /// Useful for hit-testing: entities within this world-distance from the cursor are "hit".
        /// </summary>
        /// <param name="screenPixels">Pick tolerance in screen pixels (e.g., 5).</param>
        /// <returns>Equivalent distance in world coordinates.</returns>
        public double PickTolerance(double screenPixels = 5.0)
        {
            return ScreenToWorldDistance(screenPixels);
        }

        #endregion

        /// <summary>
        /// Resets the transform to identity (no pan, no zoom).
        /// </summary>
        public void Reset()
        {
            Scale = 1.0;
            OffsetX = 0;
            OffsetY = 0;
        }
    }
}
