using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DxfLayerEditor.Models;
using DxfLayerEditor.Services;
using DxfLayerEditor.Utilities;

namespace DxfLayerEditor.Controls
{
    /// <summary>
    /// Right sidebar panel containing:
    /// - Entity properties (type, layer, coordinates)
    /// - Tool-specific options
    /// - Export settings (filename, chord tolerance, mirror, scale)
    /// - Export stats (gaps closed, open gaps list)
    /// </summary>
    public class PropertiesPanel : UserControl
    {
        private EditorState? _state;

        // Entity properties
        private GroupBox _grpEntity = null!;
        private Label _lblEntityType = null!;
        private Label _lblEntityLayer = null!;
        private Label _lblEntityCoords = null!;
        private Label _lblEntityChain = null!;

        // Tool options
        private GroupBox _grpToolOptions = null!;
        private Panel _toolOptionsContainer = null!;

        // Export settings
        private GroupBox _grpExport = null!;
        private TextBox _txtExportFilename = null!;
        private TrackBar _trkChordTolerance = null!;
        private Label _lblChordValue = null!;
        private CheckBox _chkMirrorX = null!;
        private CheckBox _chkMirrorY = null!;
        private NumericUpDown _numScale = null!;
        private Button _btnExport = null!;

        // Stats
        private GroupBox _grpStats = null!;
        private Label _lblEntityCount = null!;
        private Label _lblChainCount = null!;
        private Label _lblGapsClosed = null!;
        private ListView _lstOpenGaps = null!;

        // Point-to-circle dialog controls
        private TextBox? _txtDiameter;
        private Button? _btnConvertP2C;

        /// <summary>Raised when the user clicks Export.</summary>
        public event EventHandler? ExportRequested;

        /// <summary>Raised when the user clicks an open gap to pan to it.</summary>
        public event EventHandler<(double X, double Y)>? GapClicked;

        public PropertiesPanel()
        {
            InitializeComponents();
        }

        /// <summary>Binds this panel to an editor state instance.</summary>
        public void BindState(EditorState state)
        {
            _state = state;
            _state.SelectionChanged += (s, e) => RefreshEntityProperties();
            _state.ToolChanged += (s, e) => RefreshToolOptions();
            _state.EntitiesChanged += (s, e) => RefreshStats();
            _state.FileChanged += (s, e) => RefreshAll();
            _state.LayersChanged += (s, e) => RefreshStats();
            _state.UndoRedoChanged += (s, e) => RefreshStats();
        }

        private void InitializeComponents()
        {
            AutoScroll = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(37, 37, 38);
            ForeColor = Color.FromArgb(220, 220, 220);
            Padding = new Padding(4);

            var mainLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0)
            };

            // ── Entity Properties ──
            _grpEntity = CreateGroupBox("Entity Properties", 110);

            _lblEntityType = CreateInfoLabel("Type: —");
            _lblEntityLayer = CreateInfoLabel("Layer: —");
            _lblEntityCoords = CreateInfoLabel("Coords: —");
            _lblEntityChain = CreateInfoLabel("Chain: —");

            var entityPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            entityPanel.Controls.AddRange(new Control[]
            {
                _lblEntityType, _lblEntityLayer, _lblEntityCoords, _lblEntityChain
            });
            _grpEntity.Controls.Add(entityPanel);

            // ── Tool Options ──
            _grpToolOptions = CreateGroupBox("Tool Options", 100);
            _toolOptionsContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            _grpToolOptions.Controls.Add(_toolOptionsContainer);

            // ── Export Settings ──
            _grpExport = CreateGroupBox("Export Settings", 200);

