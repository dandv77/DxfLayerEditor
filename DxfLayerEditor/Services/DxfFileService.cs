using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DxfLayerEditor.Models;

namespace DxfLayerEditor.Services
{
    /// <summary>
    /// Handles reading and writing DXF files using the netDxf library.
    /// Converts between netDxf entity types and the internal editor models.
    /// </summary>
    public class DxfFileService
    {
        /// <summary>
        /// Parses a DXF file into the internal entity model.
        /// </summary>
        /// <param name="filePath">Path to the DXF file.</param>
        /// <returns>Tuple of (entities, layers, skippedTypes).</returns>
        public (List<EntityBase> Entities, List<Layer> Layers, List<string> SkippedTypes) LoadDxf(string filePath)
        {
            var entities = new List<EntityBase>();
            var layers = new List<Layer>();
            var skippedTypes = new HashSet<string>();

            try
            {
                var doc = netDxf.DxfDocument.Load(filePath);
                if (doc == null)
                    throw new InvalidOperationException($"Failed to load DXF file: {filePath}");

                // Extract layers
                foreach (var dxfLayer in doc.Layers)
                {
                    layers.Add(new Layer
                    {
                        Name = dxfLayer.Name,
                        Color = dxfLayer.Color.Index,
                        IsVisible = dxfLayer.IsVisible
                    });
                }

                // Parse modelspace entities
                foreach (var dxfEntity in doc.Entities.Lines)
                {
                    entities.Add(ConvertLine(dxfEntity));
                }

                foreach (var dxfEntity in doc.Entities.Arcs)
                {
                    entities.Add(ConvertArc(dxfEntity));
                }

                foreach (var dxfEntity in doc.Entities.Circles)
                {
                    entities.Add(ConvertCircle(dxfEntity));
                }

                foreach (var dxfEntity in doc.Entities.Points)
                {
                    entities.Add(ConvertPoint(dxfEntity));
                }

                foreach (var dxfEntity in doc.Entities.Polylines2D)
                {
                    entities.Add(ConvertPolyline2D(dxfEntity));
                }

                foreach (var dxfEntity in doc.Entities.Ellipses)
                {
                    entities.Add(ConvertEllipse(dxfEntity));
                    // Note: Ellipses are stored as PolylineEntity approximations
                }

                // Track skipped types
                if (doc.Entities.Dimensions.Count() > 0) skippedTypes.Add("DIMENSION");
                if (doc.Entities.Hatches.Count() > 0) skippedTypes.Add("HATCH");
                if (doc.Entities.MLines.Count() > 0) skippedTypes.Add("MLINE");
                if (doc.Entities.Splines.Count() > 0)
                {
                    foreach (var spline in doc.Entities.Splines)
                    {
                        entities.Add(ConvertSpline(spline));
                    }
                }

                // TEXT and MTEXT — store as skipped for now (explode via dialog)
                if (doc.Entities.Texts.Count() > 0) skippedTypes.Add("TEXT");
                if (doc.Entities.MTexts.Count() > 0) skippedTypes.Add("MTEXT");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing DXF file: {ex.Message}", ex);
            }

            return (entities, layers, skippedTypes.ToList());
        }

        /// <summary>
        /// Exports entities to a new DXF file with the given layer assignments.
        /// </summary>
        public void ExportDxf(
            string outputPath,
            List<EntityBase> entities,
            Dictionary<string, HashSet<string>> assignments,
            List<Layer> newLayers,
            bool mirrorX = false,
            bool mirrorY = false,
            double scale = 1.0)
        {
            var doc = new netDxf.DxfDocument();

            // Create layers
            foreach (var layer in newLayers)
            {
                if (!doc.Layers.Contains(layer.Name))
                {
                    var dxfLayer = new netDxf.Tables.Layer(layer.Name)
                    {
                        Color = new netDxf.AciColor((short)layer.Color)
                    };
                    doc.Layers.Add(dxfLayer);
                }
            }

            // Build entity-to-layer map
            var entityLayerMap = new Dictionary<string, string>();
            foreach (var kvp in assignments)
            {
                var layer = newLayers.FirstOrDefault(l => l.Id == kvp.Key);
                if (layer == null) continue;
                foreach (var eid in kvp.Value)
                    entityLayerMap[eid] = layer.Name;
            }

            // Decompose and emit entities
            foreach (var entity in entities)
            {
                if (!entityLayerMap.TryGetValue(entity.Id, out string? targetLayer))
                    continue;

                var decomposed = GeometryDecomposer.Decompose(entity);
                foreach (var prim in decomposed)
                {
                    EmitEntity(doc, prim, targetLayer, mirrorX, mirrorY, scale);
                }
            }

            doc.Save(outputPath);
        }

