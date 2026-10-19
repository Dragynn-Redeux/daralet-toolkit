using System;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using DaraletPlayer.Feed;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// Which creatures are being watched: whatever you have selected, and the one you've chosen to track, which stays the same
    /// as you select other things. Both are asked about in one @playerui targets request, once a second.
    /// </summary>
    internal class TargetTracker {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

        private readonly PlayerFeed feed;

        public TargetTracker(PlayerFeed feed) {
            this.feed = feed;
        }

        /// <summary>The tracked creature's guid, or 0. Guids change between sessions, so this isn't saved.</summary>
        public uint Tracked { get; set; }

        /// <summary>The name the tracked creature had when it was tracked, for when the server no longer can say.</summary>
        public string TrackedName { get; set; }

        /// <summary>
        /// The selected object, if it's a monster or a player; 0 for anything else (an item, a door, nothing).
        /// </summary>
        public uint Selected {
            get {
                var guid = CoreManager.Current.Actions.CurrentSelection;
                return guid != 0 && IsCreature(guid) ? unchecked((uint)guid) : 0;
            }
        }

        public void Track(uint guid) {
            Tracked = guid;
            TrackedName = feed.Target(guid)?.Name ?? CoreManager.Current.WorldFilter[unchecked((int)guid)]?.Name;
        }

        public void StopTracking() {
            Tracked = 0;
            TrackedName = null;
        }

        /// <summary>
        /// Asks the server about the selected and tracked creatures. Called every frame by whatever shows them.
        /// </summary>
        public void Refresh() {
            var arguments = Display.TargetArguments(Selected, Tracked);

            if (arguments.Length > 0) {
                feed.Refresh(PlayerFeed.KindTargets, RefreshInterval, arguments);
            }
        }

        private static bool IsCreature(int guid) {
            var item = CoreManager.Current.WorldFilter[guid];
            return item != null && (item.ObjectClass == ObjectClass.Monster || item.ObjectClass == ObjectClass.Player);
        }
    }
}
