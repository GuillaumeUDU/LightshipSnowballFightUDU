// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using System.IO;

using Niantic.ARDK.LocationService;
using Niantic.ARVoyage.Vps;

using Mapbox.Utils;
using Mapbox.Unity.Utilities;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Niantic.ARVoyage
{
    /// <summary>
    /// ScriptableObject class for storing DevSettings.
    /// This will be automatically created on Unity Editor load if it doesn't exist.
    /// These settings will only be loaded in editor and in development builds.
    /// </summary>
    public class DevSettings : ScriptableObject
    {
        private static string AssetName => "_" + typeof(DevSettings).Name;
        private static string Path => "Assets/Modules/Shared/DevSettings/Resources/" + AssetName + ".asset";

        // The DevSettings instance. This will be null in release builds on device
        private static DevSettings Instance;

        [Tooltip("Should we skip waits in the splash scene?")]
        [SerializeField] private bool skipSplashWait = false;
        public static bool SkipSplashWait => Instance != null && Instance.skipSplashWait;

        [Tooltip("Should we skip the AR warning?")]
        [SerializeField] public bool skipARWarning = false;
        public static bool SkipARWarning => Instance != null && Instance.skipARWarning;


        [Header("VPS")]
        [Tooltip("Simulate release build")]
        [SerializeField] public bool releaseBuildDebugMenu = false;
        public static bool ReleaseBuildDebugMenu => Instance != null ? Instance.releaseBuildDebugMenu : false;

        [Tooltip("Should we skip the VPS Intro?")]
        [SerializeField] public bool skipVPSIntro = false;
        public static bool SkipVPSIntro => Instance != null && Instance.skipVPSIntro;

        [Tooltip("Clear persistent data JSON on every run in editor?")]
        [SerializeField] public bool clearPersistentDataOnPlay = false;

        [Tooltip("Which state should override the Vps scene start state? This only affects running in editor.")]
        [SerializeField] private VpsSceneState vpsStartStateOverride;
        public static VpsSceneState VpsStartingStateOverride
        {
            get
            {
                if (Application.isEditor && Instance != null)
                {
                    return Instance.vpsStartStateOverride;
                }
                else
                {
                    return VpsSceneState.None;
                }
            }
        }

        [Tooltip("Mock user location in editor?")]
        [SerializeField] public LatLng mockUserLatLongInEditor;
        [SerializeField] public bool overrideMockUserLatLongWithFirstMockAsset = false;
        public static LatLng MockUserLatLongInEditor
        {
            get
            {
                if (Instance != null)
                {
                    if (Instance.overrideMockUserLatLongWithFirstMockAsset && Instance.mockVpsAssets.Length != 0)
                    {
                        return Instance.mockVpsAssets[0].vpsDataEntry.latitudeLongitude;
                    }
                    else
                    {
                        return Instance.mockUserLatLongInEditor;
                    }
                }
                return new LatLng(0, 0);
            }
        }

        public enum VpsSceneState
        {
            None,
            StateVpsLocalization
        }

        [Tooltip("Which mock VPS asset should we use when mocking in the experience state? The first valid entry in this list will be selected," +
            "but it's convenient to have a list to store values.")]
        [SerializeField] private VpsContentSO[] mockVpsAssets;
        public static VpsContentSO[] MockVpsAssets => Instance != null ? Instance.mockVpsAssets : new VpsContentSO[] { };

        public static string GetFirstMockIdentifier()
        {
            if (Instance == null)
            {
                return null;
            }

            if (Instance.mockVpsAssets.Length == 0)
            {
                return null;
            }

            return Instance.mockVpsAssets[0].vpsDataEntry.identifier;
        }


        [Header("SnowballFight")]

        [Tooltip("In Snowball Fight we skip to state main with mock peers in editor?")]
        [SerializeField] public bool skipToSnowballFightMainInEditor = false;
        public static bool SkipToSnowballFightMainInEditor => Instance != null && Instance.skipToSnowballFightMainInEditor;



#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadSettings()
        {
            Instance = LoadDevSettings();
            if (Instance != null)
            {
                Instance.ProcessSettingsBeforeAwake();
            }
        }
#endif

        // Do any load-time processing of settings before Awake methods are called on components.
        // This method is called in editor and in development builds on device
        public void ProcessSettingsBeforeAwake()
        {
            if (DevSettings.SkipARWarning)
            {
                StateWarning.occurred = true;
            }

            // Process any additional platform-agnostic settings here

#if UNITY_EDITOR
            ProcessEditorOnlySettings();
#else
            ProcessNonEditorSettings();
#endif
        }

        // Only called in Unity Editor
        private void ProcessEditorOnlySettings()
        {
            if (clearPersistentDataOnPlay) PersistentDataUtility.Clear();
        }

        // Only called outisde of Unity Editor in development builds
        private void ProcessNonEditorSettings()
        {
            // Ensure this setting is false outside of editor
            skipToSnowballFightMainInEditor = false;
        }

        private static DevSettings LoadDevSettings()
        {
            return Resources.Load<DevSettings>(AssetName);
        }

        // On editor load, create the DevSettings SO if it doesn't exist
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void CreateDevSettings()
        {
            if (!File.Exists(Path))
            {
                DevSettings soAsset = ScriptableObject.CreateInstance<DevSettings>();

                AssetDatabase.CreateAsset(soAsset, Path);
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log("Created " + soAsset.name);
            }
        }
#endif
    }
}
