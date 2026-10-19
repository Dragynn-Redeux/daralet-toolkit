# Working on this plugin

Daralet (the player plugin: `Daralet.dll`, `DaraletPlayer` as its namespace in code, plain "Daralet" wherever players see
a name) is a Decal plugin for Asheron's Call, for players on the Daralet server (daralet-ACE). Its sibling, Daralet Admin
(`../daralet-admin-plugin`), is the staff plugin this one was started from: when something here is unclear, see how it's
done there. Until v0.1.0 the dll was `DaraletPlayer.dll`; the assembly guid (what Decal knows it by) didn't change. This file is the knowledge that building them took: copy it into any new Daralet Decal plugin and keep it
up to date.

## The stack

- SDK-style project, `net48`, `PlatformTarget` x86, `LangVersion` 12. No C# records: net48 has no `IsExternalInit`.
- UI: [UtilityBelt.Service](https://gitlab.com/utilitybelt/utilitybelt.service) 2.2.12 from NuGet, with
  `ExcludeAssets="runtime; build; contentFiles"`, because it's installed on the client by UtilityBelt. Windows are
  `UBService.Huds.CreateHud(name)` with `OnRender`, drawn with ImGui (`ImGuiNET`). Images use `ManagedTexture`, made
  lazily the first time it's drawn, once the game's graphics device exists.
- `deps/` holds `Decal.Adapter.dll` and `Decal.Interop.Core.dll` from Decal 2.9.8.3, referenced with `Private=False`.
- `Microsoft.DirectX.Direct3D` is referenced from `$(WINDIR)\Microsoft.NET\DirectX for Managed Code\1.0.2902.0\` only so the
  compiler can tell `ManagedTexture`'s constructors apart. UtilityBelt installs it.
- MSB3277 is suppressed: UtilityBelt.Service was built against Decal.Adapter 2.9.8.0 and Decal installs 2.9.7.5 (assembly
  version). Only the installed one is ever loaded.
- Based on UtilityBelt's [HotDecalPluginTemplate](https://gitlab.com/utilitybelt/HotDecalPluginTemplate). The loader
  (`src/DaraletPlayer.Loader`, built as `Daralet.Loader.dll`) is a Decal network filter that loads `Daralet.dll` from bytes
  and reloads it on every rebuild.

Build and test:

```bash
dotnet build DaraletPlayer.sln -c Release
dotnet test tests/DaraletPlayer.Tests/DaraletPlayer.Tests.csproj
```

## Gotchas, each found the hard way

- **Decal must be 2.9.8.x.** Decal 3.0 (2.9.7.5) runs plugins on .NET 2.0 and says "Failed to register plugin" for a net48
  dll. The agent's title bar shows the version.
- **Decal registers a .NET plugin by the assembly's `[Guid]`**, under `HKLM\SOFTWARE\WOW6432Node\Decal\Plugins\{guid}`, with
  `Object` = the plugin class's full name. Give every plugin (and loader) its own guid in `Properties/AssemblyInfo.cs`:
  never reuse Daralet Admin's, or the two can't be installed side by side.
- **A hot-reloaded plugin isn't registered with Decal**, so `PluginBase.Host`, `Core` and the base events
  (`ServerDispatch`...) never work for it. Use `CoreManager.Current` everywhere, and
  `CoreManager.Current.EchoFilter.ServerDispatch` for raw network messages. `CharacterFilter.LoginComplete` has already
  fired when a reload starts, so check `CharacterFilter.LoginStatus == 3` instead.
- **A plugin running in the game locks `bin\*.dll`** (acclient and DenAgent). To check a build while it runs, build to
  another folder (`-p:OutputPath=...`), or develop with the loader.
- **A local ACE server locks daralet-ACE's `dist\apps\server`**, so `dotnet test` there fails to copy `ACE.Server.exe`.
  Build the tests somewhere else instead: `-p:BaseOutputPath=<dir>/ -p:OutputPath=<dir>/`. Built that way, the
  `StartupTests` and physics tests fail for want of `Config.js` and the dat files; the rest are meaningful.
- **Piping `dotnet build`/`dotnet test` into `grep` can hang forever**: MSBuild's reused worker nodes keep the pipe open
  after the build ends. Pass `-nodeReuse:false` (and set `MSBUILDDISABLENODEREUSE=1`), or write to a log file and grep that.
- **Unit tests (net48 MSTest) can't load Decal.Adapter.** Keep Decal types out of anything tested: for example, pass
  logging in as `Action<string>` instead of calling `PluginCore.Log`.
- **`ImGui.GetColorU32` crashes before an ImGui context exists**, so colours in static fields are packed by hand
  (`(a << 24) | (b << 16) | (g << 8) | r`).
- **Modal dialogs opened from a right-click menu** must be opened (`OpenPopup`) at the window level on the next frame, not
  inside the menu: popups are found by ID stack. See `PlayerActions.RenderDialog` / `CommandConfirm` in Daralet Admin.
- **A hot reload must leave the old copy inert.** The loader shuts the running plugin down and starts the new build in the
  same game. If any old window kept drawing, it kept asking the server for data while its chat filter was gone, and the
  `DAP1|` replies flooded the chat box. So shutdown disposes each piece on its own (`PluginCore.ShutdownStep`,
  `PlayerHud.Dispose`), and a disposed feed or window does nothing (`PlayerFeed.Request`, `FloatingWindow.Hud_OnRender`).
- **Every Decal event handler and render callback is wrapped in try/catch.** An exception escaping into Decal can crash the
  client.
- **JSON:** System.Text.Json writes dictionaries as objects; `DataContractJsonSerializer` only reads them with
  `UseSimpleDictionaryFormat = true` (see `Feed/Json.cs`).

## What the client already gets, and what it doesn't

Read from daralet-ACE; check before relying on it.

- **Fellowship vitals only reach a client whose fellowship panel is open** (`Fellowship.OnVitalUpdate` checks
  `FellowshipPanelOpen`), so the plugin gets them from `@playerui fellowship` instead, once a second.
- **A target's health** comes back when the client asks for it (`QueryHealth`), as a percentage. Its stamina, mana, buffs
  and debuffs are never sent to the client: those need the server.
- **Threat** lives on the monster (`ThreatLevel`, `PositiveThreat`, `NegativeThreat` in `Monster_Awareness.cs`). Players can
  already see it in chat by appraising a monster with the "attempt to perceive threats" option on
  (`Player_Awareness.GetMonsterThreatTable`): a Perception roll against half the monster's Deception, at most once every
  10 seconds, and only as tiers ("Major threat"...), never numbers. The plugin deliberately doesn't follow those rules: it
  shows the exact `ThreatLevel` numbers, with no Perception roll, updated every second (the user's call, October 2026).
- **The bank** is a `Storage` container the player opens in the world. `/bank` (`Commands/PlayerCommands/BankCommand.cs`)
  does the rest: deposit, withdraw, sort, search, combine, balance, packs, inscribe, icon, autosort, log.
- **The market** is run by Market Broker NPCs (`apps/server/Market/MarketBroker.cs`): give one an item to list it, and talk
  to it (`listings`, `cancel <id>`, `payouts`, `claim expired`). Listings live in the database.
- **The Market API** (`apps/market-api`) is the public, read-only HTTP service the website uses for market search
  (`/api/market/listings`) and leaderboards (`/api/leaderboards/...`), at `https://play.daralet.ac/api/...` (nginx only
  passes `/api/` through, so its `/health` isn't reachable from outside). The plugin calls it directly over HTTPS for market
  search and leaderboards: off the game thread, with TLS 1.2 switched on (`ServicePointManager.SecurityProtocol`), since
  net48 doesn't always offer it by default. A player's own listings, sales and expired items aren't public, so those come
  from `@playerui`.
- **The arena** is `/arena` (`Commands/PlayerCommands/ArenaCommand.cs`): queue, challenge, leave, stats, top, maps, watch.
- **Map locations** come from GoArrow's `locations.xml`, in Phenyl's Daralet edition (its "About" entry says so): about 1,700
  `<loc id name type NS EW>` entries, in game coordinates (south and west negative). Types are Town, Dungeon, Landmark,
  Lifestone, Outpost, Bindstone, Vendor, NPC, AllegianceHall, TownPortal / WildernessPortal / UndergroundPortal (with
  `exitNS`/`exitEW`, where the portal goes) and Custom (notes, like the About entries in the corners). Dungeons have a
  `dungeonId`. The text inside an entry is a free note: "Level 30 Capstone" on capstone dungeons, a number on towns, a tier
  ("T2") on others. The website's `mapdata.js` is a smaller set taken from the same data, as image percentages
  (EW = x% × 2.038 − 101.9, NS = 101.9 − y% × 2.038). Daralet Admin's `Map/MapProjection.cs` and `Map/MapView.cs` draw the
  map, in the same game coordinates.

## Talking to the server

There's no custom network message: the client and Decal don't know new opcodes. Instead:

1. The plugin sends a chat command with `CoreManager.Current.Actions.InvokeChatParser("@playerui hello")`.
2. The server replies with system chat lines `DAP1|kind|id|index|count|chunk`: camelCase JSON, split into 200-character
   chunks (`PlayerUiProtocol.ChunkSize`; the same scheme as `AdminUiProtocol`, whose chunks are 4,000).
3. The plugin reads them from the raw `0xF7E0` message (`e.Message.Value<string>("text")`), joins the chunks
   (`Feed/ReplyAssembler.cs`), and hides them from chat with `CoreManager.Current.ChatBoxMessage` + `e.Eat = true`.

**Why 200:** a fellowship reply of about a thousand characters on one chat line, every second, reached the chat window
twice instead of being hidden, both times starting mid-reply (`lse,"members":...`), which looks like the client handling a
long line in pieces the plugin's "starts with `DAP1|`" check didn't catch. With short chunks every line is whole; the
plugin also hides any line containing `DAP1|`, and hides and logs once (to `log.txt` only) a line that looks like a piece of
a reply (`ReplyAssembler.LooksLikeReplyPiece`). If that log line ever appears, the cause is still there.

Server side, in daralet-ACE: `@playerui` is a Player-level command (`apps/server/Commands/PlayerCommands/PlayerUi.cs`, with
its code in `apps/server/Entity/PlayerUi/`). `PlayerUiProtocol` uses `AdminUiProtocol`'s chunking with its own prefix.
Don't reuse `@adminui`: it needs Sentinel, and it sends data about everyone. The JSON classes in `Feed/Models.cs` mirror the
records in `PlayerUiSnapshots.cs`; tests on both sides pin the names (`PlayerUiProtocolTests`, `ReplyAssemblerTests`).

The `hello` reply lists every request the server answers (`kinds`, from `PlayerUi.Kinds`). Add each new request there, and
have its tab check `PlayerFeed.Supports(kind)`, so a newer plugin on an older server says what's missing instead of waiting.

`/dap` (or `@dap`) shows and hides the window.

Requests so far: `hello`; `fellowship` (your fellowship with vitals and map places, or `null`); `position` (where you
are on the map, which the plugin only asks indoors); `targets <guid> [guid]` (up to four
creatures in your landblock or the ones around it: vitals, the spell in effect in each category, attack target and
threat table, or `"status":"gone"`). Another player as a target gets only `"status":"player"` and their name: the plugin
shows the health percentage the client already has (game event `0x01C0`), never their spells or exact vitals. The server
ignores a request of the same kind sooner than 400ms after the last (`PlayerUi.MinInterval`), and the plugin never asks
sooner than 500ms (`PlayerFeed.MinInterval`); polling goes through `PlayerFeed.Refresh`.

**Map places indoors:** the game's `GetMapCoords` gives nothing for any indoor cell (0x0100 and up), buildings included.
But a building's rooms share their landblock's coordinates, so the server places them anyway (`PlayerUiData.GetMapCoords`,
`inBuilding: true`); only a true dungeon (`Town.HasReliableNearestTown`, the check listplayers uses) has no place on the
map. The client can do the same sums (`Map/Navigation.MapCoords`) but can't tell a building from a dungeon, which is why
it asks `position` when you're indoors.

**The bank:** opening a bank chest sends the client every pack and item in it (`Storage.SendBankVaultInventory`), so
Decal's WorldFilter has them all, and moving one is the game's own move (`Actions.MoveItem`), which the server checks.
What the client can't tell is which open container is a bank: `@playerui bank` says (`open` means open and near enough to
use, like every `/bank` command but balance, log and intro needs) and numbers its packs the way `/bank packs` does.

