// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using System.Collections;
using Niantic.ARVoyage.Loading;

namespace Niantic.ARVoyage.Splash
{
    /// <summary>
    /// Manages display of fullscreen images for the app splash scene
    /// </summary>
    public class SplashManager : MonoBehaviour
    {
        public const string LoadingSceneName = "Loading";

        private Fader fader;
        private LevelSwitcher levelSwitcher;

        [SerializeField] private CanvasGroup splash1Group;
        [SerializeField] private CanvasGroup splash2Group;

        private void Awake()
        {
            fader = SceneLookup.Get<Fader>();
            levelSwitcher = SceneLookup.Get<LevelSwitcher>();

            // Start with splash 1 faded in and splash 2 faded out
            splash1Group.alpha = 1;
            splash2Group.alpha = 0;
        }

        private IEnumerator Start()
        {
            if (!DevSettings.SkipSplashWait)
            {
                // Fade in
                yield return fader.FadeSceneIn(Color.white, .5f);

                // Allow time to view splash 1
                yield return new WaitForSeconds(2f);

                // Cross fade the splashes
                fader.Fade(splash1Group, 0f, .75f);
                fader.Fade(splash2Group, 1f, .75f);

                // Allow time to view splash 2
                yield return new WaitForSeconds(3f);

                // Fade out
                yield return fader.FadeSceneOut(Color.white, .5f);
            }

            // Go to loading scene
            levelSwitcher.LoadLevel(LoadingSceneName, fadeOutBeforeLoad: false);
        }
    }
}
