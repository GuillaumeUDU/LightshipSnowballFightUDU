// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.ARVoyage.Vps;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// GUI offering the user to leave VPS and to go to the original ARVoyage Homeland island with its demos.
    /// Accessed by clicking the Return to Homeland button on the street map.
    /// </summary>
    public class ReturnToHomelandUI : MonoBehaviour
    {
        [SerializeField] private Button returnToHomelandButton;
        [SerializeField] private GameObject returnToHomelandPane;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button letsGoButton;

        protected LevelSwitcher levelSwitcher;
        AudioManager audioManager;
        private VpsPane vpsPane;

        private bool vpsTopPaneTemporarilyHidden = false;

        void Awake()
        {
            levelSwitcher = SceneLookup.Get<LevelSwitcher>();
            audioManager = SceneLookup.Get<AudioManager>();
            vpsPane = SceneLookup.Get<VpsPane>();
        }

        public void OnToggleButtonClick()
        {
            ButtonSFX();

            bool showingGUI = !returnToHomelandPane.activeInHierarchy;
            returnToHomelandPane.SetActive(showingGUI);

            ManageVpsTopPane(showingGUI);
        }

        public void OnCancelButtonClick()
        {
            ButtonSFX();
            returnToHomelandPane.SetActive(false);
            RestoreVpsTopPane();
        }

        public void OnLetsGoButtonClick()
        {
            ButtonSFX();

            if (levelSwitcher != null)
            {
                Debug.Log("Exit to homeland");
                levelSwitcher.ReturnToHomeland();
                RestoreVpsTopPane();
            }
            else
            {
                Debug.LogError(name + " couldn't find scene switcher");
            }
        }

        protected void ButtonSFX(string audioKey = AudioKeys.UI_Button_Press)
        {
            audioManager?.PlayAudioNonSpatial(audioKey);
        }

        private void ManageVpsTopPane(bool showingGUI)
        {
            // If now visible, temporarily hide the VPS top pane
            if (showingGUI)
            {
                if (vpsPane.topPane.activeInHierarchy)
                {
                    vpsPane.topPane.SetActive(false);
                    vpsTopPaneTemporarilyHidden = true;
                }
            }

            // else if now not visible, restore the VPS top pane
            else
            {
                RestoreVpsTopPane();
            }
        }

        private void RestoreVpsTopPane()
        {
            if (vpsTopPaneTemporarilyHidden)
            {
                vpsPane.topPane.SetActive(true);
                vpsTopPaneTemporarilyHidden = false;
            }
        }

    }
}
