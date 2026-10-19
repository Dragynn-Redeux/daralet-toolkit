using System.Collections.Generic;
using DaraletPlayer.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaraletPlayer.Tests {
    [TestClass]
    public class ReplyShapeTests {
        [TestMethod]
        public void ADescribeReplyKeepsEveryNewField() {
            // the shape @playerui describe sends (PlayerUiCorpseItem, nulls left out)
            var json = "[{\"guid\":5,\"name\":\"Axe\",\"stackSize\":1,\"value\":100,\"burden\":50,\"salvageable\":true,"
                + "\"item\":{\"itemName\":\"Axe\",\"baseDamage\":100,\"damageVariance\":0.5,\"damageType\":\"Slashing\",\"weaponTime\":40,"
                + "\"attacksPerSecond\":0.8,\"properties\":[\"Biting Strike\"],\"propertyValues\":{\"Biting Strike\":3}},"
                + "\"rollQuality\":{\"Bonus to Attack Skill\":0.75,\"Biting Strike\":0.4}}]";

            var item = Json.Read<List<CorpseItemInfo>>(json)[0];

            Assert.AreEqual("Slashing", item.Item.DamageType);
            Assert.AreEqual(40, item.Item.WeaponTime);
            Assert.AreEqual(3.0, item.Item.PropertyValues["Biting Strike"]);
            Assert.AreEqual(0.75, item.RollQuality["Bonus to Attack Skill"]);
        }
    }
}
