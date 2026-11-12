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
    [BepInPlugin(TabletRuntime.PluginGuid, "TSD Technologies", "1.2.1")]
    public sealed class TechnologiesPlugin : BaseUnityPlugin
    {
        private float timer;
        private HarmonyLib.Harmony? patches;
        private static TechnologiesPlugin? instance;
        private void Awake() { instance=this; patches=new HarmonyLib.Harmony(TabletRuntime.PluginGuid+".drops"); patches.Patch(HarmonyLib.AccessTools.Method(typeof(EFT.Interactive.LootItem),"Init"),prefix:new HarmonyLib.HarmonyMethod(typeof(TechnologiesPlugin),nameof(BeforeLootInit))); RefreshContext(); Logger.LogInfo("TSD Technologies device services loaded."); }
        private void Update() { timer += Time.unscaledDeltaTime; if(timer >= 1f) { timer=0; RefreshContext(); } }
        private void RefreshContext()
        {
            var state = new TabletContext { UpdatedUtcTicks = DateTime.UtcNow.Ticks };
            try
            {
                var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance as ClientGameWorld : null;
                var player = world?.MainPlayer;
                state.InRaid = player != null && player.IsYourPlayer && !(world is HideoutGameWorld);
                var session = Singleton<ClientApplication<IEftSession>>.Instantiated ? Singleton<ClientApplication<IEftSession>>.Instance.GetClientBackEndSession() : null;
                state.Nickname = state.InRaid && player != null ? player.Profile?.Info?.Nickname : session?.Profile?.Info?.Nickname;
                if(state.InRaid && player != null)
                {
                    state.HasTablet = player.Inventory?.AllRealPlayerItems?.Any(IsTablet) == true;
                    state.RaidTime = world?.GameDateTime?.Calculate().ToString("HH:mm",System.Globalization.CultureInfo.InvariantCulture);
                }
                else state.HasTablet = session?.Profile?.InventoryInfo?.AllRealPlayerItems?.Any(IsTablet) == true;
            }
            catch(Exception ex) { Logger.LogDebug("Device context is not ready: " + ex.Message); }
            TabletRuntime.PublishContext(state);
        }
        private static void BeforeLootInit(EFT.Interactive.LootItem __instance,Item item) { try { TabletDropPhysics.Repair(__instance.gameObject,item); } catch(Exception ex) { instance?.Logger.LogError("Tablet loot collision repair failed: "+ex); } }
        private static bool IsTablet(Item item) { return item != null && string.Equals(item.StringTemplateId,TabletRuntime.TerminalTemplateId,StringComparison.OrdinalIgnoreCase); }
        private void OnDestroy() { patches?.UnpatchSelf(); instance=null; TabletRuntime.PublishContext(new TabletContext()); }
    }
}