        #region Entity Conversion (netDxf → Internal Models)

        private LineEntity ConvertLine(netDxf.Entities.Line dxf)
        {
            return new LineEntity(
                new Point2D(dxf.StartPoint.X, dxf.StartPoint.Y),
                new Point2D(dxf.EndPoint.X, dxf.EndPoint.Y))
            {
                Layer = dxf.Layer.Name,
                Color = GetEntityColor(dxf)
            };
        }

        private ArcEntity ConvertArc(netDxf.Entities.Arc dxf)
        {
            return new ArcEntity(
                new Point2D(dxf.Center.X, dxf.Center.Y),
                dxf.Radius,
                dxf.StartAngle,
                dxf.EndAngle)
            {
                Layer = dxf.Layer.Name,
                Color = GetEntityColor(dxf)
            };
        }

        private CircleEntity ConvertCircle(netDxf.Entities.Circle dxf)
        {
            return new CircleEntity(
                new Point2D(dxf.Center.X, dxf.Center.Y),
                dxf.Radius)
            {
                Layer = dxf.Layer.Name,
                Color = GetEntityColor(dxf)
            };
        }

        private PointEntity ConvertPoint(netDxf.Entities.Point dxf)
        {
            return new PointEntity(
                new Point2D(dxf.Position.X, dxf.Position.Y))
            {
                Layer = dxf.Layer.Name,
                Color = GetEntityColor(dxf)
            };
        }

        private PolylineEntity ConvertPolyline2D(netDxf.Entities.Polyline2D dxf)
        {
            var entity = new PolylineEntity
            {
                Layer = dxf.Layer.Name,
                Color = GetEntityColor(dxf),
                IsClosed = dxf.IsClosed
            };

            foreach (var v in dxf.Vertexes)
            {
                entity.Vertices.Add(new PolylineVertex(
                    new Point2D(v.Position.X, v.Position.Y),
                    v.Bulge));
            }

            return entity;
        }

        private PolylineEntity ConvertEllipse(netDxf.Entities.Ellipse dxf)
        {
            // Approximate ellipse as a polyline with sampled points
            var entity = new PolylineEntity
            {
                Layer = dxf.Layer.Name,
                Color = GetEntityColor(dxf),
                IsClosed = true
            };

            double majorLen = dxf.MajorAxis;
            double minorLen = dxf.MinorAxis;
            double rotation = dxf.Rotation * Math.PI / 180.0;

            int segments = 72;
            double startParam = dxf.StartAngle * Math.PI / 180.0;
            double endParam = dxf.EndAngle * Math.PI / 180.0;
            if (endParam <= startParam) endParam += 2 * Math.PI;

            double step = (endParam - startParam) / segments;

            for (int i = 0; i <= segments; i++)
            {
                double param = startParam + i * step;
                double ex = majorLen * Math.Cos(param);
                double ey = minorLen * Math.Sin(param);

                double rx = ex * Math.Cos(rotation) - ey * Math.Sin(rotation);
                double ry = ex * Math.Sin(rotation) + ey * Math.Cos(rotation);

                entity.Vertices.Add(new PolylineVertex(
                    dxf.Center.X + rx,
                    dxf.Center.Y + ry));
            }

            return entity;
        }

