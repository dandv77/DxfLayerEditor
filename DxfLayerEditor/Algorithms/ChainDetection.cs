using System;
using System.Collections.Generic;
using System.Linq;
using DxfLayerEditor.Models;

namespace DxfLayerEditor.Algorithms
{
    /// <summary>
    /// Union-Find (Disjoint Set Union) data structure with path compression
    /// and union by rank for efficient chain detection.
    /// </summary>
    public class UnionFind
    {
        private readonly Dictionary<string, string> _parent = new();
        private readonly Dictionary<string, int> _rank = new();

        /// <summary>
        /// Finds the root representative of the set containing <paramref name="x"/>.
        /// Uses path compression for amortized near-constant time.
        /// </summary>
        /// <param name="x">Element to find.</param>
        /// <returns>Root representative of the set.</returns>
        public string Find(string x)
        {
            if (!_parent.ContainsKey(x))
            {
                _parent[x] = x;
                _rank[x] = 0;
            }

            if (_parent[x] != x)
            {
                _parent[x] = Find(_parent[x]); // Path compression
            }

            return _parent[x];
        }

        /// <summary>
        /// Merges the sets containing <paramref name="a"/> and <paramref name="b"/>.
        /// Uses union by rank to keep the tree balanced.
        /// </summary>
        /// <param name="a">First element.</param>
        /// <param name="b">Second element.</param>
        public void Union(string a, string b)
        {
            string rootA = Find(a);
            string rootB = Find(b);

            if (rootA == rootB) return;

            int rankA = _rank.GetValueOrDefault(rootA, 0);
            int rankB = _rank.GetValueOrDefault(rootB, 0);

            if (rankA < rankB)
            {
                _parent[rootA] = rootB;
            }
            else if (rankA > rankB)
            {
                _parent[rootB] = rootA;
            }
            else
            {
                _parent[rootB] = rootA;
                _rank[rootA] = rankA + 1;
            }
        }

        /// <summary>
        /// Checks if two elements are in the same set.
        /// </summary>
        public bool Connected(string a, string b)
        {
            return Find(a) == Find(b);
        }

        /// <summary>
        /// Returns all distinct groups as a dictionary of root → member list.
        /// </summary>
        public Dictionary<string, List<string>> GetGroups()
        {
            var groups = new Dictionary<string, List<string>>();
            foreach (string key in _parent.Keys)
            {
                string root = Find(key);
                if (!groups.ContainsKey(root))
                    groups[root] = new List<string>();
                groups[root].Add(key);
            }
            return groups;
        }
    }

    /// <summary>
    /// Detects connected chains among DXF entities using Union-Find on rounded endpoints.
    /// Two entities are in the same chain if they share a rounded endpoint.
    /// </summary>
    public static class ChainDetection
    {
        /// <summary>
        /// Assigns chain IDs to all entities based on endpoint connectivity.
        /// </summary>
        /// <param name="entities">The entities to group into chains.</param>
        /// <param name="tolerance">
        /// Endpoint clustering tolerance. Endpoints within this distance
        /// are considered connected. Default is 1e-4.
        /// </param>
        /// <returns>
        /// A list of <see cref="ChainGroup"/> objects, each containing
        /// the entity IDs belonging to that chain.
        /// </returns>
        public static List<ChainGroup> DetectChains(IList<EntityBase> entities, double tolerance = 1e-4)
        {
            var uf = new UnionFind();

            // Map: rounded endpoint key → first entity ID encountered at that endpoint
            var endpointToEntity = new Dictionary<string, string>();

            foreach (var entity in entities)
            {
                var endpoints = entity.GetEndpoints();
                if (endpoints.Length == 0)
                    continue; // Circles, closed polylines — standalone

                // Ensure this entity exists in the UF
                uf.Find(entity.Id);

                foreach (var ep in endpoints)
                {
                    string key = RoundEndpoint(ep, tolerance);

                    if (endpointToEntity.TryGetValue(key, out string? existingEntityId))
                    {
                        // This endpoint is shared — union the two entities
                        uf.Union(entity.Id, existingEntityId);
                    }
                    else
                    {
                        endpointToEntity[key] = entity.Id;
                    }
                }
            }

            // Build chain groups from union-find results
            var groupMap = new Dictionary<string, ChainGroup>();

            foreach (var entity in entities)
            {
                var endpoints = entity.GetEndpoints();
                string root;

                if (endpoints.Length == 0)
                {
                    // Standalone entity — its own chain
                    root = entity.Id;
                }
                else
                {
                    root = uf.Find(entity.Id);
                }

                if (!groupMap.ContainsKey(root))
                {
                    groupMap[root] = new ChainGroup
                    {
                        ChainId = $"chain-{Guid.NewGuid():N}"
                    };
                }

                groupMap[root].EntityIds.Add(entity.Id);
                entity.ChainId = groupMap[root].ChainId;
            }

            return groupMap.Values.ToList();
        }

