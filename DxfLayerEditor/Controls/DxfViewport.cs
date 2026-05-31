using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using DxfLayerEditor.Models;
using DxfLayerEditor.Services;
using DxfLayerEditor.Utilities;

namespace DxfLayerEditor.Controls
{
    /// <summary>
    /// Custom viewport control for rendering DXF entities with GDI+.
    /// Supports pan, zoom, entity selection, hover highlighting,
    /// snap candidate display, and gap marker overlays.
    /// </summary>
    public class DxfViewport : Panel
    {
        #region Fields

        private readonly ViewportTransform _transform = new();
        private EditorState? _state;

        // Pan state
        private bool _isPanning;
        private Point _panStart;
        private double _panStartOffsetX, _panStartOffsetY;

        // Selection box
        private bool _isBoxSelecting;
        private Point _boxStart, _boxEnd;

        // Rendering
        private readonly Color _backgroundColor = Color.FromArgb(30, 30, 30);
        private readonly Color _gridColor = Color.FromArgb(50, 50, 50);
        private readonly Color _gridMajorColor = Color.FromArgb(65, 65, 65);
        private readonly Color _selectionColor = Color.FromArgb(0, 180, 255);
        private readonly Color _hoverColor = Color.FromArgb(255, 200, 0);
        private readonly Color _gapMarkerColor = Color.FromArgb(255, 255, 0);
        private readonly Color _snapCandidateColor = Color.FromArgb(0, 255, 128);
        private readonly Color _rulerBackColor = Color.FromArgb(45, 45, 45);
        private readonly Color _rulerTextColor = Color.FromArgb(160, 160, 160);
        private const int RulerSize = 24;
        private const float EntityLineWidth = 1.2f;
        private const float SelectedLineWidth = 2.5f;
        private const float HoverLineWidth = 2.0f;

        // Hit-test cache
        private string? _lastHitEntityId;

        // Snap candidates for display
        private List<SnapCandidate>? _activeSnapCandidates;
        private SnapCandidate? _nearestSnap;

        // Mouse world position
        private Point2D _mouseWorldPos;

        #endregion

        #region Events

        /// <summary>Raised when the mouse world position changes.</summary>
        public event EventHandler<Point2D>? WorldPositionChanged;

        /// <summary>Raised when the zoom level changes.</summary>
        public event EventHandler<double>? ZoomChanged;

        /// <summary>Raised when an entity is clicked.</summary>
        public event EventHandler<string>? EntityClicked;

        /// <summary>Raised when a snap candidate is clicked during move mode.</summary>
        public event EventHandler<SnapCandidate>? SnapCandidateClicked;

        #endregion

        #region Constructor

