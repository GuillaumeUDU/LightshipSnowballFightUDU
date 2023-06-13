// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

using Niantic.ARDK.Extensions.Gameboard;

namespace Niantic.ARVoyage.Walkabout
{
    /// <summary>
    /// Constants, GameObjects, game state, and helper methods used by 
    /// various State classes in the Walkabout demo.
    /// </summary>
    public class WalkaboutManager : MonoBehaviour, ISceneDependency
    {
        public const int minVictorySteps = 10;

        public const float baseYetiSize = 1.6f;
        public const float origYetiSize = 3.5f;
        public const float yetiSizeDelta = 0.25f;
        public float yetiSize;
        public float baseYetiWalkSpeed;
        public const float baseSnowballCloseToTargetDist = 0.5f;
        private float minTargetDist = baseSnowballCloseToTargetDist * 1.2f;
        private float minTargetSpacing = 1f;

        [Header("Gameboard")]
        public GameboardManager gameboardManager;
        private float dynamicGameboardPollTime = 0f;
        private const float dynamicGameboardPollPeriod = 0.5f;
        private int unstableGameboardCtrForYeti = 0;

        [Header("NPCs")]
        [SerializeField] public WalkaboutActor yetiAndSnowball;
        [SerializeField] public WalkaboutSnowman snowman;

        public Transform ActorCenterTransform => yetiAndSnowball.ActorCenterTransform;

        [Header("Props")]
        [SerializeField] private GameObject snowpilePrefab;
        [HideInInspector] public List<GameObject> snowpiles = new List<GameObject>();
        private const float snowpileScale = 1.5f;

        [Header("Buttons")]
        [SerializeField] public GameObject placementButton;

        [Header("Reticle")]
        [SerializeField] public SurfaceReticle cameraReticle;
        [SerializeField] private GameObject destinationMarker;
        private Vector3 destinationPos;
        private bool destinationPointChanged = false;
        private float timeDestinationPointChanged = 0f;
        [HideInInspector] public const float reticleCloseToYetiDist = 0.4f;
        private const float snowmanHoverReticleDist = 0.3f;
        private bool showingSnowmanHover = false;

        [Header("Gauge")]
        [SerializeField] public Gauge progressGauge;

        [Header("State")]
        [SerializeField] StatePlacement statePlacement;
        [SerializeField] StateGrowSnowball stateGrow;
        [SerializeField] StateBuildSnowman stateBuild;
        [HideInInspector] public bool inStatePlacement = false;
        [HideInInspector] public bool inStateGrow = false;
        [HideInInspector] public bool inStateBuild = false;

        private GameboardHelper gameboardHelper;

        // manager state
        private Vector3 lastValidPlacementPt = Vector3.zero;
        private List<Vector3> validGameboardPoints = new List<Vector3>();

        public const string invalidGameboardHint = "Keep scanning the ground!";

        void Awake()
        {
            gameboardHelper = SceneLookup.Get<GameboardHelper>();
        }

        void OnEnable()
        {
            cameraReticle.DisplayReticle(false);
            yetiSize = baseYetiSize;
            baseYetiWalkSpeed = yetiAndSnowball.yetiWalkSpeed;
            UpdateYetiSize();
        }

        public void UpdateYetiInitialPlacement()
        {
            // place yeti at exactly at current valid place on the gameboard, if any
            Vector3 placementPt;
            if (cameraReticle.isValidPlacementPt)
            {
                placementPt = cameraReticle.validPlacementPt;
            }

            // else leave yeti where they are on gameboard
            else
            {
                placementPt = lastValidPlacementPt;
            }

            // cache this
            lastValidPlacementPt = placementPt;

            // place yeti at chosen placement point
            yetiAndSnowball.transform.position = placementPt;

            // keep yeti's y rotation toward the camera
            DemoUtil.FaceNPCToPlayer(yetiAndSnowball.gameObject);

            // move yeti forward a bit along its orientation, 
            // since its center point is not under its feet (due to hidden snowball)
            float yetiOriginOffset = yetiAndSnowball.GetYetiToCenterDist();
            yetiAndSnowball.transform.position += yetiAndSnowball.transform.forward * yetiOriginOffset;

            // Be sure yeti is visible
            if (!yetiAndSnowball.gameObject.activeSelf)
            {
                // show translucent yeti, with no snowball yet
                yetiAndSnowball.DisplaySnowball(false);
                yetiAndSnowball.SetTransparent(true);
                DemoUtil.DisplayWithBubbleScale(yetiAndSnowball.gameObject, show: true,
                                                targetScale: baseYetiSize);
            }
        }

