using System.Collections.Generic;
using System.Linq;
using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class ArenaAndLeaderboardTests {
        [TestMethod]
        public void ArenaCommandsAreTheOnesArenaCommandReads() {
            Assert.AreEqual("/arena queue", ArenaCommands.Queue(0, false, false, false));
            Assert.AreEqual("/arena queue 10 scaled unrated", ArenaCommands.Queue(10, true, true, false));
            Assert.AreEqual("/arena queue fellowship", ArenaCommands.Queue(0, false, false, true));
            // the options go after the name, where the server looks for them
            Assert.AreEqual("/arena challenge Bob Smith scaled", ArenaCommands.Challenge(" Bob  Smith ", true, false, false));
            Assert.AreEqual("/arena watch 7", ArenaCommands.Watch(7));
            Assert.AreEqual("/arena stats", ArenaCommands.Stats(""));
            Assert.AreEqual("/arena stats Alice", ArenaCommands.Stats("Alice"));
            Assert.AreEqual("/arena leave", ArenaCommands.Leave);
        }

        [TestMethod]
        public void ReadsTheArenaAsTheServerWritesIt() {
            var json = "{\"enabled\":true,\"minimumLevel\":20,\"startingRating\":1500,\"state\":\"queued\","
                + "\"status\":\"You are number 1 of 2 in the arena queue.\",\"queuePlace\":1,\"queueCount\":2,"
                + "\"boards\":[{\"name\":\"1v1\",\"size\":1,\"scaled\":false,\"rating\":1520,\"wins\":3,\"losses\":1,\"draws\":0}],"
                + "\"duels\":[{\"id\":7,\"sides\":\"Alice vs Bob\",\"size\":1,\"scaled\":false,\"rated\":true,\"map\":\"Pit\",\"state\":\"Fighting\"}],"
                + "\"recent\":[{\"id\":6,\"sides\":\"Al vs Cy\",\"size\":1,\"scaled\":true,\"rated\":false,\"map\":\"Pit\",\"winners\":\"Al\","
                + "\"calledOff\":false,\"endedAgo\":90}],\"maps\":[\"The Pit\"]}";

            var arena = Json.Read<ArenaInfo>(json);

            Assert.IsTrue(arena.Enabled);
            Assert.IsTrue(arena.CanLeave);
            Assert.AreEqual(1, arena.QueuePlace);
            Assert.AreEqual(1520, arena.Boards[0].Rating);
            Assert.AreEqual("Alice vs Bob", arena.Duels[0].Sides);
            Assert.AreEqual("Al", arena.Recent[0].Winners);
            Assert.AreEqual(90, arena.Recent[0].EndedAgo);

            Assert.IsFalse(new ArenaInfo { State = "idle" }.CanLeave);
            Assert.IsFalse(new ArenaInfo { State = "off" }.CanLeave);
            Assert.IsTrue(new ArenaInfo { State = "watching" }.CanLeave);
        }

        [TestMethod]
        public void BoardsAskForWhatTheWebsiteAsksFor() {
            var options = new LeaderboardOptions { ArenaSize = 2, ArenaScaled = true, CapstoneDungeon = "MageAcademy", CapstoneSize = 3, CapstoneDifficulty = 5 };
            var paths = Leaderboards.All.ToDictionary(b => b.Id, b => b.Path(options));

            Assert.AreEqual("api/leaderboards/level?limit=50", paths["level"]);
            Assert.AreEqual("api/leaderboards/arena?size=2&scaled=true&limit=50", paths["arena"]);
            Assert.AreEqual("api/leaderboards/capstone?dungeon=MageAcademy&fellowshipSize=3&difficulty=5&limit=50", paths["capstone"]);
            Assert.AreEqual("api/leaderboards/trade-skills", paths["trade"]);
            CollectionAssert.AllItemsAreUnique(paths.Values.ToList());
        }

        [TestMethod]
        public void ReadsRowsOfDifferentBoards() {
            var level = Json.Read<List<LeaderboardRow>>("[{\"rank\":1,\"characterName\":\"Raider Finder\",\"level\":126,\"totalXp\":4286609098}]")[0];
            Assert.AreEqual("4,286,609,098", Leaderboards.All.Single(b => b.Id == "level").Columns[2].Cell(level));
            CollectionAssert.AreEqual(new[] { "Raider Finder" }, level.Names.ToList());

            var run = Json.Read<List<LeaderboardRow>>(
                "[{\"rank\":1,\"characterNames\":[\"Al\",\"Bo\"],\"completionSeconds\":754,\"completedAt\":\"2026-10-01T12:00:00Z\"}]")[0];
            var capstone = Leaderboards.All.Single(b => b.Id == "capstone");
            Assert.AreEqual("Al, Bo", capstone.Columns[0].Cell(run));
            Assert.AreEqual("12:34", capstone.Columns[1].Cell(run));
            Assert.AreEqual("2026-10-01", capstone.Columns[2].Cell(run));
            CollectionAssert.AreEqual(new[] { "Al", "Bo" }, run.Names.ToList());

            var creature = Json.Read<List<LeaderboardRow>>("[{\"rank\":1,\"creatureName\":\"Horseflies\",\"playerDeathsCaused\":24}]")[0];
            Assert.AreEqual(0, creature.Names.Count());
        }

        [TestMethod]
        public void RunTimesAndDifficulties() {
            Assert.AreEqual("12:34", Leaderboards.Duration(754));
            Assert.AreEqual("1:06:40", Leaderboards.Duration(4000));
            Assert.AreEqual("0:05", Leaderboards.Duration(4.6));
            Assert.AreEqual("", Leaderboards.Date("someday"));

            var difficulties = Leaderboards.CapstoneDifficulties();
            Assert.AreEqual(21, difficulties.Length);
            Assert.AreEqual((0, "None"), difficulties[0]);
            Assert.AreEqual((20, "XX"), difficulties[20]);
        }
    }
}
