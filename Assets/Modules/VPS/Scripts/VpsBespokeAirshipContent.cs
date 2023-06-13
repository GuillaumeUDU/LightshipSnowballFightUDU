using Niantic.ARVoyage;
using Niantic.ARVoyage.Utilities;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Custom content (Doty and the airship) for the Gandhi location
    /// </summary>
    public class VpsBespokeAirshipContent : MonoBehaviour
    {
        public Animator yetiAnimator;
        public Animator airshipAnimator;
        public GameObject yetiAudioObject;
        public GameObject airshipAudioObject;

        string animTrigger = "AirshipIdling";

        private AudioSource airshipLoopSource = null;

        private AudioManager audioManager;

        void Awake()
        {
            audioManager = SceneLookup.Get<AudioManager>();
        }

        public void AirshipSFX(bool play = true)
        {
            // Fade out / stop the loop if it's playing
            if (airshipLoopSource != null)
            {
                audioManager.FadeOutAudioSource(airshipLoopSource, fadeDuration: (!play ? 0.5f : 0f));
                airshipLoopSource = null;
            }

            airshipLoopSource = audioManager.PlayAudioOnObject(
                AudioKeys.SFX_Airship_LP,
                airshipAudioObject,
                spatialBlend: .75f,
                loop: true,
                fadeInDuration: .5f);
        }

        public void AirshipIdling()
        {
            airshipAnimator.SetTrigger(animTrigger);
            yetiAnimator.gameObject.SetActive(false);
        }

        public void AnimatorAndSFXReset()
        {
            AirshipSFX(play: false);

            yetiAnimator.gameObject.SetActive(true);
            yetiAnimator.Rebind();
            yetiAnimator.Update(0f);

            airshipAnimator.ResetTrigger(animTrigger);
            airshipAnimator.gameObject.SetActive(true);
            airshipAnimator.Rebind();
            airshipAnimator.Update(0f);
        }
    }
}

