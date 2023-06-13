using Niantic.ARVoyage.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Multi-part GUI:
    ///  - On the streetmap and during localization, the bottom pane displays a location title and hint image.
    ///  - For all states, a pill displays hint text.
    ///  - On the streetmap, a multi-purpose button may display below the hint text pill.
    /// State classes such as StateVpsStreetMap, StateVpsLocalization, etc. call methods on this GUI.
    /// </summary>
    public class VpsPane : MonoBehaviour, ISceneDependency
    {
        public enum PaneConfiguration
        {
            HintImageDefault,
            HintImageLargeNoText,
            TextNoImage,
            TopPaneOnly
        }

        public GameObject gui;

        // -------------
        // Top pane

        [Header("Top Pane")]
        public GameObject topPane;
        [SerializeField] TMPro.TMP_Text topHintMessageText;
        [SerializeField] Image topHintMessageImage;

        [Header("Top Buttons/Status")]
        public Button bypassGpsButton;
        public Button searchButton;
        public Button teleportButton;
        public GameObject searchingStatus;

        // -------------
        // Bottom pane

        [Header("Bottom Pane")]
        public GameObject bottomPane;
        public GameObject titleTextParent;
        public TMPro.TMP_Text titleText;
        public GameObject distanceIndicatorParent;
        public TMPro.TMP_Text distanceIndicatorText;
        public Button paneButton;
        public TMPro.TMP_Text paneButtonText;
        public GameObject statusIconGreenCheck;
        public GameObject statusIconRedX;
        public GameObject bottomPaneBackdrop;
        public TMPro.TMP_Text debugText;

        // HintImageDefault configuation
        public GameObject hintImageDefaultParent;
        public RawImage hintImage;
        public Button hintImageButton;
        private Texture origHintImageTexture;

        // HintImageLargeNoText configuration
        public GameObject hintImageLargeNoTextParent;
        public RawImage hintImageLarge;
        public Button hintImageLargeButton;
        private Texture origHintImageLargeTexture;

        // TextNoImage configuration
        public GameObject textNoImageParent;
        public TMPro.TMP_Text textNoImage;


        [HideInInspector] public StreetMapMarkerLocation vpsLocationOnStreetMap;

        private Vector2 hintImageSize;
        PaneConfiguration paneConfiguration = PaneConfiguration.HintImageDefault;
        private bool usingHintImageOverride = false;

        private VpsSceneManager vpsSceneManager;
        private StreetMapManager streetMapManager;
        private AudioManager audioManager;


        void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            audioManager = SceneLookup.Get<AudioManager>();

            hintImageSize = hintImage.GetComponent<RectTransform>().sizeDelta;

            origHintImageTexture = hintImage.texture;
            origHintImageLargeTexture = hintImageLarge.texture;

            bypassGpsButton.onClick.AddListener(() =>
            {
                Debug.Log("VpsPane TopButtonClick");
                audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
                streetMapManager.BypassGpsButtonClick();
                bypassGpsButton.gameObject.SetActive(false);
            });

            searchButton.onClick.AddListener(() =>
            {
                Debug.Log("VpsPane TopButtonClick");
                audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
                streetMapManager.SearchButtonClick();
                searchButton.gameObject.SetActive(false);
            });

            teleportButton.onClick.AddListener(() =>
            {
                audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
                streetMapManager.TeleportButtonClick();
                teleportButton.gameObject.SetActive(false);
            });

        }

        public void SetPaneConfiguration(PaneConfiguration configuration,
                                            bool showTopPane = true,
                                            bool showBottomPaneBackdrop = true,
                                            bool showBottomPaneTitle = true)
        {
            Debug.Log(this + " SetPaneConfiguration " + configuration);

            paneConfiguration = configuration;

            if (configuration == PaneConfiguration.TopPaneOnly)
            {
                topPane.SetActive(true);
                bottomPane.SetActive(false);
                return;
            }

            topPane.SetActive(showTopPane);
            topHintMessageImage.color = Color.white;
            bottomPane.SetActive(true);
            bottomPaneBackdrop.SetActive(showBottomPaneBackdrop);
            titleTextParent.SetActive(showBottomPaneTitle);
            statusIconGreenCheck.SetActive(false);
            statusIconRedX.SetActive(false);

            // Disable top buttons/status.
            searchButton.gameObject.SetActive(false);
            searchingStatus.gameObject.SetActive(false);
            teleportButton.gameObject.SetActive(false);
            bypassGpsButton.gameObject.SetActive(false);

            switch (configuration)
            {
                case PaneConfiguration.HintImageDefault:
                    hintImageDefaultParent.SetActive(true);
                    hintImageLargeNoTextParent.SetActive(false);
                    textNoImageParent.SetActive(false);
                    paneButton.gameObject.SetActive(true);
                    break;

                case PaneConfiguration.HintImageLargeNoText:
                    hintImageDefaultParent.SetActive(false);
                    hintImageLargeNoTextParent.SetActive(true);
                    textNoImageParent.SetActive(false);
                    paneButton.gameObject.SetActive(false);
                    break;

                case PaneConfiguration.TextNoImage:
                    hintImageDefaultParent.SetActive(false);
                    hintImageLargeNoTextParent.SetActive(false);
                    textNoImageParent.SetActive(true);
                    paneButton.gameObject.SetActive(true);
                    break;

                default:
                    Debug.LogError("Unknown config in SetPaneConfiguration");
                    break;
            }
        }

        public void ShowError(string error)
        {
            topHintMessageImage.color = new Color(1f, 0.8f, 0.8f);
            topHintMessageText.text = error;
        }

        public void ShowHint(string error)
        {
            topHintMessageImage.color = new Color(1f, 1f, 1f);
            topHintMessageText.text = error;
        }

        public void TeleportButtonClick()
        {
        }

        public void TopButtonClick()
        {
        }

        public void PaneButtonClick()
        {
            Debug.Log("VpsPane PaneButtonClick");
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            streetMapManager.PaneButtonClick();
        }

        public void HintImageButtonClick()
        {
            Debug.Log("VpsPane HintImageButtonClick");

            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);

            // Disallow hint image min/max during localizing
            if (vpsSceneManager.CurrentlyLocalizing) return;

            if (paneConfiguration == PaneConfiguration.HintImageDefault)
            {
                SetPaneConfiguration(PaneConfiguration.HintImageLargeNoText,
                                        showBottomPaneBackdrop: bottomPaneBackdrop.activeSelf,
                                        showBottomPaneTitle: titleTextParent.activeSelf);
            }
            else if (paneConfiguration == PaneConfiguration.HintImageLargeNoText)
            {
                SetPaneConfiguration(PaneConfiguration.HintImageDefault,
                                        showBottomPaneBackdrop: bottomPaneBackdrop.activeSelf,
                                        showBottomPaneTitle: titleTextParent.activeSelf);
            }
        }

        public void ClearHintImage()
        {
            hintImage.texture = origHintImageTexture;
            hintImage.uvRect = new Rect(0, 0, 1, 1);

            hintImageLarge.texture = origHintImageLargeTexture;
            hintImage.uvRect = new Rect(0, 0, 1, 1);
        }

        public void FetchHintImage(VpsDataEntry vpsDataEntry)
        {
            // don't destroy built-in hint image overrides
            if (!usingHintImageOverride)
            {
                // Destroy any pre-existing texture to release memory
                // Serves to blank out the image, so any old image isn't still displayed
                if (hintImage.texture != null && hintImage.texture != origHintImageTexture)
                {
                    Destroy(hintImage.texture);
                }

                if (hintImageLarge.texture != null && hintImageLarge.texture != origHintImageLargeTexture)
                {
                    Destroy(hintImageLarge.texture);
                }
            }

            // If we have an override, use it
            if (vpsDataEntry.hintImage != null)
            {
                Debug.Log("VPSPane hint image override");
                hintImage.texture = vpsDataEntry.hintImage;
                FitRawImageToSquare(hintImage);

                hintImageLarge.texture = vpsDataEntry.hintImage;
                FitRawImageToSquare(hintImageLarge);

                usingHintImageOverride = true;
            }

            // else fetch the image
            else if (!string.IsNullOrEmpty(vpsDataEntry.imageUrl))
            {
                StartCoroutine(Networking.GetRemoteTextureRoutine(vpsDataEntry.imageUrl, (texture) =>
                {
                    hintImage.texture = texture;
                    FitRawImageToSquare(hintImage);

                    hintImageLarge.texture = texture;
                    FitRawImageToSquare(hintImageLarge);

                    usingHintImageOverride = false;
                }));
            }

            // otherwise assign original iconographic hint texture
            else
            {
                ClearHintImage();
            }
        }

        public static void FitRawImageToSquare(RawImage rawImage)
        {
            float aspectRatio = rawImage.texture.width / (float)rawImage.texture.height;

            if (aspectRatio >= 1)
            {
                // Landscape
                rawImage.uvRect = new Rect((1 - (1 - aspectRatio) / 2f), 0, 1 / aspectRatio, 1);
            }
            else
            {
                // Portrait
                rawImage.uvRect = new Rect(0, (1 - aspectRatio) / 2f, 1, aspectRatio);
            }
        }
    }
}
