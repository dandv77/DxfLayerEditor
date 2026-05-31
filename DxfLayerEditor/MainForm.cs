using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DxfLayerEditor.Controls;
using DxfLayerEditor.Models;
using DxfLayerEditor.Services;
using DxfLayerEditor.Utilities;

namespace DxfLayerEditor
{
    /// <summary>
    /// Main application form for the DXF Layer Editor.
    /// Provides a three-panel layout with MenuStrip, ToolStrip, and StatusStrip.
    /// </summary>
    public class MainForm : Form
    {
        #region Fields

        private readonly EditorState _state = new();
        private readonly DxfFileService _dxfService = new();

        // Layout
        private MenuStrip _menuStrip = null!;
        private ToolStrip _toolStrip = null!;
        private StatusStrip _statusStrip = null!;
        private SplitContainer _outerSplit = null!;
        private SplitContainer _innerSplit = null!;

        // Panels
        private DxfViewport _viewport = null!;
        private LayerPanel _layerPanel = null!;
        private PropertiesPanel _propertiesPanel = null!;

        // Status items
        private ToolStripStatusLabel _statusCoords = null!;
        private ToolStripStatusLabel _statusSnap = null!;
        private ToolStripStatusLabel _statusFile = null!;
        private ToolStripStatusLabel _statusZoom = null!;
        private ToolStripStatusLabel _statusTool = null!;

        // Menu items needing enable/disable
        private ToolStripMenuItem _menuUndo = null!;
        private ToolStripMenuItem _menuRedo = null!;
        private ToolStripMenuItem _menuSave = null!;
        private ToolStripMenuItem _menuExport = null!;

        // Toolbar buttons
        private ToolStripButton _btnUndo = null!;
        private ToolStripButton _btnRedo = null!;

        #endregion

        #region Constructor

        public MainForm()
        {
            InitializeForm();
            BuildMenuStrip();
            BuildToolStrip();
            BuildStatusStrip();
            BuildLayout();
            WireEvents();
            UpdateTitle();
        }

        #endregion

        #region Form Initialization

        private void InitializeForm()
        {
            Text = "DXF Layer Editor — Thermwood CNC Prep";
            Size = new Size(1400, 900);
            MinimumSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.FromArgb(220, 220, 220);
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;
            KeyPreview = true;

            Icon = CreateDefaultIcon();
        }

        private static Icon CreateDefaultIcon()
        {
            // Generate a simple icon programmatically
            var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(0, 100, 180));
            using var font = new Font("Segoe UI", 14f, FontStyle.Bold);
            g.DrawString("D", font, Brushes.White, 6, 4);
            return Icon.FromHandle(bmp.GetHicon());
        }

        #endregion

        #region Menu Strip

