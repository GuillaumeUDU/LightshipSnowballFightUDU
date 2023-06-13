// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.ARVoyage;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where an animating airship is shown (via vpsIntroManager),
    /// and two introductory GUIs are shown and clicked through.
    /// Its next state (value assigned via inspector) is StateVpsStreetMap.
    /// </summary>
    public class StateVpsIntro : StateBase
    {
        public const string SaveKeyUserViewedStateVpsIntro = "UserViewedStateVpsIntro";

        [Header("State machine")]
        [SerializeField] protected bool isStartState = true;
        [SerializeField] private GameObject nextState;
        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;
        protected float initialDelay = 1f;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private GameObject introGUI1;
        [SerializeField] private GameObject introGUI2;

        private VpsSceneManager vpsSceneManager;
        private VpsIntroManager vpsIntroManager;
        private StreetMapManager streetMapManager;
        private VpsPane vpsPane;
        private AudioManager audioManager;
        private Fader fader;

        private bool skipVpsIntro;
        private bool fadedIn;
        private int guiCtr;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            vpsIntroManager = SceneLookup.Get<VpsIntroManager>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();

            gameObject.SetActive(isStartState);

            // This is the default start state
            // If we're overriding the start state, disable this
            if (DevSettings.VpsStartingStateOverride != DevSettings.VpsSceneState.None)
            {
                gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;
            fadedIn = false;

            skipVpsIntro = DevSettings.SkipVPSIntro || SaveUtil.IsStringSaved(SaveKeyUserViewedStateVpsIntro);
            if (skipVpsIntro)
            {
                exitState = nextState;
                running = true;
                return;
            }

            // Fade in scene
            // Can't do this here, fader may not be awake yet if loading state was skipped
            //fader.FadeSceneIn(Color.white, 0.5f);

            // Fade in GUI
            gui.SetActive(true);
            StartCoroutine(DemoUtil.FadeInGUI(introGUI1, fader, bubbleScaleAnimationType: BubbleScaleAnimationType.Default, initialDelay: 2f));
            introGUI2.SetActive(false);
            guiCtr = 0;

            // Disable the debug button during intro
            SceneLookup.Get<DebugMenuButton>().gameObject.SetActive(false);

            ShowIntro();

            SaveUtil.SaveString(SaveKeyUserViewedStateVpsIntro);

            running = true;
        }

        void Update()
        {
            if (!running) return;

            // If doing intro, fade in scene if haven't yet
            if (!skipVpsIntro && !fadedIn)
            {
                fader.FadeSceneIn(Color.white, 0.5f);
                fadedIn = true;
            }

            if (exitState != null)
            {
                // Don't allow transitioning to street map until ready
                if (!streetMapManager.StreetMapManagerInitialized)
                {
                    return;
                }

                Exit(exitState);
                return;
            }
        }

        private void ShowIntro()
        {
            Debug.Log("StateVpsIntro: Showing intro...");

            vpsSceneManager.DisableCameras();
            vpsIntroManager.Show();
            vpsIntroManager.IntroComplete.AddListener(OnIntroComplete);
        }

        private void OnIntroComplete()
        {
            Debug.Log("StateVpsIntro: Intro complete.");

            vpsIntroManager.IntroComplete.RemoveListener(OnIntroComplete);
        }

        public void OnMessageGUIButtonClicked()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            if (guiCtr++ == 0)
            {
                // Fade out introGUI1, fade in introGUI2
                StartCoroutine(AdvanceGUI());
            }
            else
            {
                exitState = nextState;
            }
        }

        IEnumerator AdvanceGUI()
        {
            yield return StartCoroutine(DemoUtil.FadeOutGUI(introGUI1, fader, fadeDuration: 0.75f));
            yield return StartCoroutine(DemoUtil.FadeInGUI(introGUI2, fader, fadeDuration: 0.75f));
        }

        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        IEnumerator ExitRoutine(GameObject nextState)
        {
            // Fade out GUI
            StartCoroutine(DemoUtil.FadeOutGUI(introGUI2, fader, fadeDuration: 0.75f));

            // Fade out scene
            yield return fader.FadeSceneOut(Color.white, 0.5f);

            // Hide intro before exit.
            vpsIntroManager.Hide();

            // hide gui
            gui.SetActive(false);

            nextState.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
