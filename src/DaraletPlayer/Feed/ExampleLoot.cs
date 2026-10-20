using System.Collections.Generic;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// The Example Loot Profile: a list that shows off what rules can do (roll quality filters, marks, the unlearned scrolls
    /// switch, each outcome, and why order matters), to copy and change rather than to loot with as it is. New settings start
    /// with it after "My loot"; the Loot tab's Example button adds it again.
    /// </summary>
    public static class ExampleLoot {
        public const string Name = "Example Loot Profile";

        public static LootList Make() {
            return new LootList {
                Name = Name,
                Rules = new List<LootRule> {
                    new LootRule {
                        Name = "Scrolls I haven't learned", Category = "Misc", UnlearnedSpellsOnly = true,
                        Values = { ["Misc:miscType"] = "Scroll" }, Highlight = "Cyan", Icon = LootMarks.Exclamation,
                    },
                    new LootRule {
                        Name = "Weapons: attack rolled 90%+", Category = "Weapon", MinTier = "5",
                        Values = { ["Weapon:minAttackMod"] = "90" }, RollFilters = { "Weapon:minAttackMod" }, Highlight = "Gold", Icon = LootMarks.Star,
                    },
                    new LootRule {
                        Name = "War casters, 10%+ war magic", Category = "Weapon",
                        Values = { ["Weapon:weaponClass"] = "WarMagic", ["Weapon:minWarMagicMod"] = "10" }, Highlight = "Blue",
                    },
                    new LootRule {
                        Name = "Armor: armor level rolled 85%+", Category = "Armor", MinTier = "6",
                        Values = { ["Armor:minArmorLevel"] = "85" }, RollFilters = { "Armor:minArmorLevel" }, Highlight = "Purple", Icon = LootMarks.Diamond,
                    },
                    new LootRule {
                        Name = "Jewelry with crit damage", Category = "Jewelry",
                        Values = { ["Jewelry:minGearCritDamage"] = "1" }, Highlight = "Green",
                    },
                    new LootRule {
                        Name = "Sigil trinkets, 15%+ proc chance", Category = "SigilTrinket",
                        Values = { ["SigilTrinket:minProcChance"] = "15" }, Highlight = "Pink", Icon = LootMarks.Circle,
                    },
                    new LootRule {
                        Name = "Valuable gems", Category = "Misc",
                        Values = { ["Misc:miscType"] = "Gem", ["any:minPrice"] = "2000" },
                    },
                    new LootRule {
                        Name = "Tell me about any T7", MinTier = "7", Outcome = LootOutcome.Notify, Highlight = "Orange", Icon = LootMarks.Triangle,
                    },
                    // off to start with: it salvages, which needs an Ust, and can't be undone
                    new LootRule {
                        Name = "Salvage T5+ gear (needs an Ust)", Enabled = false, MinTier = "5", SalvageableOnly = true, Outcome = LootOutcome.Salvage,
                    },
                    new LootRule {
                        Name = "Ignore anything worth under 100", Outcome = LootOutcome.Ignore,
                        Values = { ["any:maxPrice"] = "100" },
                    },
                },
            };
        }
    }
}
