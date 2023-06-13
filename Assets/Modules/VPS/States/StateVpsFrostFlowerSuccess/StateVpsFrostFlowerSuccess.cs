using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.ARVoyage.FrostFlower;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where, 
    /// - If the first time visited, the user is shown a success message (since the user had just planted 3 seeds in a previous state).
    /// - If subsequent time visited, the user is shown a hint message to return to the map (since the user had just taken a photo in a previous state).
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsStreetMap if the user accepts the success message or presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsFrostFlowerSuccess : StateBase
    {
        private const string successTitleText = "Great Job!";
        private const string successBodyText = "Come back a little later to see your garden fully bloom in the very same spot!\n\nIn the meantime, check out the World Map for more Wayspots to plant another garden in!";
        private const string successButtonText = "Let's Go!";

        private const string returnHintText = "Tap the World button to return to the map!";

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
        [SerializeField] private GameObject messageGUI;
        [SerializeField] private TMPro.TMP_Text messageGUITitleText;
        [SerializeField] private TMPro.TMP_Text messageGUIBodyText;
        [SerializeField] private TMPro.TMP_Text messageGUIButtonText;
        [SerializeField] private Button backToStreetMapButton;

        private VpsSceneManager vpsSceneManager;
        private StreetMapManager streetMapManager;
        private VpsDebugManager vpsDebugManager;
        private VpsPane vpsPane;
        FrostFlower.FrostFlowerManager frostFlowerManager;
        private AudioManager audioManager;
        private Fader fader;

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

            frostFlowerManager = SceneLookup.Get<FrostFlower.FrostFlowerManager>();
            FrostFlowerSaveData saveData = frostFlowerManager.GetSaveData();

            // If this garden was just planted, show success GUI
            if (saveData.locationState == FrostFlowerLocationState.Planted)
            {
                // Fade in GUI
                messageGUITitleText.text = successTitleText;
                messageGUIBodyText.text = successBodyText;
                messageGUIButtonText.text = successButtonText;
                messageGUI.SetActive(true);
                gui.SetActive(true);
                StartCoroutine(DemoUtil.FadeInGUI(gui, fader, fadeDuration: 0.75f));
                vpsPane.gui.SetActive(false);
            }

            // otherwise show a simple hint banner
            else
            {
                vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.TopPaneOnly);
                vpsPane.ShowHint(returnHintText);
                vpsPane.gui.SetActive(true);
            }

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

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

            if (exitState != null)
            {
                Exit(exitState);
                return;
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
            // Fade out GUI
            yield return StartCoroutine(DemoUtil.FadeOutGUI(gui, fader, fadeDuration: 0.75f));
            vpsPane.gui.SetActive(false);
            backToStreetMapButton.gameObject.SetActive(false);

            // when leaving AR, fade out, StopAR and disable meshing
            if (nextState == stateStreetMap || nextState == stateVpsLocalization)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);
                if (nextState == stateStreetMap) vpsSceneManager.StopAR();
            }

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }

}
