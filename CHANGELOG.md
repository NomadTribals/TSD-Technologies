# TSD Technologies 1.1.0

The Field Terminal now has a physical tablet model and a raise/put-away animation, with native equipment handling sounds and working connection/activity indicators.

## Changes

- Added tablet raise and put-away animations using your configured tablet key.
- Field OS opens after the tablet settles and closes before it is put away. Pressing the key again during the raise cancels it.
- Added native gear rustle and physical signal/activity indicators.
- Refined the rectangular graphite housing, slim corner plates and matte black back, including the inventory icon and dropped model.
- Fixed the terminal's solid collision so dropped tablets remain on the ground and can be picked up again.
- Animation controls are included in the main Technologies F12 configuration. Firmware and device information display 1.1.0.

## Using the tablet

Keep your existing tablet key; fresh installations default to F9. Stop sprinting, aiming or other hands interactions before opening it. Movement stops while the interactive overlay is open.

For now, the interface is a separate movable overlay. You can resize and position it over the held tablet screen to suit your view. It does not track the tablet's movement. Direct screen integration remains a future goal, subject to feasibility and performance testing.

## Installation

Close the game and server. Follow the included upgrade guide, replace the Technologies plugin and server-mod folders, and keep your configuration and wallpapers. Logistics 3.0.0 stays installed and does not need an update. Private testers must also remove the old animation and drop-physics test plugins.

Requires SPT 4.1.6 and Anvil WebOverlay 1.12.0 or newer. The app API remains 1.0.0. Fika compatibility is unverified.

Thank you to maschine, author of Anvil WebOverlay, for the framework and continued support.

---

# TSD Technologies 1.0.0

The TSD tablet and Field OS are now an independent platform. Home, Settings, wallpapers, the operator welcome, raid/local clock, window controls and connection/activity indicators belong to the tablet. Compatible mods register as separate apps.

TSD Logistics 3.0.0 is the first separate app. Maps and App Browser currently display Coming Soon pages.

Requires SPT 4.1.6 and Anvil WebOverlay 1.12.0 or newer. Fika compatibility is unverified. Read the clean-install guide before replacing older TSD files. Existing tablet item IDs are preserved. Fresh settings default to F9.

Special thanks to Anvil (maschine34675), author of Anvil WebOverlay, for the framework, direct support and improvements that made this tablet possible.

---

