using Mapbox.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Map
{
    /// <summary>
    /// Base class used by all StreetMap markers
    /// </summary>
    public abstract class MapMarker : MonoBehaviour
    {
        public Vector2d LatitudeLongitude { get; set; }
        public Transform Transform { get { return transform; } }
        public int SiblingSortingIndex { get; set; }

        public void Initialize(Vector2d latLon)
        {
            LatitudeLongitude = latLon;
            SiblingSortingIndex = 0;
        }
    }
}
