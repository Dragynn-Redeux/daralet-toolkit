using System;
using System.Collections.Generic;
using System.Globalization;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// The pictures a game icon is drawn from, bottom to top, as portal.dat ids (0 for none): the tile behind it for its kind
    /// of item, the item, and a mark on top (a gem's material, say).
    /// </summary>
    public class IconLayers {
        /// <summary>
        /// The tiles the client draws behind an item, by the kind the Market API names (MarketListing.IconUnderlay), copied
        /// from the website's MKT_ICON_UNDERLAYS (market.js).
        /// </summary>
        public static readonly Dictionary<string, uint> Underlays = new Dictionary<string, uint> {
            ["Weapon"] = 0x060011D2,
            ["Armor"] = 0x060011CF,
            ["Clothing"] = 0x060011F3,
            ["Jewelry"] = 0x060011D5,
            ["SpellComponent"] = 0x060011CD,
            ["Misc"] = 0x060011D4,
            ["SigilBlue"] = 0x06008700,
            ["SigilYellow"] = 0x06008701,
            ["SigilRed"] = 0x06008702,
        };

        /// <summary>
        /// The colour an icon's white outline is drawn in, by its effect as the Market API names it (MarketListing.IconEffect),
        /// copied from the website's MKT_ICON_OUTLINE_COLORS (market.js): black for an item with none.
        /// </summary>
        public static readonly Dictionary<string, (byte R, byte G, byte B)> OutlineColors = new Dictionary<string, (byte, byte, byte)>(StringComparer.OrdinalIgnoreCase) {
            ["Fire"] = (255, 106, 61),
            ["Cold"] = (200, 200, 255),
            ["Acid"] = (90, 212, 90),
            ["Electric"] = (191, 0, 255),
            ["Magical"] = (102, 194, 255),
            ["Undef"] = (0, 0, 0),
            ["SigilBlue"] = (30, 110, 255),
            ["SigilYellow"] = (255, 209, 0),
            ["SigilRed"] = (220, 20, 20),
        };

        /// <summary>How light a pixel has to be, in all of red, green and blue, to count as outline (the website's).</summary>
        public const byte OutlineThreshold = 225;

        public uint Underlay;
        public uint Icon;
        public uint Overlay;

        /// <summary>Fire, Cold, Acid, Electric, Magical, a sigil's colour, or Undef: what colour the outline is.</summary>
        public string Effect;

        public IconLayers(uint underlay, uint icon, uint overlay, string effect = "Undef") {
            Underlay = underlay;
            Icon = icon;
            Overlay = overlay;
            Effect = effect ?? "Undef";
        }

        /// <summary>An item with no icon of its own draws nothing, not a bare tile.</summary>
        public bool IsEmpty => Icon == 0;

        public (byte R, byte G, byte B) OutlineColor => OutlineColors.TryGetValue(Effect, out var color) ? color : OutlineColors["Undef"];

        /// <summary>A market listing's icon, as market search shows it.</summary>
        public static IconLayers Of(MarketListing listing) {
            return listing == null ? null : FromHex(listing.IconHex, listing.IconOverlayHex, listing.IconUnderlay, listing.IconEffect);
        }

        /// <summary>One of your listings, sales or expired items, from the icon @playerui market sends with it.</summary>
        public static IconLayers Of(ListingIconInfo icon) {
            return icon == null ? null : FromHex(icon.Icon, icon.Overlay, icon.Underlay, icon.Effect);
        }

        /// <summary>Hex ids ("06001234") and an underlay kind ("Weapon"), as the Market API writes them.</summary>
        public static IconLayers FromHex(string icon, string overlay, string underlayKind, string effect = null) {
            var underlay = Underlays.TryGetValue(underlayKind ?? "Misc", out var tile) ? tile : Underlays["Misc"];
            return new IconLayers(underlay, Hex(icon), Hex(overlay), effect);
        }

        /// <summary>
        /// The effect from the game's UiEffects flags (what Decal calls IconOutline), picked in the Market API's order
        /// (ComputeIconEffect): an element first, then magic.
        /// </summary>
        public static string EffectOf(uint uiEffects) {
            if ((uiEffects & 0x0020) != 0) {
                return "Fire";
            }
            if ((uiEffects & 0x0080) != 0) {
                return "Cold";
            }
            if ((uiEffects & 0x0100) != 0) {
                return "Acid";
            }
            if ((uiEffects & 0x0040) != 0) {
                return "Electric";
            }

            return (uiEffects & 0x0001) != 0 ? "Magical" : "Undef";
        }

        private static uint Hex(string text) {
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }
    }
}
