using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where Doty offers the user a photo camera to click on and take.
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsTakePhoto once the user clicks on the photo camera.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsBespokeOfferCamera : StateBase
    {
        private const string hintText = "Tap on the Camera to take some photos!";

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

            // if no vpsDotyWithCamera, move on to next state immediately
            vpsDotyWithCamera = vpsSceneManager.VpsBespokeContent.vpsDotyWithCamera;
            if (vpsDotyWithCamera == null)
            {
                exitState = nextState;
                running = true;
                return;
            }

            // show GUI
            gui.SetActive(true);

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            // hide Doty's camera; the ShowCamera event will enable it
            vpsDotyWithCamera.photoCamera.SetActive(false);

            // trigger Doty animation offering photo camera
            vpsDotyWithCamera.yetiAnimator.SetTrigger("OfferCamera");

            // set hint text
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.TopPaneOnly);
            vpsPane.ShowHint(hintText);
            vpsPane.gui.SetActive(true);

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

            // Check for touch collision on photo camera collider
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0));
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    Debug.Log(hit.transform.gameObject.name + " clicked");
                    if (hit.transform.gameObject == vpsDotyWithCamera.photoCameraColliderObject)
                    {
                        StartCoroutine(OnPhotoCameraClickedRoutine());
                    }
                }
            }

            if (exitState != null)
            {
                Exit(exitState);
                return;
            }
        }

        private IEnumerator OnPhotoCameraClickedRoutine()
        {
            // wait a bit in case an overlapping GUI button was pressed, that should win
            yield return new WaitForSeconds(0.1f);

            if (running)
            {
                audioManager.PlayAudioOnObject(AudioKeys.SFX_General_Prop,
                                                targetObject: vpsDotyWithCamera.gameObject);
                exitState = nextState;
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

            // else fade out, then hide Doty with camera
            else
            {
                // fade out
                yield return fader.FadeSceneOut(Color.black, 0.5f);

                if (vpsDotyWithCamera != null)
                {
                    vpsDotyWithCamera.gameObject.SetActive(false);
                }
            }

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
