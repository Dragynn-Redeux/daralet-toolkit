using System;
using System.Collections.Generic;
using System.Linq;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// Puts @playerui replies back together. The server splits each JSON reply into chat lines of
    ///
    ///     DAP1|kind|id|index|count|chunk
    ///
    /// (PlayerUiProtocol in daralet-ACE). Chunks of one reply share an id, so two replies arriving at once don't mix.
    /// </summary>
    public class ReplyAssembler {
        public const string Prefix = "DAP1|";

        // a reply missing a chunk is dropped once this many newer ones are waiting, so a lost line can't leak memory
        private const int MaxPartialReplies = 16;

        // JSON keys only @playerui's replies have: a chat line with two of them is a piece of a reply that showed up on its own
        private static readonly string[] ReplyKeys = {
            "\"guid\":", "\"maxHealth\":", "\"maxStamina\":", "\"mapX\":", "\"inBuilding\":", "\"listings\":", "\"members\":",
            "\"enchantments\":", "\"threat\":", "\"items\":", "\"itemCategory\":", "\"salvageable\":", "\"expiresIn\":", "\"packs\":",
        };

        private readonly Dictionary<string, string[]> partialReplies = new Dictionary<string, string[]>();
        private readonly Queue<string> partialOrder = new Queue<string>();

        public static bool IsFeedLine(string text) {
            return text != null && text.StartsWith(Prefix, StringComparison.Ordinal);
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

            if (count == 1) {
                kind = parts[1];
                json = parts[5];
                return true;
            }

            var key = parts[1] + "|" + parts[2];

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
            json = string.Concat(chunks);
            return true;
        }
    }
}
