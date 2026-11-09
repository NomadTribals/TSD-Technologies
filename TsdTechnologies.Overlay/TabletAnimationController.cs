using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using HarmonyLib;
using TsdTechnologies.Api;
using UnityEngine;

namespace TsdTechnologies.Overlay.Animation
{
    public enum TabletHoldStyle { LeftHand, Rangefinder }
    public sealed class TabletAnimationController : MonoBehaviour
    {
        public const string Guid = "com.nomadtribals.tsdtechnologies.animation";
        internal static TabletAnimationController Instance;
        internal ConfigEntry<Vector3> Position, Rotation;
        internal ConfigEntry<float> Scale;
        private ConfigEntry<bool> animationEnabled, tabletVisual;
        private ConfigEntry<TabletHoldStyle> holdStyle;
        private ConfigEntry<float> minimumDraw, settleTime, darkPause;
        private ConfigEntry<bool> handlingSounds;
        private ConfigEntry<float> handlingVolume;
        private float darkSince = -1f;
        private ConfigEntry<bool> cameraFit;
        private ConfigEntry<Vector3> gripOffset;
        private float configPoll;
        private DateTime configWrite;
        private ConfigFile Config;
        private BepInEx.Logging.ManualLogSource Logger;
        private Harmony patches;
        private MethodInfo toggle;
        private FieldInfo visible;
        private FieldInfo overlayHandle;
        private MethodInfo executeOverlayScript;
        private float indicatorProbe;
        private bool indicatorConnected, indicatorBusy;
        private BaseUnityPlugin os;
        private Player player;
        private Item priorItem;
        private bool pending, bypass, preview, awaitingWindow, cancelled;
        private float deadline;
        private TabletHandsController active;
        private EFT.ItemInHandSubsystem.LeftHandController leftHand;
        private bool leftSession;
        private bool leftPoseFitted;
        private Camera poseCamera;

        internal void Initialize(BaseUnityPlugin owner, ConfigFile settings, BepInEx.Logging.ManualLogSource logger)
        {
            Config=settings;Logger=logger;
            Instance = this;
            animationEnabled = Config.Bind("Tablet Animation", "Enabled", true, "Raise the physical tablet before opening Field OS. Disable for interface-only access.");
            holdStyle = Config.Bind("Tablet Animation Advanced", "HoldStyle", TabletHoldStyle.LeftHand, "Native left-hand pose. Rangefinder is an alternative compatibility pose. Applies on the next draw.");
            minimumDraw = Config.Bind("Animation timing", "MinimumDrawSeconds", .9f, new ConfigDescription("Minimum left-hand raise time before Field OS can open.",new AcceptableValueRange<float>(.3f,2f)));
            settleTime = Config.Bind("Animation timing", "SettledSeconds", .18f, new ConfigDescription("How long the hand must be settled after the raise before opening Field OS.",new AcceptableValueRange<float>(.05f,.5f)));
            darkPause = Config.Bind("Animation timing", "DarkScreenPauseSeconds", .18f, new ConfigDescription("Keep the dark physical tablet raised briefly after the interface finishes shutting down, then lower it.",new AcceptableValueRange<float>(0f,.6f)));
            handlingSounds = Config.Bind("Animation audio", "GearRustleEnabled", true, "Use the character's native gear rustle when raising and putting away the tablet.");
            handlingVolume = Config.Bind("Animation audio", "GearRustleVolume", 1f, new ConfigDescription("Native equipment handling sound volume. Separate from electronic tablet sounds.",new AcceptableValueRange<float>(0f,1f)));
            tabletVisual = Config.Bind("Pose fitting", "ShowTabletModel", true, "Replace the rangefinder geometry with the existing tablet model. Turn off to compare the native pose.");
            Position = Config.Bind("Pose fitting", "TabletPosition", new Vector3(0f, 0f, 0f), "Local position relative to the native device model. Applies while holding.");
            Rotation = Config.Bind("Pose fitting", "TabletRotation", new Vector3(0f, 0f, 0f), "Local rotation in degrees. Applies while holding.");
            Scale = Config.Bind("Pose fitting", "TabletScale", .8f, new ConfigDescription("Model scale multiplier. Applies while holding.", new AcceptableValueRange<float>(0.1f, 3f)));
            cameraFit = Config.Bind("Pose fitting", "CameraAlignedPose", true, "Face the held tablet screen toward the first-person camera while retaining its palm attachment. Applies on the next draw.");
            gripOffset = Config.Bind("Pose fitting", "PalmGripOffset", new Vector3(.16f, .11f, .05f), "Small offset from the animated palm in view axes: right, up, forward (metres). Positions the hand near an edge rather than through the screen. Applies live.");
            os = owner;
            toggle = AccessTools.Method(os.GetType(), "ToggleTablet");
            visible = AccessTools.Field(os.GetType(), "visible");
            overlayHandle = AccessTools.Field(os.GetType(),"overlay");
            executeOverlayScript = overlayHandle?.FieldType.GetMethod("ExecuteScript",new[]{typeof(string)});
            if(toggle == null || visible == null) { Logger.LogError("This Field OS build has no supported animation hook."); return; }
            patches = new Harmony(Guid);
            patches.Patch(toggle, prefix: new HarmonyMethod(typeof(TabletAnimationController), nameof(BeforeToggle)));
            patches.Patch(AccessTools.Method(os.GetType(),"HandleChannel"),prefix:new HarmonyMethod(typeof(TabletAnimationController),nameof(ReceiveIndicatorState)));
            var leftType = typeof(EFT.ItemInHandSubsystem.LeftHandController);
            patches.Patch(AccessTools.Method(leftType, "CreateItem"), postfix: new HarmonyMethod(typeof(TabletAnimationController), nameof(OnLeftModelCreated)));
            patches.Patch(AccessTools.Method(leftType, "method_10"), prefix: new HarmonyMethod(typeof(TabletAnimationController), nameof(BeforeLeftModelReturned)));
            Logger.LogInfo("Tablet animation ready. The configured tablet key raises and lowers the device.");
            configWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
        }

