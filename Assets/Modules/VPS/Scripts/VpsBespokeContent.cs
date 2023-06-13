using Niantic.ARVoyage;
using Niantic.ARVoyage.Utilities;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Assets for the bespoke "tourist" experience pattern, including:
    /// - Doty holding a photo camera; same for almost all locations (null if inapplicable to this location)
    /// - Doty's custom model/animations for this location
    /// - Custom SFX to accompany Doty's custom model/animations for this location
    /// - Doty idling/waiting; same for almost all locations 
    /// - Doty planting a flag, and the looping flag itself; same for almost all locations 
    /// - Custom additional content for the Gandhi Monument location
    /// </summary>
    public class VpsBespokeContent : MonoBehaviour
    {
        public Transform contentParent;

        public WorldStandingPointIndicator worldStandingPointIndicator;

        public VpsDotyWithCamera vpsDotyWithCamera;
        public GameObject vpsDotyBespoke;
        public string vpsBespokeAudioKey;
        public GameObject vpsDotyPostBespokeIdling;
        public VpsDotyWithFlag vpsDotyWithFlag;
        public float takePhotoDurationSecs = 10f;

        // custom content logic
        public VpsBespokeAirshipContent vpsBespokeAirshipContent;

        public List<GameObject> referenceMeshes;

        private void Awake()
        {
            VpsWayspotManager.LocalizationDestabilized.AddListener(OnLocalizationDestabilized);
        }

        private void OnLocalizationDestabilized()
        {
            Debug.Log($"{this} destroying self because localization destabilized.");
            Destroy(gameObject);
            VpsWayspotManager.LocalizationDestabilized.RemoveListener(OnLocalizationDestabilized);
        }
    }
}

