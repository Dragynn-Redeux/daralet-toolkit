using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace DaraletPlayer.Map {
    /// <summary>
    /// A place from GoArrow's locations.xml. X is east (+) / west (-) and Y north (+) / south (-), in map units, as the game
    /// shows coordinates. A portal has ExitX / ExitY, where it goes.
    /// </summary>
    public class Location {
        public int Id;
        public string Name;
        /// <summary>
        /// Town, Dungeon, Landmark, Lifestone, Outpost, Bindstone, Vendor, NPC, AllegianceHall, TownPortal, WildernessPortal,
        /// UndergroundPortal, _Unknown (shops and halls) or Custom.
        /// </summary>
        public string Type;
        public float X;
        public float Y;
        public float? ExitX;
        public float? ExitY;
        public int? DungeonId;
        /// <summary>The text inside the entry: "Level 30 Capstone", a tier ("T2"), a town's number. Empty when there's none.</summary>
        public string Notes = "";

        public bool IsPortal => ExitX.HasValue && ExitY.HasValue;
    }

    /// <summary>
    /// The places the World tab shows, read from GoArrow's locations.xml (Phenyl's Daralet edition). The plugin carries a copy;
    /// a locations.xml next to the plugin dll is read instead, so an updated file can be dropped in without a new build.
    /// </summary>
    public static class Locations {
        public const string ResourceName = "DaraletPlayer.Resources.locations.xml";
        public const string FileName = "locations.xml";

        /// <summary>
        /// The location types in the order the World tab lists them, with whether each is on the map to begin with: only towns,
        /// so the map starts uncluttered (Show... turns the rest on).
        /// </summary>
        public static readonly (string Type, string Label, bool ShownByDefault)[] Types = {
            ("Town", "Towns", true),
            ("Dungeon", "Dungeons", false),
            ("Landmark", "Landmarks", false),
            ("Lifestone", "Lifestones", false),
            ("Outpost", "Outposts", false),
            ("Bindstone", "Bindstones", false),
            ("TownPortal", "Town portals", false),
            ("WildernessPortal", "Wilderness portals", false),
            ("UndergroundPortal", "Underground portals", false),
            ("AllegianceHall", "Allegiance halls", false),
            ("Vendor", "Vendors", false),
            ("NPC", "NPCs", false),
            // shops, taverns and meeting halls GoArrow never sorted
            ("_Unknown", "Other places", false),
        };

        /// <summary>
        /// Reads the file next to the plugin if there is one, otherwise the copy built into the plugin. Source says which.
        /// </summary>
        public static List<Location> LoadDefault(string pluginDirectory, Action<string> log, out string source) {
            var file = Path.Combine(pluginDirectory ?? "", FileName);

            if (File.Exists(file)) {
                try {
                    using (var stream = File.OpenRead(file)) {
                        source = file;
                        return Read(stream);
                    }
                }
                catch (Exception ex) {
                    log?.Invoke($"Couldn't read {file}, so the built-in locations are used: {ex.Message}");
                }
            }

            source = "built in";

            using (var stream = typeof(Locations).Assembly.GetManifestResourceStream(ResourceName)) {
                return stream == null ? new List<Location>() : Read(stream);
            }
        }

        /// <summary>
        /// Reads every &lt;loc&gt; with a name and coordinates. Custom entries (notes, like the "About" ones in the corners)
        /// are left out: they aren't places.
        /// </summary>
        public static List<Location> Read(Stream stream) {
            var results = new List<Location>();
            var settings = new XmlReaderSettings { IgnoreComments = true, DtdProcessing = DtdProcessing.Prohibit };

            using (var reader = XmlReader.Create(stream, settings)) {
                while (!reader.EOF) {
                    if (reader.NodeType != XmlNodeType.Element || reader.Name != "loc") {
                        reader.Read();
                        continue;
                    }

                    var location = new Location {
                        Id = Int(reader.GetAttribute("id")) ?? 0,
                        Name = (reader.GetAttribute("name") ?? "").Trim(),
                        Type = (reader.GetAttribute("type") ?? "").Trim(),
                        ExitX = Float(reader.GetAttribute("exitEW")),
                        ExitY = Float(reader.GetAttribute("exitNS")),
                        DungeonId = Int(reader.GetAttribute("dungeonId")),
                    };

                    var x = Float(reader.GetAttribute("EW"));
                    var y = Float(reader.GetAttribute("NS"));

                    // both of these leave the reader on whatever comes after the entry, often the next <loc>, which is why the
                    // loop looks at the current node before reading on
                    if (reader.IsEmptyElement) {
                        location.Notes = "";
                        reader.Read();
                    }
                    else {
                        location.Notes = CleanNotes(reader.ReadElementContentAsString());
                    }

                    if (x == null || y == null || location.Name.Length == 0 || location.Type == "Custom") {
                        continue;
                    }

                    location.X = x.Value;
                    location.Y = y.Value;
                    results.Add(location);
                }
            }

            return results;
        }

        /// <summary>
        /// The locations whose name has every word of the search in it, best first: names starting with it, then towns and
        /// dungeons, then the rest by name.
        /// </summary>
        public static List<Location> Search(IEnumerable<Location> locations, string text, int limit) {
            var words = (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0) {
                return new List<Location>();
            }

            var search = string.Join(" ", words);

            return locations
                .Where(l => words.All(w => l.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(l => l.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(l => l.Type == "Town" || l.Type == "Dungeon")
                .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }

        private static string CleanNotes(string text) {
            // "todo" is a note to whoever keeps the file, not to players
            var trimmed = (text ?? "").Trim();
            return string.Equals(trimmed, "todo", StringComparison.OrdinalIgnoreCase) ? "" : trimmed;
        }

        private static float? Float(string text) {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : (float?)null;
        }

        private static int? Int(string text) {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (int?)null;
        }
    }
}
