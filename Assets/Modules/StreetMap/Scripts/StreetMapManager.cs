using Niantic.ARDK.LocationService;

using Niantic.ARVoyage.FrostFlower;
using Niantic.ARVoyage.Utilities;

using Nexus.Map;

using Mapbox.Unity.Location;
using Mapbox.Utils;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Manager class taht adds VPS features to MapManager base class, including:
    /// - Management of screenspace VPS location/cluster markers on the streetmap (via StreetMapMarkerManager), 
    ///   including selection/deselection of markers
    /// - Management of UI elements (via VPSPane and MapControlPanel) for displaying hint text, 
    ///   VPS location names and hint images, distance meter, etc.
    /// - Management of SearchThisArea, BypassGPS buttons and functionality
    /// - Utility methods such as ActivateStreetMap, CenterOnAndSelectIdentifier, RecenterOnUser, 
    ///   ZoomToFitNearbyMarkers, IsUserCloseEnoughToLocalize
    /// Within the app experience, this manager is primary used by StateVpsStreetMap.
    /// </summary>
    public class StreetMapManager : MapManager, ISceneDependency
    {
        private bool allowAutoSelectNearbyVpsLocation = false;

        private const string searchAreaHintText = "Tap below to search for Wayspots in this area!";
        private const string agnosticLocationHintText = "Find the Wayspot pictured to activate a VPS experience!";
        private const string nearGardenLocationHintText = "Let's go see how your garden looks now! Get within 15 meters to localize and check it out!";
        private const string gardenLocationHintText = "Let's go see how your garden looks now! Localize and check it out!";
        private const string nearLocalizeHintText = "Get closer! You’ll need to be within 15 meters of the Wayspot to start!";
        private const string closeEnoughToLocalizeHintText = "You're here! Tap the Localize button to start!";
        private const string buttonTextLocalize = "Localize!";
        private const string bypassGPSHintText = "If you think you're closer to the landmark than the GPS is showing, tap the button below!";

        public const string findLocationHintText = "Find and tap on a nearby Wayspot to begin!";
        public const string teleportToCityText = "No nearby Wayspots found! Teleport to a VPS-activated city?";
        public const string teleportToSearchText = "No nearby Wayspots found! Go back to the last found search area?";

        // Used for single-location clusters and superblooms.
        const float DefaultLocationMapZoom = 17.125f;

        [SerializeField] public float minMapZoom = 10f;    // ~10km radius
        [SerializeField] public float maxMapZoom = 20f;

        [SerializeField] public MapMarkerAvatar userMarker = null;

        [SerializeField] public MockLocationProvider mockLocationProvider;
        private bool switchedToMockUserLocation = false;

        public bool StreetMapManagerInitialized { get; private set; }

        private VpsSceneManager vpsSceneManager;
        private VpsCoverageManager vpsCoverageManager;
        private StreetMapMarkerManager streetMapMarkerManager;
        private VpsPane vpsPane;

        [SerializeField] public float metersThresholdToLocalize = 15f;
        [SerializeField] public float metersThresholdToAutoSelect = 40f;
        bool closeEnoughToLocalize;
        bool nearToLocalize;

        [SerializeField] public float metersThresholdToSuggestBypassGPS = 30f;
        [SerializeField] public float thresholdSecsToSuggestBypassGPS = 30f;

        private bool offeringBypassGPS = false;
        private float timeStartedNearVPSLocation = 0f;

        [HideInInspector] public int selectedVPSLocationIndex = -1;
        [HideInInspector] public bool advanceToLocalization = false;

        [HideInInspector] public float timeLastUnselected = 0f;

        public bool IsCameraMoving
        {
            get { return Time.time < movingCameraUntilTime; }
        }

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            vpsCoverageManager = SceneLookup.Get<VpsCoverageManager>();
            streetMapMarkerManager = SceneLookup.Get<StreetMapMarkerManager>();
            vpsPane = SceneLookup.Get<VpsPane>();

            vpsPane.distanceIndicatorParent.SetActive(false);

            movement.minZoom = minMapZoom;
            movement.maxZoom = maxMapZoom;

            vpsCoverageManager.CoverageQueryStateChange.AddListener((bool status) =>
            {
                InterpolationUtil.LinearInterpolation(vpsPane.searchingStatus, vpsPane.searchingStatus,
                    duration: (status) ? 0 : .300f,
                    onComplete: () => vpsPane.searchingStatus.SetActive(status)
                );
            });

            vpsCoverageManager.CoverageQueryError.AddListener((string error) =>
            {
                // Deselect location.
                VPSLocationUnselected();

                // Clear current markers.
                streetMapMarkerManager.ClearDynamicMarkers();

                // Null search location.
                streetMapMarkerManager.SearchCoordinates = new Vector2d(0, 0);

                // Show error.
                vpsPane.ShowError(error);
            });
        }

        public void ActivateStreetMap(bool val)
        {
            if (val) vpsSceneManager.SetStreetMapCameraActive();
            mainCamera.enabled = val;
            map.gameObject.SetActive(val);
            movement.enabled = val;
            streetMapMarkerManager.vpsLocationsCanvas.gameObject.SetActive(val);
            streetMapMarkerManager.mapAvatar.gameObject.SetActive(val);
            streetMapMarkerManager.mapMarkerAvatar.gameObject.SetActive(val);
            mapControlPanel.gameObject.SetActive(val);
            ClearBypassGPS();
            closeEnoughToLocalize = false;
            nearToLocalize = false;

            SwitchToMockUserLocation();

            if (val && StreetMapManagerInitialized)
            {
                RecenterOnUser(MapZoom, duration: 0f);
            }
        }

        protected override void Update()
        {
            base.Update();

            // once markers exist, which means a location exists, 
            // we're fully initialized 
            if (!StreetMapManagerInitialized)
            {
                if (streetMapMarkerManager.StreetMapMarkerManagerInitialized)
                {
                    Debug.Log("StreetMapMgr Initialized");
                    StreetMapManagerInitialized = true;
                    RecenterOnUser(MapZoom, duration: 0f);
                }
            }

            CheckDebugKeyPresses();
        }


        // Localize button on VPS pane
        public void PaneButtonClick()
        {
            advanceToLocalization = true;
        }

        // Topmost button on VPS pane for BypassGPS
        public void BypassGpsButtonClick()
        {
            if (offeringBypassGPS)
            {
                Debug.Log("offeringBypassGPS advanceToLocalization");
                advanceToLocalization = true;
            }
        }

        // Topmost button on VPS pane for SearchThisArea 
        public void SearchButtonClick()
        {
            SearchMapCenter();
        }

        // Topmost button on VPS pane for Teleporting to nearby wayspots
        public void TeleportButtonClick()
        {
            if (streetMapMarkerManager.LastSuccessfulSearchCoordinates != null)
            {
                CenterOnAndSearchArea((Vector2d)streetMapMarkerManager.LastSuccessfulSearchCoordinates);
            }
            else
            {
                LatLng teleportLocation = (LatLng)vpsCoverageManager.GetNearestTeleportLocation(map.CenterLatitudeLongitude.ToLatLng());
                CenterOnAndSearchArea(teleportLocation.ToVector2d());
            }
        }

        public void VPSLocationUnselected()
        {
            // unselect currently selected location, if any
            streetMapMarkerManager.MapMarkerLocationUnselected();
            vpsPane.vpsLocationOnStreetMap = null;

            // only clear CurrentVpsDataEntry if we're actively in the map state
            // otherwise the map may be completing initializing while we've started the
            //  app in some other state that has set a mock CurrentVpsDataEntry
            if (vpsSceneManager.GetStreetMapCameraActive())
            {
                vpsSceneManager.CurrentVpsDataEntry = null;
                vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.HintImageDefault);
            }

            vpsPane.titleText.text = "";
            vpsPane.ClearHintImage();
            vpsPane.ShowHint(findLocationHintText);
            vpsPane.paneButtonText.text = buttonTextLocalize;
            vpsPane.paneButton.interactable = false;
            vpsPane.distanceIndicatorParent.SetActive(false);

            ClearBypassGPS();

            timeLastUnselected = Time.time;
        }

        public void VPSLocationSelected(StreetMapMarkerLocation vpsLocation)
        {
            ClearBypassGPS();

            bool toggledOff = streetMapMarkerManager.MapMarkerLocationSelected(vpsLocation);
            if (toggledOff)
            {
                VPSLocationUnselected();
                return;
            }

            vpsSceneManager.CurrentVpsDataEntry = vpsLocation.VpsDataEntry;
            vpsPane.vpsLocationOnStreetMap = vpsLocation;

            // Fill in pane
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.HintImageDefault);
            vpsPane.titleText.text = vpsLocation.VpsDataEntry.name;
            vpsPane.debugText.text = vpsLocation.name;
            ManageUserDistanceToVPSLocations(); // sets hint text

            // Disable the pane button
            vpsPane.paneButton.interactable = false;

            // Show pane and distance indicator
            vpsPane.distanceIndicatorParent.SetActive(true);

            // Start the hintImage fetch after the pane game object is active, otherwise Unity throws an exception
            vpsPane.FetchHintImage(vpsLocation.VpsDataEntry);
        }

        private void ClearBypassGPS()
        {
            if (offeringBypassGPS)
            {
                vpsPane.bypassGpsButton.gameObject.SetActive(false);
            }

            offeringBypassGPS = false;
            timeStartedNearVPSLocation = 0f;
        }

        public bool IsUserCloseEnoughToLocalize()
        {
            // If any of the required components haven't been initialized because
            // we skipped over the map for debugging, just return false
            if (streetMapMarkerManager == null || vpsPane == null ||
                vpsPane.vpsLocationOnStreetMap == null || LocationProvider == null)
            {
                return false;
            }

            float distToUser = Geography.GetDistanceBetweenLatLongs(
                vpsPane.vpsLocationOnStreetMap.LatitudeLongitude,
                LocationProvider.CurrentLocation.LatitudeLongitude);

            return distToUser <= metersThresholdToLocalize + 1;
        }

        public void ManageUserDistanceToVPSLocations()
        {
            // Display distance to selected VPS location
            if (vpsPane.vpsLocationOnStreetMap != null)
            {
                float distToUser = Geography.GetDistanceBetweenLatLongs(
                    vpsPane.vpsLocationOnStreetMap.LatitudeLongitude,
                    LocationProvider.CurrentLocation.LatitudeLongitude);

                // Display distance in km if at least 1km away
                if (distToUser >= 1000f)
                {
                    // Display tenths-digit if < 10km away
                    int kmInTenths = (int)(distToUser / 100f);
                    if (kmInTenths < 100)
                    {
                        vpsPane.distanceIndicatorText.text = ((float)kmInTenths / 10f) + " km";
                    }
                    else
                    {
                        vpsPane.distanceIndicatorText.text = (kmInTenths / 10) + " km";
                    }
                }
                else
                {
                    vpsPane.distanceIndicatorText.text = (int)distToUser + " m";
                }

                // Check if user is close enough to localize
                // Enable the localize button if close
                closeEnoughToLocalize = (distToUser <= metersThresholdToLocalize + 1);
                nearToLocalize = !closeEnoughToLocalize && (distToUser <= metersThresholdToSuggestBypassGPS);
                vpsPane.paneButton.interactable = closeEnoughToLocalize;

                // Manage timeStartedNearVPSLocation
                if (timeStartedNearVPSLocation == 0f && nearToLocalize)
                {
                    timeStartedNearVPSLocation = Time.time;
                }
                else if (timeStartedNearVPSLocation > 0f && !closeEnoughToLocalize && !nearToLocalize)
                {
                    ClearBypassGPS();
                }

                // Check if should offer bypassGPS
                // Don't offer if close enough to localize
                if (!IsUserCloseEnoughToLocalize() &&
                    timeStartedNearVPSLocation > 0f &&
                    Time.time - timeStartedNearVPSLocation > thresholdSecsToSuggestBypassGPS)
                {
                    offeringBypassGPS = true;
                    vpsPane.bypassGpsButton.gameObject.SetActive(true);
                }

                // Hint for bypassGPS
                if (offeringBypassGPS)
                {
                    vpsPane.ShowHint(bypassGPSHintText);
                }

                // Hints for bespoke locations
                else if (vpsPane.vpsLocationOnStreetMap.IsBespoke)
                {
                    if (closeEnoughToLocalize)
                    {
                        vpsPane.ShowHint(closeEnoughToLocalizeHintText);
                    }
                    else if (nearToLocalize)
                    {
                        vpsPane.ShowHint(nearLocalizeHintText);
                    }
                    else
                    {
                        vpsPane.ShowHint(agnosticLocationHintText); //vpsPane.vpsLocationOnStreetMap.BespokeDescription);
                    }
                }

                // Hints for frostflower locations
                else
                {
                    bool gardenGrowing = false;
                    if (vpsSceneManager.PersistentFrostFlowerStateLookup.ContainsKey(vpsPane.vpsLocationOnStreetMap.VpsDataEntry.identifier))
                    {
                        FrostFlowerSaveData saveData = vpsSceneManager.PersistentFrostFlowerStateLookup[vpsPane.vpsLocationOnStreetMap.VpsDataEntry.identifier];
                        gardenGrowing = saveData.locationState != FrostFlowerLocationState.Unvisited;
                    }

                    if (gardenGrowing)
                    {
                        vpsPane.ShowHint(closeEnoughToLocalize ? gardenLocationHintText : nearGardenLocationHintText);
                    }
                    else if (closeEnoughToLocalize)
                    {
                        vpsPane.ShowHint(closeEnoughToLocalizeHintText);
                    }
                    else if (nearToLocalize)
                    {
                        vpsPane.ShowHint(nearLocalizeHintText);
                    }
                    else
                    {
                        vpsPane.ShowHint(agnosticLocationHintText);
                    }
                }
            }

            // If autoSelectNearbyVpsLocation feature is activated, 
            // if a VPS location is NOT selected, check if user is near one, and auto-select it
            if (allowAutoSelectNearbyVpsLocation && vpsPane.vpsLocationOnStreetMap == null)
            {
                StreetMapMarkerLocation nearbyVpsLocation = streetMapMarkerManager.FindNearbyVpsLocation(userMarker.LatitudeLongitude);
                if (nearbyVpsLocation != null)
                {
                    VPSLocationSelected(nearbyVpsLocation);
                }
            }
        }

        public void SearchMapCenter()
        {
            streetMapMarkerManager.SearchMapAtCoordinates(map.CenterLatitudeLongitude);
        }

        public override void RecenterOnUser(float zoom, bool refreshSearch = true, float duration = 1f)
        {

            base.RecenterOnUser(zoom, refreshSearch, duration);

            // re-search at user center
            if (HasLocation && refreshSearch)
            {
                streetMapMarkerManager.SearchMapAtCoordinates(LocationProvider.CurrentLocation.LatitudeLongitude);
            }
        }

        public void TeleportUserHere()
        {
            float x = (float)map.CenterLatitudeLongitude.x;
            float y = (float)map.CenterLatitudeLongitude.y;
            mockLocationProvider.SetMockLocation(new Vector2d(y, x));
            VPSLocationUnselected();

            // Update search after teleporting user.
            SearchMapCenter();
        }

        public void AllowFullZoom()
        {
            movement.minZoom = minMapZoom = 2f;
        }

        public void CenterOnAndSelectIdentifier(string identifier, float duration = 0f)
        {
            VpsDataEntry vpsDataEntry = vpsCoverageManager.GetVpsDataEntryByIdentifier(identifier);
            StartCoroutine(CenterOnAndSelectVpsDataEntryRoutine(vpsDataEntry, duration));
        }

        public void CenterOnAndSelectVpsDataEntry(VpsDataEntry vpsDataEntry, float duration = 0f)
        {
            StartCoroutine(CenterOnAndSelectVpsDataEntryRoutine(vpsDataEntry, duration));
        }

        private IEnumerator CenterOnAndSelectVpsDataEntryRoutine(VpsDataEntry vpsDataEntry, float duration = 0f)
        {
            // center on it
            Vector2d latLon = new Vector2d(vpsDataEntry.Latitude, vpsDataEntry.Longitude);

            // start pan/zoom to latLon
            CenterOnLocation(latLon, DefaultLocationMapZoom, duration: duration);

            // wait for it to complete
            yield return new WaitForSeconds(duration);

            // set CurrentVpsDataEntry to this location;
            // refreshMarkers will auto-select the location once the pan/zoom is done
            vpsSceneManager.CurrentVpsDataEntry = vpsDataEntry;

            // search this area, to instantiate markers if we've moved outside of the search radius.
            if (streetMapMarkerManager.GetDistToSearchCenter() > VpsCoverageManager.SearchRadiusInMeters)
            {
                SearchMapCenter();
                yield return new WaitForSeconds(1f);
            }

            // select the location (to ensure the VPS Pane is updated)
            StreetMapMarkerLocation vpsLocation = streetMapMarkerManager.GetVpsLocationByVpsDataEntry(vpsDataEntry);
            if (vpsLocation != null) VPSLocationSelected(vpsLocation);
        }

        public void CenterOnAndSearchArea(Vector2d latLon, float zoom = 0f)
        {
            if (zoom <= 0f) zoom = initialMapZoom;

            StartCoroutine(CenterOnAndSearchAreaRoutine(latLon, zoom));
        }

        private IEnumerator CenterOnAndSearchAreaRoutine(Vector2d latLon, float zoom)
        {
            // start pan/zoom to latLon
            float duration = 1f;
            CenterOnLocation(latLon, zoom, duration);

            // wait for it to complete
            yield return new WaitForSeconds(duration);

            // search this area, to instantiate markers
            SearchMapCenter();
        }


        public void SwitchToMockUserLocation()
        {
            if (switchedToMockUserLocation) return;

#if UNITY_EDITOR
            Debug.Log("SwitchToMockUserLocation");

            // Wait until Mapbox has fully initialized
            if (LocationProviderFactory.Instance == null)
            {
                Debug.LogError("SwitchToMockUserLocation called too early, LocationProviderFactory.Instance is null, not initialized yet");
                return;
            }

            // Reversed because MapBox is terrible.
            mockLocationProvider.SetMockLocation(new Vector2d(
                DevSettings.MockUserLatLongInEditor.Longitude,
                DevSettings.MockUserLatLongInEditor.Latitude));

            //userOnMapPositioner.RefreshLocationProvider();

            locationProviderFactory.SetEditorLocationProvider(mockLocationProvider);
            LocationProvider = mockLocationProvider;
#endif
            switchedToMockUserLocation = true;
        }


        private void CheckDebugKeyPresses()
        {
            if (Input.GetKey(KeyCode.Q))
            {
                map.SetZoom(Mathf.Clamp(map.Zoom - (Time.deltaTime * 2f), 0, 22));
                map.UpdateMap();
            }

            if (Input.GetKey(KeyCode.E))
            {
                map.SetZoom(Mathf.Clamp(map.Zoom + (Time.deltaTime * 2f), 0, 22));
                map.UpdateMap();
            }

            // if (Input.GetKey(KeyCode.Z))
            // {
            //     ZoomToFitNearbyMarkers(zoomInAllowed: true);
            // }
        }
    }
}