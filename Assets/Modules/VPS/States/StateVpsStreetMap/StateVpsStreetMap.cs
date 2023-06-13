// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Niantic.ARVoyage.FrostFlower;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS that triggers an interactive street map, allowing the user to see their device's GPS location,
    /// interactively browse and select VPS locations ("waypoints"), and transition to a localization state 
    /// at a selected location. Achieved by calling methods on the StreetMapManager, which manages the UI 
    /// for using the map, including hint text and a display of a selected location's name and photo image.
    /// This state also:
    /// - periodically calls StreetMapManager's ManageUserDistanceToVPSLocations, to display a distance-to-location meter.
    /// - periodically calls StreetMapManager's ManageSearchThisArea, to offer a SearchThisArea button.
    /// - periodically checks if the user should be notified about a superbloom --
    ///    a now-harvestable previously planted FrostFlower garden at a VPS location.
    ///    If so this state presents the superbloom notification GUI, then saves that the notification has occurred
    ///    and centers the map on and selects that location.
    /// Its next state (value assigned via inspector) is typically StateVpsLocalization,
    /// although once per session it will first go to StateVpsARWarning.
    /// </summary>
    public class StateVpsStreetMap : StateBase
    {
        [Header("State machine")]
        [SerializeField] protected bool isStartState = true;
        [SerializeField] private GameObject nextState;
        [SerializeField] private GameObject warningState;
        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;
        protected float initialDelay = 1f;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private GameObject superbloomGUI;
        private Fader fader;

        private float checkUserDistanceTime = 0f;
        public float checkUserDistancePeriod = 0.5f;

        private VpsSceneManager vpsSceneManager;
        private StreetMapManager streetMapManager;
        private StreetMapMarkerManager streetMapMarkerManager;
        private VpsPane vpsPane;
        private VpsDebugManager vpsDebugManager;
        private AudioManager audioManager;

        private bool shownSuperbloomNotificationGUI;
        private float delayBeforeSuperbloomGUI = 3f;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            streetMapMarkerManager = SceneLookup.Get<StreetMapMarkerManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
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

            // after map is done recentering, zoom out to find nearby markers, if any
            streetMapManager.ActivateStreetMap(true);
            streetMapManager.advanceToLocalization = false;

            // unselect any previously selected location
            bool previouslyOnMap = streetMapMarkerManager.CurrentLocation != null;
            streetMapMarkerManager.MapMarkerLocationUnselected(forceMapRefresh: previouslyOnMap);

            // show GUI
            gui.SetActive(true);
            vpsPane.gui.SetActive(true);

            // Enable the debug button
            SceneLookup.Get<DebugMenuButton>().gameObject.SetActive(true);

            // enable return to homeland UI
            vpsSceneManager.returnToHomelandUI.SetActive(true);

            // enable debug menu streetmap options
            vpsDebugManager.SetOptionsActive(
                VpsSceneManager.IsReleaseBuild() ? vpsDebugManager.streetMapOptionsRelease : vpsDebugManager.streetMapOptions,
                true);

            // start off with unselected VPS location
            streetMapManager.VPSLocationUnselected();

            // reset superbloom notification state
            shownSuperbloomNotificationGUI = false;
            streetMapMarkerManager.NotifySuperbloomIdentifier = null;

            // Fade in when ready.
            fader.FadeSceneIn(Color.white, 0.5f, initialDelay: 0.5f);

            running = true;
        }

        void Update()
        {
            if (!running) return;

            // periodically manage user distance to vps locations
            // for distance display, offer bypassGPS, autoSelectNearbyVpsLocation
            if (Time.time > checkUserDistanceTime) // && !streetMapManager.showingInstructions)
            {
                checkUserDistanceTime = Time.time + checkUserDistancePeriod;
                streetMapManager.ManageUserDistanceToVPSLocations();

                // if needed, once this map session, 
                // show superbloom notification GUI, a few seconds after nothing was selected
                if (streetMapMarkerManager.NotifySuperbloomIdentifier != null &&
                    !shownSuperbloomNotificationGUI &&
                    Time.time > streetMapManager.timeLastUnselected + delayBeforeSuperbloomGUI &&
                    !vpsDebugManager.IsDebugMenuOpen())
                {
                    // Fade in GUI
                    superbloomGUI.SetActive(true);
                    vpsPane.topPane.SetActive(false);
                    StartCoroutine(DemoUtil.FadeInGUI(superbloomGUI, fader, fadeDuration: 0.75f));
                    shownSuperbloomNotificationGUI = true;
                    audioManager.PlayAudioNonSpatial(AudioKeys.SFX_Success_Magic);

                    // Save notificationShown for the notifySuperbloomIdentifier
                    FrostFlowerSaveData saveData = vpsSceneManager.PersistentFrostFlowerStateLookup[streetMapMarkerManager.NotifySuperbloomIdentifier];
                    if (saveData != null)
                    {
                        saveData.notificationShown = true;
                        vpsSceneManager.PersistentFrostFlowerStateLookup[streetMapMarkerManager.NotifySuperbloomIdentifier] = saveData;
                        vpsSceneManager.PersistentFrostFlowerStateLookup.Save();
                    }
                    else
                    {
                        Debug.LogError("PersistentFrostFlowerStateLookup could not find streetMapMarkerManager.notifySuperbloomIdentifier " + streetMapMarkerManager.NotifySuperbloomIdentifier);
                    }
                }
            }

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }

            // Once we're ready to localize, clear flag and move on to next state
            if (streetMapManager.advanceToLocalization)
            {
                streetMapManager.advanceToLocalization = false;
                exitState = (warningState == null || StateVpsARWarning.occurred) ? nextState : warningState;
            }
        }


        public void OnSuperbloomGUIButtonClicked()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            StartCoroutine(OnSuperbloomGUIButtonClickedRoutine());
        }

        private IEnumerator OnSuperbloomGUIButtonClickedRoutine()
        {
            yield return StartCoroutine(DemoUtil.FadeOutGUI(superbloomGUI, fader));

            vpsPane.topPane.SetActive(true);

            // center the map on and select that location
            streetMapManager.CenterOnAndSelectIdentifier(streetMapMarkerManager.NotifySuperbloomIdentifier, duration: 1f);
            streetMapMarkerManager.NotifySuperbloomIdentifier = null;
        }


        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // fade out
            yield return fader.FadeSceneOut(Color.white, 0.5f);

            Debug.Log(thisState + " transitioning to " + nextState);

            // Deactivate street map
            streetMapManager.ActivateStreetMap(false);

            // hide GUI
            gui.SetActive(false);

            // hide pane
            // hide distance indicator (since pane will be reused by other states)
            // hide top button in case it was active
            // disable return to homeland UI
            vpsPane.gui.SetActive(false);
            vpsPane.distanceIndicatorParent.SetActive(false);
            vpsSceneManager.returnToHomelandUI.SetActive(false);

            vpsPane.bypassGpsButton.gameObject.SetActive(false);
            vpsPane.searchButton.gameObject.SetActive(false);
            vpsPane.teleportButton.gameObject.SetActive(false);
            vpsPane.searchingStatus.SetActive(false);

            // disable debug menu streetmap options
            vpsDebugManager.SetOptionsActive(
                VpsSceneManager.IsReleaseBuild() ? vpsDebugManager.streetMapOptionsRelease : vpsDebugManager.streetMapOptions,
                false);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }

    }
}
