// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using Niantic.ARDK.Configuration;

namespace Niantic.ARVoyage.Homeland
{
    /// <summary>
    /// Manages setting the default fixed timestamp for the project
    /// </summary>
    public class HomelandManager : MonoBehaviour, ISceneDependency
    {
        public const float DefaultFixedTimestep = .02f;

        void Awake()
        {
            // When loading into homeland, restore the default fixed timestep since this may be set per scene
            Time.fixedDeltaTime = DefaultFixedTimestep;
        }
    }
}
