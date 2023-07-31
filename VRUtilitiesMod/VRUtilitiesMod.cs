using DV.CabControls.Spec;
using DV.CabControls.VRTK;
using DV.VRTK_Extensions;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using UnityModManagerNet;
using VRTK;
using VRUtilitiesMod.UMM;
using static VRUtilitiesMod.UMM.Loader;

namespace VRUtilitiesMod
{

    public class VRUtilitiesMod : MonoBehaviour
    {
        public const string Version = "0.3.2";

        private bool GameInitialized;
        internal Loader.VRUtilitiesModSettings Settings;
        public static Harmony HarmonyInst { get; private set; }
        public static CameraZoomVR CZInstance { get; private set; }

        [SaveOnReload]
        private static VRTK_ControllerEvents.ButtonAlias OriginalUseButton = VRTK_ControllerEvents.ButtonAlias.Undefined;     
        private Dictionary<string, GameObject> LocoInteriors = new Dictionary<string, GameObject>
        {
            { "LocoS282A_Interior", null },
            { "LocoS060_Interior", null },
            { "LocoDE2_Interior", null },
            { "LocoDM3_Interior", null },
            { "LocoDH4_Interior", null },
            { "LocoDE6_Interior", null }
        };
        
        public bool TouchInteractionEnabled { set; get; }
        public VRTK_ControllerEvents.ButtonAlias TouchInteractionButton { set; get; }

        void Start()
        {
            if (!VRManager.IsVREnabled())
            {
                Loader.Log("VR not enabled - DV Utilites mod disabled");
                return;
            }

            Loader.Log("VR Enabled");
            /*if (Settings.CameraZoom.Button == VRTK_ControllerEvents.ButtonAlias.Undefined &&
                Settings.CameraZoom.Axis == VRTK_ControllerEvents.Vector2AxisAlias.Undefined)
            {
                StartCoroutine(InitCoro());
            }*/

            FindPrefabs();

            HarmonyInst = new Harmony(Loader.ModEntry.Info.Id);
            Loader.LogDebug("PatchAll");
            HarmonyInst.PatchAll(Assembly.GetExecutingAssembly());

            OnSettingsChanged();
            WorldStreamingInit.LoadingFinished += OnLoadingFinished;
            UnloadWatcher.UnloadRequested += UnloadRequested;
            if (WorldStreamingInit.Instance && WorldStreamingInit.IsLoaded) OnLoadingFinished();
        }

        /*private IEnumerator InitCoro()
        {
            while (!VRTK_SDKManager.ValidInstance()) yield return null;
            VRTK_SDKManager.SubscribeLoadedSetupChanged(InitSettings);

            yield break;
        }

        private void InitSettings(VRTK_SDKManager sender, VRTK_SDKManager.LoadedSetupChangeEventArgs e)
        {
            Loader.LogDebug("Headset: " + VRTK_DeviceFinder.GetHeadsetType().ToString());

            if (VRTK_DeviceFinder.GetHeadsetType() == SDK_BaseHeadset.HeadsetType.WindowsMixedReality)
            {
                Settings.CameraZoom.Axis = VRTK_ControllerEvents.Vector2AxisAlias.TouchpadTwo;
                Settings.CameraZoom.LeftRight = ControllerSide.Right;
            }
            else
            {
                Settings.CameraZoom.Button = VRTK_ControllerEvents.ButtonAlias.TouchpadPress;
                Settings.CameraZoom.LeftRight = ControllerSide.Left;
            }

            VRTK_SDKManager.UnsubscribeLoadedSetupChanged(InitSettings);
        }*/

        public void OnDestroy()
        {
            UnloadWatcher.UnloadRequested -= UnloadRequested;
            WorldStreamingInit.LoadingFinished -= OnLoadingFinished;

            if (UnloadWatcher.isUnloading || UnloadWatcher.isQuitting)
            {
                return;
            }
            Settings.UseOverride.Enabled = false;
            setOverrideUse();
            Settings.DisableTouch = false;
            Loader.LogDebug("UnpatchAll");
            HarmonyInst.UnpatchAll(Loader.ModEntry.Info.Id);
            if (CZInstance != null) UnityEngine.Object.DestroyImmediate(CZInstance);
        }

        public void OnSettingsChanged()
        {
            if ((TouchInteractionEnabled != Settings.UseOverride.Enabled) || (TouchInteractionButton != Settings.UseOverride.Button))
            {
                TouchInteractionButton = Settings.UseOverride.Button;
                TouchInteractionEnabled = Settings.UseOverride.Enabled;
                setOverrideUse();
            }
        }

        private void OnLoadingFinished()
        {
            GameInitialized = true;
            UMM.Loader.Log("Info: Orignal Use Button was set to " + SetupDeviceSpecificControls.useOverrideButtonForButtonComponent);
            CZInstance = PlayerManager.ActiveCamera.gameObject.AddComponent<CameraZoomVR>();
        }

