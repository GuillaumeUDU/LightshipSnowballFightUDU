using Niantic.ARVoyage;
using Niantic.ARVoyage.Vps;

using Mapbox.Utils;
using Nexus.Map;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Extends MapMarker to draw VPS locations in the VPS map view with an icon
    /// based on the type of location (bespoke vs frostflower) and its current
    /// state. Clicking the marker selects for localization.
    /// </summary>
    public class StreetMapMarkerLocation : MapMarker
    {
        public VpsDataEntry VpsDataEntry { get; set; }
        public VpsMarkerType VpsMarkerType { get; private set; }

        public bool IsBespoke => (VpsDataEntry != null) ? VpsDataEntry.bespokeEnabled : false;
        public string BespokeDescription { get; set; } = null;

        private bool selected = false;
        public bool Selected
        {
            get
            {
                return selected;
            }
            set
            {
                selected = value;
            }
        }

        private bool flagFlanted = false;
        public bool FlagPlanted
        {
            get
            {
                return flagFlanted;
            }
            set
            {
                flagFlanted = value;
                RefreshFlagPlanted();
            }
        }

        public Button button;
        public GameObject sparkles;

        public Image imageTourist;
        public Image imageTouristSelected;
        public Image imageTouristFlag;

        public Image imageFrostFlowerSeed;
        public Image imageFrostFlowerSeedSelected;

        public Image imageFrostFlowerPlant;
        public Image imageFrostFlowerPlantSelected;

        public Image imageFrostFlowerBloom;
        public Image imageFrostFlowerBloomSelected;

        public bool readyToHarvest { get; private set; } = false;

        private StreetMapManager streetMapManager;
        private AudioManager audioManager;

        void Awake()
        {
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            audioManager = SceneLookup.Get<AudioManager>();
        }


        public void VpsLocationButtonClick()
        {
            Debug.Log("VpsLocationButtonClick: " + VpsDataEntry.name);

            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);
            streetMapManager.VPSLocationSelected(this);
        }

        public void SetMarkerType(VpsMarkerType vpsMarkerType)
        {
            this.VpsMarkerType = vpsMarkerType;
            SetSelected(selected);
        }

        public void SetSelected(bool value)
        {
            Selected = value;

            imageTourist.gameObject.SetActive(!value && VpsMarkerType == VpsMarkerType.Tourist);
            imageTouristSelected.gameObject.SetActive(value && VpsMarkerType == VpsMarkerType.Tourist);
            RefreshFlagPlanted();

            imageFrostFlowerSeed.gameObject.SetActive(!value && VpsMarkerType == VpsMarkerType.FrostFlowerSeed);
            imageFrostFlowerSeedSelected.gameObject.SetActive(value && VpsMarkerType == VpsMarkerType.FrostFlowerSeed);

            imageFrostFlowerPlant.gameObject.SetActive(!value && VpsMarkerType == VpsMarkerType.FrostFlowerPlant && !readyToHarvest);
            imageFrostFlowerPlantSelected.gameObject.SetActive(value && VpsMarkerType == VpsMarkerType.FrostFlowerPlant && !readyToHarvest);

            imageFrostFlowerBloom.gameObject.SetActive(!value && (VpsMarkerType == VpsMarkerType.FrostFlowerBloom || readyToHarvest));
            imageFrostFlowerBloomSelected.gameObject.SetActive(value && (VpsMarkerType == VpsMarkerType.FrostFlowerBloom || readyToHarvest));

            sparkles.SetActive(readyToHarvest);
        }

        public void SetReadyToHarvest(bool val)
        {
            readyToHarvest = val;
            SetSelected(selected);
        }

        private void RefreshFlagPlanted()
        {
            imageTouristFlag.gameObject.SetActive(flagFlanted && VpsMarkerType == VpsMarkerType.Tourist);
        }

    }
}