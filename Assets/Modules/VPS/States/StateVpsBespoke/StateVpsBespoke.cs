using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where Doty first appears in the current bespoke "tourist" location. 
    /// The bespoke Doty, props, etc. for this location are supplied in a custom prefab acquired 
    ///  via the VpsSceneManager in InstantiateBespokeContent.
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsBespokePlantFlag if the user had previously visited this location and a planted flag already exists.
    ///    (Doty's appearance is initially suppressed in this case.)
    /// - StateVpsBespokeGuideToDoty otherwise.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsBespoke : StateBase
    {
        //private const string hintText = "Hey there! Let's do something cool at this VPS location!";

        [Header("State machine")]
        [SerializeField] private GameObject nextState;
        [SerializeField] private GameObject flagAlreadyPlantedState;
        [SerializeField] private GameObject stateStreetMap;
        [SerializeField] private GameObject stateVpsLocalization;
        [SerializeField] private GameObject stateVpsLocalizationDestabilized;
        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;
        protected float initialDelay = 1f;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private Button backToStreetMapButton;

        private VpsSceneManager vpsSceneManager;
        private VpsCoverageManager vpsCoverageManager;
        private VpsWayspotManager vpsWayspotManager;
        private VpsPane vpsPane;
        private StreetMapManager streetMapManager;
        private StreetMapMarkerManager streetMapMarkerManager;
        private VpsDebugManager vpsDebugManager;
        private AudioManager audioManager;
        private Fader fader;

        private VpsDotyWithCamera vpsDotyWithCamera;
        private float timeToEnableDoty;
        private float timeToLeaveState;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            vpsCoverageManager = SceneLookup.Get<VpsCoverageManager>();
            vpsWayspotManager = SceneLookup.Get<VpsWayspotManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            streetMapMarkerManager = SceneLookup.Get<StreetMapMarkerManager>();
            vpsDebugManager = SceneLookup.Get<VpsDebugManager>();
            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();

            // By default, we're not the first state; start off disabled
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;

            // show GUI
            gui.SetActive(true);

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            // instantiate content
            // e.g. prefab with NPC and mesh
            InstantiateBespokeContent();

            // enable debug menu options
            vpsDebugManager.SetOptionsActive(
                VpsSceneManager.IsReleaseBuild() ? vpsDebugManager.bespokeFFOptionsRelease : vpsDebugManager.bespokeOptions,
                true);

            vpsDebugManager.SavePlacementOriginalTransform();

#if !UNITY_EDITOR
            // Outside of editor, disable the reference meshes by default
            foreach (var mesh in vpsSceneManager.VpsBespokeContent.referenceMeshes)
            {
                mesh.SetActive(false);
            }
#endif

            // hide Doty at first
            vpsDotyWithCamera = vpsSceneManager.VpsBespokeContent.vpsDotyWithCamera;
            if (vpsDotyWithCamera != null) vpsDotyWithCamera.gameObject.SetActive(false);
            // hide Doty's camera
            if (vpsDotyWithCamera != null) vpsDotyWithCamera.photoCamera.SetActive(false);

            // Get flag state from persistent dictionary.
            string localizationTargetId = vpsSceneManager.CurrentVpsDataEntry.identifier;
            bool poiFlagPlanted = vpsSceneManager.PersistentBespokeStateLookup.GetOrDefault(localizationTargetId);
            Debug.Log("POI Flag Planted state: " + poiFlagPlanted);

            // IF FLAG IS ALREADY PLANTED, jump to flag state
            if (poiFlagPlanted)
            {
                StateVpsBespokePlantFlag.startWithFlagPlanted = true;
                Exit(flagAlreadyPlantedState);
                return;
            }

            // Delay before enabling Doty 
            timeToEnableDoty = Time.time + 1f;

            // Delay before leaving state
            timeToLeaveState = Time.time + 2.5f;

            running = true;
        }

        void OnDisable()
        {
            backToStreetMapButton.onClick.RemoveListener(OnBackToStreetMapButtonPress);
            vpsDebugManager.Relocalize.RemoveListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.RemoveListener(OnLocalizationDestabilized);
        }

        void Update()
        {
            if (!running) return;

            // Be sure we're faded in
            if (!fader.IsSceneFadedIn)
            {
                fader.FadeSceneInImmediate();
            }

            // show Doty after delay
            if (timeToEnableDoty > 0f && Time.time >= timeToEnableDoty)
            {
                timeToEnableDoty = 0f;
                if (vpsDotyWithCamera != null)
                {
                    vpsDotyWithCamera.gameObject.SetActive(true);
                    audioManager.PlayAudioOnObject(AudioKeys.SFX_Doty_Appear,
                                                    targetObject: vpsDotyWithCamera.gameObject);
                }
            }

            // exit state after a delay
            if (timeToLeaveState > 0f && Time.time >= timeToLeaveState)
            {
                timeToLeaveState = 0f;
                exitState = nextState;
            }

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
        }

        private void InstantiateBespokeContent()
        {
            string localizationTargetId = null;

            if (vpsSceneManager.CurrentVpsDataEntry != null)
            {
                localizationTargetId = vpsSceneManager.CurrentVpsDataEntry.identifier;
            }
            else
            {
                // In a mock session, it's possible we don't have localization info,
                // since we can skip directly to this state
                if (vpsSceneManager.IsMockARSession)
                {
                    // If there's a DevSettings mock VPS content asset with bespoke content, use it
                    VpsContentSO[] mockContentAssets = DevSettings.MockVpsAssets;
                    foreach (var vpsContentAsset in mockContentAssets)
                    {
                        if (vpsContentAsset.vpsDataEntry.bespokeEnabled)
                        {
                            localizationTargetId = vpsContentAsset.vpsDataEntry.identifier;
                            break;
                        }
                    }

                    // If no DevSettings mock bespoke content asset is found, use the first in the content manager
                    if (localizationTargetId == null)
                    {
                        List<VpsDataEntry> bespokeDataEntries = vpsCoverageManager.BespokeEnabledDataEntries;
                        if (bespokeDataEntries != null && bespokeDataEntries.Count > 0)
                        {
                            localizationTargetId = bespokeDataEntries[0].identifier;

                            if (mockContentAssets.Length > 0)
                            {
                                Debug.LogWarning("None of the specified mock VPS assets are bespokeEnabled");
                            }

                            Debug.LogWarning("Using the first bespokeEnabled SO in master list: " + bespokeDataEntries[0].name);
                        }
                        else
                        {
                            // This should never happen because the app contains a fixed list of bespoke content assets
                            // but if it does, report the error and bail to the street map
                            Debug.LogError(this + " " + nameof(InstantiateBespokeContent) + " didn't find any bespoke content assets");
                            exitState = stateStreetMap;
                            return;
                        }
                    }

                    Debug.Log(this + " using mock localizationTargetId " + localizationTargetId);
                }
                else
                {
                    Debug.LogError($"Got call to {nameof(InstantiateBespokeContent)} in non-mock session without a CurrentVpsDataEntry");
                    return;
                }
            }

            // instantiate content, assigned to localizeContent
            Debug.Log(this + " instantiating content for localizationTargetId " + localizationTargetId);

            // Get the wayspot anchor tranform to be used as the bespoke content's parent
            // There should be exactly one anchor per bespoke localizationTargetId
            List<Transform> wayspotAnchorTransforms = vpsWayspotManager.GetCurrentAnchorTransforms();

            GameObject bespokeContent = null;

            // There should be exactly one anchor at a bespoke wayspot. If it's all loaded correctly, instantiate and place the content at it
            if (wayspotAnchorTransforms.Count > 0)
            {
                if (wayspotAnchorTransforms.Count > 1)
                {
                    Debug.LogError($"{nameof(InstantiateBespokeContent)} had more than one wayspot anchor. Anchoring content to the first.");
                }

                // Instantiate the bespoke content prefab as a child of the first wayspotAnchorTransform
                bespokeContent = Instantiate(vpsSceneManager.CurrentVpsDataEntry.prefab, wayspotAnchorTransforms[0]);
            }
            // If there are no loaded anchors, this is an error unless it's a dev test location where we will dynamically instantiate the anchor
            else
            {
                // If this is a mock session or we're using an injected dev location, 
                // place the content and save the test anchor payload for future use
                if (vpsSceneManager.IsMockARSession || vpsSceneManager.CurrentVpsDataEntry.injectIntoCoverageResults)
                {
                    // Instantiate the content at the hierarchy root
                    bespokeContent = Instantiate(vpsSceneManager.CurrentVpsDataEntry.prefab);

                    // Place the content at the mock localized poseS
                    vpsDebugManager.PlaceAtMockLocalizedPose(localizationTargetId, bespokeContent.transform);

                    // Anchor the content transform, and save the resulting anchor payload once the anchor is created
                    vpsWayspotManager.PlaceAnchor(bespokeContent.transform, () =>
                    {
                        Debug.Log("Saving anchor payload for bespoke content");
                        vpsWayspotManager.SaveCurrentAnchorPayloads();
                    });
                }
                else
                {
                    Debug.LogError($"{nameof(InstantiateBespokeContent)} didn't have wayspot anchors.");
                }
            }

            if (bespokeContent != null)
            {
                bespokeContent.name = bespokeContent.name + "_" + localizationTargetId;

                // Get VpsBespokeContent component
                vpsSceneManager.VpsBespokeContent = bespokeContent.GetComponentInChildren<VpsBespokeContent>();
                if (vpsSceneManager.VpsBespokeContent == null)
                {
                    Debug.LogError("No VpsBespokeContent component in bespokeContent");
                }
            }
            else
            {
                Debug.LogError("Instantiated bespoke content was null.");
            }
        }

        public void OnBackToStreetMapButtonPress()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            exitState = stateStreetMap;
        }

        private void OnRelocalization()
        {
            exitState = stateVpsLocalization;
        }

        private void OnLocalizationDestabilized()
        {
            exitState = stateVpsLocalizationDestabilized;
        }

        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // when leaving AR, fade out, StopAR and destroy bespoke content
            if (nextState == stateStreetMap || nextState == stateVpsLocalization)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);
                if (nextState == stateStreetMap) vpsSceneManager.StopAR();
                Destroy(vpsSceneManager.VpsBespokeContent.gameObject);
            }

            // hide GUI
            gui.SetActive(false);
            backToStreetMapButton.gameObject.SetActive(false);

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }
}
