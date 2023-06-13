using Niantic.ARDK.LocationService;
using Niantic.ARDK.VPSCoverage;

using Niantic.ARVoyage;
using Niantic.ARVoyage.Utilities;

using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Class representing an individual VPS location with accompanying metadata.
    /// Used primarily to wrap Localization Target data with some additional 
    /// convenience fields/methods. Also used for seeding data for developer and
    /// "bespoke" locations.
    /// </summary>
    [System.Serializable]
    public class VpsDataEntry
    {
        public VpsMarkerType VpsMarkerType { get; private set; } = VpsMarkerType.FrostFlowerSeed;

        [Tooltip("Should this SO be included in the developer debug menu for advancing to localization by content?")]
        public bool includeInDebugMenu = false;

        [Header("Localization Target Data")]
        public string identifier;
        public string name;
        public LatLng latitudeLongitude;
        public string imageUrl;

        [Header("Hint Image")]
        [Tooltip("If left null this will be populated by the Localization Target data.")]
        public Texture2D hintImage = null;

        [Header("Bespoke Content")]
        [Tooltip("Should this SO be treated as bespoke, meaning it has custom content for the VPS map ID?")]
        public bool bespokeEnabled = false;
        [Tooltip("The prefab to instantiate when localizing at the VPS Map ID associated with this SO")]
        public GameObject prefab;

        [Header("Developer")]
        public bool injectIntoCoverageResults = false;

        public string Geohash
        {
            get { return Geography.GetGeohash((float)latitudeLongitude.Latitude, (float)latitudeLongitude.Longitude, 12); }
        }

        public float Longitude
        {
            get { return (float)latitudeLongitude.Longitude; }
        }

        public float Latitude
        {
            get { return (float)latitudeLongitude.Latitude; }
        }

        public void SetType(VpsMarkerType type)
        {
            this.VpsMarkerType = type;
        }

        /// <summary>
        /// When mocking localization to view this content, the camera is placed at a position to view this point
        /// </summary>
        public Transform GetMockCameraHeightStagingPointTransform()
        {
            if (prefab != null)
            {
                if (prefab.TryGetComponent<VpsMockLocalizable>(out VpsMockLocalizable vpsMockLocalizable))
                {
                    return vpsMockLocalizable.mockCameraHeightStagingPoint;
                }
            }
            return null;
        }

        public override string ToString()
        {
            return string.Format("id: {0} label: {1} latitude: {2} longitude: {3} imageUrl: {4} isBespoke: {5}",
                                  identifier, name, Latitude, Longitude, imageUrl, bespokeEnabled);
        }
    }

    /// <summary>
    /// Custom sorting logic for VpsDataEntry to order by marker type.
    /// </summary>
    class VpsDataEntrySorter : IComparer<VpsDataEntry>
    {
        public int Compare(VpsDataEntry entryA, VpsDataEntry entryB)
        {
            return ((int)entryA.VpsMarkerType).CompareTo((int)entryB.VpsMarkerType);
        }
    }

}