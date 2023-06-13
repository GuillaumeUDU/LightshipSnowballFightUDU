// Copyright 2022 Niantic, Inc. All Rights Reserved.

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Manages the UI button that allows users to go from the Homeland scene to the VPS scene
    /// </summary>
    public class ExitToWorldMap : MonoBehaviour
    {
        [SerializeField] private Button exitToWorldMapButton;
        [SerializeField] private GameObject exitToWorldMapPane;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button letsGoButton;

        protected LevelSwitcher levelSwitcher;
        AudioManager audioManager;

        void Awake()
        {
            levelSwitcher = SceneLookup.Get<LevelSwitcher>();
        }

        public void OnToggleButtonClick()
        {
            ButtonSFX();
            exitToWorldMapPane.SetActive(!exitToWorldMapPane.activeInHierarchy);
        }

        public void OnCancelButtonClick()
        {
            ButtonSFX();
            exitToWorldMapPane.SetActive(false);
        }

        public void OnLetsGoButtonClick()
        {
            ButtonSFX();

            if (levelSwitcher != null)
            {
                Debug.Log("Exit to world map");
                levelSwitcher.ExitToWorldMap();
            }
            else
            {
                Debug.LogError(name + " couldn't find scene switcher");
            }
        }

        protected void ButtonSFX(string audioKey = AudioKeys.UI_Button_Press)
        {
            audioManager = SceneLookup.Get<AudioManager>();
            audioManager?.PlayAudioNonSpatial(audioKey);
        }
    }
}
