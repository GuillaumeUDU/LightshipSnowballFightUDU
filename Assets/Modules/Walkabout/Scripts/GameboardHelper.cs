// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using Niantic.ARDK.Extensions.Gameboard;

namespace Niantic.ARVoyage.Walkabout
{
    /// <summary>
    /// Constants, state, and helper methods for Gameboard used in Walkabout demo.
    /// Includes:
    ///  PersistentScanRoutine() - periodically calls gameBoard.Scan() to update gameboard
    ///  RaycastToGameboard()
    ///  GetRandomGameboardPosition() - calls gameBoard.FindRandomPosition()
    ///  FindClosestInnerGameboardPoint() - calls gameBoard.FindNearestFreePosition()
    ///  GetClosestPointOnCurrentSurface() - calls gameBoard.FindNearestFreePosition()
    ///  CalculateLocomotionPath() - calls gameBoard.CalculatePath()
    /// </summary>
    public class GameboardHelper : MonoBehaviour, ISceneDependency
    {
        // Defines a method to be used for selecting the surface to render
        public enum SurfaceSelectionMode
        {
            // Select the surface that is forward from the camera
            CameraForward,

            // Select the surface that is down from the actor on the gameBoard
            ActorDown
        }

        public const float checkFitMultiplier = 2.5f;
        private float scanInterval = 0.2f;
        private float scanRadius = 1.5f;
        private const int maxNumRandomPositionSamples = 50;
        public Vector3 raycastVectorOffset = Vector3.zero;

        [SerializeField] private LayerMask raycastLayerMask;
        [SerializeField] private Camera _arCamera;

        [HideInInspector] public IGameboard gameBoard = null;

        private Vector3 scanOrigin;
        private bool isScanning = false;
        [HideInInspector] public bool innerNodeExists = false;

        private SurfaceSelectionMode surfaceSelectionMode = SurfaceSelectionMode.CameraForward;
        private Transform surfaceSelectionActorTransform;

        private WalkaboutManager walkaboutManager;


        void Awake()
        {
            walkaboutManager = SceneLookup.Get<WalkaboutManager>();

            SetIsScanning(false);
        }

        void OnEnable()
        {
            StartCoroutine(PersistentScanRoutine());
        }

        void OnDisable()
        {
        }

        public void ClearTiles()
        {
            gameBoard?.Clear();
        }


        // -----------------
        // Scanning to find gameBoard

        public void SetIsScanning(bool isScanning)
        {
            this.isScanning = isScanning;
        }

        public void SetSurfaceSelectionModeCameraForward()
        {
            surfaceSelectionMode = SurfaceSelectionMode.CameraForward;
        }

        public void SetSurfaceSelectionModeActorDown(Transform actorTransform)
        {
            surfaceSelectionMode = SurfaceSelectionMode.ActorDown;
            surfaceSelectionActorTransform = actorTransform;
        }

        // Get the surface selection ray based on the current SurfaceSelectionMode
        private Ray GetSurfaceSelectionRay()
        {
            // Get the surface selection ray based on the actor's down
            if (surfaceSelectionMode == SurfaceSelectionMode.ActorDown)
            {
                if (surfaceSelectionActorTransform != null)
                {
                    return new Ray(surfaceSelectionActorTransform.position, Vector3.down);
                }
                else
                {
                    Debug.LogWarning(name + " " + SurfaceSelectionMode.ActorDown +
                                        " got null actor. Defaulting to " + SurfaceSelectionMode.CameraForward);
                }
            }

            // If the method reaches this point, default to SurfaceSelectionMode.CameraForward
            var cameraPosition = _arCamera.transform.position;
            var cameraForward = _arCamera.transform.forward + raycastVectorOffset;
            return new Ray(cameraPosition, cameraForward);
        }


        // Periodically call gameBoard.Scan() to update gameboard
        // enabled/disabled by calling SetIsScanning() above
        private IEnumerator PersistentScanRoutine()
        {
            innerNodeExists = false;

            while (true)
            {
                if (isScanning)
                {
                    // Scan at camera reticle, from camera's height
                    if (walkaboutManager.cameraReticle.everFoundSurfaceForReticle)
                    {
                        scanOrigin = walkaboutManager.cameraReticle.transform.position;
                        scanOrigin.y = Camera.main.transform.position.y;
                    }

                    // Pre-reticle, scan from scanRadius forward from camera
                    else
                    {
                        Vector3 cameraForward = (Camera.main.transform.forward + raycastVectorOffset) * scanRadius;
                        scanOrigin = Camera.main.transform.position +
                                        Vector3.ProjectOnPlane(vector: cameraForward, planeNormal: Vector3.up).normalized;
                    }

                    // SCAN
                    if (gameBoard == null)
                    {
                        gameBoard = walkaboutManager.gameboardManager.Gameboard;
                    }

                    if (gameBoard != null)
                    {
                        gameBoard.Scan(scanOrigin, range: scanRadius);

                        // Now with the latest surface(s) found,
                        // raycast from camera, to get the surface the camera is looking at
                        // OR if actor exists, raycast down from actor
                        bool hasHit = gameBoard.RayCast(GetSurfaceSelectionRay(), out Vector3 hit);

                        // initially set the innerNodeExists flag
                        if (!innerNodeExists && hasHit)
                        {
                            float checkFitSize = gameBoard.Settings.TileSize * checkFitMultiplier;
                            innerNodeExists = gameBoard.CheckFit(hit, checkFitSize);
                        }
                    }
                }

                yield return new WaitForSeconds(scanInterval);
            }
        }


