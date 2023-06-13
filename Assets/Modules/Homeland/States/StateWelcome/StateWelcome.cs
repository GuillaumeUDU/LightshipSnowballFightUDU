// Copyright 2022 Niantic, Inc. All Rights Reserved.
using Niantic.ARVoyage.Vps;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Homeland
{
    /// <summary>
    /// State in Homeland that displays initial instructions. This state only runs once per execution.
    /// </summary>
    public class StateWelcome : StateBase
    {
        // Track whether this state has ever run. It runs only once per execution.
        private static bool stateRanThisExecution = false;

        // Inspector references to relevant objects
        [Header("State Machine")]
        [SerializeField] private bool isStartState = true;
        [SerializeField] private GameObject nextState;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private GameObject fullscreenBackdrop;
        [SerializeField] private GameObject exitToWorldMap;
        [SerializeField] private GameObject settingsButton;
        [SerializeField] private Button startButton;

        private GameObject exitState;

        // Every state has a running bool that's true from OnEnable to Exit
        private bool running;

        // Fade variables
        private Fader fader;
        private float initialDelay = 0.75f;

        private BadgeManager badgeManager;
        private ErrorManager errorManager;

        void Awake()
        {
            gameObject.SetActive(isStartState);

            badgeManager = SceneLookup.Get<BadgeManager>();
            errorManager = SceneLookup.Get<ErrorManager>();

            fader = SceneLookup.Get<Fader>();
        }

        void OnEnable()
        {
            // If this state ran during this execution, exit immediately
            if (stateRanThisExecution)
            {
                // Keep the gui disabled and exit
                Skipped = true;
                gui.SetActive(false);
                exitState = nextState;
                running = true;
                return;
            }

            // State was not skipped
            Skipped = false;

            // Show fullscreen backdrop
            fullscreenBackdrop.gameObject.SetActive(true);

            // Hide exitToWorldMap button, settings button
            exitToWorldMap.gameObject.SetActive(false);
            settingsButton.gameObject.SetActive(false);

            // Hide badge row
            badgeManager.DisplayBadgeRowButtons(false);

            startButton.onClick.AddListener(OnStartButtonClicked);

            // Fade in GUI
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, initialDelay: initialDelay));

            // Set the static bool so this state won't run again this execution
            stateRanThisExecution = true;

            running = true;
        }

        void Update()
        {
            if (running)
            {
                // Check for state exit
                if (exitState != null)
                {
                    Exit(exitState);
                }
            }
        }

        void OnDisable()
        {
            // Unsubscribe from events
            startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            exitState = nextState;
        }

        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // Fade out GUI if needed
            if (gui.activeInHierarchy)
            {
                yield return StartCoroutine(DemoUtil.FadeOutGUI(gui, fader));
            }

            // Hide fullscreen backdrop
            fullscreenBackdrop.gameObject.SetActive(false);

            // Show exitToWorld map button because user is allowed to access the VPS experience
            exitToWorldMap.gameObject.SetActive(true);

            // Show settings button
            settingsButton.gameObject.SetActive(true);

            // Show badge row
            badgeManager.DisplayBadgeRowButtons(true);

            // Activate the next state
            nextState.SetActive(true);

            // Deactivate this state
            gameObject.SetActive(false);
        }
    }
}
