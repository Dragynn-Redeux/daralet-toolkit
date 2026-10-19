using System;
using System.Numerics;
using Decal.Adapter;
using DaraletPlayer.Feed;
using ImGuiNET;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// The Fellowship tab, and the fellowship vitals window it controls. Both show what @playerui fellowship sends, asked for
    /// once a second while either is on screen.
    /// </summary>
    internal class FellowshipTab : IDisposable {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
        private const float WindowBarWidth = 130;
        private const float WindowBarHeight = 14;

        private readonly PlayerFeed feed;
        private readonly Settings settings;
        private readonly FloatingWindow window;

        public FellowshipTab(PlayerFeed feed, Settings settings) {
            this.feed = feed;
            this.settings = settings;

            window = new FloatingWindow("fellowship", "Fellowship", settings, new Vector2(20, 200), RenderWindow);
        }

        public void Render() {
            Refresh();

            if (!feed.Supports(PlayerFeed.KindFellowship)) {
                ImGui.TextColored(Colors.Warning, "This server is too old to send fellowship vitals.");
                return;
            }

            window.RenderControls("Show the fellowship window", "A small window with each member's health, stamina and mana that stays on screen while you play. Click a name in it to select that member.");
            RenderWindowOptions();

            ImGui.Separator();

            var fellowship = feed.Fellowship;

            if (fellowship == null) {
                ImGui.TextDisabled(feed.FellowshipUpdatedAt == DateTime.MinValue ? "Loading..." : "You aren't in a fellowship.");
                return;
            }

            ImGui.Text(fellowship.Name ?? "");
            ImGui.SameLine();
            ImGui.TextDisabled(Describe(fellowship));
            Widgets.Tip("Sharing XP needs every member within 5 levels of the leader, or all of them level 50 or higher. Even share splits XP equally; otherwise higher levels get more. Open fellowships let any member recruit. Locked ones can't take new members.");

            RenderTable(fellowship);
        }

        private void RenderWindowOptions() {
            var data = settings.Data;

            if (ImGui.Checkbox("Stamina##fellowship", ref data.FellowshipShowStamina)) {
                settings.Changed();
            }
            Widgets.Tip("Shows each member's stamina in the fellowship window.");

            ImGui.SameLine();

            if (ImGui.Checkbox("Mana##fellowship", ref data.FellowshipShowMana)) {
                settings.Changed();
            }
            Widgets.Tip("Shows each member's mana in the fellowship window. Health is always shown.");
        }

        private static string Describe(FellowshipInfo fellowship) {
            var parts = new System.Collections.Generic.List<string>();

            parts.Add(fellowship.ShareXp ? (fellowship.EvenShare ? "sharing XP evenly" : "sharing XP") : "not sharing XP");

            if (fellowship.Open) {
                parts.Add("open");
            }

            if (fellowship.Locked) {
                parts.Add("locked");
            }

            return "(" + string.Join(", ", parts) + ")";
        }

        private void RenderTable(FellowshipInfo fellowship) {
            if (!ImGui.BeginTable("fellowship-members", 7, Widgets.TableFlags)) {
                return;
            }

            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Lvl", ImGuiTableColumnFlags.WidthFixed, 35);
            ImGui.TableSetupColumn("Health", ImGuiTableColumnFlags.WidthFixed, Widgets.BarWidth);
            ImGui.TableSetupColumn("Stamina", ImGuiTableColumnFlags.WidthFixed, Widgets.BarWidth);
            ImGui.TableSetupColumn("Mana", ImGuiTableColumnFlags.WidthFixed, Widgets.BarWidth);
            ImGui.TableSetupColumn("Dist", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthStretch);
            const string vitalTip = "Current / maximum, updated every second.";
            Widgets.HeadersRow(
                "Click a name to select that member. A * marks the leader.",
                "Character level.",
                vitalTip,
                vitalTip,
                vitalTip,
                "How far away they are, when they're near enough for the server to say.",
                "Where they are: a named place or map coordinates, or Indoors.");

            foreach (var member in fellowship.Members ?? new System.Collections.Generic.List<FellowInfo>()) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                MemberName(member, fellowship);

                ImGui.TableNextColumn();
                ImGui.Text(member.Level.ToString());
                ImGui.TableNextColumn();
                Widgets.VitalBar(member.Health, member.MaxHealth, Colors.Health);
                ImGui.TableNextColumn();
                Widgets.VitalBar(member.Stamina, member.MaxStamina, Colors.Stamina);
                ImGui.TableNextColumn();
                Widgets.VitalBar(member.Mana, member.MaxMana, Colors.Mana);
                ImGui.TableNextColumn();
                ImGui.TextDisabled(Display.Distance(member.Distance));
                ImGui.TableNextColumn();
                ImGui.TextDisabled(member.Location ?? "");
            }

            ImGui.EndTable();
        }

        private void RenderWindow() {
            Refresh();

            var fellowship = feed.Fellowship;

            if (fellowship?.Members == null || fellowship.Members.Count == 0) {
                ImGui.TextDisabled("No fellowship");
                return;
            }

            var data = settings.Data;
            var scale = window.Scale;
            var width = WindowBarWidth * scale;
            var height = WindowBarHeight * scale;
            var selected = unchecked((uint)CoreManager.Current.Actions.CurrentSelection);

            foreach (var member in fellowship.Members) {
                ImGui.PushID((int)member.Guid);

                // the name and bars are one group, so clicking anywhere on a member selects them, the way the game's own
                // fellowship panel does
                ImGui.BeginGroup();

                var far = !member.Distance.HasValue;
                ImGui.TextColored(far ? Colors.Muted : member.Guid == selected ? Colors.Info : Colors.Text, MemberLabel(member, fellowship));

                Widgets.VitalBar(member.Health, member.MaxHealth, Colors.Health, width, height);

                if (data.FellowshipShowStamina) {
                    Widgets.VitalBar(member.Stamina, member.MaxStamina, Colors.Stamina, width, height);
                }

                if (data.FellowshipShowMana) {
                    Widgets.VitalBar(member.Mana, member.MaxMana, Colors.Mana, width, height);
                }

                ImGui.EndGroup();

                if (ImGui.IsItemHovered()) {
                    ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                    Select(member);
                }

                MemberTip(member);

                ImGui.PopID();
            }
        }

        /// <summary>
        /// A member's name in the table, greyed out when they're too far away to say how far, which selects them when clicked.
        /// </summary>
        private void MemberName(FellowInfo member, FellowshipInfo fellowship) {
            var far = !member.Distance.HasValue;

            if (far) {
                ImGui.PushStyleColor(ImGuiCol.Text, Colors.Muted);
            }

            if (ImGui.Selectable($"{MemberLabel(member, fellowship)}##member-{member.Guid}")) {
                Select(member);
            }

            if (far) {
                ImGui.PopStyleColor();
            }

            MemberTip(member);
        }

        private static string MemberLabel(FellowInfo member, FellowshipInfo fellowship) {
            return (member.Guid == fellowship.Leader ? "*" : "") + member.Name;
        }

        private static void MemberTip(FellowInfo member) {
            Widgets.Tip(member.Distance.HasValue
                ? $"Click to select {member.Name}, {Display.Distance(member.Distance)} away."
                : $"{member.Name} is too far away to select. {member.Location}");
        }

        private static void Select(FellowInfo member) {
            CoreManager.Current.Actions.SelectItem(unchecked((int)member.Guid));
        }

        private void Refresh() {
            feed.Refresh(PlayerFeed.KindFellowship, RefreshInterval);
        }

        public void Dispose() {
            window.Dispose();
        }
    }
}
