using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DxfLayerEditor.Models;
using DxfLayerEditor.Services;
using DxfLayerEditor.Utilities;

namespace DxfLayerEditor.Controls
{
    /// <summary>
    /// Left sidebar panel containing:
    /// - Original Layers tree (with visibility toggle)
    /// - Layer Builder (Thermwood naming)
    /// - New Layers list (with assign, rename, color, remove)
    /// - Selection info and chain controls
    /// </summary>
    public class LayerPanel : UserControl
    {
        private EditorState? _state;

        // Controls — Original Layers
        private GroupBox _grpOriginal = null!;
        private TreeView _treeOriginal = null!;

        // Controls — Layer Builder
        private GroupBox _grpBuilder = null!;
        private ComboBox _cmbLayerType = null!;
        private TextBox _txtLayerSuffix = null!;
        private CheckBox _chkBackPrefix = null!;
        private Button _btnAddLayer = null!;
        private Label _lblBuilderError = null!;

        // Controls — New Layers
        private GroupBox _grpNewLayers = null!;
        private ListView _lstNewLayers = null!;
        private Button _btnAssign = null!;
        private Button _btnUnassign = null!;
        private Button _btnRemoveLayer = null!;

        // Controls — Selection Info
        private GroupBox _grpSelection = null!;
        private Label _lblSelectionInfo = null!;
        private Button _btnGrowChain = null!;
        private Label _lblChainTolerance = null!;
        private NumericUpDown _numTolerance = null!;

        // Image list for tree icons
        private ImageList _imageList = null!;

        public LayerPanel()
        {
            InitializeComponents();
        }

        /// <summary>Binds this panel to an editor state instance.</summary>
        public void BindState(EditorState state)
        {
            _state = state;
            _state.LayersChanged += (s, e) => RefreshAll();
            _state.SelectionChanged += (s, e) => RefreshSelectionInfo();
            _state.FileChanged += (s, e) => RefreshAll();
            _state.EntitiesChanged += (s, e) => RefreshAll();
        }

        private void InitializeComponents()
        {
            AutoScroll = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(37, 37, 38);
            ForeColor = Color.FromArgb(220, 220, 220);
            Padding = new Padding(4);

            // Create image list for layer color indicators
            _imageList = new ImageList { ImageSize = new Size(12, 12) };

            var mainLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0)
            };

