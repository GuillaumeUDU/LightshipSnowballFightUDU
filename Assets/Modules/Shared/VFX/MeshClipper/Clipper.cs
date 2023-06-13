// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;
using Niantic.ARDK.Extensions.Gameboard;

namespace Niantic.ARVoyage.Walkabout
{
    /// <summary>
    /// Experimental class to control the Clipper shader material to manage Gameboard occlusion based on elevation and a simulated "water level"
    /// </summary>
    public class Clipper : MonoBehaviour
    {
        [SerializeField] float waterLevel = 0;
        [SerializeField] float surfaceOffset = 0;
        [SerializeField] float surfaceElevation = 0;

        /*
        void Awake()
        {
            GameboardHelper.SurfaceUpdated.AddListener(OnSurfaceUpdated);
        }

        void OnDestroy()
        {
            GameboardHelper.SurfaceUpdated.RemoveListener(OnSurfaceUpdated);
        }

        public void OnSurfaceUpdated(Surface surface)
        {
            surfaceElevation = surface.Elevation;
        }
        */

        void Update()
        {
            waterLevel = surfaceElevation + surfaceOffset;
            Shader.SetGlobalFloat("_WaterLevel", waterLevel);
        }
    }
}
