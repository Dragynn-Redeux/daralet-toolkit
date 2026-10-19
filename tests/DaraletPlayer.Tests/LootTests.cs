using System.Collections.Generic;
using System.Linq;
using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class LootTests {
        private static string Key(string category, string param) {
            return MarketSearch.Filters.Single(f => f.Category == category && f.Param == param).Key;
        }

        private static CorpseItemInfo Sword(double attackMod = 1.12, int tier = 7) {
            return new CorpseItemInfo {
                Guid = 1, Name = "Fire Tachi", Value = 5000, Salvageable = true,
                Item = new MarketListing { ItemName = "Fire Tachi", ItemCategory = "Weapon", WeaponClass = "Martial", ItemTier = tier, WeaponOffense = attackMod, Dps = 210, Spells = new List<string> { "Minor Swift Hunter" } },
            };
        }

        private static CorpseItemInfo Helm() {
            return new CorpseItemInfo {
                Guid = 2, Name = "Helm", Value = 900, Salvageable = true,
                Item = new MarketListing { ItemName = "Helm", ItemCategory = "Armor", ArmorWeightClass = "Heavy", ItemTier = 7, ArmorSlots = 2, WardLevel = 40, ArmorAttackMod = 0.06, Coverage = new List<string> { "Head", "Neck" } },
            };
        }

        [TestMethod]
        public void ARuleNeedsEveryConditionFilledIn() {
            var rule = new LootRule { Category = "Weapon", MinTier = "6", Values = { [Key("Weapon", "minAttackMod")] = "10", [Key("Weapon", "weaponClass")] = "Martial" } };

            Assert.IsTrue(Loot.Matches(rule, Sword()));
            Assert.IsFalse(Loot.Matches(rule, Sword(attackMod: 1.05)), "attack mod 5% is under 10%");
            Assert.IsFalse(Loot.Matches(rule, Sword(tier: 6)), "API tier 6 is T5 on the Search tab, under T6");
            Assert.IsFalse(Loot.Matches(rule, Helm()), "wrong category");
        }

        [TestMethod]
        public void FiltersFromAnotherCategoryDontCount() {
            var rule = new LootRule { Category = "Weapon", Values = { [Key("Armor", "minArmorLevel")] = "9999" } };

            Assert.IsTrue(Loot.Matches(rule, Sword()));
        }

        [TestMethod]
        public void WardAndArmorModsArePerSlotLikeTheMarket() {
            // 40 ward over 2 slots is 20 a slot; 6% attack over 2 slots is 3% a slot
            Assert.IsTrue(Loot.Matches(new LootRule { Category = "Armor", Values = { [Key("Armor", "minWardLevel")] = "20" } }, Helm()));
            Assert.IsFalse(Loot.Matches(new LootRule { Category = "Armor", Values = { [Key("Armor", "minWardLevel")] = "21" } }, Helm()));
            Assert.IsTrue(Loot.Matches(new LootRule { Category = "Armor", Values = { [Key("Armor", "minArmorAttackMod")] = "3" } }, Helm()));
            Assert.IsFalse(Loot.Matches(new LootRule { Category = "Armor", Values = { [Key("Armor", "minArmorAttackMod")] = "4" } }, Helm()));
        }

        [TestMethod]
        public void WordsAndLists() {
            Assert.IsTrue(Loot.Matches(new LootRule { Category = "Weapon", Values = { [Key("Weapon", "spellName")] = "swift hunter" } }, Sword()));
            Assert.IsFalse(Loot.Matches(new LootRule { Category = "Weapon", Values = { [Key("Weapon", "spellName")] = "blood drinker" } }, Sword()));
            Assert.IsTrue(Loot.Matches(new LootRule { Category = "Armor", Values = { [Key("Armor", "coverage")] = "head, neck" } }, Helm()));
            Assert.IsFalse(Loot.Matches(new LootRule { Category = "Armor", Values = { [Key("Armor", "coverage")] = "Head, Chest" } }, Helm()));
            Assert.IsTrue(Loot.Matches(new LootRule { NameContains = "fire tachi" }, Sword()));
            Assert.IsFalse(Loot.Matches(new LootRule { NameContains = "frost" }, Sword()));
        }

        [TestMethod]
        public void PriceIsTheItemsValueAndSalvageableOnlyCounts() {
            Assert.IsTrue(Loot.Matches(new LootRule { Values = { [Key(null, "minPrice")] = "4000" } }, Sword()));
            Assert.IsFalse(Loot.Matches(new LootRule { Values = { [Key(null, "minPrice")] = "6000" } }, Sword()));

            var bag = new CorpseItemInfo { Name = "Salvage", Salvageable = false };
            Assert.IsFalse(Loot.Matches(new LootRule { SalvageableOnly = true }, bag));
        }

        [TestMethod]
        public void TheFirstEnabledRuleDecides() {
            var list = new LootList {
                Rules = {
                    new LootRule { Name = "off", Enabled = false, Outcome = LootOutcome.Keep },
                    new LootRule { Name = "junk swords", Outcome = LootOutcome.Salvage, Category = "Weapon" },
                    new LootRule { Name = "everything", Outcome = LootOutcome.Notify },
                },
            };

            var salvageable = Sword();
            salvageable.Workmanship = 5;
            var sword = Loot.Decide(list, salvageable);
            Assert.AreEqual("junk swords", sword.Rule.Name);
            Assert.IsTrue(sword.Loots);
            Assert.AreEqual(LootOutcome.Salvage, sword.Outcome);

            var helm = Loot.Decide(list, Helm());
            Assert.AreEqual(LootOutcome.Notify, helm.Outcome);
            Assert.IsFalse(helm.Loots);

            Assert.IsNull(Loot.Decide(new LootList(), Helm()).Rule);
        }

        [TestMethod]
        public void AnItemWithoutADescriptionOnlyMatchesPlainRules() {
            var plain = new CorpseItemInfo { Name = "Pyreal Mote", Value = 50 };

            Assert.IsTrue(Loot.Matches(new LootRule { NameContains = "mote" }, plain));
            Assert.IsFalse(Loot.Matches(new LootRule { Category = "Misc" }, plain));
            Assert.IsFalse(Loot.Matches(new LootRule { MinTier = "1" }, plain));
        }

        [TestMethod]
        public void ListsShareAsTextAndComeBackTheSame() {
            var list = new LootList { Name = "Melee mule", Rules = { new LootRule { Name = "swords", Outcome = LootOutcome.Salvage, Category = "Weapon", MinTier = "6", Values = { ["Weapon:minDps"] = "200" } } } };

            var copy = Loot.Import(Loot.Export(list), out var problem);

            Assert.IsNull(problem);
            Assert.AreEqual("Melee mule", copy.Name);
            Assert.AreEqual(LootOutcome.Salvage, copy.Rules[0].Outcome);
            Assert.AreEqual("200", copy.Rules[0].Values["Weapon:minDps"]);
            Assert.IsTrue(copy.Rules[0].Enabled);

            Assert.IsNull(Loot.Import("not a list", out problem));
            Assert.IsNotNull(problem);
        }

        [TestMethod]
        public void EachCharacterUsesTheirOwnList() {
            var data = new LootData();
            var mule = new LootList { Name = data.UniqueName("My loot") };
            data.Lists.Add(mule);

            Assert.AreEqual("My loot 2", mule.Name);
            Assert.AreSame(data.Lists[0], data.ActiveFor("Bob"));

            data.SetActive("Bob", mule);
            Assert.AreSame(mule, data.ActiveFor("Bob"));
            Assert.AreSame(data.Lists[0], data.ActiveFor("Alice"));
        }

        [TestMethod]
        public void ReadsACorpseAsTheServerWritesIt() {
            // nulls are left out of a corpse reply (PlayerUiProtocol omitNulls)
            var json = "{\"guid\":2147500000,\"status\":\"ok\",\"name\":\"Corpse of Drudge\",\"items\":[{\"guid\":2147500001,\"name\":\"Fire Tachi\","
                + "\"stackSize\":1,\"value\":5000,\"burden\":250,\"salvageable\":true,\"material\":\"Steel\",\"workmanship\":6.5,"
                + "\"item\":{\"id\":0,\"itemName\":\"Fire Tachi\",\"itemCategory\":\"Weapon\",\"weaponClass\":\"Martial\",\"itemTier\":7,"
                + "\"weaponOffense\":1.12,\"coverage\":[],\"spells\":[\"Minor Swift Hunter\"]}}]}";

            var corpse = Json.Read<CorpseInfo>(json);
            var item = corpse.Items.Single();

            Assert.AreEqual(CorpseInfo.StatusOk, corpse.Status);
            Assert.AreEqual("Corpse of Drudge", corpse.Name);
            Assert.AreEqual(2147500001u, item.Guid);
            Assert.IsTrue(item.Salvageable);
            Assert.AreEqual(6.5, item.Workmanship);
            Assert.AreEqual("Martial", item.Item.WeaponClass);
            Assert.AreEqual(1.12, item.Item.WeaponOffense);
            Assert.IsNull(item.Item.Dps);
        }

        [TestMethod]
        public void ASalvageRuleOnlyTakesItemsWithAWorkmanship() {
            var list = new LootList {
                Rules = {
                    new LootRule { Name = "Salvage weapons", Category = "Weapon", Outcome = LootOutcome.Salvage },
                    new LootRule { Name = "Keep weapons", Category = "Weapon", Outcome = LootOutcome.Keep },
                },
            };

            var withWorkmanship = Sword();
            withWorkmanship.Workmanship = 6;
            var without = Sword();

            Assert.AreEqual("Salvage weapons", Loot.Decide(list, withWorkmanship).Rule.Name);
            Assert.AreEqual("Keep weapons", Loot.Decide(list, without).Rule.Name, "left to the rules after it");
        }
    }
}