        private static void OnLeftModelCreated(EFT.ItemInHandSubsystem.LeftHandController __instance)
        {
            var self = Instance;
            if(self == null || !self.leftSession || !ReferenceEquals(self.leftHand, __instance)
                || __instance._leftHandItem == null || __instance._item?.StringTemplateId != TabletRuntime.TerminalTemplateId) return;
            var model = __instance._leftHandItem.gameObject;
            var guard = model.GetComponent<HeldTabletPhysics>() ?? model.AddComponent<HeldTabletPhysics>();
            guard.Begin();
            var housing = model.GetComponent<HeldTabletHousing>() ?? model.AddComponent<HeldTabletHousing>();
            try { housing.Begin(); }
            catch(Exception ex) { housing.Restore(); self.Logger.LogWarning("Rounded housing creation failed; keeping the original model: " + ex.Message); }
            self.Logger.LogInfo("Held tablet physics disabled before showing the model: " + guard.ColliderCount + " collider(s).");
        }

        private static void BeforeLeftModelReturned(EFT.ItemInHandSubsystem.LeftHandController __instance)
        {
            if(__instance._leftHandItem == null) return;
            __instance._leftHandItem.GetComponent<HeldTabletHousing>()?.Restore();
            __instance._leftHandItem.GetComponent<HeldTabletPhysics>()?.Restore();
        }

        private static bool BeforeToggle()
        {
            var self = Instance;
            if(self == null || self.bypass || !self.animationEnabled.Value || self.WindowVisible) return true;
            if(self.pending) { self.cancelled = true; return false; }
            var p = LocalPlayer();
            if(p == null) return true; // Menu access remains unchanged.
            if(self.leftSession) { self.preview = false; return true; }
            if(self.active != null && p.HandsController == self.active) { self.preview = false; return true; }
            self.Begin(p, false);
            return false;
        }

        private bool WindowVisible => os != null && visible != null && (bool)visible.GetValue(os);
        private static Player LocalPlayer()
        {
            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance as ClientGameWorld : null;
            var p = world?.MainPlayer;
            return p != null && p.IsYourPlayer && p.HealthController?.IsAlive == true ? p : null;
        }

