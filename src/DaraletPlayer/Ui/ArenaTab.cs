using System;
using System.Collections.Generic;
using DaraletPlayer.Feed;
using ImGuiNET;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// The Arena tab: where you stand (@playerui arena), your ratings, the /arena commands as buttons, duels you can watch,
    /// how the latest duels ended, and the top of a bracket (from the Market API, like the website's arena leaderboard).
    /// </summary>
    internal class ArenaTab {
        /// <summary>Quick while something's happening (queued, in a duel), slow otherwise.</summary>
        private static readonly TimeSpan BusyRefresh = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan IdleRefresh = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan TopRefresh = TimeSpan.FromMinutes(1);

        private readonly PlayerFeed feed;
        private readonly MarketApi api;
        private readonly ApiRequest<List<LeaderboardRow>> top = new ApiRequest<List<LeaderboardRow>>();
        private string topAskedFor;

        private int levels;
        private bool scaled;
        private bool unrated;
        private bool fellowship;
        private string challengeName = "";
        private string statsName = "";

        private int topSize = 1;
        private bool topScaled;

        public ArenaTab(PlayerFeed feed, MarketApi api) {
            this.feed = feed;
            this.api = api;
        }

        public void Render() {
            var arena = feed.Arena;
            feed.Refresh(PlayerFeed.KindArena, arena?.CanLeave == true ? BusyRefresh : IdleRefresh);

            if (!feed.Supports(PlayerFeed.KindArena)) {
                ImGui.TextColored(Colors.Warning, "This server is too old for the Arena tab.");
                return;
            }

            if (arena == null) {
                ImGui.TextDisabled("Loading...");
                return;
            }

            if (!arena.Enabled) {
                ImGui.TextColored(Colors.Warning, "Arena dueling is turned off right now.");
            }
            else {
                ImGui.TextColored(arena.CanLeave ? Colors.Info : Colors.Muted, arena.Status ?? "");
            }

            if (arena.MinimumLevel > 1) {
                ImGui.TextDisabled($"Duels need level {arena.MinimumLevel}. Only non-player killers and player killer lites can duel; nobody loses anything by being defeated.");
            }

            RenderFinding(arena);
            ImGui.Separator();
            RenderRatings(arena);
            RenderWatch(arena);
            RenderRecent(arena);
            RenderTop();
            RenderReply();
        }

        private void RenderFinding(ArenaInfo arena) {
            ImGui.Text("Find a duel");

            ImGui.Checkbox("Scaled", ref scaled);
            Widgets.Tip("The higher-level fighter is scaled down to the other's level (level 10 and up).");
            ImGui.SameLine();
            ImGui.Checkbox("Unrated", ref unrated);
            Widgets.Tip("Changes no rating or record. Only pairs you with others who asked for unrated.");
            ImGui.SameLine();
            ImGui.Checkbox("Fellowship", ref fellowship);
            Widgets.Tip("Leaders only: your whole fellowship against another of the same size.");

            var busy = arena.CanLeave || !arena.Enabled;

            ImGui.BeginDisabled(busy);
            if (ImGui.Button("Queue")) {
                feed.SendCommand(ArenaCommands.Queue(levels, scaled, unrated, fellowship));
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Waits for an opponent: {ArenaCommands.Queue(levels, scaled, unrated, fellowship)}.");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.InputInt("within levels##arena-levels", ref levels);
            levels = Math.Max(0, levels);
            Widgets.Tip("Only match with someone within this many levels of you. 0 for anyone.");

            ImGui.SetNextItemWidth(160);
            ImGui.InputTextWithHint("##arena-challenge", "player name", ref challengeName, 40);
            ImGui.SameLine();

            ImGui.BeginDisabled(busy || BankCommands.Clean(challengeName).Length == 0);
            if (ImGui.Button("Challenge")) {
                feed.SendCommand(ArenaCommands.Challenge(challengeName, scaled, unrated, fellowship));
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Asks a player to a duel, with the options above: {ArenaCommands.Challenge("<name>", scaled, unrated, fellowship)}.");

            ImGui.SameLine();
            ImGui.BeginDisabled(!arena.CanLeave);
            if (ImGui.Button("Leave")) {
                feed.SendCommand(ArenaCommands.Leave);
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Leaves the queue, calls off or gives up your duel, or stops watching: {ArenaCommands.Leave}.");

            if (arena.QueueCount > 0 && arena.QueuePlace == 0) {
                ImGui.TextDisabled($"{arena.QueueCount} waiting in the queue.");
            }
        }

        private void RenderRatings(ArenaInfo arena) {
            if (!ImGui.CollapsingHeader("Your ratings###arena-ratings", ImGuiTreeNodeFlags.DefaultOpen)) {
                return;
            }

            var boards = arena.Boards ?? new List<ArenaBoardInfo>();

            if (boards.Count == 0) {
                ImGui.TextDisabled($"You haven't fought a rated duel yet. Every rating starts at {arena.StartingRating}.");
            }
            else if (ImGui.BeginTable("arena-boards", 4, Widgets.TableFlags)) {
                ImGui.TableSetupColumn("Bracket", ImGuiTableColumnFlags.WidthFixed, 110);
                ImGui.TableSetupColumn("Rating", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("W / L / D", ImGuiTableColumnFlags.WidthFixed, 90);
                ImGui.TableSetupColumn("Won", ImGuiTableColumnFlags.WidthStretch);
                Widgets.HeadersRow("1v1 to fellowship-sized duels; scaled ones have their own ratings.", $"Elo rating, starting at {arena.StartingRating}.", "Wins, losses and draws.", "Share of duels won.");

                foreach (var board in boards) {
                    var duels = board.Wins + board.Losses + board.Draws;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(board.Name ?? "");
                    ImGui.TableNextColumn();
                    ImGui.Text(board.Rating.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled($"{board.Wins} / {board.Losses} / {board.Draws}");
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(duels > 0 ? $"{100.0 * board.Wins / duels:0}%" : "");
                }

                ImGui.EndTable();
            }

            ImGui.SetNextItemWidth(160);
            ImGui.InputTextWithHint("##arena-stats-name", "someone else's name", ref statsName, 40);
            ImGui.SameLine();

            if (ImGui.SmallButton("Look up")) {
                feed.SendCommand(ArenaCommands.Stats(statsName));
            }
            Widgets.Tip($"Their ratings and records on every bracket: {ArenaCommands.Stats("<name>")}. Shows below.");
        }

        private void RenderWatch(ArenaInfo arena) {
            var duels = arena.Duels ?? new List<ArenaDuelInfo>();

            if (!ImGui.CollapsingHeader($"Duels going on ({duels.Count})###arena-duels", ImGuiTreeNodeFlags.DefaultOpen)) {
                return;
            }

            if (duels.Count == 0) {
                ImGui.TextDisabled("No duels to watch.");
                return;
            }

            foreach (var duel in duels) {
                ImGui.BeginDisabled(arena.CanLeave);
                if (ImGui.SmallButton($"Watch##watch-{duel.Id}")) {
                    feed.SendCommand(ArenaCommands.Watch(duel.Id));
                }
                ImGui.EndDisabled();
                Widgets.Tip($"Watches it unseen: {ArenaCommands.Watch(duel.Id)}. Leave takes you back.");

                ImGui.SameLine();
                ImGui.Text($"#{duel.Id} {duel.Sides}");
                ImGui.SameLine();
                ImGui.TextDisabled($"{Bracket(duel.Size, duel.Scaled, duel.Rated)}{(duel.Map != null ? ", " + duel.Map : "")}, {duel.State}");
            }
        }

        private void RenderRecent(ArenaInfo arena) {
            var recent = arena.Recent ?? new List<ArenaResultInfo>();

            if (!ImGui.CollapsingHeader($"Latest duels ({recent.Count})###arena-recent")) {
                return;
            }

            if (recent.Count == 0) {
                ImGui.TextDisabled("None since the server started.");
                return;
            }

            var since = (DateTime.UtcNow - feed.ArenaUpdatedAt).TotalSeconds;

            foreach (var result in recent) {
                var outcome = result.CalledOff ? "called off" : result.Winners != null ? $"{result.Winners} won" : "a draw";

                ImGui.Text($"{result.Sides}: {outcome}");
                ImGui.SameLine();
                ImGui.TextDisabled($"{Bracket(result.Size, result.Scaled, result.Rated)}, {MarketSearch.Span(result.EndedAgo + since)} ago");
            }
        }

        private void RenderTop() {
            if (!ImGui.CollapsingHeader("Top ratings###arena-top")) {
                return;
            }

            var changed = false;

            foreach (var (value, label) in Leaderboards.ArenaSizes) {
                if (ImGui.RadioButton(label, topSize == value)) {
                    topSize = value;
                    changed = true;
                }

                ImGui.SameLine();
            }

            if (ImGui.Checkbox("Scaled##arena-top-scaled", ref topScaled)) {
                changed = true;
            }
            Widgets.Tip("Scaled duels have ratings of their own.");

            var path = Leaderboards.All[1].Path(new LeaderboardOptions { ArenaSize = topSize, ArenaScaled = topScaled });

            if (changed || topAskedFor != path || DateTime.UtcNow - top.UpdatedAt > TopRefresh && !top.Busy && top.Error == null) {
                topAskedFor = path;
                top.Start(() => api.GetLeaderboard(path));
            }

            LeaderboardsTab.RenderRows("arena-top-table", Leaderboards.All[1], top, feed.Hello?.Name, "", 10);
        }

        private void RenderReply() {
            if (feed.LastCommand == null || !feed.LastCommand.StartsWith("/arena", StringComparison.Ordinal) || feed.LastCommandReply.Count == 0) {
                return;
            }

            ImGui.Separator();

            foreach (var line in feed.LastCommandReply) {
                ImGui.TextWrapped(line);
            }
        }

        private static string Bracket(int size, bool scaled, bool rated) {
            return $"{size}v{size}{(scaled ? " scaled" : "")}{(rated ? "" : ", unrated")}";
        }
    }
}
