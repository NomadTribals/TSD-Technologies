# Clean installation and upgrade

Use one setup:

- **New tablet and apps:** Anvil WebOverlay + TSD Technologies + the new Tactical Supply Drops addon.
- **Original standalone Logistics:** Tactical Supply Drops **2.1.0** + Anvil WebOverlay. Do not install TSD Technologies alongside the standalone version.

## Before replacing files

Finish any active delivery, collect your cargo, and close the game and SPT server. Back up your SPT profile and any TSD settings or custom wallpapers you want to keep. The existing tablet item ID is retained; a clean mod installation does not require deleting your profile or buying a replacement tablet.

## Remove the previous mod installation

Remove these old mod folders from your SPT game installation, if present:

- `BepInEx/plugins/TacticalSupplyDrops/`
- `SPT_Runtime/user/mods/TacticalSupplyDrops/`

If you tested the separate Technologies prototype, remove its old program folders too:

- `BepInEx/plugins/TsdTechnologies/`
- `SPT_Runtime/user/mods/TsdTechnologies/`

Older installations may have loose `TacticalSupplyDrops.Client.dll` or `TacticalSupplyDrops.Overlay.dll` files directly under `BepInEx/plugins/`. Remove those copies as well; do not leave duplicate versions installed.

For a fully fresh configuration, back up and remove these TSD-specific entries if present:

- `BepInEx/config/com.nomadtribals.tacticalsupplydrops.cfg`
- `BepInEx/config/com.nomadtribals.tacticalsupplydrops.overlay.cfg`
- `BepInEx/config/com.tacticalsupplydrops.client.cfg`
- `BepInEx/config/com.nomadtribals.tsdtechnologies.cfg`
- `BepInEx/config/TacticalSupplyDrops/`
- `BepInEx/config/TsdTechnologies/`

The last two folders contain old request/delivery state, generated app caches and custom wallpaper files. Save your own images elsewhere before removing them. Do not copy old request/response files back into the fresh installation.

**Keep your SPT profiles, the SPT installation itself, Anvil WebOverlay, and unrelated mods.** These instructions apply only to the named TSD entries.

## Install the new setup

1. Install or update Anvil WebOverlay to **1.12.0 or newer**.
2. Extract the TSD Technologies archive into the SPT game root, beside `EscapeFromTarkov.exe`.
3. Extract the new Tactical Supply Drops addon archive into the same game root.
4. Start the SPT server, then the game.
5. A fresh installation defaults to **F9**. Choose your preferred tablet key in Settings if desired.
6. Copy your saved PNG/JPG wallpapers into `BepInEx/config/TsdTechnologies/Wallpapers/`, then use Settings > Wallpaper > Refresh.

Both archives contain their required `BepInEx/` and `SPT_Runtime/` paths. Do not extract them directly inside `SPT_Runtime`.
