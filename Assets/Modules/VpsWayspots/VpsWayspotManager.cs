using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.WayspotAnchors;
using Niantic.ARDK.LocationService;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Wraps the ARDK WayspotAnchorService and handles tasks related to localization and placing/resolving anchors.
    /// Broadcasts events when localization state updates so that app states can manage their content accordingly
    /// </summary>
    public class VpsWayspotManager : MonoBehaviour, ISceneDependency
    {
        // Broadcast when the WayspotAnchorService's LocalizationState updates. Sends the LocalizationFailureReason, which is only applicable 
        // if the LocalizationState == LocalizationState.Failed
        public static AppEvent<LocalizationState, LocalizationFailureReason> LocalizationStateUpdated = new AppEvent<LocalizationState, LocalizationFailureReason>();

        // Broadcast when the WayspotAnchorService's LocalizationState changes from Localized to a non-localized state
        // When this happens, the WayspotAnchorService is automatically stopped and all anchored content is destroyed
        // App states listen for this to transition to the destabilized state to keep players informed
        public static readonly AppEvent LocalizationDestabilized = new AppEvent();

        private LocalizationState localizationState = LocalizationState.Uninitialized;

        /// <summary>
        /// The current localization state
        /// Manages sending the LocalizationStateUpdated event
        /// </summary>
        public LocalizationState LocalizationState
        {
            get => localizationState;
            set
            {
                if (localizationState != value)
                {
                    LocalizationState previousLocalizationState = localizationState;
                    localizationState = value;
                    Debug.Log($"Localization State Change: {localizationState}");
                    LocalizationStateUpdated.Invoke(localizationState, WayspotAnchorService != null ? WayspotAnchorService.LocalizationFailureReason : LocalizationFailureReason.None);

                    // If the localization state changes away from localized, broadcast this event
                    // If the WayspotAnchorService is running, stop it, which will destroy all anchored content
                    if (previousLocalizationState == LocalizationState.Localized)
                    {
                        StopWayspotAnchorService();
                        LocalizationDestabilized.Invoke();
                    }
                }
            }
        }

        // The ARDK WayspotAnchorService wrapped by this manager
        public WayspotAnchorService WayspotAnchorService { get; private set; }

        private VpsSceneManager vpsSceneManager;
        private IARSession arSession;
        private ILocationService locationService;
        private IWayspotAnchorsConfiguration wayspotAnchorsConfiguration;
        private bool waitingForARSessionToStartWayspotService;

        // Stores the current anchor transforms by their Guids
        private readonly Dictionary<Guid, Transform> AnchorTransformsById = new Dictionary<Guid, Transform>();
        // Stores the transforms that are waiting for their anchors to be created so they can be childed to the anchors
        private readonly HashSet<Transform> PendingAnchorChildTransforms = new HashSet<Transform>();
        // Stores the serialized Wayspot Anchor Payloads per localization target identifier
        private readonly PersistentDataDictionary<string, List<string>> PersistentWayspotAnchorPayloadLookup = new PersistentDataDictionary<string, List<string>>(PersistentDataUtility.WayspotAnchorPayloadFilename);

        // A serialized text file of wayspot anchor payloads that are initially baked into the application
        [SerializeField] TextAsset initialWayspotAnchorPayloads;

        private Coroutine anchorStatusMonitorRoutine;

        private void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            wayspotAnchorsConfiguration = WayspotAnchorsConfigurationFactory.Create();
            ARSessionFactory.SessionInitialized += OnSessionInitialized;

            // Load the saved wayspot anchor payloads
            PersistentWayspotAnchorPayloadLookup.Load();

            // Append initial Json payloads to Persistent Dictionary.
            if (initialWayspotAnchorPayloads != null)
            {
                Debug.Log("Appending initial wayspot anchor payloads.");
                PersistentWayspotAnchorPayloadLookup.AppendFromTextAsset(initialWayspotAnchorPayloads);
                PersistentWayspotAnchorPayloadLookup.Save();
            }
        }

        private void OnDestroy()
        {
            ARSessionFactory.SessionInitialized -= OnSessionInitialized;

            if (WayspotAnchorService != null)
            {
                StopWayspotAnchorService();
            }

            if (arSession != null)
            {
                arSession.Paused -= OnSessionPause;
            }
        }

        // Called one-time per VPS scene execution, when the ARSession is initialized
        // The ARSession may run and pause multiple times while the user is running the VPS scene
        private void OnSessionInitialized(AnyARSessionInitializedArgs anyARSessionInitializedArgs)
        {
            Debug.Log("Session Initialized.");
            arSession = anyARSessionInitializedArgs.Session;
            arSession.Paused += OnSessionPause;

            locationService = LocationServiceFactory.Create(arSession.RuntimeEnvironment);
            locationService.Start(.01f, .01f);

            // If we attempted to start the WayspotAnchorService before the ARSession was
            // initialized, this flag will be true, and we star the service immediately
            if (waitingForARSessionToStartWayspotService)
            {
                waitingForARSessionToStartWayspotService = false;
                StartWayspotAnchorService();
            }
        }

        /// <summary>
        /// Start the WayspotAnchorService, which will immediatly try to localize
        /// We monitor its LocalizationState in Update()
        /// </summary>
        public void StartWayspotAnchorService()
        {
            // If we don't yet have the ARSession, set the flag to start the wayspot anchor service
            // once it's initialized and return
            if (arSession == null)
            {
                Debug.Log("Waiting for AR Session to start wayspot anchor service");
                waitingForARSessionToStartWayspotService = true;
                return;
            }

            Debug.Log($"{nameof(StartWayspotAnchorService)}");

            // First, stop any old running wayspot anchor service
            // This is needed if we're handling a request to relocalize via the debug menu
            if (WayspotAnchorService != null)
            {
                StopWayspotAnchorService();
            }

            // Create the WayspotAnchorService, which immediately begins the localization process
            // We monitor for LocalizationState changes in Update()
            WayspotAnchorService = new WayspotAnchorService(arSession, locationService, wayspotAnchorsConfiguration);

            // Reset localization state.
            LocalizationState = WayspotAnchorService.LocalizationState;

            // Monitor for changes in the WayspotAnchorService's LocalizationState
            WayspotAnchorService.LocalizationStateUpdated += (args) => LocalizationState = args.State;

			// State the persistent monitoring of anchor status
			anchorStatusMonitorRoutine = StartCoroutine(AnchorStatusMonitorRoutine());
        }

        /// <summary>
        /// Start the WayspotAnchorService if it's running
        /// As part of this, we clear all anchors and destroy all anchored content,
        /// including pending anchored content
        /// </summary>
        public void StopWayspotAnchorService()
        {
            if (WayspotAnchorService != null)
            {
                Debug.Log($"{nameof(StopWayspotAnchorService)}");

                // First, clear any current anchors
                ClearAnchors();

                // Then, dispose of the WayspotAnchorService
                WayspotAnchorService.Dispose();
                WayspotAnchorService = null;
                LocalizationState = LocalizationState.Uninitialized;
            }

            waitingForARSessionToStartWayspotService = false;

            // Also stop the anchor status monitor routine if needed
            if (anchorStatusMonitorRoutine != null)
            {
                StopCoroutine(anchorStatusMonitorRoutine);
                anchorStatusMonitorRoutine = null;
            }
        }

        /// <summary>
        /// Get a list of the currently loaded anchor transforms
        /// </summary>
        public List<Transform> GetCurrentAnchorTransforms()
        {
            return new List<Transform>(AnchorTransformsById.Values);
        }

        /// <summary>
        /// Save the currently loaded anchors for the current localization target
        /// </summary>
        public void SaveCurrentAnchorPayloads()
        {
            if (WayspotAnchorService != null)
            {
                // Get the anchors currently loaded in the service
                IWayspotAnchor[] anchors = WayspotAnchorService.GetAllWayspotAnchors();

                if (anchors.Length > 0)
                {
                    // Serialize the anchor payloads and save them in the persistent lookup
                    List<string> serializedAnchorPayloads = new List<string>();
                    for (int i = 0; i < anchors.Length; i++)
                    {
                        Debug.Log("Serializing anchor payload: " + i);
                        string serializedPayload = anchors[i].Payload.Serialize();
                        serializedAnchorPayloads.Add(serializedPayload);
                    }
                    PersistentWayspotAnchorPayloadLookup.SetAndSave(vpsSceneManager.CurrentVpsDataEntry.identifier, serializedAnchorPayloads);
                    Debug.Log($"{nameof(SaveCurrentAnchorPayloads)} saved {anchors.Length} anchor payloads for {vpsSceneManager.CurrentVpsDataEntry.identifier}");
                }
                else
                {
                    Debug.LogWarning($"Ignoring {nameof(SaveCurrentAnchorPayloads)} because there are no anchors.");
                }
            }
            else
            {
                Debug.LogWarning($"Ignoring {nameof(SaveCurrentAnchorPayloads)} because {nameof(WayspotAnchorService)} is null");
            }
        }

        /// <summary>
        /// Does the persistent wayspot anchor payload lookup have any payloads for the current localization target?
        /// </summary>
        public bool HasAnchorPayloadsForCurrentLocalizationTarget()
        {
            if (PersistentWayspotAnchorPayloadLookup.TryGetValue(
                vpsSceneManager.CurrentVpsDataEntry.identifier,
                out List<string> wayspotAnchorPayloadStrings))
            {
                return wayspotAnchorPayloadStrings != null && wayspotAnchorPayloadStrings.Count > 0;
            }
            return false;
        }

        /// <summary>
        /// Restore any WayspotAnchors for the current localization target
        /// Invokes the callbacks per anchor that starts tracking and when all have started tracking
        /// </summary>
        public void RestoreWayspotAnchorsForCurrentLocalizationTarget(Action<Transform> onAnchorStartedTracking, Action onAllAnchorsStartedTracking)
        {
            if (LocalizationState != LocalizationState.Localized)
            {
                Debug.LogError("You must be localized to load anchors.");
                return;
            }

            // Get the saved, serialized payload strings for the current localization target
            PersistentWayspotAnchorPayloadLookup.TryGetValue(
                vpsSceneManager.CurrentVpsDataEntry.identifier,
                out List<string> serializedAnchorPayloads);

            if (serializedAnchorPayloads == null || serializedAnchorPayloads.Count == 0)
            {
                Debug.LogWarning($"{nameof(RestoreWayspotAnchorsForCurrentLocalizationTarget)} found no anchors to load for {vpsSceneManager.CurrentVpsDataEntry.identifier}");
                return;
            }

            // Deserialize the payload strings into payloads
            WayspotAnchorPayload[] wayspotAnchorPayloadsForLocalizationTargetId = new WayspotAnchorPayload[serializedAnchorPayloads.Count];
            for (int i = 0; i < serializedAnchorPayloads.Count; i++)
            {
                wayspotAnchorPayloadsForLocalizationTargetId[i] = WayspotAnchorPayload.Deserialize(serializedAnchorPayloads[i]);
            }

            // Restore the anchors
            IWayspotAnchor[] wayspotAnchors = WayspotAnchorService.RestoreWayspotAnchors(wayspotAnchorPayloadsForLocalizationTargetId);
            // Create the GameObjects for the anchors and wait for them to begin tracking
            CreateAnchorGameObjects(wayspotAnchors);
            HashSet<IWayspotAnchor> anchorsPendingTracking = new HashSet<IWayspotAnchor>(wayspotAnchors);

            // Listen for each anchor to begin tracking so we can invoke the callbacks
            foreach (IWayspotAnchor wayspotAnchor in wayspotAnchors)
            {
                wayspotAnchor.StatusCodeUpdated += OnAnchorStatusUpdated;

                void OnAnchorStatusUpdated(WayspotAnchorStatusUpdate statusUpdate)
                {
                    if (statusUpdate.Code != WayspotAnchorStatusCode.Success)
                        return;

                    onAnchorStartedTracking?.Invoke(AnchorTransformsById[wayspotAnchor.ID]);
                    anchorsPendingTracking.Remove(wayspotAnchor);
                    wayspotAnchor.StatusCodeUpdated -= OnAnchorStatusUpdated;

                    // If all pending anchors are done loading, invoke the callback
                    if (anchorsPendingTracking.Count == 0)
                    {
                        onAllAnchorsStartedTracking?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Places a WayspotAnchor at the position of the anchorChildTransform
        /// Once the anchor is ready, anchorChildTransform will be childed to the anchor so it receives positional updates
        /// This method accepts an optional callback that is invoked when the anchor starts tracking
        /// </summary>
        public void PlaceAnchor(Transform anchorChildTransform, Action onAnchorStartedTracking = null)
        {
            if (LocalizationState != LocalizationState.Localized)
            {
                Debug.LogError("You must be localized to place anchors.");
                return;
            }

            Matrix4x4 localPose = Matrix4x4.TRS(anchorChildTransform.position, anchorChildTransform.rotation, Vector3.one);
            IWayspotAnchor createdAnchor = null;

            // Track this pending anchor child transform so it can be destroyed if clearing anchors before they've begun tracking
            PendingAnchorChildTransforms.Add(anchorChildTransform);

            // Create the anchor, and handle the creation in the callback
            IWayspotAnchor[] wayspotAnchors = WayspotAnchorService.CreateWayspotAnchors(localPose);
            CreateAnchorGameObjects(wayspotAnchors);
            createdAnchor = wayspotAnchors[0];
            createdAnchor.StatusCodeUpdated += OnStatusCodeUpdated_AttachAnchorChildObject;
            

            void OnStatusCodeUpdated_AttachAnchorChildObject(WayspotAnchorStatusUpdate wayspotAnchorStatusUpdate)
            {
                if (wayspotAnchorStatusUpdate.Code != WayspotAnchorStatusCode.Success)
                    return;

                // Remove the child transform from the pending set
                PendingAnchorChildTransforms.Remove(anchorChildTransform);

                // Attach the child object to the anchor GameObject with no offset
                Transform anchorTransform = AnchorTransformsById[wayspotAnchorStatusUpdate.ID];
                anchorChildTransform.SetParent(anchorTransform);
                anchorChildTransform.localPosition = Vector3.zero;
                anchorChildTransform.localRotation = Quaternion.identity;

                onAnchorStartedTracking?.Invoke();

                // Unsubscribe from future updates
                createdAnchor.StatusCodeUpdated -= OnStatusCodeUpdated_AttachAnchorChildObject;
            }
        }

        // A persistent coroutine that monitors the status of all current anchors when the WayspotAnchorService is running
        // If an anchor ever fails, the routine stops the WayspotAnchorService
        // If we were localized, this will trigger a LocalizationDestabilized event broadcast
        private IEnumerator AnchorStatusMonitorRoutine()
        {
            while (WayspotAnchorService != null)
            {
                foreach (Guid anchorGuid in AnchorTransformsById.Keys)
                {
                    WayspotAnchorStatusCode wayspotAnchorStatusCode = WayspotAnchorService.GetWayspotAnchor(anchorGuid).Status;

                    if (testTriggerAnchorFailure)
                    {
                        Debug.Log("Triggering test anchor failure");
                        testTriggerAnchorFailure = false;
                        wayspotAnchorStatusCode = WayspotAnchorStatusCode.Failed;
                    }

                    if (wayspotAnchorStatusCode == WayspotAnchorStatusCode.Failed)
                    {
                        Debug.Log($"Anchor status for {anchorGuid} is {wayspotAnchorStatusCode}. Calling {nameof(StopWayspotAnchorService)}.");
                        // If an anchor fails, stop the wayspot anchor service, which will destabilize localization
                        StopWayspotAnchorService();
                        yield break;
                    }
                }
                yield return null;
            }
        }

        // Called when the WayspotAnchorService creates an anchor so we can make a GameObject for it
        // that'll track its pose
        private void CreateAnchorGameObjects(IWayspotAnchor[] wayspotAnchors)
        {
            foreach (var wayspotAnchor in wayspotAnchors)
            {
                Guid id = wayspotAnchor.ID;
                Transform anchorTransform = new GameObject($"Anchor {id}").transform;
                AnchorTransformsById.Add(id, anchorTransform);

                // When the anchor's tracking state updates, apply its pose to the anchor transform
                wayspotAnchor.TransformUpdated += wayspotAnchorResolvedArgs =>
                {
                    if (anchorTransform != null)
                    {
                        anchorTransform.position = wayspotAnchorResolvedArgs.Position;
                        anchorTransform.rotation = wayspotAnchorResolvedArgs.Rotation;
                    }
                };
            }
        }

        /// <summary>
        /// Destroys current anchors and any pending anchored content
        /// </summary>
        public void ClearAnchors()
        {
            // Destroy all anchor GameObjects
            foreach (var anchor in AnchorTransformsById)
            {
                if (anchor.Value != null)
                {
                    Destroy(anchor.Value.gameObject);
                }
            }

            // Destroy any pending anchor child transforms
            foreach (Transform anchorChildTransform in PendingAnchorChildTransforms)
            {
                if (anchorChildTransform != null)
                {
                    Destroy(anchorChildTransform.gameObject);
                }
            }

            // Inform the WayspotAnchorServie to destroy the anchors by ID
            if (WayspotAnchorService != null)
            {
                Guid[] anchorIds = new Guid[AnchorTransformsById.Count];
                int i = 0;
                foreach (Guid anchorGuid in AnchorTransformsById.Keys)
                {
                    anchorIds[i] = anchorGuid;
                    i++;
                }
                try
                {
                    WayspotAnchorService.DestroyWayspotAnchors(anchorIds);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Caught exception in {nameof(WayspotAnchorService.DestroyWayspotAnchors)}. {e.Message} {e.StackTrace}");
                }
            }

            // Clear the collections
            AnchorTransformsById.Clear();
            PendingAnchorChildTransforms.Clear();
        }

        // Stops the WayspotAnchorService when the AR Session becomes paused
        private void OnSessionPause(ARSessionPausedArgs arSessionPausedArgs)
        {
            Debug.Log("Session Paused.");
            StopWayspotAnchorService();
        }

        // Logic for testing anchor failure via the inspector
        bool testTriggerAnchorFailure = false;
#if UNITY_EDITOR
        internal void TestTriggerAnchorFailure()
        {
            testTriggerAnchorFailure = true;
        }
#endif
    }

#if UNITY_EDITOR
    // Logic for testing anchor failure via the inspector
    [UnityEditor.CustomEditor(typeof(VpsWayspotManager))]
    public class VpsWayspotManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                VpsWayspotManager vpsWayspotManager = target as VpsWayspotManager;

                if (GUILayout.Button("Trigger anchor failure."))
                {
                    vpsWayspotManager.TestTriggerAnchorFailure();
                }
            }
        }
    }
#endif
}