using System.Collections.Generic;
using System.Numerics;
using DaraletPlayer.Feed;
using ImGuiNET;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// An item's tooltip: the lines ItemLines makes, coloured by kind. Shared by the bank, the Loot tab, the Corpse window and
    /// market search, so the layout is changed in ItemLines for all of them.
    /// </summary>
    internal static class ItemTooltip {
        private static readonly Vector4 Title = new Vector4(1f, 0.92f, 0.6f, 1f);
        private static readonly Vector4 Heading = new Vector4(0.6f, 0.75f, 1f, 1f);

        /// <summary>
        /// Shows lines as a tooltip for the item drawn just before this, while the mouse is over it.
        /// </summary>
        public static void Show(List<ItemLine> lines) {
            if (!ImGui.IsItemHovered()) {
                return;
            }

            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30);

            foreach (var line in lines) {
                switch (line.Style) {
                    case ItemLineStyle.Title:
                        ImGui.TextColored(Title, line.Text);
                        break;
                    case ItemLineStyle.Heading:
                        ImGui.Spacing();
                        ImGui.TextColored(Heading, line.Text);
                        break;
                    case ItemLineStyle.Detail:
                        ImGui.TextDisabled(line.Text);
                        break;
                    case ItemLineStyle.Good:
                        ImGui.TextColored(Colors.Good, line.Text);
                        break;
                    case ItemLineStyle.Bad:
                        ImGui.TextColored(Colors.Danger, line.Text);
                        break;
                    default:
                        ImGui.TextWrapped(line.Text);
                        break;
                }
            }

            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }
}