        /// <summary>
        /// Re-runs chain detection, updating the <see cref="EntityBase.ChainId"/>
        /// property on every entity. Convenience wrapper around <see cref="DetectChains"/>.
        /// </summary>
        /// <param name="entities">All entities in the document.</param>
        /// <param name="tolerance">Endpoint clustering tolerance.</param>
        public static void ReassignChainIds(IList<EntityBase> entities, double tolerance = 1e-4)
        {
            DetectChains(entities, tolerance);
        }

        /// <summary>
        /// Given an entity ID, returns all entity IDs in the same chain.
        /// Useful for "grow to chain" selection.
        /// </summary>
        /// <param name="entityId">The seed entity ID.</param>
        /// <param name="entities">All entities.</param>
        /// <param name="tolerance">Endpoint clustering tolerance.</param>
        /// <returns>Set of entity IDs in the same chain.</returns>
        public static HashSet<string> GrowToChain(string entityId, IList<EntityBase> entities, double tolerance = 1e-4)
        {
            var chains = DetectChains(entities, tolerance);
            var target = entities.FirstOrDefault(e => e.Id == entityId);
            if (target == null)
                return new HashSet<string>();

            var chain = chains.FirstOrDefault(c => c.EntityIds.Contains(entityId));
            return chain != null
                ? new HashSet<string>(chain.EntityIds)
                : new HashSet<string> { entityId };
        }

        /// <summary>
        /// Checks whether the entities with the given IDs form a closed chain.
        /// </summary>
        /// <param name="entityIds">IDs to check.</param>
        /// <param name="entities">All entities.</param>
        /// <param name="tolerance">Endpoint clustering tolerance.</param>
        /// <returns>True if the selected entities form one or more closed chains.</returns>
        public static bool IsSelectionClosed(IEnumerable<string> entityIds, IList<EntityBase> entities, double tolerance = 1e-4)
        {
            var idSet = new HashSet<string>(entityIds);
            var selected = entities.Where(e => idSet.Contains(e.Id)).ToList();
            var chains = DetectChains(selected, tolerance);

            return chains.All(c =>
            {
                var chainEntities = selected.Where(e => c.EntityIds.Contains(e.Id));
                return c.IsClosed(chainEntities, tolerance);
            });
        }

        /// <summary>
        /// Rounds an endpoint to a grid-aligned key string for dictionary lookups.
        /// This is the core of the endpoint clustering logic.
        /// </summary>
        /// <param name="point">The endpoint to round.</param>
        /// <param name="tolerance">Grid cell size.</param>
        /// <returns>A string key representing the rounded endpoint.</returns>
        private static string RoundEndpoint(Point2D point, double tolerance)
        {
            if (tolerance <= 0)
                return point.ToKey(10);

            var rounded = point.RoundToGrid(tolerance);
            return rounded.ToKey(10);
        }
    }
}
