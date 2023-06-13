// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using UnityEngine;

namespace Niantic.ARVoyage.Homeland
{
    /// <summary>
    /// State in Homeland that controls player level selection
    /// </summary>
    public class StateSelectLevel : StateBase
    {
        public static AppEvent<bool> SetHomelandWaypointsClickable = new AppEvent<bool>();

        // Inspector references to relevant objects
        [Header("State Machine")]
        [SerializeField] private bool isStartState = false;

        [Header("GUI")]
        [SerializeField] private GameObject levelWalkaboutGUI;
        [SerializeField] private GameObject levelSnowballTossGUI;
        [SerializeField] private GameObject levelSnowballFightGUI;
        [SerializeField] private GameObject levelBuildAShipGUI;
        private GameObject activeLevelGUI;

        [Header("World Space")]
        [SerializeField] private GameObject homelandDotHintBubble;
        private bool needHomelandDotHint = false;
        private const float delayTillShowHomelandDotHintBubble = 2f;

        private HomelandActor yetiActor;

        // Fade variables
        private Fader fader;

        // Every state has a running bool that's true from OnEnable to Exit
        private bool running;

        private Level exitLevel = Level.None;

        private BadgeManager badgeManager;
        private Level chosenLevel = Level.None;
        private float timeChosenLevel = 0f;


        void Awake()
        {
            gameObject.SetActive(isStartState);

            badgeManager = SceneLookup.Get<BadgeManager>();
            fader = SceneLookup.Get<Fader>();
            yetiActor = SceneLookup.Get<HomelandActor>();
        }

        void OnEnable()
        {
            // Subscribe to events
            HomelandWaypoint.HomelandWaypointClicked.AddListener(OnHomelandWaypointClicked);
            HomelandEvents.EventLevelGoButton.AddListener(OnEventGoButton);
            HomelandEvents.EventLevelXCloseButton.AddListener(OnEventLevelXCloseButton);
            // If a badge button is pressed, be sure to close any level GUI that may be open
            HomelandEvents.EventBadge1Button.AddListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge2Button.AddListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge3Button.AddListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge4Button.AddListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge5Button.AddListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge6Button.AddListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge7Button.AddListener(OnEventLevelXCloseButton);

            // show achieved badges
            badgeManager.DisplayBadgeRowButtons(true);

            // Make homeland dots clickable upon entering this state
            SetHomelandWaypointsClickable.Invoke(true);

            // Spawn homeland dot button hint bubble
            needHomelandDotHint = !SaveUtil.IsHomelandDotHintBubbleCompleted();
            StartCoroutine(HomelandDotHintBubbleRoutine());

            running = true;
        }

        void OnDisable()
        {
            // Unsubscribe from events
            HomelandWaypoint.HomelandWaypointClicked.RemoveListener(OnHomelandWaypointClicked);
            HomelandEvents.EventLevelGoButton.RemoveListener(OnEventGoButton);
            HomelandEvents.EventLevelXCloseButton.RemoveListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge1Button.RemoveListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge2Button.RemoveListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge3Button.RemoveListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge4Button.RemoveListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge5Button.RemoveListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge6Button.RemoveListener(OnEventLevelXCloseButton);
            HomelandEvents.EventBadge7Button.RemoveListener(OnEventLevelXCloseButton);
        }

        void Update()
        {
            // Only process update if running
            if (running)
            {
                // Check for state exit
                if (exitLevel != Level.None)
                {
                    Exit();
                }
            }
        }

        private void OnHomelandWaypointClicked(HomelandWaypoint waypoint)
        {
            Debug.Log(nameof(OnHomelandWaypointClicked) + " " + waypoint.name);

            // Start the yeti walking to the level
            chosenLevel = waypoint.level;
            yetiActor.WalkToLevel(chosenLevel);

            // Toggle the matching GUI
            ShowLevelGUI(levelWalkaboutGUI, waypoint.level == Level.Walkabout, immediateHide: true);
            ShowLevelGUI(levelSnowballTossGUI, waypoint.level == Level.SnowballToss, immediateHide: true);
            ShowLevelGUI(levelSnowballFightGUI, waypoint.level == Level.SnowballFight, immediateHide: true);
            ShowLevelGUI(levelBuildAShipGUI, waypoint.level == Level.BuildAShip, immediateHide: true);
            timeChosenLevel = Time.time;

            // Don't need homeland dot hint anymore
            if (needHomelandDotHint)
            {
                needHomelandDotHint = false;
                SaveUtil.SaveHomelandDotHintBubbleCompleted();
            }
        }

        private void ShowLevelGUI(GameObject gui, bool show, bool immediateHide = false)
        {
            if (show)
            {
                StartCoroutine(DemoUtil.FadeInGUI(gui, fader));
                activeLevelGUI = gui;
            }
            else if (gui.activeSelf)
            {
                if (immediateHide)
                {
                    gui.SetActive(false);
                }
                else
                {
                    StartCoroutine(DemoUtil.FadeOutGUI(gui, fader));
                }
                activeLevelGUI = null;
            }
        }

        private IEnumerator HomelandDotHintBubbleRoutine()
        {
            // initially hidden
            homelandDotHintBubble.gameObject.SetActive(false);

            float waitTill = Time.time + delayTillShowHomelandDotHintBubble;
            while (Time.time < waitTill) yield return null;

            // Bail if hint unneeded
            if (!needHomelandDotHint) yield break;

            // Animate it up
            homelandDotHintBubble.gameObject.transform.localScale = Vector3.zero;
            DemoUtil.DisplayWithBubbleScale(homelandDotHintBubble, show: true);

            // Wait until hint unneeded
            while (needHomelandDotHint) yield return null;

            // Hide bubble
            if (levelSnowballFightGUI.gameObject.activeSelf ||
                levelBuildAShipGUI.gameObject.activeSelf)
            {
                homelandDotHintBubble.gameObject.SetActive(false);
            }
            else
            {
                DemoUtil.DisplayWithBubbleScale(homelandDotHintBubble, show: false);
            }
        }

        private void OnEventGoButton()
        {
            exitLevel = chosenLevel;

            if (activeLevelGUI != null)
            {
                StartCoroutine(DemoUtil.FadeOutGUI(activeLevelGUI, fader));
            }

            // Make homeland dots non-clickable once an exit level is selected
            SetHomelandWaypointsClickable.Invoke(false);

            // Immediately unsubscribe to prevent listening for clicks during exit
            OnDisable();
        }

        private void OnEventLevelXCloseButton()
        {
            ShowLevelGUI(levelWalkaboutGUI, false);
            ShowLevelGUI(levelSnowballTossGUI, false);
            ShowLevelGUI(levelSnowballFightGUI, false);
            ShowLevelGUI(levelBuildAShipGUI, false);
        }


        private void Exit()
        {
            running = false;

            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            yield return null;

            // Save the level
            SaveUtil.SaveLastLevelPlayed(exitLevel);

            // Go to the exit level
            SceneLookup.Get<LevelSwitcher>().LoadLevel(exitLevel, fadeOutBeforeLoad: true);
        }
    }
}