        public void UpdateYetiInitialPlacementDone()
        {
            // show opaque yeti with snowball
            yetiAndSnowball.SetTransparent(false);
            yetiAndSnowball.DisplaySnowball(true);
        }

        public bool ReScanEnvironment()
        {
            if (inStatePlacement)
            {
                statePlacement.RewindToScanning();
                return true;
            }
            else if (inStateGrow)
            {
                stateGrow.RewindToScanning();
                return true;
            }
            else if (inStateBuild)
            {
                stateBuild.RewindToScanning();
                return true;
            }

            return false;
        }

        public bool RePlaceDoty()
        {
            if (inStateGrow)
            {
                stateGrow.RewindToPlacement();
                return true;
            }

            else if (inStateBuild)
            {
                stateBuild.RewindToPlacement();
                return true;
            }

            return false;
        }

        public void UpdateYetiLocomotion()
        {
            // If player clicks / sets a new destination for yeti
            if (destinationPointChanged && gameboardHelper != null)
            {
                destinationPointChanged = false;
                timeDestinationPointChanged = Time.time;

                // Always start on a valid surface point
                Vector3 startPoint = gameboardHelper.GetClosestPointOnCurrentSurface(yetiAndSnowball.transform.position);

                // Calculate walk path to destination
                Path path = gameboardHelper.CalculateLocomotionPath(
                    startPos: startPoint,
                    endPos: destinationMarker.transform.position);

                // Walk to destination
                if (path.Waypoints != null && path.Waypoints.Count > 0)
                {
                    // NOTE: this should be the actual worldspace position where we want to arrive
                    yetiAndSnowball.Move(path.Waypoints, destinationMarker.transform.position);
                }
            }
        }

        public void CacheYetiGameboardPosition()
        {
            validGameboardPoints.Add(yetiAndSnowball.transform.position);
        }

        // Based on position of camera reticle, or a given requested destination position,
        // choose a valid locomotion destination for the yeti
        // Returns false if gameboard is invalid
        public bool SetYetiDestination(bool useCameraReticle = true, Vector3? requestedDestPos = null)
        {
            Vector3 requestedDestinationPos = requestedDestPos ?? Vector3.zero;

            if (useCameraReticle)
            {
                // don't allow use of reticle if it's not being displayed 
                // (e.g., reticle is plane above camera)
                if (!cameraReticle.isReticleDisplayable) return true;

                requestedDestinationPos = cameraReticle.transform.position;
            }


            // if using reticle, and reticle is on surface,
            // then use the reticle position for destination point
            if (useCameraReticle && cameraReticle.isReticleOnSurface)
            {
                destinationPos = requestedDestinationPos;
            }

            // otherwise look for a nearby safe position for destination point
            else
            {
                bool hasSurface;
                Vector3 visibleValidGameboardPt;
                gameboardHelper.FindClosestInnerGameboardPoint(requestedDestinationPos,
                                                                out hasSurface,
                                                                out destinationPos,
                                                                out visibleValidGameboardPt);
                // bail if there is no surface
                if (!hasSurface)
                {
                    return false;
                }
            }

            destinationPointChanged = true;

            // Special case for targeting Snowman NPC:
            // if snowman is visible, and we choose a destination near the snowman, 
            // then lock onto the snowman
            bool destinationCloseToSnowman = snowman.gameObject.activeSelf &&
                DemoUtil.GetXZDistance(destinationPos, snowman.transform.position) < snowmanHoverReticleDist;
            if (destinationCloseToSnowman)
            {
                destinationPos.x = snowman.transform.position.x;
                destinationPos.z = snowman.transform.position.z;
            }

            // cache this destination point
            validGameboardPoints.Add(destinationPos);

            // put destination marker just above the reticle
            Vector3 destinationMarkerPt = destinationPos;
            float reticleY = cameraReticle.transform.position.y;
            destinationMarkerPt.y = reticleY + 0.001f;

            destinationMarker.gameObject.SetActive(true);
            destinationMarker.transform.position = destinationMarkerPt;
            destinationMarker.transform.rotation = Quaternion.identity;

            return true;
        }

