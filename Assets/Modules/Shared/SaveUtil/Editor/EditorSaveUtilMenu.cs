// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using UnityEditor;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Utility class for testing app save state
    /// </summary>
    public class EditorSaveUtilMenu : MonoBehaviour
    {
        #region BadgeUnlocked
        [MenuItem("SaveUtil/BadgeUnlocked/SnowballToss")]
        static void SaveBadgeUnlockedSnowballToss()
        {
            SaveBadgeUnlocked(Level.SnowballToss);
        }

        [MenuItem("SaveUtil/BadgeUnlocked/Walkabout")]
        static void SaveBadgeUnlockedWalkabout()
        {
            SaveBadgeUnlocked(Level.Walkabout);
        }

        [MenuItem("SaveUtil/BadgeUnlocked/SnowballFight")]
        static void SaveBadgeUnlockedSnowballFight()
        {
            SaveBadgeUnlocked(Level.SnowballFight);
        }

        [MenuItem("SaveUtil/BadgeUnlocked/BuildAShip")]
        static void SaveBadgeUnlockedBuildAShip()
        {
            SaveBadgeUnlocked(Level.BuildAShip);
        }

        [MenuItem("SaveUtil/BadgeUnlocked/Homeland")]
        static void SaveBadgeUnlockedHomeland()
        {
            SaveBadgeUnlocked(Level.Homeland);
        }

        [MenuItem("SaveUtil/BadgeUnlocked/All")]
        static void SaveUnlockedAllBadges()
        {
            SaveBadgeUnlocked(Level.SnowballToss);
            SaveBadgeUnlocked(Level.Walkabout);
            SaveBadgeUnlocked(Level.SnowballFight);
            SaveBadgeUnlocked(Level.BuildAShip);
            SaveBadgeUnlocked(Level.Homeland);
        }

        static void SaveBadgeUnlocked(Level level)
        {
            SaveUtil.SaveBadgeUnlocked(level);
        }
        #endregion BadgeUnlocked

        #region BadgeNotificationPresented
        [MenuItem("SaveUtil/BadgeNotificationPresented/SnowballToss")]
        static void SaveBadgeNotificationPresentedSnowballToss()
        {
            SaveBadgeNotificationPresented(Level.SnowballToss);
        }

        [MenuItem("SaveUtil/BadgeNotificationPresented/Walkabout")]
        static void SaveBadgeNotificationPresentedWalkabout()
        {
            SaveBadgeNotificationPresented(Level.Walkabout);
        }

        [MenuItem("SaveUtil/BadgeNotificationPresented/SnowballFight")]
        static void SaveBadgeNotificationPresentedSnowballFight()
        {
            SaveBadgeNotificationPresented(Level.SnowballFight);
        }

        [MenuItem("SaveUtil/BadgeNotificationPresented/BuildAShip")]
        static void SaveBadgeNotificationPresentedBuildAShip()
        {
            SaveBadgeNotificationPresented(Level.BuildAShip);
        }

        [MenuItem("SaveUtil/BadgeNotificationPresented/Homeland")]
        static void SaveBadgeNotificationPresentedHomeland()
        {
            SaveBadgeNotificationPresented(Level.Homeland);
        }

        [MenuItem("SaveUtil/BadgeNotificationPresented/All")]
        static void SaveAllBadgeNotificationsPresented()
        {
            SaveBadgeNotificationPresented(Level.SnowballToss);
            SaveBadgeNotificationPresented(Level.Walkabout);
            SaveBadgeNotificationPresented(Level.SnowballFight);
            SaveBadgeNotificationPresented(Level.BuildAShip);
            SaveBadgeNotificationPresented(Level.Homeland);
        }

        static void SaveBadgeNotificationPresented(Level level)
        {
            SaveUtil.SaveBadgeNotificationPresented(level);
        }
        #endregion BadgeNotificationPresented

        #region airship
        [MenuItem("SaveUtil/Airship/Unlocked")]
        static void SaveAirshipUnlocked()
        {
            SaveUtil.SaveAirshipUnlocked();
        }

        [MenuItem("SaveUtil/Airship/Built")]
        static void SaveAirshipBuilt()
        {
            SaveUtil.SaveAirshipBuilt();
        }

        [MenuItem("SaveUtil/Airship/Departed")]
        static void SaveAirshipDeparted()
        {
            SaveUtil.SaveAirshipDeparted();
        }

        [MenuItem("SaveUtil/Airship/All")]
        static void SaveAirshipAll()
        {
            SaveAirshipUnlocked();
            SaveAirshipBuilt();
            SaveAirshipDeparted();
        }
        #endregion airship

        #region homeland
        [MenuItem("SaveUtil/Homeland/IntroCompleted")]
        static void SaveHomelandIntroCompleted()
        {
            SaveUtil.SaveHomelandIntroCompleted();
        }

        [MenuItem("SaveUtil/Homeland/ThankYouCompleted")]
        static void SaveThankYouCompleted()
        {
            SaveUtil.SaveThankYouCompleted();
        }

        [MenuItem("SaveUtil/Homeland/All")]
        static void SaveHomelandAll()
        {
            SaveHomelandIntroCompleted();
            SaveThankYouCompleted();
        }
        #endregion homeland

        #region scenarios
        [MenuItem("SaveUtil/Scenarios/ReadyForDemoFinale")]
        static void SaveReadyForDemoFinale()
        {
            ClearSaveData();
            SaveHomelandIntroCompleted();
            SaveUtil.SaveHomelandDotHintBubbleCompleted();

            SaveUtil.SaveLastLevelPlayed(Level.BuildAShip);

            SaveAirshipUnlocked();

            SaveBadgeUnlockedAndPresented(Level.SnowballToss);
            SaveBadgeUnlockedAndPresented(Level.Walkabout);
            SaveBadgeUnlockedAndPresented(Level.SnowballFight);
            SaveBadgeUnlocked(Level.BuildAShip);
        }

        #endregion scenarios

        static void SaveBadgeUnlockedAndPresented(Level level)
        {
            SaveBadgeUnlocked(level);
            SaveBadgeNotificationPresented(level);
        }

        // Clear data
        [MenuItem("SaveUtil/Clear")]
        static void ClearSaveData()
        {
            SaveUtil.Clear();
            PersistentDataUtility.Clear();
        }
    }
}