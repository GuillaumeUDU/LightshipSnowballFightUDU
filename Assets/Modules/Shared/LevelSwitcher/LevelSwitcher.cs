// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Utility for switching levels in the project
    /// </summary>
    public class LevelSwitcher : MonoBehaviour, ISceneDependency
    {
        public void ReturnToHomeland()
        {
            LoadLevel(Level.Homeland, fadeOutBeforeLoad: true);
        }

        public void ExitToWorldMap()
        {
            LoadLevel(Level.VPS, fadeOutBeforeLoad: true);
        }

        public void LoadLevel(Level level, bool fadeOutBeforeLoad)
        {
            LoadLevel(level.ToString(), fadeOutBeforeLoad);
        }

        public void LoadLevel(string levelName, bool fadeOutBeforeLoad)
        {
            LoadScene(levelName, fadeOutBeforeLoad);
        }

        public void ReloadCurrentLevel(bool fadeOutBeforeLoad)
        {
            Scene currentScene = SceneManager.GetActiveScene();
            LoadScene(currentScene.name, fadeOutBeforeLoad);
        }

        private void LoadScene(string sceneName, bool fadeOutBeforeLoad)
        {
            if (fadeOutBeforeLoad && SceneLookup.TryGet<Fader>(out Fader fader))
            {
                fader.FadeSceneOut(Color.white, onComplete: () => SceneManager.LoadScene(sceneName));
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}

