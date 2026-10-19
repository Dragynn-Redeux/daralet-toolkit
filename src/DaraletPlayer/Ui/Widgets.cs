using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// Pieces several tabs draw the same way: tooltips, tables, combo boxes, vital bars, durations.
    /// </summary>
    internal static class Widgets {
        public const float BarWidth = 110;

        /// <summary>How every list in the plugin is drawn: striped rows, column lines, columns as wide as what's in them.</summary>
        public const ImGuiTableFlags TableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit;

        /// <summary>
        /// Shows text in a hover tooltip for the item drawn just before this, greyed-out ones included.
        /// </summary>
        public static void Tip(string text) {
            if (string.IsNullOrEmpty(text) || !ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                return;
            }

            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        /// <summary>
        /// A small (?) on the same line, explaining what's next to it when hovered.
        /// </summary>
        public static void HelpMarker(string text) {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            Tip(text);
        }

        /// <summary>
        /// A table's header row with a tooltip on each header. Call after TableSetupColumn, in place of TableHeadersRow;
        /// a null tip leaves that header without one.
        /// </summary>
        public static void HeadersRow(params string[] tips) {
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

            for (var column = 0; column < ImGui.TableGetColumnCount(); column++) {
                if (!ImGui.TableSetColumnIndex(column)) {
                    continue;
                }

                ImGui.TableHeader(ImGui.TableGetColumnName(column));

                if (column < tips.Length && tips[column] != null) {
                    Tip(tips[column]);
                }
            }
        }

        /// <summary>
        /// A tab in a tab bar with a tooltip. Returns true while it's the open tab; call EndTabItem then.
        /// </summary>
        public static bool TabItem(string label, string tip) {
            var open = ImGui.BeginTabItem(label);
            Tip(tip);
            return open;
        }

        /// <summary>
        /// A combo box over (value, label) choices, showing the chosen one's label (the first one's, when the value isn't one
        /// of them). Returns true when the choice changed.
        /// </summary>
        public static bool Choice<T>(string id, (T Value, string Label)[] options, ref T value) {
            var current = value;
            var label = options.FirstOrDefault(o => Equals(o.Value, current)).Label ?? options[0].Label;
            var changed = false;

            if (ImGui.BeginCombo(id, label)) {
                foreach (var option in options) {
                    var chosen = Equals(option.Value, current);

                    if (ImGui.Selectable(option.Label, chosen)) {
                        changed = !chosen;
                        value = option.Value;
                    }
                }

                ImGui.EndCombo();
            }

            return changed;
        }

        /// <summary>
        /// A vital as a filled bar with "current / max" on it. A height of 0 is the normal line height.
        /// </summary>
        public static void VitalBar(uint current, uint max, Vector4 color, float width = BarWidth, float height = 0) {
            var fraction = max == 0 ? 0f : Math.Min(1f, (float)current / max);

            Bar(fraction, color, $"{current} / {max}", width, height);
        }

        public static void Bar(float fraction, Vector4 color, string text, float width = BarWidth, float height = 0) {
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
            ImGui.ProgressBar(fraction, new Vector2(width, height), text);
            ImGui.PopStyleColor();
        }

        /// <summary>
        /// 45s, 12m 05s, 3h 07m, 2d 04h
        /// </summary>
        public static string Duration(double seconds) {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));

            if (time.TotalMinutes < 1) {
                return $"{time.Seconds}s";
            }
            if (time.TotalHours < 1) {
                return $"{time.Minutes}m {time.Seconds:00}s";
            }
            if (time.TotalDays < 1) {
                return $"{(int)time.TotalHours}h {time.Minutes:00}m";
            }
            return $"{(int)time.TotalDays}d {time.Hours:00}h";
        }
    }

    internal static class Colors {
        public static readonly Vector4 Warning = new Vector4(1f, 0.75f, 0.3f, 1f);
        public static readonly Vector4 Info = new Vector4(0.55f, 0.8f, 1f, 1f);
        public static readonly Vector4 Danger = new Vector4(1f, 0.4f, 0.4f, 1f);
        public static readonly Vector4 Muted = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        public static readonly Vector4 Good = new Vector4(0.4f, 0.9f, 0.45f, 1f);
        public static readonly Vector4 Text = new Vector4(1f, 1f, 1f, 1f);

        /// <summary>Spells someone else cast: dimmer than yours (Good / Danger), so yours stand out.</summary>
        public static readonly Vector4 BuffOther = new Vector4(0.45f, 0.7f, 0.5f, 1f);
        public static readonly Vector4 DebuffOther = new Vector4(0.85f, 0.55f, 0.5f, 1f);

        // Threat table bars: the name is written in white on top of them, so they're all dark enough to read it on.
        public static readonly Vector4 ThreatYou = new Vector4(0.7f, 0.18f, 0.18f, 1f);
        public static readonly Vector4 ThreatPlayer = new Vector4(0.25f, 0.4f, 0.6f, 1f);
        /// <summary>A creature in the table, such as a pet.</summary>
        public static readonly Vector4 ThreatOther = new Vector4(0.35f, 0.35f, 0.35f, 1f);

        public static readonly Vector4 Health = new Vector4(0.75f, 0.15f, 0.15f, 1f);
        public static readonly Vector4 Stamina = new Vector4(0.8f, 0.65f, 0.1f, 1f);
        public static readonly Vector4 Mana = new Vector4(0.2f, 0.4f, 0.85f, 1f);
    }
}
