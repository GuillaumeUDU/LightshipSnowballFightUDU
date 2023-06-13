// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Utility for saving player progression through the app
    /// </summary>
    public static class SaveUtil
    {
        public const string KeyUserId = "UserId";
        public const string KeyBadgeUnlocked = "BadgeUnlocked_";
        public const string KeyBadgeNotificationPresented = "BadgeNotificationPresented_";
        public const string KeyHomelandIntroCompleted = "HomelandIntroCompleted";
        public const string KeyHomelandInstructionsCompleted = "HomelandInstructionsCompleted";
        public const string KeyHomelandDotHintBubbleCompleted = "HomelandDotHintBubbleCompleted";
        public const string KeyLastLevelPlayed = "LastLevelPlayed";
        public const string KeyAirshipUnlocked = "AirshipUnlocked";
        public const string KeyAirshipBuilt = "AirshipBuilt";
        public const string KeyAirshipDeparted = "AirshipDeparted";
        public const string KeyThankYouCompleted = "ThankYouCompleted";

        private static string userId = null;
        public static string UserId
        {
            get
            {
                if (string.IsNullOrEmpty(userId))
                {
                    Debug.Log("Loading ARDK UserId from PlayerPrefs.");
                    userId = GetString(KeyUserId);

                    if (string.IsNullOrEmpty(userId))
                    {
                        Debug.Log("ARDK UserId not found, saving to PlayerPrefs.");
                        userId = System.Guid.NewGuid().ToString();
                        SaveString(KeyUserId, userId);
                    }
                }

                return userId;
            }
        }

        public static void SaveBadgeUnlocked(Level level)
        {
            if (level != Level.None)
            {
                SaveBadgeUnlocked(KeyBadgeUnlocked + level.ToString());
            }
        }

        public static void SaveBadgeUnlocked(string badgeName)
        {
            SaveString(badgeName);
        }

        public static void SaveString(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                SaveTrue(str);
            }
        }

        public static bool IsBadgeUnlocked(Level level)
        {
            return IsBadgeUnlocked(KeyBadgeUnlocked + level.ToString());
        }

        public static bool IsBadgeUnlocked(string badgeName)
        {
            return IsStringSaved(badgeName);
        }

        public static bool IsStringSaved(string str)
        {
            return !string.IsNullOrEmpty(str) ? IsTrue(str) : false;
        }

        public static void SaveBadgeNotificationPresented(Level level)
        {
            SaveTrue(KeyBadgeNotificationPresented + level.ToString());
        }

        public static bool WasBadgeNotificationPresented(Level level)
        {
            return IsTrue(KeyBadgeNotificationPresented + level.ToString());
        }

        public static void SaveHomelandIntroCompleted()
        {
            SaveTrue(KeyHomelandIntroCompleted);
        }

        public static bool HasHomelandIntroEverCompleted()
        {
            return IsTrue(KeyHomelandIntroCompleted);
        }

        public static void SaveHomelandDotHintBubbleCompleted()
        {
            SaveTrue(KeyHomelandDotHintBubbleCompleted);
        }

        public static bool IsHomelandDotHintBubbleCompleted()
        {
            return IsTrue(KeyHomelandDotHintBubbleCompleted);
        }

        public static void SaveAirshipUnlocked()
        {
            SaveTrue(KeyAirshipUnlocked);
        }

        public static bool IsAirshipUnlocked()
        {
            return IsTrue(KeyAirshipUnlocked);
        }

        public static void SaveAirshipBuilt()
        {
            SaveTrue(KeyAirshipBuilt);
        }

        public static bool IsAirshipBuilt()
        {
            return IsTrue(KeyAirshipBuilt);
        }

        public static void SaveAirshipDeparted()
        {
            SaveTrue(KeyAirshipDeparted);
        }

        public static bool IsAirshipDeparted()
        {
            return IsTrue(KeyAirshipDeparted);
        }

        public static void SaveThankYouCompleted()
        {
            SaveTrue(KeyThankYouCompleted);
        }

        public static bool IsThankYouCompleted()
        {
            return IsTrue(KeyThankYouCompleted);
        }

        public static void SaveLastLevelPlayed(Level level)
        {
            SaveString(KeyLastLevelPlayed, level.ToString());
        }

        public static Level GetLastLevelPlayed()
        {
            string lastLevelPlayed = GetString(KeyLastLevelPlayed);

            if (String.IsNullOrEmpty(lastLevelPlayed))
            {
                return Level.None;
            }

            foreach (Level level in Enum.GetValues(typeof(Level)))
            {
                if (lastLevelPlayed == level.ToString())
                {
                    return level;
                }
            }

            Debug.LogError("Couldn't find level for saved last level played " + lastLevelPlayed);
            return Level.None;
        }

        /// <summary>
        /// Clear all saved data from disk
        /// </summary>
        public static void Clear()
        {
            Debug.Log(typeof(SaveUtil).Name + " clearing saved data");
            PlayerPrefs.DeleteAll();

            // Retain user for clearing remote data with Niantic.
            SaveString(KeyUserId, userId);
        }

        static SaveUtil()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        // Write any unsaved prefs to disk on scene unloaded, 
        // since this is a safe time to cause a frame-rate hitch
        // Prefs are automatically written to disk by Unity when the application quits
        private static void OnSceneUnloaded(Scene scene)
        {
            PlayerPrefs.Save();
        }

        private static bool IsTrue(string key)
        {
            return PlayerPrefs.GetInt(key) == 1;
        }

        private static void SaveTrue(string key)
        {
            Debug.Log(typeof(SaveUtil).Name + " SetTrue " + key);

            PlayerPrefs.SetInt(key, 1);
        }

        private static void SaveFalse(string key)
        {
            Debug.Log(typeof(SaveUtil).Name + " SetFalse " + key);
            PlayerPrefs.SetInt(key, 0);
        }

        private static void SaveString(string key, string value)
        {
            Debug.Log(typeof(SaveUtil).Name + " SaveString " + key);

            PlayerPrefs.SetString(key, value);
        }

        private static string GetString(string key)
        {
            return PlayerPrefs.GetString(key);
        }
    }
}
