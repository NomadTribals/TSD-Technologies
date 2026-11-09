# Field OS app interface

Reference `TsdTechnologies.Api.dll` from the tablet installation; do not bundle another copy in each addon. The contract targets .NET Standard 2.0. A BepInEx app plugin depends on `com.nomadtribals.tsdtechnologies.overlay` and registers during Awake. Backend gameplay plugins can depend on `com.nomadtribals.tsdtechnologies` to read the current device context.

```csharp
TabletRuntime.Register(new TabletApplication {
    Id = "example.notes",
    Name = "Notes",
    IconSvg = myTrustedSvg,
    Document = myHtmlDocument,
    Channels = new[] { "notes-state" },
    Attach = host => {
        host.ChannelMessage += (channel, payload) => {
            // Handle this app's own requests and validate its gameplay rules.
        };
        host.VisibilityChanged += shown => { /* Pause UI-only work when hidden. */ };
        host.Post("notes-state", "{\"text\":\"Ready\"}");
    }
});
```

The OS injects the app bridge before the document's own scripts. No Anvil reference or custom window is needed in the addon. Use `window.tabletApp.on(channel, handler)` and `.send(channel, stringPayload)` for data. Outgoing host channels must be declared in `Channels`. The host queues initial data until the document is ready. `host.ExecuteScript` runs only inside this app's document; `host.Post(..., TabletPostOptions.LatestOnly)` coalesces streamed state on this app's own channel.

The JavaScript bridge also supplies:

- `.activity(operation, busy)` for the shared processing LED. Clear busy flags on completion or failure.
- `.sound(kind)` for device-controlled interface sounds.
- `.page(name)` for the shared app/page label.
- `.openPage = name => ...` to handle optional OS navigation shortcuts.
- `.onVisibility = shown => ...` to flush edits or pause UI-only work.
- The `tablet-theme` window event carries the device accent color in `event.detail`.

`LogPage` can name the page opened by the device's LOG shortcut. HOME navigation is owned by the OS. App names and data are rendered as text; icon SVG and HTML are trusted installed mod resources. App frame messages are matched to their actual source frame, rather than accepting a claimed app id.

Register each id once. Treat its metadata as immutable after registration. Call `TabletRuntime.Unregister(id)` during plugin shutdown. Keep each app's settings, channels, data and backend validation separate. `TabletRuntime.Context` exposes fresh device ownership, raid state, nickname and clock; it is not an authorization substitute for an app's own server checks.

Reference the API supplied with TSD Technologies 1.x, declare the tablet dependency, and test your app against the supported tablet release.
