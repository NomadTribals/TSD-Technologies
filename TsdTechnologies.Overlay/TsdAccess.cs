using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using Newtonsoft.Json;
using UnityEngine;

namespace TsdTechnologies.Overlay
{
    // Native Field OS app. Door changes run on Unity's main thread.
    internal sealed class TsdAccess
    {
        private readonly MonoBehaviour owner;
        private readonly Action<string> publish;
        private readonly ConfigEntry<float> range, duration;
        private readonly Dictionary<string, KeycardDoor> discovered = new Dictionary<string, KeycardDoor>();
        private KeycardDoor target;
        private Player actor;
        private GameWorld scene;
        private float elapsed, attemptDuration, attemptRange, refresh;
        private bool shown, committing;
        private string phase = "idle", message = "Scan for nearby electronic access controllers.";
        private string targetName = "", targetId = "";
        private float progress;

        internal TsdAccess(MonoBehaviour owner, ConfigFile config, Action<string> publish)
        {
            this.owner = owner; this.publish = publish;
            range = config.Bind("Hacking", "DetectionRangeMetres", 5f,
                new ConfigDescription("Nearby keycard-reader detection radius in metres.", new AcceptableValueRange<float>(1f, 10f)));
            duration = config.Bind("Hacking", "HackDurationSeconds", 10f,
                new ConfigDescription("Time needed to bypass a keycard door. Applies to the next attempt.", new AcceptableValueRange<float>(5f, 60f)));
        }

        private static Player LocalPlayer()
        {
            if (!Singleton<GameWorld>.Instantiated) return null;
            var world = Singleton<GameWorld>.Instance as ClientGameWorld;
            if (world == null || world is HideoutGameWorld) return null;
            var player = world.MainPlayer;
            return player != null && player.IsYourPlayer && player.HealthController?.IsAlive == true ? player : null;
        }

        private static float Distance(KeycardDoor door, Vector3 position)
        {
            float distance = Vector3.Distance(position, door.transform.position);
            if (door.Proxies != null)
                foreach (var reader in door.Proxies)
                    if (reader != null && reader.isActiveAndEnabled)
                        distance = Mathf.Min(distance, Vector3.Distance(position, reader.transform.position));
            return distance;
        }

        private static string DoorName(KeycardDoor door)
        {
            if (!string.IsNullOrEmpty(door.KeyId))
            {
                string key = door.KeyId + " Name";
                string label = key.Localized();
                if (!string.IsNullOrEmpty(label) && label != key) return label + " reader";
            }
            // Scene identifiers remain available when a modded reader has no localized name.
            string fallback = (door.name ?? "Keycard door").Replace('_', ' ');
            return fallback.Length > 80 ? fallback.Substring(0, 80) : fallback;
        }

        internal void SetVisible(bool value)
        {
            shown = value;
            if (!value) Cancel("Connection closed. No bypass completed.");
            else Scan();
        }

        internal void Scan()
        {
            if (!shown || phase == "running" || committing) return;
            phase = "idle"; progress = 0; targetId = ""; targetName = "";
            discovered.Clear();
            var player = LocalPlayer();
            if (player != null)
                foreach (var door in UnityEngine.Object.FindObjectsOfType<KeycardDoor>())
                    if (door != null && door.isActiveAndEnabled && Distance(door, player.Transform.position) <= range.Value)
                        discovered[door.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture)] = door;
            if (player == null) message = "Enter a raid to scan nearby keycard readers.";
            else if (phase == "idle") message = discovered.Count == 0 ? "No keycard readers in range. Move closer to a reader and scan again." : "Local controllers detected. Select a reader to inspect its access status.";
            Send();
        }

