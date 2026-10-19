using System;
using System.Collections.Generic;
using System.Linq;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using DaraletPlayer.Feed;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// The looter: when a corpse is opened it asks the server what's on it (@playerui corpse), decides each item with the
    /// character's active loot list, and does what the corpse mode says. Looting is the game's own move, one item at a time
    /// (the game won't take a second while one's moving); salvaging is @playerui salvage, the game's own salvage with the
    /// Ust you carry, once what's to be salvaged has been looted.
    /// </summary>
    internal class Looter : IDisposable {
        /// <summary>A move that hasn't landed by now is given up on, and the next one tried.</summary>
        private static readonly TimeSpan MoveTimeout = TimeSpan.FromSeconds(3);

        /// <summary>Asking about a corpse again, if the first answer didn't come.</summary>
        private static readonly TimeSpan CorpseRetry = TimeSpan.FromSeconds(3);

        /// <summary>How often the Corpse window checks what's still on the corpse.</summary>
        private static readonly TimeSpan DropLootedInterval = TimeSpan.FromMilliseconds(250);

        private const int MaxSalvagePerRequest = 20;

        private const int ChatColor = 13;
        private const int NotifyColor = 5;

        private readonly PlayerFeed feed;
        private readonly Settings settings;

        private uint openCorpse;
        private DateTime askedAt = DateTime.MinValue;
        private DateTime handledReplyAt = DateTime.MinValue;
        private DateTime droppedLootedAt = DateTime.MinValue;

        private readonly Queue<uint> toLoot = new Queue<uint>();
        private uint moving;
        private uint movingFrom;
        private DateTime movedAt;
        private readonly List<uint> toSalvageAfterLooting = new List<uint>();
        private bool salvageWhenLooted;

        public Looter(PlayerFeed feed, Settings settings) {
            this.feed = feed;
            this.settings = settings;

            CoreManager.Current.ContainerOpened += Core_ContainerOpened;
            CoreManager.Current.RenderFrame += Core_RenderFrame;
        }

        private LootData Data => settings.Data.Loot;

        /// <summary>The open corpse's items with what the active list says about each, once the server has said.</summary>
        public List<LootDecision> Decisions { get; private set; } = new List<LootDecision>();

        /// <summary>The open corpse, or null.</summary>
        public CorpseInfo Corpse { get; private set; }

        /// <summary>Items looted to be salvaged, waiting on the Salvage button (auto loot, confirm salvage).</summary>
        public List<uint> SalvageWaiting { get; } = new List<uint>();

        public bool Busy => moving != 0 || toLoot.Count > 0;

        /// <summary>A corpse's items have been decided: the Loot tab shows the Corpse window for the popup modes.</summary>
        public event Action Decided;

        public LootList ActiveList => Data.ActiveFor(CoreManager.Current.CharacterFilter.Name);

        /// <summary>Loots these, then salvages the ones that are to be salvaged, if salvage is true.</summary>
        public void Loot(IEnumerable<LootDecision> decisions, bool salvage) {
            foreach (var decision in decisions) {
                if (!toLoot.Contains(decision.Item.Guid) && decision.Item.Guid != moving) {
                    toLoot.Enqueue(decision.Item.Guid);
                }

                if (salvage && decision.Outcome == LootOutcome.Salvage && decision.Item.Salvageable) {
                    toSalvageAfterLooting.Add(decision.Item.Guid);
                }
            }

            salvageWhenLooted = salvage;
        }

        /// <summary>Salvages items you carry, 20 to a request (the most @playerui salvage takes).</summary>
        public void Salvage(IEnumerable<uint> guids) {
            var items = guids.Distinct().ToList();

            for (var start = 0; start < items.Count; start += MaxSalvagePerRequest) {
                var batch = items.Skip(start).Take(MaxSalvagePerRequest).ToArray();
                feed.SendCommand("@playerui salvage " + Display.TargetArguments(batch));
            }
        }

        private void Core_ContainerOpened(object sender, ContainerOpenedEventArgs e) {
            try {
                var guid = unchecked((uint)e.ItemGuid);

                if (guid == 0) {
                    // the container was closed
                    openCorpse = 0;
                    Corpse = null;
                    Decisions = new List<LootDecision>();
                    return;
                }

                var opened = CoreManager.Current.WorldFilter[e.ItemGuid];

                if (opened == null || opened.ObjectClass != ObjectClass.Corpse || !feed.Supports(PlayerFeed.KindCorpse)) {
                    return;
                }

                openCorpse = guid;
                Corpse = null;
                Decisions = new List<LootDecision>();

                if (Data.Enabled) {
                    Ask();
                }
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        private void Ask() {
            askedAt = DateTime.UtcNow;
            feed.Request(PlayerFeed.KindCorpse, $"0x{openCorpse:X8}");
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (openCorpse != 0 && Data.Enabled) {
                    TakeCorpseReply();
                }

                DropLooted();

                MoveNext();
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        private void TakeCorpseReply() {
            var reply = feed.Corpse;

            if (reply == null || reply.Guid != openCorpse || feed.CorpseUpdatedAt <= handledReplyAt) {
                if (Corpse == null && DateTime.UtcNow - askedAt > CorpseRetry) {
                    Ask();
                }

                return;
            }

            handledReplyAt = feed.CorpseUpdatedAt;
            Corpse = reply;

            if (reply.Status != CorpseInfo.StatusOk) {
                return;
            }

            var list = ActiveList;
            Decisions = (reply.Items ?? new List<CorpseItemInfo>())
                .Select(item => Feed.Loot.Decide(list, item))
                .Where(d => d.Rule != null && d.Outcome != LootOutcome.Ignore)
                .ToList();

            foreach (var notify in Decisions.Where(d => d.Outcome == LootOutcome.Notify)) {
                Chat($"{notify.Item.Name} is on {reply.Name} ({notify.Rule.Name}).", NotifyColor);
            }

            var looted = Decisions.Where(d => d.Loots).ToList();
            Decided?.Invoke();

            switch (Data.Mode) {
                case CorpseMode.AutoLootAutoSalvage:
                    Loot(looted, salvage: true);
                    break;
                case CorpseMode.AutoLootConfirmSalvage:
                    Loot(looted, salvage: false);
                    SalvageWaiting.Clear();
                    SalvageWaiting.AddRange(looted.Where(d => d.Outcome == LootOutcome.Salvage && d.Item.Salvageable).Select(d => d.Item.Guid));
                    break;
                case CorpseMode.ChatHighlights:
                    foreach (var decision in looted) {
                        Chat($"{decision.Item.Name}: {(decision.Outcome == LootOutcome.Salvage ? "salvage" : "keep")} ({decision.Rule.Name}).", ChatColor);
                    }
                    break;
            }
        }

        /// <summary>
        /// Takes items that aren't on the corpse any more out of the Corpse window: looted by the looter, by a click in the
        /// window, or dragged out by hand. Four times a second.
        /// </summary>
        private void DropLooted() {
            if (openCorpse == 0 || Decisions.Count == 0 || DateTime.UtcNow - droppedLootedAt < DropLootedInterval) {
                return;
            }

            droppedLootedAt = DateTime.UtcNow;
            Decisions.RemoveAll(d => !IsIn(d.Item.Guid, openCorpse));
        }

        /// <summary>
        /// Moves the next queued item into your main pack once the last one has landed (it's no longer on the corpse) or
        /// timed out; salvages when the queue's done, if it was asked to.
        /// </summary>
        private void MoveNext() {
            if (moving != 0) {
                var landed = !IsIn(moving, movingFrom);

                if (!landed && DateTime.UtcNow - movedAt < MoveTimeout) {
                    return;
                }

                moving = 0;
            }

            if (toLoot.Count == 0) {
                if (salvageWhenLooted && toSalvageAfterLooting.Count > 0) {
                    Salvage(toSalvageAfterLooting.ToList());
                    toSalvageAfterLooting.Clear();
                }

                salvageWhenLooted = false;
                return;
            }

            moving = toLoot.Dequeue();
            movedAt = DateTime.UtcNow;
            movingFrom = openCorpse;
            CoreManager.Current.Actions.MoveItem(unchecked((int)moving), CoreManager.Current.CharacterFilter.Id, 0, true);
        }

        /// <summary>Whether the client still sees the item in that container.</summary>
        private static bool IsIn(uint item, uint container) {
            var found = CoreManager.Current.WorldFilter[unchecked((int)item)];
            return found != null && found.Values(LongValueKey.Container, 0) == unchecked((int)container);
        }

        private static void Chat(string text, int color) {
            CoreManager.Current.Actions.AddChatText($"[Loot] {text}", color);
        }

        public void Dispose() {
            CoreManager.Current.ContainerOpened -= Core_ContainerOpened;
            CoreManager.Current.RenderFrame -= Core_RenderFrame;
        }
    }
}
