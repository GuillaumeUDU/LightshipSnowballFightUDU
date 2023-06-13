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
    /// A dynamic group of Vps locations (VpsDataEntry). These are created
    /// on the fly by StreetMapMarkerManager and shown as map icons with
    /// a number indicating the count of the locations contained within.
    /// </summary>
    [System.Serializable]
    public class VpsDataCluster
    {
        public VpsMarkerType VpsMarkerType { get; private set; } = VpsMarkerType.FrostFlowerSeed;
        public bool HasReadyToHarvestLocation { get; set; } = false;

        public string geohash;

        public float centerLatitude;
        public float centerLongitude;

        public float minLatitude = Mathf.Infinity;
        public float minLongitude = Mathf.Infinity;

        public float maxLatitude = -Mathf.Infinity;
        public float maxLongitude = -Mathf.Infinity;

        public List<VpsDataEntry> vpsDataEntries = new List<VpsDataEntry>();

        public VpsDataCluster(string geohash)
        {
            this.geohash = geohash;
        }

        public void SetType(VpsMarkerType type)
        {
            this.VpsMarkerType = type;
        }

        public override string ToString()
        {
            return string.Format("key: {0} child entries: {1}", geohash, vpsDataEntries.Count);
        }
    }

    /// <summary>
    /// Custom sorting logic for VpsDataCluster to order by marker type.
    /// </summary>
    class VpsDataClusterSorter : IComparer<VpsDataCluster>
    {
        public int Compare(VpsDataCluster clusterA, VpsDataCluster clusterB)
        {
            return ((int)clusterA.VpsMarkerType).CompareTo((int)clusterB.VpsMarkerType);
        }
    }

}