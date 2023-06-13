using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where the user is guiding to a particular location near Doty
    ///  at the current bespoke "tourist" location.
    /// The state's WorldStandingPointIndicator displays an arrow of where the user should walk to, 
    ///  as well as hint text displayed via this state.
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsBespokeOfferCamera once the user is at the worldStandingPoint.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsBespokeGuideToDoty : StateBase
    {
        private const string hintText = "Stand in Captain Doty's footsteps to start the experience!";

        private const float closeToStandingPointDistance = 0.7f;

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
        [SerializeField] private Button backToStreetMapButton;

        private VpsSceneManager vpsSceneManager;
        private VpsDebugManager vpsDebugManager;
        private VpsPane vpsPane;
        private AudioManager audioManager;
        private Fader fader;

        private VpsDotyWithCamera vpsDotyWithCamera;
        private WorldStandingPointIndicator worldStandingPointIndicator;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
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

            vpsDotyWithCamera = vpsSceneManager.VpsBespokeContent.vpsDotyWithCamera;
            worldStandingPointIndicator = vpsSceneManager.VpsBespokeContent.worldStandingPointIndicator;

            // show GUI
            gui.SetActive(true);

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            // show Doty
            // hide Doty's camera, the ShowCamera event will enable it
            if (vpsDotyWithCamera != null && !vpsDotyWithCamera.gameObject.activeSelf)
            {
                vpsDotyWithCamera.gameObject.SetActive(true);
                vpsDotyWithCamera.photoCamera.SetActive(false);
                audioManager.PlayAudioOnObject(AudioKeys.SFX_Doty_Appear,
                                                targetObject: vpsDotyWithCamera.gameObject);
            }

            // failsafe
            if (worldStandingPointIndicator == null)
            {
                OnWorldStandingPointReached();
                running = true;
                return;
            }

            // listen for worldStandingPointIndicator ReachedStandingPoint event
            worldStandingPointIndicator.ReachedStandingPoint.AddListener(OnWorldStandingPointReached);

            // show worldStandingPointIndicator
            worldStandingPointIndicator.AnimateIn();
            // SFX
            audioManager.PlayAudioOnObject(AudioKeys.SFX_Success_Magic,
                                            targetObject: worldStandingPointIndicator.gameObject);

            // set hint text
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.TopPaneOnly);
            vpsPane.ShowHint(hintText);
            vpsPane.gui.SetActive(true);

            running = true;
        }

        void OnDisable()
        {
            backToStreetMapButton.onClick.RemoveListener(OnBackToStreetMapButtonPress);
            if (worldStandingPointIndicator != null) worldStandingPointIndicator.ReachedStandingPoint.RemoveListener(OnWorldStandingPointReached);
            vpsDebugManager.Relocalize.RemoveListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.RemoveListener(OnLocalizationDestabilized);
        }

        void OnWorldStandingPointReached()
        {
            // SFX
            audioManager.PlayAudioOnObject(AudioKeys.SFX_Snowball_SizeAchieved,
                                            targetObject: worldStandingPointIndicator.gameObject);

            exitState = nextState;
        }

        void Update()
        {
            if (!running) return;

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
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
            // when leaving AR, fade out, StopAR and destroy bespoke content
            if (nextState == stateStreetMap || nextState == stateVpsLocalization)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);
                if (nextState == stateStreetMap) vpsSceneManager.StopAR();
                Destroy(vpsSceneManager.VpsBespokeContent.gameObject);
            }

            // hide GUI
            gui.SetActive(false);
            backToStreetMapButton.gameObject.SetActive(false);

            // hide worldStandingPointIndicator
            if (worldStandingPointIndicator != null) worldStandingPointIndicator.Hide();

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }

}
