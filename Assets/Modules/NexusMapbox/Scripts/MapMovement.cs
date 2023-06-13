using System.Collections;
using Mapbox.Unity.Map;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using Nexus.UI;
using Nexus.Util;
using UnityEngine;
using System;

namespace Nexus.Map
{
    /// <summary>
    /// Handles user gestures on map such as dragging or flicking to pan, and pinching to zoom.
    /// </summary>    
    public class MapMovement : MonoBehaviour
    {
        public event System.Action OnPinchEnd;

        [SerializeField]
        public Camera ReferenceCamera;

        [SerializeField]
        private AbstractMap map;

        [SerializeField]
        private MapManager mapManager;

        [HideInInspector]
        public float minZoom = 2f;

        [HideInInspector]
        public float maxZoom = 20f;

        [SerializeField]
        private float deceleration = 4;

        private float startDragThreshold = 5f;
        private float dragVelocityThreshold = 10f;

        private float mouseZoomSpeed = 0.1f;
        private float pinchZoomSpeed = 0.005f;

        private bool potentialDragStarted;
        private bool isDragging;
        private Vector2 startDragPos;
        private Vector3 startDragWorldPos;
        private Vector2d startDragCenterMercator;

        private Vector2 dragPos;
        private Vector2 lastDragPos;

        private Vector2 dragVelocity;
        private HistoryBuffer<Vector2> velocityBuffer = new HistoryBuffer<Vector2>(5);
        private bool animatingDragRelease;

        private bool isPinching;
        private float startPinchZoom;
        private float startPinchRadius;
        private Vector2 startPinchCentre;

        private bool mapInitialized;
        private bool ignoreFrame;
        private bool isAnimatingZoom;

        public float MaxZoom => maxZoom;

        private void Awake()
        {
            map.OnInitialized += () =>
            {
                mapInitialized = true;
            };
        }

        private void OnEnable()
        {
            ignoreFrame = true;
        }

        private void OnDisable()
        {
            isDragging = false;
            isPinching = false;
            potentialDragStarted = false;
            animatingDragRelease = false;
            isAnimatingZoom = false;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                isDragging = false;
                isPinching = false;
                potentialDragStarted = false;
                isAnimatingZoom = false;
            }
        }

