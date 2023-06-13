// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Universal state used by all scenes, displaying a GUI with warning text about using AR.
    /// Its next state is set via inspector, custom to that scene.
    /// </summary>
    public class StateWarning : StateBase
    {
        // static bool for whether this state (shared across demos) has occurred or not
        public static bool occurred = false;

        [Header("State machine")]
        [SerializeField] protected GameObject nextState;
        protected bool running;
        protected float timeStartedState;
        protected GameObject thisState;
        protected GameObject exitState;

        [Header("GUI")]
        [SerializeField] protected GameObject gui;
        [SerializeField] protected GameObject fullscreenBackdrop;

        protected Fader fader;
        protected bool fadeInDuringEnable = true;

        protected virtual void Awake()
        {
            // We're not the first state; start off disabled
            gameObject.SetActive(false);

            fader = SceneLookup.Get<Fader>();
        }

        protected virtual void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;

            // Subscribe to events
            DemoEvents.EventWarningOkButton.AddListener(OnEventOkButton);

            // Fade in GUI
            if (fadeInDuringEnable) StartCoroutine(DemoUtil.FadeInGUI(gui, fader));

            occurred = true;

            running = true;
        }

        protected virtual void OnDisable()
        {
            // Unsubscribe from events
            DemoEvents.EventWarningOkButton.RemoveListener(OnEventOkButton);
        }

        protected virtual void OnEventOkButton()
        {
            Debug.Log("OkButton pressed");

            // DONE - ready to exit this state to the next state
            exitState = nextState;
            Debug.Log(thisState + " beginning transition to " + exitState);
        }

        protected virtual void Update()
        {
            if (!running) return;

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
        }

        protected virtual void Exit(GameObject nextState)
        {
            running = false;
            StartCoroutine(ExitRoutine(nextState));
        }

        protected virtual IEnumerator ExitRoutine(GameObject nextState)
        {
            yield return StartCoroutine(DemoUtil.FadeOutGUI(gui, fader));

            // Hide fullscreen backdrop
            if (fullscreenBackdrop != null)
            {
                fullscreenBackdrop.gameObject.SetActive(false);
            }

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);
        }
    }
}
