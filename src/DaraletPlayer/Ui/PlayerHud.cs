using System;
using System.Collections.Generic;
using System.Numerics;
using DaraletPlayer.Feed;
using ImGuiNET;
using UtilityBelt.Service;
using UtilityBelt.Service.Views;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// The Daralet window: a status line, then a tab for each area.
    /// </summary>
    internal class PlayerHud : IDisposable {
        private readonly PlayerFeed feed;
        private readonly Hud hud;
        private readonly FellowshipTab fellowshipTab;
        private readonly TargetTab targetTab;
        private readonly WorldTab worldTab;
        private readonly BankTab bankTab;
        private readonly MarketTab marketTab;
        private readonly ArenaTab arenaTab;
        private readonly LootTab lootTab;
        private readonly LeaderboardsTab leaderboardsTab;
        private readonly MarketApi api = new MarketApi();

        /// <summary>
        /// The tabs in the order they're shown, by name, tooltip and what draws them.
        /// </summary>
        private readonly List<(string Name, string Tip, Action Render)> tabs;

        public PlayerHud(PlayerFeed feed, Settings settings) {
            this.feed = feed;

            fellowshipTab = new FellowshipTab(feed, settings);
            targetTab = new TargetTab(feed, settings);
            worldTab = new WorldTab(feed, settings);
            bankTab = new BankTab(feed);
            marketTab = new MarketTab(feed, api);
            arenaTab = new ArenaTab(feed, api);
            lootTab = new LootTab(feed, settings);
            leaderboardsTab = new LeaderboardsTab(feed, api);

            tabs = new List<(string, string, Action)> {
                ("World", "A map of Dereth with Daralet's towns, dungeons, lifestones and portals, you and your fellowship, and an arrow to point you anywhere on it.", worldTab.Render),
                ("Fellowship", "Your fellowship's health, stamina and mana, and a vitals window you can place anywhere on screen.", fellowshipTab.Render),
                ("Target", "Your selected target's vitals, buffs, debuffs and threat table, and a tracked target you can watch alongside it.", targetTab.Render),
                ("Loot", "Loot rules in named lists you can swap, and what happens when you open a corpse: loot, salvage, or just show what matches.", lootTab.Render),
                ("Bank", "Your bank's packs and items while it's open, and every /bank command as a button.", bankTab.Render),
                ("Market", "Your listings, sales and expired items, the Market Broker's actions near the market stand, and a search of everything on the market.", marketTab.Render),
                ("Arena", "Your arena ratings and where you stand, the /arena commands as buttons, duels to watch, and the top ratings.", arenaTab.Render),
                ("Leaderboards", "The leaderboards from the Daralet website: level, arena, capstones, deaths and more.", leaderboardsTab.Render),
            };

            hud = UBService.Huds.CreateHud("Daralet");
            hud.ShowInBar = true;
            hud.OnPreRender += Hud_OnPreRender;
            hud.OnRender += Hud_OnRender;
        }

        public void Toggle() {
            hud.Visible = !hud.Visible;
        }

        private void Hud_OnPreRender(object sender, EventArgs e) {
            try {
                ImGui.SetNextWindowSize(new Vector2(760, 620), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSizeConstraints(new Vector2(360, 260), new Vector2(float.MaxValue, float.MaxValue));
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        private void Hud_OnRender(object sender, EventArgs e) {
            try {
                if (disposed) {
                    return;
                }

                if (!RenderStatus()) {
                    return;
                }

                // seven tabs don't fit a narrow window, so they scroll, and the list button at the left jumps to any of them
                if (ImGui.BeginTabBar("DaraletPlayerTabs", ImGuiTabBarFlags.FittingPolicyScroll | ImGuiTabBarFlags.TabListPopupButton)) {
                    foreach (var (name, tip, render) in tabs) {
                        if (Widgets.TabItem(name, tip)) {
                            render();
                            ImGui.EndTabItem();
                        }
                    }

                    ImGui.EndTabBar();
                }
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        /// <summary>
        /// Draws the line at the top of the window. Returns false when the tabs can't be used yet.
        /// </summary>
        private bool RenderStatus() {
            switch (feed.Status) {
                case FeedStatus.Connecting:
                    ImGui.TextDisabled("Connecting to the server...");
                    return false;
                case FeedStatus.Unsupported:
                    ImGui.TextColored(Colors.Warning, "This server doesn't have @playerui. This plugin only works on the Daralet server.");
                    return false;
            }

            ImGui.TextDisabled(feed.Hello.Name);
            Widgets.Tip("The character you're on. Type /dap to show or hide this window.");
            return true;
        }

        private bool disposed;

        /// <summary>
        /// Closes every piece on its own, so one that fails can't leave the rest drawing and asking the server for data after
        /// the plugin has been shut down.
        /// </summary>
        public void Dispose() {
            disposed = true;
            hud.OnPreRender -= Hud_OnPreRender;
            hud.OnRender -= Hud_OnRender;

            foreach (var piece in new IDisposable[] { hud, fellowshipTab, targetTab, worldTab, bankTab, marketTab, lootTab, api }) {
                try {
                    piece.Dispose();
                }
                catch (Exception ex) {
                    PluginCore.Log(ex);
                }
            }
        }
    }
}
