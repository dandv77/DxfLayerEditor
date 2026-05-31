using System;
using System.Collections.Generic;
using System.Linq;
using DxfLayerEditor.Algorithms;
using DxfLayerEditor.Models;

namespace DxfLayerEditor.Services
{
    /// <summary>
    /// Active tool modes for the editor.
    /// </summary>
    public enum ToolMode
    {
        Select,
        Move,
        Trim,
        Snap,
        PointToCircle
    }

    /// <summary>
    /// Move tool phases.
    /// </summary>
    public enum MovePhase
    {
        /// <summary>No move in progress.</summary>
        Idle,
        /// <summary>User is picking which endpoint to move.</summary>
        PickEndpoint,
        /// <summary>User is hovering to find a snap target.</summary>
        PickTarget
    }

    /// <summary>
    /// Represents the state of a move operation in progress.
    /// </summary>
    public class MoveState
    {
        /// <summary>Entity being moved.</summary>
        public string? EntityId { get; set; }
        /// <summary>Which endpoint: "start" or "end".</summary>
        public string? Which { get; set; }
        /// <summary>Current phase of the move.</summary>
        public MovePhase Phase { get; set; } = MovePhase.Idle;
    }

    /// <summary>
    /// Represents a single undoable/redoable state snapshot.
    /// </summary>
    public class UndoSnapshot
    {
        public string Description { get; set; } = "";
        public List<EntityBase> Entities { get; set; } = new();
        public List<Layer> NewLayers { get; set; } = new();
        public Dictionary<string, HashSet<string>> Assignments { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for state change notifications.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        public string Property { get; set; } = "";
    }

    /// <summary>
    /// Central state management for the DXF Layer Editor.
    /// Manages entities, layers, selection, tools, undo/redo, and file info.
    /// All UI components observe this state via events.
    /// </summary>
    public class EditorState
    {
        #region Events

        /// <summary>Raised when any editor state changes.</summary>
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>Raised when entity selection changes.</summary>
        public event EventHandler? SelectionChanged;

        /// <summary>Manually raises the SelectionChanged event.</summary>
        public void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>Raised when the active tool changes.</summary>
        public event EventHandler? ToolChanged;

        /// <summary>Raised when entities are modified (requires repaint).</summary>
        public event EventHandler? EntitiesChanged;

        /// <summary>Manually raises the EntitiesChanged event.</summary>
        public void RaiseEntitiesChanged() => EntitiesChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>Raised when layer structure changes.</summary>
        public event EventHandler? LayersChanged;

        /// <summary>Raised when a file is loaded or cleared.</summary>
        public event EventHandler? FileChanged;

        /// <summary>Raised when undo/redo stack changes.</summary>
        public event EventHandler? UndoRedoChanged;

        #endregion

        #region File State

        /// <summary>Currently loaded file path.</summary>
        public string? FilePath { get; private set; }

        /// <summary>Display filename.</summary>
        public string FileName => FilePath != null ? System.IO.Path.GetFileName(FilePath) : "(none)";

        /// <summary>Whether the document has unsaved changes.</summary>
        public bool IsDirty { get; private set; }

        #endregion

        #region Entity State

        /// <summary>All entities in the current document.</summary>
        public List<EntityBase> Entities { get; private set; } = new();

        /// <summary>Original DXF layers.</summary>
        public List<Layer> OriginalLayers { get; private set; } = new();

        /// <summary>User-created target layers.</summary>
        public List<Layer> NewLayers { get; private set; } = new();

        /// <summary>
        /// Assignments: new layer ID → set of entity IDs assigned to it.
        /// </summary>
        public Dictionary<string, HashSet<string>> Assignments { get; private set; } = new();

        /// <summary>Hidden layer names (both original and new).</summary>
        public HashSet<string> HiddenLayers { get; private set; } = new();

        /// <summary>Detected chains.</summary>
        public List<ChainGroup> Chains { get; private set; } = new();

        /// <summary>Document bounding box.</summary>
        public (double MinX, double MinY, double MaxX, double MaxY) Bounds { get; private set; }

        /// <summary>Skipped entity type names during parsing.</summary>
        public List<string> SkippedTypes { get; private set; } = new();

        #endregion

        #region Selection State

