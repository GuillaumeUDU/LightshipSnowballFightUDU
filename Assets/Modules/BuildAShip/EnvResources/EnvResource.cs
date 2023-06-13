// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.BuildAShip
{
    /// <summary>
    /// ARDK semantic segmentation channels available
    /// Accurate as of 2021-06-30
    /// </summary>
    public enum Channel
    {
        sky,
        ground,
        natural_ground,
        artificial_ground,
        water,
        people,
        building,
        flowers,
        foliage,
        tree_trunk,
        pet,
        sand,
        grass,
        tv,
        dirt,
    }

    /// <summary>
    /// ScriptableObject for managing BuildAShip environmental resource data mapped to a semantic segmantation channel
    /// </summary>
    [CreateAssetMenu(fileName = "EnvResource", menuName = "ScriptableObjects/EnvResource")]
    public class EnvResource : ScriptableObject
    {
        public Channel Channel;
        public string ChannelName;
        public string ResourceName;
        public Sprite ResourceIcon;
        public Sprite ResourceSprite;
    }
}