        public DxfViewport()
        {
            // Enable double buffering for flicker-free rendering
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            BackColor = _backgroundColor;
            Cursor = Cursors.Cross;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Binds this viewport to an editor state instance.
        /// Subscribes to state change events for automatic repaint.
        /// </summary>
        public void BindState(EditorState state)
        {
            if (_state != null)
            {
                _state.EntitiesChanged -= OnRepaintNeeded;
                _state.SelectionChanged -= OnRepaintNeeded;
                _state.LayersChanged -= OnRepaintNeeded;
                _state.FileChanged -= OnFileChanged;
                _state.ToolChanged -= OnRepaintNeeded;
                _state.StateChanged -= OnStateChanged;
            }

            _state = state;
            _state.EntitiesChanged += OnRepaintNeeded;
            _state.SelectionChanged += OnRepaintNeeded;
            _state.LayersChanged += OnRepaintNeeded;
            _state.FileChanged += OnFileChanged;
            _state.ToolChanged += OnRepaintNeeded;
            _state.StateChanged += OnStateChanged;
        }

        /// <summary>Fits the viewport to show all entities.</summary>
        public void ZoomToFit()
        {
            if (_state == null || _state.Entities.Count == 0) return;

            _transform.ViewportWidth = Width - RulerSize;
            _transform.ViewportHeight = Height - RulerSize;
            _transform.FitToBounds(
                _state.Bounds.MinX, _state.Bounds.MinY,
                _state.Bounds.MaxX, _state.Bounds.MaxY, 0.08);

            ZoomChanged?.Invoke(this, _transform.Scale);
            Invalidate();
        }

        /// <summary>Zooms in by a fixed factor.</summary>
        public void ZoomIn()
        {
            _transform.Zoom(1.3, Width / 2.0, Height / 2.0);
            ZoomChanged?.Invoke(this, _transform.Scale);
            Invalidate();
        }

        /// <summary>Zooms out by a fixed factor.</summary>
        public void ZoomOut()
        {
            _transform.Zoom(1 / 1.3, Width / 2.0, Height / 2.0);
            ZoomChanged?.Invoke(this, _transform.Scale);
            Invalidate();
        }

        /// <summary>Current zoom scale.</summary>
        public double CurrentZoom => _transform.Scale;

        /// <summary>Current mouse world position.</summary>
        public Point2D MouseWorldPosition => _mouseWorldPos;

        /// <summary>Sets the snap candidates to display.</summary>
        public void SetSnapCandidates(List<SnapCandidate>? candidates)
        {
            _activeSnapCandidates = candidates;
            Invalidate();
        }

        /// <summary>Pans to center on a world-space point.</summary>
        public void PanTo(double worldX, double worldY)
        {
            _transform.CenterOn(new Point2D(worldX, worldY));
            Invalidate();
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(_backgroundColor);

            _transform.ViewportWidth = Width - RulerSize;
            _transform.ViewportHeight = Height - RulerSize;

            // Offset rendering to account for rulers
            g.TranslateTransform(RulerSize, 0);

            DrawGrid(g);
            DrawEntities(g);
            DrawSnapCandidates(g);
            DrawGapMarkers(g);
            DrawSelectionBox(g);

            // Reset transform for rulers
            g.ResetTransform();
            DrawRulers(g);

            // Origin crosshair
            g.TranslateTransform(RulerSize, 0);
            DrawOriginCrosshair(g);
            g.ResetTransform();
        }

        private void DrawGrid(Graphics g)
        {
            // Calculate grid spacing based on zoom level
            double worldWidth = _transform.ScreenToWorldDistance(Width);
            double gridStep = CalculateGridStep(worldWidth);
            double majorStep = gridStep * 5;

            // Get visible world bounds
            var topLeft = _transform.ScreenToWorld(new Point2D(0, 0));
            var bottomRight = _transform.ScreenToWorld(new Point2D(Width - RulerSize, Height));

            double startX = Math.Floor(topLeft.X / gridStep) * gridStep;
            double startY = Math.Floor(Math.Min(topLeft.Y, bottomRight.Y) / gridStep) * gridStep;
            double endX = Math.Ceiling(bottomRight.X / gridStep) * gridStep;
            double endY = Math.Ceiling(Math.Max(topLeft.Y, bottomRight.Y) / gridStep) * gridStep;

            using var gridPen = new Pen(_gridColor, 0.5f);
            using var majorPen = new Pen(_gridMajorColor, 0.5f);

            for (double x = startX; x <= endX; x += gridStep)
            {
                bool isMajor = Math.Abs(x % majorStep) < gridStep * 0.1;
                var s1 = WorldToScreen(new Point2D(x, startY));
                var s2 = WorldToScreen(new Point2D(x, endY));
                g.DrawLine(isMajor ? majorPen : gridPen, s1, s2);
            }

            for (double y = startY; y <= endY; y += gridStep)
            {
                bool isMajor = Math.Abs(y % majorStep) < gridStep * 0.1;
                var s1 = WorldToScreen(new Point2D(startX, y));
                var s2 = WorldToScreen(new Point2D(endX, y));
                g.DrawLine(isMajor ? majorPen : gridPen, s1, s2);
            }
        }

        private void DrawEntities(Graphics g)
        {
            if (_state == null) return;

            foreach (var entity in _state.Entities)
            {
                if (!_state.IsEntityVisible(entity))
                    continue;

                bool isSelected = _state.SelectedIds.Contains(entity.Id);
                bool isHovered = entity.Id == _state.HoveredEntityId;

                Color entityColor = GetEntityDisplayColor(entity);
                float lineWidth = EntityLineWidth;

                if (isSelected)
                {
                    entityColor = _selectionColor;
                    lineWidth = SelectedLineWidth;
                }
                else if (isHovered)
                {
                    entityColor = _hoverColor;
                    lineWidth = HoverLineWidth;
                }

                using var pen = new Pen(entityColor, lineWidth);

                DrawEntity(g, entity, pen);
            }
        }

        private void DrawEntity(Graphics g, EntityBase entity, Pen pen)
        {
            switch (entity)
            {
                case LineEntity line:
                    DrawLine(g, line, pen);
                    break;
                case ArcEntity arc:
                    DrawArc(g, arc, pen);
                    break;
                case CircleEntity circle:
                    DrawCircle(g, circle, pen);
                    break;
                case PointEntity point:
                    DrawPoint(g, point, pen);
                    break;
                case PolylineEntity polyline:
                    DrawPolyline(g, polyline, pen);
                    break;
            }
        }

        private void DrawLine(Graphics g, LineEntity line, Pen pen)
        {
            var s1 = WorldToScreen(line.Start);
            var s2 = WorldToScreen(line.End);
            g.DrawLine(pen, s1, s2);
        }

        private void DrawArc(Graphics g, ArcEntity arc, Pen pen)
        {
            var center = _transform.WorldToScreen(arc.Center);
            double screenRadius = _transform.WorldToScreenDistance(arc.Radius);

            if (screenRadius < 0.5) return;

            float x = (float)(center.X - screenRadius);
            float y = (float)(center.Y - screenRadius);
            float size = (float)(screenRadius * 2);

            // GDI+ arcs: angles are measured CW from +X, but we need CCW.
            // GDI+ uses CW angles (positive = CW), so we negate Y and adjust.
            float startAngle = (float)(-arc.StartAngle);
            float sweepAngle = (float)(-arc.SweepAngle);

            try
            {
                g.DrawArc(pen, x, y, size, size, startAngle, sweepAngle);
            }
            catch
            {
                // Fallback: draw as line segments
                DrawArcAsSegments(g, arc, pen);
            }
        }

        private void DrawArcAsSegments(Graphics g, ArcEntity arc, Pen pen)
        {
            int segments = Math.Max(16, (int)(arc.SweepAngle / 5));
            double step = arc.SweepAngle / segments;

            var prev = WorldToScreen(arc.PointAtAngle(arc.StartAngle));
            for (int i = 1; i <= segments; i++)
            {
                double angle = arc.StartAngle + i * step;
                var curr = WorldToScreen(arc.PointAtAngle(angle));
                g.DrawLine(pen, prev, curr);
                prev = curr;
            }
        }

        private void DrawCircle(Graphics g, CircleEntity circle, Pen pen)
        {
            var center = _transform.WorldToScreen(circle.Center);
            double screenRadius = _transform.WorldToScreenDistance(circle.Radius);

            if (screenRadius < 0.5) return;

            float x = (float)(center.X - screenRadius);
            float y = (float)(center.Y - screenRadius);
            float size = (float)(screenRadius * 2);

            g.DrawEllipse(pen, x, y, size, size);
        }

        private void DrawPoint(Graphics g, PointEntity point, Pen pen)
        {
            var sp = WorldToScreen(point.Position);
            float s = 4;
            g.DrawLine(pen, sp.X - s, sp.Y - s, sp.X + s, sp.Y + s);
            g.DrawLine(pen, sp.X - s, sp.Y + s, sp.X + s, sp.Y - s);
        }

        private void DrawPolyline(Graphics g, PolylineEntity poly, Pen pen)
        {
            var decomposed = GeometryDecomposer.DecomposePolyline(poly);
            foreach (var prim in decomposed)
            {
                DrawEntity(g, prim, pen);
            }
        }

        private void DrawSnapCandidates(Graphics g)
        {
            if (_activeSnapCandidates == null) return;

            using var brush = new SolidBrush(_snapCandidateColor);
            using var nearBrush = new SolidBrush(Color.FromArgb(255, 100, 0));

            foreach (var candidate in _activeSnapCandidates)
            {
                var sp = WorldToScreen(candidate.Position);
                float size = candidate == _nearestSnap ? 8 : 5;
                var b = candidate == _nearestSnap ? nearBrush : brush;
                g.FillEllipse(b, sp.X - size / 2, sp.Y - size / 2, size, size);

                if (candidate == _nearestSnap)
                {
                    using var outPen = new Pen(Color.White, 1.5f);
                    g.DrawEllipse(outPen, sp.X - size / 2, sp.Y - size / 2, size, size);
                }
            }
        }

        private void DrawGapMarkers(Graphics g)
        {
            if (_state?.LastExportResult?.OpenGaps == null) return;

            using var pen = new Pen(_gapMarkerColor, 2f)
            {
                DashStyle = DashStyle.Dash
            };

            foreach (var gap in _state.LastExportResult.OpenGaps)
            {
                var sp = WorldToScreen(new Point2D(gap.X, gap.Y));
                float radius = 8;
                g.DrawEllipse(pen, sp.X - radius, sp.Y - radius, radius * 2, radius * 2);
            }
        }

        private void DrawSelectionBox(Graphics g)
        {
            if (!_isBoxSelecting) return;

            int x = Math.Min(_boxStart.X, _boxEnd.X);
            int y = Math.Min(_boxStart.Y, _boxEnd.Y);
            int w = Math.Abs(_boxEnd.X - _boxStart.X);
            int h = Math.Abs(_boxEnd.Y - _boxStart.Y);

            using var pen = new Pen(Color.FromArgb(150, _selectionColor), 1f)
            {
                DashStyle = DashStyle.Dash
            };
            using var brush = new SolidBrush(Color.FromArgb(30, _selectionColor));

            g.FillRectangle(brush, x, y, w, h);
            g.DrawRectangle(pen, x, y, w, h);
        }

        private void DrawOriginCrosshair(Graphics g)
        {
            var origin = _transform.WorldToScreen(Point2D.Zero);
            using var pen = new Pen(Color.FromArgb(80, 255, 80, 80), 0.5f);
            g.DrawLine(pen, (float)origin.X, 0, (float)origin.X, Height);
            g.DrawLine(pen, 0, (float)origin.Y, Width, (float)origin.Y);
        }

        private void DrawRulers(Graphics g)
        {
            using var bgBrush = new SolidBrush(_rulerBackColor);
            using var textBrush = new SolidBrush(_rulerTextColor);
            using var linePen = new Pen(Color.FromArgb(80, 80, 80), 0.5f);
            using var font = new Font("Segoe UI", 7f);

            // Top ruler
            g.FillRectangle(bgBrush, RulerSize, 0, Width - RulerSize, RulerSize);
            // Left ruler
            g.FillRectangle(bgBrush, 0, 0, RulerSize, Height);

            // Corner square
            g.FillRectangle(new SolidBrush(Color.FromArgb(38, 38, 38)), 0, 0, RulerSize, RulerSize);

            // Calculate ruler markings based on zoom
            double worldWidth = _transform.ScreenToWorldDistance(Width);
            double step = CalculateGridStep(worldWidth);

            var topLeft = _transform.ScreenToWorld(new Point2D(0, 0));
            var bottomRight = _transform.ScreenToWorld(new Point2D(Width - RulerSize, Height));

            // Horizontal ruler markings
            double startX = Math.Floor(topLeft.X / step) * step;
            for (double x = startX; x <= bottomRight.X; x += step)
            {
                var sp = _transform.WorldToScreen(new Point2D(x, 0));
                float sx = (float)sp.X + RulerSize;
                g.DrawLine(linePen, sx, RulerSize - 6, sx, RulerSize);
                string label = FormatRulerValue(x);
                var sz = g.MeasureString(label, font);
                g.DrawString(label, font, textBrush, sx - sz.Width / 2, 2);
            }

            // Vertical ruler markings
            double startY = Math.Floor(Math.Min(topLeft.Y, bottomRight.Y) / step) * step;
            double endY = Math.Max(topLeft.Y, bottomRight.Y);
            for (double y = startY; y <= endY; y += step)
            {
                var sp = _transform.WorldToScreen(new Point2D(0, y));
                float sy = (float)sp.Y;
                g.DrawLine(linePen, RulerSize - 6, sy, RulerSize, sy);
                string label = FormatRulerValue(y);
                var sz = g.MeasureString(label, font);
                g.DrawString(label, font, textBrush, 1, sy - sz.Height / 2);
            }
        }

        #endregion

        #region Mouse Handling

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            var adjustedPos = new Point(e.X - RulerSize, e.Y);

            if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Control) &&
                 _state?.ActiveTool == ToolMode.Select))
            {
                // Start pan
                _isPanning = true;
                _panStart = e.Location;
                _panStartOffsetX = _transform.OffsetX;
                _panStartOffsetY = _transform.OffsetY;
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left && _state != null)
            {
                var worldPt = _transform.ScreenToWorld(new Point2D(adjustedPos.X, adjustedPos.Y));

                switch (_state.ActiveTool)
                {
                    case ToolMode.Select:
                        HandleSelectClick(worldPt, e);
                        break;
                    case ToolMode.Move:
                        HandleMoveClick(worldPt);
                        break;
                    case ToolMode.Trim:
                        HandleTrimClick(worldPt);
                        break;
                    case ToolMode.PointToCircle:
                        HandleSelectClick(worldPt, e);
                        break;
                    case ToolMode.Snap:
                        HandleSnapClick(worldPt);
                        break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var adjustedPos = new Point(e.X - RulerSize, e.Y);
            _mouseWorldPos = _transform.ScreenToWorld(new Point2D(adjustedPos.X, adjustedPos.Y));
            WorldPositionChanged?.Invoke(this, _mouseWorldPos);

            if (_isPanning)
            {
                double dx = e.X - _panStart.X;
                double dy = -(e.Y - _panStart.Y);
                _transform.OffsetX = _panStartOffsetX + dx;
                _transform.OffsetY = _panStartOffsetY + dy;
                Invalidate();
                return;
            }

            if (_isBoxSelecting)
            {
                _boxEnd = adjustedPos;
                Invalidate();
                return;
            }

            // Hit-test for hover
            if (_state != null)
            {
                string? hitId = HitTest(_mouseWorldPos);
                if (hitId != _lastHitEntityId)
                {
                    _lastHitEntityId = hitId;
                    _state.SetHovered(hitId);
                    Invalidate();
                }

                // Update snap candidates during move mode
                if (_state.ActiveTool == ToolMode.Move &&
                    _state.MoveState.Phase == MovePhase.PickTarget)
                {
                    UpdateSnapCandidates(_mouseWorldPos);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isPanning)
            {
                _isPanning = false;
                UpdateCursorForTool();
                return;
            }

            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                PerformBoxSelection();
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
            _transform.Zoom(factor, e.X - RulerSize, e.Y);
            ZoomChanged?.Invoke(this, _transform.Scale);
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (e.Button == MouseButtons.Left && _state != null)
            {
                var adjustedPos = new Point(e.X - RulerSize, e.Y);
                var worldPt = _transform.ScreenToWorld(new Point2D(adjustedPos.X, adjustedPos.Y));
                string? hitId = HitTest(worldPt);
                if (hitId != null)
                {
                    _state.SelectChain(hitId);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape)
            {
                _state?.CancelTool();
                _activeSnapCandidates = null;
                _nearestSnap = null;
                Invalidate();
            }
        }

        #endregion

        #region Click Handlers

        private void HandleSelectClick(Point2D worldPt, MouseEventArgs e)
        {
            if (_state == null) return;

            string? hitId = HitTest(worldPt);

            if (hitId == null)
            {
                if (!ModifierKeys.HasFlag(Keys.Shift) && !ModifierKeys.HasFlag(Keys.Control))
                {
                    // Start box selection or clear
                    _state.ClearSelection();
                    _isBoxSelecting = true;
                    _boxStart = _boxEnd = new Point(e.X - RulerSize, e.Y);
                }
                return;
            }

            if (ModifierKeys.HasFlag(Keys.Shift))
            {
                _state.AddChainToSelection(hitId);
            }
            else if (ModifierKeys.HasFlag(Keys.Control))
            {
                _state.ToggleSelection(hitId);
            }
            else
            {
                _state.SelectEntity(hitId);
                EntityClicked?.Invoke(this, hitId);
            }
        }

        private void HandleMoveClick(Point2D worldPt)
        {
            if (_state == null) return;

            if (_state.MoveState.Phase == MovePhase.Idle)
            {
                // Need exactly 1 LINE selected
                var selected = _state.GetSelectedEntities().ToList();
                if (selected.Count != 1 || selected[0] is not LineEntity line) return;

                // Determine which endpoint is closer
                double dStart = worldPt.DistanceTo(line.Start);
                double dEnd = worldPt.DistanceTo(line.End);

                _state.MoveState = new MoveState
                {
                    EntityId = line.Id,
                    Which = dStart <= dEnd ? "start" : "end",
                    Phase = MovePhase.PickTarget
                };
                Invalidate();
            }
            else if (_state.MoveState.Phase == MovePhase.PickTarget)
            {
                if (_nearestSnap != null)
                {
                    SnapCandidateClicked?.Invoke(this, _nearestSnap);
                }
                _state.MoveState = new MoveState();
                _activeSnapCandidates = null;
                _nearestSnap = null;
                Invalidate();
            }
        }

        private void HandleTrimClick(Point2D worldPt)
        {
            if (_state == null) return;

            string? hitId = HitTest(worldPt);
            if (hitId == null) return;

            // Trim requires exactly 2 selected entities
            _state.ToggleSelection(hitId);
        }

        private void HandleSnapClick(Point2D worldPt)
        {
            if (_nearestSnap != null)
            {
                SnapCandidateClicked?.Invoke(this, _nearestSnap);
            }
        }

        #endregion

        #region Hit Testing

        /// <summary>
        /// Finds the entity closest to the given world-space point within pick tolerance.
        /// </summary>
        private string? HitTest(Point2D worldPt)
        {
            if (_state == null) return null;

            double pickTol = _transform.PickTolerance(6);
            string? bestId = null;
            double bestDist = double.MaxValue;

            foreach (var entity in _state.Entities)
            {
                if (!_state.IsEntityVisible(entity))
                    continue;

                double dist = DistanceToEntity(worldPt, entity);
                if (dist < pickTol && dist < bestDist)
                {
                    bestDist = dist;
                    bestId = entity.Id;
                }
            }

            return bestId;
        }

        private double DistanceToEntity(Point2D pt, EntityBase entity)
        {
            switch (entity)
            {
                case LineEntity line:
                    return GeometryMath.PointToSegmentDistance(pt, line.Start, line.End);
                case ArcEntity arc:
                    return GeometryMath.PointToArcDistance(pt, arc);
                case CircleEntity circle:
                    return GeometryMath.PointToCircleDistance(pt, circle);
                case PointEntity point:
                    return pt.DistanceTo(point.Position);
                case PolylineEntity poly:
                    return DistanceToPolyline(pt, poly);
                default:
                    return double.MaxValue;
            }
        }

        private double DistanceToPolyline(Point2D pt, PolylineEntity poly)
        {
            double minDist = double.MaxValue;
            var decomposed = GeometryDecomposer.DecomposePolyline(poly);
            foreach (var prim in decomposed)
            {
                double d = DistanceToEntity(pt, prim);
                minDist = Math.Min(minDist, d);
            }
            return minDist;
        }

        #endregion

        #region Box Selection

        private void PerformBoxSelection()
        {
            if (_state == null) return;

            var w1 = _transform.ScreenToWorld(new Point2D(
                Math.Min(_boxStart.X, _boxEnd.X),
                Math.Min(_boxStart.Y, _boxEnd.Y)));
            var w2 = _transform.ScreenToWorld(new Point2D(
                Math.Max(_boxStart.X, _boxEnd.X),
                Math.Max(_boxStart.Y, _boxEnd.Y)));

            double minWX = Math.Min(w1.X, w2.X);
            double maxWX = Math.Max(w1.X, w2.X);
            double minWY = Math.Min(w1.Y, w2.Y);
            double maxWY = Math.Max(w1.Y, w2.Y);

            // Collect IDs first, then use SelectEntity / toggle API
            var hitIds = new List<string>();
            foreach (var entity in _state.Entities)
            {
                if (!_state.IsEntityVisible(entity)) continue;

                var (eMinX, eMinY, eMaxX, eMaxY) = entity.GetBounds();
                if (eMinX >= minWX && eMaxX <= maxWX && eMinY >= minWY && eMaxY <= maxWY)
                {
                    hitIds.Add(entity.Id);
                }
            }

            if (hitIds.Count > 0)
            {
                // Select first to trigger the event, then add the rest
                _state.SelectEntity(hitIds[0]);
                for (int i = 1; i < hitIds.Count; i++)
                    _state.SelectedIds.Add(hitIds[i]);
            }
        }

        #endregion

        #region Snap Candidates

        private void UpdateSnapCandidates(Point2D mouseWorld)
        {
            if (_state == null) return;

            var candidates = new List<SnapCandidate>();
            double pickRadius = _transform.PickTolerance(50);

            foreach (var entity in _state.Entities)
            {
                if (!_state.IsEntityVisible(entity)) continue;
                if (entity.Id == _state.MoveState.EntityId) continue;

                double dist = DistanceToEntity(mouseWorld, entity);
                if (dist > pickRadius) continue;

                Point2D? anchor = null;
                if (_state.MoveState.EntityId != null)
                {
                    var moveEntity = _state.Entities.FirstOrDefault(
                        e => e.Id == _state.MoveState.EntityId) as LineEntity;
                    if (moveEntity != null)
                    {
                        anchor = _state.MoveState.Which == "start"
                            ? moveEntity.Start : moveEntity.End;
                    }
                }

                candidates.AddRange(SnapUtilities.GetSnapCandidates(entity, anchor));
            }

            _activeSnapCandidates = candidates;
            _nearestSnap = SnapUtilities.FindNearest(mouseWorld, candidates, _transform.PickTolerance(15));

            Invalidate();
        }

        #endregion

        #region Helpers

        private PointF WorldToScreen(Point2D world)
        {
            var screen = _transform.WorldToScreen(world);
            return new PointF((float)screen.X, (float)screen.Y);
        }

        private Color GetEntityDisplayColor(EntityBase entity)
        {
            // Check if assigned to a new layer
            var assignedLayer = _state?.GetAssignedLayer(entity.Id);
            if (assignedLayer != null)
                return AciColorTable.GetDisplayColor(assignedLayer.Color);

            return AciColorTable.GetDisplayColor(entity.Color);
        }

        private void UpdateCursorForTool()
        {
            if (_state == null)
            {
                Cursor = Cursors.Cross;
                return;
            }

            Cursor = _state.ActiveTool switch
            {
                ToolMode.Select => Cursors.Cross,
                ToolMode.Move => Cursors.Hand,
                ToolMode.Trim => Cursors.Cross,
                ToolMode.Snap => Cursors.Cross,
                ToolMode.PointToCircle => Cursors.Cross,
                _ => Cursors.Cross
            };
        }

        private double CalculateGridStep(double worldWidth)
        {
            // Choose grid step so roughly 10-30 grid lines visible
            double rawStep = worldWidth / 20.0;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double normalized = rawStep / magnitude;

            if (normalized < 1.5) return magnitude;
            if (normalized < 3.5) return 2 * magnitude;
            if (normalized < 7.5) return 5 * magnitude;
            return 10 * magnitude;
        }

        private string FormatRulerValue(double value)
        {
            if (Math.Abs(value) < 1e-10) return "0";
            if (Math.Abs(value) >= 1000) return value.ToString("F0");
            if (Math.Abs(value) >= 1) return value.ToString("F1");
            return value.ToString("F3");
        }

        private void OnRepaintNeeded(object? sender, EventArgs e)
        {
            UpdateCursorForTool();
            Invalidate();
        }

        private void OnFileChanged(object? sender, EventArgs e)
        {
            ZoomToFit();
        }

        private void OnStateChanged(object? sender, StateChangedEventArgs e)
        {
            if (e.Property == "Hovered")
                Invalidate();
        }

        #endregion

        #region Fix Pan (direct offset access)

        // ViewportTransform needs public setters for smooth panning
        // We work around by re-creating the pan computation

        protected new bool DoubleBuffered
        {
            get => base.DoubleBuffered;
            set => base.DoubleBuffered = value;
        }

        #endregion
    }
}
