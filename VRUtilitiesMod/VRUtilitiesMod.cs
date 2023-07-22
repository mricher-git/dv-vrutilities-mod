using BepInEx;
using BepInEx.Configuration;
using DV.CabControls.Spec;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRTK;

namespace VRUtilitiesMod
{

    internal static class PluginInfo
    {
        public const string Guid = "VRUtilities";
        public const string Name = "VRUtilities Mod";
        public const string Version = "0.1.0";
    }

    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public class VRUtilitiesMod : BaseUnityPlugin
    {
        public static VRUtilitiesMod Instance { get; private set; }
        public static Harmony HarmonyInst { get; private set; }

        private bool GameInitialized;
        private const string TWEAKS_SECTION = "Tweaks";
        private const string FIXES_SECTION = "Fixes";

        public ConfigEntry<bool> DisableTouch;
        public ConfigEntry<bool> OverrideInteractionButton;
        public ConfigEntry<string> InteractionButton;
        private static VRTK_ControllerEvents.ButtonAlias OriginalUseButton = VRTK_ControllerEvents.ButtonAlias.Undefined;
        public ConfigEntry<bool> DisableWindowLights;
        public VRTK_ControllerEvents.ButtonAlias useButton;

        private GameObject windowLights;

        private Dictionary<string, GameObject> LocoInteriors = new Dictionary<string, GameObject>
        {
            { "LocoS282A_Interior", null },
            { "LocoDE2_Interior", null },
            { "LocoDM3_Interior", null },
            { "LocoDH4_Interior", null },
            { "LocoDE6_Interior", null }
        };

        void Start()
        {
            if (Instance != null)
            {
                Logger.LogFatal("VR Utilities is already loaded!");
                Destroy(this);
                return;
            }

            Instance = this;

            DisableTouch = Instance.Config.Bind(TWEAKS_SECTION, "Disable Touch Interaction", false, "Stops controls from automatically interacticting by touch, require button press");
            DisableTouch.SettingChanged += (_, __) => setDisableTouch();
            OverrideInteractionButton = Instance.Config.Bind(TWEAKS_SECTION, "Override Interaction Button", false, "Override VR controller button use to interact with buttons/switches");
            OverrideInteractionButton.SettingChanged += (_, __) => setOverrideInteraction();
            InteractionButton = Instance.Config.Bind(TWEAKS_SECTION, "Interaction Button", "TriggerPress", new ConfigDescription("Choose which button to override to: TriggerPress or GripPress", new AcceptableValueList<string>("TriggerPress", "GripPress")));
            InteractionButton.SettingChanged += (_, __) => setOverrideInteraction();
            DisableWindowLights = Instance.Config.Bind(FIXES_SECTION, "Night time performance fix", true, "Disables building window lights due to current bug in build 93-998");
            DisableWindowLights.SettingChanged += (_, __) => setDisableWindowLights();

            Instance.Config.SaveOnConfigSet = true;
            if (VRManager.IsVREnabled())
            {
                Logger.LogInfo("VR Enabled");
            }
            else
            {
                Logger.LogInfo("VR not enabled - DV Utilites mod disabled");
                return;
            }

            WorldStreamingInit.LoadingFinished += OnLoadingFinished;
            UnloadWatcher.UnloadRequested += UnloadRequested;
            FindPrefabs();
            setDisableTouch();

            HarmonyInst = new Harmony(PluginInfo.Guid);
            HarmonyInst.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void OnLoadingFinished()
        {
            GameInitialized = true;
            windowLights = GameObject.Find("windowLights 0");
            setDisableWindowLights();
            Logger.LogInfo("OLF: Use Button set to" + SetupDeviceSpecificControls.useOverrideButtonForButtonComponent);
        }

        private void UnloadRequested()
        {
            GameInitialized = false;
        }

        private void setDisableTouch()
        {
            foreach (var go in LocoInteriors.Values)
            {
                //FindObjectOfType<ToggleSwitchVRTK>();
                var buttons = go.GetComponentsInChildren<DV.CabControls.Spec.Button>(true);
                foreach (var button in buttons)
                {
                    button.disableTouchUse = DisableTouch.Value;
                }
                var toggles = go.GetComponentsInChildren<DV.CabControls.Spec.ToggleSwitch>(true);
                foreach (var toggle in toggles)
                {
                    toggle.disableTouchUse = DisableTouch.Value;
                }
            }
        }

        private void setDisableWindowLights()
        {
            if (!GameInitialized) return;

            if (windowLights == null) return;

            windowLights.SetActive(!DisableWindowLights.Value);
        }

        public void setOverrideInteraction()
        {
            if (InteractionButton.Value == "TriggerPress") useButton = VRTK_ControllerEvents.ButtonAlias.TriggerPress;
            else if (InteractionButton.Value == "GripPress") useButton = VRTK_ControllerEvents.ButtonAlias.GripPress;
            else useButton = VRTK_ControllerEvents.ButtonAlias.Undefined;

            var buttons = Resources.FindObjectsOfTypeAll<Button>();

            if (OverrideInteractionButton.Value)
            {
                if (OverrideUseInteractionButton.ControllersInit)
                    SetupDeviceSpecificControls.useOverrideButtonForButtonComponent = useButton;

                foreach (var button in buttons)
                {
                    button.overrideUseButton = useButton;
                }
            }
            else
            {
                if (OverrideUseInteractionButton.ControllersInit)
                    SetupDeviceSpecificControls.useOverrideButtonForButtonComponent = OriginalUseButton;
                
                foreach (var button in buttons)
                {
                    if (button.transform.parent?name.StartsWith("MapMarker") : false)
                    {
                        button.overrideUseButton = VRTK_ControllerEvents.ButtonAlias.TriggerPress;
                    }
                    else
                    {
                        button.overrideUseButton = VRTK_ControllerEvents.ButtonAlias.Undefined;
                    }
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
                    Logger.LogInfo($"Found Prefab: {go.name}");
                }
            }
        }

        [HarmonyPatch(typeof(SetupDeviceSpecificControls), "SetupForDevice")]
        public static class OverrideUseInteractionButton
        {
            public static bool ControllersInit { get; private set; }
            public static void Postfix()//, ControlImplBase ___control, ref Coroutine ___UpdaterCoroutine)
            {
                if (!ControllersInit)
                {
                    OriginalUseButton = SetupDeviceSpecificControls.useOverrideButtonForButtonComponent;
                }
                ControllersInit = true;
                VRUtilitiesMod.Instance.setOverrideInteraction();
            }
        }
    }
}
