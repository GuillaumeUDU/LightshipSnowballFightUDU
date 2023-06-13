using UnityEngine;
using Niantic.ARVoyage.Utilities;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Utility class to watch for an event from a specific RectTransform and
    /// dynamically update the map camera viewport. Used to keep the MapBox map
    /// "centered" in the viewable area above the VPS pane.
    /// </summary>
    public class DynamicCameraSizer : MonoBehaviour
    {
        [SerializeField] RectTransformEventBridge rectTransformEventBridge;
        [SerializeField] Camera targetCamera;
        [SerializeField] float padding = -4;

        private StreetMapManager streetMapManager;

        //private int originalCameraPixelHeight;
        private bool refreshDimensions = false;

        void Awake()
        {
            streetMapManager = SceneLookup.Get<StreetMapManager>();
            RefreshDimensions();
        }

        void OnEnable()
        {
            rectTransformEventBridge.DimensionsChanged.AddListener(RefreshDimensions);
        }

        void OnDestroy()
        {
            rectTransformEventBridge.DimensionsChanged.RemoveListener(RefreshDimensions);
        }

        void Update()
        {
            if (refreshDimensions)
            {
                if (targetCamera == null) return;

                // Get transform corners.
                Vector3[] worldCorners = new Vector3[4];
                rectTransformEventBridge.RectTransform.GetWorldCorners(worldCorners);

                // Normalize based on top left corner.
                float percent = 1 - ((worldCorners[1].y + padding) / Screen.height);

                // Resize viewport.
                targetCamera.rect = new Rect(0, (1 - percent), 1f, percent);

                Debug.Log("Updating camera viewport: " + targetCamera.rect + " " + Screen.height);

                // Update map.
                streetMapManager.map.UpdateMap();

                refreshDimensions = false;

            }
        }

        private void RefreshDimensions()
        {
            refreshDimensions = true;
        }
    }
}