            var exportLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = true
            };
            exportLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            exportLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _txtExportFilename = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "export.dxf"
            };
            _txtExportFilename.TextChanged += (s, e) =>
            {
                if (_state != null) _state.ExportFileName = _txtExportFilename.Text;
            };

            _trkChordTolerance = new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                TickFrequency = 10,
                SmallChange = 1
            };
            _lblChordValue = CreateInfoLabel("0.001");
            _trkChordTolerance.ValueChanged += (s, e) =>
            {
                double val = _trkChordTolerance.Value / 10000.0;
                _lblChordValue.Text = val.ToString("F4");
                if (_state != null) _state.ChordTolerance = val;
            };

            _chkMirrorX = new CheckBox
            {
                Text = "Mirror X",
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true
            };
            _chkMirrorX.CheckedChanged += (s, e) =>
            {
                if (_state != null) _state.MirrorX = _chkMirrorX.Checked;
            };

            _chkMirrorY = new CheckBox
            {
                Text = "Mirror Y",
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true
            };
            _chkMirrorY.CheckedChanged += (s, e) =>
            {
                if (_state != null) _state.MirrorY = _chkMirrorY.Checked;
            };

            _numScale = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0.001m,
                Maximum = 1000m,
                DecimalPlaces = 3,
                Increment = 0.1m,
                Value = 1.0m,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            _numScale.ValueChanged += (s, e) =>
            {
                if (_state != null) _state.ExportScale = (double)_numScale.Value;
            };

            _btnExport = new Button
            {
                Text = "⬇ Export DXF",
                Dock = DockStyle.Bottom,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 100, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            _btnExport.Click += (s, e) => ExportRequested?.Invoke(this, EventArgs.Empty);

            exportLayout.Controls.Add(CreateSmallLabel("File:"), 0, 0);
            exportLayout.Controls.Add(_txtExportFilename, 1, 0);
            exportLayout.Controls.Add(CreateSmallLabel("Chord:"), 0, 1);
            exportLayout.Controls.Add(_trkChordTolerance, 1, 1);
            exportLayout.Controls.Add(CreateSmallLabel(""), 0, 2);
            exportLayout.Controls.Add(_lblChordValue, 1, 2);

            var mirrorPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Height = 26,
                Dock = DockStyle.Fill
            };
            mirrorPanel.Controls.AddRange(new Control[] { _chkMirrorX, _chkMirrorY });
            exportLayout.Controls.Add(CreateSmallLabel("Mirror:"), 0, 3);
            exportLayout.Controls.Add(mirrorPanel, 1, 3);

            exportLayout.Controls.Add(CreateSmallLabel("Scale:"), 0, 4);
            exportLayout.Controls.Add(_numScale, 1, 4);

            _grpExport.Controls.Add(_btnExport);
            _grpExport.Controls.Add(exportLayout);

            // ── Stats ──
            _grpStats = CreateGroupBox("Statistics", 180);

            _lblEntityCount = CreateInfoLabel("Entities: 0");
            _lblChainCount = CreateInfoLabel("Chains: 0");
            _lblGapsClosed = CreateInfoLabel("Gaps closed: —");

            _lstOpenGaps = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.FromArgb(255, 200, 100),
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 7.5f)
            };
            _lstOpenGaps.Columns.Add("Layer", 70);
            _lstOpenGaps.Columns.Add("Position", 90);
            _lstOpenGaps.Columns.Add("Dist", 50);
            _lstOpenGaps.DoubleClick += LstOpenGaps_DoubleClick;

            var statsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                Height = 60,
                WrapContents = false
            };
            statsPanel.Controls.AddRange(new Control[]
            {
                _lblEntityCount, _lblChainCount, _lblGapsClosed
            });

            _grpStats.Controls.Add(_lstOpenGaps);
            _grpStats.Controls.Add(statsPanel);

            // Add to main layout
            mainLayout.Controls.Add(_grpEntity);
            mainLayout.Controls.Add(_grpToolOptions);
            mainLayout.Controls.Add(_grpExport);
            mainLayout.Controls.Add(_grpStats);

            Controls.Add(mainLayout);
        }

        #region Refresh Methods

        private void RefreshAll()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(RefreshAll)); return; }

            RefreshEntityProperties();
            RefreshToolOptions();
            RefreshStats();

            if (_state != null)
            {
                _txtExportFilename.Text = _state.ExportFileName;
            }
        }

        private void RefreshEntityProperties()
        {
            if (_state == null || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(RefreshEntityProperties)); return; }

            if (_state.SelectedIds.Count == 0)
            {
                _lblEntityType.Text = "Type: —";
                _lblEntityLayer.Text = "Layer: —";
                _lblEntityCoords.Text = "Coords: —";
                _lblEntityChain.Text = "Chain: —";
                return;
            }

            if (_state.SelectedIds.Count == 1)
            {
                var entity = _state.Entities.FirstOrDefault(
                    e => e.Id == _state.SelectedIds.First());
                if (entity == null) return;

                _lblEntityType.Text = $"Type: {entity.EntityType}";
                var assigned = _state.GetAssignedLayer(entity.Id);
                _lblEntityLayer.Text = $"Layer: {entity.Layer}" +
                    (assigned != null ? $" → {assigned.Name}" : "");
                _lblEntityCoords.Text = GetCoordsText(entity);
                _lblEntityChain.Text = $"Chain: {entity.ChainId?.Substring(0, 8) ?? "—"}";
            }
            else
            {
                _lblEntityType.Text = $"Type: ({_state.SelectedIds.Count} entities)";
                _lblEntityLayer.Text = "Layer: (multiple)";
                _lblEntityCoords.Text = "";
                _lblEntityChain.Text = "";
            }
        }

        private void RefreshToolOptions()
        {
            if (_state == null || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(RefreshToolOptions)); return; }

            _toolOptionsContainer.Controls.Clear();

            switch (_state.ActiveTool)
            {
                case ToolMode.Select:
                    _grpToolOptions.Text = "Select Tool";
                    var selectLabel = CreateInfoLabel(
                        "Click: select\nShift+click: add chain\n" +
                        "Ctrl+click: toggle\nDouble-click: select chain\n" +
                        "Drag: box select");
                    selectLabel.Height = 80;
                    _toolOptionsContainer.Controls.Add(selectLabel);
                    break;

                case ToolMode.Move:
                    _grpToolOptions.Text = "Move Tool";
                    string moveText = _state.MoveState.Phase switch
                    {
                        MovePhase.Idle => "Select 1 LINE, then click\nan endpoint to begin.",
                        MovePhase.PickEndpoint => "Click an endpoint marker\nto pick which end to move.",
                        MovePhase.PickTarget => "Hover near entities to see\nsnap candidates. Click to snap.",
                        _ => ""
                    };
                    var moveLabel = CreateInfoLabel(moveText);
                    moveLabel.Height = 60;
                    _toolOptionsContainer.Controls.Add(moveLabel);
                    break;

                case ToolMode.Trim:
                    _grpToolOptions.Text = "Trim Tool";
                    var trimLabel = CreateInfoLabel(
                        "Select exactly 2 LINE or ARC\nentities, then click Trim\nin the toolbar.");
                    trimLabel.Height = 50;
                    _toolOptionsContainer.Controls.Add(trimLabel);
                    break;

                case ToolMode.PointToCircle:
                    _grpToolOptions.Text = "Point → Circle";
                    BuildPointToCircleOptions();
                    break;

                case ToolMode.Snap:
                    _grpToolOptions.Text = "Snap Tool";
                    var snapLabel = CreateInfoLabel(
                        "Hover entities to see snap\npoints. Click to snap the\nselected endpoint.");
                    snapLabel.Height = 50;
                    _toolOptionsContainer.Controls.Add(snapLabel);
                    break;
            }
        }

        private void BuildPointToCircleOptions()
        {
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(2)
            };

            var lbl = CreateInfoLabel("Diameter:");
            _txtDiameter = new TextBox
            {
                Width = 120,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "0.25",
                Font = new Font("Segoe UI", 9f)
            };

            _btnConvertP2C = new Button
            {
                Text = "Convert to Circles",
                Width = 140,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(65, 65, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(0, 4, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnConvertP2C.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 105);
            _btnConvertP2C.FlatAppearance.BorderSize = 1;
            _btnConvertP2C.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 85);
            _btnConvertP2C.FlatAppearance.MouseDownBackColor = Color.FromArgb(45, 45, 48);
            _btnConvertP2C.Click += BtnConvertP2C_Click;

            layout.Controls.AddRange(new Control[] { lbl, _txtDiameter, _btnConvertP2C });
            _toolOptionsContainer.Controls.Add(layout);
        }

        private void BtnConvertP2C_Click(object? sender, EventArgs e)
        {
            if (_state == null || _txtDiameter == null) return;
            if (!double.TryParse(_txtDiameter.Text, out double diameter) || diameter <= 0)
            {
                MessageBox.Show("Please enter a valid positive diameter.",
                    "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int count = _state.ConvertPointsToCircles(diameter);
            MessageBox.Show($"Converted {count} point(s) to circles.",
                "Point → Circle", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RefreshStats()
        {
            if (_state == null || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(RefreshStats)); return; }

            _lblEntityCount.Text = $"Entities: {_state.Entities.Count}";
            _lblChainCount.Text = $"Chains: {_state.Chains.Count}";

            if (_state.LastExportResult != null)
            {
                _lblGapsClosed.Text = $"Gaps closed: {_state.LastExportResult.GapsClosed}";
                _lstOpenGaps.Items.Clear();
                foreach (var gap in _state.LastExportResult.OpenGaps)
                {
                    var item = new ListViewItem(new[]
                    {
                        gap.Layer,
                        $"({gap.X:F2}, {gap.Y:F2})",
                        gap.Distance.ToString("F3")
                    })
                    {
                        Tag = gap
                    };
                    _lstOpenGaps.Items.Add(item);
                }
            }
            else
            {
                _lblGapsClosed.Text = "Gaps closed: —";
                _lstOpenGaps.Items.Clear();
            }
        }

        private void LstOpenGaps_DoubleClick(object? sender, EventArgs e)
        {
            if (_lstOpenGaps.SelectedItems.Count == 0) return;
            if (_lstOpenGaps.SelectedItems[0].Tag is OpenGap gap)
            {
                GapClicked?.Invoke(this, (gap.X, gap.Y));
            }
        }

        #endregion

        #region Helpers

        private string GetCoordsText(EntityBase entity)
        {
            return entity switch
            {
                LineEntity l => $"({l.Start.X:F3}, {l.Start.Y:F3}) → ({l.End.X:F3}, {l.End.Y:F3})",
                ArcEntity a => $"C({a.Center.X:F3}, {a.Center.Y:F3}) R={a.Radius:F3}",
                CircleEntity c => $"C({c.Center.X:F3}, {c.Center.Y:F3}) R={c.Radius:F3}",
                PointEntity p => $"({p.Position.X:F3}, {p.Position.Y:F3})",
                _ => "—"
            };
        }

        private GroupBox CreateGroupBox(string title, int height)
        {
            return new GroupBox
            {
                Text = title,
                Width = 238,
                Height = height,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI Semibold", 9f),
                Margin = new Padding(2, 4, 2, 2),
                Padding = new Padding(4, 12, 4, 4)
            };
        }

        private Label CreateInfoLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 220,
                Height = 18,
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 8.5f)
            };
        }

        private Label CreateSmallLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 8f),
                Padding = new Padding(0, 4, 0, 0)
            };
        }

        #endregion
    }
}
