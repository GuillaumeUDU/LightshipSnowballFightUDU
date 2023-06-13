// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using UnityEngine;


namespace Niantic.ARVoyage
{
    /// <summary>
    /// Universal state used by all scenes, displaying a GUI with instructions text.
    /// If the AR Warning GUI has never been shown, its next state is StateWarning.
    /// Otherwise, its next state is set via inspector, custom to that scene.
    /// </summary>
    public class StateInstructions : StateBase
    {
        // Inspector references to relevant objects
        [Header("State Machine")]
        [SerializeField] protected bool isStartState = true;
        [SerializeField] protected GameObject nextState;
        [SerializeField] protected GameObject warningState;
        protected bool running;
        protected float timeStartedState;
        protected GameObject thisState;
        protected GameObject exitState;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private GameObject fullscreenBackdrop;

        // Fade variables
        private Fader fader;
        private float initialDelay = 0.75f;

        public AppEvent GuiFadedOutDuringExit = new AppEvent();

        protected virtual void Awake()
        {
            gameObject.SetActive(isStartState);

            fader = SceneLookup.Get<Fader>();
        }

        protected virtual void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;

            // Subscribe to events
            DemoEvents.EventStartButton.AddListener(OnEventStartButton);

            // Show fullscreen backdrop
            if (fullscreenBackdrop != null)
            {
                fullscreenBackdrop.gameObject.SetActive(true);
            }

            // Fade in GUI
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, initialDelay: initialDelay));

            running = true;
        }

        void Update()
        {
            // Only process update if running
            if (running)
            {
                // Check for state exit
                if (exitState != null)
                {
                    Exit(exitState);
                }
            }
        }

        protected virtual void OnDisable()
        {
            // Unsubscribe from events
            DemoEvents.EventStartButton.RemoveListener(OnEventStartButton);
        }

        protected virtual void OnEventStartButton()
        {
            exitState = (warningState == null || StateWarning.occurred) ? nextState : warningState;
        }

        protected void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        protected virtual IEnumerator ExitRoutine(GameObject nextState)
        {
            // Fade out GUI
            yield return StartCoroutine(DemoUtil.FadeOutGUI(gui, fader));

            GuiFadedOutDuringExit.Invoke();

            // Hide fullscreen backdrop, unless going to Warning state
            if (exitState != warningState)
            {
                if (fullscreenBackdrop != null)
                {
                    fullscreenBackdrop.gameObject.SetActive(false);
                }
            }

            nextState.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
