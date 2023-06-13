using Niantic.ARDK.Extensions;
using Niantic.ARDK.LocationService;
using System.Collections.Generic;

using Mapbox.Utils;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// High-level management class for all debug functionality
    /// through the various VPS states.
    /// </summary>
    public class VpsDebugManager : MonoBehaviour, ISceneDependency
    {
        public const float DefaultMockPlacementDistanceInFrontOfCamera = 3f;
        public const float DefaultMockPlacementDistanceBelowCamera = 1f;

        public AppEvent Relocalize = new AppEvent();

        [SerializeField] DebugMenuGUI debugMenuGUI;

        private GameObject activeOptions = null;

        // shared
        [SerializeField] Toggle[] toggleOcclusionButtons;

        // StreetMap options
        [Header("StreetMap")]
        [SerializeField] public GameObject streetMapOptions;
        [SerializeField] Button advanceToLocalizationButton;
        [SerializeField] Button teleportUserHereButton;
        [SerializeField] Button allowFullZoomButton;
        [SerializeField] Button panToEmbarcaderoButton;
        [SerializeField] Button resetProgressButton;

        [Header("StreetMap Release")]
        [SerializeField] public GameObject streetMapOptionsRelease;
        [SerializeField] Button goToLosAngelesButton;
        [SerializeField] Button goToLondonButton;
        [SerializeField] Button goToNewYorkButton;
        [SerializeField] Button goToSanFranciscoButton;
        [SerializeField] Button goToSeattleButton;
        [SerializeField] Button goToTokyoButton;

        [Header("Bespoke")]
        [SerializeField] public GameObject bespokeOptions;
        [SerializeField] Toggle toggleReferenceMeshesButton;
        [SerializeField] Toggle togglePlacementButton;
        [SerializeField] Button resetPlacementButton;
        [SerializeField] GameObject placementCanvas;
        [SerializeField] PlacementControl transformRotationY;
        [SerializeField] PlacementControl transformPositionY;
        [SerializeField] PlacementControl transformScale;
        [SerializeField] PlacementControl transformPositionXZ;
        [SerializeField] TMPro.TMP_Text transformRotationText;
        [SerializeField] TMPro.TMP_Text transformPositionText;
        [SerializeField] TMPro.TMP_Text transformScaleText;
        [SerializeField] TMPro.TMP_Text cameraPositionText;
        [SerializeField] TMPro.TMP_Text cameraRotationText;

        [Header("BespokeFFRelease")]
        [SerializeField] public GameObject bespokeFFOptionsRelease;
        [SerializeField] Button relocalizeButtonRelease;

        private Vector3 placementBeginRotation;
        private Vector3 placementBeginPosition;
        private Vector3 placementBeginScale;

        private Vector3 placementOriginalRotation;
        private Vector3 placementOriginalPosition;
        private Vector3 placementOriginalScale;

        [Header("States")]
        [SerializeField] private StateVpsLocalization stateVpsLocalization;

        private VpsSceneManager vpsSceneManager;
        private StreetMapManager streetMapManager;
        private VpsCoverageManager vpsCoverageManager;

        Transform mockContentPositionHelperTransform;
        Transform mockContentFaceCameraHelperTransform;
        private Dictionary<string, string> drowndownEntryLabelsToIdentifiers = new Dictionary<string, string>();

        bool initialized;

        void Awake()
        {
            InitializeIfNeeded();

            // Placement transform handlers.
            {
                //Reset Placement
                resetPlacementButton.onClick.AddListener(() =>
                {
                    RestorePlacementOriginalTransform();
                });

                // RotationY

                transformRotationY.PlacementBegin.AddListener(() =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;
                    placementBeginRotation = target.transform.localRotation.eulerAngles;
                });

                transformRotationY.PlacementUpdate.AddListener((delta) =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;
                    target.transform.localRotation = Quaternion.Euler(placementBeginRotation.x, placementBeginRotation.y + (delta.x / 10f), placementBeginRotation.z);
                    RefreshPlacementDisplayValues(target);
                });

                // PositionY

                transformPositionY.PlacementBegin.AddListener(() =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;
                    placementBeginPosition = target.transform.localPosition;
                });

                transformPositionY.PlacementUpdate.AddListener((delta) =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;
                    target.transform.localPosition = new Vector3(placementBeginPosition.x, placementBeginPosition.y + (delta.y / 100f), placementBeginPosition.z);
                    RefreshPlacementDisplayValues(target);
                });

                // Position XZ

                transformPositionXZ.PlacementBegin.AddListener(() =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;
                    placementBeginPosition = target.transform.localPosition;
                });

                transformPositionXZ.PlacementUpdate.AddListener((delta) =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;

                    Vector3 cameraForward = target.transform.parent.InverseTransformDirection(Camera.main.transform.forward);
                    cameraForward.y = 0;
                    cameraForward.Normalize();

                    Vector3 cameraRight = target.transform.parent.InverseTransformDirection(Camera.main.transform.right);
                    cameraRight.y = 0;
                    cameraRight.Normalize();

                    target.transform.localPosition = placementBeginPosition + (cameraForward * (delta.y / 100f) + (cameraRight * (delta.x / 100f)));
                    RefreshPlacementDisplayValues(target);
                });

                // Scale

                transformScale.PlacementBegin.AddListener(() =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;
                    placementBeginScale = target.transform.localScale;
                });

                transformScale.PlacementUpdate.AddListener((delta) =>
                {
                    Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                    if (target == null) return;
                    target.transform.localScale = new Vector3(placementBeginScale.x + (delta.x / 100f), placementBeginScale.y + (delta.x / 100f), placementBeginScale.z + (delta.x / 100f));
                    RefreshPlacementDisplayValues(target);
                });
            }
        }

        void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            vpsCoverageManager = SceneLookup.Get<VpsCoverageManager>();

            mockContentPositionHelperTransform = new GameObject("MockContentPositionHelper").transform;
            mockContentFaceCameraHelperTransform = new GameObject("MockFaceCameraHelper").transform;
        }

        void Start()
        {
            // If there's a starting state override, activate it now 
            // that managers have had a chance to initialize in their Awake methods
            switch (DevSettings.VpsStartingStateOverride)
            {
                case DevSettings.VpsSceneState.StateVpsLocalization:
                    stateVpsLocalization.gameObject.SetActive(true);
                    break;
            }
        }

        void OnEnable()
        {
            advanceToLocalizationButton.onClick.AddListener(AdvanceToLocalization);
            teleportUserHereButton.onClick.AddListener(TeleportUserHere);
            allowFullZoomButton.onClick.AddListener(AllowFullZoom);

            panToEmbarcaderoButton.onClick.AddListener(GoToEmbarcadero);
            goToLosAngelesButton.onClick.AddListener(GoToLosAngeles);
            goToLondonButton.onClick.AddListener(GoToLondon);
            goToNewYorkButton.onClick.AddListener(GoToNewYork);
            goToSanFranciscoButton.onClick.AddListener(GoToSanFrancisco);
            goToSeattleButton.onClick.AddListener(GoToSeattle);
            goToTokyoButton.onClick.AddListener(GoToTokyo);

            togglePlacementButton.onValueChanged.AddListener(TogglePlacement);
            foreach (var toggleOcclusionButton in toggleOcclusionButtons)
            {
                toggleOcclusionButton.onValueChanged.AddListener(OnToggleOcclusionValueChanged);
            }
            toggleReferenceMeshesButton.onValueChanged.AddListener(ToggleReferenceMeshes);
            relocalizeButtonRelease.onClick.AddListener(OnRelocalizeButtonReleaseClick);
            resetProgressButton.onClick.AddListener(OnResetProgressClick);
        }

        void OnDisable()
        {
            advanceToLocalizationButton.onClick.RemoveListener(AdvanceToLocalization);
            teleportUserHereButton.onClick.RemoveListener(TeleportUserHere);
            allowFullZoomButton.onClick.RemoveListener(AllowFullZoom);

            panToEmbarcaderoButton.onClick.RemoveListener(GoToEmbarcadero);
            goToLosAngelesButton.onClick.RemoveListener(GoToLosAngeles);
            goToLondonButton.onClick.RemoveListener(GoToLondon);
            goToNewYorkButton.onClick.RemoveListener(GoToNewYork);
            goToSanFranciscoButton.onClick.RemoveListener(GoToSanFrancisco);
            goToSeattleButton.onClick.RemoveListener(GoToSeattle);
            goToTokyoButton.onClick.RemoveListener(GoToTokyo);

            togglePlacementButton.onValueChanged.RemoveListener(TogglePlacement);
            foreach (var toggleOcclusionButton in toggleOcclusionButtons)
            {
                toggleOcclusionButton.onValueChanged.RemoveListener(OnToggleOcclusionValueChanged);
            }
            toggleReferenceMeshesButton.onValueChanged.RemoveListener(ToggleReferenceMeshes);
            relocalizeButtonRelease.onClick.RemoveListener(OnRelocalizeButtonReleaseClick);
            resetProgressButton.onClick.RemoveListener(OnResetProgressClick);
        }

        void Update()
        {
            if (placementCanvas.gameObject.activeSelf)
            {
                Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
                if (target != null)
                {
                    Vector3 localTransform = target.InverseTransformPoint(vpsSceneManager.arCamera.transform.position);
                    cameraPositionText.text = localTransform.ToString("F4");

                    Vector3 localCameraForward = target.InverseTransformDirection(vpsSceneManager.arCamera.transform.forward);
                    Vector3 localCameraUp = target.InverseTransformDirection(vpsSceneManager.arCamera.transform.up);

                    Quaternion localRotation = Quaternion.LookRotation(localCameraForward, localCameraUp);
                    cameraRotationText.text = localRotation.eulerAngles.ToString("F4");
                }
            }
        }

        public void SetOptionsActive(GameObject options, bool show)
        {
            if (options == null) return;

            // Always disable placement with altering options.
            togglePlacementButton.isOn = false;

            // Disable previous panel.
            if (options != activeOptions && activeOptions != null) activeOptions.SetActive(false);

            // Set state for current panel;
            options.SetActive(show);

            // Store current panel.
            activeOptions = (show) ? options : null;
        }

        public bool IsDebugMenuOpen()
        {
            return debugMenuGUI.gameObject.activeSelf;
        }

        void AdvanceToLocalization()
        {
            streetMapManager.advanceToLocalization = true;
            debugMenuGUI.HideGUI();
        }

        void TeleportUserHere()
        {
            streetMapManager.TeleportUserHere();
            debugMenuGUI.HideGUI();
        }

        void AllowFullZoom()
        {
            streetMapManager.AllowFullZoom();
            debugMenuGUI.HideGUI();
        }

        void GoToEmbarcadero() { GoToLatLon(VpsCoverageManager.TeleportLocation.Embarcadero); }
        void GoToLosAngeles() { GoToLatLon(VpsCoverageManager.TeleportLocation.LosAngeles); }
        void GoToLondon() { GoToLatLon(VpsCoverageManager.TeleportLocation.London); }
        void GoToNewYork() { GoToLatLon(VpsCoverageManager.TeleportLocation.NewYork); }
        void GoToSanFrancisco() { GoToLatLon(VpsCoverageManager.TeleportLocation.SanFrancisco); }
        void GoToSeattle() { GoToLatLon(VpsCoverageManager.TeleportLocation.Seattle); }
        void GoToTokyo() { GoToLatLon(VpsCoverageManager.TeleportLocation.Tokyo); }

        void GoToLatLon(LatLng latLng)
        {
            streetMapManager.CenterOnAndSearchArea(latLng.ToVector2d(), zoom: streetMapManager.minMapZoom);
            debugMenuGUI.HideGUI();
        }

        public void OnToggleOcclusionValueChanged(bool value)
        {
            ARDepthManager arDepthManager = vpsSceneManager.arCamera.GetComponent<ARDepthManager>();
            arDepthManager.OcclusionTechnique = (value) ? ARDepthManager.OcclusionMode.Auto : ARDepthManager.OcclusionMode.None;
            foreach (var toggleOcclusionButton in toggleOcclusionButtons)
            {
                toggleOcclusionButton.SetIsOnWithoutNotify(value);
            }
        }

        public void ToggleReferenceMeshes(bool value)
        {
            foreach (GameObject referenceMesh in vpsSceneManager?.VpsBespokeContent?.referenceMeshes)
            {
                referenceMesh?.SetActive(!referenceMesh.activeSelf);
            }
        }

        public void OnResetProgressClick()
        {
            // Clear progress
            PersistentDataUtility.Clear();
            SaveUtil.Clear();
            // For dev convenience, still skip the intro on reload
            SaveUtil.SaveString(StateVpsIntro.SaveKeyUserViewedStateVpsIntro);
            SceneLookup.Get<LevelSwitcher>().ReloadCurrentLevel(fadeOutBeforeLoad: true);
        }

        public void OnRelocalizeButtonReleaseClick()
        {
            Relocalize.Invoke();
            debugMenuGUI.HideGUI();
        }

        public void TogglePlacement(bool value)
        {
            Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
            if (target != null && value)
            {
                RefreshPlacementDisplayValues(target);
            }

            placementCanvas.SetActive(value);

            debugMenuGUI.HideGUI();
        }

        public void RefreshPlacementDisplayValues(Transform target)
        {
            // Update placement display values.
            transformPositionText.text = target.transform.localPosition.ToString("F4");
            transformRotationText.text = target.transform.localRotation.eulerAngles.ToString("F4");
            transformScaleText.text = target.transform.localScale.ToString("F4");
        }

        public void SavePlacementOriginalTransform()
        {
            // Cache original values.
            Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
            if (target != null)
            {
                placementOriginalPosition = target.transform.localPosition;
                placementOriginalRotation = target.transform.localRotation.eulerAngles;
                placementOriginalScale = target.transform.localScale;
            }
        }

        public void RestorePlacementOriginalTransform()
        {
            // Restore original values.
            Transform target = vpsSceneManager?.VpsBespokeContent?.contentParent;
            if (target != null)
            {
                target.transform.localPosition = placementOriginalPosition;
                target.transform.localRotation = Quaternion.Euler(placementOriginalRotation);
                target.transform.localScale = placementOriginalScale;

                RefreshPlacementDisplayValues(target);
            }
        }

        /// <summary>
        /// Place mock localized content relative to the current camera position
        /// </summary>
        public void PlaceAtMockLocalizedPose(string identifier, Transform transformToPlace)
        {
            // Ensure initialization, since this can be called before Awake when skipping states
            InitializeIfNeeded();

            VpsDataEntry vpsDataEntry = vpsCoverageManager.GetVpsDataEntryByIdentifier(identifier);
            Transform mockFaceCameraTransform = vpsDataEntry.GetMockCameraHeightStagingPointTransform();

            float mockPlacementDistanceInFrontOfCamera = 0f;

            // Make the face-camera helper the child of the content to set its local offset
            mockContentPositionHelperTransform.SetParent(null);
            mockContentFaceCameraHelperTransform.SetParent(mockContentPositionHelperTransform);
            if (mockFaceCameraTransform != null)
            {
                mockContentFaceCameraHelperTransform.localPosition = mockFaceCameraTransform.localPosition;
                mockContentFaceCameraHelperTransform.localRotation = mockFaceCameraTransform.localRotation;
            }
            else
            {
                mockPlacementDistanceInFrontOfCamera = DefaultMockPlacementDistanceInFrontOfCamera;
                mockContentFaceCameraHelperTransform.localPosition = new Vector3(0, DefaultMockPlacementDistanceBelowCamera, 0);
            }

            // Swap the parentage so we can move the face-camera transform to the correct position, 
            // bringing the content along while maintaining the positional and rotational relationship
            mockContentFaceCameraHelperTransform.SetParent(null);
            mockContentPositionHelperTransform.SetParent(mockContentFaceCameraHelperTransform);

            // Select a point in front of the camera along the x-z plane
            Transform cameraTransform = Camera.main.transform;
            Vector3 cameraForwardXZ = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);

            // Place the face-camera helper at that point, facing the camera
            // The content, which is now a child of it, will end up in the correct position
            mockContentFaceCameraHelperTransform.position =
                cameraTransform.position + cameraForwardXZ * mockPlacementDistanceInFrontOfCamera;
            mockContentFaceCameraHelperTransform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y + 180, 0);

            transformToPlace.position = mockContentPositionHelperTransform.position;
            transformToPlace.rotation = mockContentPositionHelperTransform.rotation;
        }

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(VpsDebugManager))]
        public class VpsDebugManagerEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                if (Application.isPlaying)
                {
                    VpsDebugManager vpsDebugManager = target as VpsDebugManager;

                    if (GUILayout.Button("Advance to Localization"))
                    {
                        vpsDebugManager.AdvanceToLocalization();
                    }
                }
            }
        }
#endif
    }
}
