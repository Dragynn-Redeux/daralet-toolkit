using System;
using DaraletPlayer.Feed;
using Decal.Adapter;
using ImGuiNET;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// The Bank tab: your bank as a window of its own (BankBrowser) while it's open, and every /bank command as buttons. The
    /// server checks everything again: these only send the same commands you could type.
    /// </summary>
    internal class BankTab : IDisposable {
        /// <summary>How often the server is asked about the bank: quickly at the bank, slowly away from it.</summary>
        private static readonly TimeSpan OpenRefresh = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan ClosedRefresh = TimeSpan.FromSeconds(5);

        private readonly PlayerFeed feed;
        private readonly BankBrowser browser;

        private string what = "";
        private int count;
        private int logEntries = 20;

        /// <summary>The last container the game opened: asking about the bank with it means a newly opened bank is asked
        /// about straight away (the server doesn't need it).</summary>
        private int openedContainer;

        public BankTab(PlayerFeed feed) {
            this.feed = feed;
            browser = new BankBrowser(feed);

            CoreManager.Current.ContainerOpened += Core_ContainerOpened;
        }

        public void Render() {
            var bank = feed.Bank;
            feed.Refresh(PlayerFeed.KindBank, bank?.Open == true ? OpenRefresh : ClosedRefresh, openedContainer.ToString());

            if (!feed.Supports(PlayerFeed.KindBank)) {
                ImGui.TextColored(Colors.Warning, "This server is too old for the Bank tab.");
                return;
            }

            RenderStatus(bank);

            if (!ImGui.BeginTabBar("bank-tabs")) {
                return;
            }

            if (Widgets.TabItem("Bank", "Your bank's packs and items, and the packs you carry, while your bank is open. Drag items between them.")) {
                if (bank?.Open != true) {
                    ImGui.TextDisabled("Open your bank to see what's in it here.");
                }
                else if (!bank.Loaded) {
                    ImGui.TextDisabled("Your bank is opening...");
                }
                else {
                    browser.Render(bank);
                }

                ImGui.EndTabItem();
            }

            if (Widgets.TabItem("Commands", "Every /bank command: deposit, withdraw, sort, search and more.")) {
                RenderCommands(bank?.Open == true);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        private void RenderStatus(BankInfo bank) {
            if (bank?.Open == true && bank.Loaded) {
                ImGui.Text($"{bank.Coins:N0} pyreals");
                ImGui.SameLine();
                ImGui.TextDisabled($"and {bank.Notes:N0} in {bank.NoteCount:N0} trade note{(bank.NoteCount == 1 ? "" : "s")}");
                Widgets.Tip("What's in this bank. /bank balance counts your whole account, and works anywhere.");
            }
            else {
                ImGui.TextDisabled("Your bank isn't open.");
                Widgets.Tip("Most /bank commands need you at your open bank. Balance, log and intro work anywhere.");
            }

            if (feed.LastCommand != null) {
                ImGui.SameLine();
                ImGui.TextDisabled($"   {feed.LastCommand}");
            }
        }

        private void RenderCommands(bool open) {
            ImGui.SetNextItemWidth(260);
            ImGui.InputTextWithHint("##bank-what", "what: a category, type, tier or item name", ref what, 100);
            Widgets.Tip("What the buttons below work on: a category (weapons), a type (swords), a tier (t6), any mix (swords t6), or an item's name for withdraw and search.");

            ImGui.SameLine();
            if (ImGui.Button("Pick...")) {
                ImGui.OpenPopup("bank-words");
            }
            Widgets.Tip("The words /bank understands.");
            RenderWordPicker();

            var hasWhat = BankCommands.HasWhat(what);

            ImGui.BeginDisabled(!open || !hasWhat);
            if (ImGui.Button("Deposit")) {
                feed.SendCommand(BankCommands.Deposit(what));
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Moves those items from your packs into your bank: {BankCommands.Deposit("<what>")}. Packs inscribed \"keep\" are left alone. Needs your bank open.");

            ImGui.SameLine();
            ImGui.BeginDisabled(!open || !hasWhat);
            if (ImGui.Button("Withdraw")) {
                feed.SendCommand(BankCommands.Withdraw(count, what));
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Takes those items out of your bank into your packs, filling named and inscribed packs first: {BankCommands.Withdraw(null, "<what>")}. With a count, takes that many. Stops at your burden limit. Needs your bank open.");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.InputInt("how many##bank-count", ref count);
            count = Math.Max(0, count);
            Widgets.Tip("How many to withdraw, or 0 for all of them.");

            ImGui.BeginDisabled(!open);
            if (ImGui.Button("Sort")) {
                feed.SendCommand(BankCommands.Sort(what));
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Files items into their inscribed packs, combines stacks and puts everything in order: {BankCommands.Sort("[what]")}. Sorts only what's typed above, or everything. Needs your bank open.");

            ImGui.SameLine();
            ImGui.BeginDisabled(!open || !hasWhat);
            if (ImGui.Button("Search")) {
                feed.SendCommand(BankCommands.Search(what));
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Lists matching items in your bank and where they are: {BankCommands.Search("<what>")}. Needs your bank open.");

            ImGui.SameLine();
            ImGui.BeginDisabled(!open);
            if (ImGui.Button("Deposit packs")) {
                feed.SendCommand(BankCommands.DepositPacks);
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Empties the packs you carry inscribed \"deposit\" into your bank: {BankCommands.DepositPacks}. Needs your bank open.");

            ImGui.SameLine();
            ImGui.BeginDisabled(!open);
            if (ImGui.Button("Combine salvage")) {
                feed.SendCommand(BankCommands.Combine);
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Combines salvage bags in your bank with the same material and workmanship: {BankCommands.Combine}. Needs your bank open.");

            ImGui.Separator();

            if (ImGui.Button("Balance")) {
                feed.SendCommand(BankCommands.Balance);
            }
            Widgets.Tip($"The pyreals and trade notes in your bank and across your account: {BankCommands.Balance}. Works anywhere.");

            ImGui.SameLine();
            if (ImGui.Button("Log")) {
                feed.SendCommand(BankCommands.Log(logEntries));
            }
            Widgets.Tip($"Your account's recent deposits, withdrawals and salvage combines, by any of your characters: {BankCommands.Log(null)} [how many]. Works anywhere.");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.InputInt("entries##bank-log", ref logEntries);
            logEntries = Math.Max(1, Math.Min(100, logEntries));

            ImGui.SameLine();
            ImGui.BeginDisabled(!open);
            if (ImGui.Button("Packs")) {
                feed.SendCommand(BankCommands.Packs);
            }
            ImGui.EndDisabled();
            Widgets.Tip($"Your bank space and how each pack is tagged: {BankCommands.Packs}.");

            ImGui.SameLine();
            var autoSort = feed.Bank?.AutoSort ?? false;
            if (ImGui.Checkbox("Autosort", ref autoSort)) {
                feed.SendCommand(BankCommands.AutoSort(autoSort));
            }
            Widgets.Tip($"Sorts your bank after every deposit and withdrawal: {BankCommands.AutoSort(true)} / off.");

            ImGui.SameLine();
            if (ImGui.Button("Intro")) {
                feed.SendCommand(BankCommands.Intro);
            }
            Widgets.Tip($"Shows the bank's introduction again: {BankCommands.Intro}.");

            ImGui.Separator();
            RenderReply();
        }

        private void RenderWordPicker() {
            if (!ImGui.BeginPopup("bank-words")) {
                return;
            }

            ImGui.TextDisabled("Categories");
            WordButtons(BankCommands.Categories);

            foreach (var (category, types) in BankCommands.Types) {
                ImGui.TextDisabled(category);
                WordButtons(types);
            }

            ImGui.TextDisabled("Tiers (add to the others, or alone for any gear of that tier)");
            WordButtons(BankCommands.Tiers);

            ImGui.EndPopup();
        }

        /// <summary>
        /// A row of small buttons, each adding its word to the "what" box.
        /// </summary>
        private void WordButtons(string[] words) {
            for (var i = 0; i < words.Length; i++) {
                if (i > 0 && i % 7 != 0) {
                    ImGui.SameLine();
                }

                if (ImGui.SmallButton(words[i])) {
                    what = BankCommands.Clean(what + " " + words[i]);
                }
                Widgets.Tip($"Adds \"{words[i]}\" to the box above.");
            }
        }

        private void RenderReply() {
            if (feed.LastCommand == null) {
                ImGui.TextDisabled("What the server says back shows here.");
                return;
            }

            if (!ImGui.BeginChild("bank-reply", new System.Numerics.Vector2(0, 0), true)) {
                ImGui.EndChild();
                return;
            }

            foreach (var line in feed.LastCommandReply) {
                ImGui.TextWrapped(line);
            }

            ImGui.EndChild();
        }

        private void Core_ContainerOpened(object sender, ContainerOpenedEventArgs e) {
            try {
                openedContainer = e.ItemGuid;
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        public void Dispose() {
            CoreManager.Current.ContainerOpened -= Core_ContainerOpened;
        }
    }
}
