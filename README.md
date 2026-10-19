# Daralet

Daralet is the official [Decal](https://www.decaldev.com/) plugin for players on the Daralet server. It opens a window in
the game with:

- **World**: a map of Dereth with Daralet's towns, dungeons, lifestones, portals and more. It shows where you are, where
  your fellowship is (unless they're in a dungeon), and an arrow window that points you anywhere on the map, or follows a
  fellow. Search for a place by name, or right-click the map.
- **Fellowship**: your fellowship's health, stamina and mana, with a small window that stays on screen while you play.
  Click a name to select that member.
- **Target**: what you're fighting: its health, stamina and mana, every buff and debuff on it, and its threat table (who
  it wants to attack). Has a window that stays on screen too.
- **Loot**: a looter with loot lists you write yourself, using the same filters as the market search. Each character picks
  a list, and you can swap lists at any time. When you open a corpse, it can loot automatically, ask first, or just point
  out what matches. It can also salvage what you tell it to.
- **Bank**: browse your bank and your packs, and deposit, withdraw, sort and search them without typing `/bank` commands.
- **Market**: your listings, sales and expired items, with buttons for the Market Broker (list, cancel, claim), plus a
  search of the whole market like the website's.
- **Arena**: queue, challenge, leave and watch duels, with your ratings and the latest results.
- **Leaderboards**: the website's leaderboards: level, arena, capstones, deaths and more.

Type `/dap` to open and close the window. Hover over almost anything for a tooltip saying what it does.

## What you need

- **Decal 2.9.8.3.** Older versions, including "Decal 3.0" (which reports itself as 2.9.7.5), can't run this plugin.
  The Decal agent's title bar shows which version you have.
- **[UtilityBelt](https://utilitybelt.gitlab.io/).** Daralet draws its window with UtilityBelt's interface, so
  UtilityBelt has to be installed, but you don't have to use UtilityBelt's own features.
- **A Daralet account.** The plugin only works on the Daralet server.

## Installing

1. **Update Decal if you need to.** Open the Decal agent (the icon in your system tray) and look at the version in its
   title bar. If it isn't 2.9.8.x, install [Decal 2.9.8.3](https://www.decaldev.com/) over it. Install the Visual C++ 2017
   (x86) runtime it links to as well. Your other plugins keep working.
2. **Install UtilityBelt** from [utilitybelt.gitlab.io](https://utilitybelt.gitlab.io/) if you don't have it. This also
   installs Managed DirectX and UtilityBelt.Service, which Daralet needs.
3. **Download `Daralet.dll`** from the
   [latest release](https://github.com/daralet-ac/daralet-player-plugin/releases/latest) (under **Assets**).
4. **Put it somewhere it can stay**, for example `C:\Games\Decal Plugins\Daralet\`. Decal loads the file from wherever you
   put it, so don't leave it in your Downloads folder.
5. **Add it to Decal.** In the Decal agent, click **Add**, then **Browse**, and pick `Daralet.dll`.
6. **Make sure Daralet is ticked** under **Plugins** in the Decal agent.
7. **Start the game** through Decal and log in. Type `/dap` to open the window.

## Updating

Download the new `Daralet.dll` from the
[latest release](https://github.com/daralet-ac/daralet-player-plugin/releases/latest). Close the game, replace the old
`Daralet.dll` with the new one (same name, same folder), then start the game again. You don't need to add it to Decal
again, and your settings are kept.

**Coming from v0.1.0?** That version's file was called `DaraletPlayer.dll`. Put `Daralet.dll` in the same folder, then
in the Decal agent click **Add**, **Browse**, and pick `Daralet.dll`: it replaces the old entry, since Decal knows them
as the same plugin. Then delete `DaraletPlayer.dll`. Your settings are kept.

## Your settings

Window positions, display options and loot lists are saved in

    Documents\Asheron's Call\Daralet\settings.json

and shared by every character on your computer. To start fresh, close the game and delete that file.

To move your loot lists to another computer, or share them with a friend, use **Export** on the Loot tab and **Import**
on the other end.

## Things to know

- **Loot:** the looter starts off. If you also use VirindiTank's looter, only turn one of them on, or they'll fight over
  the same corpse. Salvaging needs an Ust in your packs.
- **Market:** listing, cancelling and claiming only work when you're near a Market Broker (the market stand in
  Hebian-To). Searching works from anywhere. Market search and leaderboards come from the Daralet website, so they need
  an internet connection, the same as the website.
- **Map:** to use your own copy of the map image, put a square `dereth-map.png` next to `Daralet.dll`. It has to
  keep the original's layout. To use your own places, do the same with a GoArrow-style `locations.xml`.

## Problems

- **"Failed to register plugin" when adding the dll:** Decal is older than 2.9.8. See step 1 of Installing.
- **Daralet isn't in Decal's list, or isn't ticked:** add it again (step 5) and tick it.
- **Nothing happens when you type `/dap`:** check that UtilityBelt is installed, and that Daralet is ticked under
  **Plugins** in the Decal agent. Then restart the game.
- **A tab says the server is too old:** the server hasn't been updated for that part of the plugin yet. The rest still
  works.
- **Lines starting with `DAP1|` show up in chat:** those are messages from the server to the plugin, and the plugin
  normally hides them. Report it, and include `log.txt` from the folder `Daralet.dll` is in.
- **Anything else:** errors are written to `log.txt` in the same folder as `Daralet.dll`. Include that file when you
  report a problem.

## Uninstalling

In the Decal agent, select **Daralet** and click **Remove** (or just untick it). Then delete `Daralet.dll`, and
`Documents\Asheron's Call\Daralet\` if you don't want your settings any more.

## For developers

See [CLAUDE.md](CLAUDE.md) for how the plugin is built and how it talks to the server. In short:

```bash
dotnet build DaraletPlayer.sln -c Release
```

```bash
dotnet test tests/DaraletPlayer.Tests/DaraletPlayer.Tests.csproj
```

Both dlls end up in `bin/`. `Daralet.Loader.dll` is for development only: add it as a Decal network filter (with
Daralet unticked) and it reloads the plugin every time you rebuild. Players only need `Daralet.dll`.
