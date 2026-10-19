using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using DaraletPlayer.Feed;
using ImGuiNET;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// Packs and what's in them: the packs on the left, what's in the chosen one on the right, as a list or a grid. Drag an
    /// item onto a pack, or right-click it, to move it; hover it for its details. One of these shows your bank and its packs
    /// (the server says which container is the bank and numbers its packs, @playerui bank), another the packs you carry.
    /// The items are Decal's (WorldFilter); moving is the game's own move, so the server checks it like a drag in the game.
    /// </summary>
    internal class BankBrowser {
        private const string DragType = "DAP_ITEM";

        /// <summary>A grid icon's size, in pixels: the game's own, as portal.dat has them.</summary>
        private const float GridIconSize = 32;

        /// <summary>The items are read from Decal this often, not every frame: a bank can hold a thousand of them.</summary>
        private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMilliseconds(500);

        private readonly PlayerFeed feed;
        private readonly ItemIcons icons;
        private readonly Settings settings;

        private readonly List<Holder> holders = new List<Holder>();
        private readonly Dictionary<int, List<Item>> contents = new Dictionary<int, List<Item>>();
        private DateTime snapshotAt = DateTime.MinValue;

        private int selected;
        private string search = "";
        private string inscription = "";
        private int inscriptionFor;
        private int dragged;

        /// <summary>The packs you carry rather than your bank's.</summary>
        private readonly bool carried;

        /// <param name="host">The window it's drawn in ("main" for the Daralet window's tabs, "bank-window"): the views in one
        /// window share whether it's been sized for the paperdoll.</param>
        public BankBrowser(PlayerFeed feed, ItemIcons icons, Settings settings, bool carried, string host) {
            this.feed = feed;
            this.icons = icons;
            this.settings = settings;
            this.carried = carried;
            this.host = host;
        }

        private readonly string host;

        /// <summary>
        /// Whether each window was last sized for the paperdoll, by host: it grows or shrinks once when Worn changes, however
        /// many of its views (Carried and Bank, in the Daralet window) show it. Missing until the window first draws, which
        /// takes its size as it is, already right for the setting it was saved with.
        /// </summary>
        private static readonly Dictionary<string, bool> wornShownAs = new Dictionary<string, bool>();

        /// <summary>A container on the left: the bank, one of its packs, your main pack or one of your packs.</summary>
        private class Holder {
            public int Id;
            public string Label;
            public string Detail;
            public bool InBank;
            /// <summary>The bank pack's number in /bank packs, or 0.</summary>
            public int PackNumber;
            public string Inscription;
            public IconLayers Icon;
        }

        private class Item {
            public int Id;
            public string Name;
            public int Stack;
            public int Value;
            public int Burden;
            public int Slot;
            public int Container;
            public IconLayers Icon;
            /// <summary>The loot rule that marks it, with Loot marks on, or null.</summary>
            public LootRule Mark;
            /// <summary>Where it's worn (the game's equip locations), or 0 for one that isn't.</summary>
            public uint Worn;
        }

        /// <summary>What you're wearing, for the paperdoll (Worn), from the same snapshot as the packs.</summary>
        private readonly List<Item> worn = new List<Item>();

        /// <summary>Space between the paperdoll's squares.</summary>
        private const float WornGap = 4;

        /// <param name="bank">The open bank, for the bank's view; the packs you carry don't need it.</param>
        public void Render(BankInfo bank = null) {
            Snapshot(bank);

            // the paperdoll slides out: turning Worn on or off makes the window that much wider or narrower, so the packs and
            // items keep their room (squeezing them hid the very checkbox that turns it off)
            var panelWidth = EquipSlots.Columns * GridIconSize + (EquipSlots.Columns - 1) * WornGap + ImGui.GetStyle().WindowPadding.X * 2
                + ImGui.GetStyle().ItemSpacing.X;

            var showWorn = settings.Data.BankShowWorn;

            if (!wornShownAs.TryGetValue(host, out var sizedFor)) {
                wornShownAs[host] = showWorn;
            }
            else if (sizedFor != showWorn) {
                wornShownAs[host] = showWorn;
                ImGui.SetWindowSize(ImGui.GetWindowSize() + new Vector2(showWorn ? panelWidth : -panelWidth, 0));
            }

            var wornWidth = settings.Data.BankShowWorn ? panelWidth - ImGui.GetStyle().ItemSpacing.X : 0;
            var size = ImGui.GetContentRegionAvail();
            var room = size.X - (wornWidth > 0 ? panelWidth : 0);

            if (ImGui.BeginChild("bank-packs", new Vector2(Math.Min(260, room * 0.38f), 0), true)) {
                RenderHolders();
            }
            ImGui.EndChild();

            ImGui.SameLine();

            if (ImGui.BeginChild("bank-items", new Vector2(wornWidth > 0 ? -panelWidth : 0, 0), true)) {
                RenderItems();
            }
            ImGui.EndChild();

            // dropped anywhere on the items, an item goes into the pack they're the items of: a worn item comes off into it
            DropTarget(selected);

            if (wornWidth > 0) {
                ImGui.SameLine();

                if (ImGui.BeginChild("bank-worn", new Vector2(wornWidth, 0), true)) {
                    RenderWorn();
                }
                ImGui.EndChild();
            }
        }

        /// <summary>
        /// What you're wearing, laid out as the game's equipment panel (EquipSlots): each place a square, with what's worn
        /// there. Hover one for its details, right-click it to appraise it, or drag it onto a pack to take it off.
        /// </summary>
        private void RenderWorn() {
            Widgets.TextDisabled("Worn");
            Widgets.Tip("What you're wearing, where you're wearing it. Hover an item for its details, right-click it to appraise it, or drag it onto a pack to take it off.");

            ImGui.SameLine();

            if (ImGui.SmallButton("Hide##worn-hide")) {
                settings.Data.BankShowWorn = false;
                settings.Changed();
            }
            Widgets.Tip("Slides the worn items back in. The Worn checkbox above the packs brings them back.");

            var origin = ImGui.GetCursorScreenPos();
            var start = ImGui.GetCursorPos();
            var drawList = ImGui.GetWindowDrawList();
            var cell = GridIconSize;
            var wornBy = worn.Select(i => (unchecked((uint)i.Id), i.Worn)).ToList();

            foreach (var slot in EquipSlots.All) {
                var offset = new Vector2(slot.Column * (cell + WornGap), slot.Row * (cell + WornGap));
                ImGui.SetCursorPos(start + offset);
                ImGui.InvisibleButton($"worn-{slot.Name}", new Vector2(cell, cell));

                var at = origin + offset;
                var corner = at + new Vector2(cell, cell);
                var guid = EquipSlots.ItemIn(slot, wornBy);
                var item = guid == 0 ? null : worn.FirstOrDefault(i => unchecked((uint)i.Id) == guid);

                drawList.AddRectFilled(at, corner, ImGui.GetColorU32(ImGuiCol.FrameBg));

                if (item != null) {
                    icons.DrawAt(drawList, Icon(item), at, cell);
                    ItemIcons.DrawMark(drawList, item.Mark, at, cell);
                }
                else {
                    // the game's own picture of the empty slot
                    icons.DrawPlain(drawList, slot.EmptyIcon, at, cell);
                }

                drawList.AddRect(at, corner, ImGui.GetColorU32(ImGui.IsItemHovered() ? ImGuiCol.ButtonHovered : ImGuiCol.Border));
                EquipTarget();

                if (item == null) {
                    Widgets.Tip(slot.Name + ": nothing worn.");
                    continue;
                }

                DragSource(item);
                ShowTooltip(item, iconHovered: false);
                ItemMenu(item);
            }

            // past the last row, so the child's scroll height covers the paperdoll
            ImGui.SetCursorPos(start + new Vector2(0, EquipSlots.Rows * (cell + WornGap)));
            ImGui.Dummy(Vector2.Zero);
        }

        /// <summary>The bank's view, showing the packs you carry too.</summary>
        private bool ShowsBoth => !carried && settings.Data.BankShowCarried;

        private void RenderHolders() {
            // here, where the column keeps its width, so it's always there to turn the paperdoll off again
            if (ImGui.Checkbox("Worn", ref settings.Data.BankShowWorn)) {
                settings.Changed();
                snapshotAt = DateTime.MinValue;
            }
            Widgets.Tip("Slides out what you're wearing on the right of the window, laid out as the game's equipment panel; the window grows to make room. Drag a worn item onto a pack to take it off.");

            if (carried) {
                ImGui.Separator();
            }
            else {
                ImGui.SameLine();
            }

            if (!carried) {
                if (ImGui.Checkbox("Show my packs", ref settings.Data.BankShowCarried)) {
                    settings.Changed();
                    snapshotAt = DateTime.MinValue;
                }
                Widgets.Tip("Lists the packs you carry here too, above the bank's, so you can drag items between them.");
                ImGui.Separator();
            }

            bool? inBank = null;

            foreach (var holder in holders) {
                // a heading for each group, when both are shown
                if (ShowsBoth && inBank != holder.InBank) {
                    inBank = holder.InBank;
                    ImGui.Spacing();
                    Widgets.TextDisabled(holder.InBank ? "Bank" : "You carry");
                }

                icons.Draw(holder.Icon);

                if (ImGui.Selectable($"{holder.Label}##holder-{holder.Id}", holder.Id == selected)) {
                    selected = holder.Id;
                    search = "";
                }

                Widgets.Tip(holder.Detail);
                DropTarget(holder.Id);
            }
        }

        private void RenderItems() {
            var switchWidth = ImGui.CalcTextSize("List").X + ImGui.CalcTextSize("Grid").X + ImGui.CalcTextSize("Loot marks").X
                + ImGui.GetFrameHeight() * 3 + ImGui.GetStyle().ItemInnerSpacing.X * 3 + ImGui.GetStyle().ItemSpacing.X * 3;

            ImGui.SetNextItemWidth(Math.Max(100, ImGui.GetContentRegionAvail().X - switchWidth));
            ImGui.InputTextWithHint("##bank-search", carried ? "Find in your packs" : ShowsBoth ? "Find in the bank and your packs" : "Find in the bank", ref search, 100);
            Widgets.Tip("Lists every item whose name has this, in every pack listed on the left.");

            RenderViewSwitch();

            var holder = holders.FirstOrDefault(h => h.Id == selected);
            var searching = search.Trim().Length > 0;

            if (!searching && holder?.PackNumber > 0) {
                RenderInscribe(holder);
            }

            var items = searching
                ? contents.Values.SelectMany(list => list).Where(i => Matches(i, search)).OrderBy(i => i.Name).ToList()
                : contents.TryGetValue(selected, out var list) ? list : new List<Item>();

            if (items.Count == 0) {
                Widgets.TextDisabled(searching ? "Nothing by that name." : "Empty.");
                return;
            }

            if (settings.Data.BankGrid) {
                RenderGrid(items);
            }
            else {
                RenderList(items, searching);
            }
        }

        private void RenderViewSwitch() {
            ImGui.SameLine();

            if (ImGui.RadioButton("List", !settings.Data.BankGrid)) {
                settings.Data.BankGrid = false;
                settings.Changed();
            }
            Widgets.Tip("Each item on a line, with its value and burden.");

            ImGui.SameLine();

            if (ImGui.RadioButton("Grid", settings.Data.BankGrid)) {
                settings.Data.BankGrid = true;
                settings.Changed();
            }
            Widgets.Tip("Only the items' icons, side by side, to see many at once. Hover one for its details.");

            ImGui.SameLine();

            if (ImGui.Checkbox("Loot marks", ref settings.Data.BankLootMarks)) {
                settings.Changed();
                snapshotAt = DateTime.MinValue;
            }
            Widgets.Tip("Marks the items your active loot profile's rules would mark, with each rule's highlight and icon (set in the Loot tab). The server describes every item listed to check them (@playerui describe), so a full bank takes a little while to fill in.");

        }

        /// <summary>
        /// The items as icons side by side, as many to a row as fit. Each works like a line of the list: hover it for its
        /// details, drag it onto a pack, or right-click it. A stack shows its size in the corner.
        /// </summary>
        private void RenderGrid(List<Item> items) {
            if (ImGui.BeginChild("bank-items-grid", Vector2.Zero, false)) {
                // the game's own icon size: portal.dat icons are 32x32, drawn pixel for pixel
                var cell = GridIconSize;
                var spacing = ImGui.GetStyle().ItemSpacing.X;
                var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + spacing) / (cell + spacing)));
                var drawList = ImGui.GetWindowDrawList();

                for (var i = 0; i < items.Count; i++) {
                    var item = items[i];

                    if (i % columns != 0) {
                        ImGui.SameLine();
                    }

                    // not a Selectable: that widens its highlight over half the gap round it, so it sat off the icon
                    ImGui.InvisibleButton($"grid-item-{item.Id}", new Vector2(cell, cell));
                    var at = ImGui.GetItemRectMin();
                    var corner = ImGui.GetItemRectMax();

                    if (!icons.DrawAt(drawList, Icon(item), at, cell)) {
                        // no icon (yet): the start of its name, so the square isn't blank
                        var initials = item.Name.Length > 3 ? item.Name.Substring(0, 3) : item.Name;
                        drawList.AddRect(at, corner, ImGui.GetColorU32(ImGuiCol.Border));
                        drawList.AddText(at + new Vector2(3, 3), ImGui.GetColorU32(ImGuiCol.TextDisabled), initials);
                    }

                    ItemIcons.DrawMark(drawList, item.Mark, at, cell);

                    if (item.Stack > 1) {
                        ItemIcons.StackCount(drawList, item.Stack, at, cell);
                    }

                    // a frame round the icon under the mouse; its tile is solid, so a fill behind it wouldn't show
                    if (ImGui.IsItemHovered()) {
                        drawList.AddRect(at, corner, ImGui.GetColorU32(ImGuiCol.ButtonHovered), 0, ImDrawFlags.None, 2);
                    }

                    DragSource(item);
                    ShowTooltip(item, iconHovered: false);
                    ItemMenu(item);
                }
            }
            ImGui.EndChild();
        }

        private void RenderList(List<Item> items, bool searching) {
            var flags = Widgets.TableFlags | ImGuiTableFlags.ScrollY;

            if (!ImGui.BeginTable("bank-items-table", searching ? 4 : 3, flags)) {
                return;
            }

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Burden", ImGuiTableColumnFlags.WidthFixed, 55);
            if (searching) {
                ImGui.TableSetupColumn("Where", ImGuiTableColumnFlags.WidthFixed, 120);
            }
            Widgets.HeadersRow(
                "Drag an item onto a pack on the left to move it there. Right-click it for the game's appraisal and a menu to move it. Hover it for its details.",
                "What a vendor would pay for it, in pyreals.",
                "How heavy it is.",
                "Which pack it's in.");

            foreach (var item in items) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var iconHovered = icons.Draw(Icon(item), item.Mark);
                ImGui.Selectable(item.Stack > 1 ? $"{item.Name} ({item.Stack:N0})##item-{item.Id}" : $"{item.Name}##item-{item.Id}");
                DragSource(item);
                ShowTooltip(item, iconHovered);
                ItemMenu(item);

                ImGui.TableNextColumn();
                Widgets.TextDisabled(item.Value.ToString("N0"));
                ImGui.TableNextColumn();
                Widgets.TextDisabled(item.Burden.ToString("N0"));

                if (searching) {
                    ImGui.TableNextColumn();
                    Widgets.TextDisabled(holders.FirstOrDefault(h => h.Id == item.Container)?.Label ?? "");
                }
            }

            ImGui.EndTable();
        }

        /// <summary>
        /// The box to change what a bank pack collects, which sends /bank inscribe with its number.
        /// </summary>
        private void RenderInscribe(Holder holder) {
            if (inscriptionFor != holder.Id) {
                inscriptionFor = holder.Id;
                inscription = holder.Inscription ?? "";
            }

            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##bank-inscription", "weapons, swords t6...", ref inscription, 100);
            ImGui.SameLine();

            if (ImGui.Button("Tag pack")) {
                feed.SendCommand(BankCommands.Inscribe(holder.PackNumber, inscription));
            }
            Widgets.Tip($"Inscribes this pack with what it should collect, for deposits, withdrawals and sorts: category, type and tier words. Sends {BankCommands.Inscribe(holder.PackNumber, "...")}. Empty clears it.");
        }

        private void DragSource(Item item) {
            if (!ImGui.BeginDragDropSource()) {
                return;
            }

            // the payload itself carries nothing: which item is being dragged is kept here
            dragged = item.Id;
            ImGui.SetDragDropPayload(DragType, IntPtr.Zero, 0);

            // what follows the mouse: the item's icon, the grid's size, with its stack in the corner, and its name
            var size = GridIconSize;
            var at = ImGui.GetCursorScreenPos();
            var top = ImGui.GetCursorPosY();
            var drawList = ImGui.GetWindowDrawList();

            if (icons.DrawAt(drawList, Icon(item), at, size) && item.Stack > 1) {
                ItemIcons.StackCount(drawList, item.Stack, at, size);
            }

            ImGui.Dummy(new Vector2(size, size));
            ImGui.SameLine();
            ImGui.SetCursorPosY(top + (size - ImGui.GetTextLineHeight()) / 2);
            Widgets.Text(item.Stack > 1 ? $"{item.Name} ({item.Stack:N0})" : item.Name);

            ImGui.EndDragDropSource();
        }

        /// <summary>
        /// A paperdoll square as a place to drop an item: it's worn, with the game's own use (as double-clicking it in the
        /// game does), which puts it where it goes and takes off whatever was there. The server checks it can be worn.
        /// </summary>
        private unsafe void EquipTarget() {
            if (!ImGui.BeginDragDropTarget()) {
                return;
            }

            if (ImGui.AcceptDragDropPayload(DragType).NativePtr != null && dragged != 0) {
                var item = CoreManager.Current.WorldFilter[dragged];

                // one already worn is left where it is
                if (item != null && item.Values(LongValueKey.EquippedSlots, 0) == 0) {
                    CoreManager.Current.Actions.UseItem(dragged, 0);
                    snapshotAt = DateTime.MinValue;
                }

                dragged = 0;
            }

            ImGui.EndDragDropTarget();
        }

        private unsafe void DropTarget(int containerId) {
            if (!ImGui.BeginDragDropTarget()) {
                return;
            }

            if (ImGui.AcceptDragDropPayload(DragType).NativePtr != null && dragged != 0) {
                Move(dragged, containerId);
                dragged = 0;
            }

            ImGui.EndDragDropTarget();
        }

        /// <summary>Right-clicking an item: appraising it, and where to move it.</summary>
        private void ItemMenu(Item item) {
            if (!ImGui.BeginPopupContextItem($"item-menu-{item.Id}")) {
                return;
            }

            Appraisal.MenuItems(unchecked((uint)item.Id));
            ImGui.Separator();

            // the packs you carry can be looked at anywhere, but only put in the bank while it's open
            var bankGuid = feed.Bank?.Open == true && feed.Bank.Loaded ? unchecked((int)feed.Bank.Guid) : 0;
            var inBank = holders.Any(h => h.InBank && h.Id == item.Container);

            if (inBank && ImGui.MenuItem("Take out")) {
                Move(item.Id, CoreManager.Current.CharacterFilter.Id);
            }

            if (!inBank && bankGuid != 0 && ImGui.MenuItem("Put in the bank")) {
                Move(item.Id, bankGuid);
            }

            if (ImGui.BeginMenu("Move to")) {
                foreach (var holder in holders) {
                    if (holder.Id != item.Container && ImGui.MenuItem($"{holder.Label}##move-{holder.Id}")) {
                        Move(item.Id, holder.Id);
                    }
                }

                ImGui.EndMenu();
            }

            ImGui.EndPopup();
        }

        /// <summary>
        /// The item's every stat, as the server describes it (@playerui describe, asked for the first time the mouse is over
        /// it; the client's own appraisal has too few). Until the answer comes, what the client was sent with it.
        /// </summary>
        private void ShowTooltip(Item item, bool iconHovered) {
            if (!ImGui.IsItemHovered() && !iconHovered) {
                return;
            }

            var guid = unchecked((uint)item.Id);
            feed.Describe(guid);

            if (feed.Described.TryGetValue(guid, out var described)) {
                ItemTooltip.Show(ItemLines.Build(described), iconHovered);
                return;
            }

            ItemTooltip.Show(new List<ItemLine> {
                new ItemLine(item.Name, ItemLineStyle.Title),
                new ItemLine($"Value: {item.Value:N0}   Burden: {item.Burden:N0}{(item.Stack > 1 ? $"   Stack: {item.Stack:N0}" : "")}", ItemLineStyle.Detail),
                new ItemLine("Getting the details...", ItemLineStyle.Detail),
            }, iconHovered);
        }

        /// <summary>
        /// The server's icon for the item once it's described it (it knows a sigil's colour), the client's until then.
        /// </summary>
        private IconLayers Icon(Item item) {
            return feed.Described.TryGetValue(unchecked((uint)item.Id), out var described) && described.Item?.IconHex != null
                ? IconLayers.Of(described.Item)
                : item.Icon;
        }

        private void Move(int itemId, int containerId) {
            if (itemId == 0 || containerId == 0) {
                return;
            }

            // dropped back on the pack it's already in (from that pack's own items): nothing to do, unless it's worn
            var item = CoreManager.Current.WorldFilter[itemId];

            if (item != null && item.Values(LongValueKey.Container, 0) == containerId && item.Values(LongValueKey.EquippedSlots, 0) == 0) {
                return;
            }

            // stack: true merges a stackable item into a matching stack in the pack, as dropping it in the game does
            CoreManager.Current.Actions.MoveItem(itemId, containerId, 0, true);
            snapshotAt = DateTime.MinValue;
        }

        private static bool Matches(Item item, string text) {
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return words.All(w => item.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Reads the bank's, or your packs', items from Decal, every half second.
        /// </summary>
        private void Snapshot(BankInfo bank) {
            if (DateTime.UtcNow - snapshotAt < SnapshotInterval) {
                return;
            }

            snapshotAt = DateTime.UtcNow;
            holders.Clear();
            contents.Clear();

            var world = CoreManager.Current.WorldFilter;

            // every object by the container it says it's in, read off the object itself: WorldFilter.GetByContainer
            // missed items moved into a bank pack after the bank was opened (a /bank deposit), though the client had them
            var all = world.GetAll().Cast<WorldObject>().ToList();
            var byContainer = all.ToLookup(o => o.Values(LongValueKey.Container, 0));

            if (settings.Data.BankShowWorn) {
                SnapshotWorn(all);
            }

            // your packs first, then the bank's, when the bank's view shows both
            if (carried || ShowsBoth) {
                SnapshotCarried(byContainer);
            }

            if (!carried && bank != null) {
                SnapshotBank(bank, world, byContainer);
            }

            if (settings.Data.BankLootMarks) {
                MarkItems();
            }

            // starts on the bank in the bank's view, even with your packs listed above it
            if (holders.Count > 0 && holders.All(h => h.Id != selected)) {
                selected = (holders.FirstOrDefault(h => h.InBank != carried) ?? holders[0]).Id;
            }
        }

        /// <summary>
        /// Marks each item the way the active loot list does: the rule that decides it, if that rule has a highlight or an
        /// icon. Matching needs the server's description, so each item is asked about once (PlayerFeed.Describe), and is
        /// marked from the next snapshot after its description comes back.
        /// </summary>
        private void MarkItems() {
            var list = settings.Data.Loot.ActiveFor(CoreManager.Current.CharacterFilter.Name);

            foreach (var item in contents.Values.SelectMany(items => items)) {
                var guid = unchecked((uint)item.Id);

                if (!feed.Described.TryGetValue(guid, out var described)) {
                    feed.Describe(guid);
                    continue;
                }

                var rule = Loot.Decide(list, described).Rule;
                item.Mark = LootMarks.Marks(rule) ? rule : null;
            }
        }

        private void SnapshotBank(BankInfo bank, WorldFilter world, ILookup<int, WorldObject> byContainer) {
            var bankId = unchecked((int)bank.Guid);

            holders.Add(new Holder { Id = bankId, Label = $"Bank  {bank.Used}/{bank.Capacity}", Detail = "Your bank's own slots.", InBank = true });
            contents[bankId] = Items(byContainer[bankId], skipPacks: true);

            foreach (var pack in bank.Packs ?? new List<BankPackInfo>()) {
                var id = unchecked((int)pack.Guid);
                holders.Add(new Holder {
                    Id = id,
                    Label = $"{pack.Number}. {pack.Name}{Tags(pack.Tags)}  {pack.Used}/{pack.Capacity}",
                    Detail = string.IsNullOrEmpty(pack.Tags) ? "" : pack.Tags,
                    InBank = true,
                    PackNumber = pack.Number,
                    Inscription = pack.Inscription,
                    Icon = ItemIcons.Of(world[id]),
                });
                contents[id] = Items(byContainer[id], skipPacks: false);
            }
        }

        /// <summary>Your main pack and the packs in it, read the same way: GetInventory can still list what's been banked.</summary>
        private void SnapshotCarried(ILookup<int, WorldObject> byContainer) {
            var me = CoreManager.Current.CharacterFilter.Id;

            holders.Add(new Holder { Id = me, Label = "Main pack", Detail = "The pack you carry yourself." });
            contents[me] = Items(byContainer[me].Where(i => i.Values(LongValueKey.EquippedSlots, 0) == 0), skipPacks: true);

            foreach (var pack in byContainer[me].Where(i => i.ObjectClass == ObjectClass.Container).OrderBy(i => i.Values(LongValueKey.Slot, 0))) {
                var inscription = pack.Values(StringValueKey.Inscription, "");
                holders.Add(new Holder { Id = pack.Id, Label = pack.Name + Tags(inscription), Detail = inscription, Icon = ItemIcons.Of(pack) });
                contents[pack.Id] = Items(byContainer[pack.Id], skipPacks: false);
            }
        }

        /// <summary>A pack's tags as they follow its name: " (weapons, swords t6)", or nothing.</summary>
        private static string Tags(string tags) {
            return string.IsNullOrWhiteSpace(tags) ? "" : $" ({tags.Trim()})";
        }

        /// <summary>
        /// What you're wearing: everything with an equip location that you wield (or that's in your main pack, as the client
        /// sometimes has a worn item).
        /// </summary>
        private void SnapshotWorn(List<WorldObject> all) {
            var me = CoreManager.Current.CharacterFilter.Id;

            worn.Clear();
            worn.AddRange(Items(all.Where(o => o.Values(LongValueKey.EquippedSlots, 0) != 0
                && (o.Values(LongValueKey.Wielder, 0) == me || o.Values(LongValueKey.Container, 0) == me)), skipPacks: false));

            foreach (var item in worn) {
                item.Worn = unchecked((uint)CoreManager.Current.WorldFilter[item.Id].Values(LongValueKey.EquippedSlots, 0));
            }

            if (settings.Data.BankLootMarks) {
                var list = settings.Data.Loot.ActiveFor(CoreManager.Current.CharacterFilter.Name);

                foreach (var item in worn) {
                    var guid = unchecked((uint)item.Id);

                    if (feed.Described.TryGetValue(guid, out var described)) {
                        var rule = Loot.Decide(list, described).Rule;
                        item.Mark = LootMarks.Marks(rule) ? rule : null;
                    }
                    else {
                        feed.Describe(guid);
                    }
                }
            }
        }

        private static List<Item> Items(IEnumerable<WorldObject> objects, bool skipPacks) {
            return objects
                .Where(o => !skipPacks || o.ObjectClass != ObjectClass.Container)
                .Select(o => new Item {
                    Id = o.Id,
                    Name = o.Name ?? "",
                    Stack = o.Values(LongValueKey.StackCount, 1),
                    Value = o.Values(LongValueKey.Value, 0),
                    Burden = o.Values(LongValueKey.Burden, 0),
                    Slot = o.Values(LongValueKey.Slot, 0),
                    Container = o.Values(LongValueKey.Container, 0),
                    Icon = ItemIcons.Of(o),
                })
                .OrderBy(i => i.Slot)
                .ToList();
        }
    }
}
