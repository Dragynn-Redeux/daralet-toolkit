using System.Collections.Generic;
using System.Linq;
using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class ItemLinesTests {
        private static List<string> Texts(List<ItemLine> lines) => lines.Select(l => l.Text).ToList();

        private static ItemLine Line(List<ItemLine> lines, string startsWith) => lines.FirstOrDefault(l => l.Text.StartsWith(startsWith));

        [TestMethod]
        public void AWeaponShowsEveryStatItHas() {
            var lines = ItemLines.Build(new CorpseItemInfo {
                Name = "Fire Tachi", Value = 5000, Burden = 250, Salvageable = true, Material = "WhiteSapphire", Workmanship = 6.5,
                Item = new MarketListing {
                    ItemName = "Fire Tachi", ItemCategory = "Weapon", WeaponClass = "Martial", ItemTier = 7, LevelReq = 75,
                    ReqAttribute = "Strength", ReqAttributeAmount = 230, OtherWieldRequirements = new List<string> { "150 Heavy Weapons" },
                    BaseDamage = 239, DamageVariance = 0.64, AvgDamage = 162.1, Speed = 1.43, Dps = 174.28, WeaponOffense = 1.12,
                    WeaponPhysicalDefense = 0.95, Spellcraft = 304, Mana = 231, Workmanship = 5, NumTimesTinkered = 2,
                    Properties = new List<string> { "Biting Strike" }, Spells = new List<string> { "Minor Swift Hunter" },
                },
            });

            var texts = Texts(lines);

            Assert.AreEqual(ItemLineStyle.Title, lines[0].Style);
            Assert.AreEqual("Fire Tachi", texts[0]);
            Assert.AreEqual("Weapon, Martial, T6", texts[1]);
            CollectionAssert.Contains(texts, "Requires level 75");
            CollectionAssert.Contains(texts, "Requires 230 Strength");
            CollectionAssert.Contains(texts, "Requires 150 Heavy Weapons");
            CollectionAssert.Contains(texts, "Damage 239 (64% variance, 162.1 on average)");
            CollectionAssert.Contains(texts, "Speed 1.43, 174.3 damage a second");
            Assert.AreEqual(ItemLineStyle.Good, Line(lines, "Attack").Style);
            Assert.AreEqual("Attack +12%", Line(lines, "Attack").Text);
            Assert.AreEqual(ItemLineStyle.Bad, Line(lines, "Melee defense").Style);
            Assert.AreEqual("Melee defense -5%", Line(lines, "Melee defense").Text);
            CollectionAssert.Contains(texts, "Spellcraft 304, mana 231");
            CollectionAssert.Contains(texts, "Workmanship 5, tinkered 2 times");
            CollectionAssert.Contains(texts, "Minor Swift Hunter");
            CollectionAssert.Contains(texts, "Value 5,000   Burden 250");
            CollectionAssert.Contains(texts, "Salvages to White Sapphire, workmanship 6.5");
            Assert.IsTrue(lines.Any(l => l.Style == ItemLineStyle.Heading && l.Text == "Weapon"));
            Assert.IsFalse(lines.Any(l => l.Text == "Armor"), "no empty sections");
        }

        [TestMethod]
        public void ArmorShowsWardPerSlotProtectionsAndMods() {
            var texts = Texts(ItemLines.Build(new MarketListing {
                ItemName = "Helm", ItemCategory = "Armor", ArmorWeightClass = "Heavy", ItemTier = 8, ArmorLevel = 600, WardLevel = 40,
                ArmorSlots = 2, ArmorModVsSlash = 1.2, ArmorModVsFire = 0.8, ArmorAttackMod = 0.06, Coverage = new List<string> { "Head", "UpperArms" },
                GearCritDamage = 3,
            }));

            CollectionAssert.Contains(texts, "Armor, Heavy, T7");
            CollectionAssert.Contains(texts, "Armor level 600");
            CollectionAssert.Contains(texts, "Ward 40 (20 a slot over 2 slots)");
            CollectionAssert.Contains(texts, "Protection: Slash 1.20, Fire 0.80");
            CollectionAssert.Contains(texts, "Covers Head, Upper Arms");
            CollectionAssert.Contains(texts, "Attack +6%");
            CollectionAssert.Contains(texts, "Critical damage rating 3");
        }

        [TestMethod]
        public void SigilsAndSalvage() {
            var sigil = Texts(ItemLines.Build(new MarketListing {
                ItemName = "Scarab of Growth", ItemCategory = "SigilTrinket", SigilTrinketType = "Scarab", SigilEffectName = "Growth",
                SigilTriggerChance = 0.15, SigilCooldown = 30, SigilCurrentLevel = 2, SigilMaxLevel = 5, SigilManaReserved = 0.1,
            }));

            CollectionAssert.Contains(sigil, "Sigil Trinket, Scarab");
            CollectionAssert.Contains(sigil, "Effect: Growth");
            CollectionAssert.Contains(sigil, "15% chance, 30s cooldown");
            CollectionAssert.Contains(sigil, "Level 2 of 5");
            CollectionAssert.Contains(sigil, "Reserves 10% mana");

            var bag = Texts(ItemLines.Build(new MarketListing {
                ItemName = "Salvage", ItemCategory = "Salvage", SalvageMaterial = "GreenJade", SalvageWorkmanship = 7.25, SalvageUnits = 100,
                SalvageTargetTypes = new List<string> { "Weapon", "Armor" },
            }));

            CollectionAssert.Contains(bag, "Green Jade salvage, workmanship 7.25, 100 units");
            CollectionAssert.Contains(bag, "For weapon, armor");
        }

        [TestMethod]
        public void AnItemWithoutADescriptionStillHasItsName() {
            var lines = ItemLines.Build(new CorpseItemInfo { Name = "Pyreal Mote", Value = 50, Burden = 5, StackSize = 3 });
            var texts = Texts(lines);

            Assert.AreEqual("Pyreal Mote", texts[0]);
            CollectionAssert.Contains(texts, "Value 50   Burden 5   Stack 3");
        }

        [TestMethod]
        public void TiersAndWords() {
            Assert.AreEqual("T0", ItemLines.Tier(1));
            Assert.AreEqual("T7", ItemLines.Tier(8));
            Assert.AreEqual("Sigil Trinket", ItemLines.Spaced("SigilTrinket"));
            Assert.AreEqual("War Magic", ItemLines.Spaced("WarMagic"));
            Assert.AreEqual("+5%", ItemLines.Pct(0.05, signed: true));
            Assert.AreEqual("-2.5%", ItemLines.Pct(-0.025, signed: true));
        }
    }
}
