using System;
using System.Linq;
using BepInEx;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using TsdTechnologies.Api;
using UnityEngine;
namespace TsdTechnologies.Client
{
    [BepInPlugin(TabletRuntime.PluginGuid, "TSD Technologies", "1.0.0")]
    public sealed class TechnologiesPlugin : BaseUnityPlugin
    {
        private float timer;
        private void Awake() { RefreshContext(); Logger.LogInfo("TSD Technologies device services loaded."); }
        private void Update() { timer += Time.unscaledDeltaTime; if(timer >= 1f) { timer=0; RefreshContext(); } }
        private void RefreshContext()
        {
            var state = new TabletContext { UpdatedUtcTicks = DateTime.UtcNow.Ticks };
            try
            {
                var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance as ClientGameWorld : null;
                var player = world?.MainPlayer;
                state.InRaid = player != null && player.IsYourPlayer;
                var session = Singleton<ClientApplication<IEftSession>>.Instantiated ? Singleton<ClientApplication<IEftSession>>.Instance.GetClientBackEndSession() : null;
                state.Nickname = state.InRaid ? player.Profile?.Info?.Nickname : session?.Profile?.Info?.Nickname;
                if(state.InRaid)
                {
                    state.HasTablet = player.Inventory?.AllRealPlayerItems?.Any(IsTablet) == true;
                    state.RaidTime = world.GameDateTime?.Calculate().ToString("HH:mm",System.Globalization.CultureInfo.InvariantCulture);
                }
                else state.HasTablet = session?.Profile?.InventoryInfo?.AllRealPlayerItems?.Any(IsTablet) == true;
            }
            catch(Exception ex) { Logger.LogDebug("Device context is not ready: " + ex.Message); }
            TabletRuntime.PublishContext(state);
        }
        private static bool IsTablet(Item item) { return item != null && string.Equals(item.StringTemplateId,TabletRuntime.TerminalTemplateId,StringComparison.OrdinalIgnoreCase); }
        private void OnDestroy() { TabletRuntime.PublishContext(new TabletContext()); }
    }
}
