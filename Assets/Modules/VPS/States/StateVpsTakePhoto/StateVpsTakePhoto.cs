using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where the user is offered a photo camera viewfinder and photo button
    /// to take photos of the experience underway.
    /// - If in a bespoke "tourist" experience, the user has to take 3 photos of Doty in action.
    /// - If in frostflower experience, the user has to take 1 photo of the current plants.
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsViewPhoto once the user clicks on the photo camera.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsTakePhoto : StateBase
    {
        private string hintTextBespoke = "Tap the Trigger below to get some fun snaps of Captain Doty in action!";
        private string hintTextFrostFlower = "Tap the Trigger below to take a photo of your garden!";

        [Header("State machine")]
        [SerializeField] private GameObject nextState;
        [SerializeField] private GameObject stateStreetMap;
        [SerializeField] private GameObject stateVpsLocalization;
        [SerializeField] private GameObject stateVpsLocalizationDestabilized;

        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;
        protected float initialDelay = 1f;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private Button photoButton;
        [SerializeField] private Button backToStreetMapButton;
        [SerializeField] private Photo previewPhoto;

        private VpsSceneManager vpsSceneManager;
        private VpsDebugManager vpsDebugManager;
        private VpsPane vpsPane;
        private AudioManager audioManager;
        private Fader fader;

        private PhotoManager photoAndShareManager;
        private ButtonWithCooldown photoButtonWithCooldown;
        private bool inPhotoCooldown;
        private int photoCtr;

        private GameObject vpsDotyBespoke;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            photoAndShareManager = SceneLookup.Get<PhotoManager>();
            vpsDebugManager = SceneLookup.Get<VpsDebugManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();

            photoButtonWithCooldown = photoButton.GetComponentInChildren<ButtonWithCooldown>();
            if (photoButtonWithCooldown == null)
            {
                Debug.LogError("Null ButtonWithCooldown component in photoButton");
            }

            // By default, we're not the first state; start off disabled
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;

            inPhotoCooldown = false;
            photoCtr = 0;

            // show GUI with photo button and overlay
            gui.SetActive(true);
            photoAndShareManager.ShowViewFinder(true);

            // fade in
            fader.FadeSceneIn(Color.black, 0.5f);

            // add button listeners
            photoButton.onClick.AddListener(OnTakePhotoButtonClicked);

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            // if bespoke, show bespoke Doty, play bespoke SFX
            if (vpsSceneManager.VpsBespokeContent != null)
            {
                vpsDotyBespoke = vpsSceneManager.VpsBespokeContent.vpsDotyBespoke;
                vpsDotyBespoke.SetActive(true);

                // SPECIAL CASE: if at Gandhi Monument, customize SFX play
                if (vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent != null)
                {
                    // Play the airship SFX
                    vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent.AirshipSFX();
                    // Play the bespoke audio on the custom yeti target object since it'll be in motion
                    // Mix the spatial blend so that we'll hear the audio regardless of the yeti's distance away
                    audioManager.PlayAudioOnObject(
                        audioKey: vpsSceneManager.VpsBespokeContent.vpsBespokeAudioKey,
                        targetObject: vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent.yetiAudioObject,
                        spatialBlend: .5f);
                }
                else if (vpsSceneManager.VpsBespokeContent.vpsBespokeAudioKey != null)
                {
                    audioManager.PlayAudioOnObject(vpsSceneManager.VpsBespokeContent.vpsBespokeAudioKey,
                                                    targetObject: vpsDotyBespoke);
                }
            }

            // set hint text
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.TopPaneOnly);
            if (vpsSceneManager.VpsBespokeContent != null)
            {
                vpsPane.ShowHint(hintTextBespoke);
            }
            else
            {
                vpsPane.ShowHint(hintTextFrostFlower);
            }
            vpsPane.gui.SetActive(true);

            // clear photo stack
            photoAndShareManager.photoStack.ClearStack();

            // Hide preview image.
            previewPhoto.gameObject.SetActive(false);

            running = true;
        }

        void OnDisable()
        {
            photoButton.onClick.RemoveListener(OnTakePhotoButtonClicked);
            backToStreetMapButton.onClick.RemoveListener(OnBackToStreetMapButtonPress);
            vpsDebugManager.Relocalize.RemoveListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.RemoveListener(OnLocalizationDestabilized);
        }


        void Update()
        {
            if (!running) return;

            // (re)enable photos, after any cooldown
            if (inPhotoCooldown && exitState == null && !photoButtonWithCooldown.IsPlayingAnimation())
            {
                inPhotoCooldown = false;
            }

            // end state after at least 1 photo was taken, and not in cooldown (needed to ensure photo capture has time to complete)
            if (photoCtr > 0 && !inPhotoCooldown &&
                (vpsSceneManager.VpsBespokeContent == null ||
                 // if bespoke, also wait until Doty is done posing
                 Time.time - timeStartedState > vpsSceneManager.VpsBespokeContent.takePhotoDurationSecs))
            {
                exitState = nextState;
            }

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
        }

        public void OnTakePhotoButtonClicked()
        {
            if (inPhotoCooldown)
            {
                Debug.Log("Photo suppressed during cooldown");
                return;
            }
            inPhotoCooldown = true;
            ++photoCtr;

            // Set photo caption.
            {
                string title = vpsSceneManager.CurrentVpsDataEntry?.name;

                string format = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
                string date = System.DateTime.Now.ToString(format);

                string caption = title + System.Environment.NewLine + date;
                photoAndShareManager.SetCaption(caption);
            }

            photoAndShareManager.TakePhoto((texture) =>
            {
                // Show preview.
                previewPhoto.gameObject.SetActive(true);
                previewPhoto.SetPhotoTexture(texture);
                previewPhoto.Develop();

                // Pop preview photo.
                BubbleScaleUtil.StopRunningScale(previewPhoto.gameObject);
                Vector3 endScale = previewPhoto.transform.localScale;
                previewPhoto.transform.localScale = endScale * .5f;
                BubbleScaleUtil.ScaleUp(previewPhoto.gameObject, endScale.x);

            });
        }

        public void OnBackToStreetMapButtonPress()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            exitState = stateStreetMap;
        }

        private void OnRelocalization()
        {
            exitState = stateVpsLocalization;
        }

        private void OnLocalizationDestabilized()
        {
            exitState = stateVpsLocalizationDestabilized;
        }

        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // when leaving AR, fade out, StopAR
            if (nextState == stateStreetMap || nextState == stateVpsLocalization)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);

                if (nextState == stateStreetMap) vpsSceneManager.StopAR();
            }

            // hide GUI
            gui.SetActive(false);
            photoAndShareManager.ShowViewFinder(false);
            backToStreetMapButton.gameObject.SetActive(false);

            // if bespoke
            if (vpsSceneManager.VpsBespokeContent != null)
            {
                // SPECIAL CASE: if at Gandhi Monument, show airship idling (which hides Doty)
                if (vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent != null)
                {
                    vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent.AirshipIdling();
                }

                // else hide bespoke Doty 
                else
                {
                    vpsDotyBespoke.SetActive(false);
                }
            }

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }

}
