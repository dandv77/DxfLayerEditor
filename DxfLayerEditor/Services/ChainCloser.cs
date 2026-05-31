using System;
using System.Collections.Generic;
using System.Linq;
using DxfLayerEditor.Algorithms;
using DxfLayerEditor.Models;
using DxfLayerEditor.Utilities;

namespace DxfLayerEditor.Services
{
    /// <summary>
    /// Describes an unresolved gap in a chain after the close pass.
    /// </summary>
    public class OpenGap
    {
        /// <summary>X coordinate of the gap.</summary>
        public double X { get; set; }

        /// <summary>Y coordinate of the gap.</summary>
        public double Y { get; set; }

        /// <summary>Distance of the gap (between the two open endpoints).</summary>
        public double Distance { get; set; }

        /// <summary>Layer name where the gap was found.</summary>
        public string Layer { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of the chain-close pass on export.
    /// </summary>
    public class ChainCloseResult
    {
        /// <summary>Number of gaps that were successfully closed.</summary>
        public int GapsClosed { get; set; }

        /// <summary>List of gaps that could not be automatically resolved.</summary>
        public List<OpenGap> OpenGaps { get; set; } = new();
    }

    /// <summary>
    /// Implements the chain-close algorithm for DXF export.
    /// Detects open endpoints within a threshold, clusters nearby endpoints,
    /// snaps clusters to their centroids, and performs infinite-line extension/trimming
    /// for outline/border/panel layers.
    /// </summary>
    public static class ChainCloser
    {
        /// <summary>
        /// Default snap tolerance for chain closing (half of typical chain detection tolerance).
        /// </summary>
        public const double DefaultSnapTolerance = 5e-5;

        /// <summary>
        /// Runs the chain-close pass on a set of entities grouped by layer.
        /// Modifies entities in place.
        /// </summary>
        /// <param name="entitiesByLayer">
        /// Dictionary of layer name → list of primitive entities on that layer.
        /// Only layers in <see cref="Layer.AutoCloseOnExport"/> are processed.
        /// </param>
        /// <param name="snapTolerance">Maximum distance for endpoint clustering.</param>
        /// <returns>A <see cref="ChainCloseResult"/> with gap statistics.</returns>
        public static ChainCloseResult CloseChains(
            Dictionary<string, List<EntityBase>> entitiesByLayer,
            double snapTolerance = DefaultSnapTolerance)
        {
            var result = new ChainCloseResult();

            foreach (var kvp in entitiesByLayer)
            {
                string layerName = kvp.Key;
                var entities = kvp.Value;

                if (!Layer.ShouldAutoClose(layerName))
                    continue;

                CloseChainInPlace(entities, layerName, snapTolerance, result);
            }

            return result;
        }

        /// <summary>
        /// Closes gaps in a single layer's entities.
        /// 1. Clusters open endpoints within snapTolerance/2.
        /// 2. Snaps each cluster to its centroid.
        /// 3. For LINE↔LINE gap pairs, trims to infinite-line intersection.
        /// </summary>
        /// <param name="entities">Entities on this layer (modified in place).</param>
        /// <param name="layerName">Layer name for gap reporting.</param>
        /// <param name="snapTolerance">Snap tolerance.</param>
        /// <param name="result">Accumulator for gap statistics.</param>
        private static void CloseChainInPlace(
            List<EntityBase> entities,
            string layerName,
            double snapTolerance,
            ChainCloseResult result)
        {
            double clusterTol = snapTolerance / 2.0;

            // Step 1: Find all open endpoints
            var openEndpoints = FindOpenEndpoints(entities, clusterTol);

            if (openEndpoints.Count == 0)
                return;

            // Step 2: Cluster nearby open endpoints using Union-Find
            var clusters = ClusterEndpoints(openEndpoints, snapTolerance);

            // Step 3: Process each cluster
            foreach (var cluster in clusters)
            {
                if (cluster.Count < 2)
                {
                    // Single unmatched endpoint — report as open gap
                    var ep = cluster[0];
                    result.OpenGaps.Add(new OpenGap
                    {
                        X = ep.Position.X,
                        Y = ep.Position.Y,
                        Distance = 0,
                        Layer = layerName
                    });
                    continue;
                }

                // Try LINE↔LINE infinite-line trimming first
                if (cluster.Count == 2 && TryLineLineTrim(cluster[0], cluster[1], snapTolerance))
                {
                    result.GapsClosed++;
                    continue;
                }

                // Fall back to centroid snapping
                var centroid = ComputeCentroid(cluster);
                foreach (var ep in cluster)
                {
                    SnapEndpointTo(ep, centroid);
                }
                result.GapsClosed++;
            }
        }

        /// <summary>
        /// Identifies all "open" endpoints — those that appear exactly once
        /// (not shared with another entity) within the clustering tolerance.
        /// </summary>
        private static List<EndpointRef> FindOpenEndpoints(
            List<EntityBase> entities, double tolerance)
        {
            var buckets = new Dictionary<string, List<EndpointRef>>();

            foreach (var entity in entities)
            {
                var endpoints = entity.GetEndpoints();
                for (int i = 0; i < endpoints.Length; i++)
                {
                    var ep = endpoints[i];
                    string key = ep.RoundToGrid(tolerance).ToKey(10);
                    var epRef = new EndpointRef(entity, i, ep);

                    if (!buckets.ContainsKey(key))
                        buckets[key] = new List<EndpointRef>();
                    buckets[key].Add(epRef);
                }
            }

            var open = new List<EndpointRef>();
            foreach (var bucket in buckets.Values)
            {
                if (bucket.Count == 1)
                    open.Add(bucket[0]);
            }

            return open;
        }

        /// <summary>
        /// Groups nearby open endpoints into clusters using Union-Find.
        /// </summary>
        private static List<List<EndpointRef>> ClusterEndpoints(
            List<EndpointRef> endpoints, double snapTolerance)
        {
            if (endpoints.Count <= 1)
                return endpoints.Count == 1
                    ? new List<List<EndpointRef>> { new() { endpoints[0] } }
                    : new List<List<EndpointRef>>();

            var uf = new UnionFind();

            // Initialize each endpoint in UF
            for (int i = 0; i < endpoints.Count; i++)
                uf.Find(i.ToString());

            // Union endpoints that are within snapTolerance
            for (int i = 0; i < endpoints.Count; i++)
            {
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    double dist = endpoints[i].Position.DistanceTo(endpoints[j].Position);
                    if (dist <= snapTolerance)
                    {
                        uf.Union(i.ToString(), j.ToString());
                    }
                }
            }

            // Build clusters
            var groups = uf.GetGroups();
            var clusters = new List<List<EndpointRef>>();

            foreach (var group in groups.Values)
            {
                var cluster = new List<EndpointRef>();
                foreach (var idx in group)
                {
                    cluster.Add(endpoints[int.Parse(idx)]);
                }
                clusters.Add(cluster);
            }

            return clusters;
        }

        /// <summary>
        /// Attempts to close a gap between two LINE endpoints by computing
        /// the infinite-line intersection and trimming both lines to meet there.
        /// Only succeeds if the intersection is near both gap endpoints.
        /// </summary>
        private static bool TryLineLineTrim(EndpointRef a, EndpointRef b, double tolerance)
        {
            if (a.Entity is not LineEntity lineA || b.Entity is not LineEntity lineB)
                return false;

            if (!GeometryMath.LineLineIntersection(
                    lineA.Start, lineA.End,
                    lineB.Start, lineB.End,
                    out Point2D intersection))
                return false;

            // Check that the intersection is near both gap endpoints
            double distA = intersection.DistanceTo(a.Position);
            double distB = intersection.DistanceTo(b.Position);

            if (distA > tolerance * 10 || distB > tolerance * 10)
                return false;

            // Trim both lines to the intersection
            SnapEndpointTo(a, intersection);
            SnapEndpointTo(b, intersection);

            return true;
        }

        /// <summary>
        /// Computes the centroid of a cluster of endpoint references.
        /// </summary>
        private static Point2D ComputeCentroid(List<EndpointRef> cluster)
        {
            double sumX = 0, sumY = 0;
            foreach (var ep in cluster)
            {
                sumX += ep.Position.X;
                sumY += ep.Position.Y;
            }
            return new Point2D(sumX / cluster.Count, sumY / cluster.Count);
        }

        /// <summary>
        /// Snaps an endpoint reference to a new position, modifying the entity in place.
        /// </summary>
        private static void SnapEndpointTo(EndpointRef epRef, Point2D target)
        {
            switch (epRef.Entity)
            {
                case LineEntity line:
                    if (epRef.EndpointIndex == 0)
                        line.Start = target;
                    else
                        line.End = target;
                    break;

                case ArcEntity arc:
                    // For arcs, adjust the start or end angle to match the target point
                    double newAngle = Math.Atan2(
                        target.Y - arc.Center.Y,
                        target.X - arc.Center.X) * 180.0 / Math.PI;
                    newAngle = ArcEntity.NormalizeAngle(newAngle);

                    if (epRef.EndpointIndex == 0)
                        arc.StartAngle = newAngle;
                    else
                        arc.EndAngle = newAngle;
                    break;
            }
        }

        /// <summary>
        /// Internal reference to a specific endpoint of a specific entity.
        /// </summary>
        private class EndpointRef
        {
            public EntityBase Entity { get; }
            public int EndpointIndex { get; }
            public Point2D Position { get; }

            public EndpointRef(EntityBase entity, int endpointIndex, Point2D position)
            {
                Entity = entity;
                EndpointIndex = endpointIndex;
                Position = position;
            }
        }
    }
}
