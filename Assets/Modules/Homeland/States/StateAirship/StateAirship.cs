// Copyright 2022 Niantic, Inc. All Rights Reserved.
using Niantic.ARVoyage.Vps;
using System.Collections;
using UnityEngine;

namespace Niantic.ARVoyage.Homeland
{
    /// <summary>
    /// State in Homeland that controls display and behavior of the Niantic airship.
    /// When player has unlocked all badges, the airship flies away and the player is presented with a thank you message.
    /// </summary>
    public class StateAirship : StateBase
    {
        [Header("State Machine")]
        [SerializeField] private bool isStartState = false;
        [SerializeField] private GameObject nextState;
        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;

        [Header("GUI")]
        [SerializeField] private GameObject thankYouGUI;
        [SerializeField] private GameObject vpsThankYouGUI;
        [SerializeField] private GameObject resetProgressGUI;

        private Fader fader;

        private Airship airship;
        private HomelandActor yetiActor;
        private BadgeManager badgeManager;
        protected LevelSwitcher levelSwitcher;


        void Awake()
        {
            gameObject.SetActive(isStartState);

            airship = SceneLookup.Get<Airship>();
            yetiActor = SceneLookup.Get<HomelandActor>();
            badgeManager = SceneLookup.Get<BadgeManager>();
            levelSwitcher = SceneLookup.Get<LevelSwitcher>();
            fader = SceneLookup.Get<Fader>();
        }

        void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;

            // Subscribe to events
            HomelandEvents.EventResetProgressRequestButton.AddListener(OnEventResetProgressRequestButton);
            HomelandEvents.EventResetProgressConfirmButton.AddListener(OnEventResetProgressConfirmButton);
            HomelandEvents.EventResetProgressCancelButton.AddListener(OnEventResetProgressCancelButton);
            HomelandEvents.EventBackToHomelandButton.AddListener(OnEventBackToHomelandButton);

            // Be sure GUIs are hidden
            thankYouGUI.SetActive(false);
            vpsThankYouGUI.SetActive(false);
            resetProgressGUI.SetActive(false);

            // Start the coroutine that runs the stages of this state
            StartCoroutine(AirshipStagesRoutine());

            running = true;
        }


        void OnDisable()
        {
            HomelandEvents.EventResetProgressRequestButton.RemoveListener(OnEventResetProgressRequestButton);
            HomelandEvents.EventResetProgressConfirmButton.RemoveListener(OnEventResetProgressConfirmButton);
            HomelandEvents.EventResetProgressCancelButton.RemoveListener(OnEventResetProgressCancelButton);
            HomelandEvents.EventBackToHomelandButton.RemoveListener(OnEventBackToHomelandButton);
        }

        private IEnumerator AirshipStagesRoutine()
        {
            // If airship is unlocked but not yet built, build it and wait for completion
            if (SaveUtil.IsAirshipUnlocked() && !SaveUtil.IsAirshipBuilt())
            {
                Debug.Log(name + " building airship");
                yield return airship.Build();
            }

            // If airship is built and all badges are earned, trigger the airship depart
            if (SaveUtil.IsAirshipBuilt() && !SaveUtil.IsAirshipDeparted() &&
                badgeManager.AreAllBadgesPresented())
            {
                Debug.Log(name + " airship departing");

                // hide yeti
                yield return yetiActor.BubbleScaleDown();

                // wait a moment
                yield return new WaitForSeconds(.5f);

                // trigger and wait for airship to depart
                yield return airship.Depart();
            }

            // If airship is departed, but the thank you hasn't completed, trigger thankYou GUI
            if (SaveUtil.IsAirshipDeparted() && !SaveUtil.IsThankYouCompleted())
            {
                // Fade in GUI
                StartCoroutine(DemoUtil.FadeInGUI(vpsThankYouGUI, fader));
                SaveUtil.SaveThankYouCompleted();
            }

            // else move on to next state
            else
            {
                exitState = nextState;
            }
        }


        void Update()
        {
            if (!running) return;

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
        }

        // Switch from thankYouGUI to resetProgressGUI
        private void OnEventResetProgressRequestButton()
        {
            thankYouGUI.SetActive(false);
            resetProgressGUI.SetActive(true);
        }

        // Move on to next state
        private void OnEventBackToHomelandButton()
        {
            // Save that the thank you process is completed
            SaveUtil.SaveThankYouCompleted();

            yetiActor.BubbleScaleUp();
            exitState = nextState;
        }

        // Clear all save data and reload the homeland scene
        private void OnEventResetProgressConfirmButton()
        {
            SaveUtil.Clear();
            levelSwitcher.LoadLevel(Level.Homeland, fadeOutBeforeLoad: true);
        }

        // Switch from resetProgressGUI back to thankYouGUI
        private void OnEventResetProgressCancelButton()
        {
            resetProgressGUI.SetActive(false);
            thankYouGUI.SetActive(true);
        }

        public void OnVpsThankYouBackButton()
        {
            StartCoroutine(DemoUtil.FadeOutGUI(vpsThankYouGUI, fader));
        }

        public void OnVpsThankYouLetsGoButton()
        {
            StartCoroutine(DemoUtil.FadeOutGUI(vpsThankYouGUI, fader));

            Debug.Log("Exit to world map");
            levelSwitcher.ExitToWorldMap();
        }

        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // Fade out GUI if needed
            if (thankYouGUI.activeInHierarchy)
            {
                yield return StartCoroutine(DemoUtil.FadeOutGUI(thankYouGUI, fader));
            }
            if (resetProgressGUI.activeInHierarchy)
            {
                yield return StartCoroutine(DemoUtil.FadeOutGUI(resetProgressGUI, fader));
            }

            nextState.SetActive(true);
            gameObject.SetActive(false);

            yield break;
        }
    }

}