        /// <summary>Currently selected entity IDs.</summary>
        public HashSet<string> SelectedIds { get; private set; } = new();

        /// <summary>Entity under the mouse cursor (hover highlight).</summary>
        public string? HoveredEntityId { get; private set; }

        #endregion

        #region Tool State

        /// <summary>Currently active tool.</summary>
        public ToolMode ActiveTool { get; private set; } = ToolMode.Select;

        /// <summary>State of the move tool.</summary>
        public MoveState MoveState { get; set; } = new();

        /// <summary>Chain detection tolerance.</summary>
        public double ChainTolerance { get; set; } = 1e-4;

        /// <summary>Display units: "in" or "mm".</summary>
        public string Units { get; set; } = "in";

        /// <summary>Whether snap is enabled.</summary>
        public bool SnapEnabled { get; set; } = true;

        /// <summary>Snap tolerance in world units.</summary>
        public double SnapTolerance { get; set; } = 0.01;

        /// <summary>Chord tolerance for spline/ellipse export flattening.</summary>
        public double ChordTolerance { get; set; } = 1e-3;

        #endregion

        #region Export State

        /// <summary>Export filename.</summary>
        public string ExportFileName { get; set; } = "";

        /// <summary>Mirror X on export.</summary>
        public bool MirrorX { get; set; }

        /// <summary>Mirror Y on export.</summary>
        public bool MirrorY { get; set; }

        /// <summary>Scale factor on export.</summary>
        public double ExportScale { get; set; } = 1.0;

        /// <summary>Last export gap statistics.</summary>
        public ChainCloseResult? LastExportResult { get; set; }

        #endregion

        #region Undo/Redo

        private readonly Stack<UndoSnapshot> _undoStack = new();
        private readonly Stack<UndoSnapshot> _redoStack = new();
        private const int MaxUndoDepth = 50;

        /// <summary>Whether an undo operation is available.</summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>Whether a redo operation is available.</summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Pushes the current state onto the undo stack.
        /// Call before making a mutation.
        /// </summary>
        public void PushUndo(string description)
        {
            _undoStack.Push(CreateSnapshot(description));
            _redoStack.Clear();

            // Trim old entries
            if (_undoStack.Count > MaxUndoDepth)
            {
                var temp = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = 0; i < MaxUndoDepth; i++)
                    _undoStack.Push(temp[i]);
            }

