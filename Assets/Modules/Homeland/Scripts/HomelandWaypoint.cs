// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using UnityEngine.EventSystems;

namespace Niantic.ARVoyage.Homeland
{
    /// <summary>
    /// Manages functionality tappable homeland waypoints
    /// </summary>
    public class HomelandWaypoint : MonoBehaviour, IPointerClickHandler
    {
        public static AppEvent<HomelandWaypoint> HomelandWaypointClicked = new AppEvent<HomelandWaypoint>();

        // When the actor is walking to this waypoint and reaches this percent away on the path, this waypoint will hide its arrow
        private const float ActorPathPercentFromWaypointToHideArrow = .05f;

        // The level this waypoint is mapped to
        public Level level;

        [SerializeField] private Renderer waypointIconRenderer;
        [SerializeField] GameObject arrow;
        [SerializeField] private Texture2D iconBadgeLocked;
        [SerializeField] private Texture2D iconBadgeUnlocked;

        private AudioManager audioManager;

        private bool clickable;
        private bool arrowShown;

        private void Awake()
        {
            arrow.SetActive(false);
            arrow.transform.localScale = Vector3.zero;
            audioManager = SceneLookup.Get<AudioManager>();
        }

        private void OnEnable()
        {
            // Subscribe to events
            HomelandActor.ActorStartingWalkToLevel.AddListener(OnActorWalkingToLevel);
            HomelandActor.ActorPathPercentFromLevelChanged.AddListener(OnActorPathPercentFromLevelChanged);
            HomelandActor.ActorJumpedToLevel.AddListener(OnActorJumpedToLevel);
            StateSelectLevel.SetHomelandWaypointsClickable.AddListener(SetClickable);

            // Set the correct icon based on whether this level's badge is unlocked
            if (SaveUtil.IsBadgeUnlocked(level))
            {
                waypointIconRenderer.material.mainTexture = iconBadgeUnlocked;
            }
            else
            {
                waypointIconRenderer.material.mainTexture = iconBadgeLocked;
            }
        }

        private void SetClickable(bool clickable)
        {
            this.clickable = clickable;
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            HomelandActor.ActorStartingWalkToLevel.RemoveListener(OnActorWalkingToLevel);
            HomelandActor.ActorPathPercentFromLevelChanged.RemoveListener(OnActorPathPercentFromLevelChanged);
            HomelandActor.ActorJumpedToLevel.RemoveListener(OnActorJumpedToLevel);
            StateSelectLevel.SetHomelandWaypointsClickable.RemoveListener(SetClickable);
        }

        private void OnActorWalkingToLevel(Level level)
        {
            // Show or hide the arrow depending on which level the actor is walking to
            ShowArrow(level == this.level);
        }

        private void OnActorPathPercentFromLevelChanged(Level level, float actorPathPercentFromLevel)
        {
            // If that actor is walking to this waypoint and reaches the designated path percent, hide the arrow
            if (arrowShown && level == this.level && actorPathPercentFromLevel <= ActorPathPercentFromWaypointToHideArrow)
            {
                ShowArrow(false);
            }
        }

        private void OnActorJumpedToLevel(Level level)
        {
            // Hide any homeland arrows when the actor jumps to a level
            ShowArrow(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Only process if this waypoint is clickable
            if (clickable)
            {
                HomelandWaypointClicked.Invoke(this);
                audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            }
        }

        public void ShowArrow(bool show)
        {
            if (show == arrowShown)
            {
                return;
            }

            arrowShown = show;

            if (show)
            {
                BubbleScaleUtil.ScaleUp(arrow,
                    activateTargetOnStart: true);
            }
            else
            {
                // Scale down if active, otherwise just set the scale to 0
                if (arrow.activeInHierarchy)
                {
                    BubbleScaleUtil.ScaleDown(arrow,
                            deactivateTargetOnComplete: true);
                }
                else
                {
                    arrow.transform.localScale = Vector3.zero;
                }
            }
        }

#if UNITY_EDITOR && FALSE
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                ShowArrow(true);
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                ShowArrow(false);
            }
        }
#endif
    }
}
