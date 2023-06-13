// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS to run when a current localization becomes destabilized.
    ///  e.g. due to camera tracking state degrading.
    /// If the user is still close enough to localize at the current localization target, this state transitions to StateVpsLocalization.
    /// If the user is too far away to localize at the current localization target, this state transitions to StateVpsStreetMap.
    /// </summary>
    public class StateVpsLocalizationDestabilized : StateBase
    {
        [Header("State machine")]
        private bool running;
        private GameObject thisState;
        private GameObject nextState;
        private GameObject exitState;

        [SerializeField] private GameObject stateVpsLocalization;
        [SerializeField] private GameObject stateVpsStreetMap;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private Button okButton;
        [SerializeField] private GameObject bodyTextReturnToLocalization;
        [SerializeField] private GameObject bodyTextReturnToStreetMap;

        private StreetMapManager streetMapManager;
        private VpsSceneManager vpsSceneManager;
        private VpsPane vpsPane;
        private AudioManager audioManager;
        private Fader fader;

        private float timeToCheckFadedIn;

        void Awake()
        {
            // We're not the first state; start off disabled
            gameObject.SetActive(false);

            streetMapManager = SceneLookup.Get<StreetMapManager>();
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();
        }

        void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);

            // Subscribe to events
            okButton.onClick.AddListener(OnOkButtonClick);

            // If still close enough to localize, allow user to return to StateVpsLocalization, remaining in AR
            if (streetMapManager.IsUserCloseEnoughToLocalize())
            {
                nextState = stateVpsLocalization;
                SetBodyTextActive(bodyTextReturnToLocalization);
            }
            // Otherwise, return to StateVpsStreetMap
            else
            {
                nextState = stateVpsStreetMap;
                SetBodyTextActive(bodyTextReturnToStreetMap);
            }

            // Disable the Vps Pane if in case it's still showing information
            vpsPane.gui.SetActive(false);

            // Disable the debug button during warning state
            SceneLookup.Get<DebugMenuButton>().gameObject.SetActive(false);

            // Fade in GUI
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, fadeDuration: 0.75f));

            timeToCheckFadedIn = Time.time + 2f;

            running = true;
        }

        private void SetBodyTextActive(GameObject bodyTextToActivate)
        {
            bodyTextReturnToLocalization.SetActive(bodyTextReturnToLocalization == bodyTextToActivate);
            bodyTextReturnToStreetMap.SetActive(bodyTextReturnToStreetMap == bodyTextToActivate);
        }

        void OnDisable()
        {
            // Unsubscribe from events
            okButton.onClick.RemoveListener(OnOkButtonClick);
        }

        private void OnOkButtonClick()
        {
            Debug.Log("OkButton pressed");

            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);

            exitState = nextState;

            Debug.Log(thisState + " beginning transition to " + exitState);
        }

        void Update()
        {
            // Check once if we need to fade in
            if (timeToCheckFadedIn > 0f && Time.time > timeToCheckFadedIn)
            {
                if (!fader.IsSceneFadedIn)
                {
                    fader.FadeSceneIn(Color.black, 0.5f, initialDelay: 0.5f);
                }
                timeToCheckFadedIn = 0f;
            }

            if (!running) return;

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
        }

        private void Exit(GameObject nextState)
        {
            running = false;
            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            yield return StartCoroutine(DemoUtil.FadeOutGUI(gui, fader, fadeDuration: 0.75f));

            Debug.Log(thisState + " transitioning to " + nextState);

            // If returning to StreetMap, stop AR
            if (nextState == stateVpsStreetMap)
            {
                vpsSceneManager.StopAR();
            }

            nextState.SetActive(true);
            thisState.SetActive(false);
        }
    }
}
