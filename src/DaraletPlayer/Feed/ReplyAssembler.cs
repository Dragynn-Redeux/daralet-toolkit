using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// Puts @playerui replies back together. The server splits each reply, JSON in base64, into chat lines of
    ///
    ///     DAP2|kind|id|index|count|chunk
    ///
    /// (PlayerUiProtocol in daralet-ACE). Chunks of one reply share an id, so two replies arriving at once don't mix.
    ///
    /// Base64, because other chat plugins act on readable text before this one can hide it: Virindi Chat System posts its own
    /// copy of any line with coordinates in it, such as a fellow's location, and that copy showed in chat. Older servers send
    /// DAP1 lines, the same with the JSON as it is, which are still read.
    /// </summary>
    public class ReplyAssembler {
        public const string Prefix = "DAP2|";
        public const string LegacyPrefix = "DAP1|";

        // a reply missing a chunk is dropped once this many newer ones are waiting, so a lost line can't leak memory
        private const int MaxPartialReplies = 16;

        // JSON keys only @playerui's replies have: a chat line with two of them is a piece of a DAP1 reply that showed up on
        // its own (Virindi Chat System's copy of a line with coordinates in it, cut short)
        private static readonly string[] ReplyKeys = {
            "\"guid\":", "\"maxHealth\":", "\"maxStamina\":", "\"mapX\":", "\"inBuilding\":", "\"listings\":", "\"members\":",
            "\"enchantments\":", "\"threat\":", "\"items\":", "\"itemCategory\":", "\"salvageable\":", "\"expiresIn\":", "\"packs\":",
        };

        private readonly Dictionary<string, string[]> partialReplies = new Dictionary<string, string[]>();
        private readonly Queue<string> partialOrder = new Queue<string>();

        public static bool IsFeedLine(string text) {
            return text != null
                && (text.StartsWith(Prefix, StringComparison.Ordinal) || text.StartsWith(LegacyPrefix, StringComparison.Ordinal));
        }

        /// <summary>
        /// Whether a chat window line is a reply and must be hidden. Anywhere in the line, not just at its start, in case
        /// something such as a timestamp comes before it.
        /// </summary>
        public static bool IsReplyChat(string text) {
            return text != null
                && (text.IndexOf(Prefix, StringComparison.Ordinal) >= 0 || text.IndexOf(LegacyPrefix, StringComparison.Ordinal) >= 0);
        }

        public static bool LooksLikeReplyPiece(string text) {
            return !string.IsNullOrEmpty(text) && ReplyKeys.Count(key => text.IndexOf(key, StringComparison.Ordinal) >= 0) >= 2;
        }

        /// <summary>
        /// Adds one chat line. Returns true, with the reply's kind and JSON, when it was the last chunk that reply was missing.
        /// </summary>
        public bool TryAdd(string line, out string kind, out string json) {
            kind = null;
            json = null;

            if (!IsFeedLine(line)) {
                return false;
            }

            // the client adds a newline to chat text; the JSON itself never has a raw one
            var parts = line.TrimEnd('\r', '\n').Split(new[] { '|' }, 6);

            if (parts.Length != 6
                || !int.TryParse(parts[3], out var index)
                || !int.TryParse(parts[4], out var count)
                || count < 1
                || index < 1
                || index > count) {
                return false;
            }

            var base64 = parts[0] + "|" == Prefix;

            if (count == 1) {
                kind = parts[1];
                json = Decode(parts[5], base64);
                return true;
            }

            var key = parts[0] + "|" + parts[1] + "|" + parts[2];

            if (!partialReplies.TryGetValue(key, out var chunks)) {
                chunks = new string[count];
                partialReplies[key] = chunks;
                partialOrder.Enqueue(key);

                while (partialOrder.Count > MaxPartialReplies) {
                    partialReplies.Remove(partialOrder.Dequeue());
                }
            }

            if (chunks.Length != count) {
                return false;
            }

            chunks[index - 1] = parts[5];

            if (chunks.Any(chunk => chunk == null)) {
                return false;
            }

            partialReplies.Remove(key);

            kind = parts[1];
            json = Decode(string.Concat(chunks), base64);
            return true;
        }

        private static string Decode(string text, bool base64) {
            return base64 ? Encoding.UTF8.GetString(Convert.FromBase64String(text)) : text;
        }
    }
}
