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
        /// <summary>
        /// A move that hasn't landed or failed by this long after it was sent is sent again, at the least: a move can be
        /// dropped with no answer at all, so waiting much longer than one pickup was most of Loot all's time (3 seconds made it
        /// 4 an item). The wait grows with how long pickups have been taking (MoveTimeoutNow), for a slower connection. A
        /// second move for an item already on its way is just refused.
        /// </summary>
        private static readonly TimeSpan MinMoveTimeout = TimeSpan.FromMilliseconds(900);

        /// <summary>
        /// How long after an item lands the next is sent. The log showed a move sent the moment the last landed was dropped
        /// with no answer every time (the pickup's animation still finishing), costing a whole timeout; a short pause gets it
        /// through first time.
        /// </summary>
        private static readonly TimeSpan AfterLanding = TimeSpan.FromMilliseconds(250);

        /// <summary>How long the last first-try pickup took to land, sent to landed; zero before one has.</summary>
        private TimeSpan lastLanding = TimeSpan.Zero;

        /// <summary>When the next item may be sent (AfterLanding after the last landed).</summary>
        private DateTime nextSendAt = DateTime.MinValue;

        private TimeSpan MoveTimeoutNow => TimeSpan.FromTicks(Math.Max(MinMoveTimeout.Ticks, (long)(lastLanding.Ticks * 1.5)));

        /// <summary>
        /// How long after a refused move it's sent again. The server refuses a pickup that comes while the last one is still
        /// starting ("You're too busy", Player.HandleActionPutItemInContainer), which happens when items are looted one
        /// after another; a pickup sent while the last is finishing is queued by the server instead.
        /// </summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(150);

        /// <summary>An item still not looted after this long, retries and all, is given up on (a full pack, say).</summary>
        private static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(8);

        private const uint GameEventMessageType = 0xF7B0;

        /// <summary>The server's answer to a move it refused (GameEventInventoryServerSaveFailed).</summary>
        private const int MoveFailedEvent = 0x00A0;

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

        private readonly LinkedList<uint> toLoot = new LinkedList<uint>();
        private uint moving;
        private uint movingFrom;
        private DateTime movedAt;
        /// <summary>When the item being moved was first tried, for giving up on it.</summary>
        private DateTime firstTriedAt;
        /// <summary>The server refused the move in flight; it's tried again once RetryDelay has passed.</summary>
        private bool moveFailed;
        private DateTime retryAt = DateTime.MinValue;

        /// <summary>How many times the item moving now has been sent, for the log.</summary>
        private int tries;
        private uint lastSent;
        private readonly List<uint> toSalvageAfterLooting = new List<uint>();
        private bool salvageWhenLooted;

        public Looter(PlayerFeed feed, Settings settings) {
            this.feed = feed;
            this.settings = settings;

            CoreManager.Current.ContainerOpened += Core_ContainerOpened;
            CoreManager.Current.RenderFrame += Core_RenderFrame;
            CoreManager.Current.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
        }

        private LootData Data => settings.Data.Loot;

        /// <summary>The open corpse's items with what the active list says about each, once the server has said.</summary>
        public List<LootDecision> Decisions { get; private set; } = new List<LootDecision>();

        /// <summary>The open corpse, or null.</summary>
        public CorpseInfo Corpse { get; private set; }

        /// <summary>Items looted to be salvaged, waiting on the Salvage button (auto loot, confirm salvage).</summary>
        public List<uint> SalvageWaiting { get; } = new List<uint>();

        public bool Busy => moving != 0 || toLoot.Count > 0;

        /// <summary>Items still to loot, the one moving now included.</summary>
        public int Remaining => toLoot.Count + (moving != 0 ? 1 : 0);

        /// <summary>The name of the item being looted now, or null.</summary>
        public string MovingName => moving == 0 ? null : CoreManager.Current.WorldFilter[unchecked((int)moving)]?.Name;

        /// <summary>
        /// Stops looting: nothing more is moved or salvaged. An item already on its way may still land.
        /// </summary>
        public void Cancel() {
            toLoot.Clear();
            moving = 0;
            moveFailed = false;
            retryAt = DateTime.MinValue;
            toSalvageAfterLooting.Clear();
            salvageWhenLooted = false;
        }

        /// <summary>
        /// Every item in the open corpse or chest, each with the rule that decides it (or none), for the Corpse window with
        /// Loot window on, where it's the only view of what's in there.
        /// </summary>
        public List<LootDecision> Everything { get; private set; } = new List<LootDecision>();

        /// <summary>
        /// Loot window is on and the server can do it: corpses and chests open in the plugin's Corpse window instead of the
        /// game's, and the server says when one opens and closes (the client's ContainerOpened doesn't fire without the
        /// game's window).
        /// </summary>
        public bool UsesWindow => settings.Data.LootWindow && feed.Supports(PlayerFeed.KindLootWindow);

        /// <summary>The open corpse or chest, or 0.</summary>
        public uint OpenGuid => openCorpse;

        public LootList ActiveList => Data.ActiveFor(CoreManager.Current.CharacterFilter.Name);

        /// <summary>Loots these, then salvages the ones that are to be salvaged, if salvage is true.</summary>
        public void Loot(IEnumerable<LootDecision> decisions, bool salvage) {
            foreach (var decision in decisions) {
                if (!toLoot.Contains(decision.Item.Guid) && decision.Item.Guid != moving) {
                    toLoot.AddLast(decision.Item.Guid);
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

                var opened = guid == 0 ? null : CoreManager.Current.WorldFilter[e.ItemGuid];

                if (UsesWindow) {
                    // the server says when one opens and closes (FollowServer); the client's own word isn't about the
                    // plugin's window, and a close for the last one can come after the next has opened
                    LogCorpse($"client ContainerOpened 0x{guid:X8} {opened?.Name} ({opened?.ObjectClass}), ignored with Loot window on");
                    return;
                }

                if (guid == 0) {
                    // the container was closed
                    Forget();
                    return;
                }

                if (opened == null || !IsLootable(opened) || !feed.Supports(PlayerFeed.KindCorpse)) {
                    return;
                }

                Forget();
                openCorpse = guid;

                if (Data.Enabled) {
                    Ask();
                }
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        /// <summary>
        /// A corpse, or a container standing in the world (a chest): not one of your packs, which are in your inventory, and
        /// not your open bank. The server has the last word: it only describes a chest you have open, and answers
        /// "notcorpse" for any other container (a house's storage, say), which the looter then leaves alone.
        /// </summary>
        private bool IsLootable(WorldObject opened) {
            if (opened.ObjectClass == ObjectClass.Corpse) {
                return true;
            }

            return opened.ObjectClass == ObjectClass.Container
                && opened.Values(LongValueKey.Container, 0) == 0
                && unchecked((uint)opened.Id) != (feed.Bank?.Guid ?? 0);
        }

        private void Ask() {
            askedAt = DateTime.UtcNow;
            feed.Request(PlayerFeed.KindCorpse, $"0x{openCorpse:X8}");
        }

        private void Forget() {
            seenInCorpse.Clear();
            openCorpse = 0;
            Corpse = null;
            Decisions = new List<LootDecision>();
            Everything = new List<LootDecision>();
        }

        /// <summary>
        /// With Loot window on, the server says when a corpse or chest opens (its description, unasked) and closes (status
        /// "closed"), since the client doesn't: this takes those up.
        /// </summary>
        private void FollowServer() {
            var reply = feed.Corpse;

            if (reply == null || feed.CorpseUpdatedAt <= handledReplyAt) {
                return;
            }

            if (reply.Status == CorpseInfo.StatusClosed) {
                handledReplyAt = feed.CorpseUpdatedAt;
                LogCorpse($"closed 0x{reply.Guid:X8}{(reply.Guid == openCorpse ? "" : " (not the open one)")}");

                if (reply.Guid == openCorpse) {
                    Forget();
                }

                return;
            }

            // a newly opened one: TakeCorpseReply decides it, now it's the open one. One the server won't describe (too far,
            // not yours) is taken too, so the window can say why instead of not opening
            if (reply.Guid != openCorpse && reply.Status != CorpseInfo.StatusNotLootable) {
                LogCorpse($"opened 0x{reply.Guid:X8} {reply.Name}: {reply.Status}, {reply.Items?.Count ?? 0} items");
                Forget();
                openCorpse = reply.Guid;
                askedAt = DateTime.UtcNow;
            }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (feed.Status == FeedStatus.Ready) {
                    feed.WantWindow(PlayerFeed.KindLootWindow, settings.Data.LootWindow);
                }

                if (UsesWindow) {
                    FollowServer();
                }

                // with Loot window on, the window is the only view of the corpse, so it's described even with the looter off
                if (openCorpse != 0 && (Data.Enabled || UsesWindow)) {
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
            LogCorpse($"described 0x{reply.Guid:X8} {reply.Name}: {reply.Status}, {reply.Items?.Count ?? 0} items");

            // a container the server won't loot through the plugin (not a corpse or a chest): nothing to show
            if (reply.Status == CorpseInfo.StatusNotLootable) {
                openCorpse = 0;
                Corpse = null;
                return;
            }

            Corpse = reply;

            if (reply.Status != CorpseInfo.StatusOk) {
                return;
            }

            var list = ActiveList;
            Everything = (reply.Items ?? new List<CorpseItemInfo>())
                .Select(item => Feed.Loot.Decide(list, item))
                .ToList();
            Decisions = Everything
                .Where(d => d.Rule != null && d.Outcome != LootOutcome.Ignore)
                .ToList();

            if (!Data.Enabled) {
                // only the window's list: the looter itself is off
                return;
            }

            foreach (var notify in Decisions.Where(d => d.Outcome == LootOutcome.Notify)) {
                Chat($"{notify.Item.Name} is on {reply.Name} ({notify.Rule.Name}).", NotifyColor);
            }

            var looted = Decisions.Where(d => d.Loots).ToList();

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
            if (openCorpse == 0 || Everything.Count == 0 || DateTime.UtcNow - droppedLootedAt < DropLootedInterval) {
                return;
            }

            droppedLootedAt = DateTime.UtcNow;
            Decisions.RemoveAll(d => Left(d.Item.Guid, openCorpse));
            var dropped = Everything.RemoveAll(d => Left(d.Item.Guid, openCorpse));

            if (dropped > 0) {
                LogCorpse($"{dropped} item(s) left 0x{openCorpse:X8}, {Everything.Count} still in it");
            }
        }

        /// <summary>
        /// What the looter heard and did, in log.txt only, for working out why a corpse showed wrong. Corpses and chests
        /// behave differently (use radius, when their items reach the client), so it's worth keeping until they're settled.
        /// </summary>
        private static void LogCorpse(string text) {
            PluginCore.LogFile("[loot] " + text);
        }

        /// <summary>
        /// Items the client has had in the open corpse. With Loot window on, the server's word that a corpse opened can come
        /// before the items themselves reach the client, so an item the client doesn't have yet hasn't left: it only counts as
        /// gone once it's been seen there.
        /// </summary>
        private readonly HashSet<uint> seenInCorpse = new HashSet<uint>();

        /// <summary>Whether the item was in the container and isn't now: looted, by the looter or by hand.</summary>
        private bool Left(uint item, uint container) {
            if (IsIn(item, container)) {
                seenInCorpse.Add(item);
                return false;
            }

            return seenInCorpse.Contains(item);
        }

        /// <summary>
        /// Moves the next queued item into your main pack once the last one has landed (it's no longer on the corpse) or
        /// timed out; salvages when the queue's done, if it was asked to.
        /// </summary>
        private void MoveNext() {
            var now = DateTime.UtcNow;

            if (moving != 0) {
                if (Left(moving, movingFrom)) {
                    // landed (or taken by hand): on to the next
                    LogCorpse($"0x{moving:X8} landed {(now - firstTriedAt).TotalMilliseconds:0}ms after the first try, {tries} tr{(tries == 1 ? "y" : "ies")}");

                    // only a first try times a pickup cleanly
                    if (tries == 1) {
                        lastLanding = now - movedAt;
                    }

                    moving = 0;
                    moveFailed = false;
                    retryAt = DateTime.MinValue;
                    nextSendAt = now + AfterLanding;
                }
                else if (moveFailed || now - movedAt > MoveTimeoutNow) {
                    // refused, most likely for coming while the last pickup was still starting: the same item again, after
                    // a moment, until it's been tried for long enough that something else is wrong
                    if (now - firstTriedAt < GiveUpAfter) {
                        if (retryAt == DateTime.MinValue) {
                            retryAt = now + RetryDelay;
                        }

                        if (now >= retryAt) {
                            LogCorpse($"0x{moving:X8} {(moveFailed ? "refused" : "no answer")} after {(now - movedAt).TotalMilliseconds:0}ms, sending again");
                            retryAt = DateTime.MinValue;
                            moveFailed = false;
                            Send(moving);
                        }

                        return;
                    }

                    Chat($"Couldn't loot {CoreManager.Current.WorldFilter[unchecked((int)moving)]?.Name ?? "an item"}: is your main pack full?", NotifyColor);
                    moving = 0;
                    moveFailed = false;
                    retryAt = DateTime.MinValue;
                }
                else {
                    return;
                }
            }

            if (toLoot.Count == 0) {
                if (salvageWhenLooted && toSalvageAfterLooting.Count > 0) {
                    Salvage(toSalvageAfterLooting.ToList());
                    toSalvageAfterLooting.Clear();
                }

                salvageWhenLooted = false;
                return;
            }

            if (now < nextSendAt) {
                return;
            }

            moving = toLoot.First.Value;
            toLoot.RemoveFirst();
            movingFrom = openCorpse;
            firstTriedAt = now;
            Send(moving);
        }

        /// <summary>Moves the item into your main pack, where the server picks its slot.</summary>
        private void Send(uint item) {
            tries = item == lastSent ? tries + 1 : 1;
            lastSent = item;
            movedAt = DateTime.UtcNow;
            CoreManager.Current.Actions.MoveItem(unchecked((int)item), CoreManager.Current.CharacterFilter.Id, 0, true);
        }

        /// <summary>
        /// Notices the server refusing a move. Only one move is ever in flight, so a refusal while one is is that one's.
        /// </summary>
        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (moving != 0 && e.Message.Type == GameEventMessageType && e.Message.Value<int>("event") == MoveFailedEvent) {
                    moveFailed = true;
                }
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
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
            CoreManager.Current.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
        }
    }
}