        internal void Start(string id)
        {
            if (!shown || phase == "running" || committing || id == null || id.Length > 64) return;
            var player = LocalPlayer();
            KeycardDoor door;
            if (player == null || !TsdTechnologies.Api.TabletRuntime.Context.IsFresh || !TsdTechnologies.Api.TabletRuntime.Context.HasTablet ||
                !discovered.TryGetValue(id, out door) || door == null || !door.isActiveAndEnabled ||
                Distance(door, player.Transform.position) > range.Value)
            { phase = "failed"; message = "Reader unavailable. Move closer and scan again."; Send(); return; }
            if (!door.Operatable || door.NoInteractionsAllowed || door.DoorState != EDoorState.Locked)
            { phase = "failed"; message = "Controller cannot be bypassed in its current state."; Send(); return; }
            actor = player; scene = Singleton<GameWorld>.Instance; target = door; targetId = id; targetName = DoorName(door);
            elapsed = 0; progress = 0; attemptDuration = duration.Value; attemptRange = range.Value;
            phase = "running"; message = "Initializing local connection…"; refresh = 0; Send();
        }

        internal void Cancel(string reason = "Bypass cancelled. Door unchanged.")
        {
            if (phase != "running" || committing) return;
            phase = "cancelled"; message = reason; target = null; actor = null; scene = null; Send();
        }

        internal void Tick(float delta, bool tabletVisible)
        {
            if (phase == "running" && !committing)
            {
                var player = LocalPlayer();
                var context = TsdTechnologies.Api.TabletRuntime.Context;
                if (!shown || !tabletVisible || player == null || player != actor || !Singleton<GameWorld>.Instantiated || scene != Singleton<GameWorld>.Instance || !context.IsFresh || !context.HasTablet)
                    Cancel("Connection interrupted. No bypass completed.");
                else if (target == null || !target.isActiveAndEnabled || Distance(target, player.Transform.position) > attemptRange)
                    Cancel("Signal lost. Return within reader range and try again.");
                else if (!target.Operatable || target.NoInteractionsAllowed || target.DoorState != EDoorState.Locked)
                    Cancel("Controller state changed. Scan again before retrying.");
                else
                {
                    elapsed += delta; progress = Mathf.Min(99f, elapsed / attemptDuration * 100f);
                    var stages = new[] { "Initializing local connection…", "Identifying access controller…", "Analyzing authentication protocol…", "Attempting credential bypass…", "Verifying access response…" };
                    message = stages[Mathf.Min(4, (int)(progress / 20f))];
                    if (elapsed >= attemptDuration)
                    { committing = true; message = "Awaiting controller acknowledgement…"; Send(); owner.StartCoroutine(Unlock(target)); }
                }
            }
            if (!shown) return;
            refresh += delta;
            if (refresh >= .2f) { refresh = 0; Send(); }
        }

        private IEnumerator Unlock(KeycardDoor door)
        {
            bool autoOpen = door._openOnUnlock;
            bool completed = false;
            try
            {
                // Preserve the native handle, reader lights and unlock audio, without auto-opening.
                door._openOnUnlock = false;
                var native = door.UnlockCoroutine();
                bool failed = false;
                while (true)
                {
                    bool next;
                    try { next = native.MoveNext(); }
                    catch { failed = true; break; }
                    if (!next) break;
                    yield return native.Current;
                }
                completed = !failed && door != null && (door.DoorState == EDoorState.Shut || door.DoorState == EDoorState.Open);
            }
            finally
            {
                if (door != null) door._openOnUnlock = autoOpen;
                committing = false; target = null; actor = null; scene = null;
                phase = completed ? "success" : "failed";
                progress = completed ? 100f : 99f;
                message = completed ? "Access granted. Door unlocked." : "Controller did not confirm access. Scan again to check its state.";
                Send();
            }
        }

        private void Send()
        {
            var player = LocalPlayer();
            publish(JsonConvert.SerializeObject(new {
                phase, message, progress, targetId, targetName, duration = duration.Value, range = range.Value,
                busy = phase == "running", committing,
                doors = discovered.Where(p => p.Value != null && player != null && Distance(p.Value, player.Transform.position) <= range.Value)
                    .Select(p => new { id = p.Key, name = DoorName(p.Value), distance = Math.Round(Distance(p.Value, player.Transform.position), 1),
                        state = p.Value.DoorState.ToString(), available = p.Value.Operatable && !p.Value.NoInteractionsAllowed && p.Value.DoorState == EDoorState.Locked })
                    .OrderBy(p => p.distance).ToArray()
            }));
        }
    }
}
