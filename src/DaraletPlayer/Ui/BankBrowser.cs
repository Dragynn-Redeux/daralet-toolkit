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
    /// The bank as a window of its own: your bank's packs and the packs you carry on the left, what's in the chosen one on the
    /// right. Drag an item onto a pack, or right-click it, to move it; hover it for its details. The items are the ones the
    /// client was sent when the bank opened (Decal's WorldFilter); the server says which container is the bank and numbers its
    /// packs (@playerui bank). Moving is the game's own move, so the server checks it like a drag in the game's bank window.
    /// </summary>
    internal class BankBrowser {
        private const string DragType = "DAP_ITEM";

        /// <summary>The items are read from Decal this often, not every frame: a bank can hold a thousand of them.</summary>
        private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMilliseconds(500);

        private readonly PlayerFeed feed;

        private readonly List<Holder> holders = new List<Holder>();
        private readonly Dictionary<int, List<Item>> contents = new Dictionary<int, List<Item>>();
        private DateTime snapshotAt = DateTime.MinValue;

        private int selected;
        private string search = "";
        private string inscription = "";
        private int inscriptionFor;
        private int dragged;

        public BankBrowser(PlayerFeed feed) {
            this.feed = feed;
        }

        /// <summary>A container on the left: the bank, one of its packs, your main pack or one of your packs.</summary>
        private class Holder {
            public int Id;
            public string Label;
            public string Detail;
            public bool InBank;
            /// <summary>The bank pack's number in /bank packs, or 0.</summary>
            public int PackNumber;
            public string Inscription;
        }

        private class Item {
            public int Id;
            public string Name;
            public int Stack;
            public int Value;
            public int Burden;
            public int Slot;
            public int Container;
        }

        public void Render(BankInfo bank) {
            Snapshot(bank);

            var size = ImGui.GetContentRegionAvail();

            if (ImGui.BeginChild("bank-packs", new Vector2(Math.Min(260, size.X * 0.38f), 0), true)) {
                RenderHolders();
            }
            ImGui.EndChild();

            ImGui.SameLine();

            if (ImGui.BeginChild("bank-items", new Vector2(0, 0), true)) {
                RenderItems();
            }
            ImGui.EndChild();
        }

        private void RenderHolders() {
            var inBank = true;

            foreach (var holder in holders) {
                if (inBank && !holder.InBank) {
                    inBank = false;
                    ImGui.Spacing();
                    ImGui.TextDisabled("You carry");
                    ImGui.Separator();
                }

                if (ImGui.Selectable($"{holder.Label}##holder-{holder.Id}", holder.Id == selected)) {
                    selected = holder.Id;
                    search = "";
                }

                Widgets.Tip(holder.Detail);
                DropTarget(holder.Id);
            }
        }

        private void RenderItems() {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##bank-search", "Find in the bank and your packs", ref search, 100);
            Widgets.Tip("Lists every item whose name has this, in the bank and in your packs.");

            var holder = holders.FirstOrDefault(h => h.Id == selected);
            var searching = search.Trim().Length > 0;

            if (!searching && holder?.PackNumber > 0) {
                RenderInscribe(holder);
            }

            var items = searching
                ? contents.Values.SelectMany(list => list).Where(i => Matches(i, search)).OrderBy(i => i.Name).ToList()
                : contents.TryGetValue(selected, out var list) ? list : new List<Item>();

            if (items.Count == 0) {
                ImGui.TextDisabled(searching ? "Nothing by that name." : "Empty.");
                return;
            }

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
                "Drag an item onto a pack on the left to move it there, or right-click it. Hover it for its details.",
                "What a vendor would pay for it, in pyreals.",
                "How heavy it is.",
                "Which pack it's in.");

            foreach (var item in items) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.Selectable(item.Stack > 1 ? $"{item.Name} ({item.Stack:N0})##item-{item.Id}" : $"{item.Name}##item-{item.Id}");
                DragSource(item);
                ShowTooltip(item);
                ItemMenu(item);

                ImGui.TableNextColumn();
                ImGui.TextDisabled(item.Value.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.TextDisabled(item.Burden.ToString("N0"));

                if (searching) {
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(holders.FirstOrDefault(h => h.Id == item.Container)?.Label ?? "");
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
            ImGui.Text(item.Stack > 1 ? $"{item.Name} ({item.Stack:N0})" : item.Name);
            ImGui.EndDragDropSource();
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

        private void ItemMenu(Item item) {
            if (!ImGui.BeginPopupContextItem($"item-menu-{item.Id}")) {
                return;
            }

            var bankGuid = unchecked((int)(feed.Bank?.Guid ?? 0));
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

            if (ImGui.MenuItem("Select")) {
                CoreManager.Current.Actions.SelectItem(item.Id);
            }

            ImGui.EndPopup();
        }

        /// <summary>
        /// The item's every stat, as the server describes it (@playerui describe, asked for the first time the mouse is over
        /// it; the client's own appraisal has too few). Until the answer comes, what the client was sent with it.
        /// </summary>
        private void ShowTooltip(Item item) {
            if (!ImGui.IsItemHovered()) {
                return;
            }

            var guid = unchecked((uint)item.Id);
            feed.Describe(guid);

            if (feed.Described.TryGetValue(guid, out var described)) {
                ItemTooltip.Show(ItemLines.Build(described));
                return;
            }

            ItemTooltip.Show(new List<ItemLine> {
                new ItemLine(item.Name, ItemLineStyle.Title),
                new ItemLine($"Value {item.Value:N0}   Burden {item.Burden:N0}{(item.Stack > 1 ? $"   Stack {item.Stack:N0}" : "")}", ItemLineStyle.Detail),
                new ItemLine("Getting the details...", ItemLineStyle.Detail),
            });
        }

        private void Move(int itemId, int containerId) {
            if (itemId == 0 || containerId == 0) {
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
        /// Reads the bank's and your packs' items from Decal, every half second.
        /// </summary>
        private void Snapshot(BankInfo bank) {
            if (DateTime.UtcNow - snapshotAt < SnapshotInterval) {
                return;
            }

            snapshotAt = DateTime.UtcNow;
            holders.Clear();
            contents.Clear();

            var world = CoreManager.Current.WorldFilter;
            var bankId = unchecked((int)bank.Guid);

            holders.Add(new Holder { Id = bankId, Label = $"Bank  {bank.Used}/{bank.Capacity}", Detail = "Your bank's own slots.", InBank = true });
            contents[bankId] = Items(world.GetByContainer(bankId).Cast<WorldObject>(), skipPacks: true);

            foreach (var pack in bank.Packs ?? new List<BankPackInfo>()) {
                var id = unchecked((int)pack.Guid);
                holders.Add(new Holder {
                    Id = id,
                    Label = $"{pack.Number}. {pack.Name}{Tags(pack.Tags)}  {pack.Used}/{pack.Capacity}",
                    Detail = string.IsNullOrEmpty(pack.Tags) ? "" : pack.Tags,
                    InBank = true,
                    PackNumber = pack.Number,
                    Inscription = pack.Inscription,
                });
                contents[id] = Items(world.GetByContainer(id).Cast<WorldObject>(), skipPacks: false);
            }

            var me = CoreManager.Current.CharacterFilter.Id;
            var carried = world.GetInventory().Cast<WorldObject>().ToList();

            holders.Add(new Holder { Id = me, Label = "Main pack", Detail = "The pack you carry yourself." });
            contents[me] = Items(carried.Where(i => i.Values(LongValueKey.Container, 0) == me && i.Values(LongValueKey.EquippedSlots, 0) == 0), skipPacks: true);

            foreach (var pack in carried.Where(i => i.ObjectClass == ObjectClass.Container && i.Values(LongValueKey.Container, 0) == me)
                         .OrderBy(i => i.Values(LongValueKey.Slot, 0))) {
                var inscription = pack.Values(StringValueKey.Inscription, "");
                holders.Add(new Holder { Id = pack.Id, Label = pack.Name + Tags(inscription), Detail = inscription });
                contents[pack.Id] = Items(carried.Where(i => i.Values(LongValueKey.Container, 0) == pack.Id), skipPacks: false);
            }

            if (holders.All(h => h.Id != selected)) {
                selected = bankId;
            }
        }

        /// <summary>A pack's tags as they follow its name: " (weapons, swords t6)", or nothing.</summary>
        private static string Tags(string tags) {
            return string.IsNullOrWhiteSpace(tags) ? "" : $" ({tags.Trim()})";
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
                })
                .OrderBy(i => i.Slot)
                .ToList();
        }
    }
}
