// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Universal button in the upper left corner of all demos, 
    /// allowing the player to return to the Homeland (main menu) at all times.
    /// Includes a retractable confirmation button.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ReturnToHomelandButton : MonoBehaviour
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private GameObject confirmationButtonPanel;
        [SerializeField] private Button confirmationButton;

        private float timeToggled = 0f;
        private const float toggleTimeout = 5f;

        protected LevelSwitcher levelSwitcher;
        AudioManager audioManager;

        void Awake()
        {
            levelSwitcher = SceneLookup.Get<LevelSwitcher>();
        }

        private void OnEnable()
        {
            toggleButton.onClick.AddListener(OnToggleButtonClick);
            confirmationButton.onClick.AddListener(OnConfirmationButtonClick);
        }

        private void OnDisable()
        {
            toggleButton.onClick.RemoveListener(OnToggleButtonClick);
            confirmationButton.onClick.RemoveListener(OnConfirmationButtonClick);
        }

        void Update()
        {
            // untoggle after a timeout
            if (confirmationButtonPanel.activeInHierarchy &&
                Time.time > timeToggled + toggleTimeout)
            {
                OnToggleButtonClick();
            }
        }

        protected virtual void OnToggleButtonClick()
        {
            confirmationButtonPanel.gameObject.SetActive(!confirmationButtonPanel.activeInHierarchy);

            if (confirmationButtonPanel.activeInHierarchy)
            {
                timeToggled = Time.time;
            }

            ButtonSFX(AudioKeys.UI_Slide_Flip);
        }

        protected void OnConfirmationButtonClick()
        {
            ButtonSFX(AudioKeys.UI_Close_Window);

            if (levelSwitcher != null)
            {
                Debug.Log("Return to homeland");
                levelSwitcher.ReturnToHomeland();
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
