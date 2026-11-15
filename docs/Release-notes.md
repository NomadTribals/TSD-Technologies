# TSD Technologies 1.4.1 — Expanded Map Controls

- Hold M to enlarge the minimap while continuing to move, sprint and aim. Release it to restore the original size and position.
- Choose Hold or Toggle under F12 → TSD Technologies → Satellite → MapExpansionMode. Hold is the default; Toggle uses one press to enlarge and another to restore.
- Expanded size defaults to 85% of the screen. Adjust ExpandedMapSizePercent in F12.
- The enlarged map defaults to 80% opacity so the game remains visible behind it. Adjust ExpandedMapOpacity in F12; 1 is fully opaque. The compact minimap stays fully opaque.
- A short compositor transition smooths expansion and collapse while resizing the live map once per change. The existing viewer and stream stay loaded.
- Corrects map-image caching to reduce repeat transfers when reopening the map. Live raid data and preferences remain uncached.

**Control change:** M now controls the enlarged minimap only. Open the interactive Maps app through the normal tablet key and Home screen. The configured tablet key is preserved; existing users can keep F10. Change the enlarged-map key under F12 → Controls → MapToggleKey.

Enable the minimap from Maps before using enlargement. Existing map settings, wallpapers and tablet preferences are preserved.

Requires SPT 4.1.6 and Anvil WebOverlay 1.12.0 or newer. Satellite is an optional, separate installation for Maps; tested with Satellite 1.0.1. Messenger 1.0.0 and Logistics 3.1.0 remain compatible and do not need updating.

Thanks to Fums for Satellite (CC BY-NC 4.0) and to maschine for Anvil WebOverlay. Satellite code and map scans are not bundled.
