using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DxfLayerEditor.Models
{
    /// <summary>
    /// Represents a DXF layer with Thermwood CNC naming validation.
    /// </summary>
    public class Layer
    {
        /// <summary>Unique identifier for this layer.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Layer name (validated against Thermwood conventions).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>AutoCAD Color Index (0–255).</summary>
        public int Color { get; set; } = 7;

        /// <summary>Whether the layer is visible on the canvas.</summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Valid Thermwood layer type prefixes / names.
        /// </summary>
        public static readonly string[] ValidLayerTypes = new[]
        {
            "outline", "border", "panel", "cutout", "pocket",
            "pocketclamp", "mortise", "drill", "vgroove", "score",
            "dado", "chain"
        };

        /// <summary>
        /// Layer types that require closed chains when assigning entities.
        /// </summary>
        public static readonly HashSet<string> ClosedChainRequired = new(StringComparer.OrdinalIgnoreCase)
        {
            "outline", "border", "panel"
        };

        /// <summary>
        /// Layer types where the 'back' prefix is forbidden.
        /// </summary>
        public static readonly HashSet<string> BackPrefixForbidden = new(StringComparer.OrdinalIgnoreCase)
        {
            "outline", "border", "panel"
        };

        /// <summary>
        /// Layers that participate in auto chain-close on export.
        /// </summary>
        public static readonly HashSet<string> AutoCloseOnExport = new(StringComparer.OrdinalIgnoreCase)
        {
            "outline", "border", "panel", "chain", "pocket", "pocketclamp", "dado"
        };

        /// <summary>
        /// Validates a Thermwood layer name and returns any validation errors.
        /// </summary>
        /// <param name="name">The proposed layer name.</param>
        /// <returns>List of validation error messages; empty if valid.</returns>
        public static List<string> ValidateName(string name)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Layer name cannot be empty.");
                return errors;
            }

            string lower = name.Trim().ToLowerInvariant();

            // Check if name starts with 'back' and is an outline-type layer
            bool hasBackPrefix = lower.StartsWith("back", StringComparison.Ordinal);
            string nameWithoutBack = hasBackPrefix
                ? lower.Substring(4).TrimStart('_', '-', ' ')
                : lower;

            // Extract the base layer type (first segment before any separator)
            string baseType = ExtractBaseType(nameWithoutBack);

            if (!ValidLayerTypes.Contains(baseType))
            {
                // Also check without the back prefix stripped
                string rawBase = ExtractBaseType(lower);
                if (!ValidLayerTypes.Contains(rawBase))
                {
                    errors.Add($"Layer name '{name}' does not start with a valid Thermwood type. " +
                              $"Valid types: {string.Join(", ", ValidLayerTypes)}");
                }
                else
                {
                    baseType = rawBase;
                }
            }

            // 'back' prefix banned on outline/border/panel
            if (hasBackPrefix && BackPrefixForbidden.Contains(baseType))
            {
                errors.Add($"The 'back' prefix is not allowed on {baseType} layer types.");
            }

            return errors;
        }

        /// <summary>
        /// Determines if a layer name is an outline-type (requires closed chains).
        /// </summary>
        public static bool IsOutlineType(string name)
        {
            string baseType = ExtractBaseType(name.Trim().ToLowerInvariant());
            return ClosedChainRequired.Contains(baseType);
        }

        /// <summary>
        /// Determines if a layer should auto-close chains on export.
        /// </summary>
        public static bool ShouldAutoClose(string name)
        {
            string lower = name.Trim().ToLowerInvariant();
            // Strip 'back' prefix if present
            if (lower.StartsWith("back", StringComparison.Ordinal))
                lower = lower.Substring(4).TrimStart('_', '-', ' ');

            string baseType = ExtractBaseType(lower);
            return AutoCloseOnExport.Contains(baseType);
        }

        /// <summary>
        /// Extracts the base Thermwood layer type from a full layer name.
        /// E.g., "outline_router" → "outline", "pocket2" → "pocket".
        /// </summary>
        private static string ExtractBaseType(string lowerName)
        {
            // Try matching each valid type as a prefix
            foreach (string t in ValidLayerTypes.OrderByDescending(t => t.Length))
            {
                if (lowerName.StartsWith(t, StringComparison.Ordinal))
                    return t;
            }
            return lowerName;
        }
    }
}
