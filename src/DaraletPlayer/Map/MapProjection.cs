using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DaraletPlayer.Map {
    /// <summary>
    /// Map coordinates as the game shows them: X is east (+) / west (-) and Y is north (+) / south (-), from -102 to +102.
    /// One map unit is 240 meters, 1.25 landblocks, and 10 pixels of the map image.
    /// </summary>
    public static class MapProjection {
        /// <summary>
        /// The map image is 2048 pixels square: 2040 pixels of map with 4 pixels of sea around it (made by tools/MapGen in
        /// Daralet Admin, from the server's dat files).
        /// </summary>
        public const int ImageSize = 2048;
        public const int Margin = 4;
        public const float PixelsPerUnit = 10;

        /// <summary>The farthest the map image reaches in any direction, margin included.</summary>
        public const float ImageExtent = 102 + Margin / PixelsPerUnit;

        /// <summary>Dereth itself, without the margin.</summary>
        public const float WorldExtent = 102;

        private static readonly Regex CoordsPattern = new Regex(
            @"^\s*([\d.]+)\s*([ns])[\s,]*([\d.]+)\s*([ew])\s*$",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Like the game shows them, e.g. 42.1N, 33.6E.
        /// </summary>
        public static string Format(float x, float y) {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0}{1}, {2:0.0}{3}",
                Math.Abs(y), y >= 0 ? "N" : "S",
                Math.Abs(x), x >= 0 ? "E" : "W");
        }

        /// <summary>
        /// The coordinates the way /tele reads them, e.g. 42.1n 33.6e.
        /// </summary>
        public static string ForTeleCommand(float x, float y) {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0}{1} {2:0.0}{3}",
                Math.Abs(y), y >= 0 ? "n" : "s",
                Math.Abs(x), x >= 0 ? "e" : "w");
        }

        /// <summary>
        /// Reads coordinates typed like 42.1N, 33.6E or 42.1n 33.6e. North / south comes first, as in the game.
        /// </summary>
        public static bool TryParse(string text, out float x, out float y) {
            x = 0;
            y = 0;

            var match = CoordsPattern.Match(text ?? "");

            if (!match.Success
                || !float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var northSouth)
                || !float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var eastWest)) {
                return false;
            }

            y = char.ToLowerInvariant(match.Groups[2].Value[0]) == 's' ? -northSouth : northSouth;
            x = char.ToLowerInvariant(match.Groups[4].Value[0]) == 'w' ? -eastWest : eastWest;

            return IsInWorld(x, y);
        }

        public static bool IsInWorld(float x, float y) {
            return Math.Abs(x) <= WorldExtent && Math.Abs(y) <= WorldExtent;
        }
    }
}
