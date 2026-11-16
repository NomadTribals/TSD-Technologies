using System;
using Comfort.Common;
using EFT;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace TsdTechnologies.Overlay
{
    internal sealed class MinimapVisibility : IDisposable
    {
        private static MinimapVisibility instance;
        private readonly Harmony patch=new Harmony("com.nomadtribals.tsdtechnologies.minimap-visibility");
        private GameWorld startedWorld;
        private float timerPeekUntil;
        private static readonly System.Reflection.FieldInfo remainingField=AccessTools.Field(typeof(EFT.UI.BattleTimer.TimerPanel),"TimeSpan");
        internal void ReadClocks(out string worldTime,out string remaining){
            worldTime=null;remaining=null;
            var world=startedWorld;
            if(world==null)return;
            worldTime=world.GameDateTime?.Calculate().ToString("HH:mm",System.Globalization.CultureInfo.InvariantCulture);
            var panel=GameUI.Instance?.TimerPanel?._mainTimerPanel;
            if(panel!=null&&remainingField?.GetValue(panel) is TimeSpan span){
                var seconds=Math.Max(0,(int)Math.Ceiling(span.TotalSeconds));
                remaining=TimeSpan.FromSeconds(seconds).ToString(seconds>=3600?@"h\:mm\:ss":@"mm\:ss");
            }
        }
        internal MinimapVisibility()
        {
            instance=this;
            patch.Patch(AccessTools.Method(typeof(GameWorld),nameof(GameWorld.OnGameStarted)),postfix:new HarmonyMethod(typeof(MinimapVisibility),nameof(RaidStarted)));
            patch.Patch(AccessTools.Method(typeof(ExtractionTimersPanel),nameof(ExtractionTimersPanel.ShowTimer)),postfix:new HarmonyMethod(typeof(MinimapVisibility),nameof(TimerRequested)));
        }
        private static void RaidStarted(GameWorld __instance){if(instance!=null)instance.startedWorld=__instance;}
        private static void TimerRequested(bool showExits){if(instance!=null)instance.timerPeekUntil=Time.unscaledTime+(showExits?8f:4f);}
        internal bool GameplayVisible()
        {
            var world=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance as ClientGameWorld:null;
            if(world==null||world is HideoutGameWorld||!ReferenceEquals(world,startedWorld))return false;
            var player=world.MainPlayer;
            if(player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true)return false;
            var ui=CommonUI.Instance;
            if(ui==null||!Shown(ui.EftBattleUIScreen))return false;
            if(Shown(ui.InventoryScreen)||Shown(ui.ScavengerInventoryScreen)||Shown(ui.WeaponModdingScreen)
                ||Shown(ui.EditBuildScreen)||Shown(ui.SettingsScreen)||Shown(ui.TransferItemsInRaidScreen)||Shown(ui.MenuScreen))return false;
            var timers=GameUI.Instance?.TimerPanel;
            if(Shown(timers)){
                if(Shown(timers._timersPanel))return false;
                var clock=timers._mainTimerPanel;
                if(clock!=null&&clock.DisplayTime&&(!clock.ForcePull||Time.unscaledTime<timerPeekUntil))return false;
            }
            return player.HandsController==null||!player.HandsController.IsInventoryOpen();
        }
        private static bool Shown(Component screen)=>screen!=null&&screen.gameObject.activeInHierarchy;
        public void Dispose(){patch.UnpatchSelf();startedWorld=null;if(ReferenceEquals(instance,this))instance=null;}
    }
}
