using System;
using System.Globalization;

namespace DaraletPlayer.Map {
    /// <summary>
    /// The sums behind the destination arrow. Map coordinates as the game shows them: X east (+) / west (-), Y north (+) /
    /// south (-). Headings as the game has them: degrees clockwise from north.
    /// </summary>
    public static class Navigation {
        /// <summary>Meters in one map unit (0.1 of the game's coordinates is 24 meters).</summary>
        public const float MetersPerUnit = 240;

        /// <summary>
        /// The map coordinates of a position Decal reports: the landcell and the position in its landblock. The same sums as
        /// PositionExtensions.GetMapCoords on the server, which stops at indoor cells (0x0100 and up); this doesn't, so it's
        /// only right outdoors and in buildings, not in a dungeon, where the landblock isn't a place on the map.
        /// </summary>
        public static void MapCoords(uint landcell, double x, double y, out float mapX, out float mapY) {
            var landblockX = (landcell >> 24) & 0xFF;
            var landblockY = (landcell >> 16) & 0xFF;

            mapX = (float)((landblockX * 192 + x) / 240 - 102);
            mapY = (float)((landblockY * 192 + y) / 240 - 102);
        }

        /// <summary>Whether a landcell is outdoors: its cell number is below 0x0100.</summary>
        public static bool IsOutdoors(uint landcell) {
            return (landcell & 0xFFFF) < 0x100;
        }

        /// <summary>
        /// The direction from one spot to another, in degrees clockwise from north (0 to 360).
        /// </summary>
        public static double Bearing(float fromX, float fromY, float toX, float toY) {
            var degrees = Math.Atan2(toX - fromX, toY - fromY) * 180 / Math.PI;
            return (degrees + 360) % 360;
        }

        /// <summary>
        /// Where a bearing is from the way you're facing, in degrees clockwise (-180 to 180): 0 is straight ahead, 90 to your
        /// right.
        /// </summary>
        public static double Relative(double bearing, double heading) {
            var turn = (bearing - heading) % 360;

            if (turn > 180) {
                turn -= 360;
            }
            else if (turn <= -180) {
                turn += 360;
            }

            return turn;
        }

        public static float Distance(float fromX, float fromY, float toX, float toY) {
            var dx = toX - fromX;
            var dy = toY - fromY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// A distance in map units the way the arrow shows it: "3.4 (816m)", or "120m" when it's close.
        /// </summary>
        public static string DistanceText(float units) {
            var meters = units * MetersPerUnit;

            if (units < 1) {
                return string.Format(CultureInfo.InvariantCulture, "{0:0}m", meters);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} ({1:0}m)", units, meters);
        }

        /// <summary>
        /// One of the eight compass points for a bearing: N, NE, E...
        /// </summary>
        public static string Compass(double bearing) {
            string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            var index = (int)Math.Round(((bearing % 360) + 360) % 360 / 45) % 8;
            return points[index];
        }
    }
}
