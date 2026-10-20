using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaraletPlayer.Map;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class WorldDataTests {
        private static readonly byte[] BuiltInMap = Png(2048, 2048, 1);
        private static readonly byte[] NewMap = Png(2048, 2048, 2);
        private static readonly byte[] NewPlaces = Encoding.UTF8.GetBytes("<locations><loc id=\"5\" name=\"Al-Arqas\" type=\"Town\" NS=\"-31.1\" EW=\"13.7\"></loc></locations>");

        private string folder;

        /// <summary>What the fake release has, by name, and what was asked for.</summary>
        private Dictionary<string, byte[]> release;
        private List<string> downloaded;

        [TestInitialize]
        public void Setup() {
            folder = Path.Combine(Path.GetTempPath(), "DaraletPlayerTests-" + Path.GetRandomFileName());
            release = new Dictionary<string, byte[]>();
            downloaded = new List<string>();
        }

        [TestCleanup]
        public void RemoveFolder() {
            if (Directory.Exists(folder)) {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>A PNG header that big, then a byte to tell them apart. Only the header is ever read.</summary>
        private static byte[] Png(int width, int height, byte tag) {
            var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 0, 0, 0, 0, 0, tag };
            bytes[16] = (byte)(width >> 24); bytes[17] = (byte)(width >> 16); bytes[18] = (byte)(width >> 8); bytes[19] = (byte)width;
            bytes[20] = (byte)(height >> 24); bytes[21] = (byte)(height >> 16); bytes[22] = (byte)(height >> 8); bytes[23] = (byte)height;
            return bytes;
        }

        private void Publish(params (string Name, byte[] Bytes)[] files) {
            foreach (var (name, bytes) in files) {
                release[name] = bytes;
            }

            var manifest = new WorldDataManifest {
                Format = WorldData.Format,
                Published = "2026-10-20T14:05:00Z",
                Files = files.Select(f => new WorldDataFile { Name = f.Name, Sha256 = WorldData.Hash(f.Bytes), Size = f.Bytes.Length }).ToList(),
            };
            release[WorldData.ManifestName] = Encoding.UTF8.GetBytes(WorldData.WriteManifest(manifest));
        }

        private WorldData Make() {
            return new WorldData(folder, new[] { "locations.xml", "dereth-map.png" },
                name => name == "dereth-map.png" ? new MemoryStream(BuiltInMap) : null,
                (name, bytes) => name == "locations.xml" ? Locations.Check(bytes) : WorldData.CheckSquarePng(bytes),
                name => {
                    downloaded.Add(name);
                    return release.TryGetValue(name, out var bytes) ? Task.FromResult(bytes) : Task.FromException<byte[]>(new FileNotFoundException(name));
                });
        }

        [TestMethod]
        public void DownloadsWhatChangedAndSkipsWhatsBuiltIn() {
            Publish(("locations.xml", NewPlaces), ("dereth-map.png", BuiltInMap));
            var data = Make();

            var changed = data.Check().Result;

            CollectionAssert.AreEqual(new[] { "locations.xml" }, changed);
            CollectionAssert.AreEqual(new[] { "manifest.json", "locations.xml" }, downloaded, "the map is the same as the built-in one, so isn't downloaded");
            CollectionAssert.AreEqual(NewPlaces, File.ReadAllBytes(data.PathFor("locations.xml")));
            Assert.IsNull(data.PathFor("dereth-map.png"));
            Assert.AreEqual(1, data.Version);
            StringAssert.Contains(data.Status, "2026-10-20 14:05 UTC");

            // nothing new the second time
            downloaded.Clear();
            Assert.AreEqual(0, data.Check().Result.Count);
            CollectionAssert.AreEqual(new[] { "manifest.json" }, downloaded);
            Assert.AreEqual(1, data.Version);
            StringAssert.StartsWith(data.Status, "Up to date");
        }

        [TestMethod]
        public void GoesBackToTheBuiltInFileWhenTheReleaseDoes() {
            Publish(("dereth-map.png", NewMap));
            var data = Make();

            CollectionAssert.AreEqual(new[] { "dereth-map.png" }, data.Check().Result);
            Assert.IsNotNull(data.PathFor("dereth-map.png"));

            Publish(("dereth-map.png", BuiltInMap));

            CollectionAssert.AreEqual(new[] { "dereth-map.png" }, data.Check().Result);
            Assert.IsNull(data.PathFor("dereth-map.png"));
            Assert.AreEqual(2, data.Version);
        }

        [TestMethod]
        public void KeepsWhatItHasWhenADownloadIsWrong() {
            Publish(("locations.xml", NewPlaces), ("dereth-map.png", Png(2048, 1024, 3)));
            // and a locations.xml that doesn't match the manifest, as if it were being replaced right now
            release["locations.xml"] = Encoding.UTF8.GetBytes("<locations></locations>");
            var data = Make();

            Assert.AreEqual(0, data.Check().Result.Count);
            Assert.IsNull(data.PathFor("locations.xml"));
            Assert.IsNull(data.PathFor("dereth-map.png"));
            Assert.IsNull(data.PathFor("manifest.json"), "the manifest is only kept once everything in it is");
            StringAssert.Contains(data.Status, "didn't match the manifest");
            StringAssert.Contains(data.Status, "not square");
        }

        [TestMethod]
        public void LeavesFilesAloneForANewerFormat() {
            Publish(("locations.xml", NewPlaces));
            var manifest = WorldData.ReadManifest(release["manifest.json"]);
            manifest.Format = WorldData.Format + 1;
            release["manifest.json"] = Encoding.UTF8.GetBytes(WorldData.WriteManifest(manifest));

            Assert.AreEqual(0, Make().Check().Result.Count);
            CollectionAssert.AreEqual(new[] { "manifest.json" }, downloaded);
        }

        [TestMethod]
        public void ReadsTheManifestTheWorkflowWrites() {
            // the shape .github/workflows/world-data.yml writes
            var json = "{\"format\":1,\"published\":\"2026-10-20T14:05:00Z\",\"commit\":\"abc123\",\"files\":[{\"name\":\"locations.xml\",\"sha256\":\"ABCDEF\",\"size\":155030}]}";
            var manifest = WorldData.ReadManifest(Encoding.UTF8.GetBytes(json));

            Assert.AreEqual(1, manifest.Format);
            Assert.AreEqual("abc123", manifest.Commit);
            Assert.AreEqual("locations.xml", manifest.Files[0].Name);
            Assert.AreEqual(155030, manifest.Files[0].Size);
        }

        [TestMethod]
        public void SaysWhenNothingsBeenPublished() {
            var data = Make();

            Assert.AreEqual(0, data.Check().Result.Count);
            Assert.AreEqual("No map updates have been published yet.", data.Status);
        }

        [TestMethod]
        public void ChecksPngsAndLocations() {
            Assert.IsNull(WorldData.CheckSquarePng(NewMap));
            StringAssert.Contains(WorldData.CheckSquarePng(Png(100, 50, 0)), "100x50");
            Assert.AreEqual("it isn't a PNG", WorldData.CheckSquarePng(Encoding.UTF8.GetBytes("<html>Not Found</html>")));

            Assert.IsNull(Locations.Check(NewPlaces));
            Assert.AreEqual("it has no places in it", Locations.Check(Encoding.UTF8.GetBytes("<locations></locations>")));
            StringAssert.StartsWith(Locations.Check(Encoding.UTF8.GetBytes("<html>")), "it isn't readable XML");
        }
    }
}
