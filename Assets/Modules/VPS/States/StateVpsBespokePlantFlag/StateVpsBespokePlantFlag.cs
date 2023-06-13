using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where Doty plants a flag, representing completion of this bespoke "tourist" experience.
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsBespokeGuideToDoty if the user clicks the flag, to replay the bespoke experience.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsBespokePlantFlag : StateBase
    {
        private const string hintTextLetsPlant = "Tap the flag to snap more photos, or go back to the World Map!";
        private const string hintTextFlagAlreadyPlanted = hintTextLetsPlant;

        [Header("State machine")]
        [SerializeField] private GameObject nextState;
        [SerializeField] private GameObject restartState;
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
        [SerializeField] private Button backToStreetMapButton;

        private VpsSceneManager vpsSceneManager;
        private StreetMapManager streetMapManager;
        private VpsDebugManager vpsDebugManager;
        private VpsPane vpsPane;
        private AudioManager audioManager;
        private Fader fader;

        private VpsDotyWithFlag vpsDotyWithFlag;
        private AudioSource flagWindSFX = null;

        public static bool startWithFlagPlanted = false;
        private float timeToUpdateHint = 0f;
        private const float delaySecsForUpdatedHint = 5f;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            vpsDebugManager = SceneLookup.Get<VpsDebugManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            audioManager = SceneLookup.Get<AudioManager>();
            fader = SceneLookup.Get<Fader>();

            // By default, we're not the first state; start off disabled
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            thisState = this.gameObject;
            exitState = null;
            Debug.Log("Starting " + thisState);
            timeStartedState = Time.time;

            // show GUI
            gui.SetActive(true);

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            // show Doty with flag
            string animTrigger = "FlagAlreadyPlanted";
            vpsDotyWithFlag = vpsSceneManager.VpsBespokeContent.vpsDotyWithFlag;
            vpsDotyWithFlag.yetiAnimator.ResetTrigger(animTrigger);
            vpsDotyWithFlag.gameObject.SetActive(true);
            flagWindSFX = audioManager.PlayAudioOnObject(AudioKeys.SFX_General_FlagWind_LP,
                                                        targetObject: vpsDotyWithFlag.gameObject,
                                                        loop: true,
                                                        fadeInDuration: 2f);

            // if directed to start with flag already planted, 
            // jump Doty/flag animation to flag-already-planted
            if (startWithFlagPlanted)
            {
                vpsDotyWithFlag.yetiAnimator.SetTrigger(animTrigger);

                // SPECIAL CASE: if at Gandhi Monument, show airship idling
                if (vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent != null)
                {
                    vpsSceneManager.VpsBespokeContent.vpsDotyBespoke.SetActive(true);
                    vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent.AirshipIdling();
                }
            }

            // else Doty will plant it; record that it was planted
            else
            {
                bool poiFlagPlanted = true;
                Debug.Log("Setting POI Flag Planted state: " + poiFlagPlanted);
                vpsSceneManager.PersistentBespokeStateLookup[vpsSceneManager.CurrentVpsDataEntry.identifier] = poiFlagPlanted;
                vpsSceneManager.PersistentBespokeStateLookup.Save();

                // plant flag SFX
                audioManager.PlayAudioOnObject(AudioKeys.SFX_General_FlagPlant_Frm_0,
                                                targetObject: vpsDotyWithFlag.gameObject);
            }

            // set hint text
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.TopPaneOnly);
            vpsPane.ShowHint(startWithFlagPlanted ? hintTextFlagAlreadyPlanted : hintTextLetsPlant);
            vpsPane.gui.SetActive(true);

            startWithFlagPlanted = false;
            timeToUpdateHint = Time.time + delaySecsForUpdatedHint;

            running = true;
        }

        void OnDisable()
        {
            backToStreetMapButton.onClick.RemoveListener(OnBackToStreetMapButtonPress);
            vpsDebugManager.Relocalize.RemoveListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.RemoveListener(OnLocalizationDestabilized);
        }

        void Update()
        {
            if (!running) return;

            // Be sure we're faded in
            if (!fader.IsSceneFadedIn)
            {
                fader.FadeSceneInImmediate();
            }

            // Update hint text after delay
            if (timeToUpdateHint > 0f && Time.time > timeToUpdateHint)
            {
                vpsPane.ShowHint(hintTextFlagAlreadyPlanted);
                timeToUpdateHint = 0f;
            }

            // Check for touch collision on flag collider
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0));
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    Debug.Log(hit.transform.gameObject.name + " clicked");
                    if (hit.transform.gameObject == vpsDotyWithFlag.flagColliderObject)
                    {
                        StartCoroutine(OnPOIFlagClickedRoutine());
                    }
                }
            }

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
        }

        private IEnumerator OnPOIFlagClickedRoutine()
        {
            // wait a bit, in case an overlapping GUI button was pressed that should win
            yield return new WaitForSeconds(0.1f);

            if (running)
            {
                audioManager.PlayAudioOnObject(AudioKeys.SFX_General_Prop,
                                                targetObject: vpsDotyWithFlag.gameObject);
                exitState = restartState;
            }
        }

        public void OnNextButtonClicked()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            exitState = nextState;
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

            // If not restarting the photo session, destroy bespoke content
            if (nextState != restartState)
            {
                Destroy(vpsSceneManager.VpsBespokeContent.gameObject);
            }
            else
            {
                // hide Doty with flag
                vpsDotyWithFlag.gameObject.SetActive(false);

                // SPECIAL CASE: if at Gandhi Monument, reset and hide airship
                if (vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent != null)
                {
                    vpsSceneManager.VpsBespokeContent.vpsBespokeAirshipContent.AnimatorAndSFXReset();
                    vpsSceneManager.VpsBespokeContent.vpsDotyBespoke.SetActive(false);
                }
            }

            // hide GUI
            gui.SetActive(false);
            backToStreetMapButton.gameObject.SetActive(false);

            // fade out flagWindSFX
            if (flagWindSFX != null)
            {
                audioManager.FadeOutAudioSource(flagWindSFX, fadeDuration: 1f);
                flagWindSFX = null;
            }

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }

}
