using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class ReplyAssemblerTests {
        [TestMethod]
        public void SingleLineReplyCompletesStraightAway() {
            var assembler = new ReplyAssembler();

            Assert.IsTrue(assembler.TryAdd("DAP1|hello|1|1|1|{\"protocol\":1}\n", out var kind, out var json));
            Assert.AreEqual("hello", kind);
            Assert.AreEqual("{\"protocol\":1}", json);
        }

        [TestMethod]
        public void ChunksArrivingOutOfOrderAreJoinedInOrder() {
            var assembler = new ReplyAssembler();

            Assert.IsFalse(assembler.TryAdd("DAP1|players|4|2|3|BB", out _, out _));
            Assert.IsFalse(assembler.TryAdd("DAP1|players|4|3|3|CC", out _, out _));
            Assert.IsTrue(assembler.TryAdd("DAP1|players|4|1|3|AA", out var kind, out var json));

            Assert.AreEqual("players", kind);
            Assert.AreEqual("AABBCC", json);
        }

        [TestMethod]
        public void TwoRepliesAtOnceDontMix() {
            var assembler = new ReplyAssembler();

            Assert.IsFalse(assembler.TryAdd("DAP1|players|5|1|2|a1", out _, out _));
            Assert.IsFalse(assembler.TryAdd("DAP1|fellowships|6|1|2|b1", out _, out _));
            Assert.IsTrue(assembler.TryAdd("DAP1|players|5|2|2|a2", out _, out var players));
            Assert.IsTrue(assembler.TryAdd("DAP1|fellowships|6|2|2|b2", out _, out var fellowships));

            Assert.AreEqual("a1a2", players);
            Assert.AreEqual("b1b2", fellowships);
        }

        [TestMethod]
        public void SeparatorInsideTheJsonIsKept() {
            var assembler = new ReplyAssembler();

            Assert.IsTrue(assembler.TryAdd("DAP1|players|1|1|1|[{\"name\":\"a|b\"}]", out _, out var json));
            Assert.AreEqual("[{\"name\":\"a|b\"}]", json);
        }

        [TestMethod]
        public void OtherChatAndBrokenLinesAreIgnored() {
            var assembler = new ReplyAssembler();

            Assert.IsFalse(assembler.TryAdd("You say, \"DAP1|players|1|1|1|[]\"", out _, out _));
            Assert.IsFalse(assembler.TryAdd("DAP1|players|1|1", out _, out _));
            Assert.IsFalse(assembler.TryAdd("DAP1|players|1|2|1|[]", out _, out _));
            Assert.IsFalse(assembler.TryAdd("DAP1|players|1|x|1|[]", out _, out _));
            Assert.IsFalse(assembler.TryAdd(null, out _, out _));
        }

        [TestMethod]
        public void PiecesOfARepliesAreRecognisedButChatIsnt() {
            // the start of a line the client showed from the middle of a fellowship reply
            Assert.IsTrue(ReplyAssembler.LooksLikeReplyPiece("lse,\"members\":[{\"guid\":1342177283,\"name\":\"+Spellsword\",\"level\":64"));
            Assert.IsTrue(ReplyAssembler.LooksLikeReplyPiece("(Indoors)\",\"mapX\":82.9659,\"mapY\":-39.45138,\"inBuilding\":true}]}"));
            Assert.IsFalse(ReplyAssembler.LooksLikeReplyPiece("Bob says, \"anyone want to group?\""));
            Assert.IsFalse(ReplyAssembler.LooksLikeReplyPiece("Bob says, \"my guid: is weird\""));
            Assert.IsFalse(ReplyAssembler.LooksLikeReplyPiece(null));
        }

        [TestMethod]
        public void DaraletAdminLinesAreNotOurs() {
            // staff can run both plugins, and each must leave the other's replies alone
            var assembler = new ReplyAssembler();

            Assert.IsFalse(ReplyAssembler.IsFeedLine("DAA1|hello|1|1|1|{}"));
            Assert.IsFalse(assembler.TryAdd("DAA1|hello|1|1|1|{}", out _, out _));
        }

        [TestMethod]
        public void ReadsHelloAsTheServerWritesIt() {
            // what System.Text.Json writes for PlayerUiHello with camelCase names
            var hello = Json.Read<HelloInfo>("{\"protocol\":1,\"name\":\"Bob Smith\",\"guid\":1342177281,\"kinds\":[\"hello\"]}");

            Assert.AreEqual(1, hello.Protocol);
            Assert.AreEqual("Bob Smith", hello.Name);
            Assert.AreEqual(0x50000001u, hello.Guid);
            CollectionAssert.AreEqual(new[] { "hello" }, hello.Kinds);
        }
    }
}
