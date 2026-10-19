using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// A row of any leaderboard from the Market API (/api/leaderboards/...). Each board fills in its own fields; the rest stay
    /// empty.
    /// </summary>
    [DataContract]
    public class LeaderboardRow {
        [DataMember(Name = "rank")] public int Rank;
        [DataMember(Name = "characterName")] public string CharacterName;
        [DataMember(Name = "level")] public int? Level;
        [DataMember(Name = "totalXp")] public long? TotalXp;
        [DataMember(Name = "rating")] public int? Rating;
        [DataMember(Name = "wins")] public int? Wins;
        [DataMember(Name = "losses")] public int? Losses;
        [DataMember(Name = "draws")] public int? Draws;
        [DataMember(Name = "characterNames")] public List<string> CharacterNames;
        [DataMember(Name = "completionSeconds")] public double? CompletionSeconds;
        /// <summary>ISO time, kept as text (DataContractJsonSerializer can't read it as a DateTime).</summary>
        [DataMember(Name = "completedAt")] public string CompletedAt;
        [DataMember(Name = "numDeaths")] public int? NumDeaths;
        [DataMember(Name = "allegianceRank")] public int? AllegianceRank;
        [DataMember(Name = "creatureName")] public string CreatureName;
        [DataMember(Name = "playerDeathsCaused")] public int? PlayerDeathsCaused;
        [DataMember(Name = "townsAttuned")] public int? TownsAttuned;
        [DataMember(Name = "skillName")] public string SkillName;
        [DataMember(Name = "skillLevel")] public int? SkillLevel;

        /// <summary>Everyone the row is about: one character, a fellowship, or none (a creature).</summary>
        public IEnumerable<string> Names => CharacterNames ?? (CharacterName != null ? new List<string> { CharacterName } : new List<string>());
    }

    /// <summary>
    /// One of the leaderboards the website's Leaderboards page shows (daralet-website leaderboards.js): what it's called, the
    /// API path it reads, and its columns. Arena and capstone boards take options (OptionA etc.) that pick the bracket.
    /// </summary>
    public class LeaderboardDefinition {
        public string Id;
        public string Label;
        public string Tip;
        /// <summary>Makes the API path from the chosen options (Arena: size, scaled; Capstone: dungeon, size, difficulty).</summary>
        public Func<LeaderboardOptions, string> Path;
        public (string Header, Func<LeaderboardRow, string> Cell)[] Columns;
    }

    /// <summary>The brackets the arena and capstone boards are picked by.</summary>
    public class LeaderboardOptions {
        public int ArenaSize = 1;
        public bool ArenaScaled;
        public string CapstoneDungeon = Leaderboards.CapstoneDungeons[0].Value;
        public int CapstoneSize = 1;
        public int CapstoneDifficulty;
    }

    /// <summary>The boards, in the website's order.</summary>
    public static class Leaderboards {
        /// <summary>How many rows each board asks for, as the website does.</summary>
        public const int Limit = 50;

        public static readonly (string Value, string Label)[] CapstoneDungeons = {
            ("EmpyreanGarrison", "Empyrean Garrison"), ("FolthidCellar", "Folthid Cellar"), ("GlendenWoodDungeon", "Glenden Wood Dungeon"),
            ("GredalineConsulate", "Gredaline Consulate"), ("GreenMireGrave", "Green Mire Grave"), ("GrievousVault", "Grievous Vault"),
            ("HallsOfTheHelm", "Halls of the Helm"), ("LugianMines", "Lugian Mines"), ("MageAcademy", "Mage Academy"),
            ("ManseOfPanderlou", "Manse of Panderlou"), ("MinesOfColier", "Mines of Colier"), ("MinesOfDespair", "Mines of Despair"),
            ("MountainFortress", "Mountain Fortress"), ("SandShallow", "Sand Shallow"), ("SmugglersHideaway", "Smugglers Hideaway"),
        };

        /// <summary>Fellowship sizes the capstone board splits by: solo, two, and three or more.</summary>
        public static readonly (int Value, string Label)[] CapstoneSizes = { (1, "Solo"), (2, "2"), (3, "3+") };

        /// <summary>The arena brackets the board has.</summary>
        public static readonly (int Value, string Label)[] ArenaSizes = { (1, "1v1"), (2, "2v2"), (3, "3v3") };

        public static readonly LeaderboardDefinition[] All = {
            new LeaderboardDefinition {
                Id = "level", Label = "Level", Tip = "The highest-level characters, by total experience.",
                Path = _ => $"api/leaderboards/level?limit={Limit}",
                Columns = new (string, Func<LeaderboardRow, string>)[] {
                    ("Character", r => r.CharacterName), ("Level", r => N(r.Level)), ("Total XP", r => N(r.TotalXp)),
                },
            },
            new LeaderboardDefinition {
                Id = "arena", Label = "Arena", Tip = "The best arena ratings in a bracket.",
                Path = o => $"api/leaderboards/arena?size={o.ArenaSize}&scaled={(o.ArenaScaled ? "true" : "false")}&limit={Limit}",
                Columns = new (string, Func<LeaderboardRow, string>)[] {
                    ("Character", r => r.CharacterName), ("Rating", r => N(r.Rating)), ("W / L / D", r => $"{r.Wins ?? 0} / {r.Losses ?? 0} / {r.Draws ?? 0}"),
                },
            },
            new LeaderboardDefinition {
                Id = "capstone", Label = "Capstones", Tip = "The fastest capstone runs, by dungeon, fellowship size and difficulty.",
                Path = o => $"api/leaderboards/capstone?dungeon={Uri.EscapeDataString(o.CapstoneDungeon ?? "")}&fellowshipSize={o.CapstoneSize}&difficulty={o.CapstoneDifficulty}&limit={Limit}",
                Columns = new (string, Func<LeaderboardRow, string>)[] {
                    ("Fellowship", r => string.Join(", ", r.CharacterNames ?? new List<string>())),
                    ("Time", r => r.CompletionSeconds.HasValue ? Duration(r.CompletionSeconds.Value) : ""),
                    ("Date", r => Date(r.CompletedAt)),
                },
            },
            new LeaderboardDefinition {
                Id = "deaths", Label = "Deaths", Tip = "Who has died the most.",
                Path = _ => $"api/leaderboards/deaths?limit={Limit}",
                Columns = new (string, Func<LeaderboardRow, string>)[] { ("Character", r => r.CharacterName), ("Deaths", r => N(r.NumDeaths)) },
            },
            new LeaderboardDefinition {
                Id = "allegiance", Label = "Allegiance rank", Tip = "The highest allegiance ranks.",
                Path = _ => $"api/leaderboards/allegiance-rank?limit={Limit}",
                Columns = new (string, Func<LeaderboardRow, string>)[] { ("Character", r => r.CharacterName), ("Allegiance rank", r => N(r.AllegianceRank)) },
            },
            new LeaderboardDefinition {
                Id = "creatures", Label = "Dangerous creatures", Tip = "The creatures that have killed the most players.",
                Path = _ => $"api/leaderboards/dangerous-creatures?limit={Limit}",
                Columns = new (string, Func<LeaderboardRow, string>)[] { ("Creature", r => r.CreatureName), ("Player deaths caused", r => N(r.PlayerDeathsCaused)) },
            },
            new LeaderboardDefinition {
                Id = "towns", Label = "Town attunements", Tip = "Who has attuned to the most towns.",
                Path = _ => $"api/leaderboards/town-attunements?limit={Limit}",
                Columns = new (string, Func<LeaderboardRow, string>)[] { ("Character", r => r.CharacterName), ("Towns attuned", r => N(r.TownsAttuned)) },
            },
            new LeaderboardDefinition {
                Id = "trade", Label = "Trade skills", Tip = "The best at each trade skill.",
                Path = _ => "api/leaderboards/trade-skills",
                Columns = new (string, Func<LeaderboardRow, string>)[] {
                    ("Skill", r => r.SkillName), ("Character", r => r.CharacterName ?? "-"), ("Skill level", r => N(r.SkillLevel)),
                },
            },
        };

        /// <summary>The capstone difficulties: None (no Dungeon: Difficulty mod), then I to XX.</summary>
        public static (int Value, string Label)[] CapstoneDifficulties() {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX" };
            return new[] { (0, "None") }.Concat(numerals.Select((n, i) => (i + 1, n))).ToArray();
        }

        /// <summary>754 -> "12:34", 4000 -> "1:06:40", as the website writes run times.</summary>
        public static string Duration(double totalSeconds) {
            var s = (int)Math.Max(0, Math.Round(totalSeconds));
            var h = s / 3600;
            var m = s % 3600 / 60;
            var sec = s % 60;
            return h > 0 ? $"{h}:{m:00}:{sec:00}" : $"{m}:{sec:00}";
        }

        /// <summary>An ISO date as yyyy-MM-dd, or "" if it can't be read.</summary>
        public static string Date(string iso) {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        }

        private static string N(long? value) {
            return value?.ToString("N0", CultureInfo.InvariantCulture) ?? "";
        }

        private static string N(int? value) {
            return value?.ToString("N0", CultureInfo.InvariantCulture) ?? "";
        }
    }
}
