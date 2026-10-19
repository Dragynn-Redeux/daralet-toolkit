using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace DaraletPlayer.Feed {
    // The loot classes set their defaults in SetDefaults, called both when one is made and before one is read: the JSON
    // reader doesn't run constructors or field initialisers.

    public enum LootOutcome {
        /// <summary>Loot it.</summary>
        Keep,
        /// <summary>Loot it, then salvage it.</summary>
        Salvage,
        /// <summary>Say so in chat, but leave it.</summary>
        Notify,
        /// <summary>Leave it, and stop later rules matching it.</summary>
        Ignore,
    }

    /// <summary>What happens when a corpse is opened.</summary>
    public enum CorpseMode {
        AutoLootAutoSalvage,
        /// <summary>Loots, then the Corpse window asks before salvaging.</summary>
        AutoLootConfirmSalvage,
        /// <summary>The Corpse window lists the matches with Loot and Loot and salvage buttons.</summary>
        ConfirmBoth,
        /// <summary>Moves nothing; lists the matches in chat.</summary>
        ChatHighlights,
        /// <summary>Moves nothing; the Corpse window lists the matches, and clicking one loots it.</summary>
        HighlightPopup,
    }

    /// <summary>
    /// A loot rule: a market-style search (the Search tab's filters, keyed by MarketFilter.Key) and what to do with what it
    /// finds. Every condition that's filled in has to hold.
    /// </summary>
    [DataContract]
    public class LootRule {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "enabled")] public bool Enabled;
        [DataMember(Name = "outcome")] public LootOutcome Outcome;
        /// <summary>A market category, or "" for any.</summary>
        [DataMember(Name = "category")] public string Category;
        /// <summary>Tiers as the Search tab shows them, 0 to 7, or "".</summary>
        [DataMember(Name = "minTier")] public string MinTier;
        [DataMember(Name = "maxTier")] public string MaxTier;
        /// <summary>Words the item's name has to have, all of them.</summary>
        [DataMember(Name = "nameContains")] public string NameContains;
        [DataMember(Name = "salvageableOnly")] public bool SalvageableOnly;
        [DataMember(Name = "carvedJewelOnly")] public bool CarvedJewelOnly;
        [DataMember(Name = "values")] public Dictionary<string, string> Values;

        public LootRule() {
            SetDefaults();
        }

        public LootRule Copy() {
            return Json.Read<LootRule>(Json.Write(this));
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) {
            SetDefaults();
        }

        private void SetDefaults() {
            Name = "New rule";
            Enabled = true;
            Outcome = LootOutcome.Keep;
            Category = "";
            MinTier = "";
            MaxTier = "";
            NameContains = "";
            Values = new Dictionary<string, string>();
        }
    }

    /// <summary>A named list of rules, checked in order: the first that matches decides.</summary>
    [DataContract]
    public class LootList {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "rules")] public List<LootRule> Rules;

        public LootList() {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) {
            SetDefaults();
        }

        private void SetDefaults() {
            Name = "My loot";
            Rules = new List<LootRule>();
        }
    }

    /// <summary>
    /// The loot settings (SettingsData.Loot): the lists, which one each character uses, and what opening a corpse does. The
    /// looter starts switched off, so it doesn't fight VirindiTank's over the same corpse.
    /// </summary>
    [DataContract]
    public class LootData {
        [DataMember(Name = "enabled")] public bool Enabled;
        [DataMember(Name = "mode")] public CorpseMode Mode;
        [DataMember(Name = "lists")] public List<LootList> Lists;
        /// <summary>Each character's list, by character name; a character not here uses the first.</summary>
        [DataMember(Name = "activeList")] public Dictionary<string, string> ActiveList;

        public LootData() {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) {
            SetDefaults();
        }

        private void SetDefaults() {
            Mode = CorpseMode.HighlightPopup;
            Lists = new List<LootList> { new LootList() };
            ActiveList = new Dictionary<string, string>();
        }

        /// <summary>The list a character uses: the one they picked, or the first.</summary>
        public LootList ActiveFor(string character) {
            if (Lists.Count == 0) {
                Lists.Add(new LootList());
            }

            if (character != null && ActiveList.TryGetValue(character, out var name)) {
                return Lists.FirstOrDefault(l => l.Name == name) ?? Lists[0];
            }

            return Lists[0];
        }

        public void SetActive(string character, LootList list) {
            if (character != null && list != null) {
                ActiveList[character] = list.Name;
            }
        }

        /// <summary>A list name nothing else has yet: the given one, or it with " 2", " 3"... after it.</summary>
        public string UniqueName(string name) {
            var baseName = string.IsNullOrWhiteSpace(name) ? "My loot" : name.Trim();
            var candidate = baseName;

            for (var i = 2; Lists.Any(l => string.Equals(l.Name, candidate, StringComparison.OrdinalIgnoreCase)); i++) {
                candidate = $"{baseName} {i}";
            }

            return candidate;
        }
    }

    /// <summary>
    /// An item on a corpse, as @playerui corpse sends it (PlayerUiCorpseItem in daralet-ACE): its own facts, and Item, its
    /// description in the Market API's terms (null if the server couldn't make one).
    /// </summary>
    [DataContract]
    public class CorpseItemInfo {
        [DataMember(Name = "guid")] public uint Guid;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "stackSize")] public int StackSize;
        [DataMember(Name = "value")] public int Value;
        [DataMember(Name = "burden")] public int Burden;
        /// <summary>Salvaging could turn it into salvage.</summary>
        [DataMember(Name = "salvageable")] public bool Salvageable;
        [DataMember(Name = "material")] public string Material;
        [DataMember(Name = "workmanship")] public double? Workmanship;
        [DataMember(Name = "item")] public MarketListing Item;
    }

    [DataContract]
    public class CorpseInfo {
        public const string StatusOk = "ok";

        [DataMember(Name = "guid")] public uint Guid;
        /// <summary>ok, gone (not near enough), denied (someone else's to loot) or notcorpse.</summary>
        [DataMember(Name = "status")] public string Status;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "items")] public List<CorpseItemInfo> Items;
    }

    /// <summary>The rule that decided an item, if any.</summary>
    public class LootDecision {
        public CorpseItemInfo Item;
        public LootRule Rule;

        public LootOutcome Outcome => Rule?.Outcome ?? LootOutcome.Ignore;
        public bool Loots => Outcome == LootOutcome.Keep || Outcome == LootOutcome.Salvage;
    }

    /// <summary>
    /// Matching items against rules, and lists as text to share. Each filter compares the same value the Market API's search
    /// does (MarketRepository.cs in daralet-ACE): ward level and the armor and clothing mods per armor slot, as there; "price"
    /// is the item's vendor value here.
    /// </summary>
    public static class Loot {
        /// <summary>
        /// What each numeric filter compares, by its parameter without "min"/"max", and whether it's per armor slot.
        /// </summary>
        private static readonly Dictionary<string, (Func<CorpseItemInfo, double?> Get, bool PerSlot)> Numbers =
            new Dictionary<string, (Func<CorpseItemInfo, double?>, bool)>(StringComparer.OrdinalIgnoreCase) {
                ["Price"] = (i => i.Value, false),
                ["Dps"] = (i => i.Item?.Dps, false),
                ["BaseDamage"] = (i => i.Item?.BaseDamage, false),
                ["DamageVariance"] = (i => i.Item?.DamageVariance, false),
                ["Speed"] = (i => i.Item?.Speed, false),
                ["AttackMod"] = (i => i.Item?.WeaponOffense, false),
                ["PhysDefMod"] = (i => i.Item?.WeaponPhysicalDefense, false),
                ["MagicDefMod"] = (i => i.Item?.WeaponMagicalDefense, false),
                ["WarMagicMod"] = (i => i.Item?.WeaponWarMagicMod, false),
                ["LifeMagicMod"] = (i => i.Item?.WeaponLifeMagicMod, false),
                ["Spellcraft"] = (i => i.Item?.Spellcraft, false),
                ["Difficulty"] = (i => i.Item?.Difficulty, false),
                ["ArmorLevel"] = (i => i.Item?.ArmorLevel, false),
                ["WardLevel"] = (i => i.Item?.WardLevel, true),
                ["ArmorModVsSlash"] = (i => i.Item?.ArmorModVsSlash, false),
                ["ArmorModVsPierce"] = (i => i.Item?.ArmorModVsPierce, false),
                ["ArmorModVsBludgeon"] = (i => i.Item?.ArmorModVsBludgeon, false),
                ["ArmorModVsCold"] = (i => i.Item?.ArmorModVsCold, false),
                ["ArmorModVsFire"] = (i => i.Item?.ArmorModVsFire, false),
                ["ArmorModVsAcid"] = (i => i.Item?.ArmorModVsAcid, false),
                ["ArmorModVsElectric"] = (i => i.Item?.ArmorModVsElectric, false),
                ["ArmorWarMagicMod"] = (i => i.Item?.ArmorWarMagicMod, true),
                ["ArmorLifeMagicMod"] = (i => i.Item?.ArmorLifeMagicMod, true),
                ["ArmorManaRegenMod"] = (i => i.Item?.ArmorManaRegenMod, true),
                ["ArmorManaConversionMod"] = (i => i.Item?.ArmorManaConversionMod, true),
                ["ArmorAttackMod"] = (i => i.Item?.ArmorAttackMod, true),
                ["ArmorShieldMod"] = (i => i.Item?.ArmorShieldMod, true),
                ["ArmorDualWieldMod"] = (i => i.Item?.ArmorDualWieldMod, true),
                ["ArmorThieveryMod"] = (i => i.Item?.ArmorThieveryMod, true),
                ["ArmorRunMod"] = (i => i.Item?.ArmorRunMod, true),
                ["ArmorStaminaRegenMod"] = (i => i.Item?.ArmorStaminaRegenMod, true),
                ["ArmorPhysicalDefMod"] = (i => i.Item?.ArmorPhysicalDefMod, true),
                ["ArmorMagicDefMod"] = (i => i.Item?.ArmorMagicDefMod, true),
                ["ArmorTwohandedCombatMod"] = (i => i.Item?.ArmorTwohandedCombatMod, true),
                ["ArmorHealthRegenMod"] = (i => i.Item?.ArmorHealthRegenMod, true),
                ["ArmorPerceptionMod"] = (i => i.Item?.ArmorPerceptionMod, true),
                ["ArmorDeceptionMod"] = (i => i.Item?.ArmorDeceptionMod, true),
                ["ArmorHealthMod"] = (i => i.Item?.ArmorHealthMod, true),
                ["ArmorManaMod"] = (i => i.Item?.ArmorManaMod, true),
                ["ArmorStaminaMod"] = (i => i.Item?.ArmorStaminaMod, true),
                ["GearCritDamage"] = (i => i.Item?.GearCritDamage, false),
                ["GearCritDamageResist"] = (i => i.Item?.GearCritDamageResist, false),
                ["GearDamage"] = (i => i.Item?.GearDamage, false),
                ["GearDamageResist"] = (i => i.Item?.GearDamageResist, false),
                ["GearHealingBoost"] = (i => i.Item?.GearHealingBoost, false),
                ["GearMaxHealth"] = (i => i.Item?.GearMaxHealth, false),
                ["ProcChance"] = (i => i.Item?.SigilTriggerChance, false),
                ["Cooldown"] = (i => i.Item?.SigilCooldown, false),
                ["HealthReserved"] = (i => i.Item?.SigilHealthReserved, false),
                ["StaminaReserved"] = (i => i.Item?.SigilStaminaReserved, false),
                ["ManaReserved"] = (i => i.Item?.SigilManaReserved, false),
                ["Workmanship"] = (i => i.Item?.SalvageWorkmanship, false),
                ["Units"] = (i => i.Item?.SalvageUnits, false),
                ["PricePerUnit"] = (i => i.Item?.SalvageUnits > 0 ? (double)i.Value / i.Item.SalvageUnits.Value : (double?)null, false),
                ["GemQuality"] = (i => i.Item?.GemQuality, false),
                ["TrophyQuality"] = (i => i.Item?.TrophyQuality, false),
            };

        /// <summary>
        /// What each choice or text filter compares, by parameter: the item's values for it, and whether the rule's text only
        /// has to be in one of them (contains) rather than be one of them (equals). Spaces don't count ("GreenJade" is
        /// "Green Jade").
        /// </summary>
        private static readonly Dictionary<string, (Func<MarketListing, IEnumerable<string>> Get, bool Contains)> Words =
            new Dictionary<string, (Func<MarketListing, IEnumerable<string>>, bool)>(StringComparer.OrdinalIgnoreCase) {
                ["weaponClass"] = (l => One(l.WeaponClass), false),
                ["armorWeightClass"] = (l => One(l.ArmorWeightClass), false),
                ["jewelrySlot"] = (l => One(l.JewelrySlot), false),
                ["sigilTrinketType"] = (l => One(l.SigilTrinketType), false),
                ["sigilEffect"] = (l => One(l.SigilEffectName), false),
                ["salvageTargetType"] = (l => l.SalvageTargetTypes ?? new List<string>(), false),
                ["salvageMaterial"] = (l => One(l.SalvageMaterial), false),
                ["consumableType"] = (l => One(l.ConsumableType), false),
                ["miscType"] = (l => One(l.MiscType), false),
                ["scrollSchool"] = (l => One(l.SpellSchool), false),
                ["gemMaterial"] = (l => One(l.GemMaterialType), false),
                ["trophyType"] = (l => One(l.TrophyType), false),
                ["spellName"] = (l => l.Spells ?? new List<string>(), true),
                ["scrollSpellName"] = (l => l.Spells ?? new List<string>(), true),
            };

        /// <summary>
        /// The rule in the list that decides an item: the first enabled one it matches, or none.
        /// </summary>
        public static LootDecision Decide(LootList list, CorpseItemInfo item) {
            var rule = list?.Rules?.FirstOrDefault(r => r.Enabled && Matches(r, item));
            return new LootDecision { Item = item, Rule = rule };
        }

        public static bool Matches(LootRule rule, CorpseItemInfo item) {
            var listing = item.Item;
            var category = rule.Category ?? "";

            if (category.Length > 0 && !string.Equals(listing?.ItemCategory, category, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (!NameHas(item.Name, rule.NameContains)) {
                return false;
            }

            if (rule.SalvageableOnly && !item.Salvageable) {
                return false;
            }

            // tiers as the Search tab shows them (0-7); the description counts from 1, like the market's
            if (category != "Salvage" && (int.TryParse(rule.MinTier, out _) || int.TryParse(rule.MaxTier, out _))) {
                var tier = listing?.ItemTier;

                if (!tier.HasValue) {
                    return false;
                }

                if (int.TryParse(rule.MinTier, out var min) && tier.Value < min + 1) {
                    return false;
                }

                if (int.TryParse(rule.MaxTier, out var max) && tier.Value > max + 1) {
                    return false;
                }
            }

            if (category == "Misc" && rule.CarvedJewelOnly && listing?.IsCarvedJewel != true) {
                return false;
            }

            foreach (var filter in MarketSearch.Filters) {
                if (filter.Category != null && filter.Category != category) {
                    continue;
                }

                if (rule.Values == null || !rule.Values.TryGetValue(filter.Key, out var raw)) {
                    continue;
                }

                var value = MarketSearch.Convert(filter, raw);

                if (value != null && !FilterHolds(filter, value, item)) {
                    return false;
                }
            }

            return true;
        }

        private static bool FilterHolds(MarketFilter filter, string value, CorpseItemInfo item) {
            var param = filter.Param;

            if (param.Equals("coverage", StringComparison.OrdinalIgnoreCase)) {
                var covers = item.Item?.Coverage ?? new List<string>();
                return value.Split(',').Select(Squash).Where(w => w.Length > 0).All(w => covers.Any(c => Squash(c) == w));
            }

            if (Words.TryGetValue(param, out var words)) {
                if (item.Item == null) {
                    return false;
                }

                var wanted = Squash(value);
                return words.Get(item.Item).Any(v => words.Contains ? Squash(v).Contains(wanted) : Squash(v) == wanted);
            }

            var isMin = param.StartsWith("min", StringComparison.Ordinal);
            var isMax = param.StartsWith("max", StringComparison.Ordinal);

            if ((isMin || isMax) && Numbers.TryGetValue(param.Substring(3), out var number)) {
                var actual = number.Get(item);

                if (!actual.HasValue || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var limit)) {
                    return false;
                }

                if (number.PerSlot) {
                    actual = actual.Value / Math.Max(item.Item?.ArmorSlots ?? 1, 1);
                }

                return isMin ? actual.Value >= limit : actual.Value <= limit;
            }

            // a filter the loot rules don't know how to check yet leaves the rule alone rather than blocking it
            return true;
        }

        private static bool NameHas(string name, string words) {
            var wanted = (words ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return wanted.All(w => (name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string Squash(string text) {
            return (text ?? "").Replace(" ", "").Trim().ToLowerInvariant();
        }

        private static IEnumerable<string> One(string value) {
            return value == null ? new string[0] : new[] { value };
        }

        /// <summary>
        /// A list as text to copy and share: its JSON. The rules' filter keys are the Search tab's, so a list made on one
        /// computer works on another.
        /// </summary>
        public static string Export(LootList list) {
            return Json.Write(list);
        }

        /// <summary>
        /// A list from shared text, or null (with why) if it isn't one.
        /// </summary>
        public static LootList Import(string text, out string problem) {
            problem = null;

            try {
                var list = Json.Read<LootList>((text ?? "").Trim());

                if (list?.Rules == null) {
                    problem = "That isn't a loot list.";
                    return null;
                }

                list.Rules.RemoveAll(r => r == null);
                return list;
            }
            catch (Exception ex) {
                problem = $"That isn't a loot list: {ex.Message}";
                return null;
            }
        }
    }
}
