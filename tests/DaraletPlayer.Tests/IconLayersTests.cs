using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class IconLayersTests {
        [TestMethod]
        public void AListingsIconIsItsHexIdsOnTheTileForItsKind() {
            var icon = IconLayers.Of(new MarketListing { IconHex = "06001234", IconOverlayHex = "06005678", IconUnderlay = "Weapon" });

            Assert.AreEqual(0x060011D2u, icon.Underlay);
            Assert.AreEqual(0x06001234u, icon.Icon);
            Assert.AreEqual(0x06005678u, icon.Overlay);
            Assert.IsFalse(icon.IsEmpty);
        }

        [TestMethod]
        public void AnUnknownKindGetsTheMiscTileAndNoIconIsEmpty() {
            var icon = IconLayers.FromHex(null, null, "SomethingNew");

            Assert.AreEqual(IconLayers.Underlays["Misc"], icon.Underlay);
            Assert.AreEqual(0u, icon.Overlay);
            Assert.IsTrue(icon.IsEmpty, "a tile with nothing on it isn't drawn");
            Assert.IsNull(IconLayers.Of((MarketListing)null));
            Assert.IsNull(IconLayers.Of((ListingIconInfo)null));
        }

        [TestMethod]
        public void TheOutlineIsBlackUnlessTheItemHasAnEffect() {
            Assert.AreEqual(((byte)0, (byte)0, (byte)0), IconLayers.FromHex("06001234", null, "Misc").OutlineColor);
            Assert.AreEqual(((byte)0, (byte)0, (byte)0), IconLayers.FromHex("06001234", null, "Misc", "SomethingNew").OutlineColor);
            Assert.AreEqual(((byte)102, (byte)194, (byte)255), IconLayers.Of(new MarketListing { IconHex = "06001234", IconEffect = "Magical" }).OutlineColor);
            Assert.AreEqual(((byte)30, (byte)110, (byte)255), IconLayers.FromHex("06001234", null, "SigilBlue", "SigilBlue").OutlineColor);
        }

        [TestMethod]
        public void TheClientsUiEffectsPickTheSameEffectAsTheMarketApi() {
            Assert.AreEqual("Undef", IconLayers.EffectOf(0));
            Assert.AreEqual("Magical", IconLayers.EffectOf(0x0001));
            Assert.AreEqual("Fire", IconLayers.EffectOf(0x0020 | 0x0001), "an element before magic");
            Assert.AreEqual("Electric", IconLayers.EffectOf(0x0040));
            Assert.AreEqual("Cold", IconLayers.EffectOf(0x0080));
            Assert.AreEqual("Acid", IconLayers.EffectOf(0x0100));
        }

        [TestMethod]
        public void YourListingsReadTheIconTheServerSends() {
            var market = Json.Read<MarketInfo>(
                "{\"listings\":[{\"id\":12,\"name\":\"Ono\",\"icon\":{\"icon\":\"06001234\",\"overlay\":null,\"underlay\":\"Armor\"}}],"
                    + "\"sales\":[{\"id\":11,\"name\":\"Tachi\",\"icon\":null}],\"expired\":[]}");

            var icon = IconLayers.Of(market.Listings[0].Icon);

            Assert.AreEqual(0x06001234u, icon.Icon);
            Assert.AreEqual(IconLayers.Underlays["Armor"], icon.Underlay);
            Assert.IsNull(IconLayers.Of(market.Sales[0].Icon));
        }
    }
}