        private void Update()
        {
            if (!mapInitialized)
            {
                return;
            }

            if (ignoreFrame)
            {
                ignoreFrame = false;
                return;
            }

            if (isAnimatingZoom)
            {
                return;
            }

            PointerGestures.GetPinchInfo(out var pinchRadius, out var pinchCentre);

            bool newIsPinching = pinchRadius > 0;

            if (!isPinching && newIsPinching)
            {
                // start pinch
                startPinchZoom = map.Zoom;
                startPinchRadius = pinchRadius;
                velocityBuffer.Clear();
                isPinching = true;
                animatingDragRelease = false;
                startPinchCentre = pinchCentre;
                mapManager.SetCenteredOnUser(false);
            }

            if (isPinching && !newIsPinching)
            {
                // end pinch
                OnPinchEnd?.Invoke();

                // reset drag if a finger still down
                if (PointerGestures.PointerPressed)
                {
                    ResetDragStart();
                }
            }

            isPinching = newIsPinching;

            if (!isDragging && !potentialDragStarted && PointerGestures.PointerDownThisFrame && !PointerGestures.PointerOverGameObject)
            {
                potentialDragStarted = true;
                ResetDragStart();
            }

            if (PointerGestures.PointerPressed)
            {
                // don't stop drag deceleration when clicking on a map marker
                if (animatingDragRelease && PointerGestures.PointerOverGameObject)
                {
                    return;
                }

                dragPos = PointerGestures.PointerPos;

                if (potentialDragStarted && !isDragging)
                {
                    var dragDistance = Vector2.Distance(startDragPos, dragPos);

                    if (dragDistance > startDragThreshold)
                    {
                        isDragging = true;
                        potentialDragStarted = false;
                        mapManager.SetCenteredOnUser(false);
                    }
                }

                if (!isPinching)
                {
                    var dragDelta = (dragPos - lastDragPos);
                    var v = dragDelta / Time.deltaTime;

                    velocityBuffer.Add(v);
                }
            }

            if (PointerGestures.PointerUpThisFrame)
            {
                potentialDragStarted = false;

                if (isDragging)
                {
                    isDragging = false;

                    int q1Ctr = 0;
                    int q2Ctr = 0;
                    int q3Ctr = 0;
                    int q4Ctr = 0;
                    int mostPerQuadrant = 0;
                    int mostCommonQuadrant = 0;

                    // Two passes on accumulated pointer directions:
                    // First pass now detects pointer direction outliers that would result in unwanted reverse-flicks
                    // Second pass filters out the outliers
                    for (int pass = 0; pass < 2; pass++)
                    {
                        dragVelocity = Vector2.zero;
                        int ctr = 0;
                        int numAdded = 0;

                        foreach (var item in velocityBuffer)
                        {
                            // Ignore bogus pointer velocities
                            if (item.x == 0f || item.y == 0f) continue;

                            // In first pass, count number of pointer directions in each quadrant
                            // Determine mostCommonQuadrant as we go, favoring later values over earlier ones
                            int quadrant = 0;
                            if (item.x > 0f && item.y < 0f) quadrant = 1;
                            if (item.x < 0f && item.y < 0f) quadrant = 2;
                            if (item.x < 0f && item.y > 0f) quadrant = 3;
                            if (pass == 0)
                            {
                                if (quadrant == 0)
                                {
                                    ++q1Ctr;
                                    if (q1Ctr >= mostPerQuadrant) { mostCommonQuadrant = 0; mostPerQuadrant = q1Ctr; }
                                }
                                else if (quadrant == 1)
                                {
                                    ++q2Ctr;
                                    if (q2Ctr >= mostPerQuadrant) { mostCommonQuadrant = 1; mostPerQuadrant = q2Ctr; }
                                }
                                else if (quadrant == 2)
                                {
                                    ++q3Ctr;
                                    if (q3Ctr >= mostPerQuadrant) { mostCommonQuadrant = 2; mostPerQuadrant = q3Ctr; }
                                }
                                else if (quadrant == 3)
                                {
                                    ++q4Ctr;
                                    if (q4Ctr >= mostPerQuadrant) { mostCommonQuadrant = 3; mostPerQuadrant = q4Ctr; }
                                }
                            }

                            if (pass == 0 ||
                                // In second pass, filter out pointer directions not matching the most common quadrant
                                (pass == 1 && quadrant == mostCommonQuadrant))
                            {
                                dragVelocity += item;
                                ++numAdded;
                            }

                            ++ctr;
                        }

                        dragVelocity /= numAdded;

                        if (dragVelocity.magnitude > dragVelocityThreshold)
                        {
                            animatingDragRelease = true;

                            if (pass == 0)
                            {
                                Debug.Log("DragRelease: mostCommonQuadrant " + mostCommonQuadrant + " (" + q1Ctr + " " + q2Ctr + " " + q3Ctr + " " + q4Ctr + ")");
                            }
                            else
                            {
                                Debug.Log("DragRelease: Filtered out " + (ctr - numAdded));
                            }
                        }
                    }
                }
            }

            lastDragPos = dragPos;

            if (animatingDragRelease)
            {
                var velocityReduction = (deceleration * Time.deltaTime) * dragVelocity;

                dragVelocity -= velocityReduction;

                if (dragVelocity.magnitude < dragVelocityThreshold)
                {
                    animatingDragRelease = false;
                }

                dragPos += dragVelocity * Time.deltaTime;
            }

            if (isPinching)
            {
                float pinchDelta = pinchRadius - startPinchRadius;
                var newZoom = Mathf.Clamp(startPinchZoom + (pinchDelta * pinchZoomSpeed), minZoom, maxZoom);

                ZoomAroundPoint(newZoom, startPinchCentre, map.CenterLatitudeLongitude);
            }
            else if (isDragging || animatingDragRelease)
            {
                var newWorldPos = ScreenToWorldPos(dragPos);
                var worldDelta = startDragWorldPos - newWorldPos;

                var newCenter = WorldToGeoPos(worldDelta, startDragCenterMercator);

                //Debug.Log("UpdateMapfrom MapMovement " + newCenter.x + ", " + newCenter.y);
                map.UpdateMap(newCenter);
            }
            else
            {
                // comment this out: allow in-editor mouse zoom while pointer is over buttons
                //if (!PointerGestures.PointerOverGameObject)   
                // but check if pointer is in viewport
                var view = ReferenceCamera.ScreenToViewportPoint(PointerGestures.PointerPos);
                var isOutside = view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1;
                if (!isOutside)
                {
                    // mouse zoom
                    var scrollDelta = PointerGestures.ScrollWheelDelta;
                    if (scrollDelta != 0)
                    {
                        float zoomDelta = scrollDelta * mouseZoomSpeed * Time.deltaTime;
                        var newZoom = Mathf.Clamp(map.Zoom + zoomDelta, minZoom, maxZoom);

                        ZoomAroundPoint(newZoom, PointerGestures.PointerPos, map.CenterLatitudeLongitude);
                        mapManager.SetCenteredOnUser(false);
                    }
                }
            }
        }

