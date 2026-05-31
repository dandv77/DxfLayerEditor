using System;
using System.Collections.Generic;
using System.Linq;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents a group of connected entities forming a chain.
    /// Chains are detected via Union-Find on rounded endpoints.
    /// </summary>
    public class ChainGroup
    {
        /// <summary>Unique chain identifier.</summary>
        public string ChainId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Entity IDs belonging to this chain.</summary>
        public List<string> EntityIds { get; set; } = new();

        /// <summary>
        /// Determines if this chain is closed by checking whether all
        /// endpoints are shared (each rounded endpoint appears an even number of times).
        /// </summary>
        /// <param name="entities">All entities in this chain.</param>
        /// <param name="tolerance">Endpoint matching tolerance.</param>
        /// <returns>True if the chain forms a closed loop.</returns>
        public bool IsClosed(IEnumerable<EntityBase> entities, double tolerance = 1e-4)
        {
            var endpointCounts = new Dictionary<string, int>();

            foreach (var entity in entities)
            {
                foreach (var ep in entity.GetEndpoints())
                {
                    string key = ep.RoundToGrid(tolerance).ToKey(8);
                    endpointCounts.TryGetValue(key, out int count);
                    endpointCounts[key] = count + 1;
                }
            }

            // A closed chain has every endpoint appearing exactly twice
            // (or the chain has no endpoints — e.g., all circles)
            return endpointCounts.Count == 0 ||
                   endpointCounts.Values.All(c => c >= 2);
        }

        /// <summary>
        /// Returns the open (unmatched) endpoints of this chain.
        /// </summary>
        /// <param name="entities">All entities in this chain.</param>
        /// <param name="tolerance">Endpoint matching tolerance.</param>
        /// <returns>List of open endpoint positions.</returns>
        public List<Point2D> GetOpenEndpoints(IEnumerable<EntityBase> entities, double tolerance = 1e-4)
        {
            var endpointMap = new Dictionary<string, List<Point2D>>();

            foreach (var entity in entities)
            {
                foreach (var ep in entity.GetEndpoints())
                {
                    string key = ep.RoundToGrid(tolerance).ToKey(8);
                    if (!endpointMap.ContainsKey(key))
                        endpointMap[key] = new List<Point2D>();
                    endpointMap[key].Add(ep);
                }
            }

            var openEndpoints = new List<Point2D>();
            foreach (var kvp in endpointMap)
            {
                if (kvp.Value.Count == 1)
                    openEndpoints.Add(kvp.Value[0]);
            }

            return openEndpoints;
        }
    }
}