        private void Update()
        {
            if(WindowVisible&&leftSession)
            {
                indicatorProbe-=Time.unscaledDeltaTime;
                if(indicatorProbe<=0f)
                {
                    indicatorProbe=1f;
                    try
                    {
                        var handle=overlayHandle?.GetValue(os);
                        if(handle!=null)executeOverlayScript?.Invoke(handle,new object[]{IndicatorScript});
                    }
                    catch(Exception ex){Logger.LogDebug("Physical indicator bridge deferred: "+ex.Message);}
                }
            }
            if(leftSession || active != null)
            {
                configPoll += Time.unscaledDeltaTime;
                if(configPoll >= 2f)
                {
                    configPoll = 0f;
                    try
                    {
                        var write = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
                        if(write != configWrite) { Config.Reload(); configWrite = write; Logger.LogInfo("Animation pose settings reloaded."); }
                    }
                    catch(Exception ex) { Logger.LogDebug("Pose settings reload deferred: " + ex.Message); }
                }
            }
            if(leftSession)
            {
                if(LocalPlayer() != player) { ClearSession(); return; }
                if(leftHand?._leftHandItem != null)
                {
                    leftHand._leftHandItem.localPosition = Position.Value;
                    if(!leftPoseFitted) leftHand._leftHandItem.localRotation = Quaternion.Euler(Rotation.Value);
                    leftHand._leftHandItem.localScale = Vector3.one * Mathf.Clamp(Scale.Value, .1f, 3f);
                }
                if(player.HandsController?.Item != priorItem || !animationEnabled.Value || player.IsSprintEnabled
                    || player.HandsController.IsInventoryOpen() || (!pending && !leftHand.IsUsing)) { Restore(true); return; }
                if(!pending && !preview && !WindowVisible && (!awaitingWindow || Time.unscaledTime >= deadline) && DarkPauseComplete()) { Restore(); return; }
                if(WindowVisible) { awaitingWindow = false; darkSince = -1f; }
            }
            if(active != null)
            {
                active.ApplyPose(Position.Value, Rotation.Value, Scale.Value);
                if(LocalPlayer() != player || player.HandsController != active) { ClearSession(); return; }
                if(!animationEnabled.Value || player.IsSprintEnabled || player.HandsController.IsInventoryOpen()) { Restore(true); return; }
                if(!pending && !preview && !WindowVisible && (!awaitingWindow || Time.unscaledTime >= deadline) && DarkPauseComplete()) Restore();
                if(WindowVisible) { awaitingWindow = false; darkSince = -1f; }
            }
            if(pending && !cancelled && Time.unscaledTime >= deadline)
            {
                Logger.LogWarning("Tablet draw timed out; requesting previous equipment through normal hands operations.");
                cancelled = true;
                if(active != null && player != null && player.HandsController == active) Restore();
            }
        }

        private void LateUpdate()
        {
            if(!leftSession || leftHand?._leftHandItem == null) return;
            leftHand._leftHandItem.GetComponent<HeldTabletHousing>()?.SetIndicators(WindowVisible,indicatorConnected,indicatorBusy,Time.unscaledTime);
            if(!leftPoseFitted || poseCamera == null)return;
            // Use a small offset from the actual animated palm, never an absolute
            // camera-space target captured before the arm finished moving.
            var model = leftHand._leftHandItem;
            var view = poseCamera.transform;
            var grip = gripOffset.Value;
            model.position = model.parent.TransformPoint(Position.Value)
                + view.right * grip.x + view.up * grip.y + view.forward * grip.z;
            model.rotation = Quaternion.LookRotation(-view.forward, view.up)
                * Quaternion.Euler(Rotation.Value);
        }

