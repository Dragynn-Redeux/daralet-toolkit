using System;
using System.IO;

namespace DaraletPlayer {
    /// <summary>
    /// Documents\Decal Plugins\Daralet: where everything the plugin writes is kept (settings.json, log.txt, Loot Profiles,
    /// world-data), shared by every character on this computer.
    /// </summary>
    public static class DataFolder {
        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins", "Daralet");

        /// <summary>A file or folder in it.</summary>
        public static string For(string name) {
            return Path.Combine(Root, name);
        }
    }
}
