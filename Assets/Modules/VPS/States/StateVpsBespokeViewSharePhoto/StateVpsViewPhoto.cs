using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where the user is shown a stack of 1-3 photos taken in the previous TakePhoto state.
    /// The user can browse the photos until clicking the Continue button.
    /// Its next state (values assigned via inspector) is either:
    /// - If in a bespoke "tourist" experience:
    ///     - StateVpsBespokeBadgeUnlocked if a tourist badge has been earned.
    ///     - StateVpsBespokePlantFlag otherwise.
    /// - If in a frostflower experience:
    ///     - StateVpsFrostFlowerBadgeUnlocked if a superbloom badge has been earned.
    ///     - StateVpsFrostFlowerSuccess otherwise.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsViewPhoto : StateBase
    {
        private const string hintText = "Great job! All your photos have been saved to your device!";

        [Header("State machine")]
        [SerializeField] private GameObject nextState;
        [SerializeField] private GameObject statePlantFlag;
        [SerializeField] private GameObject stateFrostFlowerBadgeUnlocked;
        [SerializeField] private GameObject stateFrostFlowerSuccess;
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
        [SerializeField] private Button skipButton;
        [SerializeField] private Button backToStreetMapButton;

        private VpsSceneManager vpsSceneManager;
        private PhotoManager photoAndShareManager;
        private VpsDebugManager vpsDebugManager;
        private VpsPane vpsPane;
        private AudioManager audioManager;
        private Fader fader;

        private GameObject vpsDotyPostBespokeIdling;

        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            photoAndShareManager = SceneLookup.Get<PhotoManager>();
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

            // if bespoke, show idling Doty
            if (vpsSceneManager.VpsBespokeContent != null)
            {
                vpsDotyPostBespokeIdling = vpsSceneManager.VpsBespokeContent.vpsDotyPostBespokeIdling;
                vpsDotyPostBespokeIdling.SetActive(true);
            }

            // display photos in stack
            photoAndShareManager.photoStack.gui.SetActive(true);
            photoAndShareManager.photoStack.DisplayPhotos(true);

            // add button listeners
            skipButton.onClick.AddListener(OnNextButtonClicked);
            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            // set hint text
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.TopPaneOnly);
            vpsPane.ShowHint(hintText);

            running = true;
        }

        void OnDisable()
        {
            skipButton.onClick.RemoveListener(OnNextButtonClicked);
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
            string savePrefix = "FirstBadgeAtIdentifier";

            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);

            // if FrostFlower
            if (vpsSceneManager.VpsBespokeContent == null)
            {
                FrostFlower.FrostFlowerManager frostFlowerManager = SceneLookup.Get<FrostFlower.FrostFlowerManager>();
                exitState = frostFlowerManager.BadgeJustUnlocked() ? stateFrostFlowerBadgeUnlocked : stateFrostFlowerSuccess;
            }

            // if Tourist Visit 1 - NO BADGE UNLOCKED YET, go to statePlantFlag
            // record this location identifier as well
            else if (!SaveUtil.IsBadgeUnlocked(VpsSceneManager.TouristBadgeKey1))
            {
                SaveUtil.SaveBadgeUnlocked(VpsSceneManager.TouristBadgeKey1);
                SaveUtil.SaveString(savePrefix + vpsSceneManager.CurrentVpsDataEntry.identifier);
                exitState = statePlantFlag;
            }

            // if Tourist Visit 2 - BADGE UNLOCKED - go to stateBadgeUnlocked
            else if (!SaveUtil.IsBadgeUnlocked(VpsSceneManager.TouristBadgeKey2) &&
                        // but only if this is a different location than Tourist Visit 1
                        !SaveUtil.IsStringSaved(savePrefix + vpsSceneManager.CurrentVpsDataEntry.identifier))
            {
                SaveUtil.SaveBadgeUnlocked(VpsSceneManager.TouristBadgeKey2);
                exitState = nextState;
            }

            // if Tourist Visit 3+
            else
            {
                exitState = statePlantFlag;
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
            // when leaving AR, fade out, StopAR
            if (nextState == stateStreetMap || nextState == stateVpsLocalization)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);

                if (nextState == stateStreetMap) vpsSceneManager.StopAR();
            }

            // else if bespoke, hide Doty
            else if (vpsSceneManager.VpsBespokeContent != null)
            {
                vpsDotyPostBespokeIdling.SetActive(false);
            }

            // hide photos in stack
            photoAndShareManager.photoStack.DisplayPhotos(false);
            photoAndShareManager.photoStack.gui.SetActive(false);

            // hide GUI
            gui.SetActive(false);
            backToStreetMapButton.gameObject.SetActive(false);

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }

}
