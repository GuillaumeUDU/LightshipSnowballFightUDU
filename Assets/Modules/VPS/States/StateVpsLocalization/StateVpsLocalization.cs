// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;

using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Extensions;
using Niantic.ARDK.AR.WayspotAnchors;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where the camera and AR are activated, and the user is prompted to try to localize
    /// on a given object nearby. A UI pane at the bottom of the screen displays a photo image the object.
    /// The state initially waits to fade in until the first OnFrameUpdated occurs from the device's camera.
    /// Various hint text is displayed. If localization fails, error text and additional cycling hints are displayed.
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsBespoke if the selected location is a bespoke "tourist" location, with custom content.
    /// - StateVpsFrostFlower otherwise, where users will throw seeds to plant a frostflower garden.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// </summary>
    public class StateVpsLocalization : StateBase
    {
        private const string hintTextLocalizing = "Try to frame the Wayspot as shown in the target image below!";
        private const string buttonTextLocalizing = "Localizing...";

        private const string hintTextSuccess = "Localization successful!";
        private const string hintTextSuccessTourist = "Localization successful! Find Captain Doty's footprints to start!";
        private const string buttonTextSuccess = "Success!";

        private const string hintTextFailure_2a_3a_11a = "Could not localize. Let’s try that again!";
        private const string hintTextPostFailure_2b_3b = "Ensure your view of the Wayspot looks similar to the target image shown!";
        private const string hintTextPostFailure_3c = "Try walking around the Wayspot to view it from different angles.";
        private const string hintTextPostFailure_3d = "Localization will only work in daytime lighting conditions.";
        private const string hintTextFailure_5678910a = "Could not connect. Let’s try that again!";
        private const string hintTextPostFailure_6b = "Make sure you have a wifi or mobile data connection.";
        private const string hintTextPostFailure_10b = "Ensure that location permissions are enabled for this application.";
        private const string hintTextPostFailure_11b = "Make sure you are within a few meters of the Wayspot.";
        private const string hintTextFailure_12 = "Lightship server failure. Try again later!";
        private const string buttonTextFailure = "Restarting...";

        private List<string> postFailureHints = new List<string>();
        private float nextPostFailureHintTime;
        private int postFailureHintCtr = 0;
        private const float postFailureHintCyclePeriodSecs = 8f;

        [Header("State machine")]
        [SerializeField] private GameObject nextStateBespoke;
        [SerializeField] private GameObject nextStateFrostFlower;
        [SerializeField] private GameObject prevStateStreetMap;
        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;
        protected float initialDelay = 1f;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private GameObject scanPrompt;
        [SerializeField] private Button backToStreetMapButton;
        private Fader fader;

        private VpsSceneManager vpsSceneManager;
        private VpsWayspotManager vpsWayspotManager;
        private VpsPane vpsPane;
        private StreetMapManager streetMapManager;
        private StreetMapMarkerManager streetMapMarkerManager;
        private VpsDebugManager vpsDebugManager;
        private DebugMenuButton debugMenuButton;
        private AudioManager audioManager;
        private FeaturePointHelper featurePointHelper;
        private IARSession arSession;

        private bool arWasAlreadyActive = false;

        private bool succeeded = false;
        private float successTime = 0f;

        private bool failed = false;
        private float failureTime = 0f;


        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            vpsWayspotManager = SceneLookup.Get<VpsWayspotManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            streetMapMarkerManager = SceneLookup.Get<StreetMapMarkerManager>();
            vpsDebugManager = SceneLookup.Get<VpsDebugManager>();
            debugMenuButton = SceneLookup.Get<DebugMenuButton>();
            audioManager = SceneLookup.Get<AudioManager>();
            featurePointHelper = SceneLookup.Get<FeaturePointHelper>();
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

            // If we've jumped straight to this state without defining the current vps location,
            // then create a mock vps location
            if (vpsSceneManager.CurrentVpsDataEntry == null)
            {
                streetMapManager.ActivateStreetMap(false);
                streetMapMarkerManager.CurrentLocation = streetMapMarkerManager.GetMockVpsLocation();
                vpsSceneManager.CurrentVpsDataEntry = streetMapMarkerManager.CurrentLocation.VpsDataEntry;
                Debug.Log("Using mock VPS location " + vpsSceneManager.CurrentVpsDataEntry);
            }

            vpsSceneManager.CurrentlyLocalizing = true;

            // N.B. we wait to fade in from white until the camera is active, via OnFrameUpdated

            // show GUI
            gui.SetActive(true);
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.HintImageLargeNoText,
                                            showBottomPaneBackdrop: false);
            vpsPane.paneButton.interactable = false;
            vpsPane.titleText.text = "";
            vpsPane.gui.SetActive(true);

            // disable the debug button
            debugMenuButton.gameObject.SetActive(false);

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            // Listen to ARDK localization state updates
            VpsWayspotManager.LocalizationStateUpdated.AddListener(OnLocalizationStateUpdated);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            // activate AR camera
            // start AR
            // start localization, including hint text, statusIcons and scanPrompt
            arSession = null;

            // if the scene is already faded in, we know we came from StateVpsARWarning
            // if we're still faded out, it means we came from StateVpsStreetMap; we need to StartAR and fade in
            arWasAlreadyActive = fader.IsSceneFadedIn;
            if (!arWasAlreadyActive)
            {
                Debug.Log("StateVpsLocalization starting AR, and will fade in when device camera is active");

                // Meshing collision is only enabled for non-bespoke locations. 
                bool meshingCollisionEnabled = !vpsSceneManager.CurrentVpsDataEntry.bespokeEnabled;

                ARSessionFactory.SessionInitialized += OnARSessionInitialized;
                vpsSceneManager.SetARCameraActive();
                vpsSceneManager.StartAR(meshingCollisionEnabled);
            }

            // Immediately start localizing
            StartLocalization();

            // Show feature points
            featurePointHelper.Spawning = true;
            featurePointHelper.Tracking = true;

            running = true;
        }

        private void OnDisable()
        {
            vpsSceneManager.CurrentlyLocalizing = false;

            VpsWayspotManager.LocalizationStateUpdated.RemoveListener(OnLocalizationStateUpdated);
            VpsWayspotManager.LocalizationDestabilized.RemoveListener(OnLocalizationDestabilized);
            if (!arWasAlreadyActive)
            {
                ARSessionFactory.SessionInitialized -= OnARSessionInitialized;
                if (arSession != null) arSession.FrameUpdated -= OnFrameUpdated;
            }
        }

        private void OnARSessionInitialized(AnyARSessionInitializedArgs args)
        {
            arSession = args.Session;
            arSession.FrameUpdated += OnFrameUpdated;
        }

        private void OnFrameUpdated(FrameUpdatedArgs args)
        {
            Debug.Log("StateVpsLocalization OnFrameUpdated");

            // unsubscribe so this method is only called this one time
            arSession.FrameUpdated -= OnFrameUpdated;

            // Now that device's camera is active, fade in
            fader.FadeSceneIn(Color.white, 0.5f);
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, initialDelay: 0.75f));
        }

        private void StartLocalization()
        {
            Debug.Log("Starting localization for '" + vpsSceneManager.CurrentVpsDataEntry.name + "' Identifier " + vpsSceneManager.CurrentVpsDataEntry.identifier);
            vpsPane.ShowHint(hintTextLocalizing);
            vpsPane.paneButtonText.text = buttonTextLocalizing;
            nextPostFailureHintTime = 0f;
            postFailureHintCtr = 0;

            // Disable scanPrompt
            scanPrompt.SetActive(false);

            // Starting the VpsWayspotManager's wayspot anchor service begins the localization process
            // We listen for its LocalizationStateUpdated event to know when localization has succeeded or failed,
            // which is handled in OnLocalizationStateUpdated
            vpsWayspotManager.StartWayspotAnchorService();

            succeeded = false;
            failed = false;
            UpdateStatusIcons();
        }

        private void OnLocalizationStateUpdated(LocalizationState localizationState, LocalizationFailureReason localizationFailureReason)
        {
            switch (localizationState)
            {
                // These states are unused
                case LocalizationState.Uninitialized:
                case LocalizationState.Initializing:
                case LocalizationState.Localizing:
                case LocalizationState.Stopped:
                    break;
                // success
                case LocalizationState.Localized:
                    // Localization succeeded - move onto anchor loading and content experience
                    OnLocalizationSucceeded();
                    break;
                // failure
                case LocalizationState.Failed:
                    // Localization failed - communicates failure to the user and then retries
                    OnLocalizationFailed(localizationFailureReason);
                    break;
                default:
                    Debug.LogError($"{nameof(StateVpsLocalization)}.{nameof(OnLocalizationStateUpdated)} got unhandled state: {localizationState}");
                    break;
            }
        }

        private void OnLocalizationSucceeded()
        {
            bool tryToLoadAnchors = true;

            // In mock sessions, don't try to load bespoke anchors. Those anchor payloads only work on device
            // StateVpsBespoke will create an anchor to use
            if (vpsSceneManager.IsMockARSession)
            {
                tryToLoadAnchors = !vpsSceneManager.CurrentVpsDataEntry.bespokeEnabled;
            }

            // If there are wayspot anchors associated with the current localization target, restore them before succeeding
            if (tryToLoadAnchors && vpsWayspotManager.HasAnchorPayloadsForCurrentLocalizationTarget())
            {
                vpsWayspotManager.RestoreWayspotAnchorsForCurrentLocalizationTarget(
                    onAnchorStartedTracking: null,
                    onAllAnchorsStartedTracking: OnLocalizationAndAnchorRestorationSucceeded);
            }
            // Otherwise, succeed immediately
            else
            {
                OnLocalizationAndAnchorRestorationSucceeded();
            }
        }

        private void OnLocalizationAndAnchorRestorationSucceeded()
        {
            Debug.Log($"{nameof(OnLocalizationAndAnchorRestorationSucceeded)} for {vpsSceneManager.CurrentVpsDataEntry.identifier}");

            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.HintImageLargeNoText,
                                            showBottomPaneBackdrop: false);
            vpsPane.ShowHint(vpsSceneManager.CurrentVpsDataEntry.bespokeEnabled ?
                hintTextSuccessTourist : hintTextSuccess);
            vpsPane.paneButtonText.text = buttonTextSuccess;

            scanPrompt.SetActive(false);
            succeeded = true;
            failed = false;
            UpdateStatusIcons();
            successTime = Time.time;
        }

        private void OnLocalizationDestabilized()
        {
            // In this state, treat destabilization as a failure so we communicate it to the user and restart localization
            OnLocalizationFailed(LocalizationFailureReason.None, hintTextOverride: "Localization destabilized.");
        }

        private void OnLocalizationFailed(LocalizationFailureReason failureReason, string hintTextOverride = null)
        {
            // Uncomment to force a particular failure reason
            //failureReason = LocalizationFailureReason.Canceled;

            Debug.Log("Localization failed for " + failureReason);
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.HintImageLargeNoText,
                                            showBottomPaneBackdrop: false);

            string hintTextFailure;
            postFailureHints.Clear();

            if (hintTextOverride == null)
            {
                switch (failureReason)
                {
                    case LocalizationFailureReason.Timeout: // 2
                        hintTextFailure = hintTextFailure_2a_3a_11a;
                        postFailureHints.Add(hintTextPostFailure_2b_3b);
                        break;
                    case LocalizationFailureReason.Canceled: // 3
                        hintTextFailure = hintTextFailure_2a_3a_11a;
                        postFailureHints.Add(hintTextPostFailure_2b_3b);
                        postFailureHints.Add(hintTextPostFailure_3c);
                        postFailureHints.Add(hintTextPostFailure_3d);
                        break;
                    case LocalizationFailureReason.CannotConnectToServer: // 5
                    case LocalizationFailureReason.BadResponse: // 7
                    case LocalizationFailureReason.BadIdentifier: // 8
                    case LocalizationFailureReason.LocationDataNotAvailable: // 9
                        hintTextFailure = hintTextFailure_5678910a;
                        break;
                    case LocalizationFailureReason.BadRequest: // 6
                        hintTextFailure = hintTextFailure_5678910a;
                        postFailureHints.Add(hintTextPostFailure_6b);
                        break;
                    case LocalizationFailureReason.NotSupportedAtLocation: // 10
                        hintTextFailure = hintTextFailure_5678910a;
                        postFailureHints.Add(hintTextPostFailure_10b);
                        break;
                    case LocalizationFailureReason.InternalServerFailure: // 11
                        hintTextFailure = hintTextFailure_2a_3a_11a;
                        postFailureHints.Add(hintTextPostFailure_11b);
                        break;
                    case LocalizationFailureReason.InvalidAPIKey: // 12
                        hintTextFailure = hintTextFailure_12;
                        break;
                    default:
                        hintTextFailure = "Error: " + failureReason;
                        break;
                }

                // append failureReason's numeric value to hint text
                hintTextFailure = hintTextFailure + " (" + ((int)failureReason) + ")";
            }
            else
            {
                hintTextFailure = hintTextOverride;
            }

            postFailureHints.Add(hintTextLocalizing);

            vpsPane.ShowError(hintTextFailure);
            vpsPane.paneButtonText.text = buttonTextFailure;
            scanPrompt.SetActive(false);
            failed = true;
            succeeded = false;
            UpdateStatusIcons();
            failureTime = Time.time;

            // Stop the wayspot anchor service - we'll restart it when retrying after displaying the error
            // We keep listening for the VpsDebugManager localization events which can come in at any time
            vpsWayspotManager.StopWayspotAnchorService();
        }

        void Update()
        {
            if (!running) return;

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }

            // 3s after localization succeeded, move on to next state
            if (succeeded)
            {
                // While debug menu is open, "pause" success; keep resetting successTime
                if (vpsDebugManager.IsDebugMenuOpen())
                {
                    successTime = Time.time;
                }

                // Otherwise move on to experience state, after the 3s display duration
                else if (Time.time > successTime + 3f)
                {
                    exitState = vpsSceneManager.CurrentVpsDataEntry.bespokeEnabled ?
                        nextStateBespoke :
                        nextStateFrostFlower;
                }
            }

            // 3s after localization failed, restart localization
            if (failed)
            {
                // While debug menu is open, "pause" failure; keep resetting failureTime
                if (vpsDebugManager.IsDebugMenuOpen())
                {
                    failureTime = Time.time;
                }

                // Otherwise re-start localization after the 3s display duration
                else if (Time.time > failureTime + 3f)
                {
                    failed = false;
                    StartLocalization();
                }
            }

            // post-failure hint cycling
            if (!succeeded && !failed && postFailureHints.Count > 0 && Time.time > nextPostFailureHintTime)
            {
                nextPostFailureHintTime = Time.time + postFailureHintCyclePeriodSecs;
                vpsPane.ShowHint(postFailureHints[postFailureHintCtr]);
                if (++postFailureHintCtr >= postFailureHints.Count) postFailureHintCtr = 0;
            }
        }


        private void UpdateStatusIcons()
        {
            vpsPane.statusIconGreenCheck.SetActive(succeeded);
            vpsPane.statusIconRedX.SetActive(failed);
        }


        public void OnBackToStreetMapButtonPress()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            exitState = prevStateStreetMap;
        }


        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // Hide feature points
            featurePointHelper.Spawning = false;
            featurePointHelper.Tracking = false;

            // if returning to streetmap (not continuing to an AR state), then fade out, stop AR
            if (nextState == prevStateStreetMap)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);
                vpsSceneManager.StopAR();
            }

            // hide GUI
            gui.SetActive(false);
            vpsPane.gui.SetActive(false);
            debugMenuButton.gameObject.SetActive(true);

            // disable and stop listening for the backToStreetMap button
            backToStreetMapButton.onClick.RemoveListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(false);

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }
}
