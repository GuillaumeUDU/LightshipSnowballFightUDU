// Copyright 2022 Niantic, Inc. All Rights Reserved.

using Mapbox.Utils;

using Niantic.ARDK.Configuration;
using Niantic.ARDK.Extensions;
using Niantic.ARDK.LocationService;
using Niantic.ARDK.Utilities;

using Niantic.ARVoyage.Vps;

using System.Collections;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using LocationServiceStatus = Niantic.ARDK.LocationService.LocationServiceStatus;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Niantic.ARVoyage.Loading
{
    /// <summary>
    /// State in Loading that displays a message and handles various initial load logic. The state runs the following:
    /// 1. Checks for device AR capability. If the device doesn't have AR capability, the player is not able to proceed.
    /// 2. Manages preloading the ARDK feature models used in this project. If the player is not able to download the features, they are not able to proceed.
    /// 4. Manages requesting permissions. Camera permission is required to proceed.
    /// 5. Sends user to the Homeland portion of the experience if camera permission was granted.S

    /// The loading % UI is used to communicate load process:
    /// 0-97%:   ARDK feature download
    /// 98%:     Location permission
    /// 99%:     Native gallery permission
    /// 100%:     Camera permission
    /// </summary>
    public class StateLoading : StateBase
    {
        private const float RetryDownloadMessageDisplayTime = 2f;
        private const float TimeBetweenDownloadProgressChangeUntilTimeout = 5f;

        private const string LoadingText = "Loading... ";
        private const string LoadingDoneText = "Let's Go!";
        private const string ErrorTextCapabilityCheck = "Your device is not supported!";
        private const string ErrorTextConnectionRequired = "Internet connection needed!";
        private const string ErrorTextReconnecting = "Trying to reconnect...";

        // Inspector reference to the scene's CapabilityChecker to confirm the device is capable of AR
        [SerializeField] private CapabilityChecker capabilityChecker;

        // Inspector references to relevant objects
        [Header("State Machine")]
        [SerializeField] private bool isStartState = true;
        [SerializeField] private GameObject nextState;
        [SerializeField] private StateBase[] precedingStates = new StateBase[] { };

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private Button guiButton;

        // Inspector references and variables for managing ARDK feature download
        [SerializeField] private FeaturePreloadManager featurePreloadManager;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private GameObject mainBodyText;
        [SerializeField] private GameObject cameraPermissionBodyText;

        private bool succeededFeatureDataLoad;
        private bool capabilityCheckSucceeded;

        // This variable is used in Android only, so we disable the warning that'll be presented in iOS
#pragma warning disable 0414
        private bool waitingForApplicationToRegainFocus = false;
#pragma warning restore 0414

        private GameObject exitState;

        // Every state has a running bool that's true from OnEnable to Exit
        private bool running;

        private ErrorManager errorManager;
        private LevelSwitcher levelSwitcher;
        private AudioManager audioManager;

        // Fade variables
        private Fader fader;
        private float initialDelay = 0.75f;

        void Awake()
        {
            gameObject.SetActive(isStartState);

            errorManager = SceneLookup.Get<ErrorManager>();
            levelSwitcher = SceneLookup.Get<LevelSwitcher>();
            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();

            if (!Debug.isDebugBuild)
            {
                // Disable Unity debug logging outside of debug
                Debug.unityLogger.logEnabled = false;
            }
        }

        void OnEnable()
        {
            // State was not skipped
            Skipped = false;

            // Set button non-interactable (but active/visible) until launch downloads are complete
            guiButton.interactable = false;
            buttonText.fontSize = 22;

            // Only use the initialDelay if every state preceding this was skipped.
            var firstState = true;
            foreach (var state in precedingStates)
                firstState = firstState && state.Skipped;

            var delay = firstState ? initialDelay : 0.0f;

            // Fade in GUI
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, initialDelay: delay,
                onComplete: () =>
                {
                    // Confirm this device is capable of running ARDK
                    if (capabilityChecker.HasSucceeded)
                    {
                        OnCapabilityCheckerSuccess();
                    }
                    else
                    {
                        capabilityChecker.Success.AddListener(OnCapabilityCheckerSuccess);
                        capabilityChecker.Failure.AddListener(OnCapabilityCheckerFailure);
                    }
                }
            ));

            running = true;
        }

        void OnDisable()
        {
            // Unsubscribe from events
            guiButton.onClick.RemoveListener(OnEventGUIButtonClicked);
            capabilityChecker.Failure.RemoveListener(OnCapabilityCheckerFailure);
            capabilityChecker.Success.RemoveListener(OnCapabilityCheckerSuccess);
        }

        private void OnCapabilityCheckerSuccess()
        {
            capabilityChecker.Failure.RemoveListener(OnCapabilityCheckerFailure);
            capabilityChecker.Success.RemoveListener(OnCapabilityCheckerSuccess);

            // Once the capability check succeeds, run the main routine
            StartCoroutine(MainRoutine());
        }

        private void OnCapabilityCheckerFailure(CapabilityChecker.FailureReason failureReason)
        {
            capabilityChecker.Failure.RemoveListener(OnCapabilityCheckerFailure);
            capabilityChecker.Success.RemoveListener(OnCapabilityCheckerSuccess);

            // Permanently display the error banner
            errorManager.DisplayErrorBanner(ErrorTextCapabilityCheck, autoHideErrorBanner: false);

            // Set running false and disable the start button to hang in this state
            guiButton.interactable = false;
            running = false;
        }

        private IEnumerator MainRoutine()
        {
            // Set ARDK user id.
            Debug.Log($"Setting ARDK UserId: {SaveUtil.UserId}");
            ArdkGlobalConfig.SetUserIdOnLogin(SaveUtil.UserId);

            // First, load all necessary data
            // Advances load progress from 0 - 97%
            SetLoadingProgressText(0f);
            yield return LoadFeatureDataRoutine();

            // Then, manage getting user location permission
            // This is handled differently per platform
#if UNITY_IOS
            yield return IosLocationPermissionRoutine();
#endif
#if UNITY_ANDROID
            yield return AndroidLocationPermissionRoutine();
#endif

            // Communicate load progress to user
            SetLoadingProgressText(.98f);

            // Then, manage permission for saving photos to gallery
            // This is not a required permission to advance
            NativeGallery.Permission permissionResult = NativeGallery.CheckPermission(NativeGallery.PermissionType.Write);
            Debug.Log($"{this} checked {nameof(NativeGallery)} write permission and got result: {permissionResult}");

            if (permissionResult == NativeGallery.Permission.ShouldAsk)
            {
                Debug.Log($"{this} requesting {nameof(NativeGallery)} write permission.");
                permissionResult = NativeGallery.RequestPermission(NativeGallery.PermissionType.Write);
                Debug.Log($"{this} got {nameof(NativeGallery)} write permission result: {permissionResult}");
            }

            // Communicate load progress to user
            SetLoadingProgressText(.99f);

            // Finally, manage checking camera permission, which is required to advance to the rest of the app
            // After all, can't have much fun in AR without the camera
            // The camera permission request has platform-specific logic

#if UNITY_IOS
            yield return IosCameraPermissionRoutine();
#endif
#if UNITY_ANDROID
            yield return AndroidCameraPermissionRoutine();
#endif

            Debug.Log($"{this} {nameof(MainRoutine)} is complete. Allowing user to advance to rest of application");
            // Consider this fully done with load
            // Communicate load progress to user
            SetLoadingProgressText(1f);

            // If we get to this point, we have camera permission and can continue
            // Ensure we're showing the main body text in case we'd been showing the camera permissions text
            ShowMainBodyText();

            // Activate the start button and listen for the click to progress
            guiButton.interactable = true;
            guiButton.onClick.AddListener(OnEventGUIButtonClicked);
            buttonText.fontSize = 24;
            buttonText.text = LoadingDoneText;
        }

        private void ShowCameraPermissionBodyText()
        {
            mainBodyText.SetActive(false);
            cameraPermissionBodyText.SetActive(true);
        }

        private void ShowMainBodyText()
        {
            mainBodyText.SetActive(true);
            cameraPermissionBodyText.SetActive(false);
        }