            IsDirty = true;
            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Undoes the last operation.</summary>
        public void Undo()
        {
            if (!CanUndo) return;

            _redoStack.Push(CreateSnapshot("Redo"));
            var snapshot = _undoStack.Pop();
            RestoreSnapshot(snapshot);

            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
            LayersChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Redoes the last undone operation.</summary>
        public void Redo()
        {
            if (!CanRedo) return;

            _undoStack.Push(CreateSnapshot("Undo"));
            var snapshot = _redoStack.Pop();
            RestoreSnapshot(snapshot);

            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
            LayersChanged?.Invoke(this, EventArgs.Empty);
        }

        private UndoSnapshot CreateSnapshot(string desc)
        {
            // Deep copy entities (simplified — copies key properties)
            var snap = new UndoSnapshot { Description = desc };
            // For now, store references — full deep clone would be needed for production
            snap.NewLayers = NewLayers.Select(l => new Layer
            {
                Id = l.Id, Name = l.Name, Color = l.Color, IsVisible = l.IsVisible
            }).ToList();
            snap.Assignments = Assignments.ToDictionary(
                kvp => kvp.Key,
                kvp => new HashSet<string>(kvp.Value));
            return snap;
        }

        private void RestoreSnapshot(UndoSnapshot snap)
        {
            NewLayers = snap.NewLayers;
            Assignments = snap.Assignments;
        }

        #endregion

        #region File Operations

        /// <summary>
        /// Loads a new set of entities into the editor, replacing any existing document.
        /// </summary>
        public void LoadDocument(
            string filePath,
            List<EntityBase> entities,
            List<Layer> originalLayers,
            List<string> skippedTypes)
        {
            FilePath = filePath;
            Entities = entities;
            OriginalLayers = originalLayers;
            SkippedTypes = skippedTypes;
            NewLayers.Clear();
            Assignments.Clear();
            HiddenLayers.Clear();
            SelectedIds.Clear();
            HoveredEntityId = null;
            IsDirty = false;
            _undoStack.Clear();
            _redoStack.Clear();

            // Compute bounds
            RecomputeBounds();

            // Detect chains
            RedetectChains();

            // Set export filename
            ExportFileName = System.IO.Path.GetFileNameWithoutExtension(filePath) + "_export.dxf";

            FileChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
            LayersChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Clears the current document.</summary>
        public void ClearDocument()
        {
            FilePath = null;
            Entities.Clear();
            OriginalLayers.Clear();
            NewLayers.Clear();
            Assignments.Clear();
            HiddenLayers.Clear();
            SelectedIds.Clear();
            Chains.Clear();
            HoveredEntityId = null;
            IsDirty = false;
            _undoStack.Clear();
            _redoStack.Clear();
            LastExportResult = null;
            Bounds = (0, 0, 0, 0);

            FileChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
            LayersChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            UndoRedoChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Selection

        /// <summary>Selects a single entity, clearing previous selection.</summary>
        public void SelectEntity(string entityId)
        {
            SelectedIds.Clear();
            SelectedIds.Add(entityId);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Toggles an entity in the selection (Ctrl-click).</summary>
        public void ToggleSelection(string entityId)
        {
            if (!SelectedIds.Remove(entityId))
                SelectedIds.Add(entityId);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Adds all entities in the same chain to the selection (Shift-click).</summary>
        public void AddChainToSelection(string entityId)
        {
            var chainIds = ChainDetection.GrowToChain(entityId, Entities, ChainTolerance);
            foreach (var id in chainIds)
                SelectedIds.Add(id);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Selects the entire chain of an entity (double-click).</summary>
        public void SelectChain(string entityId)
        {
            SelectedIds.Clear();
            var chainIds = ChainDetection.GrowToChain(entityId, Entities, ChainTolerance);
            foreach (var id in chainIds)
                SelectedIds.Add(id);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Grows the current selection to include full chains.</summary>
        public void GrowSelectionToChains()
        {
            var toAdd = new HashSet<string>();
            foreach (var id in SelectedIds)
            {
                var chainIds = ChainDetection.GrowToChain(id, Entities, ChainTolerance);
                foreach (var cid in chainIds)
                    toAdd.Add(cid);
            }
            foreach (var id in toAdd)
                SelectedIds.Add(id);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Clears all selection.</summary>
        public void ClearSelection()
        {
            SelectedIds.Clear();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Sets the hovered entity ID.</summary>
        public void SetHovered(string? entityId)
        {
            if (HoveredEntityId != entityId)
            {
                HoveredEntityId = entityId;
                StateChanged?.Invoke(this, new StateChangedEventArgs { Property = "Hovered" });
            }
        }

        /// <summary>Returns the selected entities.</summary>
        public IEnumerable<EntityBase> GetSelectedEntities()
        {
            return Entities.Where(e => SelectedIds.Contains(e.Id));
        }

        #endregion

        #region Tool Operations

        /// <summary>Sets the active tool mode.</summary>
        public void SetTool(ToolMode tool)
        {
            if (ActiveTool != tool)
            {
                ActiveTool = tool;
                MoveState = new MoveState();
                ToolChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Cancels any in-progress tool operation.</summary>
        public void CancelTool()
        {
            MoveState = new MoveState();
            ToolChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Layer Operations

        /// <summary>Creates a new target layer.</summary>
        public Layer AddNewLayer(string name, int color = 7)
        {
            PushUndo("Add layer");
            var layer = new Layer { Name = name, Color = color };
            NewLayers.Add(layer);
            Assignments[layer.Id] = new HashSet<string>();
            LayersChanged?.Invoke(this, EventArgs.Empty);
            return layer;
        }

        /// <summary>Removes a target layer and unassigns its entities.</summary>
        public void RemoveNewLayer(string layerId)
        {
            PushUndo("Remove layer");
            NewLayers.RemoveAll(l => l.Id == layerId);
            Assignments.Remove(layerId);
            LayersChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Renames a target layer.</summary>
        public void RenameNewLayer(string layerId, string newName)
        {
            var layer = NewLayers.FirstOrDefault(l => l.Id == layerId);
            if (layer == null) return;
            PushUndo("Rename layer");
            layer.Name = newName;
            LayersChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Sets the color of a target layer.</summary>
        public void SetLayerColor(string layerId, int aciColor)
        {
            var layer = NewLayers.FirstOrDefault(l => l.Id == layerId);
            if (layer == null) return;
            PushUndo("Change layer color");
            layer.Color = aciColor;
            LayersChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Assigns the current selection to a target layer.</summary>
        public void AssignSelectionToLayer(string layerId)
        {
            if (SelectedIds.Count == 0) return;
            if (!Assignments.ContainsKey(layerId)) return;

            PushUndo("Assign to layer");

            // Remove from any other layer first
            foreach (var kvp in Assignments)
            {
                foreach (var id in SelectedIds)
                    kvp.Value.Remove(id);
            }

            // Add to target layer
            foreach (var id in SelectedIds)
                Assignments[layerId].Add(id);

            IsDirty = true;
            LayersChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Unassigns selected entities from all target layers.</summary>
        public void UnassignSelection()
        {
            if (SelectedIds.Count == 0) return;
            PushUndo("Unassign");

            foreach (var kvp in Assignments)
            {
                foreach (var id in SelectedIds)
                    kvp.Value.Remove(id);
            }

            IsDirty = true;
            LayersChanged?.Invoke(this, EventArgs.Empty);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Toggles visibility of a layer.</summary>
        public void ToggleLayerVisibility(string layerName)
        {
            if (!HiddenLayers.Remove(layerName))
                HiddenLayers.Add(layerName);
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Gets the assigned layer for an entity, if any.</summary>
        public Layer? GetAssignedLayer(string entityId)
        {
            foreach (var kvp in Assignments)
            {
                if (kvp.Value.Contains(entityId))
                    return NewLayers.FirstOrDefault(l => l.Id == kvp.Key);
            }
            return null;
        }

        #endregion

        #region Geometry Operations

        /// <summary>Re-runs chain detection on all entities.</summary>
        public void RedetectChains()
        {
            Chains = ChainDetection.DetectChains(Entities, ChainTolerance);
        }

        /// <summary>Recomputes the document bounding box.</summary>
        public void RecomputeBounds()
        {
            if (Entities.Count == 0)
            {
                Bounds = (0, 0, 0, 0);
                return;
            }

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var e in Entities)
            {
                var (eMinX, eMinY, eMaxX, eMaxY) = e.GetBounds();
                minX = Math.Min(minX, eMinX);
                minY = Math.Min(minY, eMinY);
                maxX = Math.Max(maxX, eMaxX);
                maxY = Math.Max(maxY, eMaxY);
            }

            Bounds = (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Determines if an entity is visible based on layer visibility settings.
        /// </summary>
        public bool IsEntityVisible(EntityBase entity)
        {
            // Check original layer visibility
            if (HiddenLayers.Contains(entity.Layer))
                return false;

            // Check assigned layer visibility
            var assigned = GetAssignedLayer(entity.Id);
            if (assigned != null && HiddenLayers.Contains(assigned.Name))
                return false;

            return true;
        }

        /// <summary>
        /// Converts selected POINT entities to CIRCLE entities.
        /// </summary>
        public int ConvertPointsToCircles(double diameter)
        {
            PushUndo("Points to circles");
            int converted = 0;

            var pointIds = SelectedIds
                .Where(id => Entities.Any(e => e.Id == id && e is PointEntity))
                .ToList();

            foreach (var pid in pointIds)
            {
                int idx = Entities.FindIndex(e => e.Id == pid);
                if (idx >= 0 && Entities[idx] is PointEntity point)
                {
                    var circle = point.ToCircle(diameter);
                    Entities[idx] = circle;
                    converted++;
                }
            }

            if (converted > 0)
            {
                IsDirty = true;
                RedetectChains();
                EntitiesChanged?.Invoke(this, EventArgs.Empty);
            }

            return converted;
        }

        #endregion

        #region Notify Helpers

        private void NotifyStateChanged(string property)
        {
            StateChanged?.Invoke(this, new StateChangedEventArgs { Property = property });
        }

        #endregion
    }
}