        // Periodically poll if gameboard has dynamically disappeared underneath each NPC
        // If so, teleport NPC to closest valid gameboard point
        public void HandleDynamicGameboard(bool includeSnowman = false)
        {
            // wait till next poll time
            if (Time.time < dynamicGameboardPollTime) return;
            dynamicGameboardPollTime = Time.time + dynamicGameboardPollPeriod;

            // for each NPC requested            
            for (int i = 0; i < (includeSnowman ? 2 : 1); i++)
            {
                GameObject npc = i == 0 ? yetiAndSnowball.gameObject : snowman.gameObject;

                // find preferably visible, valid gameboard position closest to NPC 
                // (hopefully where the NPC currently is, so we don't have to teleport)
                bool hasSurface;
                Vector3 validGameboardPt;
                Vector3 visibleValidGameboardPt;
                gameboardHelper.FindClosestInnerGameboardPoint(
                    npc.transform.position,
                    out hasSurface,
                    out validGameboardPt,
                    out visibleValidGameboardPt,
                    // inner points are too strict - results in too much teleportation
                    // instead, allow for points anywhere still on or near gameboard edge
                    allowGameboardEdgePoints: true);

                // bump it up to NPC's y
                validGameboardPt.y = npc.transform.position.y;

                // how far is it from the NPC?
                float dist = DemoUtil.GetXZDistance(npc.transform.position, validGameboardPt);

                // if too far, it means gameboard has disappeared under NPC
                bool unstableGameboard = dist > gameboardHelper.gameBoard.Settings.TileSize;
                if (unstableGameboard)
                {
                    // For yeti, require 2 unstable gameboard periods in a row
                    if (i == 0)
                    {
                        if (unstableGameboardCtrForYeti++ == 0)
                        {
                            Debug.Log("Unstable gameboard for " + npc + ", waiting another " + dynamicGameboardPollPeriod + "s before teleporting");
                            return;
                        }
                    }

                    // TELEPORT NPC
                    Debug.Log("Teleporting " + npc + " from " + npc.transform.position + " to " + validGameboardPt);
                    npc.transform.position = validGameboardPt;

                    // For yeti, re-path any locomotion, if snowman not complete
                    if (npc == yetiAndSnowball.gameObject && yetiAndSnowball.Rolling)
                    {
                        yetiAndSnowball.Stop();

                        // Re-path to existing destination, after a short delay to allow teleport to happen
                        if (!yetiAndSnowball.SnowmanComplete)
                        {
                            StartCoroutine(SetYetiDestinationRoutine(destinationMarker.gameObject.transform.position));
                        }
                    }
                }

                // if gameboard is stable for yeti, reset ctr
                else
                {
                    if (i == 0)
                    {
                        unstableGameboardCtrForYeti = 0;
                    }
                }
            }
        }

        private IEnumerator SetYetiDestinationRoutine(Vector3 destinationPos, float initialDelay = 0.25f)
        {
            float waitTill = Time.time + initialDelay;
            while (Time.time < waitTill) yield return null;

            SetYetiDestination(useCameraReticle: false, requestedDestPos: destinationPos);
        }

        public int GetNumYetiSteps()
        {
            return yetiAndSnowball.Footsteps;
        }

