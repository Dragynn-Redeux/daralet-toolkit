using System;
using System.Linq;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// The /bank commands the Bank tab sends (BankCommand.cs in daralet-ACE), and the words they take.
    /// </summary>
    public static class BankCommands {
        /// <summary>The categories /bank deposit, withdraw, sort and search take (BankCommand.CategoryList).</summary>
        public static readonly string[] Categories = {
            "all", "gear", "weapons", "armor", "jewelry", "trinkets", "salvage", "ammo", "animal", "components",
            "consumables", "gems", "keys", "manastones", "trophies", "currency", "misc",
        };

        /// <summary>The kinds within the categories (BankCommand.TypeList), by the category they're part of.</summary>
        public static readonly (string Category, string[] Types)[] Types = {
            ("Weapons", new[] { "swords", "maces", "axes", "spears", "daggers", "staffs", "unarmed", "two-handed", "bows", "crossbows", "atlatls", "thrown", "casters" }),
            ("Armor", new[] { "heavy", "light", "cloth", "clothing" }),
            ("Jewelry", new[] { "necklaces", "rings", "bracelets" }),
            ("Salvage", new[] { "blacksmithing", "tailoring", "spellcrafting", "woodworking", "jewelcrafting", "imbue" }),
        };

        /// <summary>Loot tiers, which can go with a category or type ("swords t6") or alone ("t6": any gear of that tier).</summary>
        public static readonly string[] Tiers = { "t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7" };

        public static string Deposit(string what) {
            return "/bank deposit " + Clean(what);
        }

        /// <summary>/bank deposit packs: empties the packs you carry inscribed "deposit".</summary>
        public const string DepositPacks = "/bank deposit packs";

        /// <summary>
        /// /bank withdraw, with a count when one's given ("/bank withdraw 200 arrows").
        /// </summary>
        public static string Withdraw(int? count, string what) {
            return count.HasValue && count.Value > 0 ? $"/bank withdraw {count.Value} {Clean(what)}" : "/bank withdraw " + Clean(what);
        }

        /// <summary>/bank sort, of everything or only what's given.</summary>
        public static string Sort(string what) {
            var cleaned = Clean(what);
            return cleaned.Length == 0 ? "/bank sort" : "/bank sort " + cleaned;
        }

        public static string Search(string what) {
            return "/bank search " + Clean(what);
        }

        public const string Combine = "/bank combine";
        public const string Balance = "/bank balance";
        public const string Packs = "/bank packs";
        public const string Intro = "/bank intro";

        public static string Log(int? entries) {
            return entries.HasValue && entries.Value > 0 ? $"/bank log {entries.Value}" : "/bank log";
        }

        public static string AutoSort(bool on) {
            return on ? "/bank autosort on" : "/bank autosort off";
        }

        /// <summary>
        /// /bank inscribe for a pack in the bank, by its number. "clear" clears it, which the server reads as no tags.
        /// </summary>
        public static string Inscribe(int packNumber, string tags) {
            var cleaned = Clean(tags);
            return $"/bank inscribe {packNumber} {(cleaned.Length == 0 ? "clear" : cleaned)}";
        }

        /// <summary>
        /// Whether the text names something to deposit, withdraw or search for: anything but nothing.
        /// </summary>
        public static bool HasWhat(string what) {
            return Clean(what).Length > 0;
        }

        /// <summary>
        /// Text typed into a box, made safe to put on a command line: one line, no slashes at the start (that would read as
        /// another command), single spaces.
        /// </summary>
        public static string Clean(string text) {
            var oneLine = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            var words = oneLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words).TrimStart('/', '@').Trim();
        }

        /// <summary>
        /// Every word the category box offers, in order: categories, then each category's types, then tiers.
        /// </summary>
        public static string[] AllWords() {
            return Categories.Concat(Types.SelectMany(t => t.Types)).Concat(Tiers).ToArray();
        }
    }
}
