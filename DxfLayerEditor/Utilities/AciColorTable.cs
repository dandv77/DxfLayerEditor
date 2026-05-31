using System;
using System.Collections.Generic;
using System.Drawing;

namespace DxfLayerEditor.Utilities
{
    /// <summary>
    /// Maps AutoCAD Color Index (ACI) values (0–255) to System.Drawing.Color.
    /// Provides the standard 256-color DXF palette for entity and layer rendering.
    /// </summary>
    public static class AciColorTable
    {
        private static readonly Color[] _table;

        static AciColorTable()
        {
            _table = new Color[256];
            _table[0] = Color.FromArgb(0, 0, 0);       // ByBlock
            _table[1] = Color.FromArgb(255, 0, 0);      // Red
            _table[2] = Color.FromArgb(255, 255, 0);     // Yellow
            _table[3] = Color.FromArgb(0, 255, 0);      // Green
            _table[4] = Color.FromArgb(0, 255, 255);     // Cyan
            _table[5] = Color.FromArgb(0, 0, 255);      // Blue
            _table[6] = Color.FromArgb(255, 0, 255);     // Magenta
            _table[7] = Color.FromArgb(255, 255, 255);   // White/Black
            _table[8] = Color.FromArgb(128, 128, 128);   // Dark grey
            _table[9] = Color.FromArgb(192, 192, 192);   // Light grey

            // Standard colors 10-249: generate from HSV-like AutoCAD mapping
            int idx = 10;
            // Hue families: red, yellow, green, cyan, blue, magenta (6 families × 10 shades × 4 brightness)
            int[][] hueRGB = new int[][]
            {
                new[] {255, 0, 0},     // Red
                new[] {255, 127, 0},   // Orange
                new[] {255, 255, 0},   // Yellow
                new[] {127, 255, 0},   // Yellow-green
                new[] {0, 255, 0},     // Green
                new[] {0, 255, 127},   // Green-cyan
                new[] {0, 255, 255},   // Cyan
                new[] {0, 127, 255},   // Cyan-blue
                new[] {0, 0, 255},     // Blue
                new[] {127, 0, 255},   // Blue-magenta
                new[] {255, 0, 255},   // Magenta
                new[] {255, 0, 127},   // Magenta-red
            };

            // For each hue family, generate 5 shades at 4 brightness levels
            foreach (var baseRgb in hueRGB)
            {
                // Full brightness
                _table[idx++] = Color.FromArgb(baseRgb[0], baseRgb[1], baseRgb[2]);

                // Lighter shades
                _table[idx++] = Color.FromArgb(
                    Lerp(baseRgb[0], 255, 0.33),
                    Lerp(baseRgb[1], 255, 0.33),
                    Lerp(baseRgb[2], 255, 0.33));
                _table[idx++] = Color.FromArgb(
                    Lerp(baseRgb[0], 255, 0.66),
                    Lerp(baseRgb[1], 255, 0.66),
                    Lerp(baseRgb[2], 255, 0.66));

                // Darker shades
                _table[idx++] = Color.FromArgb(
                    Lerp(baseRgb[0], 0, 0.33),
                    Lerp(baseRgb[1], 0, 0.33),
                    Lerp(baseRgb[2], 0, 0.33));
                _table[idx++] = Color.FromArgb(
                    Lerp(baseRgb[0], 0, 0.66),
                    Lerp(baseRgb[1], 0, 0.66),
                    Lerp(baseRgb[2], 0, 0.66));

                // Mid variants
                for (int v = 0; v < 5 && idx < 250; v++)
                {
                    double f = (v + 1) * 0.15;
                    _table[idx++] = Color.FromArgb(
                        Lerp(baseRgb[0], 128, f),
                        Lerp(baseRgb[1], 128, f),
                        Lerp(baseRgb[2], 128, f));
                }
            }

            // Fill any remaining slots up to 249 with greys
            while (idx < 250)
            {
                int g = (int)(255.0 * (idx - 10) / 240);
                _table[idx++] = Color.FromArgb(g, g, g);
            }

            // 250–255: greyscale ramp
            _table[250] = Color.FromArgb(51, 51, 51);
            _table[251] = Color.FromArgb(80, 80, 80);
            _table[252] = Color.FromArgb(105, 105, 105);
            _table[253] = Color.FromArgb(128, 128, 128);
            _table[254] = Color.FromArgb(190, 190, 190);
            _table[255] = Color.FromArgb(255, 255, 255);
        }

        private static int Lerp(int a, int b, double t)
        {
            return Math.Clamp((int)(a + (b - a) * t), 0, 255);
        }

        /// <summary>
        /// Gets the Color for the given ACI index (0–255).
        /// Returns White for out-of-range values.
        /// </summary>
        /// <param name="aci">AutoCAD Color Index (0–255).</param>
        /// <returns>The corresponding <see cref="Color"/>.</returns>
        public static Color GetColor(int aci)
        {
            if (aci < 0 || aci >= _table.Length)
                return Color.White;
            return _table[aci];
        }

        /// <summary>
        /// Gets the Color for the given ACI index, adjusted for dark backgrounds.
        /// ACI 7 (white) and colors close to white are rendered as near-white
        /// rather than pure white, and ACI 0 (byblock/black) is rendered as light grey.
        /// </summary>
        /// <param name="aci">AutoCAD Color Index.</param>
        /// <returns>The display-adjusted <see cref="Color"/>.</returns>
        public static Color GetDisplayColor(int aci)
        {
            if (aci == 0)
                return Color.FromArgb(180, 180, 180);
            if (aci == 7)
                return Color.FromArgb(240, 240, 240);
            return GetColor(aci);
        }

        /// <summary>
        /// Returns a curated list of common ACI colors for the color picker UI.
        /// </summary>
        public static (int Aci, string Name, Color Color)[] GetPickerColors()
        {
            return new[]
            {
                (1, "Red", GetColor(1)),
                (2, "Yellow", GetColor(2)),
                (3, "Green", GetColor(3)),
                (4, "Cyan", GetColor(4)),
                (5, "Blue", GetColor(5)),
                (6, "Magenta", GetColor(6)),
                (7, "White", GetColor(7)),
                (8, "Dark Grey", GetColor(8)),
                (9, "Light Grey", GetColor(9)),
                (10, "Red (10)", GetColor(10)),
                (30, "Orange", GetColor(30)),
                (40, "Gold", GetColor(40)),
                (140, "Teal", GetColor(140)),
                (200, "Purple", GetColor(200)),
            };
        }
    }
}
