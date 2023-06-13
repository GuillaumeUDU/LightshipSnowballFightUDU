// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Walkabout
{
    /// <summary>
    /// State in Walkabout where player can use the placement button to repeatedly set 
    /// a locomotion destination target on the gameboard for Doty to roll their snowball to.
    /// Multiple small snow piles appear distributed about the gameboard.
    /// To grow Doty's snowball, the user must target snow piles for Doty to collect them.
    /// This state completes once the snowball is large enough to be part of a snowman.
    /// Its next state (set via inspector) is StateBuildSnowman.
    /// </summary>
    public class StateGrowSnowball : StateBase
    {
        private const int maxSnowPiles = 3;
        private const int numSnowPilesRequired = 3;
        public const float closeToSnowpileDist = 0.4f;
        private const float firstSnowPileDelay = 0.75f;
        private const float nextSnowPileDelay = 1f;

        private string[] guideYetiMessages = {
            "Place a Waypoint to get rolling!",
            "Keep rolling!",
            "Just a bit more!"
        };

        private WalkaboutManager walkaboutManager;
        private GameboardHelper gameboardHelper;
        private AudioManager audioManager;

        [Header("State machine")]
        [SerializeField] private GameObject nextState;
        [SerializeField] private GameObject scanningState;
        [SerializeField] private GameObject placementState;
        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;
        protected float initialDelay = 0f;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private TMPro.TMP_Text guideText;
        private CanvasGroup guiCanvasGroup;
        private Fader fader;
        protected const float fadeDuration = 0f;

        private float gameProgress = 0f;
        private int guideIndex = 0;

        private float nextSnowPileTime = 0f;

        private string hintInvalidGameboard = null;

        void Awake()
        {
            // We're not the first state; start off disabled
            gameObject.SetActive(false);

            walkaboutManager = SceneLookup.Get<WalkaboutManager>();
            gameboardHelper = SceneLookup.Get<GameboardHelper>();
            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();
        }

        void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;

            // Subscribe to events
            WalkaboutEvents.EventPlacementButton.AddListener(OnEventPlacementButton);

            // Fade in GUI
            gui.SetActive(true);
            guiCanvasGroup = gui.GetComponent<CanvasGroup>();
            guiCanvasGroup.alpha = 0;
            fader.Fade(guiCanvasGroup, alpha: 1f, duration: fadeDuration, initialDelay: initialDelay);

            // Show Placement button and gauge
            walkaboutManager.placementButton.gameObject.SetActive(true);
            walkaboutManager.progressGauge.gameObject.SetActive(true);

            // Set these before calling UpdateHUD()
            gameProgress = 0f;
            guideIndex = 0;

            // Init HUD
            UpdateHUD(forceUpdate: true);

            // Tell scene manager we're in this state, with no snowpiles yet
            walkaboutManager.snowpiles.Clear();
            nextSnowPileTime = Time.time + firstSnowPileDelay;
            walkaboutManager.inStateGrow = true;

            // ensure this is updated, 
            // in case we're re-entering this state with some snowball progress after rewinding
            walkaboutManager.yetiAndSnowball.UpdateSnowballScale();

            running = true;
        }


        void OnDisable()
        {
            // Unsubscribe from events
            WalkaboutEvents.EventPlacementButton.RemoveListener(OnEventPlacementButton);
        }

        private void OnEventPlacementButton()
        {
            Debug.Log("PlacementButton pressed");

            if (walkaboutManager.cameraReticle.isValidPlacementPt &&
                !walkaboutManager.cameraReticle.isTooCloseToGameObject)
            {
                bool setDest = walkaboutManager.SetYetiDestination();

                // If no destination could be set, show invalidGameboardHint
                hintInvalidGameboard = setDest ? null : WalkaboutManager.invalidGameboardHint;
                UpdateHUD(forceUpdate: true);
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

            // Update managers
            walkaboutManager.cameraReticle.UpdateReticle(
                // Remove constraint of disallowing destinations close to yeti
                //gameObjectForProximityCheck: walkaboutManager.yetiAndSnowball.gameObject,
                //closeToGameObjectDist: WalkaboutManager.reticleCloseToYetiDist
                );
            walkaboutManager.HandleDynamicGameboard();
            walkaboutManager.UpdateYetiLocomotion();

            // Create a snowpile if needed and ready
            if (walkaboutManager.snowpiles.Count < maxSnowPiles && Time.time > nextSnowPileTime)
            {
                GameObject snowpile = walkaboutManager.CreateSnowpile();

                // Set time delay before next snowpile instantiates
                nextSnowPileTime = Time.time + nextSnowPileDelay;

                // SFX on new snowpile
                audioManager.PlayAudioOnObject(AudioKeys.SFX_Snowball_SizeAchieved, snowpile);
            }

            // Has yeti reached a snowpile?
            bool reachedSnowpile = false;
            for (int i = 0; i < walkaboutManager.snowpiles.Count && !reachedSnowpile; i++)
            {
                GameObject snowpile = walkaboutManager.snowpiles[i];
                if (walkaboutManager.IsYetiSnowballNearTarget(snowpile.transform.position, nearDist: closeToSnowpileDist))
                {
                    reachedSnowpile = true;

                    // bubble scale down and destroy snowpile
                    walkaboutManager.DestroySnowpile(snowpile);

                    // SFX
                    audioManager.PlayAudioAtPosition(AudioKeys.SFX_Success_Magic, walkaboutManager.yetiAndSnowball.gameObject.transform.position);

                    // Set time delay before next snowpile instantiates
                    nextSnowPileTime = Time.time + nextSnowPileDelay;

                    // Increment snowball progress
                    walkaboutManager.yetiAndSnowball.IncrementProgress((1f / (float)numSnowPilesRequired) + 0.0001f);
                }
            }


            // Update HUD
            UpdateHUD();

            // DONE once progress reaches 100%
            if (walkaboutManager.yetiAndSnowball.GetProgress() >= 1f)
            {
                // Ready to exit this state to the next state
                exitState = nextState;
                Debug.Log(thisState + " beginning transition to " + exitState);
            }
        }



        private void UpdateHUD(bool forceUpdate = false)
        {
            // update guide text when progress changes
            float latestSnowballProgress = walkaboutManager.yetiAndSnowball.GetProgress();
            if (latestSnowballProgress != gameProgress || forceUpdate)
            {
                gameProgress = latestSnowballProgress;

                walkaboutManager.progressGauge.FillToPercent(gameProgress);

                // hint about invalid gameboard 
                if (hintInvalidGameboard != null)
                {
                    guideText.text = hintInvalidGameboard;
                }

                // guide yeti hints
                else
                {
                    // by default, dole out the hints over the course of progress
                    int which = (int)(gameProgress * guideYetiMessages.Length);
                    if (which > guideYetiMessages.Length - 1) which = guideYetiMessages.Length - 1;

                    // advance the guide index as needed
                    if (which > guideIndex) guideIndex = which;

                    // set the guide text
                    guideText.text = guideYetiMessages[guideIndex];
                }
            }

            // advance past first hint after initial walk is done
            else if (guideIndex == 0 && gameProgress > 0.1f && !walkaboutManager.yetiAndSnowball.Rolling)
            {
                guideIndex = 1;
                guideText.text = guideYetiMessages[guideIndex];
            }
        }

        public void RewindToScanning()
        {
            walkaboutManager.yetiAndSnowball.Stop();
            exitState = scanningState;
        }

        public void RewindToPlacement()
        {
            walkaboutManager.yetiAndSnowball.Stop();
            exitState = placementState;
        }


        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // Play the gauge completion
            if (exitState != placementState)
            {
                yield return walkaboutManager.progressGauge.PlayCompletedSequence();
            }

            // be sure snowpiles are destroyed
            while (walkaboutManager.snowpiles.Count > 0)
            {
                walkaboutManager.DestroySnowpile(walkaboutManager.snowpiles[0]);
            }

            // Tell scene manager we're leaving this state
            walkaboutManager.inStateGrow = false;

            // Fade out GUI
            yield return fader.Fade(guiCanvasGroup, alpha: 0f, duration: fadeDuration);

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);
        }

    }
}
