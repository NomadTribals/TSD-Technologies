# TSD Technologies 1.3.0

- Adds generic unread badges for compatible optional apps on the Home screen.
- Adds app visibility reporting so an app can distinguish a visible chat from a remembered page while the tablet is closed or on Home.
- Provides the tablet support required by TSD Messenger 1.0.0, available separately.
- Existing tablet settings, wallpapers, startup/resume behaviour and Logistics compatibility are preserved.

Requires SPT 4.1.6 and Anvil WebOverlay 1.12.0 or newer. TSD Logistics remains at 3.1.0 and does not require another update. Messenger is optional and is not bundled with Technologies.

# TSD Technologies 1.2.1

- **Boot once per raid and outside raids:** the first completed opening plays the normal startup and welcome sequence. Further openings during the same raid resume the last page without repeating the startup sequence. Menus, stash and hideout share a separate completed startup for the current game session.
- The physical tablet raise and lower animations remain in place.
- A new raid starts with the normal boot sequence again. Cancelling the initial startup before it finishes does not skip it on the next attempt.
- Returning from a raid resumes the completed menu startup. Restarting the game resets the menu startup.

Requires SPT 4.1.6 and Anvil WebOverlay 1.12.0 or newer. TSD Logistics 3.1.0 remains a separate app and does not need another update.

# TSD Technologies 1.2.0

- **TSD Access** scans for nearby keycard readers and shows their names and distances. Select a locked reader to run a timed bypass. The terminal displays ACCESS GRANTED once the door confirms the unlock.
- The default scan radius is 5 metres and the default bypass time is 10 seconds. Both are adjustable in F12 under Hacking.
- **App Browser** includes a starter catalogue of compatible Field OS apps with app details and Forge links.
- The tablet animation uses the native left-hand pose. The animation toggle remains available in F12.
- **Maps** displays a Coming Soon page.

Requires SPT 4.1.6 and Anvil WebOverlay 1.12.0 or newer. Tactical Supply Drops Logistics 3.0.0 is installed separately.

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
