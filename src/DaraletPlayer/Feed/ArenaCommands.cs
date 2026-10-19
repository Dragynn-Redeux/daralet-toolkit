using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// The /arena commands the Arena tab sends (ArenaCommand.cs in daralet-ACE).
    /// </summary>
    public static class ArenaCommands {
        /// <summary>
        /// /arena queue, optionally only against someone within that many levels, with the duel options.
        /// </summary>
        public static string Queue(int levels, bool scaled, bool unrated, bool fellowship) {
            return "/arena queue" + (levels > 0 ? " " + levels : "") + Options(scaled, unrated, fellowship);
        }

        /// <summary>
        /// /arena challenge &lt;name&gt;. The options go after the name, where the server looks for them.
        /// </summary>
        public static string Challenge(string name, bool scaled, bool unrated, bool fellowship) {
            return "/arena challenge " + BankCommands.Clean(name) + Options(scaled, unrated, fellowship);
        }

        public const string Leave = "/arena leave";

        /// <summary>/arena watch, by duel number.</summary>
        public static string Watch(int duel) {
            return "/arena watch " + duel;
        }

        /// <summary>/arena stats for someone else (or you, with no name).</summary>
        public static string Stats(string name) {
            var cleaned = BankCommands.Clean(name);
            return cleaned.Length == 0 ? "/arena stats" : "/arena stats " + cleaned;
        }

        private static string Options(bool scaled, bool unrated, bool fellowship) {
            return (scaled ? " scaled" : "") + (unrated ? " unrated" : "") + (fellowship ? " fellowship" : "");
        }
    }

    /// <summary>
    /// The arena as @playerui arena sends it. State is off, idle, queued, watching, defeated, accepting, starting or fighting.
    /// </summary>
    [DataContract]
    public class ArenaInfo {
        [DataMember(Name = "enabled")] public bool Enabled;
        [DataMember(Name = "minimumLevel")] public int MinimumLevel;
        [DataMember(Name = "startingRating")] public int StartingRating;
        [DataMember(Name = "state")] public string State;
        /// <summary>The same as State, in words.</summary>
        [DataMember(Name = "status")] public string Status;
        [DataMember(Name = "queuePlace")] public int QueuePlace;
        [DataMember(Name = "queueCount")] public int QueueCount;
        [DataMember(Name = "boards")] public List<ArenaBoardInfo> Boards;
        [DataMember(Name = "duels")] public List<ArenaDuelInfo> Duels;
        [DataMember(Name = "recent")] public List<ArenaResultInfo> Recent;
        [DataMember(Name = "maps")] public List<string> Maps;

        /// <summary>In the queue, a duel, or watching one: /arena leave does something.</summary>
        public bool CanLeave => State != null && State != "idle" && State != "off";
    }

    [DataContract]
    public class ArenaBoardInfo {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "size")] public int Size;
        [DataMember(Name = "scaled")] public bool Scaled;
        [DataMember(Name = "rating")] public int Rating;
        [DataMember(Name = "wins")] public int Wins;
        [DataMember(Name = "losses")] public int Losses;
        [DataMember(Name = "draws")] public int Draws;
    }

    [DataContract]
    public class ArenaDuelInfo {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "sides")] public string Sides;
        [DataMember(Name = "size")] public int Size;
        [DataMember(Name = "scaled")] public bool Scaled;
        [DataMember(Name = "rated")] public bool Rated;
        [DataMember(Name = "map")] public string Map;
        [DataMember(Name = "state")] public string State;
    }

    [DataContract]
    public class ArenaResultInfo {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "sides")] public string Sides;
        [DataMember(Name = "size")] public int Size;
        [DataMember(Name = "scaled")] public bool Scaled;
        [DataMember(Name = "rated")] public bool Rated;
        [DataMember(Name = "map")] public string Map;
        /// <summary>Null for a draw, or a duel called off.</summary>
        [DataMember(Name = "winners")] public string Winners;
        [DataMember(Name = "calledOff")] public bool CalledOff;
        [DataMember(Name = "endedAgo")] public double EndedAgo;
    }
}
