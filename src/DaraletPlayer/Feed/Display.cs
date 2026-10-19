using System;
using System.Globalization;
using System.Linq;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// How the windows write numbers: times, distances, schools of magic.
    /// </summary>
    public static class Display {
        /// <summary>
        /// A spell's time left, counted down from the reply it came in: "45s", "4:05", "1h 12m", or "" for one that never
        /// runs out. Rounded up, so it reads 30s until a whole second has gone.
        /// </summary>
        public static string TimeLeft(double secondsLeftAtReply, double secondsSinceReply) {
            if (secondsLeftAtReply < 0) {
                return "";
            }

            var left = (int)Math.Ceiling(Math.Max(0, secondsLeftAtReply - secondsSinceReply));

            if (left < 60) {
                return $"{left}s";
            }
            if (left < 3600) {
                return $"{left / 60}:{left % 60:00}";
            }
            return $"{left / 3600}h {left % 3600 / 60:00}m";
        }

        /// <summary>"12m", or "" when the server couldn't say.</summary>
        public static string Distance(float? meters) {
            return meters.HasValue ? meters.Value.ToString("0", CultureInfo.InvariantCulture) + "m" : "";
        }

        /// <summary>A school of magic as the server names it ("LifeMagic", "CreatureEnchantment") in one word.</summary>
        public static string School(string school) {
            switch (school) {
                case null:
                case "":
                case "None":
                    return "";
                case "CreatureEnchantment":
                    return "Creature";
            }

            return school.EndsWith("Magic", StringComparison.Ordinal) && school.Length > 5 ? school.Substring(0, school.Length - 5) : school;
        }

        /// <summary>A threat value as a share of the highest, 0 to 1, for the bar behind it.</summary>
        public static float ThreatShare(int threat, int highest) {
            return highest <= 0 ? 0 : Math.Max(0f, Math.Min(1f, (float)threat / highest));
        }

        /// <summary>Guids the way @playerui requests take them: each once, in hex, leaving out 0.</summary>
        public static string TargetArguments(params uint[] guids) {
            return string.Join(" ", guids.Where(guid => guid != 0).Distinct().Select(guid => $"0x{guid:X8}"));
        }
    }
}