#if UNITY_IOS
        private IEnumerator IosCameraPermissionRoutine()
        {
            bool hasCameraPermission = false;

            // N.B. In order for the iOS Application.RequestUserAuthorization to function for
            // camera permission, the compiler must detect a reference to the WebCamTexture class or Unity will
            // strip out the corresponding framework and this call will silently fail to do anything!
            foreach (var webcamDevice in WebCamTexture.devices)
            {
                Debug.Log("Detected device webacm: " + webcamDevice);
            }

            // If we already have permission, skip the request
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.Log($"{this} detected that user already has camera permission. Bypassing request.");
                hasCameraPermission = true;
            }
            // Otherwise, inform the user to update their privacy settings and repeatedly check
            else
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
                hasCameraPermission = Application.HasUserAuthorization(UserAuthorization.WebCam);
                Debug.Log($"After permission request, {this} {nameof(hasCameraPermission)} is: {hasCameraPermission}");

                // If user hasn't granted camera permission, wait here indefinitely, prompting them to fix this in privacy settings
                while (!hasCameraPermission)
                {
                    Debug.Log("User hasn't granted camera permission. Displaying prompt to repair this in settings.");
                    ShowCameraPermissionBodyText();
                    yield return new WaitForSeconds(1f);
                    hasCameraPermission = Application.HasUserAuthorization(UserAuthorization.WebCam);
                    Debug.Log($"After wait, {this} {nameof(hasCameraPermission)} is: {hasCameraPermission}");
                    // Currently, iOS will actually force an app restart if the app's camera permission changes, but
                    // if they ever change that behavior, we should be able to resume at this point and allow
                    // the user to proceed
                }
            }
        }