            // ── Selection Info ──
            _grpSelection = CreateGroupBox("Selection", 100);
            _lblSelectionInfo = new Label
            {
                Text = "No selection",
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 20,
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 8.5f)
            };
            _btnGrowChain = CreateButton("Grow to Chain", 0);
            _btnGrowChain.Click += (s, e) => _state?.GrowSelectionToChains();

            var tolPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Height = 28,
                Width = 230,
                Margin = new Padding(0, 2, 0, 0)
            };
            _lblChainTolerance = new Label
            {
                Text = "Tol:",
                AutoSize = true,
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 8f),
                Padding = new Padding(0, 4, 0, 0)
            };
            _numTolerance = new NumericUpDown
            {
                Minimum = 0.00001m,
                Maximum = 1m,
                DecimalPlaces = 5,
                Increment = 0.0001m,
                Value = 0.0001m,
                Width = 90,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            _numTolerance.ValueChanged += (s, e) =>
            {
                if (_state != null)
                {
                    _state.ChainTolerance = (double)_numTolerance.Value;
                    _state.RedetectChains();
                }
            };
            tolPanel.Controls.AddRange(new Control[] { _lblChainTolerance, _numTolerance });

            _grpSelection.Controls.Add(tolPanel);
            _grpSelection.Controls.Add(_btnGrowChain);
            _grpSelection.Controls.Add(_lblSelectionInfo);

            // ── Original Layers ──
            _grpOriginal = CreateGroupBox("Original Layers", 160);
            _treeOriginal = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.None,
                CheckBoxes = true,
                ShowLines = false,
                Font = new Font("Segoe UI", 8.5f),
                ItemHeight = 20
            };
            _treeOriginal.AfterCheck += TreeOriginal_AfterCheck;
            _grpOriginal.Controls.Add(_treeOriginal);

            // ── Layer Builder ──
            _grpBuilder = CreateGroupBox("Layer Builder", 140);

            var builderLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true
            };
            builderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            builderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _cmbLayerType = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(220, 220, 220),
                FlatStyle = FlatStyle.Flat
            };
            _cmbLayerType.Items.AddRange(Layer.ValidLayerTypes);
            if (_cmbLayerType.Items.Count > 0) _cmbLayerType.SelectedIndex = 0;
            _cmbLayerType.SelectedIndexChanged += (s, e) => UpdateBuilderPreview();

            _txtLayerSuffix = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "optional suffix"
            };
            _txtLayerSuffix.TextChanged += (s, e) => UpdateBuilderPreview();

            _chkBackPrefix = new CheckBox
            {
                Text = "Back side",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 8.5f)
            };
            _chkBackPrefix.CheckedChanged += (s, e) => UpdateBuilderPreview();

            _btnAddLayer = CreateButton("Add Layer", 2);
            _btnAddLayer.Click += BtnAddLayer_Click;

            _lblBuilderError = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(255, 100, 100),
                Font = new Font("Segoe UI", 7.5f),
                AutoSize = false,
                Height = 18
            };

            builderLayout.Controls.Add(CreateLabel("Type:"), 0, 0);
            builderLayout.Controls.Add(_cmbLayerType, 1, 0);
            builderLayout.Controls.Add(CreateLabel("Suffix:"), 0, 1);
            builderLayout.Controls.Add(_txtLayerSuffix, 1, 1);
            builderLayout.Controls.Add(_chkBackPrefix, 0, 2);
            builderLayout.SetColumnSpan(_chkBackPrefix, 2);
            builderLayout.Controls.Add(_lblBuilderError, 0, 3);
            builderLayout.SetColumnSpan(_lblBuilderError, 2);

            _grpBuilder.Controls.Add(_btnAddLayer);
            _grpBuilder.Controls.Add(builderLayout);

            // ── New Layers ──
            _grpNewLayers = CreateGroupBox("New Layers", 180);

            _lstNewLayers = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 8.5f)
            };
            _lstNewLayers.Columns.Add("Name", 120);
            _lstNewLayers.Columns.Add("Entities", 55);
            _lstNewLayers.Columns.Add("Color", 45);
            _lstNewLayers.SmallImageList = _imageList;

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0)
            };

            _btnAssign = CreateSmallButton("Assign ▸");
            _btnAssign.Click += BtnAssign_Click;
            _btnUnassign = CreateSmallButton("Unassign");
            _btnUnassign.Click += (s, e) => _state?.UnassignSelection();
            _btnRemoveLayer = CreateSmallButton("Remove");
            _btnRemoveLayer.Click += BtnRemoveLayer_Click;

            btnPanel.Controls.AddRange(new Control[] { _btnAssign, _btnUnassign, _btnRemoveLayer });

            _grpNewLayers.Controls.Add(_lstNewLayers);
            _grpNewLayers.Controls.Add(btnPanel);

            // Add to main layout
            mainLayout.Controls.Add(_grpSelection);
            mainLayout.Controls.Add(_grpOriginal);
            mainLayout.Controls.Add(_grpBuilder);
            mainLayout.Controls.Add(_grpNewLayers);

            Controls.Add(mainLayout);
        }

        #region Event Handlers

        private void TreeOriginal_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_state == null || e.Node == null) return;
            string layerName = e.Node.Text;
            bool visible = e.Node.Checked;

            if (visible)
                _state.HiddenLayers.Remove(layerName);
            else
                _state.HiddenLayers.Add(layerName);

            _state.RaiseEntitiesChanged();
        }

        private void UpdateBuilderPreview()
        {
            string? type = _cmbLayerType.SelectedItem?.ToString();
            if (type == null) return;

            bool isBackForbidden = Layer.BackPrefixForbidden.Contains(type);
            _chkBackPrefix.Enabled = !isBackForbidden;
            if (isBackForbidden) _chkBackPrefix.Checked = false;

            string name = BuildLayerName();
            var errors = Layer.ValidateName(name);
            _lblBuilderError.Text = errors.Count > 0 ? errors[0] : "";
            _btnAddLayer.Enabled = errors.Count == 0;
        }

        private string BuildLayerName()
        {
            string type = _cmbLayerType.SelectedItem?.ToString() ?? "outline";
            string prefix = _chkBackPrefix.Checked ? "back" : "";
            string suffix = _txtLayerSuffix.Text.Trim();

            string name = prefix + type;
            if (!string.IsNullOrEmpty(suffix))
                name += "_" + suffix;
            return name;
        }

        private void BtnAddLayer_Click(object? sender, EventArgs e)
        {
            if (_state == null) return;
            string name = BuildLayerName();
            var errors = Layer.ValidateName(name);
            if (errors.Count > 0)
            {
                _lblBuilderError.Text = errors[0];
                return;
            }

            _state.AddNewLayer(name, 7);
            _txtLayerSuffix.Clear();
        }

        private void BtnAssign_Click(object? sender, EventArgs e)
        {
            if (_state == null || _lstNewLayers.SelectedItems.Count == 0) return;
            string layerId = _lstNewLayers.SelectedItems[0].Tag?.ToString() ?? "";
            _state.AssignSelectionToLayer(layerId);
        }

        private void BtnRemoveLayer_Click(object? sender, EventArgs e)
        {
            if (_state == null || _lstNewLayers.SelectedItems.Count == 0) return;
            string layerId = _lstNewLayers.SelectedItems[0].Tag?.ToString() ?? "";
            _state.RemoveNewLayer(layerId);
        }

        #endregion

        #region Refresh

        private void RefreshAll()
        {
            if (_state == null || IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshAll));
                return;
            }

            RefreshOriginalLayers();
            RefreshNewLayers();
            RefreshSelectionInfo();
        }

        private void RefreshOriginalLayers()
        {
            if (_state == null) return;

            _treeOriginal.BeginUpdate();
            _treeOriginal.Nodes.Clear();

            foreach (var layer in _state.OriginalLayers)
            {
                int entityCount = _state.Entities.Count(e => e.Layer == layer.Name);
                var node = new TreeNode($"{layer.Name} ({entityCount})")
                {
                    Checked = !_state.HiddenLayers.Contains(layer.Name),
                    ForeColor = AciColorTable.GetDisplayColor(layer.Color),
                    Tag = layer.Name
                };
                _treeOriginal.Nodes.Add(node);
            }

            _treeOriginal.EndUpdate();
        }

        private void RefreshNewLayers()
        {
            if (_state == null) return;

            _lstNewLayers.BeginUpdate();
            _lstNewLayers.Items.Clear();
            _imageList.Images.Clear();

            foreach (var layer in _state.NewLayers)
            {
                int entityCount = _state.Assignments.TryGetValue(layer.Id, out var set) ? set.Count : 0;

                // Create color icon
                var bmp = new Bitmap(12, 12);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(AciColorTable.GetDisplayColor(layer.Color));
                }
                string imgKey = layer.Id;
                _imageList.Images.Add(imgKey, bmp);

                var item = new ListViewItem(new[] { layer.Name, entityCount.ToString(), "" })
                {
                    Tag = layer.Id,
                    ForeColor = AciColorTable.GetDisplayColor(layer.Color),
                    ImageKey = imgKey
                };
                _lstNewLayers.Items.Add(item);
            }

            _lstNewLayers.EndUpdate();
        }

        private void RefreshSelectionInfo()
        {
            if (_state == null || IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshSelectionInfo));
                return;
            }

            int count = _state.SelectedIds.Count;
            if (count == 0)
            {
                _lblSelectionInfo.Text = "No selection";
                _btnGrowChain.Enabled = false;
            }
            else
            {
                var types = _state.GetSelectedEntities()
                    .GroupBy(e => e.EntityType)
                    .Select(g => $"{g.Count()} {g.Key}")
                    .ToList();
                _lblSelectionInfo.Text = $"{count} selected: {string.Join(", ", types)}";
                _btnGrowChain.Enabled = true;
            }

            _btnAssign.Enabled = count > 0 && _lstNewLayers.SelectedItems.Count > 0;
            _btnUnassign.Enabled = count > 0;
        }

        #endregion

        #region Helper Controls

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

        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 8.5f),
                Padding = new Padding(0, 4, 0, 0)
            };
        }

        private Button CreateButton(string text, int topMargin)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Bottom,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 55, 58),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(0, topMargin, 0, 2),
                Cursor = Cursors.Hand
            };
        }

        private Button CreateSmallButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 72,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 55, 58),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 7.5f),
                Margin = new Padding(2, 2, 2, 2),
                Cursor = Cursors.Hand
            };
        }

        #endregion
    }
}
