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
            Settings = UnityModManager.ModSettings.Load<VRUtilitiesModSettings>(modEntry);
            ModEntry.OnSaveGUI = OnSave;
            ModEntry.OnGUI = OnGUI;
            ModEntry.OnUnload = Unload;

            var go = new GameObject("[VRUtilitiesMod]");
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<VRUtilitiesMod>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance.Settings = Settings;

            return true;
        }

        private static bool Unload(UnityModManager.ModEntry modEntry)
        {
            if (Instance != null) UnityEngine.Object.DestroyImmediate(Instance);
            return true;
        }

        private static void OnSave(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(ModEntry);
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
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

        public class VRUtilitiesModSettings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Disable Touch Interaction", Tooltip = "Stops controls from automatically interacticting by touch, require button press")]
            public bool DisableTouch;

            [Draw("Override Use Button", Vertical = true)]
            public UseOverrideGroup UseOverride = new UseOverrideGroup();
            [Draw("Disable Jumping")]
            public bool DisableJump;
            
            public class UseOverrideGroup
            {
                [Draw("Enabled", Tooltip = "Override VR controller button use to interact with buttons/switches")]
                public bool Enabled;
                [Draw("Use Button", Tooltip = "If override is enabled, which button to use", VisibleOn = "Enabled|true")]
                public VRTK_ControllerEvents.ButtonAlias Button;
            }

            public override void Save(UnityModManager.ModEntry modEntry)
            {
                Save(this, modEntry);
            }

            public void OnChange()
            {
                Instance.OnSettingsChanged();
            }
        }
    }
}