        public GameObject CreateSnowpile()
        {
            // Find position to place a new snowpile
            Vector3 snowpilePos = gameboardHelper.GetRandomGameboardPosition(
                yetiAndSnowball.transform.position, minTargetDist,
                snowpiles, minTargetSpacing);

            // Instantiate and bubble scale up the new snowpile            
            snowpilePos.y = cameraReticle.transform.position.y + 0.01f;  // Place it above reticle
            GameObject snowpile = Instantiate(snowpilePrefab, snowpilePos, Quaternion.identity);
            snowpile.transform.eulerAngles = new Vector3(0f, Random.Range(0f, 360f), 0f); // random y rotation
            snowpiles.Add(snowpile);
            DemoUtil.DisplayWithBubbleScale(snowpile, show: true, targetScale: snowpileScale);
            return snowpile;
        }

        public void DestroySnowpile(GameObject snowpile)
        {
            if (snowpile == null) return;

            snowpiles.Remove(snowpile);

            // Scale down snowpile, then destroy it
            DemoUtil.DisplayWithBubbleScale(snowpile, show: false,
                onComplete: () =>
                {
                    Destroy(snowpile);
                }
            );
        }

        public bool CreateSnowman()
        {
            Vector3 snowmanPosition = gameboardHelper.GetRandomGameboardPosition(
                    yetiAndSnowball.transform.position, minTargetDist);

            // place snowman, with its base revealed
            snowmanPosition.y = yetiAndSnowball.transform.position.y;
            snowman.transform.position = snowmanPosition;

            DemoUtil.FaceNPCToPlayer(snowman.gameObject);
            ShowSnowman(true);
            snowman.RevealBase();

            return true;
        }

        public void ShowSnowman(bool showSnowman)
        {
            if (!showSnowman)
            {
                snowman.Reset();
            }

            snowman.transform.localScale = Vector3.one * baseYetiSize;
            snowman.gameObject.SetActive(showSnowman);
        }

        public void UpdateSnowmanHoverVFX()
        {
            bool reticleCloseToSnowman = DemoUtil.GetXZDistance(cameraReticle.transform.position,
                                            snowman.transform.position) < snowmanHoverReticleDist;

            bool destinationCloseToSnowman = DemoUtil.GetXZDistance(destinationPos,
                                                snowman.transform.position) < snowmanHoverReticleDist;

            bool shouldShowHover = reticleCloseToSnowman && !destinationCloseToSnowman;

            if ((!showingSnowmanHover && shouldShowHover) ||
                (showingSnowmanHover && !shouldShowHover))
            {
                showingSnowmanHover = !showingSnowmanHover;
                snowman.SetHover(showingSnowmanHover);
            }
        }

        public bool IsYetiSnowballNearTarget(Vector3 targetPos, float nearDist = baseSnowballCloseToTargetDist)
        {
            float dist = DemoUtil.GetXZDistance(yetiAndSnowball.transform.position, targetPos);
            return dist < (nearDist * (yetiSize / origYetiSize));
        }

        public void CompleteSnowman()
        {
            yetiAndSnowball.Complete();
            snowman.RevealBody();
        }

        public void RestartGame()
        {
            ResetGameBoard();
            yetiAndSnowball.ResetProgress();
        }

        public void ResetGameBoard()
        {
            gameboardHelper.ClearTiles();

            validGameboardPoints.Clear();
            destinationMarker.gameObject.SetActive(false);

            // hide yeti and snowman
            DemoUtil.DisplayWithBubbleScale(yetiAndSnowball.gameObject, show: false);
            DemoUtil.DisplayWithBubbleScale(snowman.gameObject, show: false);

            ReScanEnvironment();
        }

        public void UpdateYetiSize()
        {
            yetiAndSnowball.transform.localScale = new Vector3(yetiSize, yetiSize, yetiSize);
            yetiAndSnowball.yetiWalkSpeed = baseYetiWalkSpeed * (yetiSize / baseYetiSize);
            snowman.transform.localScale = new Vector3(yetiSize, yetiSize, yetiSize);
        }
    }
}
