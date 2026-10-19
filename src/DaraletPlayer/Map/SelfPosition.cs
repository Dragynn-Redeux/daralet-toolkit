using System;
using Decal.Adapter;
using DaraletPlayer.Feed;

namespace DaraletPlayer.Map {
    /// <summary>
    /// Where you are on the map and which way you're facing, for the map and the destination arrow. Outdoors Decal's position
    /// is all it takes. Indoors the same sums still place a building's rooms, but a dungeon's landblock isn't a place on the
    /// map, and only the server can tell the two apart, so it's asked (@playerui position) when you go into a new cell.
    /// </summary>
    internal class SelfPosition {
        private static readonly TimeSpan IndoorRefresh = TimeSpan.FromSeconds(5);

        private readonly PlayerFeed feed;

        public SelfPosition(PlayerFeed feed) {
            this.feed = feed;
        }

        /// <summary>Degrees clockwise from north.</summary>
        public double Heading => CoreManager.Current.Actions.Heading;

        /// <summary>
        /// Your map coordinates, or false in a dungeon (or indoors before the server has said where). Called every frame by
        /// whatever shows them.
        /// </summary>
        public bool TryGet(out float x, out float y, out bool inBuilding) {
            x = 0;
            y = 0;
            inBuilding = false;

            var actions = CoreManager.Current.Actions;
            var landcell = unchecked((uint)actions.Landcell);

            if (landcell == 0) {
                return false;
            }

            if (!Navigation.IsOutdoors(landcell)) {
                // the cell is sent along so a new cell is asked about straight away; the server doesn't need it
                feed.Refresh(PlayerFeed.KindPosition, IndoorRefresh, $"0x{landcell:X8}");

                var position = feed.Position;

                if (position?.MapX == null || !position.InBuilding) {
                    return false;
                }

                inBuilding = true;
            }

            // worked out here rather than taken from the server's reply, so the marker moves as you walk
            Navigation.MapCoords(landcell, actions.LocationX, actions.LocationY, out x, out y);
            return true;
        }
    }
}
