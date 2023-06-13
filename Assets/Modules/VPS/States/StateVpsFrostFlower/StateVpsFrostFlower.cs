using Niantic.ARVoyage.FrostFlower;

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// State in VPS where for the currently selected VPS location,
    /// - The first time visited, the user is shown instructions, then prompted to throw 3 seeds (one at a time)
    ///   that land in the world and sprout small frostflower plants, with a cluster of surrounding vegetation.
    /// - Subsequent time visited, the user is shown the full-grown ("bloomed") versions of the existing 3 plants.
    /// Achieved by setting properties and calling methods on the FrostFlowerManager, such as PlantingEnabled, ShowSeed, etc.
    /// The state of the plants are saved/loaded locally to/from the user's device via vpsSceneManager.PersistentFrostFlowerStateLookup.
    /// Its next state (values assigned via inspector) is either:
    /// - StateVpsFrostFlowerSuccess if this is the first visit and the 3 seeds were thrown.
    /// - StateVpsTakePhoto if this is a subsequent visit, to offer the user the chance to (re)take a photo of their bloomed plants.
    /// - StateVpsStreetMap if the user presses the BackToStreetMapButton.
    /// - StateVpsLocalization if the Relocalize button is pressed in the debug menu.
    /// - StateVpsLocalizationDestabilized if OnLocalizationDestabilized is externally triggered.
    /// </summary>
    public class StateVpsFrostFlower : StateBase
    {
        private const string instructionsTitleText = "Frostflower Garden";
        private const string instructionsBodyText = "Captain Doty has left some Frostflower seeds here for you to grow your very own garden!\n\nPlant the seeds at this Wayspot now, then come back later to see them fully bloom!";
        private const string instructionsButtonText = "Let's Go!";

        private const string hintText1 = "Take aim and launch the seeds to grow a Frostflower garden!";
        private const string hintText2 = "Two seeds left!";
        private const string hintText3 = "One seed left, make it count!";

        [Header("State machine")]
        [SerializeField] private GameObject nextState;
        [SerializeField] private GameObject stateTakePhoto;
        [SerializeField] private GameObject stateStreetMap;
        [SerializeField] private GameObject stateVpsLocalization;
        [SerializeField] private GameObject stateVpsLocalizationDestabilized;

        [Header("GUI")]
        [SerializeField] private GameObject gui;
        [SerializeField] private Button backToStreetMapButton;
        [SerializeField] private GameObject messageGUI;
        [SerializeField] private TMPro.TMP_Text messageGUITitleText;
        [SerializeField] private TMPro.TMP_Text messageGUIBodyText;
        [SerializeField] private TMPro.TMP_Text messageGUIButtonText;
        [SerializeField] private ButtonWithCooldown plantSeedButton;
        [SerializeField] private GameObject seedPouch;
        [SerializeField] private TMPro.TMP_Text seedPouchText;

        private bool running;
        private float timeStartedState;
        private GameObject thisState;
        private GameObject exitState;
        protected float initialDelay = 1f;

        private GameObject frostFlowerContent;
        private string localizationTargetId = null;

        private VpsWayspotManager vpsWayspotManager;
        private VpsSceneManager vpsSceneManager;
        private StreetMapManager streetMapManager;
        private StreetMapMarkerManager streetMapMarkerManager;
        private VpsPane vpsPane;
        private VpsDebugManager vpsDebugManager;
        private FrostFlower.FrostFlowerManager frostFlowerManager;
        private AudioManager audioManager;
        private Fader fader;

        [SerializeField] private int numInitialSeeds = 3;
        private int seedsLeft;
        private int seedsThrown;
        private int seedsPlanted;
        private bool inThrowCooldown;

        private bool showingInstructions;

        private float timeAdvanceState;

        void Awake()
        {
            vpsWayspotManager = SceneLookup.Get<VpsWayspotManager>();
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            streetMapMarkerManager = SceneLookup.Get<StreetMapMarkerManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
            vpsDebugManager = SceneLookup.Get<VpsDebugManager>();
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
            timeAdvanceState = 0f;

            // Fade in GUI
            messageGUITitleText.text = instructionsTitleText;
            messageGUIBodyText.text = instructionsBodyText;
            messageGUIButtonText.text = instructionsButtonText;
            gui.SetActive(true);
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, fadeDuration: 0.75f));
            vpsPane.gui.SetActive(false);

            // enable debug menu options
            vpsDebugManager.SetOptionsActive(
                VpsSceneManager.IsReleaseBuild() ? vpsDebugManager.bespokeFFOptionsRelease : null,
                true);

            // assigns frostFlowerManager
            InstantiateFrostFlowerContent();
            // Clear this, so that TakePhoto state knows we are not bespoke
            vpsSceneManager.VpsBespokeContent = null;

            // Restore existing location data.
            FrostFlowerSaveData saveData = vpsSceneManager.PersistentFrostFlowerStateLookup.GetOrDefault(localizationTargetId);
            if (saveData != null)
            {
                frostFlowerManager.Clear();
                frostFlowerManager.SetLocationState(saveData.locationState);
            }

            // Respawn plants with wayspot transforms.
            List<Transform> transforms = vpsWayspotManager.GetCurrentAnchorTransforms();
            if (transforms.Count > 0) frostFlowerManager.RespawnPlants(transforms);

            // Subscribe to planted events to place anchors.
            frostFlowerManager.SeedPlanted.AddListener((Transform transform) =>
            {
                vpsWayspotManager.PlaceAnchor(transform, () =>
                {
                    seedsPlanted++;

                    if (seedsPlanted == numInitialSeeds)
                    {
                        Debug.Log("Saving anchor payloads");
                        vpsWayspotManager.SaveCurrentAnchorPayloads();
                    }
                });
            });

            frostFlowerManager.PlantingEnabled = false;
            seedPouch.SetActive(false);
            inThrowCooldown = false;

            // Show instructions if never planted anything
            bool returningToPlantedGarden = saveData != null && saveData.locationState != FrostFlowerLocationState.Unvisited;
            showingInstructions = !returningToPlantedGarden;
            messageGUI.SetActive(showingInstructions);

            // If returning to planted garden, don't show plant seed button
            if (returningToPlantedGarden)
            {
                plantSeedButton.gameObject.SetActive(false);

                // If this is the first time visiting a Planted garden, 
                // immediately progress the FrostFlower state to Harvested
                // (as the Harvested animation is beginning to play)
                if (saveData.locationState == FrostFlowerLocationState.Planted)
                {
                    frostFlowerManager.SetLocationState(FrostFlowerLocationState.Harvested);
                    // Retain existing saveData, since plants haven't respawned yet; we don't want to re-save below with 0 regrown plants
                    //saveData = frostFlowerManager.GetSaveData();
                    // Therefore we need to manually update its location state to Harvested 
                    saveData.locationState = FrostFlowerLocationState.Harvested;
                    // also consider the notification to be shown, in case we returned to this location before the notification
                    saveData.notificationShown = true;
                    Debug.Log("Progressing FrostFlower state to " + saveData.locationState);
                    vpsSceneManager.PersistentFrostFlowerStateLookup[vpsSceneManager.CurrentVpsDataEntry.identifier] = saveData;
                    vpsSceneManager.PersistentFrostFlowerStateLookup.Save();
                }

                // OnRespawnComplete callback will advance us to next state
            }

            // If unplanted, initially make plantSeedButton noninteractable
            // setup for seed throwing, will happen after instructions are done
            else
            {
                plantSeedButton.gameObject.SetActive(true);
                plantSeedButton.Interactable = false;
                plantSeedButton.ShouldAnimateCooldown = true;
                seedsThrown = 0;
                seedsPlanted = 0;
                UpdateSeedsLeft();
            }

            // add button listeners
            plantSeedButton.GetComponent<Button>().onClick.AddListener(OnPlantSeedButtonClicked);

            // if returning to planted garden, we'll get a respawn complete event            
            frostFlowerManager.RespawnComplete.AddListener(OnRespawnComplete);

            // enable and listen for the backToStreetMap button
            backToStreetMapButton.onClick.AddListener(OnBackToStreetMapButtonPress);
            backToStreetMapButton.gameObject.SetActive(true);

            vpsDebugManager.Relocalize.AddListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);

            running = true;
        }

        void OnDisable()
        {
            plantSeedButton.GetComponent<Button>().onClick.RemoveListener(OnPlantSeedButtonClicked);
            backToStreetMapButton.onClick.RemoveListener(OnBackToStreetMapButtonPress);
            vpsDebugManager.Relocalize.RemoveListener(OnRelocalization);
            VpsWayspotManager.LocalizationDestabilized.RemoveListener(OnLocalizationDestabilized);
            if (frostFlowerManager != null)
            {
                frostFlowerManager.SeedPlanted.RemoveListener(OnFinalSeedPlanted);
                frostFlowerManager.RespawnComplete.RemoveListener(OnRespawnComplete);
            }
        }

        public void OnInstructionsButtonClicked()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);

            // Fade out message GUI
            StartCoroutine(DemoUtil.FadeOutGUI(messageGUI, fader, fadeDuration: 0.75f));

            showingInstructions = false;

            // Show seed and seed pouch
            RepositionSeedContainer();
            frostFlowerManager.PlantingEnabled = true;
            frostFlowerManager.ShowSeed();
            seedPouch.SetActive(true);

            // Show hint text
            vpsPane.SetPaneConfiguration(VpsPane.PaneConfiguration.TopPaneOnly);
            UpdateHintText();
            vpsPane.gui.SetActive(true);
        }


        // Once the final seed lands, progress the FrostFlower state from Unplanted to Planted
        private void OnFinalSeedPlanted(Transform transform)
        {
            frostFlowerManager.SetLocationState(FrostFlowerLocationState.Planted);
            FrostFlowerSaveData saveData = frostFlowerManager.GetSaveData();
            Debug.Log("Progressing FrostFlower state to " + saveData.locationState);
            vpsSceneManager.PersistentFrostFlowerStateLookup[vpsSceneManager.CurrentVpsDataEntry.identifier] = saveData;
            vpsSceneManager.PersistentFrostFlowerStateLookup.Save();
        }

        private void OnRespawnComplete()
        {
            Debug.Log("OnRespawnComplete");
            exitState = stateTakePhoto;
        }

        void Update()
        {
            if (!running) return;

            // Be sure we're faded in
            if (!fader.IsSceneFadedIn)
            {
                fader.FadeSceneInImmediate();
            }

            // Update button state based on target validity.
            if (plantSeedButton != null && frostFlowerManager != null && !showingInstructions)
            {
                plantSeedButton.Interactable = frostFlowerManager.HasValidTarget && seedsLeft > 0;
            }

            // When time, advance to next state
            if (timeAdvanceState > 0f && Time.time >= timeAdvanceState)
            {
                exitState = nextState;
            }

            // once button cooldown is done animating, 
            // spawn a new seed, and turn off inThrowCooldown
            if (inThrowCooldown)
            {
                inThrowCooldown = plantSeedButton.InCooldown;

                // Once we're on our final seed, inform button to not animate after next throw
                if (seedsLeft == 1)
                {
                    plantSeedButton.ShouldAnimateCooldown = false;
                }
            }

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

        public void OnPlantSeedButtonClicked()
        {
            if (!inThrowCooldown && frostFlowerManager != null && frostFlowerManager.HasValidTarget)
            {
                frostFlowerManager.Spawn(reshowDelay: 1f);
                seedsThrown++;
                UpdateSeedsLeft();

                if (seedsLeft > 0)
                {
                    inThrowCooldown = true;
                }
            }
            else
            {
                Debug.Log("Invalid click.");
            }
        }

        private void UpdateSeedsLeft()
        {
            seedsLeft = Math.Max(numInitialSeeds - seedsThrown, 0);
            seedPouchText.text = seedsLeft.ToString();
            UpdateHintText();

            // When out of seeds, advance to next state soon
            if (seedsLeft <= 0)
            {
                frostFlowerManager.PlantingEnabled = false;
                timeAdvanceState = Time.time + 7f;

                // Once the final seed lands, we'll progress from Unplanted to Planted
                frostFlowerManager.SeedPlanted.AddListener(OnFinalSeedPlanted);
            }
        }

        private void UpdateHintText()
        {
            if (seedsLeft >= 3) vpsPane.ShowHint(hintText1);
            else if (seedsLeft == 2) vpsPane.ShowHint(hintText2);
            else if (seedsLeft == 1) vpsPane.ShowHint(hintText3);
            else vpsPane.gui.SetActive(false);
        }

        private void InstantiateFrostFlowerContent()
        {
            if (vpsSceneManager.CurrentVpsDataEntry != null)
            {
                localizationTargetId = vpsSceneManager.CurrentVpsDataEntry.identifier;
            }
            else
            {
                // In a mock session, it's possible to not have localization info since we can skip directly to this state
                if (vpsSceneManager.IsMockARSession)
                {
                    // If there's a DevSettings mock VPS content asset with non-bespoke (FrostFlower) content, use it
                    VpsContentSO[] mockContentAssets = DevSettings.MockVpsAssets;
                    foreach (var vpsContentAsset in mockContentAssets)
                    {
                        if (!vpsContentAsset.vpsDataEntry.bespokeEnabled)
                        {
                            localizationTargetId = vpsContentAsset.vpsDataEntry.identifier;
                            break;
                        }
                    }

                    // If no mock DevSettings FrostFlower content asset is found, just use a placeholder localizationTargetId
                    if (localizationTargetId == null)
                    {
                        localizationTargetId = nameof(StateVpsFrostFlower) + "_MockLocalizationTargetId";
                    }

                    Debug.Log(this + " using mock localizationTargetId " + localizationTargetId);
                }
                else
                {
                    Debug.LogError($"Got call to {nameof(InstantiateFrostFlowerContent)} in non-mock session without a CurrentVpsDataEntry");
                    return;
                }
            }

            Debug.Log(this + " instantiating content for localizationTargetId " + localizationTargetId);

            // instantiate content
            frostFlowerContent = Instantiate(vpsSceneManager.CurrentVpsDataEntry.prefab);

            if (frostFlowerContent != null)
            {
                frostFlowerContent.name = "FrostFlowerContentForId_" + localizationTargetId;

                frostFlowerManager = frostFlowerContent.GetComponentInChildren<FrostFlower.FrostFlowerManager>();
            }
            else
            {
                Debug.LogError("Instantiated FrostFlower content was null.");
            }
        }

        private void RepositionSeedContainer()
        {
            Vector3 offsetScreenPostition = plantSeedButton.transform.position;
            offsetScreenPostition.z = .25f;

            Vector3 offsetWorldPosition = Camera.main.ScreenToWorldPoint(offsetScreenPostition);
            offsetWorldPosition = Camera.main.ViewportToWorldPoint(new Vector3(offsetScreenPostition.x / (float)Screen.width, offsetScreenPostition.y / (float)Screen.height, .25f));
            frostFlowerManager.SetSeedOffset(Camera.main.transform.InverseTransformPoint(offsetWorldPosition));
            Debug.Log($"OffsetWorldPosition: {offsetWorldPosition}");
        }

        private void Exit(GameObject nextState)
        {
            running = false;

            StartCoroutine(ExitRoutine(nextState));
        }

        private IEnumerator ExitRoutine(GameObject nextState)
        {
            // when leaving AR, fade out, StopAR and disable meshing
            if (nextState == stateStreetMap || nextState == stateVpsLocalization)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);
                if (nextState == stateStreetMap) vpsSceneManager.StopAR();
            }

            // fade out to black if taking photo next
            if (nextState == stateTakePhoto)
            {
                yield return fader.FadeSceneOut(Color.black, 0.5f);
            }

            // hide GUI
            gui.SetActive(false);
            // restore scaled down instructions
            messageGUI.gameObject.transform.localScale = Vector3.one;
            backToStreetMapButton.gameObject.SetActive(false);

            // when leaving AR, fade out
            if (nextState == stateStreetMap || nextState == stateVpsLocalization)
            {
                yield return fader.FadeSceneOut(Color.white, 0.5f);
            }

            Debug.Log(thisState + " transitioning to " + nextState);

            nextState.SetActive(true);
            thisState.SetActive(false);

            yield break;
        }
    }
}
