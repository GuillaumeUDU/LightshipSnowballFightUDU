// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Loading
{
    /// <summary>
    /// State in Loading that prompts the user to read and accept a specific legal doc.
    /// This state will appear first and block the user from proceeding until they accept
    /// the document. Once they have, it no longer does anything and simply skips to the next
    /// state for the rest of this installation. This state is expected to be used for prefab
    /// variants, one for each type of legal doc.
    /// </summary>
    public class StateLegalDoc : StateBase
    {
        [Header("Player Prefs")]
        [SerializeField] private string legalDocPrefName; // Must be unique per document

        // Inspector references to relevant objects
        [Header("State Machine")]
        [SerializeField] private bool isStartState = true;
        [SerializeField] private GameObject nextState;
        [SerializeField] private StateBase[] precedingStates = new StateBase[] { };

        // Requires user to review and accept before continuing
        [SerializeField] private bool requiresUserReview = true;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private CheckboxButton acceptCheckbox;
        [SerializeField] private Button continueButton;

        // Used to cache PlayerPref data to avoid repeat calls
        private static Dictionary<string, int> acceptedLegalDocByPref =
            new Dictionary<string, int>();

        private bool hasCheckedAcceptBox;
        private GameObject exitState;

        private AudioManager audioManager;

        // Every state has a running bool that's true from OnEnable to Exit
        private bool running;

        // Fade variables
        private Fader fader;
        private float initialDelay = 0.75f;

        void Awake()
        {
            gameObject.SetActive(isStartState);

            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();

            // Create the static document entry if it doesn't already exist
            int docAccepted;
            if (!acceptedLegalDocByPref.TryGetValue(legalDocPrefName, out docAccepted))
                acceptedLegalDocByPref.Add(legalDocPrefName, 0);
        }

        private void OnEnable()
        {
            Debug.LogFormat("Entering StateLegalDoc for doc with pref name {0}", legalDocPrefName);

            if (acceptedLegalDocByPref[legalDocPrefName] == 0)
            {
                acceptedLegalDocByPref[legalDocPrefName] = PlayerPrefs.GetInt(legalDocPrefName);
            }

            // If the player has already accepted this document, skip to next state. 
            if (acceptedLegalDocByPref[legalDocPrefName] > 0)
            {
                // Keep the GUI disabled and exit
                Skipped = true;
                gui.SetActive(false);
                exitState = nextState;
                running = true;
                return;
            }

            // State was not skipped
            Skipped = false;

            // Set Continue button non-interactable (but active/visible) until user "accepts"
            continueButton.interactable = false;

            // Now handle whether they actually need to accept.
            if (requiresUserReview)
            {
                // Subscribe to the accept checkbox button
                acceptCheckbox.checkboxButton.onClick.AddListener(OnEventAcceptCheckbox);
                hasCheckedAcceptBox = acceptCheckbox.isChecked;
            }
            else
            {
                // Otherwise, don't subscribe to the checkbox and just treat it as though
                // they've already accepted.
                hasCheckedAcceptBox = true;
            }

            // Only use the initialDelay if every state preceding this was skipped.
            var firstState = true;
            foreach (var state in precedingStates)
                firstState = firstState && state.Skipped;

            var delay = firstState ? initialDelay : 0.0f;

            // Fade in GUI
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, initialDelay: delay));

            running = true;
        }

        void Update()
        {
            if (running)
            {
                // Once the user has accepted, allow them to continue
                if (!continueButton.interactable && hasCheckedAcceptBox)
                {
                    continueButton.interactable = true;
                    continueButton.onClick.AddListener(OnContinueButtonClicked);
                }
                else if (continueButton.interactable && !hasCheckedAcceptBox)
                {
                    // Handle if they uncheck it
                    continueButton.interactable = false;
                    continueButton.onClick.RemoveListener(OnContinueButtonClicked);
                }

                // Check for state exit
                if (exitState != null)
                {
                    Exit(exitState);
                }
            }
        }

        private void OnEventAcceptCheckbox()
        {
            // We have to track the state ourselves due to CheckboxButton's API
            hasCheckedAcceptBox = !hasCheckedAcceptBox;
        }

        private void OnContinueButtonClicked()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            exitState = nextState;
        }

        private void Exit(GameObject nextState)
        {
            Debug.LogFormat("Exiting StateLegalDoc for doc with pref name {0}", legalDocPrefName);
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

            // Mark in the player prefs that they've accepted the docs, so this state persists
            // between executions
            acceptedLegalDocByPref[legalDocPrefName] = 1;
            PlayerPrefs.SetInt(legalDocPrefName, 1);

            // Activate the next state
            nextState.SetActive(true);

            // Deactivate this state
            gameObject.SetActive(false);
        }
    }
}