        private PolylineEntity ConvertSpline(netDxf.Entities.Spline dxf)
        {
            var entity = new PolylineEntity
            {
                Layer = dxf.Layer.Name,
                Color = GetEntityColor(dxf),
                IsClosed = dxf.IsClosed
            };

            // Use fit points if available, otherwise control points as approximation
            if (dxf.FitPoints != null && dxf.FitPoints.Count > 0)
            {
                foreach (var pt in dxf.FitPoints)
                    entity.Vertices.Add(new PolylineVertex(pt.X, pt.Y));
            }
            else if (dxf.ControlPoints != null && dxf.ControlPoints.Length > 0)
            {
                foreach (var cp in dxf.ControlPoints)
                    entity.Vertices.Add(new PolylineVertex(cp.X, cp.Y));
            }

            return entity;
        }

        private int GetEntityColor(netDxf.Entities.EntityObject entity)
        {
            if (entity.Color.IsByLayer)
                return entity.Layer.Color.Index;
            return entity.Color.Index;
        }

        #endregion

        #region Entity Emission (Internal Models → netDxf)

        private void EmitEntity(
            netDxf.DxfDocument doc,
            EntityBase entity,
            string layerName,
            bool mirrorX, bool mirrorY, double scale)
        {
            switch (entity)
            {
                case LineEntity line:
                    var pt1 = Transform(line.Start, mirrorX, mirrorY, scale);
                    var pt2 = Transform(line.End, mirrorX, mirrorY, scale);
                    var dxfLine = new netDxf.Entities.Line(
                        new netDxf.Vector3(pt1.X, pt1.Y, 0),
                        new netDxf.Vector3(pt2.X, pt2.Y, 0));
                    dxfLine.Layer = doc.Layers[layerName];
                    doc.Entities.Add(dxfLine);
                    break;

                case ArcEntity arc:
                    var center = Transform(arc.Center, mirrorX, mirrorY, scale);
                    double radius = arc.Radius * scale;
                    double startAngle = arc.StartAngle;
                    double endAngle = arc.EndAngle;

                    if (mirrorX)
                    {
                        startAngle = 180 - startAngle;
                        endAngle = 180 - endAngle;
                        (startAngle, endAngle) = (endAngle, startAngle);
                    }
                    if (mirrorY)
                    {
                        startAngle = -startAngle;
                        endAngle = -endAngle;
                        (startAngle, endAngle) = (endAngle, startAngle);
                    }
                    startAngle = ArcEntity.NormalizeAngle(startAngle);
                    endAngle = ArcEntity.NormalizeAngle(endAngle);

                    var dxfArc = new netDxf.Entities.Arc(
                        new netDxf.Vector3(center.X, center.Y, 0),
                        radius, startAngle, endAngle);
                    dxfArc.Layer = doc.Layers[layerName];
                    doc.Entities.Add(dxfArc);
                    break;

                case CircleEntity circle:
                    var cc = Transform(circle.Center, mirrorX, mirrorY, scale);
                    var dxfCircle = new netDxf.Entities.Circle(
                        new netDxf.Vector3(cc.X, cc.Y, 0),
                        circle.Radius * scale);
                    dxfCircle.Layer = doc.Layers[layerName];
                    doc.Entities.Add(dxfCircle);
                    break;

                case PointEntity point:
                    var pp = Transform(point.Position, mirrorX, mirrorY, scale);
                    var dxfPoint = new netDxf.Entities.Point(
                        new netDxf.Vector3(pp.X, pp.Y, 0));
                    dxfPoint.Layer = doc.Layers[layerName];
                    doc.Entities.Add(dxfPoint);
                    break;
            }
        }

        private Point2D Transform(Point2D pt, bool mirrorX, bool mirrorY, double scale)
        {
            double x = pt.X * scale;
            double y = pt.Y * scale;
            if (mirrorX) x = -x;
            if (mirrorY) y = -y;
            return new Point2D(x, y);
        }

        #endregion
    }
}
