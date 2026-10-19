using System.IO;
using System.Linq;
using System.Text;
using DaraletPlayer.Feed;
using DaraletPlayer.Map;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class MapTests {
        private static Stream Xml(string body) {
            return new MemoryStream(Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='utf-8'?><locations>" + body + "</locations>"));
        }

        [TestMethod]
        public void ReadsGoArrowLocations() {
            var locations = Locations.Read(Xml(
                "<loc id=\"5\" name=\"Al-Arqas\" type=\"Town\" NS=\"-31.1\" EW=\"13.7\">125</loc>"
                + "<loc id=\"14\" name=\"G-TN to Cragstone\" type=\"TownPortal\" NS=\"13.7\" EW=\"0.6\" exitNS=\"25.6\" exitEW=\"49.2\"></loc>"
                + "<loc id=\"59\" name=\"Empyrean Garrison\" type=\"Dungeon\" NS=\"-54.2\" EW=\"81.8\" dungeonId=\"353\">Level 30 Capstone</loc>"
                + "<loc id=\"229\" name=\"Dungeon Muddy\" type=\"Dungeon\" NS=\"-27\" EW=\"71\" dungeonId=\"438\">todo</loc>"
                + "<loc id=\"420\" name=\"About\" type=\"Custom\" NS=\"-101.9\" EW=\"101.9\">Adapted from stock GoArrow</loc>"
                + "<loc id=\"999\" name=\"Broken\" type=\"Town\" NS=\"north\" EW=\"1\"></loc>"));

            Assert.AreEqual(4, locations.Count);

            var town = locations[0];
            Assert.AreEqual("Al-Arqas", town.Name);
            Assert.AreEqual("Town", town.Type);
            Assert.AreEqual(13.7f, town.X, 0.001f);
            Assert.AreEqual(-31.1f, town.Y, 0.001f);
            Assert.AreEqual("125", town.Notes);
            Assert.IsFalse(town.IsPortal);

            var portal = locations[1];
            Assert.IsTrue(portal.IsPortal);
            Assert.AreEqual(49.2f, portal.ExitX.Value, 0.001f);
            Assert.AreEqual(25.6f, portal.ExitY.Value, 0.001f);

            Assert.AreEqual(353, locations[2].DungeonId);
            Assert.AreEqual("Level 30 Capstone", locations[2].Notes);
            Assert.AreEqual("", locations[3].Notes);
        }

        [TestMethod]
        public void TheBuiltInLocationsLoad() {
            var locations = Locations.LoadDefault(Path.GetTempPath() + "no-such-folder", null, out var source);

            Assert.AreEqual("built in", source);
            Assert.IsTrue(locations.Count > 1500, $"only {locations.Count}");
            Assert.IsTrue(locations.Any(l => l.Name == "Hebian-to" && l.Type == "Town"));
            Assert.IsFalse(locations.Any(l => l.Type == "Custom"));

            // every type in the file is one the World tab has a checkbox for
            var known = Locations.Types.Select(t => t.Type).ToList();
            CollectionAssert.IsSubsetOf(locations.Select(l => l.Type).Distinct().ToList(), known);
        }

        [TestMethod]
        public void AFileNextToThePluginIsReadInstead() {
            var folder = Path.Combine(Path.GetTempPath(), "DaraletPlayerTests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try {
                File.WriteAllText(Path.Combine(folder, Locations.FileName),
                    "<locations><loc id=\"1\" name=\"Test Town\" type=\"Town\" NS=\"1\" EW=\"2\"></loc></locations>");

                var locations = Locations.LoadDefault(folder, null, out var source);

                Assert.AreEqual(1, locations.Count);
                Assert.AreEqual(Path.Combine(folder, Locations.FileName), source);
            }
            finally {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void SearchFindsEveryWordAndPutsTheBestFirst() {
            var locations = Locations.Read(Xml(
                "<loc id=\"1\" name=\"Hebian-to Sewers\" type=\"Dungeon\" NS=\"1\" EW=\"1\"></loc>"
                + "<loc id=\"2\" name=\"Hebian-To\" type=\"Town\" NS=\"1\" EW=\"1\"></loc>"
                + "<loc id=\"3\" name=\"Hebian-to Lifestone\" type=\"Lifestone\" NS=\"1\" EW=\"1\"></loc>"
                + "<loc id=\"4\" name=\"Old Hebian Ruin\" type=\"Landmark\" NS=\"1\" EW=\"1\"></loc>"));

            var results = Locations.Search(locations, "hebian", 10);

            Assert.AreEqual(4, results.Count);
            Assert.AreEqual("Hebian-To", results[0].Name);
            Assert.AreEqual("Old Hebian Ruin", results[3].Name);

            Assert.AreEqual("Hebian-to Sewers", Locations.Search(locations, "sewers hebian", 10).Single().Name);
            Assert.AreEqual(0, Locations.Search(locations, "  ", 10).Count);
            Assert.AreEqual(2, Locations.Search(locations, "hebian", 2).Count);
        }

        [TestMethod]
        public void MapCoordsFromALandcellMatchTheGame() {
            // the middle of Holtburg's landblock, outdoors and in a building (the same sums either way)
            Navigation.MapCoords(0xA9B4001C, 96, 96, out var x, out var y);
            Assert.AreEqual(33.6f, x, 0.001f);
            Assert.AreEqual(42.4f, y, 0.001f);

            Assert.IsTrue(Navigation.IsOutdoors(0xA9B4001C));
            Assert.IsFalse(Navigation.IsOutdoors(0xA9B40105));
        }

        [TestMethod]
        public void BearingsAreClockwiseFromNorth() {
            Assert.AreEqual(0, Navigation.Bearing(0, 0, 0, 5), 0.001);
            Assert.AreEqual(90, Navigation.Bearing(0, 0, 5, 0), 0.001);
            Assert.AreEqual(180, Navigation.Bearing(0, 0, 0, -5), 0.001);
            Assert.AreEqual(270, Navigation.Bearing(0, 0, -5, 0), 0.001);
            Assert.AreEqual(45, Navigation.Bearing(1, 1, 2, 2), 0.001);
        }

        [TestMethod]
        public void RelativeTurnsAreTheShortWayRound() {
            Assert.AreEqual(0, Navigation.Relative(90, 90), 0.001);
            Assert.AreEqual(90, Navigation.Relative(90, 0), 0.001);
            Assert.AreEqual(-90, Navigation.Relative(0, 90), 0.001);
            Assert.AreEqual(20, Navigation.Relative(10, 350), 0.001);
            Assert.AreEqual(-20, Navigation.Relative(350, 10), 0.001);
            Assert.AreEqual(180, Navigation.Relative(180, 0), 0.001);
        }

        [TestMethod]
        public void DistancesAndCompassPoints() {
            Assert.AreEqual(5f, Navigation.Distance(0, 0, 3, 4), 0.001f);
            Assert.AreEqual("3.4 (816m)", Navigation.DistanceText(3.4f));
            Assert.AreEqual("120m", Navigation.DistanceText(0.5f));

            Assert.AreEqual("N", Navigation.Compass(10));
            Assert.AreEqual("NE", Navigation.Compass(40));
            Assert.AreEqual("W", Navigation.Compass(275));
            Assert.AreEqual("N", Navigation.Compass(350));
        }

        [TestMethod]
        public void FormatsAndReadsCoordinatesLikeTheGame() {
            Assert.AreEqual("42.1N, 33.6E", MapProjection.Format(33.6f, 42.1f));
            Assert.IsTrue(MapProjection.TryParse("12.5s 70w", out var x, out var y));
            Assert.AreEqual(-70f, x, 0.001f);
            Assert.AreEqual(-12.5f, y, 0.001f);
            Assert.IsFalse(MapProjection.TryParse("hebian", out _, out _));
        }

        [TestMethod]
        public void ReadsFellowsMapPlacesAsTheServerWritesThem() {
            var json = "{\"name\":\"Raiders\",\"leader\":1,\"open\":true,\"shareXp\":true,\"evenShare\":false,\"locked\":false,"
                + "\"members\":[{\"guid\":1,\"name\":\"Bob\",\"level\":126,\"health\":1,\"maxHealth\":2,\"stamina\":3,"
                + "\"maxStamina\":4,\"mana\":5,\"maxMana\":6,\"distance\":null,\"location\":\"Holtburg\",\"mapX\":33.5,\"mapY\":42.25,\"inBuilding\":true},"
                + "{\"guid\":2,\"name\":\"Al\",\"level\":50,\"health\":1,\"maxHealth\":2,\"stamina\":3,"
                + "\"maxStamina\":4,\"mana\":5,\"maxMana\":6,\"distance\":3,\"location\":\"Indoors\",\"mapX\":null,\"mapY\":null,\"inBuilding\":false}]}";

            var members = Json.Read<FellowshipInfo>(json).Members;

            Assert.IsTrue(members[0].IsOnMap);
            Assert.AreEqual(33.5f, members[0].MapX);
            Assert.IsTrue(members[0].InBuilding);
            Assert.IsFalse(members[1].IsOnMap);

            var position = Json.Read<PositionInfo>("{\"mapX\":33.5,\"mapY\":null,\"inBuilding\":true}");
            Assert.AreEqual(33.5f, position.MapX);
            Assert.IsNull(position.MapY);
        }
    }
}