        private void BuildMenuStrip()
        {
            _menuStrip = new MenuStrip
            {
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.FromArgb(220, 220, 220),
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new DarkMenuRenderer()
            };

            // ── File ──
            var fileMenu = CreateMenu("&File");
            fileMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("&New", Keys.Control | Keys.N, (s, e) => NewDocument()),
                CreateMenuItem("&Open...", Keys.Control | Keys.O, (s, e) => OpenDocument()),
                _menuSave = CreateMenuItem("&Save", Keys.Control | Keys.S, (s, e) => SaveDocument()),
                new ToolStripSeparator(),
                _menuExport = CreateMenuItem("&Export DXF...", Keys.Control | Keys.E, (s, e) => ExportDocument()),
                new ToolStripSeparator(),
                CreateMenuItem("E&xit", Keys.Alt | Keys.F4, (s, e) => Close())
            });
            _menuSave.Enabled = false;
            _menuExport.Enabled = false;

            // ── Edit ──
            var editMenu = CreateMenu("&Edit");
            _menuUndo = CreateMenuItem("&Undo", Keys.Control | Keys.Z, (s, e) => _state.Undo());
            _menuRedo = CreateMenuItem("&Redo", Keys.Control | Keys.Y, (s, e) => _state.Redo());
            _menuUndo.Enabled = false;
            _menuRedo.Enabled = false;
            editMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                _menuUndo,
                _menuRedo,
                new ToolStripSeparator(),
                CreateMenuItem("Select &All", Keys.Control | Keys.A, (s, e) => SelectAll()),
                CreateMenuItem("&Deselect", Keys.Escape, (s, e) => _state.ClearSelection())
            });

            // ── View ──
            var viewMenu = CreateMenu("&View");
            viewMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("Zoom &In", Keys.Control | Keys.Oemplus, (s, e) => _viewport.ZoomIn()),
                CreateMenuItem("Zoom &Out", Keys.Control | Keys.OemMinus, (s, e) => _viewport.ZoomOut()),
                CreateMenuItem("Zoom to &Fit", Keys.Control | Keys.D0, (s, e) => _viewport.ZoomToFit()),
                new ToolStripSeparator(),
                CreateCheckMenuItem("&Snap Enabled", true, (s, e) =>
                {
                    var item = (ToolStripMenuItem)s!;
                    _state.SnapEnabled = item.Checked;
                }),
                CreateCheckMenuItem("&Units (in/mm)", false, (s, e) =>
                {
                    var item = (ToolStripMenuItem)s!;
                    _state.Units = item.Checked ? "mm" : "in";
                    UpdateStatusBar();
                })
            });

            // ── Tools ──
            var toolsMenu = CreateMenu("&Tools");
            toolsMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("&Select", Keys.S, (s, e) => _state.SetTool(ToolMode.Select)),
                CreateMenuItem("&Move", Keys.M, (s, e) => _state.SetTool(ToolMode.Move)),
                CreateMenuItem("&Trim", Keys.T, (s, e) => ExecuteTrim()),
                CreateMenuItem("S&nap", Keys.N, (s, e) => _state.SetTool(ToolMode.Snap)),
                CreateMenuItem("&Point → Circle", Keys.P, (s, e) => _state.SetTool(ToolMode.PointToCircle)),
                new ToolStripSeparator(),
                CreateMenuItem("Grow to &Chain", Keys.G, (s, e) => _state.GrowSelectionToChains()),
            });

            // ── Help ──
            var helpMenu = CreateMenu("&Help");
            helpMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("&About", Keys.None, (s, e) => ShowAbout())
            });

            _menuStrip.Items.AddRange(new ToolStripItem[]
            {
                fileMenu, editMenu, viewMenu, toolsMenu, helpMenu
            });

            Controls.Add(_menuStrip);
            MainMenuStrip = _menuStrip;
        }

        #endregion

        #region Tool Strip

        private void BuildToolStrip()
        {
            _toolStrip = new ToolStrip
            {
                BackColor = Color.FromArgb(50, 50, 53),
                ForeColor = Color.FromArgb(220, 220, 220),
                GripStyle = ToolStripGripStyle.Hidden,
                Renderer = new DarkToolStripRenderer(),
                ImageScalingSize = new Size(18, 18),
                Padding = new Padding(4, 2, 4, 2)
            };

            // File operations
            _toolStrip.Items.Add(CreateToolButton("📄", "New (Ctrl+N)", (s, e) => NewDocument()));
            _toolStrip.Items.Add(CreateToolButton("📂", "Open (Ctrl+O)", (s, e) => OpenDocument()));
            _toolStrip.Items.Add(CreateToolButton("💾", "Save (Ctrl+S)", (s, e) => SaveDocument()));
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Undo/Redo
            _btnUndo = CreateToolButton("↩", "Undo (Ctrl+Z)", (s, e) => _state.Undo());
            _btnRedo = CreateToolButton("↪", "Redo (Ctrl+Y)", (s, e) => _state.Redo());
            _btnUndo.Enabled = false;
            _btnRedo.Enabled = false;
            _toolStrip.Items.Add(_btnUndo);
            _toolStrip.Items.Add(_btnRedo);
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Tools
            _toolStrip.Items.Add(CreateToolButton("⬚", "Select (S)", (s, e) => _state.SetTool(ToolMode.Select)));
            _toolStrip.Items.Add(CreateToolButton("✥", "Move (M)", (s, e) => _state.SetTool(ToolMode.Move)));
            _toolStrip.Items.Add(CreateToolButton("✂", "Trim (T)", (s, e) => ExecuteTrim()));
            _toolStrip.Items.Add(CreateToolButton("◎", "Snap (N)", (s, e) => _state.SetTool(ToolMode.Snap)));
            _toolStrip.Items.Add(CreateToolButton("●", "Point→Circle (P)", (s, e) => _state.SetTool(ToolMode.PointToCircle)));
            _toolStrip.Items.Add(new ToolStripSeparator());

            // View
            _toolStrip.Items.Add(CreateToolButton("🔍+", "Zoom In", (s, e) => _viewport.ZoomIn()));
            _toolStrip.Items.Add(CreateToolButton("🔍-", "Zoom Out", (s, e) => _viewport.ZoomOut()));
            _toolStrip.Items.Add(CreateToolButton("⊞", "Zoom to Fit (Ctrl+0)", (s, e) => _viewport.ZoomToFit()));

            Controls.Add(_toolStrip);
        }

        #endregion

        #region Status Strip

        private void BuildStatusStrip()
        {
            _statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(0, 100, 180),
                ForeColor = Color.White,
                SizingGrip = true,
                Font = new Font("Segoe UI", 8.5f)
            };

            _statusCoords = new ToolStripStatusLabel("X: 0.000  Y: 0.000")
            {
                AutoSize = false,
                Width = 200,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _statusSnap = new ToolStripStatusLabel("Snap: ON")
            {
                AutoSize = false,
                Width = 80,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _statusTool = new ToolStripStatusLabel("Tool: Select")
            {
                AutoSize = false,
                Width = 120,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _statusFile = new ToolStripStatusLabel("No file loaded")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _statusZoom = new ToolStripStatusLabel("Zoom: 100%")
            {
                AutoSize = false,
                Width = 100,
                TextAlign = ContentAlignment.MiddleRight
            };

            _statusStrip.Items.AddRange(new ToolStripItem[]
            {
                _statusCoords, _statusSnap, _statusTool, _statusFile, _statusZoom
            });

            Controls.Add(_statusStrip);
        }

        #endregion

        #region Layout

        private void BuildLayout()
        {
            // Outer split: left panel | rest
            _outerSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 250,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 3,
                BackColor = Color.FromArgb(30, 30, 30),
                Panel1MinSize = 200,
                Panel2MinSize = 400
            };
            _outerSplit.Panel1.BackColor = Color.FromArgb(37, 37, 38);
            _outerSplit.Panel2.BackColor = Color.FromArgb(30, 30, 30);

            // Inner split: viewport | right panel
            _innerSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 3,
                BackColor = Color.FromArgb(30, 30, 30),
                Panel2MinSize = 200
            };
            _innerSplit.Panel1.BackColor = Color.FromArgb(30, 30, 30);
            _innerSplit.Panel2.BackColor = Color.FromArgb(37, 37, 38);

            // Create panels
            _layerPanel = new LayerPanel();
            _layerPanel.BindState(_state);
            _outerSplit.Panel1.Controls.Add(_layerPanel);

            _viewport = new DxfViewport();
            _viewport.BindState(_state);
            _innerSplit.Panel1.Controls.Add(_viewport);

            _propertiesPanel = new PropertiesPanel();
            _propertiesPanel.BindState(_state);
            _innerSplit.Panel2.Controls.Add(_propertiesPanel);

            _outerSplit.Panel2.Controls.Add(_innerSplit);
            Controls.Add(_outerSplit);

            // Adjust splitter after layout
            Load += (s, e) =>
            {
                _innerSplit.SplitterDistance = _innerSplit.Width - 250;
            };
        }

        #endregion

        #region Event Wiring

        private void WireEvents()
        {
            // Viewport events
            _viewport.WorldPositionChanged += (s, pos) =>
            {
                string unit = _state.Units == "mm" ? "mm" : "in";
                double x = _state.Units == "mm" ? pos.X * 25.4 : pos.X;
                double y = _state.Units == "mm" ? pos.Y * 25.4 : pos.Y;
                _statusCoords.Text = $"X: {x:F3}  Y: {y:F3} {unit}";
            };

            _viewport.ZoomChanged += (s, zoom) =>
            {
                _statusZoom.Text = $"Zoom: {zoom * 100:F0}%";
            };

            _viewport.SnapCandidateClicked += (s, snap) =>
            {
                if (_state.ActiveTool == ToolMode.Move && _state.MoveState.EntityId != null)
                {
                    PerformMoveEndpoint(snap.Position);
                }
            };

            // State events
            _state.FileChanged += (s, e) =>
            {
                UpdateTitle();
                _menuSave.Enabled = _state.FilePath != null;
                _menuExport.Enabled = _state.Entities.Count > 0;
                _statusFile.Text = _state.FileName;
            };

            _state.UndoRedoChanged += (s, e) =>
            {
                _menuUndo.Enabled = _state.CanUndo;
                _menuRedo.Enabled = _state.CanRedo;
                _btnUndo.Enabled = _state.CanUndo;
                _btnRedo.Enabled = _state.CanRedo;
                UpdateTitle();
            };

            _state.ToolChanged += (s, e) =>
            {
                _statusTool.Text = $"Tool: {_state.ActiveTool}";
            };

            _state.SelectionChanged += (s, e) =>
            {
                UpdateStatusBar();
            };

            // Properties panel events
            _propertiesPanel.ExportRequested += (s, e) => ExportDocument();
            _propertiesPanel.GapClicked += (s, pos) =>
            {
                _viewport.PanTo(pos.X, pos.Y);
            };

            // Keyboard shortcuts
            KeyDown += MainForm_KeyDown;
        }

        #endregion

        #region Keyboard

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.N: NewDocument(); e.Handled = true; break;
                    case Keys.O: OpenDocument(); e.Handled = true; break;
                    case Keys.S: SaveDocument(); e.Handled = true; break;
                    case Keys.E: ExportDocument(); e.Handled = true; break;
                    case Keys.Z: _state.Undo(); e.Handled = true; break;
                    case Keys.Y: _state.Redo(); e.Handled = true; break;
                    case Keys.A: SelectAll(); e.Handled = true; break;
                    case Keys.D0: _viewport.ZoomToFit(); e.Handled = true; break;
                }
                return;
            }

            if (e.Alt) return;

            // Tool shortcuts (only when no text field has focus)
            if (ActiveControl is not TextBox && ActiveControl is not NumericUpDown)
            {
                switch (e.KeyCode)
                {
                    case Keys.Escape:
                        _state.CancelTool();
                        _state.ClearSelection();
                        e.Handled = true;
                        break;
                    case Keys.Delete:
                        _state.UnassignSelection();
                        e.Handled = true;
                        break;
                }
            }
        }

        #endregion

        #region File Operations

        private void NewDocument()
        {
            if (_state.IsDirty)
            {
                var result = MessageBox.Show(
                    "Save changes before creating a new document?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes) SaveDocument();
            }

            _state.ClearDocument();
        }

        private void OpenDocument()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Open DXF File",
                Filter = "DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var (entities, layers, skipped) = _dxfService.LoadDxf(dlg.FileName);
                _state.LoadDocument(dlg.FileName, entities, layers, skipped);

                if (skipped.Count > 0)
                {
                    MessageBox.Show(
                        $"Some entity types were skipped:\n{string.Join(", ", skipped)}",
                        "Skipped Entities",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading DXF file:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void SaveDocument()
        {
            if (_state.FilePath == null) return;

            // For now, "save" just marks as clean since we modify in-memory
            MessageBox.Show(
                "Document state saved. Use Export to write a new DXF file.",
                "Save",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ExportDocument()
        {
            if (_state.Entities.Count == 0)
            {
                MessageBox.Show("No entities to export.", "Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Export DXF",
                Filter = "DXF Files (*.dxf)|*.dxf",
                FileName = _state.ExportFileName
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;

                // Run chain-close pass on qualifying layers
                var entitiesByLayer = new System.Collections.Generic.Dictionary<string,
                    System.Collections.Generic.List<EntityBase>>();

                foreach (var kvp in _state.Assignments)
                {
                    var layer = _state.NewLayers.FirstOrDefault(l => l.Id == kvp.Key);
                    if (layer == null) continue;

                    var layerEntities = _state.Entities
                        .Where(e => kvp.Value.Contains(e.Id))
                        .SelectMany(e => GeometryDecomposer.Decompose(e))
                        .ToList();

                    if (layerEntities.Count > 0)
                        entitiesByLayer[layer.Name] = layerEntities;
                }

                var closeResult = ChainCloser.CloseChains(entitiesByLayer, _state.SnapTolerance);
                _state.LastExportResult = closeResult;

                // Export
                _dxfService.ExportDxf(
                    dlg.FileName,
                    _state.Entities,
                    _state.Assignments,
                    _state.NewLayers,
                    _state.MirrorX,
                    _state.MirrorY,
                    _state.ExportScale);

                string msg = $"Exported to {Path.GetFileName(dlg.FileName)}\n" +
                            $"Gaps closed: {closeResult.GapsClosed}\n" +
                            $"Open gaps: {closeResult.OpenGaps.Count}";

                MessageBox.Show(msg, "Export Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error exporting DXF:\n{ex.Message}",
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        #endregion

        #region Tool Operations

        private void ExecuteTrim()
        {
            if (_state.SelectedIds.Count != 2)
            {
                MessageBox.Show(
                    "Select exactly 2 LINE or ARC entities to trim.",
                    "Trim",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var selected = _state.GetSelectedEntities().ToList();
            var a = selected[0];
            var b = selected[1];

            // Only LINE and ARC supported for trim
            if (a is not (LineEntity or ArcEntity) ||
                b is not (LineEntity or ArcEntity))
            {
                MessageBox.Show(
                    "Trim only supports LINE and ARC entities.",
                    "Trim",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Compute intersection and trim
            bool trimmed = false;

            if (a is LineEntity lineA && b is LineEntity lineB)
            {
                if (GeometryMath.LineLineIntersection(
                        lineA.Start, lineA.End,
                        lineB.Start, lineB.End,
                        out Point2D intersection))
                {
                    _state.PushUndo("Trim");

                    // Trim each line to the intersection point
                    TrimLineToIntersection(lineA, intersection);
                    TrimLineToIntersection(lineB, intersection);
                    trimmed = true;
                }
            }
            else if (a is LineEntity trimLine && b is ArcEntity trimArc)
            {
                var hits = GeometryMath.LineArcIntersections(trimLine, trimArc);
                if (hits.Count > 0)
                {
                    _state.PushUndo("Trim");
                    TrimLineToIntersection(trimLine, hits[0]);
                    TrimArcToIntersection(trimArc, hits[0]);
                    trimmed = true;
                }
            }
            else if (a is ArcEntity trimArc2 && b is LineEntity trimLine2)
            {
                var hits = GeometryMath.LineArcIntersections(trimLine2, trimArc2);
                if (hits.Count > 0)
                {
                    _state.PushUndo("Trim");
                    TrimLineToIntersection(trimLine2, hits[0]);
                    TrimArcToIntersection(trimArc2, hits[0]);
                    trimmed = true;
                }
            }
            else if (a is ArcEntity arcA && b is ArcEntity arcB)
            {
                var hits = GeometryMath.ArcArcIntersections(arcA, arcB);
                if (hits.Count > 0)
                {
                    _state.PushUndo("Trim");
                    TrimArcToIntersection(arcA, hits[0]);
                    TrimArcToIntersection(arcB, hits[0]);
                    trimmed = true;
                }
            }

            if (trimmed)
            {
                _state.RedetectChains();
                _state.RecomputeBounds();
                _state.ClearSelection();
            }
            else
            {
                MessageBox.Show(
                    "No intersection found between the selected entities.",
                    "Trim",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void TrimLineToIntersection(LineEntity line, Point2D intersection)
        {
            // Move the endpoint that's further from the intersection
            double dStart = line.Start.DistanceTo(intersection);
            double dEnd = line.End.DistanceTo(intersection);

            if (dStart > dEnd)
                line.Start = intersection;
            else
                line.End = intersection;
        }

        private void TrimArcToIntersection(ArcEntity arc, Point2D intersection)
        {
            double angle = Math.Atan2(
                intersection.Y - arc.Center.Y,
                intersection.X - arc.Center.X) * 180.0 / Math.PI;
            angle = ArcEntity.NormalizeAngle(angle);

            double dStart = Math.Abs(angle - arc.StartAngle);
            double dEnd = Math.Abs(angle - arc.EndAngle);
            if (dStart > 180) dStart = 360 - dStart;
            if (dEnd > 180) dEnd = 360 - dEnd;

            if (dStart < dEnd)
                arc.StartAngle = angle;
            else
                arc.EndAngle = angle;
        }

        private void PerformMoveEndpoint(Point2D target)
        {
            if (_state.MoveState.EntityId == null) return;

            var entity = _state.Entities.FirstOrDefault(
                e => e.Id == _state.MoveState.EntityId) as LineEntity;
            if (entity == null) return;

            _state.PushUndo("Move endpoint");

            if (_state.MoveState.Which == "start")
                entity.Start = target;
            else
                entity.End = target;

            _state.RedetectChains();
            _state.RecomputeBounds();
            _state.CancelTool();
        }

        private void SelectAll()
        {
            if (_state.Entities.Count == 0) return;

            foreach (var entity in _state.Entities)
            {
                if (_state.IsEntityVisible(entity))
                    _state.SelectedIds.Add(entity.Id);
            }
            _state.RaiseSelectionChanged();
        }

        #endregion

        #region UI Updates

        private void UpdateTitle()
        {
            string dirty = _state.IsDirty ? " •" : "";
            string file = _state.FileName;
            Text = $"DXF Layer Editor — {file}{dirty}";
        }

        private void UpdateStatusBar()
        {
            _statusSnap.Text = _state.SnapEnabled ? "Snap: ON" : "Snap: OFF";
            _statusTool.Text = $"Tool: {_state.ActiveTool}";
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "DXF Layer Editor\n" +
                "Version 1.0\n\n" +
                "CAD prep tool for Thermwood CNC machines.\n" +
                "Load a DXF → Re-layer geometry → Repair → Export.\n\n" +
                "Built with .NET 8 WinForms + netDxf",
                "About DXF Layer Editor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion

        #region Form Events

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_state.IsDirty)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Exit anyway?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                    e.Cancel = true;
            }

            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _viewport?.Invalidate();
        }

        #endregion

        #region Control Factory Helpers

        private ToolStripMenuItem CreateMenu(string text)
        {
            var menu = new ToolStripMenuItem(text)
            {
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            return menu;
        }

        private ToolStripMenuItem CreateMenuItem(string text, Keys shortcut, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text)
            {
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
            }
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateCheckMenuItem(string text, bool initialChecked, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text)
            {
                Checked = initialChecked,
                CheckOnClick = true,
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            item.Click += handler;
            return item;
        }

        private ToolStripButton CreateToolButton(string text, string tooltip, EventHandler handler)
        {
            var btn = new ToolStripButton(text)
            {
                ToolTipText = tooltip,
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 11f),
                AutoSize = true,
                Margin = new Padding(1, 1, 1, 1)
            };
            btn.Click += handler;
            return btn;
        }

        #endregion
    }

    #region Dark Theme Renderers

    /// <summary>Custom renderer for dark-themed menus.</summary>
    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                var rect = new Rectangle(Point.Empty, e.Item.Size);
                using var brush = new SolidBrush(Color.FromArgb(62, 62, 66));
                e.Graphics.FillRectangle(brush, rect);
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(220, 220, 220);
            base.OnRenderItemText(e);
        }
    }

    /// <summary>Color table for dark menu theme.</summary>
    internal class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color MenuStripGradientEnd => Color.FromArgb(45, 45, 48);
        public override Color MenuBorder => Color.FromArgb(51, 51, 55);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.FromArgb(62, 62, 66);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 62, 66);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 62, 66);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(27, 27, 28);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(27, 27, 28);
        public override Color ToolStripDropDownBackground => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientBegin => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientEnd => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(27, 27, 28);
        public override Color SeparatorDark => Color.FromArgb(51, 51, 55);
        public override Color SeparatorLight => Color.FromArgb(51, 51, 55);
    }

    /// <summary>Custom renderer for dark-themed toolstrips.</summary>
    internal class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkMenuColorTable()) { }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                var rect = new Rectangle(Point.Empty, e.Item.Size);
                using var brush = new SolidBrush(Color.FromArgb(70, 70, 74));
                e.Graphics.FillRectangle(brush, rect);
                using var pen = new Pen(Color.FromArgb(85, 85, 88));
                e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
            }
            else
            {
                base.OnRenderButtonBackground(e);
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Color.FromArgb(50, 50, 53));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(63, 63, 70));
            e.Graphics.DrawLine(pen, 0, e.AffectedBounds.Height - 1,
                e.AffectedBounds.Width, e.AffectedBounds.Height - 1);
        }
    }

    #endregion
}
