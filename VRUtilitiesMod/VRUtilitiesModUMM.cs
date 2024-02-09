using System;
using UnityEngine;
using UnityModManagerNet;
using VRTK;

namespace VRUtilitiesMod.UMM
{
#if DEBUG
    [EnableReloading]
#endif
    public static class Loader
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static VRUtilitiesMod Instance { get; private set; }

        internal static VRUtilitiesModSettings Settings;

        private static bool Main(UnityModManager.ModEntry modEntry)
        {
            if (ModEntry != null || Instance != null)
            {
                modEntry.Logger.Warning("VRUtilities is already loaded!");
                return false;
            }

            ModEntry = modEntry;
            ModEntry.OnSaveGUI = OnSave;
            ModEntry.OnGUI = OnGUI;
            ModEntry.OnUnload = Unload;
            
            Settings = UnityModManager.ModSettings.Load<VRUtilitiesModSettings>(modEntry);

            var go = new GameObject("[VRUtilitiesMod]");
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<VRUtilitiesMod>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance.Settings = Settings;

            return true;
        }

        private static bool Unload(UnityModManager.ModEntry modEntry)
        {
            if (Instance != null) UnityEngine.Object.DestroyImmediate(Instance.gameObject);
            return true;
        }

        private static void OnSave(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(ModEntry);
        }
        private static VRTK_ControllerEvents.ButtonAlias origZoomButton;
        private static VRTK_ControllerEvents.Vector2AxisAlias origZoomAxis;

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            origZoomAxis = Settings.CameraZoom.Axis;
            origZoomButton = Settings.CameraZoom.Button;

            Settings.Draw(ModEntry);
        }

        public static void LogError(string message)
        {
            ModEntry.Logger.Error(message);
        }

        public static void Log(string message)
        {
            ModEntry.Logger.Log(message);
        }

        public static void LogWarning(string message)
        {
            ModEntry.Logger.Warning(message);
        }

        public static void LogException(Exception e)
        {
            ModEntry.Logger.LogException(e);
        }

        public static void LogDebug(string message)
        {
#if DEBUG
                ModEntry.Logger.Log(message);
#endif
        }

        public enum ControllerSide { Left, Right };

        public class VRUtilitiesModSettings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Disable Touch Interaction", Tooltip = "Stops controls from automatically interacticting by touch, require button press")]
            public bool DisableTouch;

            [Draw("Override Use Button", Vertical = true, Box = true)]
            public UseOverrideGroup UseOverride = new UseOverrideGroup();
            [Draw("Disable Jumping")]
            public bool DisableJump;

            [Draw("Camera Zoom", Box = true)]
            public CameraZoomVR CameraZoom = new CameraZoomVR();
            
            public class UseOverrideGroup
            {
                [Draw("Enabled", Tooltip = "Override VR controller button use to interact with buttons/switches")]
                public bool Enabled;
                [Draw("Use Button", Tooltip = "If override is enabled, which button to use")]
                public VRTK_ControllerEvents.ButtonAlias Button;
            }

            public class CameraZoomVR
            {
                [Draw("Enable Zoom", Tooltip = "Enable camera zooming feature")]
                public bool ZoomEnabled = true;
                
                [Draw("Zoom Button", Tooltip = "Non-WMR Typically Button: TouchpadPress")]
                public VRTK_ControllerEvents.ButtonAlias Button;
                
                [Draw("Zoom Axis", Tooltip = "WMR Typically Axis: TouchpadTwo")]
                public VRTK_ControllerEvents.Vector2AxisAlias Axis = VRTK_ControllerEvents.Vector2AxisAlias.TouchpadTwo;
                
                [Draw("Which controller", DrawType.ToggleGroup)] 
                public ControllerSide LeftRight = ControllerSide.Left;

                [Draw("Zoom Factor")]
                public float ZoomFactor = 2.5f;

                [Draw("Smoothing Time")]
                public float ZoomTime = 0.2f;

                [Draw("Comfort Tunnel Enabled")]
                public bool TunnelEnabled = true;

                [Draw("Comfort Tunnel Size")]
                public float ComfortTunnelSize = 0.94f;

                [Draw("Comfort Tunnel Feather")]
                public float ComfortTunnerFeather = 0.04f;

                [Draw("Enable Debug Output")]
                public bool DebugEnabled = false;
            }

            public override void Save(UnityModManager.ModEntry modEntry)
            {
                Save(this, modEntry);
            }

            public void OnChange()
            {
                if (origZoomAxis != Settings.CameraZoom.Axis)
                    Settings.CameraZoom.Button = VRTK_ControllerEvents.ButtonAlias.Undefined;
                else if (origZoomButton != Settings.CameraZoom.Button)
                    Settings.CameraZoom.Axis = VRTK_ControllerEvents.Vector2AxisAlias.Undefined;
                Instance.OnSettingsChanged();
            }
        }
    }
}
