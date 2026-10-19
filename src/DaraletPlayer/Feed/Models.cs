using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DaraletPlayer.Feed {
    // These mirror the records in daralet-ACE apps/server/Entity/PlayerUi/PlayerUiSnapshots.cs, which the server sends as camelCase JSON.

    [DataContract]
    public class HelloInfo {
        [DataMember(Name = "protocol")] public int Protocol;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "guid")] public uint Guid;
        /// <summary>
        /// Every request the server's @playerui answers. A tab whose request isn't in here was added after the server was built,
        /// so it says so instead of waiting for a reply that won't come.
        /// </summary>
        [DataMember(Name = "kinds")] public string[] Kinds;
    }

    [DataContract]
    public class FellowshipInfo {
        [DataMember(Name = "name")] public string Name;
        /// <summary>The leader's guid.</summary>
        [DataMember(Name = "leader")] public uint Leader;
        [DataMember(Name = "open")] public bool Open;
        [DataMember(Name = "shareXp")] public bool ShareXp;
        [DataMember(Name = "evenShare")] public bool EvenShare;
        [DataMember(Name = "locked")] public bool Locked;
        [DataMember(Name = "members")] public List<FellowInfo> Members;
    }

    [DataContract]
    public class FellowInfo {
        [DataMember(Name = "guid")] public uint Guid;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "level")] public int Level;
        [DataMember(Name = "health")] public uint Health;
        [DataMember(Name = "maxHealth")] public uint MaxHealth;
        [DataMember(Name = "stamina")] public uint Stamina;
        [DataMember(Name = "maxStamina")] public uint MaxStamina;
        [DataMember(Name = "mana")] public uint Mana;
        [DataMember(Name = "maxMana")] public uint MaxMana;
        /// <summary>Meters from you, or null when they're too far away for the server to say.</summary>
        [DataMember(Name = "distance")] public float? Distance;
        [DataMember(Name = "location")] public string Location;
        /// <summary>East (+) / west (-) in map units, or null in a dungeon.</summary>
        [DataMember(Name = "mapX")] public float? MapX;
        /// <summary>North (+) / south (-) in map units, or null in a dungeon.</summary>
        [DataMember(Name = "mapY")] public float? MapY;
        /// <summary>Inside a building: still on the map, where the building is.</summary>
        [DataMember(Name = "inBuilding")] public bool InBuilding;

        public bool IsOnMap => MapX.HasValue && MapY.HasValue;
    }

    /// <summary>
    /// Where you are on the map, which the plugin asks for only indoors: outdoors it works it out itself.
    /// </summary>
    [DataContract]
    public class PositionInfo {
        /// <summary>East (+) / west (-) in map units, or null in a dungeon.</summary>
        [DataMember(Name = "mapX")] public float? MapX;
        [DataMember(Name = "mapY")] public float? MapY;
        [DataMember(Name = "inBuilding")] public bool InBuilding;
    }

    /// <summary>
    /// Your bank, as far as the server's concerned: whether it's open (and near enough to use), its space and money, and its
    /// packs numbered as /bank packs numbers them. The items are what the client was sent when the bank opened.
    /// </summary>
    [DataContract]
    public class BankInfo {
        [DataMember(Name = "open")] public bool Open;
        /// <summary>Its items have come from the database: a moment after it opens.</summary>
        [DataMember(Name = "loaded")] public bool Loaded;
        /// <summary>The bank chest's guid: the container its items are in.</summary>
        [DataMember(Name = "guid")] public uint Guid;
        [DataMember(Name = "used")] public int Used;
        [DataMember(Name = "capacity")] public int Capacity;
        [DataMember(Name = "coins")] public long Coins;
        /// <summary>What the trade notes are worth, in pyreals.</summary>
        [DataMember(Name = "notes")] public long Notes;
        [DataMember(Name = "noteCount")] public int NoteCount;
        [DataMember(Name = "autoSort")] public bool AutoSort;
        [DataMember(Name = "packs")] public List<BankPackInfo> Packs;
    }

    [DataContract]
    public class BankPackInfo {
        /// <summary>The pack's number in /bank packs, which /bank inscribe and /bank icon take.</summary>
        [DataMember(Name = "number")] public int Number;
        [DataMember(Name = "guid")] public uint Guid;
        [DataMember(Name = "name")] public string Name;
        /// <summary>What it collects, in words.</summary>
        [DataMember(Name = "tags")] public string Tags;
        [DataMember(Name = "inscription")] public string Inscription;
        [DataMember(Name = "used")] public int Used;
        [DataMember(Name = "capacity")] public int Capacity;
    }

    /// <summary>
    /// Your side of the market (@playerui market): your account's listings, recent sales and expired listings, and whether a
    /// Market Broker is near enough for the broker actions. Fees are fractions (0.05 is 5%).
    /// </summary>
    [DataContract]
    public class MarketInfo {
        [DataMember(Name = "nearBroker")] public bool NearBroker;
        [DataMember(Name = "brokerName")] public string BrokerName;
        [DataMember(Name = "brokerDistance")] public float? BrokerDistance;
        [DataMember(Name = "payoutFee")] public double PayoutFee;
        [DataMember(Name = "cancelFee")] public double CancelFee;
        /// <summary>What the sales waiting to be claimed come to, after fees.</summary>
        [DataMember(Name = "pendingPayout")] public long PendingPayout;
        [DataMember(Name = "listings")] public List<MyListingInfo> Listings;
        [DataMember(Name = "sales")] public List<SaleInfo> Sales;
        [DataMember(Name = "expired")] public List<ExpiredInfo> Expired;
    }

    [DataContract]
    public class MyListingInfo {
        /// <summary>The listing's id, which cancelling takes.</summary>
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "weenieClassId")] public uint WeenieClassId;
        [DataMember(Name = "tier")] public int? Tier;
        [DataMember(Name = "price")] public int Price;
        /// <summary>Seconds left, as of the reply.</summary>
        [DataMember(Name = "expiresIn")] public double ExpiresIn;
    }

    [DataContract]
    public class SaleInfo {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "weenieClassId")] public uint WeenieClassId;
        [DataMember(Name = "price")] public int Price;
        /// <summary>What you get, after the fee.</summary>
        [DataMember(Name = "payout")] public int Payout;
        /// <summary>The payout has been collected from a broker.</summary>
        [DataMember(Name = "claimed")] public bool Claimed;
        [DataMember(Name = "soldAgo")] public double SoldAgo;
    }

    [DataContract]
    public class ExpiredInfo {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "weenieClassId")] public uint WeenieClassId;
        [DataMember(Name = "price")] public int Price;
        [DataMember(Name = "expiredAgo")] public double ExpiredAgo;
    }

    [DataContract]
    public class TargetInfo {
        /// <summary>A creature near you, with everything filled in.</summary>
        public const string StatusOk = "ok";
        /// <summary>Another player: only the name is sent, and the client's own health bar is shown.</summary>
        public const string StatusPlayer = "player";
        /// <summary>Dead, despawned, too far away or not a creature. Only Guid is set.</summary>
        public const string StatusGone = "gone";

        [DataMember(Name = "guid")] public uint Guid;
        [DataMember(Name = "status")] public string Status;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "level")] public int Level;
        [DataMember(Name = "health")] public uint Health;
        [DataMember(Name = "maxHealth")] public uint MaxHealth;
        [DataMember(Name = "stamina")] public uint Stamina;
        [DataMember(Name = "maxStamina")] public uint MaxStamina;
        [DataMember(Name = "mana")] public uint Mana;
        [DataMember(Name = "maxMana")] public uint MaxMana;
        [DataMember(Name = "distance")] public float? Distance;
        /// <summary>The guid of whoever it's attacking, or null.</summary>
        [DataMember(Name = "attackTarget")] public uint? AttackTarget;
        [DataMember(Name = "attackTargetName")] public string AttackTargetName;
        [DataMember(Name = "enchantments")] public List<EnchantmentInfo> Enchantments;
        /// <summary>Highest first, or null when the creature doesn't keep a threat table.</summary>
        [DataMember(Name = "threat")] public List<ThreatInfo> Threat;

        public bool IsOk => Status == StatusOk;
        public bool IsPlayer => Status == StatusPlayer;
    }

    [DataContract]
    public class EnchantmentInfo {
        [DataMember(Name = "spellId")] public int SpellId;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "level")] public uint Level;
        [DataMember(Name = "school")] public string School;
        [DataMember(Name = "beneficial")] public bool Beneficial;
        /// <summary>As of the reply; -1 for a spell that never runs out.</summary>
        [DataMember(Name = "secondsLeft")] public double SecondsLeft;
        [DataMember(Name = "casterGuid")] public uint CasterGuid;
        [DataMember(Name = "casterName")] public string CasterName;
        /// <summary>You cast it.</summary>
        [DataMember(Name = "yours")] public bool Yours;
        [DataMember(Name = "iconId")] public uint IconId;
    }

    [DataContract]
    public class ThreatInfo {
        [DataMember(Name = "guid")] public uint Guid;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "threat")] public int Threat;
        [DataMember(Name = "isYou")] public bool IsYou;
        [DataMember(Name = "isPlayer")] public bool IsPlayer;
    }
}