        private void UnloadRequested()
        {
            GameInitialized = false;
        }

        public void setOverrideUse()
        {
            var vrtkButtons = (ButtonVRTK[])Resources.FindObjectsOfTypeAll<ButtonVRTK>();
            var vrtkToggle = (ToggleSwitchVRTK[])Resources.FindObjectsOfTypeAll<ToggleSwitchVRTK>();

            if (Settings.UseOverride.Enabled && Settings.UseOverride.Button != VRTK_ControllerEvents.ButtonAlias.Undefined)
            {
                if (OriginalUseButton != VRTK_ControllerEvents.ButtonAlias.Undefined)
                    SetupDeviceSpecificControls.useOverrideButtonForButtonComponent = Settings.UseOverride.Button;

                foreach(var button in vrtkButtons)
                {
                    if (button.gameObject.scene.name == null) continue;
                    Traverse traverseButton = Traverse.Create(button);
                    traverseButton.Field("interactable").Field("useOverrideButton").SetValue(Settings.UseOverride.Button);
                    traverseButton.Field("useOverrideButtonSet").SetValue(true);
                }
                foreach (var button in vrtkToggle)
                {
                    if (button.gameObject.scene.name == null) continue;
                    Traverse traverseButton = Traverse.Create(button);
                    traverseButton.Field("interactable").Field("useOverrideButton").SetValue(Settings.UseOverride.Button);
                    traverseButton.Field("useOverrideButtonSet").SetValue(true);
                }
            }
            else
            {
                SetupDeviceSpecificControls.useOverrideButtonForButtonComponent = OriginalUseButton;
                
                foreach (var button in vrtkButtons)
                {
                    if (button.gameObject.scene.name == null) continue;
                    var overrideUseButton = Traverse.Create(button).Field("spec").Field("overrideUseButton").GetValue<VRTK_ControllerEvents.ButtonAlias>();
                    if (overrideUseButton == VRTK_ControllerEvents.ButtonAlias.Undefined || VRTK_DeviceFinder.GetControllerReferenceRightHand().IsWandOrUndefined() || VRTK_DeviceFinder.GetControllerReferenceLeftHand().IsWandOrUndefined())
                    {
                        Traverse.Create(button).Field("interactable").Field("useOverrideButton").SetValue(OriginalUseButton);
                    }
                    else
                    {
                        Traverse.Create(button).Field("interactable").Field("useOverrideButton").SetValue(overrideUseButton);
                    }
                    Traverse.Create(button).Field("useOverrideButtonSet").SetValue(false);
                }
                foreach (var button in vrtkToggle)
                {
                    if (button.gameObject.scene.name == null) continue;
                    var overrideUseButton = Traverse.Create(button).Field("spec").Field("overrideUseButton").GetValue<VRTK_ControllerEvents.ButtonAlias>();
                    if (overrideUseButton == VRTK_ControllerEvents.ButtonAlias.Undefined || VRTK_DeviceFinder.GetControllerReferenceRightHand().IsWandOrUndefined() || VRTK_DeviceFinder.GetControllerReferenceLeftHand().IsWandOrUndefined())
                    {
                        Traverse.Create(button).Field("interactable").Field("useOverrideButton").SetValue(OriginalUseButton);
                    }
                    else
                    {
                        Traverse.Create(button).Field("interactable").Field("useOverrideButton").SetValue(overrideUseButton);
                    }
                    Traverse.Create(button).Field("useOverrideButtonSet").SetValue(false);
                }
            }
        }

        public void FindPrefabs()
        {
            var gos = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var go in gos)
            {
                if (LocoInteriors.ContainsKey(go.name) && go.activeInHierarchy == false && go.transform.parent == null)
                {
                    LocoInteriors[go.name] = go;
                    UMM.Loader.LogDebug($"Found Prefab: {go.name}");
                }
            }
        }

        [HarmonyPatch(typeof(SetupDeviceSpecificControls), "SetupForDevice")]
        public static class OverrideUseInteractionButton
        {
            public static void Postfix()
            {
                if (OriginalUseButton == VRTK_ControllerEvents.ButtonAlias.Undefined)
                {
                    OriginalUseButton = SetupDeviceSpecificControls.useOverrideButtonForButtonComponent;
                }
                Loader.Instance?.setOverrideUse();
            }
        }

        [HarmonyPatch(typeof(ButtonVRTK), "OnTouched")]
        [HarmonyPatch(typeof(ToggleSwitchVRTK), "OnTouched")]
        public static class DisableToggleSwitchTouchInteraction
        {
            public static bool Prefix()
            {
                if (Loader.Settings.DisableTouch) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(LocomotionInputWrapper), "JumpRequested", MethodType.Getter)]
        public static class DisableJump
        {
            public static void Postfix(ref bool __result)
            {
                if (Loader.Settings.DisableJump) __result = false;
            }
        }
    }
}
