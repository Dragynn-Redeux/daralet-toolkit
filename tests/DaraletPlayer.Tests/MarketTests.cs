using System;
using System.Collections.Generic;
using System.Linq;
using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class MarketTests {
        private static MarketFilter Filter(string category, string param) {
            return MarketSearch.Filters.Single(f => f.Category == category && f.Param == param);
        }

        [TestMethod]
        public void ValuesAreSentTheWayTheWebsiteSendsThem() {
            Assert.AreEqual("250", MarketSearch.Convert(Filter("Weapon", "minDps"), " 250 "));
            Assert.AreEqual("0.15", MarketSearch.Convert(Filter("Weapon", "minDamageVariance"), "15"));
            Assert.AreEqual("1.15", MarketSearch.Convert(Filter("Weapon", "minAttackMod"), "15"));
            Assert.AreEqual("Martial", MarketSearch.Convert(Filter("Weapon", "weaponClass"), "Martial"));
            Assert.IsNull(MarketSearch.Convert(Filter("Weapon", "minDps"), ""));
            Assert.IsNull(MarketSearch.Convert(Filter("Weapon", "minDps"), "lots"));
        }

        [TestMethod]
        public void TheQueryHasOnlyTheCategorysFilters() {
            var values = new Dictionary<string, string> {
                [Filter(null, "minPrice").Key] = "1000",
                [Filter("Weapon", "minAttackMod").Key] = "10",
                [Filter("Armor", "minArmorLevel").Key] = "500",
            };

            var query = MarketSearch.Query("ono", "Weapon", "5", "", values, "price", true, 2);

            Assert.AreEqual("q=ono&minTier=6&itemCategory=Weapon&minPrice=1000&minAttackMod=1.1&sortBy=price&sortDir=desc&page=2&pageSize=20", query);
        }

        [TestMethod]
        public void SalvageHasNoTiersAndCarvedJewelsAreMiscOnly() {
            var salvage = MarketSearch.Query("", "Salvage", "3", "7", null, "", false, 1);
            Assert.AreEqual("itemCategory=Salvage&page=1&pageSize=20", salvage);

            Assert.IsTrue(MarketSearch.Query("", "Misc", "", "", null, "", false, 1, carvedJewelOnly: true).Contains("carvedJewelOnly=true"));
            Assert.IsFalse(MarketSearch.Query("", "Weapon", "", "", null, "", false, 1, carvedJewelOnly: true).Contains("carvedJewelOnly"));
        }

        [TestMethod]
        public void TextIsEscaped() {
            StringAssert.StartsWith(MarketSearch.Query("Tusker & Co", "", "", "", null, "", false, 1), "q=Tusker%20%26%20Co&");
        }

        [TestMethod]
        public void FilterKeysAreUniqueAndChoicesStartWithAny() {
            CollectionAssert.AllItemsAreUnique(MarketSearch.Filters.Select(f => f.Key).ToList());

            foreach (var filter in MarketSearch.Filters.Where(f => f.Kind == FilterKind.Choice)) {
                Assert.AreEqual("", filter.Options[0].Value, filter.Label);
            }

            Assert.IsTrue(Filter("Salvage", "salvageMaterial").Options.Any(o => o.Value == "GreenJade" && o.Label == "Green Jade"));
        }

        [TestMethod]
        public void ReadsAListingAsTheMarketApiSendsIt() {
            // trimmed from a real reply, with fields the plugin doesn't read left in to show they're skipped
            var json = "{\"totalCount\":2,\"page\":1,\"pageSize\":1,\"listings\":[{\"id\":101,\"itemWeenieClassId\":336,\"listedPrice\":1000,"
                + "\"itemTier\":6,\"marketVendorTier\":6,\"createdAtUtc\":\"2026-10-17T02:25:42.18935\",\"expiresAtUtc\":\"2026-10-24T02:25:42.18935\","
                + "\"itemName\":\"Ono\",\"iconHex\":\"06001642\",\"iconOverlayHex\":null,\"levelReq\":null,\"reqAttribute\":\"Strength\","
                + "\"reqAttributeAmount\":230,\"otherWieldRequirements\":[],\"itemCategory\":\"Weapon\",\"weaponClass\":\"Martial\","
                + "\"coverage\":[],\"imbuedEffects\":[],\"properties\":[\"Biting Strike\"],\"spells\":[\"Minor Swift Hunter\"],\"baseDamage\":239,"
                + "\"speed\":1.4300000000000002,\"dps\":174.2833555933308,\"spellcraft\":304,\"difficulty\":0,\"workmanship\":5,"
                + "\"numTimesTinkered\":null,\"armorLevel\":null,\"salvageTargetTypes\":[],\"salvageMaterial\":null,\"salvageUnits\":null}]}";

            var result = Json.Read<MarketSearchResult>(json);
            var listing = result.Listings.Single();

            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual("Ono", listing.ItemName);
            Assert.AreEqual(1000L, listing.ListedPrice);
            Assert.AreEqual(6, listing.ItemTier);
            Assert.AreEqual(230, listing.ReqAttributeAmount);
            Assert.AreEqual(239, listing.BaseDamage);
            Assert.AreEqual(174.28, listing.Dps.Value, 0.01);
            Assert.AreEqual(5.0, listing.Workmanship);
            Assert.IsNull(listing.ArmorLevel);
            CollectionAssert.AreEqual(new[] { "Minor Swift Hunter" }, listing.Spells);

            Assert.AreEqual("3d 00h", MarketSearch.ExpiresIn(listing.ExpiresAtUtc, new DateTime(2026, 10, 21, 2, 25, 42, DateTimeKind.Utc)));
            Assert.AreEqual("expired", MarketSearch.ExpiresIn(listing.ExpiresAtUtc, new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual("", MarketSearch.ExpiresIn("soon", DateTime.UtcNow));
        }

        [TestMethod]
        public void SpansLikeTheOtherTimers() {
            Assert.AreEqual("45s", MarketSearch.Span(45));
            Assert.AreEqual("12m", MarketSearch.Span(12 * 60 + 5));
            Assert.AreEqual("3h 07m", MarketSearch.Span(3 * 3600 + 7 * 60));
            Assert.AreEqual("2d 04h", MarketSearch.Span(2 * 86400 + 4 * 3600));
            Assert.AreEqual("0s", MarketSearch.Span(-10));
        }

        [TestMethod]
        public void ReadsMyMarketAsTheServerWritesIt() {
            var json = "{\"nearBroker\":true,\"brokerName\":\"Market Broker\",\"brokerDistance\":4.5,\"payoutFee\":0.05,\"cancelFee\":0.1,"
                + "\"pendingPayout\":9500,\"listings\":[{\"id\":12,\"name\":\"Ono\",\"weenieClassId\":336,\"tier\":6,\"price\":1000,"
                + "\"expiresIn\":3600}],\"sales\":[{\"id\":11,\"name\":\"Tachi\",\"weenieClassId\":337,\"price\":10000,\"payout\":9500,"
                + "\"claimed\":false,\"soldAgo\":120}],\"expired\":[{\"id\":10,\"name\":\"Yari\",\"weenieClassId\":338,\"price\":500,"
                + "\"expiredAgo\":60}]}";

            var market = Json.Read<MarketInfo>(json);

            Assert.IsTrue(market.NearBroker);
            Assert.AreEqual(4.5f, market.BrokerDistance);
            Assert.AreEqual(0.05, market.PayoutFee);
            Assert.AreEqual(9500L, market.PendingPayout);
            Assert.AreEqual(12, market.Listings[0].Id);
            Assert.AreEqual(3600, market.Listings[0].ExpiresIn);
            Assert.IsFalse(market.Sales[0].Claimed);
            Assert.AreEqual(9500, market.Sales[0].Payout);
            Assert.AreEqual("Yari", market.Expired[0].Name);
        }
    }
}
