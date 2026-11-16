# TSD Technologies 1.5.0 — Minimap Navigation & Raid Clocks

- Pan the enlarged minimap with the arrow keys and zoom with the mouse wheel. Mouse-wheel zoom can be disabled in F12. Returning to the compact view restores player follow.
- The minimap now hides during inventory, corpse/container looting and other blocking game screens, and waits until the raid has started before appearing.
- Displaying the raid timer or extraction list temporarily hides the minimap so those panels remain readable. The minimap returns when the panels close.
- Optional in-game clock at the top left and remaining raid time at the top right. Each has its own F12 toggle; both are enabled by default.
- The raid countdown is amber above ten minutes and turns red at 10:00 remaining or less.
- New MinimapPerformanceMode reduces drawing resolution while preserving the larger viewing area and normal icon sizes. Enabled by default; disable it for sharper native-resolution drawing. Responsiveness still depends on map complexity and system load.
- Removed the enlargement transition delay. Hold/Toggle controls, expanded opacity, saved minimap placement and map visibility settings remain available.

## Controls and settings

Hold M to enlarge by default, then release to restore the compact map. Toggle mode remains available under F12 → TSD Technologies → Satellite → MapExpansionMode. Rebind M under Controls → MapToggleKey.

While enlarged: arrow keys pan; the mouse wheel zooms. The display stays click-through. Normal game bindings remain active, including any bindings assigned to those keys or the wheel. Use the full tablet Maps app for Satellite menus and settings.

F12 → TSD Technologies → Satellite:
- MinimapGameClock: in-game time at the top left.
- MinimapRaidTimer: remaining raid time at the top right, with the ten-minute warning.
- ExpandedMapMouseWheelZoom: enable or disable wheel zoom while enlarged.
- MinimapPerformanceMode: lower-resolution drawing for reduced rendering load, or native-resolution drawing when disabled.

## Updating

Close the game and SPT server, then extract into the main SPT folder and merge BepInEx and SPT_Runtime. Preserve existing configuration files and separately installed apps. No new save or configuration reset is required.

Requires SPT 4.1.6 and Anvil WebOverlay 1.12.0 or newer. Satellite is optional and installed separately for Maps; tested with Satellite 1.0.1. Messenger 1.0.0 and Logistics 3.1.0 remain compatible and do not need updating.

Thanks to Fums for Satellite (CC BY-NC 4.0) and to maschine for Anvil WebOverlay. Satellite code and map scans are not bundled.