**The market:** a Market Broker is driven by talking to it (`MarketBroker.HandleTalkDirect`: `listings`, `cancel <id>`,
`claim payouts`, `claim expired`, a price after you give it an item). `@playerui market` sends your listings, sales (last 30
days, from `GetSoldListingsForAccount`) and expired items, and whether a broker is within 30 meters
(`PlayerUiMarketData.BrokerRange`, found from what the player can see). `@playerui broker list|cancel|claim|expired` hands
the same words to that broker, so its checks and Yes/No confirmations apply; with no broker near it says so. Working out a
listing's item name loads the item from the database (`MarketBroker.TryGetListingItemName`), so names are cached by
listing id. The broker answers in tells ("Market Broker tells you, ..."), which `SendCommand` keeps as replies too.

**The arena:** `@playerui arena` (`Arena/ArenaManager.PlayerUi.cs`, a partial like the admin one) sends your state in one
word (off, idle, queued, watching, defeated, accepting, starting, fighting) and in words (`ArenaManager.Status`), your
ratings (`ArenaBoards.All`), duels to watch and the latest results. The buttons send the `/arena` commands themselves.

**Loot:** `@playerui corpse <guid>` describes a corpse's items (you have to be allowed to loot it, `Corpse.HasPermission`,
and near enough to use it); `@playerui describe <guid>...` does the same for up to 20 items you carry (the Loot tab's test);
`@playerui salvage <guid>...` salvages up to 20 carried items with the Ust in your packs, through `Player.HandleSalvaging`
(so pack space, retained and unstable items are checked as ever; `Player.IsSalvageable` is the per-item check both use).
Each item is described by the Market API's own code, shared as linked source: `apps/market-api/MarketRepository.Items.cs`
(`MarketRepository.Describe(IItemFacts)`), `ItemFacts.cs` and `MarketListing.cs` are compiled into the server too. The API
reads the facts from its query's row (`RowFacts`); the server from an item snapshot (`SnapshotItemFacts`, made in memory by
`MarketListingSnapshotSerializer.TryCreateLiveSnapshotJson`, since loot isn't in the database yet). `ItemColumns.Paths` says
where each column is in a snapshot, copied from the SQL; `SharedItemDescriptionTests` checks the two agree, so a column
added to the query needs a row there too. Tiers come from `MarketBroker.GetItemTier`, the market's own. Corpse replies
leave nulls out (`PlayerUiProtocol.BuildMessages(..., omitNulls: true)`): each item is a whole listing.

## The plugin's own pieces

- **The map image** is `Resources/dereth-map.png`, 2048x2048: 2040 pixels of Dereth (10 pixels a map unit, -102 to +102 both
  ways, north up) with 4 pixels of sea round it (`Map/MapProjection.cs`). It came from Daralet Admin's `tools/MapGen`, made
  from the server's dat files, then had some islands and artifacts painted out by hand; `Resources/dereth-map.PSD` is the
  Photoshop source for that edit (not built into the plugin). To change it, edit that file and rebuild. Or, to try an edit first, put a `dereth-map.png`
  next to the plugin dll: it's drawn instead (`MapView.OpenImage`), once the plugin next starts. It has to keep the same
  layout, or places land in the wrong spots, and be square (one that isn't is skipped, with a line in chat).
- **Item tooltips** (`Feed/ItemLines.cs`, `Ui/ItemTooltip.cs`): every stat an item's market description has, as lines in
  sections; the bank, the Loot tab, the Corpse window and market search all draw them, so the layout changes in one place.
  The bank asks for descriptions as the mouse passes over items (`PlayerFeed.Describe`, 20 a request; `@playerui describe`
  covers your packs and your open bank). Tiers show as T0-T7 everywhere, the way the Search tab's filter has them.
- **Loot tab** (`Ui/LootTab.cs`, `Ui/Looter.cs`, `Feed/Loot.cs`): named lists in `SettingsData.Loot`, each character
  picking one (`LootData.ActiveFor`). A rule is a category, tiers, name words and any of `MarketSearch.Filters`, plus an
  outcome (Keep, Salvage, Notify, Ignore); the first enabled rule that matches decides. `Loot.Matches` compares each filter
  with the same field the API's SQL does, per armor slot where the API is (ward level, armor and clothing mods), with
  "price" as the item's vendor value. A filter it doesn't know leaves the rule alone. The looter moves one item at a time
  (`Actions.MoveItem` into the main pack, the next once the last has left the corpse or after 3 seconds), and salvages after.
  It starts off; there's no way to tell from a plugin whether VirindiTank's looter is on, so the tab just warns.

- **Arena and Leaderboards tabs** (`Ui/ArenaTab.cs`, `Ui/LeaderboardsTab.cs`, `Feed/ArenaCommands.cs`,
  `Feed/Leaderboards.cs`): the leaderboards are the website's (`leaderboards.js`), read from `/api/leaderboards/...` with
  one `LeaderboardRow` for every board. A new board is a row in `Leaderboards.All`. One `MarketApi` (one `HttpClient`) is
  shared by the Market, Arena and Leaderboards tabs and made by `PlayerHud`.

- **Market tab** (`Ui/MarketTab.cs`, `Feed/MarketSearch.cs`, `Feed/MarketApi.cs`): My market, and Search over HTTP. The
  search filters are a table (`MarketSearch.Filters`) copied from the website's `market.js`: when the website gains a filter,
  add a row. Percent filters send a fraction (15 → 0.15), "percent of one" ones a multiplier (15 → 1.15), tiers go up by one
  (the API counts from 1), and Salvage has no tier. `ApiRequest<T>` runs a request off the game thread; the newest one wins.
- **The website's icons** are at `https://play.daralet.ac/images/<iconHex>.png` (13,000 of them, `daralet-website/images`),
  named by the portal.dat icon id, which is what the market listings' `iconHex` and Decal's `LongValueKey.Icon` give.

- **Bank tab** (`Ui/BankTab.cs`, `Ui/BankBrowser.cs`, `Feed/BankCommands.cs`): the bank browser and `/bank` buttons.
  `PlayerFeed.SendCommand` keeps the System and Help chat (types 5 and 15, Decal's `e.Color`) that comes back within three
  seconds as the command's reply. Item icons aren't drawn yet: UtilityBelt.Service only exposes an icon loader to its
  scripts (`ScriptHudManager.GetIconTexture`).

- **World tab** (`Ui/WorldTab.cs`, `Map/`): the map (Daralet Admin's `dereth-map.png`), the places in
  `Resources/locations.xml` (a `locations.xml` next to the dll wins), you, your fellows, and a destination arrow window.
  The file also has 43 `_Unknown` entries (shops, taverns, halls): every type needs a row in `Locations.Types`, which a
  test checks.

- **Floating windows** (`Ui/FloatingWindow.cs`): small auto-sized UB huds that stay on screen (fellowship vitals, target,
  tracked target). Locked means no title bar and no dragging; right-click one to lock, unlock or hide it.
- **Settings** (`Settings.cs`) live in `Documents\Asheron's Call\Daralet\settings.json` (copied once from the
  `Daralet Player` folder they had before the plugin was renamed, `Settings.CarryOver`), shared by every character:
  window positions and visibility, and display options. A DataContract reader skips field initialisers, so new settings
  get their defaults in `SetDefaults`, which runs before a file is read as well. Saved two seconds after the last change.

`@playerui` only ever sends a player data about themselves, their fellowship and what they're fighting (for a monster
they've selected: its vitals, every buff and debuff on it, and its threat table). The server is the
only thing that can be trusted: never let the plugin's request decide what it's allowed to see. Anything that changes the
game (banking, listing, queueing) goes through the existing player commands, which check everything again; the plugin's own
checks (greying out market buttons away from a broker, say) are only there to be helpful.

## Conventions

- Braces on every `if`, even one-line bodies.
- Code comments explain why, in plain words; tooltips (`Widgets.Tip`) on every control say what it does and which command it
  sends.
- The user commits and pushes with GitHub Desktop: leave changes staged (`git add`), don't commit. New branches with
  `git switch -c <name> --no-track origin/main`.

## Planned tabs (to talk through before building)

- **LFG:** looking for group, for leveling, capstones, raids, quests and PvP, with the same system available in the
  `/lfg` channel. The server has only the client's built-in LFG chat channel today (a Turbine chat room,
  `TurbineChatChannel`, heard with the `ListenToLFGChat` character option): plain chat, nothing structured behind it.