        [Serializable]private sealed class IndicatorState{public bool connected=false;public bool busy=false;}
        private static bool ReceiveIndicatorState(string channel,string payload)
        {
            if(channel!="tsd-physical-indicators")return true;
            var self=Instance;
            if(self!=null&&payload!=null&&payload.Length<512)
            {
                try{var state=JsonUtility.FromJson<IndicatorState>(payload);self.indicatorConnected=state.connected;self.indicatorBusy=state.busy;}
                catch(Exception ex){self.Logger.LogDebug("Physical indicator state ignored: "+ex.Message);}
            }
            return false;
        }
        private const string IndicatorScript=@"(()=>{
 if(window.__tsdPhysicalIndicatorObserver){window.__tsdPhysicalIndicatorObserver.send();return;}
 const link=document.querySelector('.tablet-link-indicator'),data=document.querySelector('.tablet-link-led');
 if(!link||!data||!window.overlay?.send)return;
 const send=()=>window.overlay.send('tsd-physical-indicators',JSON.stringify({connected:link.classList.contains('is-connected'),busy:data.classList.contains('is-data-active')}));
 const observer=new MutationObserver(send);observer.observe(link,{attributes:true,attributeFilter:['class']});observer.observe(data,{attributes:true,attributeFilter:['class']});
 window.__tsdPhysicalIndicatorObserver={observer,send};send();
})();";

        private async void Begin(Player p, bool posePreview)
        {
            if(holdStyle.Value == TabletHoldStyle.LeftHand) { await BeginLeft(p, posePreview); return; }
            if(pending || active != null || !p.StateIsSuitableForHandInput || p.IsSprintEnabled || p.InventoryController.IsChangingWeapon
                || p.HandsController == null || !p.HandsController.CanRemove() || p.HandsController.IsInInteractionStrictCheck())
            { Logger.LogInfo("Wait until your hands are idle before raising the tablet."); return; }
            var terminal = p.Inventory?.AllRealPlayerItems?.FirstOrDefault(i => i.StringTemplateId == TabletRuntime.TerminalTemplateId);
            if(terminal == null) { Logger.LogInfo("Carry the TSD Field Terminal to use it."); return; }
            player = p; priorItem = p.HandsController.Item; preview = posePreview; pending = true; cancelled = false;
            deadline = Time.unscaledTime + 15f;
            try
            {
                // Visual-only donor: never inserted into a player's inventory or persisted.
                var donor = Singleton<ItemFactory>.Instance.CreateItem(MongoID.Generate(), "61605e13ffa6e502ac5e7eef", null);
                var resources = donor.Template.AllResources.Concat(terminal.Template.AllResources).Distinct().ToArray();
                await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Online, resources, JobYieldPriority.Low);
                if(cancelled || LocalPlayer() != p || Time.unscaledTime >= deadline) { ClearSession(); return; }
                if(p.HandsController.Item != priorItem || !p.HandsController.CanRemove()) { ClearSession(); return; }
                Func<TabletHandsController> factory = () =>
                {
                    active = TabletHandsController.Build(p, terminal, donor, tabletVisual.Value);
                    return active;
                };
                new Player.Process<TabletHandsController, IUsableItemController>(p, factory, terminal, fastHide: false)
                    .Proceed(null, result =>
                    {
                        pending = false;
                        if(result.Failed) { Logger.LogWarning("Tablet draw failed: " + result.Error); Restore(); return; }
                        if(cancelled || LocalPlayer() != p) { Restore(); return; }
                        Logger.LogInfo("Tablet draw finished; holding device.");
                        if(!preview)
                        {
                            bypass = true;
                            try { toggle.Invoke(os, null); awaitingWindow = true; deadline = Time.unscaledTime + 4f; }
                            finally { bypass = false; }
                        }
                    }, true);
            }
            catch(Exception ex)
            {
                Logger.LogError("Animation creation failed: " + ex);
                pending = false;
                if(active != null && player != null && player.HandsController == active) Restore();
                else ClearSession();
            }
        }

        private async Task BeginLeft(Player p, bool posePreview)
        {
            if(pending || active != null || leftSession || !p.StateIsSuitableForHandInput || p.IsSprintEnabled
                || p.InventoryController.IsChangingWeapon || p.HandsController == null || p.HandsController.IsAiming
                || !p.HandsController.CanRemove() || p.HandsController.IsInInteractionStrictCheck())
            { Logger.LogInfo("Wait until the weapon is idle and lowered from aiming before the tablet animation."); return; }
            var terminal = p.Inventory?.AllRealPlayerItems?.FirstOrDefault(i => i.StringTemplateId == TabletRuntime.TerminalTemplateId);
            if(terminal == null) { Logger.LogInfo("Carry the TSD Field Terminal to use it."); return; }
            var native = AccessTools.Field(typeof(Player), "_leftHandController")?.GetValue(p) as EFT.ItemInHandSubsystem.LeftHandController;
            if(native == null || native.IsUsing || native.InAction) { Logger.LogInfo("The native left hand is unavailable or already occupied."); return; }
            player = p; priorItem = p.HandsController.Item; preview = posePreview; pending = true; cancelled = false;
            deadline = Time.unscaledTime + 12f;
            try
            {
                PlayHandlingSound(p,"raise request");
                await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,
                    ObjectsFactory.AssemblyType.Online, terminal.Template.AllResources.ToArray(), JobYieldPriority.Low);
                if(cancelled || LocalPlayer() != p || Time.unscaledTime >= deadline || p.HandsController?.Item != priorItem)
                { ClearSession(); return; }
                leftHand = native; leftSession = true;
                float drawStarted = Time.unscaledTime;
                p.SetLeftHandItem(terminal);
                if(!native.IsUsing || native._item != terminal)
                { Logger.LogWarning("The game declined the left-hand interaction in this stance."); ClearSession(); return; }
                while(LocalPlayer() == p && !cancelled && Time.unscaledTime < deadline && native.IsUsing
                    && (native._leftHandItem == null || native.InAction)) await Task.Yield();
                if(cancelled || LocalPlayer() != p || Time.unscaledTime >= deadline || native._leftHandItem == null || !native.IsUsing)
                { Logger.LogWarning("Left-hand preview did not reach its holding pose."); Restore(); return; }
                FitLeftPose(native._leftHandItem);
                if(!await WaitForSettledHand(p,native,drawStarted)) { Restore(); return; }
                pending = false;
                Logger.LogInfo("Left-hand tablet ready; original weapon controller retained. Model=" + native._leftHandItem.name);
                if(!preview)
                {
                    bypass = true;
                    try { toggle.Invoke(os, null); awaitingWindow = true; deadline = Time.unscaledTime + 4f; }
                    finally { bypass = false; }
                }
            }
            catch(Exception ex) { Logger.LogError("Left-hand creation failed: " + ex); Restore(); }
            finally { pending = false; }
        }

        private void FitLeftPose(Transform model)
        {
            leftPoseFitted = false;
            if(!cameraFit.Value || model == null || model.parent == null) return;
            var camera = Camera.allCameras.FirstOrDefault(c => c.enabled && c.name == "FPS Camera") ?? Camera.main;
            if(camera == null) { Logger.LogWarning("No first-person camera available for fitting; keeping the manual palm pose."); return; }
            poseCamera = camera;
            leftPoseFitted = true;
            Logger.LogInfo("Tablet screen orientation follows " + camera.name + "; model remains at its native palm attachment.");
        }

        private async Task<bool> WaitForSettledHand(Player p,EFT.ItemInHandSubsystem.LeftHandController native,float started)
        {
            var gate=new TabletTransitionTiming(started,minimumDraw.Value,settleTime.Value,4f);
            var hand=native._handBone;
            var view=poseCamera!=null?poseCamera.transform:null;
            Vector3 previousPosition=Vector3.zero;Quaternion previousRotation=Quaternion.identity;
            float previousTime=-1f;
            while(!cancelled && leftSession && LocalPlayer()==p && native.IsUsing && p.HandsController?.Item==priorItem)
            {
                float now=Time.unscaledTime;
                if(now<=previousTime){await Task.Yield();continue;}
                bool moving=false;
                if(hand!=null&&view!=null)
                {
                    var position=view.InverseTransformPoint(hand.position);
                    var rotation=Quaternion.Inverse(view.rotation)*hand.rotation;
                    if(previousTime>=0f)
                    {
                        float dt=now-previousTime;
                        moving=Vector3.Distance(position,previousPosition)/dt>.15f || Quaternion.Angle(rotation,previousRotation)/dt>120f;
                    }
                    else moving=true;
                    previousPosition=position;previousRotation=rotation;
                }
                previousTime=now;
                var state=gate.Observe(now,native._leftHandItem!=null,native.InAction,moving);
                if(state==DrawReadiness.Ready){Logger.LogInfo("Tablet raise settled; Field OS may open after "+(now-started).ToString("F2")+"s.");return true;}
                if(state==DrawReadiness.TimedOut){Logger.LogWarning("Tablet did not settle; cancelling opening rather than displaying during the raise.");return false;}
                await Task.Yield();
            }
            return false;
        }

        private bool DarkPauseComplete()
        {
            if(darkSince<0f)darkSince=Time.unscaledTime;
            return Time.unscaledTime-darkSince>=darkPause.Value;
        }

        private void PlayHandlingSound(Player p,string phase)
        {
            if(!handlingSounds.Value || handlingVolume.Value<=0f || p==null || LocalPlayer()!=p)return;
            try
            {
                p.PlayGearSound(handlingVolume.Value,true);
                Logger.LogInfo("Tablet gear rustle: "+phase+" at "+Time.unscaledTime.ToString("F2")+"s; volume="+handlingVolume.Value.ToString("F2"));
            }
            catch(Exception ex) { Logger.LogWarning("Native tablet handling sound was unavailable: "+ex.Message); }
        }

        private void Restore(bool immediate=false)
        {
            var p = player; var controller = active; var item = priorItem;
            if(WindowVisible)
            {
                bypass = true;
                try { toggle.Invoke(os, null); } finally { bypass = false; }
                if(!immediate){preview=false;awaitingWindow=false;return;}
            }
            if(leftSession)
            {
                cancelled = true;
                if(p != null && p.HealthController?.IsAlive == true)
                {
                    try { if(leftHand!=null&&leftHand.IsUsing)PlayHandlingSound(p,"put-away"); p.RemoveLeftHandItem(); }
                    catch(Exception ex) { Logger.LogWarning("Left-hand put-away failed: " + ex.Message); }
                }
                ClearSession();
                return;
            }
            if(p != null && p.HealthController?.IsAlive == true && p.HandsController == controller)
            {
                try
                {
                    // Do not force the remembered weapon if the player no longer owns it.
                    bool owned = item != null && p.Inventory.AllRealPlayerItems.Any(i => i.Id == item.Id);
                    ClearSession();
                    if(owned) p.TryProceed(item, r => { if(r.Failed) Logger.LogWarning("Previous equipment could not be restored: " + r.Error); });
                    else p.SetEmptyHands(r => { if(r.Failed) Logger.LogWarning("Empty hands transition failed: " + r.Error); });
                }
                catch(Exception ex) { Logger.LogError("Equipment restoration needs attention: " + ex); }
            }
            else ClearSession();
        }

        private void ClearSession() { active = null; leftHand = null; leftSession = false; leftPoseFitted = false; poseCamera = null; player = null; priorItem = null; pending = false; preview = false; awaitingWindow = false; darkSince=-1f; indicatorProbe=0f;indicatorConnected=false;indicatorBusy=false; }
        private void OnDestroy() { cancelled = true; Restore(true); patches?.UnpatchSelf(); Instance = null; }
    }

    // Changes only the held pooled instance. Loot collision is restored before reuse.
    public sealed class HeldTabletPhysics : MonoBehaviour
    {
        private Collider[] colliders;
        private bool[] colliderStates;
        private Rigidbody[] bodies;
        private bool[] collisionStates, kinematicStates;
        private bool captured;
        public int ColliderCount => colliders?.Length ?? 0;

        public void Begin()
        {
            Restore();
            colliders = GetComponentsInChildren<Collider>(true);
            colliderStates = colliders.Select(c => c.enabled).ToArray();
            bodies = GetComponentsInChildren<Rigidbody>(true);
            collisionStates = bodies.Select(b => b.detectCollisions).ToArray();
            kinematicStates = bodies.Select(b => b.isKinematic).ToArray();
            captured = true;
            foreach(var collider in colliders) collider.enabled = false;
            foreach(var body in bodies) { body.detectCollisions = false; body.isKinematic = true; }
        }

        public void Restore()
        {
            if(!captured) return;
            captured = false;
            for(int i=0;i<colliders.Length;i++) if(colliders[i] != null) colliders[i].enabled = colliderStates[i];
            for(int i=0;i<bodies.Length;i++) if(bodies[i] != null) { bodies[i].isKinematic = kinematicStates[i]; bodies[i].detectCollisions = collisionStates[i]; }
        }

        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }

    public sealed class TabletHandsController : PortableRangeFinderController
    {
        private GameObject tablet;
        private Transform pose;
        private Renderer[] hidden;
        private bool[] rendererStates;
        private Collider[] tabletColliders;
        private bool[] colliderStates;
        private Transform[] tabletTransforms;
        private int[] originalLayers;

        internal static TabletHandsController Build(Player player, Item terminal, Item donor, bool replaceVisual)
        {
            var controller = Player.ItemHandsController.smethod_1<TabletHandsController>(player, terminal,
                (item,p) => Singleton<ObjectsFactory>.Instance.CreateItem(donor, Player.GetVisibleToCamera(p), p, true));
            Player.UsableItemController.Setup(controller, player);
            if(replaceVisual)
            {
                try { controller.AttachTablet(terminal); }
                catch(Exception ex) { controller.ReleaseVisual(); Debug.LogWarning("TSD tablet visual fitting failed; keeping the native preview: " + ex.Message); }
            }
            return controller;
        }

        private void AttachTablet(Item terminal)
        {
            // Preserve the native animated arms; hide only rigid device meshes on this instance.
            hidden = ControllerGameObject.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();
            rendererStates = hidden.Select(r => r.enabled).ToArray();
            var anchor = hidden.FirstOrDefault(r => r.enabled)?.transform;
            if(anchor == null) throw new InvalidOperationException("Native device geometry could not be located for tablet fitting.");
            tablet = Singleton<ObjectsFactory>.Instance.CreateItem(terminal, false);
            if(tablet == null) throw new InvalidOperationException("Tablet model could not be loaded.");
            pose = new GameObject("TSD_Tablet_Pose").transform;
            pose.SetParent(anchor.parent, false);
            pose.localPosition = anchor.localPosition;
            pose.localRotation = anchor.localRotation;
            tablet.transform.SetParent(pose, false);
            foreach(var r in hidden) r.enabled = false;
            tabletColliders = tablet.GetComponentsInChildren<Collider>(true);
            colliderStates = tabletColliders.Select(c => c.enabled).ToArray();
            foreach(var collider in tabletColliders) collider.enabled = false;
            tabletTransforms = tablet.GetComponentsInChildren<Transform>(true);
            originalLayers = tabletTransforms.Select(t => t.gameObject.layer).ToArray();
            foreach(var child in tabletTransforms) child.gameObject.layer = anchor.gameObject.layer;
            tablet.SetActive(true);
            ApplyPose(TabletAnimationController.Instance.Position.Value, TabletAnimationController.Instance.Rotation.Value, TabletAnimationController.Instance.Scale.Value);
        }

        internal void ApplyPose(Vector3 position, Vector3 rotation, float scale)
        {
            if(tablet == null) return;
            tablet.transform.localPosition = position;
            tablet.transform.localRotation = Quaternion.Euler(rotation);
            tablet.transform.localScale = Vector3.one * Mathf.Clamp(scale, .1f, 3f);
        }

        public override void Destroy()
        {
            ReleaseVisual();
            base.Destroy();
        }

        private void ReleaseVisual()
        {
            if(tabletColliders != null) for(int i=0;i<tabletColliders.Length;i++) if(tabletColliders[i] != null) tabletColliders[i].enabled = colliderStates[i];
            if(tabletTransforms != null) for(int i=0;i<tabletTransforms.Length;i++) if(tabletTransforms[i] != null) tabletTransforms[i].gameObject.layer = originalLayers[i];
            if(tablet != null) { AssetPoolObject.ReturnToPool(tablet); tablet = null; }
            if(pose != null) { UnityEngine.Object.Destroy(pose.gameObject); pose = null; }
            if(hidden != null) for(int i=0;i<hidden.Length;i++) if(hidden[i] != null) hidden[i].enabled = rendererStates[i];
            tabletColliders = null; colliderStates = null; hidden = null; rendererStates = null;
            tabletTransforms = null; originalLayers = null;
        }
    }
}
