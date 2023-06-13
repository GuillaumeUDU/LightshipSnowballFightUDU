
// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using UnityEngine.UI;

using Niantic.ARDK.Utilities.VersionUtilities;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Manages functionality for the AboutInfo UI
    /// </summary>
    public class AboutInfoManager : MonoBehaviour
    {
        [SerializeField] private GameObject aboutInfoGUI;

        [SerializeField] private GameObject aboutInfoButton;
        [SerializeField] private GameObject aboutInfoBackdrop;

        [SerializeField] private Image clipboardImage;
        [SerializeField] private Image clipboardCheckImage;

        [SerializeField] private TMPro.TMP_Text ardkVersionText;
        [SerializeField] private TMPro.TMP_Text userIdText;
        [SerializeField] private GameObject resetProgressGUI;

        protected AudioManager audioManager;

        public void OnAboutInfoButton()
        {
#if !UNITY_EDITOR
            // Since this accesses the ARDK dll, we only do it outside of editor
            ardkVersionText.text = "ARDK Version: " + ARDKGlobalVersion.GetARDKVersion();
#else
            ardkVersionText.text = "ARDK Version: 2.1.0";
#endif

            userIdText.text = SaveUtil.UserId;

            aboutInfoButton.gameObject.SetActive(false);
            aboutInfoBackdrop.gameObject.SetActive(true);
            aboutInfoGUI.gameObject.SetActive(true);

            clipboardCheckImage.gameObject.SetActive(false);
            clipboardImage.gameObject.SetActive(true);

            ButtonSFX();
        }

        public void OnAboutInfoXCloseButton()
        {
            aboutInfoButton.gameObject.SetActive(true);
            aboutInfoBackdrop.gameObject.SetActive(false);
            aboutInfoGUI.gameObject.SetActive(false);
            ButtonSFX(AudioKeys.UI_Close_Window);
        }

        public void OnAboutInfoResetProgressRequestButton()
        {
            resetProgressGUI.gameObject.SetActive(true);
            ButtonSFX();
        }

        public void OnAboutInfoResetProgressConfirmButton()
        {
            resetProgressGUI.gameObject.SetActive(false);
            ButtonSFX();

            Debug.Log("Reset Progress confirmed");

            // reset progress
            PersistentDataUtility.Clear();
            SaveUtil.Clear();
            SceneLookup.Get<LevelSwitcher>().ReloadCurrentLevel(fadeOutBeforeLoad: true);

        }

        public void OnAboutInfoResetProgressCancelButton()
        {
            resetProgressGUI.gameObject.SetActive(false);
            ButtonSFX();
        }

        public void OnAboutInfoCopyButton()
        {
            clipboardCheckImage.gameObject.SetActive(true);
            clipboardImage.gameObject.SetActive(false);

            GUIUtility.systemCopyBuffer = SaveUtil.UserId;
            ButtonSFX();
        }

        protected void ButtonSFX(string audioKey = AudioKeys.UI_Button_Press)
        {
            audioManager = SceneLookup.Get<AudioManager>();
            audioManager?.PlayAudioNonSpatial(audioKey);
        }
    }
}