#endif // UNITY_IOS

#if UNITY_ANDROID
        private IEnumerator AndroidCameraPermissionRoutine()
        {
            // Finally, manage camera permission. This IS required in order for the user to enter the rest of the application
            bool hasCameraPermission = false;

            // If we already have permission, skip the request
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log($"{this} detected that user already has camera permission. Bypassing request.");
                hasCameraPermission = true;
            }
            // Otherwise handle the request and callback wait
            else
            {

                bool gotPermissionCallback = false;

                System.Action<string> onPermissionDenied = permissionName =>
                {
                    Debug.Log($"Got Android camera permission denied");
                    gotPermissionCallback = true;
                };

                System.Action<string> onPermissionGranted = permissionName =>
                {
                    Debug.Log($"Got Android camera permission granted");
                    gotPermissionCallback = true;
                    hasCameraPermission = true;
                };

                PermissionCallbacks permissionCallbacks = new PermissionCallbacks();
                permissionCallbacks.PermissionGranted += onPermissionGranted;
                permissionCallbacks.PermissionDenied += onPermissionDenied;
                permissionCallbacks.PermissionDeniedAndDontAskAgain += onPermissionDenied;

                // Monitor camera permission status, and re-ask any time the application regains focus
                // This is necessary in case the user updates permission to "ask every time"
                // in which we need to re-ask in order to get the permission state to update
                while (!hasCameraPermission)
                {
                    gotPermissionCallback = false;

                    Permission.RequestUserPermission(Permission.Camera, permissionCallbacks);

                    yield return new WaitUntil(() => gotPermissionCallback);

                    Debug.Log($"After permission request, {this} {nameof(hasCameraPermission)} is: {hasCameraPermission}");

                    if (!hasCameraPermission)
                    {
                        Debug.Log("User hasn't granted camera permission. Displaying prompt to repair this in settings.");
                        ShowCameraPermissionBodyText();
                        waitingForApplicationToRegainFocus = true;
                        yield return new WaitUntil(() => !waitingForApplicationToRegainFocus);
                    }

                    hasCameraPermission = Permission.HasUserAuthorizedPermission(Permission.Camera);
                }
            }
        }