        public Vector3 GetRandomGameboardPosition(Vector3 sourcePos, float minTargetDist,
                                                    List<GameObject> otherObjects = null,
                                                    float minTargetSpacing = 0f)
        {
            Vector3 pos = Vector3.zero;

            float closestDist = 0f;
            Vector3 closestPos = Vector3.zero;

            float farthestDist = 0f;
            Vector3 farthestPos = Vector3.zero;

            // try up to maxNumRandomPositionSamples to randomly find a gameboard position that is:
            // - farther than minTargetDist from sourcePos
            // - farther than minTargetSpacing from all otherObjects (if any)
            // - includes IsPointOnInnerGridNode CheckFit test
            // Fallback to returning farthest random point on gameboard found, ignoring farEnoughFromOthers
            bool found = false;
            for (int i = 0; i < maxNumRandomPositionSamples && !found; i++)
            {
                gameBoard.FindRandomPosition(out pos);

                // check distances from source and others
                float distFromSource = DemoUtil.GetXZDistance(sourcePos, pos);
                bool farEnoughFromOthers = true;
                if (otherObjects != null)
                {
                    for (int j = 0; j < otherObjects.Count && farEnoughFromOthers; j++)
                    {
                        float distFromOther = DemoUtil.GetXZDistance(otherObjects[j].transform.position, pos);
                        farEnoughFromOthers = distFromOther > minTargetSpacing;
                    }
                }

                // have we found a good random position?
                found = distFromSource >= minTargetDist &&
                        farEnoughFromOthers &&
                        IsPointOnInnerGridNode(pos);

                // cache closestPos
                if (distFromSource > minTargetDist && farEnoughFromOthers &&
                    (closestDist == 0f || distFromSource < closestDist))
                {
                    closestDist = distFromSource;
                    closestPos = pos;
                }

                // cache farthestPos, ignoring farEnoughFromOthers
                if (distFromSource > farthestDist)
                {
                    farthestDist = distFromSource;
                    farthestPos = pos;
                }
            }

            if (found) return pos;
            else if (closestDist > minTargetDist) return closestPos;
            else return farthestPos;
        }


        // Calls gameboard's FindNearestFreePosition() method
        public Vector3 GetClosestPointOnCurrentSurface(Vector3 referencePoint)
        {
            if (gameBoard == null) return default;

            Vector3 surfacePoint;
            gameBoard.FindNearestFreePosition(referencePoint, out surfacePoint);

            return surfacePoint;
        }


        // -----------------
        // Player targeting of gameBoard

        public bool RaycastToGameboard(out float dist, out Vector3 gameBoardPt)
        {
            Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward + raycastVectorOffset);

            if (gameBoard.RayCast(ray, out gameBoardPt))
            {
                dist = Vector3.Distance(Camera.main.transform.position, gameBoardPt);
                return true;
            }

            dist = 0f;
            gameBoardPt = Vector3.zero;
            return false;
        }

        public bool IsPointOnInnerGridNode(Vector3 gameBoardPt)
        {
            float checkFitSize = gameBoard.Settings.TileSize * checkFitMultiplier;
            return gameBoard.CheckFit(gameBoardPt, checkFitSize);
        }

        // Converts a world position to a node on the game board.
        private GridNode PositionToGridNode(Vector3 worldPosition)
        {
            return new GridNode(PositionToTile(worldPosition));
        }

        // Converts a world position to grid coordinates.
        private Vector2Int PositionToTile(Vector3 position)
        {
            return new Vector2Int
            (
                Mathf.FloorToInt(position.x / gameBoard.Settings.TileSize),
                Mathf.FloorToInt(position.z / gameBoard.Settings.TileSize)
            );
        }


        // Calls gameboard's FindNearestFreePosition() method
        public void FindClosestInnerGameboardPoint(Vector3 pos, out bool hasSurface,
                                                    out Vector3 validGameboardPt,
                                                    out Vector3 visibleValidGameboardPt,
                                                    bool allowGameboardEdgePoints = false)
        {
            Vector3 closestPosOnGameboard;
            hasSurface = gameBoard.FindNearestFreePosition(pos, out closestPosOnGameboard);
            validGameboardPt = closestPosOnGameboard;
            visibleValidGameboardPt = closestPosOnGameboard;
        }


        // -----------------
        // Locomoting on gameBoard

        public Path CalculateLocomotionPath(Vector3 startPos, Vector3 endPos)
        {
            Path path;
            AgentConfiguration agentConfiguration = new AgentConfiguration(0, 0f, PathFindingBehaviour.SingleSurface);
            gameBoard.CalculatePath(startPos, endPos, agentConfiguration, out path);
            return path;
        }


        // -----------------
        // Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (isScanning)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawCube(scanOrigin, new Vector3(0.08f, 0.01f, 0.08f));
            }
        }
#endif
    }
}
