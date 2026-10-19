using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DaraletPlayer.Feed {
    /// <summary>How a line of an item tooltip is drawn.</summary>
    public enum ItemLineStyle {
        /// <summary>The item's name.</summary>
        Title,
        /// <summary>What kind of item it is, and other quiet facts.</summary>
        Detail,
        /// <summary>A section's name: Weapon, Armor, Spells...</summary>
        Heading,
        Text,
        /// <summary>Something good about it (a bonus).</summary>
        Good,
        /// <summary>Something that holds it back (a penalty).</summary>
        Bad,
    }

    public class ItemLine {
        public string Text;
        public ItemLineStyle Style;

        public ItemLine(string text, ItemLineStyle style) {
            Text = text;
            Style = style;
        }

        public override string ToString() => $"{Style}: {Text}";
    }

    /// <summary>
    /// An item's tooltip as lines: every stat its description (the Market API's, MarketListing) has, in sections. The tooltips
    /// in the bank, the Loot tab, the Corpse window and market search all draw these, so the layout is changed in one place.
    /// </summary>
    public static class ItemLines {
        /// <summary>
        /// An item from a corpse, your packs or your bank: the listing's stats and its own value, burden, stack and salvage.
        /// </summary>
        public static List<ItemLine> Build(CorpseItemInfo item) {
            var lines = Build(item.Item, item.Name);

            var basics = $"Value {item.Value:N0}   Burden {item.Burden:N0}";
            if (item.StackSize > 1) {
                basics += $"   Stack {item.StackSize:N0}";
            }
            lines.Add(new ItemLine(basics, ItemLineStyle.Detail));

            if (item.Salvageable) {
                lines.Add(new ItemLine($"Salvages to {Spaced(item.Material)}, workmanship {N(item.Workmanship, "0.##")}", ItemLineStyle.Detail));
            }

            return lines;
        }

        /// <summary>
        /// A market listing's stats; name is used when the listing has none.
        /// </summary>
        public static List<ItemLine> Build(MarketListing l, string name = null) {
            var lines = new List<ItemLine> { new ItemLine(l?.ItemName ?? name ?? "", ItemLineStyle.Title) };

            if (l == null) {
                lines.Add(new ItemLine("No details yet.", ItemLineStyle.Detail));
                return lines;
            }

            var kind = new List<string> { Spaced(l.ItemCategory) };
            kind.Add(Spaced(l.WeaponClass ?? l.ArmorWeightClass ?? l.JewelrySlot ?? l.SigilTrinketType ?? l.ConsumableType ?? l.MiscType));
            if (l.ItemTier.HasValue) {
                kind.Add(Tier(l.ItemTier.Value));
            }
            Add(lines, string.Join(", ", kind.Where(k => k.Length > 0)), ItemLineStyle.Detail);

            Requirements(lines, l);
            Weapon(lines, l);
            Armor(lines, l);
            Ratings(lines, l);
            Sigil(lines, l);
            Materials(lines, l);
            Magic(lines, l);
            Crafting(lines, l);

            List(lines, "Properties", l.Properties);
            List(lines, "Imbued", l.ImbuedEffects);
            List(lines, "Spells", l.Spells);

            return lines;
        }

        /// <summary>
        /// A tier as the Search tab and loot rules show them: T0 to T7. Listings count from 1 (the Market API's tiers).
        /// </summary>
        public static string Tier(int apiTier) {
            return $"T{Math.Max(0, apiTier - 1)}";
        }

        private static void Requirements(List<ItemLine> lines, MarketListing l) {
            if (l.LevelReq.HasValue) {
                lines.Add(new ItemLine($"Requires level {l.LevelReq}", ItemLineStyle.Text));
            }

            if (!string.IsNullOrEmpty(l.ReqAttribute) && l.ReqAttributeAmount.HasValue) {
                lines.Add(new ItemLine($"Requires {l.ReqAttributeAmount} {Spaced(l.ReqAttribute)}", ItemLineStyle.Text));
            }

            foreach (var other in l.OtherWieldRequirements ?? new List<string>()) {
                lines.Add(new ItemLine($"Requires {other}", ItemLineStyle.Text));
            }
        }

        private static void Weapon(List<ItemLine> lines, MarketListing l) {
            var section = new List<ItemLine>();

            if (l.BaseDamage.HasValue) {
                var damage = $"Damage {l.BaseDamage}";
                if (l.DamageVariance.HasValue) {
                    damage += $" ({Pct(l.DamageVariance.Value, signed: false)} variance";
                    damage += l.AvgDamage.HasValue ? $", {N(l.AvgDamage, "0.#")} on average)" : ")";
                }
                section.Add(new ItemLine(damage, ItemLineStyle.Text));
            }

            var pace = new List<string>();
            if (l.Speed.HasValue) {
                pace.Add($"speed {N(l.Speed, "0.00")}");
            }
            if (l.Dps.HasValue) {
                pace.Add($"{N(l.Dps, "0.0")} damage a second");
            }
            Add(section, Capital(string.Join(", ", pace)), ItemLineStyle.Text);

            Bonus(section, "Damage", l.DamageMod, multiplier: true);
            Bonus(section, "Elemental damage", l.ElementalDamageMod, multiplier: true);
            if (l.ElementalDamageBonus > 0) {
                section.Add(new ItemLine($"+{l.ElementalDamageBonus} elemental damage", ItemLineStyle.Good));
            }
            Bonus(section, "Healing spells", l.RestorationMod, multiplier: true);
            Bonus(section, "Attack", l.WeaponOffense, multiplier: true);
            Bonus(section, "Melee defense", l.WeaponPhysicalDefense, multiplier: true);
            Bonus(section, "Magic defense", l.WeaponMagicalDefense, multiplier: true);
            Bonus(section, "War magic", l.WeaponWarMagicMod, multiplier: false);
            Bonus(section, "Life magic", l.WeaponLifeMagicMod, multiplier: false);

            Section(lines, "Weapon", section);
        }

        private static void Armor(List<ItemLine> lines, MarketListing l) {
            var section = new List<ItemLine>();

            if (l.ArmorLevel.HasValue) {
                section.Add(new ItemLine($"Armor level {l.ArmorLevel}", ItemLineStyle.Text));
            }

            if (l.WardLevel.HasValue) {
                var slots = l.ArmorSlots > 1 ? $" ({N(l.WardPerSlot ?? (double)l.WardLevel.Value / l.ArmorSlots.Value, "0.#")} a slot over {l.ArmorSlots} slots)" : "";
                section.Add(new ItemLine($"Ward {l.WardLevel}{slots}", ItemLineStyle.Text));
            }

            var protections = new (string Name, double? Value)[] {
                ("Slash", l.ArmorModVsSlash), ("Pierce", l.ArmorModVsPierce), ("Bludgeon", l.ArmorModVsBludgeon), ("Cold", l.ArmorModVsCold),
                ("Fire", l.ArmorModVsFire), ("Acid", l.ArmorModVsAcid), ("Lightning", l.ArmorModVsElectric), ("Nether", l.ArmorModVsNether),
            }.Where(p => p.Value.HasValue).Select(p => $"{p.Name} {N(p.Value, "0.00")}").ToList();

            if (protections.Count > 0) {
                section.Add(new ItemLine("Protection: " + string.Join(", ", protections), ItemLineStyle.Text));
            }

            if (l.Coverage != null && l.Coverage.Count > 0) {
                section.Add(new ItemLine("Covers " + string.Join(", ", l.Coverage.Select(Spaced)), ItemLineStyle.Detail));
            }

            Bonus(section, "War magic", l.ArmorWarMagicMod, multiplier: false);
            Bonus(section, "Life magic", l.ArmorLifeMagicMod, multiplier: false);
            Bonus(section, "Mana regen", l.ArmorManaRegenMod, multiplier: false);
            Bonus(section, "Mana conversion", l.ArmorManaConversionMod, multiplier: false);
            Bonus(section, "Attack", l.ArmorAttackMod, multiplier: false);
            Bonus(section, "Shield", l.ArmorShieldMod, multiplier: false);
            Bonus(section, "Dual wield", l.ArmorDualWieldMod, multiplier: false);
            Bonus(section, "Thievery", l.ArmorThieveryMod, multiplier: false);
            Bonus(section, "Run", l.ArmorRunMod, multiplier: false);
            Bonus(section, "Stamina regen", l.ArmorStaminaRegenMod, multiplier: false);
            Bonus(section, "Melee defense", l.ArmorPhysicalDefMod, multiplier: false);
            Bonus(section, "Magic defense", l.ArmorMagicDefMod, multiplier: false);
            Bonus(section, "Two-handed combat", l.ArmorTwohandedCombatMod, multiplier: false);
            Bonus(section, "Health regen", l.ArmorHealthRegenMod, multiplier: false);
            Bonus(section, "Perception", l.ArmorPerceptionMod, multiplier: false);
            Bonus(section, "Deception", l.ArmorDeceptionMod, multiplier: false);
            Bonus(section, "Max health", l.ArmorHealthMod, multiplier: false);
            Bonus(section, "Max mana", l.ArmorManaMod, multiplier: false);
            Bonus(section, "Max stamina", l.ArmorStaminaMod, multiplier: false);

            Section(lines, l.ItemCategory == "Clothing" ? "Clothing" : "Armor", section);
        }

        private static void Ratings(List<ItemLine> lines, MarketListing l) {
            var section = new List<ItemLine>();

            Rating(section, "Critical damage", l.GearCritDamage);
            Rating(section, "Critical damage resistance", l.GearCritDamageResist);
            Rating(section, "Damage", l.GearDamage);
            Rating(section, "Damage resistance", l.GearDamageResist);
            Rating(section, "Healing boost", l.GearHealingBoost);
            Rating(section, "Max health", l.GearMaxHealth);

            Section(lines, "Ratings", section);
        }

        private static void Sigil(List<ItemLine> lines, MarketListing l) {
            var section = new List<ItemLine>();

            Add(section, l.SigilEffectName != null ? $"Effect: {l.SigilEffectName}" : "", ItemLineStyle.Text);

            var trigger = new List<string>();
            if (l.SigilTriggerChance.HasValue) {
                trigger.Add($"{Pct(l.SigilTriggerChance.Value, signed: false)} chance");
            }
            if (l.SigilCooldown.HasValue) {
                trigger.Add($"{N(l.SigilCooldown, "0.#")}s cooldown");
            }
            Add(section, Capital(string.Join(", ", trigger)), ItemLineStyle.Text);

            if (l.SigilCurrentLevel.HasValue) {
                section.Add(new ItemLine($"Level {l.SigilCurrentLevel}{(l.SigilMaxLevel.HasValue ? $" of {l.SigilMaxLevel}" : "")}", ItemLineStyle.Text));
            }

            var reserved = new (string Name, double? Value)[] { ("health", l.SigilHealthReserved), ("stamina", l.SigilStaminaReserved), ("mana", l.SigilManaReserved) }
                .Where(r => r.Value > 0).Select(r => $"{Pct(r.Value.Value, signed: false)} {r.Name}").ToList();

            if (reserved.Count > 0) {
                section.Add(new ItemLine("Reserves " + string.Join(", ", reserved), ItemLineStyle.Bad));
            }

            Section(lines, "Sigil", section);
        }

        private static void Materials(List<ItemLine> lines, MarketListing l) {
            var section = new List<ItemLine>();

            if (l.SalvageMaterial != null || l.SalvageUnits.HasValue) {
                var bag = $"{Spaced(l.SalvageMaterial)} salvage".Trim();
                if (l.SalvageWorkmanship.HasValue) {
                    bag += $", workmanship {N(l.SalvageWorkmanship, "0.##")}";
                }
                if (l.SalvageUnits.HasValue) {
                    bag += $", {l.SalvageUnits} units";
                }
                section.Add(new ItemLine(Capital(bag), ItemLineStyle.Text));
            }

            if (l.SalvageTargetTypes != null && l.SalvageTargetTypes.Count > 0) {
                section.Add(new ItemLine("For " + string.Join(", ", l.SalvageTargetTypes.Select(t => Spaced(t).ToLowerInvariant())), ItemLineStyle.Detail));
            }

            if (l.GemMaterialType != null || l.GemQuality.HasValue) {
                var gem = $"{Spaced(l.GemMaterialType)}{(l.IsCarvedJewel == true ? " jewel" : " gem")}".Trim();
                section.Add(new ItemLine(Capital(gem) + (l.GemQuality.HasValue ? $", quality {l.GemQuality}" : ""), ItemLineStyle.Text));
            }

            if (l.TrophyType != null || l.TrophyQuality.HasValue) {
                section.Add(new ItemLine($"{l.TrophyType ?? "Trophy"}{(l.TrophyQuality.HasValue ? $", quality {l.TrophyQuality}" : "")}", ItemLineStyle.Text));
            }

            if (l.SpellSchool != null) {
                section.Add(new ItemLine($"School: {Spaced(l.SpellSchool)}", ItemLineStyle.Text));
            }

            Section(lines, "Materials", section);
        }

        private static void Magic(List<ItemLine> lines, MarketListing l) {
            var parts = new List<string>();

            if (l.Spellcraft.HasValue) {
                parts.Add($"spellcraft {l.Spellcraft}");
            }
            if (l.Difficulty > 0) {
                parts.Add($"arcane lore {l.Difficulty}");
            }
            if (l.Mana.HasValue) {
                parts.Add($"mana {l.Mana:N0}");
            }

            Add(lines, Capital(string.Join(", ", parts)), ItemLineStyle.Text);
        }

        private static void Crafting(List<ItemLine> lines, MarketListing l) {
            var parts = new List<string>();

            if (l.Workmanship.HasValue) {
                parts.Add($"workmanship {N(l.Workmanship, "0.##")}");
            }
            if (l.NumTimesTinkered > 0) {
                parts.Add($"tinkered {l.NumTimesTinkered} time{(l.NumTimesTinkered == 1 ? "" : "s")}");
            }
            if (l.Sockets > 0) {
                parts.Add($"{l.Sockets} socket{(l.Sockets == 1 ? "" : "s")}");
            }

            Add(lines, Capital(string.Join(", ", parts)), ItemLineStyle.Detail);
        }

        private static void Section(List<ItemLine> lines, string heading, List<ItemLine> section) {
            if (section.Count == 0) {
                return;
            }

            lines.Add(new ItemLine(heading, ItemLineStyle.Heading));
            lines.AddRange(section);
        }

        private static void List(List<ItemLine> lines, string heading, List<string> values) {
            if (values == null || values.Count == 0) {
                return;
            }

            lines.Add(new ItemLine(heading, ItemLineStyle.Heading));
            lines.Add(new ItemLine(string.Join(", ", values), ItemLineStyle.Text));
        }

        /// <summary>
        /// A bonus as a percentage: a fraction (0.05 is +5%), or a multiplier on 1 (1.05 is +5%). Nothing for none, or for 0.
        /// </summary>
        private static void Bonus(List<ItemLine> lines, string name, double? value, bool multiplier) {
            if (!value.HasValue) {
                return;
            }

            var fraction = multiplier ? value.Value - 1 : value.Value;

            if (Math.Abs(fraction) < 0.0005) {
                return;
            }

            lines.Add(new ItemLine($"{name} {Pct(fraction, signed: true)}", fraction > 0 ? ItemLineStyle.Good : ItemLineStyle.Bad));
        }

        private static void Rating(List<ItemLine> lines, string name, int? value) {
            if (value > 0) {
                lines.Add(new ItemLine($"{name} rating {value}", ItemLineStyle.Good));
            }
        }

        private static void Add(List<ItemLine> lines, string text, ItemLineStyle style) {
            if (!string.IsNullOrWhiteSpace(text)) {
                lines.Add(new ItemLine(text, style));
            }
        }

        /// <summary>0.123 as "12.3%", or "+12.3%" signed.</summary>
        public static string Pct(double fraction, bool signed) {
            var text = (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            return signed && fraction > 0 ? "+" + text : text;
        }

        private static string N(double? value, string format) {
            return value?.ToString(format, CultureInfo.InvariantCulture) ?? "";
        }

        /// <summary>
        /// An enum's name in words: "SigilTrinket" as "Sigil Trinket", "WarMagic" as "War Magic".
        /// </summary>
        public static string Spaced(string name) {
            if (string.IsNullOrEmpty(name)) {
                return "";
            }

            var spaced = new System.Text.StringBuilder();

            for (var i = 0; i < name.Length; i++) {
                if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1])) {
                    spaced.Append(' ');
                }

                spaced.Append(name[i]);
            }

            return spaced.ToString();
        }

        private static string Capital(string text) {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
