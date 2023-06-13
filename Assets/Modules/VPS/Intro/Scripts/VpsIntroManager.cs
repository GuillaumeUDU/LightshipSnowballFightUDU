using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Manager class for the animated airship intro before VPS.
    /// It handles various audio and animation tasks for this sequence.
    /// </summary>
    public class VpsIntroManager : MonoBehaviour, ISceneDependency
    {
        [SerializeField] GameObject introScene;
        [SerializeField] Transform airshipPositionRoot;

        public AppEvent IntroComplete;

        private AudioSource airshipLoopSource = null;
        private AudioSource windLoopSource = null;
        private AudioSource musicLoopSource = null;

        private AudioManager audioManager;
        private LightEstimationHelper lightEstimationHelper;

        void Awake()
        {
            audioManager = SceneLookup.Get<AudioManager>();
            lightEstimationHelper = SceneLookup.Get<LightEstimationHelper>();
        }

        public void Show()
        {
            StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            // Wait one frame to avoid lighting issues in editor.
            yield return null;

            introScene.SetActive(true);
            lightEstimationHelper.gameObject.SetActive(false);

            // Start SFX / music
            airshipLoopSource = audioManager.PlayAudioOnObject(
                AudioKeys.SFX_Airship_LP,
                airshipPositionRoot.gameObject,
                spatialBlend: .5f,
                loop: true,
                fadeInDuration: .5f);

            windLoopSource = audioManager.PlayAudioOnObject(
                AudioKeys.SFX_MountainWind_LP,
                airshipPositionRoot.gameObject,
                spatialBlend: .5f,
                loop: true,
                fadeInDuration: .5f);

            musicLoopSource = audioManager.PlayAudioNonSpatial(
                AudioKeys.MX_Background,
                loop: true,
                fadeInDuration: .5f);

            // Animate airship.
            Vector3 origin = airshipPositionRoot.localPosition;
            Vector3 startPosition = airshipPositionRoot.localPosition + (airshipPositionRoot.right * 40f);

            InterpolationUtil.EasedInterpolation(airshipPositionRoot, airshipPositionRoot, InterpolationUtil.EaseOutSine, 4,
            onUpdate: (t) =>
            {
                airshipPositionRoot.localPosition = Vector3.Lerp(startPosition, origin, t);
            },
            onComplete: () =>
            {
            });

        }

        public void Hide()
        {
            // Fade out SFX loops if playing
            if (airshipLoopSource != null)
            {
                audioManager.FadeOutAudioSource(airshipLoopSource, fadeDuration: 0.5f);
                airshipLoopSource = null;
            }
            if (windLoopSource != null)
            {
                audioManager.FadeOutAudioSource(windLoopSource, fadeDuration: 0.5f);
                windLoopSource = null;
            }
            if (musicLoopSource != null)
            {
                audioManager.FadeOutAudioSource(musicLoopSource, fadeDuration: 0.5f);
                musicLoopSource = null;
            }

            InterpolationUtil.StopRunningInterpolation(airshipPositionRoot, airshipPositionRoot);

            lightEstimationHelper.gameObject.SetActive(true);
            introScene.SetActive(false);
        }
    }
}