#endif // UNITY_ANDROID

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!Application.isEditor)
            {
                Debug.Log($"{this} OnApplicationFocus {hasFocus}");

                if (hasFocus)
                {
                    waitingForApplicationToRegainFocus = false;
                }
            }
        }

        private IEnumerator LoadFeatureDataRoutine()
        {
            // Attempt to downlaod the feature data until it succeeds
            while (!succeededFeatureDataLoad)
            {
                Debug.Log($"{this} starting feature download");
                yield return FeatureDownloadRoutine(initialProgressPercent: 0, completedProgressPercent: .97f);

                if (!succeededFeatureDataLoad)
                {
                    Debug.Log($"{this} failed feature download. Running error routine before restarting.");
                    yield return ConnectionErrorRoutine();
                }
            }
        }

        private IEnumerator FeatureDownloadRoutine(float initialProgressPercent, float completedProgressPercent)
        {
            // Initialize the featurePreloadManager. This is harmless if it's already initialized
            featurePreloadManager.Initialize();

            // If features are already downloaded, disable the loading text UI and break out
            if (featurePreloadManager.AreAllFeaturesDownloaded())
            {
                Debug.Log(name + " all features already donwnloaded - bypassing FeatureDownloadRoutine");
                succeededFeatureDataLoad = true;
                SetLoadingProgressText(completedProgressPercent);
                yield break;
            }

            // Otherwise, start the feature download
            Debug.Log(name + " beginning feature download");

            SetLoadingProgressText(initialProgressPercent);

            // Listen for updates as the features download
            FeaturePreloadManager.PreloadProgressUpdatedArgs lastPreloadProgressArgs = null;

            ArdkEventHandler<FeaturePreloadManager.PreloadProgressUpdatedArgs> onPreloadProgressUpdated =
                (FeaturePreloadManager.PreloadProgressUpdatedArgs args) => { lastPreloadProgressArgs = args; };

            featurePreloadManager.ProgressUpdated += onPreloadProgressUpdated;

            float lastProgress = 0f;
            float lastProgressChangeTime = Time.time;

            // Begin the feature download
            featurePreloadManager.StartDownload();

            // Wait feature download to complete or timeout
            while (
                // There has either been no progress, or only incomplete progress 
                (lastPreloadProgressArgs == null || !lastPreloadProgressArgs.PreloadAttemptFinished) &&
                // AND it hasn't been too long since the last progress change
                Time.time - lastProgressChangeTime < TimeBetweenDownloadProgressChangeUntilTimeout)
            {
                float progress = lastPreloadProgressArgs == null ? 0 : lastPreloadProgressArgs.Progress;

                if (!Mathf.Approximately(progress, lastProgress))
                {
                    lastProgressChangeTime = Time.time;
                }

                lastProgress = progress;

                // Lerp progress between the designated range for this part of the load
                SetLoadingProgressText(Mathf.Lerp(initialProgressPercent, completedProgressPercent, progress));

                yield return null;
            }

            // Once finished, unsubscribe from future updates
            featurePreloadManager.ProgressUpdated -= onPreloadProgressUpdated;

            // Stop the download in case if it's still running
            featurePreloadManager.StopDownload();

            // It's a success if the attempt finished with no failures
            bool success = lastPreloadProgressArgs.PreloadAttemptFinished && lastPreloadProgressArgs.FailedPreloads.Count == 0;

            if (success)
            {
                Debug.Log(name + " feature download succeeded.");

                // Set progress to 100% and we're done
                SetLoadingProgressText(completedProgressPercent);

                succeededFeatureDataLoad = true;
            }
            else
            {
                Debug.Log(name + " feature download failed " + (lastPreloadProgressArgs.PreloadAttemptFinished ? " after completing." : " after timeout."));
            }
        }

