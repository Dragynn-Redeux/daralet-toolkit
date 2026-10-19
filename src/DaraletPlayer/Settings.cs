using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using DaraletPlayer.Feed;

namespace DaraletPlayer {
    // The settings classes set their defaults in SetDefaults, called both when one is made and before one is read from the
    // file: DataContractJsonSerializer doesn't run constructors or field initialisers, and an older file may not have every
    // setting.

    /// <summary>
    /// Where one of the floating windows is and how it's shown. X and Y are null until it's been placed.
    /// </summary>
    [DataContract]
    public class WindowPlacement {
        public const float MinScale = 0.5f;
        public const float MaxScale = 2.5f;

        [DataMember(Name = "x")] public float? X;
        [DataMember(Name = "y")] public float? Y;
        [DataMember(Name = "visible")] public bool Visible;
        /// <summary>No title bar, and can't be dragged.</summary>
        [DataMember(Name = "locked")] public bool Locked;
        /// <summary>How big the window's text and bars are drawn: 1 is normal size.</summary>
        [DataMember(Name = "scale")] public float Scale;

        public WindowPlacement() {
            SetDefaults();
        }

        public float ClampedScale => Math.Max(MinScale, Math.Min(MaxScale, Scale));

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) {
            SetDefaults();
        }

        private void SetDefaults() {
            Visible = true;
            Scale = 1;
        }
    }

    /// <summary>
    /// Where the destination arrow points: a spot on the map (X east, Y north, in map units), or a fellow, followed as they
    /// move (FellowGuid; X and Y are where they were last seen).
    /// </summary>
    [DataContract]
    public class Destination {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "x")] public float X;
        [DataMember(Name = "y")] public float Y;
        [DataMember(Name = "fellowGuid")] public uint? FellowGuid;
    }

    [DataContract]
    public class SettingsData {
        [DataMember(Name = "windows")] public Dictionary<string, WindowPlacement> Windows;

        [DataMember(Name = "fellowshipShowStamina")] public bool FellowshipShowStamina;
        [DataMember(Name = "fellowshipShowMana")] public bool FellowshipShowMana;

        [DataMember(Name = "targetShowBuffs")] public bool TargetShowBuffs;
        [DataMember(Name = "targetShowDebuffs")] public bool TargetShowDebuffs;
        [DataMember(Name = "targetShowThreat")] public bool TargetShowThreat;
        /// <summary>Rows of the threat table the floating target windows show.</summary>
        [DataMember(Name = "threatRows")] public int ThreatRows;

        /// <summary>Location types shown or hidden by hand (Locations.Types); one that isn't here is as it starts.</summary>
        [DataMember(Name = "mapTypes")] public Dictionary<string, bool> MapTypes;
        [DataMember(Name = "mapShowFellows")] public bool MapShowFellows;
        [DataMember(Name = "mapShowLabels")] public bool MapShowLabels;
        [DataMember(Name = "mapTextScale")] public float MapTextScale;
        [DataMember(Name = "mapFollow")] public bool MapFollow;
        /// <summary>Where the arrow points, or null.</summary>
        [DataMember(Name = "destination")] public Destination Destination;

        [DataMember(Name = "loot")] public LootData Loot;

        public SettingsData() {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) {
            SetDefaults();
        }

        private void SetDefaults() {
            Windows = new Dictionary<string, WindowPlacement>();
            FellowshipShowStamina = true;
            FellowshipShowMana = true;
            TargetShowBuffs = true;
            TargetShowDebuffs = true;
            TargetShowThreat = true;
            ThreatRows = 5;
            MapTypes = new Dictionary<string, bool>();
            MapShowFellows = true;
            MapShowLabels = true;
            MapTextScale = 1;
            Loot = new LootData();
        }
    }

    /// <summary>
    /// The plugin's settings, in Documents\Asheron's Call\Daralet\settings.json, shared by every character on this computer.
    /// A change is saved two seconds after the last one, so dragging a window doesn't write the file every frame.
    /// </summary>
    public class Settings {
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

        private readonly string filePath;
        private readonly Action<string> log;
        private DateTime? changedAt;

        public SettingsData Data { get; private set; } = new SettingsData();

        /// <param name="filePath">The file to use; the real one when null (tests pass their own).</param>
        /// <param name="log">Where to report a file that can't be read or written.</param>
        public Settings(string filePath = null, Action<string> log = null) {
            this.filePath = filePath ?? DefaultPath();
            this.log = log ?? (_ => { });

            if (filePath == null) {
                CarryOver(OldDefaultPath(), this.filePath, this.log);
            }

            Load();
        }

        public static string DefaultPath() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Asheron's Call", "Daralet", "settings.json");
        }

        /// <summary>Where the settings were while the plugin was called Daralet Player.</summary>
        public static string OldDefaultPath() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Asheron's Call", "Daralet Player", "settings.json");
        }

        /// <summary>
        /// Copies the settings from where they used to be, when they're there and there are none here yet. The old file is
        /// left where it is.
        /// </summary>
        public static void CarryOver(string oldPath, string newPath, Action<string> log) {
            try {
                if (File.Exists(newPath) || !File.Exists(oldPath)) {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                File.Copy(oldPath, newPath);
            }
            catch (Exception ex) {
                log?.Invoke($"Couldn't copy the settings from {oldPath}: {ex.Message}");
            }
        }

        /// <summary>A window's placement by its id, made the first time it's asked for.</summary>
        public WindowPlacement Window(string id, bool visibleByDefault = true) {
            if (!Data.Windows.TryGetValue(id, out var placement) || placement == null) {
                placement = new WindowPlacement { Visible = visibleByDefault };
                Data.Windows[id] = placement;
            }

            return placement;
        }

        /// <summary>Call after changing anything in Data.</summary>
        public void Changed() {
            changedAt = changedAt ?? DateTime.UtcNow;
        }

        /// <summary>Called every frame: saves once the first unsaved change is two seconds old.</summary>
        public void SaveIfDue() {
            if (changedAt.HasValue && DateTime.UtcNow - changedAt.Value > SaveDelay) {
                Save();
            }
        }

        /// <summary>Saves now if anything's changed, for when the plugin shuts down.</summary>
        public void SaveIfChanged() {
            if (changedAt.HasValue) {
                Save();
            }
        }

        public void Save() {
            changedAt = null;

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                // written to a temporary file first, so a crash part way through can't wipe the settings
                var temporary = filePath + ".tmp";

                using (var stream = File.Create(temporary)) {
                    Json.Write(stream, Data);
                }

                if (File.Exists(filePath)) {
                    File.Replace(temporary, filePath, null);
                }
                else {
                    File.Move(temporary, filePath);
                }
            }
            catch (Exception ex) {
                log($"Couldn't save the settings to {filePath}: {ex.Message}");
            }
        }

        private void Load() {
            try {
                if (!File.Exists(filePath)) {
                    return;
                }

                using (var stream = File.OpenRead(filePath)) {
                    Data = Json.Read<SettingsData>(stream) ?? new SettingsData();
                }

                // a file can say null outright
                Data.Windows = Data.Windows ?? new Dictionary<string, WindowPlacement>();
                Data.MapTypes = Data.MapTypes ?? new Dictionary<string, bool>();
                Data.Loot = Data.Loot ?? new LootData();
            }
            catch (Exception ex) {
                log($"Couldn't read the settings from {filePath}: {ex.Message}");
            }
        }
    }
}
