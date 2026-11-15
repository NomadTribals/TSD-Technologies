# Installing or updating TSD Technologies 1.4.0

1. Close the game and SPT server.
2. Back up the player profile and tablet settings.
3. Install Anvil WebOverlay 1.12.0 or newer if needed.
4. Extract TsdTechnologies-1.4.0.zip into the main SPT folder beside EscapeFromTarkov.exe. Merge BepInEx and SPT_Runtime and replace Technologies program files.
5. Keep existing configuration, wallpapers, map preferences and separately installed apps. No settings reset is required.
6. For Maps, install Satellite separately following its instructions, including the map pack, then start the server and game. Tested with Satellite 1.0.1.

Logistics 3.1.0 and Messenger 1.0.0 remain compatible. Do not combine Technologies with standalone Tactical Supply Drops 2.1.0. Fika compatibility is unverified.

## Controls

Fresh tablet key: F9; an existing tablet key is preserved. Maps shortcut: M, configurable or disabled with None under F12 → TSD Technologies → Controls → MapToggleKey.

F12 → Tablet Startup → BootFrequency defaults to OncePerGameLaunch. OncePerRaid remains available.

Enable the minimap from Maps. Adjust its width, height and top/right margins under F12 → TSD Technologies → Satellite. It displays during raids while the main tablet is closed and tablet access is available. It does not capture mouse or keyboard input.

Use Satellite’s menu inside the tablet to select markers and information. These map preferences persist in BepInEx/config/TsdTechnologies/Satellite/Preferences.json. Keep that file when updating. The minimap inherits visibility choices but retains a compact follow view.

Satellite is optional. Missing installations show a download link; connection failures offer a reload after checking the server. Satellite’s original installed files and external-browser settings are unchanged.
