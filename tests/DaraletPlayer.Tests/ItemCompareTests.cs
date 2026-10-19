using System.Collections.Generic;
using System.Linq;
using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class ItemCompareTests {
        private static CorpseItemInfo Armor(int armorLevel, double attackMod, int burden, params string[] spells) {
            return new CorpseItemInfo {
                Name = "Breastplate", Burden = burden,
                Item = new MarketListing { ArmorLevel = armorLevel, ArmorAttackMod = attackMod, Spells = spells.ToList() },
            };
        }

        private static CompareRow Row(List<CompareRow> rows, string label) {
            return rows.Single(r => r.Stat.Label == label);
        }

        [TestMethod]
        public void OnlyStatsEitherItemHasAreCompared() {
            var rows = ItemCompare.Rows(Armor(500, 0.05, 900), Armor(450, 0, 1000));

            CollectionAssert.AreEquivalent(new[] { "Armor level", "Attack (armor) / slot", "Burden" }, rows.Select(r => r.Stat.Label).ToList());
            Assert.IsNull(Row(rows, "Attack (armor) / slot").Equipped, "a zero counts as not having it");
        }

        [TestMethod]
        public void BetterFollowsWhichWayIsGood() {
            var rows = ItemCompare.Rows(Armor(500, 0.05, 900), Armor(450, 0.08, 1000));

            Assert.IsTrue(Row(rows, "Armor level").Better.Value, "more armor is better");
            Assert.IsFalse(Row(rows, "Attack (armor) / slot").Better.Value, "less attack is worse");
            Assert.IsTrue(Row(rows, "Burden").Better.Value, "less burden is better");
        }

        [TestMethod]
        public void EqualStatsHaveNoChange() {
            var row = Row(ItemCompare.Rows(Armor(500, 0, 900), Armor(500, 0, 900)), "Armor level");

            Assert.IsNull(row.Change);
            Assert.IsNull(row.Better);
        }

        [TestMethod]
        public void ChangesAreWrittenTheWayTheStatIs() {
            var rows = ItemCompare.Rows(Armor(500, 0.05, 900), Armor(450, 0.08, 1000));

            Assert.AreEqual("+50", ItemCompare.ChangeText(Row(rows, "Armor level").Stat, Row(rows, "Armor level").Change.Value));
            Assert.AreEqual("-3%", ItemCompare.ChangeText(Row(rows, "Attack (armor) / slot").Stat, Row(rows, "Attack (armor) / slot").Change.Value));
            Assert.AreEqual("5%", ItemCompare.Text(Row(rows, "Attack (armor) / slot").Stat, 0.05));

            var attack = ItemCompare.Stats.Single(s => s.Label == "Attack");
            Assert.AreEqual("12%", ItemCompare.Text(attack, 1.12), "a multiplier shows as its bonus");
            Assert.AreEqual("+3%", ItemCompare.ChangeText(attack, 1.15 - 1.12));
            Assert.AreEqual("-", ItemCompare.Text(attack, null));
        }

        [TestMethod]
        public void ProtectionsCompareAsTheArmorTheyGive() {
            var mine = new CorpseItemInfo { Item = new MarketListing { ArmorLevel = 600, ArmorModVsSlash = 1.2 } };
            var worn = new CorpseItemInfo { Item = new MarketListing { ArmorLevel = 500, ArmorModVsSlash = 1.0 } };
            var row = Row(ItemCompare.Rows(mine, worn), "Vs slash");

            Assert.AreEqual(720, row.This);
            Assert.AreEqual(500, row.Equipped);
            Assert.AreEqual("+220", ItemCompare.ChangeText(row.Stat, row.Change.Value));
        }

        [TestMethod]
        public void WardAndArmorModsCompareASlot() {
            // a two-slot coat with 40 ward and 6% attack is 20 ward and 3% attack a slot, against a one-slot helm's 25 and 4%
            var coat = new CorpseItemInfo { Item = new MarketListing { ArmorSlots = 2, WardLevel = 40, ArmorAttackMod = 0.06 } };
            var helm = new CorpseItemInfo { Item = new MarketListing { ArmorSlots = 1, WardLevel = 25, ArmorAttackMod = 0.04 } };
            var rows = ItemCompare.Rows(coat, helm);

            Assert.AreEqual(20, Row(rows, "Ward level / slot").This);
            Assert.IsFalse(Row(rows, "Ward level / slot").Better.Value, "more ward in all, but less a slot");
            Assert.AreEqual("-1%", ItemCompare.ChangeText(Row(rows, "Attack (armor) / slot").Stat, Row(rows, "Attack (armor) / slot").Change.Value));
        }

        [TestMethod]
        public void SpellsGainedAndLost() {
            var (gained, lost) = ItemCompare.Spells(Armor(500, 0, 900, "Major Willpower", "Epic Armor"), Armor(450, 0, 900, "major willpower", "Minor Focus"));

            CollectionAssert.AreEqual(new[] { "Epic Armor" }, gained);
            CollectionAssert.AreEqual(new[] { "Minor Focus" }, lost);
        }
    }
}
