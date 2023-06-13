using Niantic.ARVoyage;
using Niantic.ARVoyage.Vps;
using Niantic.ARVoyage.Utilities;

using Mapbox.Utils;
using Mapbox.Unity.Utilities;
using Nexus.Map;

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Extends MapMarker to draw "clusters" of VPS locations in the VPS map view
    /// with an icon based on the contents of the cluster. On click, multi-location
    /// clusters zoom the map to frame the contents of the cluster and single-location
    /// clusters zoom to frame and then select the individual cluster.
    /// </summary>
    public class StreetMapMarkerCluster : MapMarker //StreetMapMarker
    {
        [SerializeField] Button button;
        [SerializeField] TMPro.TMP_Text label;
        [SerializeField] GameObject sparkles;

        [SerializeField] Image imageFrostFlowerSeed;
        [SerializeField] Image imageFrostFlowerBloom;
        [SerializeField] Image imageTourist;

        public VpsDataCluster VpsDataCluster { get; set; }
        public VpsMarkerType VpsMarkerType { get; private set; }

        public string Text
        {
            get { return label.text; }
            set { label.text = value; }
        }

        public void VpsLocationButtonClick()
        {
            Debug.Log("Cluster: " + VpsDataCluster.geohash);

            StreetMapManager streetMapManager = SceneLookup.Get<StreetMapManager>();
            StreetMapMarkerManager streetMapMarkerManager = SceneLookup.Get<StreetMapMarkerManager>();

            if (VpsDataCluster.vpsDataEntries.Count == 1)
            {
                VpsDataEntry vpsDataEntry = VpsDataCluster.vpsDataEntries[0];
                streetMapManager.CenterOnAndSelectVpsDataEntry(vpsDataEntry, duration: 1f);
            }
            else
            {
                // Create a bounding box based on source and refelected entry.
                Vector2d sw = new Vector2d(VpsDataCluster.minLatitude, VpsDataCluster.minLongitude);
                Vector2d ne = new Vector2d(VpsDataCluster.maxLatitude, VpsDataCluster.maxLongitude);

                // Animate to this bounding box with screen fill padding.
                streetMapManager.SetCenteredOnUser(false);

                // streetMapManager.AnimateToFrameBounds(new Vector2dBounds(sw, ne), new Vector2(.65f, .45f), duration: 1f);
                Vector2dBounds bounds = new Vector2dBounds(sw, ne);
                float zoom = streetMapManager.GetNewZoomForFrameBounds(bounds, new Vector2(.65f, .45f));

                // Round down to the nearest .5 zoom level to widen the view without changing geohash precision.
                zoom = Mathf.Floor(zoom / .5f) * .5f;

                // Enforce a minimum zoom that will break apart clusters.
                zoom = Mathf.Max(zoom, StreetMapMarkerManager.ClusteringZoomThreshold);

                // Look for better center point.
                Vector2d center = bounds.Center;
                {
                    VpsDataEntry minimumDistanceEntry = null;
                    float minimumDistance = Mathf.Infinity;

                    foreach (VpsDataEntry vpsDataEntry in VpsDataCluster.vpsDataEntries)
                    {
                        float distance = Geography.GetDistanceBetweenLatLongs(
                            vpsDataEntry.Latitude, vpsDataEntry.Longitude,
                            VpsDataCluster.centerLatitude, VpsDataCluster.centerLongitude
                        );

                        Debug.Log("MinimumDistanceEntry: " + vpsDataEntry + " distance:" + distance);

                        if (distance < minimumDistance)
                        {
                            minimumDistance = distance;
                            minimumDistanceEntry = vpsDataEntry;
                        }
                    }

                    if (minimumDistanceEntry != null)
                    {
                        Debug.Log("MinimumDistanceEntry: " + minimumDistanceEntry + " distance:" + minimumDistance);
                        center = new Vector2d(minimumDistanceEntry.Latitude, minimumDistanceEntry.Longitude);
                    }
                }

                // Fudge factor to prevent unbroken clusters.
                zoom += .125f;

                // Animate to new bounds center and zoom level.
                Debug.Log("Cluster zoom: " + zoom);
                streetMapManager.ZoomAndPan(center, zoom, 1f);
            }

        }

        public void SetMarkerType(VpsMarkerType vpsMarkerType)
        {
            this.VpsMarkerType = vpsMarkerType;

            imageFrostFlowerSeed.gameObject.SetActive(vpsMarkerType == VpsMarkerType.FrostFlowerSeed);
            imageFrostFlowerBloom.gameObject.SetActive(vpsMarkerType == VpsMarkerType.FrostFlowerBloom);
            imageTourist.gameObject.SetActive(vpsMarkerType == VpsMarkerType.Tourist);
            if (sparkles != null) sparkles.SetActive(VpsDataCluster != null && VpsDataCluster.HasReadyToHarvestLocation);
        }

        public float GetZoomForScreenWidthInMeters(float m, float maxZoom)
        {
            float newZoomValue = maxZoom - (float)(Math.Log(m / 20f) / Math.Log(2));
            return newZoomValue;
        }
    }

}