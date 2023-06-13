using Niantic.ARVoyage;
using Mapbox.Directions;
using Mapbox.Utils;
using Mapbox.Unity;
using Mapbox.Unity.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.Map
{
    /// <summary>
    /// Offers a recenter button on the map.
    /// DropPin functionalty is unused in this app.
    /// </summary>    
    public class MapControlPanel : MonoBehaviour
    {
        [SerializeField] private MapManager mapManager;
        [SerializeField] private Button recenterButton;

        private AudioManager audioManager;

        void Awake()
        {
            audioManager = SceneLookup.Get<AudioManager>();
        }

        public void HandleRecenterClick()
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.UI_Button_Press);

            // When recentering, retain current close-in-zoom value.
            // Otherwise reset zoom to initial value
            float zoom = Mathf.Max(mapManager.MapZoom, mapManager.initialMapZoom);
            mapManager.RecenterOnUser(zoom);
        }

        public void SetRecenterButtonInteractable(bool val)
        {
            recenterButton.interactable = val;
        }

        public void HandleDropPinClick()
        {
            Vector2d mapCenter = new Vector2d((float)mapManager.map.CenterLatitudeLongitude.x,
                                                (float)mapManager.map.CenterLatitudeLongitude.y);
            //mapManager.mapMarkerManager.SpawnMapMarkerLocation(mapCenter);
        }
    }
}
