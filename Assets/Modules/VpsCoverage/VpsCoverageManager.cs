using Niantic.ARDK;
using Niantic.ARDK.Configuration;
using Niantic.ARDK.Configuration.Authentication;
using Niantic.ARDK.VPSCoverage;
using Niantic.ARDK.LocationService;
using Niantic.ARDK.VirtualStudio.VpsCoverage;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
namespace Niantic.ARVoyage.Vps
{

#if UNITY_EDITOR
    /// <summary>
    /// Custom inspector buttons for firing test errors in editor. 
    /// </summary>
    [UnityEditor.CustomEditor(typeof(VpsCoverageManager))]
    public class VpsCoverageManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                VpsCoverageManager vpsCoverageManager = target as VpsCoverageManager;

                if (GUILayout.Button("Trigger Coverage Area Error"))
                {
                    vpsCoverageManager.TriggerCoverageAreasError("0");
                }

                if (GUILayout.Button("Trigger Localization Target Error"))
                {
                    vpsCoverageManager.TriggerLocalizationTargetsError("0");
                }
            }
        }
    }
#endif

    /// <summary>
    /// Manages queries to the Coverage API and caches the responses as VpsDataEntries.
    /// Offers other search/location-based functionality and data as well.
    /// </summary>
    public class VpsCoverageManager : MonoBehaviour, ISceneDependency
    {
        public const int SearchRadiusInMeters = 500;
        public const int Timeout = 5000;
        public const int MaximumLocalizationTargetsPerQuery = 100;

        private VpsSceneManager vpsSceneManager;
        private ICoverageClient coverageClient;

        [Header("Coverage Api")]
        [SerializeField] RuntimeEnvironment coverageClientRuntime = RuntimeEnvironment.Default;
        [SerializeField] VpsCoverageResponses mockResponses;

        [Header("Vps Data Entry Assets")]
        [SerializeField] List<VpsContentSO> vpsContentAssets = new List<VpsContentSO>();
        [SerializeField] VpsContentSO defaultVpsContentAsset;

        // Location "presets" for VPS-populated areas.
        public class TeleportLocation
        {
            public static readonly LatLng Embarcadero = new LatLng(37.79536, -122.3923);
            public static readonly LatLng LosAngeles = new LatLng(34.049072, -118.241622);     // Sontuko Ninomiya, Los Angeles
            public static readonly LatLng London = new LatLng(51.514545, -0.124689);           // The cross keys, London
            public static readonly LatLng NewYork = new LatLng(40.739745, -73.985059);         // Gramercy Theater, New York
            public static readonly LatLng SanFrancisco = new LatLng(37.766264, -122.453161);   // Foggy Sunset Skyline, San Francisco
            public static readonly LatLng Seattle = new LatLng(47.627642, -122.342193);        // Neptune’s compass, Seattle
            public static readonly LatLng Tokyo = new LatLng(35.714478, 139.796513);

            public static readonly List<LatLng> All = new List<LatLng>() {
                Embarcadero, LosAngeles, London, NewYork, SanFrancisco, Seattle, Tokyo
            };
        }

        // Query state.
        private bool coverageQueryInProgress = false;
        public bool CoverageQueryInProgress
        {
            get
            {
                return coverageQueryInProgress;
            }
            private set
            {
                if (coverageQueryInProgress != value)
                {
                    Debug.Log("CoverageQueryStateChange:" + value);
                    CoverageQueryStateChange?.Invoke(value);
                }
                coverageQueryInProgress = value;
            }
        }

        // Coverage related events.
        public AppEvent<bool> CoverageQueryStateChange;
        public AppEvent<string> CoverageQueryError;

        // Lookups
        private Dictionary<string, VpsDataEntry> vpsDataEntryLookup = new Dictionary<string, VpsDataEntry>();
        private List<VpsDataEntry> injectableDataEntries = new List<VpsDataEntry>();
        public List<VpsDataEntry> BespokeEnabledDataEntries { get; private set; } = new List<VpsDataEntry>();

        // List of identifiers to request and cache.
        private HashSet<string> localizationTargetQueue = new HashSet<string>();
        private bool waitingForLocalizationTargetResponse = false;

        private void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();

            coverageClient = CoverageClientFactory.Create(coverageClientRuntime, mockResponses);

            // Populate data entry lookup with content assets.
            foreach (VpsContentSO contentAsset in vpsContentAssets)
            {
                // Instantiate a copy of the SO so we don't ever alter the original data.
                VpsContentSO contentAssetInstance = Object.Instantiate(contentAsset);
                VpsDataEntry vpsDataEntry = contentAssetInstance.vpsDataEntry;

                // Fallback default localization content.
                if (vpsDataEntry.bespokeEnabled != true)
                {
                    vpsDataEntry.prefab = defaultVpsContentAsset.vpsDataEntry.prefab;
                }

                // If we have a null identifier, make a fake one.
                if (string.IsNullOrEmpty(vpsDataEntry.identifier))
                {
                    vpsDataEntry.identifier = "mock-" + System.Guid.NewGuid();
                }

                // Add to lookups.
                vpsDataEntryLookup.Add(vpsDataEntry.identifier, vpsDataEntry);
                if (vpsDataEntry.injectIntoCoverageResults) injectableDataEntries.Add(vpsDataEntry);
                if (vpsDataEntry.bespokeEnabled) BespokeEnabledDataEntries.Add(vpsDataEntry);

                // Schedule an initial query from the Coverage API so custom locations are always availble.
                localizationTargetQueue.Add(vpsDataEntry.identifier);
            }

#if UNITY_EDITOR
            // Allows the use of the live coverage API in editor.
            if (coverageClientRuntime == RuntimeEnvironment.LiveDevice)
            {
                var authConfig = Resources.Load<ArdkAuthConfig>("ARDK/ArdkAuthConfig");
                ArdkGlobalConfig.SetApiKey(authConfig.ApiKey);
                ArdkGlobalConfig.SetUserIdOnLogin(SaveUtil.UserId);
            }
#endif

        }

        void Start()
        {
            // Schedule an initial query from the Coverage API so visited FF locations are always availble.
            // This allows us to return to Superbloom locations even if they haven't been loaded yet.
            foreach (string identifier in vpsSceneManager.PersistentFrostFlowerStateLookup.Keys)
            {
                localizationTargetQueue.Add(identifier);
            }

            // Kick off routine that periodically requests queued location target identifiers.
            StartCoroutine(RequestLocalizationTargetsRoutine());
        }

        // Monitors a queue of requested targets and makes API requests as needed.
        private IEnumerator RequestLocalizationTargetsRoutine()
        {
            WaitForSeconds waitForFiveSeconds = new WaitForSeconds(5);

            while (true)
            {
                string[] identifiers = System.Linq.Enumerable.ToArray(localizationTargetQueue);

                if (identifiers.Length > 0 && !waitingForLocalizationTargetResponse)
                {
                    Debug.Log($"Requesting localization target data for {identifiers.Length} identifiers.");
                    waitingForLocalizationTargetResponse = true;

                    coverageClient.RequestLocalizationTargets(identifiers, (localizationTargetsResult) =>
                    {
                        waitingForLocalizationTargetResponse = false;

                        if (localizationTargetsResult.Status != ResponseStatus.Success)
                        {
                            Debug.LogWarning("Localization target query failed: " + localizationTargetsResult.Status.ToString());
                            return;
                        }

                        foreach (LocalizationTarget target in localizationTargetsResult.ActivationTargets.Values)
                        {
                            Debug.Log($"Coverage Api Response: Queued Identifier: {target.Identifier} Name: {target.Name}");

                            // "Getting" a data entry implicitly creates/caches it.
                            GetOrCreateVpsDataEntryForLocalizationTarget(target);

                            // Remove from queue.
                            localizationTargetQueue.Remove(target.Identifier);
                        }
                    });
                }

                yield return waitForFiveSeconds;
            }
        }

        // Handles errors related to "RequestCoverageAreasAsync".
        public void TriggerCoverageAreasError(string status)
        {
            CoverageQueryInProgress = false;
            CoverageQueryError?.Invoke($"Coverage query failed. Please try again. ({status})");
        }

        // Handles errors related to "RequestLocalizationTargetsAsync".
        public void TriggerLocalizationTargetsError(string status)
        {
            CoverageQueryInProgress = false;
            CoverageQueryError?.Invoke($"Localization target query failed. Please try again. ({status})");
        }

        // Called when a search returns no results. Allows the user to "teleport"
        // to a location with known wayspots.
        public LatLng? GetNearestTeleportLocation(LatLng coordinates)
        {
            double minimumDistance = Mathf.Infinity;
            LatLng? nearestTeleportLocation = null;

            foreach (LatLng teleportLocation in TeleportLocation.All)
            {
                double distance = coordinates.Distance(teleportLocation);
                if (distance < minimumDistance)
                {
                    minimumDistance = distance;
                    nearestTeleportLocation = teleportLocation;
                }
            }

            return nearestTeleportLocation;
        }

        // The main search routine for populating the map.
        public async void GetCoverageAtCoordinates(LatLng coordinates, System.Action<List<VpsDataEntry>> callback)
        {
            if (CoverageQueryInProgress == true)
            {
                Debug.LogWarning("Query already in progress.");
                return;
            }

            Debug.Log($"Coverage API Search: {coordinates.ToString()}");
            CoverageQueryInProgress = true;

            // Request areas/localization  targets from Coverage API.
            Task<CoverageAreasResult> requestCoverageAreasTask = coverageClient.RequestCoverageAreasAsync(coordinates, 500);
            Task requestCoverageAreasResult = await Task.WhenAny(requestCoverageAreasTask, Task.Delay(Timeout));

            // Check whether we timed out.
            if (requestCoverageAreasResult != requestCoverageAreasTask)
            {
                Debug.Log("Coverage API query failed: Timeout");
                TriggerCoverageAreasError("Timeout");
                return;
            }

            // Process query result.
            CoverageAreasResult coverageAreasResult = await (Task<CoverageAreasResult>)requestCoverageAreasTask;

            if (coverageAreasResult.Status != ResponseStatus.Success)
            {
                Debug.LogWarning("Coverage API query failed: " + coverageAreasResult.Status.ToString());
                TriggerCoverageAreasError(coverageAreasResult.Status.ToString());
                return;
            }

            Debug.Log($"Coverage API Response: Status: {coverageAreasResult.Status.ToString()} Areas: {coverageAreasResult.Areas.Length} .");

            // Prepare a container for the returned results.
            List<VpsDataEntry> vpsDataEntries = new List<VpsDataEntry>();

            // Compile a list of all location target identifiers across areas.
            List<string> localizationTargetIdentifiers = new List<string>();
            foreach (CoverageArea area in coverageAreasResult.Areas)
            {
                Debug.Log($"Coverage API Response: Area: {area.Centroid} Location Target Identifiers: {area.LocalizationTargetIdentifiers.Length}");
                foreach (string localizationTargetIdentifier in area.LocalizationTargetIdentifiers)
                {
                    // Don't query for locations we already know about.
                    if (vpsDataEntryLookup.ContainsKey(localizationTargetIdentifier))
                    {
                        vpsDataEntries.Add(vpsDataEntryLookup[localizationTargetIdentifier]);
                    }
                    else
                    {
                        if (!localizationTargetIdentifiers.Contains(localizationTargetIdentifier)) localizationTargetIdentifiers.Add(localizationTargetIdentifier);
                    }
                }
            }

            // Log identifier data before search.
            Debug.Log($"Coverage API Response Total Localization Target Identifiers: {localizationTargetIdentifiers.Count}");

            // Build an array for the localization target query with a max count.
            int count = Mathf.Min(MaximumLocalizationTargetsPerQuery, localizationTargetIdentifiers.Count);
            string[] localizationTargetIdentifierArray = localizationTargetIdentifiers.GetRange(0, count).ToArray();

            // Request localization targets from coverage api.
            Task<LocalizationTargetsResult> requestLocalizationTargetsTask = coverageClient.RequestLocalizationTargetsAsync(localizationTargetIdentifierArray);
            Task requestLocalizationTargetsResult = await Task.WhenAny(requestLocalizationTargetsTask, Task.Delay(Timeout));

            // Check whether we timed out.
            if (requestLocalizationTargetsResult != requestLocalizationTargetsTask)
            {
                Debug.Log("Coverage API query failed: Timeout");
                TriggerLocalizationTargetsError("Timeout");
                return;
            }

            // Process query result.
            LocalizationTargetsResult localizationTargetsResult = await (Task<LocalizationTargetsResult>)requestLocalizationTargetsResult;

            if (localizationTargetsResult.Status != ResponseStatus.Success)
            {
                Debug.LogWarning("Coverage API query failed: " + localizationTargetsResult.Status.ToString());
                TriggerLocalizationTargetsError(localizationTargetsResult.Status.ToString());
                return;
            }

            // Get/create VpsDataEntries for the resulting location data.
            foreach (LocalizationTarget target in localizationTargetsResult.ActivationTargets.Values)
            {
                Debug.Log($"Coverage Api Response: Localization Target Identifier: {target.Identifier} Name: {target.Name} Coordinates: {target.Center}");

                VpsDataEntry vpsDataEntry = GetOrCreateVpsDataEntryForLocalizationTarget(target);
                vpsDataEntries.Add(vpsDataEntry);
            }

            // Inject dev localization targets if they're within the search radius.
            foreach (VpsDataEntry vpsDataEntry in injectableDataEntries)
            {
                Debug.Log($"Coverage Api Response: Injected VpsDataEntry Identifier: {vpsDataEntry.identifier} Name: {vpsDataEntry.name}");
                if (vpsDataEntry.latitudeLongitude.Distance(coordinates) <= SearchRadiusInMeters)
                {
                    vpsDataEntries.Add(vpsDataEntry);
                }
            }

            callback?.Invoke(vpsDataEntries);

            CoverageQueryInProgress = false;
        }

        // Check the cache for existing data entries for a given localization target identifier.
        public VpsDataEntry GetVpsDataEntryByIdentifier(string identifier)
        {
            VpsDataEntry vpsDataEntry = null;
            vpsDataEntryLookup.TryGetValue(identifier, out vpsDataEntry);
            return vpsDataEntry;
        }

        // Creates, caches and returns a VpsDataEntry for unknown localization targets.
        // Finds, updates and returns an existing VpsDataEntry when available.
        private VpsDataEntry GetOrCreateVpsDataEntryForLocalizationTarget(LocalizationTarget target)
        {
            VpsDataEntry vpsDataEntry = null;

            // Look for existing entry, create if not found.
            vpsDataEntryLookup.TryGetValue(target.Identifier, out vpsDataEntry);

            if (vpsDataEntry == null)
            {
                // Add entry to lookup when one doesn't exist.
                vpsDataEntry = new VpsDataEntry();

                // Copy target data to new entry.
                vpsDataEntry.identifier = target.Identifier;
                vpsDataEntry.name = target.Name;
                vpsDataEntry.latitudeLongitude = target.Center;
                vpsDataEntry.imageUrl = target.ImageURL;

                // Fallback default localization content.
                vpsDataEntry.prefab = defaultVpsContentAsset.vpsDataEntry.prefab;

                // Add to lookup.
                vpsDataEntryLookup.Add(vpsDataEntry.identifier, vpsDataEntry);
            }
            else
            {
                // Refresh some data on existing entries.
                vpsDataEntry.latitudeLongitude = target.Center;
            }

            return vpsDataEntry;
        }
    }
}