#if UNITY_ANDROID
        private IEnumerator AndroidLocationPermissionRoutine()
        {
            // If we already have permission, skip the request
            if (Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.Log($"{this} detected that user already has location permission. Bypassing request.");
                yield break;
            }
            // Otherwise handle the request and callback wait
            else
            {
                bool gotPermissionCallback = false;
                bool hasLocationPermission = false;

                System.Action<string> onPermissionDenied = permissionName =>
                {
                    Debug.Log($"Got Android location permission denied");
                    gotPermissionCallback = true;
                };

                System.Action<string> onPermissionGranted = permissionName =>
                {
                    Debug.Log($"Got Android loctaion permission granted");
                    gotPermissionCallback = true;
                    hasLocationPermission = true;
                };

                PermissionCallbacks permissionCallbacks = new PermissionCallbacks();
                permissionCallbacks.PermissionGranted += onPermissionGranted;
                permissionCallbacks.PermissionDenied += onPermissionDenied;
                permissionCallbacks.PermissionDeniedAndDontAskAgain += onPermissionDenied;
                Permission.RequestUserPermission(Permission.FineLocation, permissionCallbacks);

                yield return new WaitUntil(() => gotPermissionCallback);

                Debug.Log($"After permission request, {this} {nameof(hasLocationPermission)} is: {hasLocationPermission}");
            }
        }
#endif

#if UNITY_IOS
        private IEnumerator IosLocationPermissionRoutine()
        {
            // On iOS, location permission is requested upon activating the service
            // First, request location permission
            Debug.Log("IosLocationPermissionRoutine Starting location service");
            ILocationService locationService = LocationServiceFactory.Create();

            LocationServiceStatus locationServiceStatus = LocationServiceStatus.Stopped;
            ArdkEventHandler<LocationStatusUpdatedArgs> onLocationServiceStatusUpdated = (LocationStatusUpdatedArgs args) =>
            {
                Debug.Log($"Location service status updated to {args.Status}");
                locationServiceStatus = args.Status;
            };

            locationService.StatusUpdated += onLocationServiceStatusUpdated;

            locationService.Start();

            // Wait until we get the user's location, a failure or an error
            yield return new WaitUntil(
                () =>
                locationServiceStatus == LocationServiceStatus.Running ||
                locationServiceStatus == LocationServiceStatus.PermissionFailure ||
                locationServiceStatus == LocationServiceStatus.DeviceAccessError ||
                locationServiceStatus == LocationServiceStatus.UnknownError);

            locationService.StatusUpdated -= onLocationServiceStatusUpdated;
            locationService.Stop();

            Debug.Log($"{this} completed {nameof(IosLocationPermissionRoutine)} with locationServiceStatus {locationServiceStatus}");
        }
#endif // UNITY_IOS

        private IEnumerator ConnectionErrorRoutine()
        {
            // Display the connection required banner
            errorManager.DisplayErrorBanner(ErrorTextConnectionRequired, autoHideErrorBanner: false);

            // Wait before displaying next message
            yield return new WaitForSeconds(RetryDownloadMessageDisplayTime);

            // Display the reconnecting banner
            errorManager.DisplayErrorBanner(ErrorTextReconnecting, autoHideErrorBanner: false);

            // Wait before retrying
            yield return new WaitForSeconds(RetryDownloadMessageDisplayTime);

            // Hide the banner
            errorManager.HideErrorBanner();
        }

        private void SetLoadingProgressText(float progressPercent)
        {
            int integerProgressPercent = Mathf.RoundToInt(Mathf.Clamp01(progressPercent) * 100);
            buttonText.text = LoadingText + integerProgressPercent + "%";
        }

        private void OnEventGUIButtonClicked()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);

            if (running)
            {
                Exit();
            }
        }

        private void Exit()
        {
            running = false;
            levelSwitcher.LoadLevel(Level.Homeland, fadeOutBeforeLoad: true);
        }
    }
}