        private void ResetDragStart()
        {
            startDragPos = PointerGestures.PointerPos;
            startDragWorldPos = ScreenToWorldPos(PointerGestures.PointerPos);
            startDragCenterMercator = Conversions.LatLonToMeters(map.CenterLatitudeLongitude);
            velocityBuffer.Clear();
            animatingDragRelease = false;
        }

        private void ZoomAroundPoint(float newZoom, Vector2 screenPoint, Vector2d centerLatLon)
        {
            var currentWorldOffset = ScreenToWorldPos(screenPoint);

            ZoomAroundWorldPos(newZoom, currentWorldOffset, centerLatLon);
        }

        private Vector3 ScreenToWorldPos(Vector2 screenPointerPos)
        {
            return ReferenceCamera.ScreenToWorldPoint(new Vector3(screenPointerPos.x, screenPointerPos.y, ReferenceCamera.transform.localPosition.y));
        }

        private Vector2d WorldToGeoPos(Vector3 worldPos, Vector2d mercatorRefPoint)
        {
            var scaleFactor = Mathf.Pow(2, (map.InitialZoom - map.AbsoluteZoom));
            var localPos = map.Root.InverseTransformPoint(worldPos);
            return localPos.GetGeoPosition(mercatorRefPoint, map.WorldRelativeScale * scaleFactor);
        }


        private void ZoomAroundWorldPos(float newZoom, Vector3 worldPos, Vector2d centerLatLon)
        {
            var centerMercator = Conversions.LatLonToMeters(centerLatLon);
            var curPointerGeoPos = WorldToGeoPos(worldPos, centerMercator);

            // two update method
            // ideally would do the necessary calcs on the center offset and do in one update call

            // do the zoom around center
            map.UpdateMap(centerLatLon, newZoom);

            // recenter around the pointer pos
            var newPointerGeoPos = WorldToGeoPos(worldPos, centerMercator);
            var centerOffset = curPointerGeoPos - newPointerGeoPos;

            var newCenter = centerLatLon + centerOffset;

            map.UpdateMap(newCenter);
        }

        public Vector2d ScreenPointToLatLon(Vector2 screenPoint, Vector2d refPoint)
        {
            var worldPos = ScreenToWorldPos(screenPoint);
            return WorldToGeoPos(worldPos, Conversions.LatLonToMeters(refPoint));
        }

        public void AnimateZoomAroundPoint(Vector2d zoomPoint, float newZoom, float duration = 0.5f) //, Ease ease = Ease.InOutCubic)
        {
            if (isAnimatingZoom)
            {
                return;
            }

            float startZoom = map.Zoom;
            InterpolationUtil.EasedInterpolation(gameObject, gameObject,
                InterpolationUtil.EaseInOutCubic, duration,
                onUpdate: (t) =>
                {
                    ZoomAroundWorldPos(Mathf.Lerp(startZoom, newZoom, t),
                                        map.GeoToWorldPosition(zoomPoint), map.CenterLatitudeLongitude);
                }
            );

            isAnimatingZoom = false;
        }


        public void AnimateZoomAndPan(Vector2d targetCentreLatLon, float newZoom, float duration) //, Ease ease)
        {
            //Debug.Log("UpdateMapfrom AnimateZoomAndPan " + targetCentreLatLon.x + ", " + targetCentreLatLon.y);
            if (isAnimatingZoom)
            {
                return;
            }

            var startLatLon = map.CenterLatitudeLongitude;
            float startZoom = map.Zoom;

            // if nothing to do, bail
            if (startLatLon.x == targetCentreLatLon.x &&
                startLatLon.y == targetCentreLatLon.y &&
                startZoom == newZoom)
            {
                //Debug.Log("UpdateMapfrom AnimateZoomAndPan already there");
                return;
            }

            InterpolationUtil.EasedInterpolation(gameObject, gameObject,
                InterpolationUtil.EaseInOutCubic, duration,
                onUpdate: (t) =>
                {
                    map.UpdateMap(Vector2d.Lerp(startLatLon, targetCentreLatLon, t),
                                    Mathf.Lerp(startZoom, newZoom, t));
                }
            );
            isAnimatingZoom = false;
        }


        private void CancelUserInteraction()
        {
            isDragging = false;
            isPinching = false;
            potentialDragStarted = false;
            velocityBuffer.Clear();
            animatingDragRelease = false;
        }
    }
}
