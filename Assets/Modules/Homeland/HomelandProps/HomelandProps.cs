// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Homeland
{
    /// <summary>
    /// Manages display of the "prop" objects that communicate the user's progress at each Homeland scene waypoint
    /// </summary>
    public class HomelandProps : MonoBehaviour
    {
        [SerializeField] GameObject propWalkaboutIncomplete;
        [SerializeField] GameObject propWalkaboutComplete;

        [SerializeField] GameObject propSnowballTossIncomplete;
        [SerializeField] GameObject propSnowballTossComplete;

        [SerializeField] GameObject propSnowballFightIncomplete;
        [SerializeField] GameObject propSnowballFightComplete;

        private void OnEnable()
        {
            bool walkaboutCompleted = SaveUtil.IsBadgeUnlocked(Level.Walkabout);
            propWalkaboutIncomplete.SetActive(!walkaboutCompleted);
            propWalkaboutComplete.SetActive(walkaboutCompleted);

            bool snowballTossCompleted = SaveUtil.IsBadgeUnlocked(Level.SnowballToss);
            propSnowballTossIncomplete.SetActive(!snowballTossCompleted);
            propSnowballTossComplete.SetActive(snowballTossCompleted);

            bool snowballFightCompleted = SaveUtil.IsBadgeUnlocked(Level.SnowballFight);
            propSnowballFightIncomplete.SetActive(!snowballFightCompleted);
            propSnowballFightComplete.SetActive(snowballFightCompleted);
        }
    }
}
