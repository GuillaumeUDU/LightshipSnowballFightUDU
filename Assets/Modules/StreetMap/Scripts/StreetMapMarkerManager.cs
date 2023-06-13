#define HIDE_SPRITE_LAYER_AVATAR

using Mapbox.CheapRulerCs;
using Mapbox.Unity.Location;
using Mapbox.Utils;

using Nexus.Map;

using Niantic.ARDK.LocationService;

using Niantic.ARVoyage.FrostFlower;
using Niantic.ARVoyage.Utilities;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Niantic.ARVoyage.Vps
{

    public enum VpsMarkerType
    {
        FrostFlowerSeed,
        FrostFlowerPlant,
        FrostFlowerBloom,
        Tourist
    }

    /// <summary>
    /// Manager class for drawing and updating the individual map markers on the
    /// VPS location map. It handles dynamic clustering of locations, drawing
    /// locations and clusters based on zoom level, monitoring for "superbloom"
    /// status and various other tasks related to searching the map and selecting
    /// individual locations. 
    /// </summary>
    public class StreetMapMarkerManager : MonoBehaviour, ISceneDependency
    {
        public const float ClusteringZoomThreshold = 15.5f;
        public const int ClusteringPrecisionLevel = 6;

        [Header("Marker Prefabs")]
        [SerializeField] GameObject vpsLocationPrefab;
        [SerializeField] GameObject vpsClusterPrefab;
        [SerializeField] GameObject vpsUserPrefab;

        [Header("Marker Containers")]
        [SerializeField] Transform vpsLocationContainer;
        [SerializeField] Transform vpsClusterContainer;
        [SerializeField] Transform vpsUserContainer;

        [SerializeField] public GameObject vpsLocationsCanvas;

        public bool StreetMapMarkerManagerInitialized { get; private set; }

        public StreetMapMarkerLocation CurrentLocation { get; set; }
        private StreetMapMarkerLocation mockVpsLocation;

        private VpsSceneManager vpsSceneManager;
        private StreetMapManager streetMapManager;
        private VpsPane vpsPane;
        private VpsCoverageManager vpsCoverageManager;

        // Search location.
        public Vector2d SearchCoordinates { get; set; }
        public Vector2d? LastSuccessfulSearchCoordinates { get; private set; } = null;

        // Dynamic cluster/location dictionary.
        List<StreetMapMarkerCluster> streetMapMarkerClusters = new List<StreetMapMarkerCluster>();
        List<StreetMapMarkerLocation> streetMapMarkerLocations = new List<StreetMapMarkerLocation>();
        private bool forceDynamicMarkerRefresh = false;
        private Coroutine refreshDynamicMarkersCoroutine;

        // Cached list of local entries.
        private List<VpsDataEntry> localVpsDataEntries = new List<VpsDataEntry>();

        [SerializeField]
        public MapAvatar mapAvatar;

        [SerializeField]
        public MapMarkerAvatar mapMarkerAvatar;

        private float lastAvatarRotationUpdateTime = 0f;
        private const float avatarRotationUpdatePeriodSecs = 2f;

        // periodically search citywide for needed superbloom notifications
        [SerializeField] private float superbloomNotificationAgeSecs = 30f;
        [SerializeField] private float superbloomSearchEverySecs = 30f;

        public string NotifySuperbloomIdentifier { get; set; } = null;
        private float lastSuperbloomSearchTime = 0f;

        private float lastZoom;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            vpsCoverageManager = SceneLookup.Get<VpsCoverageManager>();
        }

        void OnEnable()
        {
            streetMapManager.map.OnInitialized += OnStreetMapInitialized;
            streetMapManager.map.OnUpdated += OnStreetMapUpdated;

            lastZoom = streetMapManager.map.Zoom;

#if HIDE_SPRITE_LAYER_AVATAR
            // hide the map avatar sprite
            mapAvatar.transform.localScale = Vector3.zero;
#endif
        }

        void OnDisable()
        {
            streetMapManager.map.OnInitialized -= OnStreetMapInitialized;
            streetMapManager.map.OnUpdated -= OnStreetMapUpdated;
        }

        private void OnStreetMapInitialized()
        {
            Debug.Log("StreetMap StreetMapMarkerManagerInitialized");

            // Initial search location comes from initial user position, if it is available yet
            if (streetMapManager.HasLocation)
            {
                Location userLocation = streetMapManager.LocationProvider.CurrentLocation;
                Debug.Log("OnStreetMapInitialized: User location available:" + userLocation.LatitudeLongitude);

                // Set SearchLatitudeLongitude
                SearchCoordinates = userLocation.LatitudeLongitude;
                StreetMapMarkerManagerInitialized = true;
            }

            // If user location not yet available
            else
            {
                Debug.Log("OnStreetMapInitialized: User location not yet available.");
                streetMapManager.OnLocationUpdated += OnUserLocationUpdated;
            }
        }

        // Used during map initialization if needed
        private void OnUserLocationUpdated(Location userLocation)
        {

            if (streetMapManager.HasLocation)
            {
                Debug.Log($"OnStreetMapInitialized: Valid user location update: ({userLocation.LatitudeLongitude.x}, {userLocation.LatitudeLongitude.y})");
                streetMapManager.OnLocationUpdated -= OnUserLocationUpdated;

                // Set SearchLatitudeLongitude
                SearchCoordinates = userLocation.LatitudeLongitude;
                StreetMapMarkerManagerInitialized = true;
            }
            else
            {
                Debug.LogWarning("OnStreetMapInitialized: Invalid user location update.");
            }
        }

        // Distance between the current map view and the last search coordinates.
        public float GetDistToSearchCenter()
        {
            return Geography.GetDistanceBetweenLatLongs(streetMapManager.map.CenterLatitudeLongitude, SearchCoordinates);
        }

        // Queries VpsCoverageManager for new WaySpots to display and handles the response.
        public void SearchMapAtCoordinates(Vector2d latitudeLongitude)
        {
            Debug.Log("SearchMapAtCoordinates: " + latitudeLongitude);
            SearchCoordinates = latitudeLongitude;

            vpsCoverageManager.GetCoverageAtCoordinates(latitudeLongitude.ToLatLng(),
            (List<VpsDataEntry> vpsDataEntries) =>
            {
                localVpsDataEntries = vpsDataEntries;

                if (localVpsDataEntries.Count == 0)
                {
                    // No wayspots found.
                    if (LastSuccessfulSearchCoordinates == null)
                    {
                        vpsPane.ShowHint(StreetMapManager.teleportToCityText);
                    }
                    else
                    {
                        vpsPane.ShowHint(StreetMapManager.teleportToSearchText);
                    }

                    if (vpsPane != null && vpsPane.teleportButton != null)
                    {
                        vpsPane.teleportButton.gameObject.SetActive(true);
                    }

                    ClearDynamicMarkers();
                }
                else
                {
                    // Wayspots found.
                    vpsPane.ShowHint(StreetMapManager.findLocationHintText);

                    LastSuccessfulSearchCoordinates = SearchCoordinates;

                    if (vpsPane != null && vpsPane.searchButton != null)
                    {
                        vpsPane.searchButton.gameObject.SetActive(false);
                    }

                    if (vpsPane != null && vpsPane.teleportButton != null)
                    {
                        vpsPane.teleportButton.gameObject.SetActive(false);
                    }

                    RefreshDynamicMarkers(true);
                }
            });
        }

        // Fired anytime the map is updated based on updates to the map view.
        private void OnStreetMapUpdated()
        {
            // Handle search radius.
            if (GetDistToSearchCenter() > VpsCoverageManager.SearchRadiusInMeters)
            {
                // We're far from search center

                // Deselect location.
                if (CurrentLocation != null) streetMapManager.VPSLocationUnselected();

                // Show search button if we're not animating.
                if (!streetMapManager.IsCameraMoving)
                {
                    vpsPane.ShowHint(StreetMapManager.findLocationHintText);
                    vpsPane.searchButton.gameObject.SetActive(true);
                }

                // Hide teleport button.
                vpsPane.teleportButton.gameObject.SetActive(false);
            }
            else
            {
                // Search button should never be visible if we're within the search radius.
                vpsPane.searchButton.gameObject.SetActive(false);

                if (!streetMapManager.IsCameraMoving && localVpsDataEntries.Count == 0)
                {
                    if (LastSuccessfulSearchCoordinates == null)
                    {
                        vpsPane.ShowHint(StreetMapManager.teleportToCityText);
                    }
                    else
                    {
                        vpsPane.ShowHint(StreetMapManager.teleportToSearchText);
                    }
                    vpsPane.teleportButton.gameObject.SetActive(true);
                }
            }

            // Refresh map markers as needed.
            if (streetMapManager.map.Zoom != lastZoom || forceDynamicMarkerRefresh)
            {
                RefreshDynamicMarkers();

                if (forceDynamicMarkerRefresh)
                {
                    Debug.Log("Dynamic marker refresh forced.");
                    forceDynamicMarkerRefresh = false;
                }

                lastZoom = streetMapManager.map.Zoom;
            }
            else
            {
                // No refresh needed, just update positions.
                UpdateDynamicMarkers();
            }
        }

        // Updates the coordinates of the map markers to match the underlying map.
        private void UpdateDynamicMarkers()
        {
            // Update cluster positions.
            foreach (MapMarker streetMapMarker in streetMapMarkerClusters)
                UpdateMarkerPosition(streetMapMarker);

            // Update location positions.
            foreach (StreetMapMarkerLocation streetMapMarkerLocation in streetMapMarkerLocations)
            {
                UpdateMarkerPosition(streetMapMarkerLocation);

                // Sort selected marker to be last in hierarchy, so its draws on top of other markers
                if (streetMapMarkerLocation.VpsDataEntry == vpsSceneManager.CurrentVpsDataEntry)
                {
                    streetMapMarkerLocation.transform.SetSiblingIndex(streetMapMarkerLocation.transform.parent.childCount - 1);
                }
            }
        }

        // Calculates the on-screen marker position based on the map coordinates it represents.
        private void UpdateMarkerPosition(MapMarker streetMapMarker)
        {
            Vector3 worldPosition = streetMapManager.map.GeoToWorldPosition(streetMapMarker.LatitudeLongitude);
            Vector3 screenPoint = streetMapManager.mainCamera.WorldToScreenPoint(worldPosition);
            screenPoint.z = 0;
            streetMapMarker.Transform.position = screenPoint;
        }

        // Clears all makers from the map.
        public void ClearDynamicMarkers()
        {
            // Clear existing clusters.
            foreach (MapMarker streetMapMarker in streetMapMarkerClusters)
            {
                BubbleScaleUtil.StopRunningScale(streetMapMarker.Transform.gameObject);
                Destroy(streetMapMarker.Transform.gameObject);
            }
            streetMapMarkerClusters.Clear();

            // Clear existing locations.
            foreach (MapMarker streetMapMarker in streetMapMarkerLocations)
            {
                BubbleScaleUtil.StopRunningScale(streetMapMarker.Transform.gameObject);
                Destroy(streetMapMarker.Transform.gameObject);
            }
            streetMapMarkerLocations.Clear();
        }

        // Calculates dynamic "clusters" of nearby VpsDataEntries based on their geohash.
        private IEnumerator GenerateClustersForVpsDataEntries(List<VpsDataEntry> localVpsDataEntries, int clusterPrecision, System.Action<SortedDictionary<string, VpsDataCluster>> callback)
        {
            Debug.Log("Generating clusters.");
            SortedDictionary<string, VpsDataCluster> vpsDataClusters = new SortedDictionary<string, VpsDataCluster>();

            if (localVpsDataEntries != null && localVpsDataEntries.Count > 0)
            {
                double startTime = Time.realtimeSinceStartupAsDouble;
                double yieldTimeOut = .005f;
                int processedCount = 0;

                foreach (VpsDataEntry vpsDataEntry in localVpsDataEntries)
                {
                    bool hasReadyToHarvestLocation = false;

                    {
                        string clusterGeohash = vpsDataEntry.Geohash.Substring(0, clusterPrecision);
                        string clusterKey = clusterGeohash;

                        // Bespoke locations always use full precision so they are never clustered.
                        if (vpsDataEntry.bespokeEnabled)
                        {
                            clusterKey += "-bespoke";
                        }

                        else
                        {
                            // Check if FrostFlower location is ready to harvest
                            if (vpsSceneManager.PersistentFrostFlowerStateLookup.ContainsKey(vpsDataEntry.identifier))
                            {
                                FrostFlowerSaveData saveData = vpsSceneManager.PersistentFrostFlowerStateLookup[vpsDataEntry.identifier];
                                if (saveData != null)
                                {
                                    hasReadyToHarvestLocation = saveData.locationState == FrostFlowerLocationState.Planted &&
                                                                saveData.GetAgeInSeconds() > superbloomNotificationAgeSecs;
                                }
                            }
                        }

                        // See if parent cluster already exists.
                        VpsDataCluster vpsDataCluster;
                        vpsDataClusters.TryGetValue(clusterKey, out vpsDataCluster);

                        // Create and add parent cluster if needed.
                        if (vpsDataCluster == null)
                        {
                            vpsDataCluster = new VpsDataCluster(clusterGeohash);

                            // Default to seed
                            vpsDataCluster.SetType(VpsMarkerType.FrostFlowerSeed);

                            vpsDataClusters.Add(clusterKey, vpsDataCluster);
                        }

                        // Marker type.
                        if (vpsDataEntry.VpsMarkerType == VpsMarkerType.Tourist)
                        {
                            vpsDataCluster.SetType(VpsMarkerType.Tourist);
                        }
                        else if (vpsDataEntry.VpsMarkerType == VpsMarkerType.FrostFlowerBloom)
                        {
                            vpsDataCluster.SetType(VpsMarkerType.FrostFlowerBloom);
                        }

                        // Set if cluster has a ReadyToHarvest location
                        if (hasReadyToHarvestLocation)
                        {
                            vpsDataCluster.HasReadyToHarvestLocation = true;
                        }

                        // Add entry to cluster.
                        vpsDataCluster.vpsDataEntries.Add(vpsDataEntry);

                        // Add to per-cluster lat/long sum for averaging.
                        float entryLatitude = (float)vpsDataEntry.Latitude;
                        float entryLongitude = (float)vpsDataEntry.Longitude;

                        vpsDataCluster.centerLatitude += entryLatitude;
                        vpsDataCluster.centerLongitude += entryLongitude;

                        // Min/Max
                        if (entryLatitude < vpsDataCluster.minLatitude) vpsDataCluster.minLatitude = entryLatitude;
                        if (entryLongitude < vpsDataCluster.minLongitude) vpsDataCluster.minLongitude = entryLongitude;
                        if (entryLatitude > vpsDataCluster.maxLatitude) vpsDataCluster.maxLatitude = entryLatitude;
                        if (entryLongitude > vpsDataCluster.maxLongitude) vpsDataCluster.maxLongitude = entryLongitude;
                    }

                    if (Time.realtimeSinceStartupAsDouble > startTime + yieldTimeOut)
                    {
                        Debug.Log($"Generate clusters yield. Time: {Time.realtimeSinceStartupAsDouble} Start: {startTime} Count: {processedCount}");
                        yield return null;
                        startTime = Time.realtimeSinceStartupAsDouble;
                    }

                    processedCount++;
                }

                // Divide all center values to find cluster centroids.
                foreach (VpsDataCluster vpsDataCluster in vpsDataClusters.Values)
                {
                    vpsDataCluster.centerLatitude /= (float)vpsDataCluster.vpsDataEntries.Count;
                    vpsDataCluster.centerLongitude /= (float)vpsDataCluster.vpsDataEntries.Count;

                    if (Time.realtimeSinceStartupAsDouble > startTime + yieldTimeOut)
                    {
                        startTime = Time.realtimeSinceStartupAsDouble;
                        yield return null;
                    }
                }
            }

            if (callback != null) callback(vpsDataClusters);
        }

        // Clicks off the routine to refresh the currently displayed
        // set of markers on the map.
        private void RefreshDynamicMarkers(bool bubbleScale = false)
        {
            // Kill running refresh.
            if (refreshDynamicMarkersCoroutine != null)
            {
                StopCoroutine(refreshDynamicMarkersCoroutine);
                refreshDynamicMarkersCoroutine = null;
            }

            // Start dynamic refresh routine.
            refreshDynamicMarkersCoroutine = StartCoroutine(RefreshDynamicMarkersRoutine(bubbleScale));
        }

        // Handles the creation of interactive markers on the street map based
        // on a set of VpsDataEntries.
        private IEnumerator RefreshDynamicMarkersRoutine(bool bubbleScale = false)
        {
            bool reestablishedPreviouslySelectedLocation = false;

            // Calculate marker types for local entries and sort them.
            {

                foreach (VpsDataEntry vpsDataEntry in localVpsDataEntries)
                {
                    // Bespoke logic
                    if (vpsDataEntry.bespokeEnabled)
                    {
                        // Default to tourist.
                        vpsDataEntry.SetType(VpsMarkerType.Tourist);
                    }
                    else
                    {
                        // Default to seed.
                        vpsDataEntry.SetType(VpsMarkerType.FrostFlowerSeed);

                        FrostFlowerSaveData saveData;
                        Debug.Log(vpsDataEntry);
                        vpsSceneManager.PersistentFrostFlowerStateLookup.TryGetValue(vpsDataEntry.identifier, out saveData);
                        if (saveData != null)
                        {
                            long ageInSeconds = saveData.GetAgeInSeconds();

                            if (saveData.locationState == FrostFlowerLocationState.Harvested ||
                                (saveData.locationState == FrostFlowerLocationState.Planted &&
                                 ageInSeconds > superbloomNotificationAgeSecs))
                            {
                                // Use bloom if harvested or ready to harvest.
                                vpsDataEntry.SetType(VpsMarkerType.FrostFlowerBloom);
                            }
                            else if (saveData.locationState == FrostFlowerLocationState.Planted)
                            {
                                // Use plant if planted.
                                vpsDataEntry.SetType(VpsMarkerType.FrostFlowerPlant);
                            }
                        }
                    }
                }

                localVpsDataEntries.Sort(new VpsDataEntrySorter());
            }

            // Clustering
            int currentPrecision = ClusteringPrecisionLevel;
            bool clusterResults = streetMapManager.MapZoom < ClusteringZoomThreshold;

            // Should we cluster or not?
            if (clusterResults)
            {
                // Cluster entries.
                int clusterPrecision = Mathf.Min(12, currentPrecision + 1);

                // Cluster container.
                SortedDictionary<string, VpsDataCluster> localVpsDataClusters = new SortedDictionary<string, VpsDataCluster>();
                yield return GenerateClustersForVpsDataEntries(localVpsDataEntries, clusterPrecision, (vpsDataClusters) =>
                {
                    localVpsDataClusters = vpsDataClusters;
                });

                List<VpsDataCluster> vpsDataClusters = new List<VpsDataCluster>(localVpsDataClusters.Values);
                vpsDataClusters.Sort(new VpsDataClusterSorter());

                // Clear existing entries.
                ClearDynamicMarkers();

                // Draw clusters in search region.
                if (vpsDataClusters != null)
                {
                    foreach (VpsDataCluster vpsDataCluster in vpsDataClusters)
                    {
                        // Create markers
                        {
                            GameObject instance = Instantiate(vpsClusterPrefab, vpsClusterContainer, false);
                            instance.name = vpsDataCluster.geohash;

                            StreetMapMarkerCluster streetMapMarkerCluster = instance.GetComponent<StreetMapMarkerCluster>();
                            streetMapMarkerCluster.Initialize(new Vector2d(vpsDataCluster.centerLatitude, vpsDataCluster.centerLongitude));

                            // do this before SetMarkerType
                            streetMapMarkerCluster.VpsDataCluster = vpsDataCluster;

                            streetMapMarkerCluster.SetMarkerType(vpsDataCluster.VpsMarkerType);

                            streetMapMarkerCluster.Text = vpsDataCluster.vpsDataEntries.Count.ToString();

                            int clusterSize = vpsDataCluster.vpsDataEntries.Count.ToString().Length;

                            streetMapMarkerClusters.Add(streetMapMarkerCluster);

                            if (bubbleScale) BubbleScaleMapMarker(streetMapMarkerCluster);
                        }
                    }
                }
            }
            else
            {
                // No clustering.

                // Clear existing markers.
                ClearDynamicMarkers();

                if (localVpsDataEntries != null)
                {
                    int index = 0;
                    foreach (VpsDataEntry vpsDataEntry in localVpsDataEntries)
                    {

                        // Create marker.
                        GameObject instance = Instantiate(vpsLocationPrefab, vpsLocationContainer, false);
                        instance.name = vpsDataEntry.Geohash;

                        StreetMapMarkerLocation streetMapMarkerLocation = instance.GetComponent<StreetMapMarkerLocation>();
                        streetMapMarkerLocation.Initialize(new Vector2d(vpsDataEntry.Latitude, vpsDataEntry.Longitude));

                        // Cache marker's sibling sort index, so we can restore it later as needed
                        streetMapMarkerLocation.SiblingSortingIndex =
                            // GetSiblingIndex isn't accurate, since the Destroy calls in ClearDynamicMarkers() haven't fully taken effect yet
                            //streetMapMarkerLocation.transform.GetSiblingIndex();
                            // use the loop count instead
                            index++;

                        // This must be set before checking "bespokeness".
                        streetMapMarkerLocation.VpsDataEntry = vpsDataEntry;

                        // reestablish CurrentLocation, if any
                        if (vpsSceneManager.CurrentVpsDataEntry == streetMapMarkerLocation.VpsDataEntry)
                        {
                            // these got nulled out, restore them
                            vpsPane.vpsLocationOnStreetMap = streetMapMarkerLocation;
                            CurrentLocation = streetMapMarkerLocation;
                            CurrentLocation.SetSelected(true);
                            reestablishedPreviouslySelectedLocation = true;
                        }

                        // Bespoke logic
                        if (streetMapMarkerLocation.IsBespoke)
                        {
                            streetMapMarkerLocation.SetMarkerType(VpsMarkerType.Tourist);

                            // Check visited (flagPlanted) state.
                            if (vpsSceneManager.PersistentBespokeStateLookup.ContainsKey(streetMapMarkerLocation.VpsDataEntry.identifier))
                            {
                                bool visited = vpsSceneManager.PersistentBespokeStateLookup[streetMapMarkerLocation.VpsDataEntry.identifier];
                                streetMapMarkerLocation.FlagPlanted = visited;
                            }
                            else
                            {
                                // Unvisited.
                                streetMapMarkerLocation.FlagPlanted = false;
                            }
                        }
                        else
                        // Frost Flower logic
                        {
                            // Frost flower location. Check map state.
                            if (vpsSceneManager.PersistentFrostFlowerStateLookup.ContainsKey(streetMapMarkerLocation.VpsDataEntry.identifier))
                            {
                                // We have saved state.
                                FrostFlowerSaveData saveData = vpsSceneManager.PersistentFrostFlowerStateLookup[streetMapMarkerLocation.VpsDataEntry.identifier];

                                switch (saveData.locationState)
                                {
                                    case FrostFlowerLocationState.Unvisited:
                                        {
                                            streetMapMarkerLocation.SetMarkerType(VpsMarkerType.FrostFlowerSeed);
                                            break;
                                        }
                                    case FrostFlowerLocationState.Planted:
                                        {
                                            streetMapMarkerLocation.SetMarkerType(VpsMarkerType.FrostFlowerPlant);
                                            streetMapMarkerLocation.SetReadyToHarvest(saveData.GetAgeInSeconds() > superbloomNotificationAgeSecs);
                                            break;
                                        }
                                    case FrostFlowerLocationState.Harvested:
                                        {
                                            streetMapMarkerLocation.SetMarkerType(VpsMarkerType.FrostFlowerBloom);
                                            break;
                                        }
                                }
                            }
                            else
                            {
                                // Unvisited.
                                streetMapMarkerLocation.SetMarkerType(VpsMarkerType.FrostFlowerSeed);
                            }
                        }

                        streetMapMarkerLocations.Add(streetMapMarkerLocation);

                        Vector3 targetScale = instance.transform.localScale;
                        if (bubbleScale) BubbleScaleMapMarker(streetMapMarkerLocation);
                    }
                }
            }

            // Logic to bubble scale up with a delay based on distance.
            void BubbleScaleMapMarker(MapMarker streetMapMarker)
            {
                float distance = Geography.GetDistanceBetweenLatLongs(streetMapMarker.LatitudeLongitude, SearchCoordinates);
                float delay = distance / VpsCoverageManager.SearchRadiusInMeters * .5f;

                float targetScale = streetMapMarker.transform.localScale.x;
                streetMapMarker.transform.localScale = Vector3.zero;
                BubbleScaleUtil.ScaleUp(streetMapMarker.gameObject, targetScale, .75f, preWait: delay);
            }

            // tell StreetMapManager if our previously selected location is now gone
            if (vpsSceneManager.CurrentVpsDataEntry != null && !reestablishedPreviouslySelectedLocation)
            {
                streetMapManager.VPSLocationUnselected();
            }

            // Force a map update.
            UpdateDynamicMarkers();
        }


        // Search for unharvested "superblooms" within a citywideSearchProximity from the current device location.
        // A superbloom is a planted frostflower garden older than 30 seconds.
        private void SuperbloomSearch()
        {
            // If we've already chosen a notification, we're done
            if (NotifySuperbloomIdentifier != null) return;

            Debug.Log("Periodic superbloom search.");

            foreach (string identifier in vpsSceneManager.PersistentFrostFlowerStateLookup.Keys)
            {
                FrostFlowerSaveData saveData = vpsSceneManager.PersistentFrostFlowerStateLookup[identifier];

                if (saveData.locationState == FrostFlowerLocationState.Planted)
                {
                    long ageInSeconds = saveData.GetAgeInSeconds();
                    Debug.Log($"FrostFlower garden {identifier}: nofified {saveData.notificationShown}, age {ageInSeconds}");

                    if (ageInSeconds > superbloomNotificationAgeSecs && !saveData.notificationShown)
                    {
                        // Make sure the coverage API knows about this identifier.
                        VpsDataEntry vpsDataEntry = vpsCoverageManager.GetVpsDataEntryByIdentifier(identifier);
                        if (vpsDataEntry != null)
                        {
                            NotifySuperbloomIdentifier = identifier;
                            Debug.Log("Superbloom search found notifySuperbloomIdentifier " + NotifySuperbloomIdentifier);
                        }
                        else
                        {
                            Debug.LogWarning("Superbloom ready, but it has an unknown identifier");
                        }
                    }
                }
            }
        }

        // Returns a Location Marker for a given VpsDataEntry. 
        public StreetMapMarkerLocation GetVpsLocationByVpsDataEntry(VpsDataEntry vpsDataEntry)
        {
            foreach (StreetMapMarkerLocation vpsLocation in streetMapMarkerLocations)
            {
                if (vpsLocation.VpsDataEntry == vpsDataEntry)
                {
                    return vpsLocation;
                }
            }

            Debug.LogWarning("GetVpsLocationByVpsDataEntry couldn't find vpsDataEntry: " + vpsDataEntry.Geohash);
            return null;
        }

        // Loops through the active set of markers to find a nearby location.
        public StreetMapMarkerLocation FindNearbyVpsLocation(Vector2d fromLatLong)
        {
            StreetMapMarkerLocation closestVpsLocation = null;
            List<StreetMapMarkerLocation> nearbyVpsLocations = new List<StreetMapMarkerLocation>();

            // first, efficiently gather list of nearby locations
            int fromLatRounded = (int)(fromLatLong.x * 10f);
            int fromLongRounded = (int)(fromLatLong.y * 10f);
            foreach (StreetMapMarkerLocation vpsLocation in streetMapMarkerLocations)
            {
                //if (vpsLocation.isUser) continue;
                int latRounded = (int)(vpsLocation.LatitudeLongitude.x * 10f);
                int longRounded = (int)(vpsLocation.LatitudeLongitude.y * 10f);
                if (fromLatRounded == latRounded && fromLongRounded == longRounded)
                {
                    nearbyVpsLocations.Add(vpsLocation);
                }
            }

            // on small nearby list, do actual distance checks
            float closestDist = 0f;
            foreach (StreetMapMarkerLocation nearbyVpsLocation in nearbyVpsLocations)
            {
                float dist = Geography.GetDistanceBetweenLatLongs(fromLatLong, nearbyVpsLocation.LatitudeLongitude);
                if (dist <= streetMapManager.metersThresholdToAutoSelect &&
                    closestDist == 0f || dist < closestDist)
                {
                    closestDist = dist;
                    closestVpsLocation = nearbyVpsLocation;
                }
            }

            if (closestVpsLocation != null)
            {
                Debug.Log("FindNearbyVpsLocation found " + closestVpsLocation.VpsDataEntry.name + " among " + nearbyVpsLocations.Count + " nearby locations");
            }

            return closestVpsLocation;
        }

        // Returns a "mock" location for debugging.
        public StreetMapMarkerLocation GetMockVpsLocation()
        {
            if (mockVpsLocation == null)
            {
                mockVpsLocation = Instantiate(vpsLocationPrefab).GetComponent<StreetMapMarkerLocation>();
                mockVpsLocation.VpsDataEntry = null;

                string mockIdentifier = DevSettings.GetFirstMockIdentifier();

                if (mockIdentifier != null)
                {
                    mockVpsLocation.VpsDataEntry = vpsCoverageManager.GetVpsDataEntryByIdentifier(mockIdentifier);
                    if (mockVpsLocation.VpsDataEntry == null)
                    {
                        Debug.LogError("null GetVpsDataEntryById for mockIdentifier " + mockIdentifier);
                    }
                }

                if (mockVpsLocation.VpsDataEntry == null)
                {
                    mockIdentifier = "0";
                    mockVpsLocation.VpsDataEntry = new VpsDataEntry()
                    {
                        identifier = mockIdentifier,
                        name = "Mock VPS Location",
                        latitudeLongitude = new LatLng(45.5232f, -122.6814)
                    };
                }
            }

            return mockVpsLocation;
        }

        // Handle marker deselect.
        public void MapMarkerLocationUnselected(bool forceMapRefresh = false)
        {
            // unselect currently selected location, if any
            if (CurrentLocation != null && CurrentLocation.transform != null && CurrentLocation.transform.parent != null)
            {
                // Restore the marker's original sort index
                if (CurrentLocation.SiblingSortingIndex >= 0 &&
                    CurrentLocation.SiblingSortingIndex < CurrentLocation.transform.parent.childCount &&
                    CurrentLocation.VpsDataEntry != null)
                {
                    Debug.Log("Restoring " + CurrentLocation.VpsDataEntry.name + " sort index to " + CurrentLocation.SiblingSortingIndex + " out of " + CurrentLocation.transform.parent.childCount);
                    CurrentLocation.transform.SetSiblingIndex(CurrentLocation.SiblingSortingIndex);
                }

                CurrentLocation.SetSelected(false);
                CurrentLocation = null;
            }

            if (forceMapRefresh)
            {
                forceDynamicMarkerRefresh = true;
                OnStreetMapUpdated();
            }
        }

        // Handle marker select.
        public bool MapMarkerLocationSelected(StreetMapMarkerLocation mapMarkerLocation)
        {
            bool togglingOff = mapMarkerLocation == CurrentLocation;

            // DISALLOW TOGGLING-OFF LOCATIONS BY TAPPING THEM
            if (togglingOff) return false;

            // unselect currently selected location, if any
            MapMarkerLocationUnselected();

            if (!togglingOff)
            {
                CurrentLocation = mapMarkerLocation;
                CurrentLocation.SetSelected(true);
            }

            return togglingOff;
        }

        void Update()
        {
            // when in streetmap, periodically check for superbloom notifications citywide
            if (vpsSceneManager.GetStreetMapCameraActive() &&
                Time.time > lastSuperbloomSearchTime + superbloomSearchEverySecs)
            {
                lastSuperbloomSearchTime = Time.time;
                SuperbloomSearch();
            }
        }


        void LateUpdate()
        {
            // keep user avatar marker position in sync with map
            mapMarkerAvatar.LatitudeLongitude = streetMapManager.LocationProvider.CurrentLocation.LatitudeLongitude;
            UpdateMarkerPosition(mapMarkerAvatar);

            // periodically lerp user avatar marker rotation to device rotation
            if (Time.time > lastAvatarRotationUpdateTime + avatarRotationUpdatePeriodSecs)
            {
                lastAvatarRotationUpdateTime = Time.time;
                float lerpDuration = 1f;
                if (lerpDuration < avatarRotationUpdatePeriodSecs)
                {
                    Quaternion startRotation = mapMarkerAvatar.gameObject.transform.localRotation;
                    Quaternion newRotation = mapAvatar.directionIndicator.localRotation;
                    InterpolationUtil.EasedInterpolation(gameObject, gameObject,
                        InterpolationUtil.EaseInOutCubic, lerpDuration,
                        onUpdate: (t) =>
                        {
                            mapMarkerAvatar.gameObject.transform.localRotation = Quaternion.Lerp(startRotation, newRotation, t);
                        }
                    );
                }
            }
        }

    }

}