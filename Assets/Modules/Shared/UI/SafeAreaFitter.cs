// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// "Safe area" refers to the subset of the screen usable for all supported device aspect ratios. 
    /// Placed alongside a canvas to properly set the scale of the safe area child transform.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public class SafeAreaFitter : MonoBehaviour
    {
        const int AssetReferenceResolutionY = 667;

        private RectTransform canvasTransform;
        private CanvasScaler canvasScaler;

        [SerializeField] private RectTransform safeAreaParentTransform;

        [SerializeField] private RectTransform layoutScaleTransform;
        [SerializeField] private bool fullScreenWidth = false;
        [SerializeField] private bool enforceSafeAreaOffset = false;

        private Rect safeArea;
        private float canvasScaleX;
        private float canvasScaleY;

        // Waits until start to allow for canvas scaling
        private void Start()
        {
            canvasTransform = transform as RectTransform;
            canvasScaler = GetComponent<CanvasScaler>();

            UpdateSafeAreaSize();

#if UNITY_EDITOR
            // Poll for updates in editor
            StartCoroutine(EditorPollRoutine());
#endif
        }

        private void UpdateSafeAreaSize()
        {
            // Get the screen safe area and canvas scales
            safeArea = Screen.safeArea;
            canvasScaleX = canvasTransform.localScale.x;
            canvasScaleY = canvasTransform.localScale.y;

            // Get our reference resolution from the scaler
            Vector2 referenceResolution = canvasScaler.referenceResolution;

            // Scale the safe area to fit to the screen safe height, compensating for any canvas scaling
            float safeAreaX = safeArea.width / canvasScaleX;
            float safeAreaY = safeArea.height / canvasScaleY;

            // Determine the scale to match the y reference resolution. this will be applied to x and y to keep uniform scale
            float scaleToMatchReferenceResolutionY = safeAreaY / referenceResolution.y;
            safeAreaParentTransform.localScale = new Vector3(scaleToMatchReferenceResolutionY, scaleToMatchReferenceResolutionY, 1);

            if (enforceSafeAreaOffset)
            {
                // The safe area is a cropped portion of the overall screen. Apply its offset, scaled into canvas space.
                // For this to work properly, the SafeAreaParentTransform should be anchored bottom/left.
                safeAreaParentTransform.anchoredPosition = new Vector3(safeArea.x / (float)canvasScaleX, safeArea.y / (float)canvasScaleY);
            }

            // Set the size of the safe area parent
            if (!fullScreenWidth)
            {
                // Determine the x value to use to compensate for this scaling
                float scaledSafeAreaX = safeAreaX / scaleToMatchReferenceResolutionY;
                safeAreaParentTransform.sizeDelta = new Vector2(scaledSafeAreaX, referenceResolution.y);
            }
            else
            {
                // Aspect ratio of the cropped safe area.
                float aspectRatio = safeArea.width / (float)safeArea.height;

                // Enforce aspect ratio on SafeAreaParent.
                safeAreaParentTransform.sizeDelta = new Vector2(referenceResolution.y * aspectRatio, referenceResolution.y);

                // Update scale and size delta for LayoutScaleTransform based on asset resolution.
                float scaleToMatchAssetResolutionY = referenceResolution.y / (float)AssetReferenceResolutionY;
                layoutScaleTransform.localScale = new Vector3(scaleToMatchAssetResolutionY, scaleToMatchAssetResolutionY, 1);
                layoutScaleTransform.sizeDelta = new Vector2(AssetReferenceResolutionY * aspectRatio, AssetReferenceResolutionY);
            }

            Debug.LogFormat("UpdateSafeAreaSize [screen size {0}] [canvas size {1}] [canvas scale {2}] [total safe area {3}] [scaled safe area {4}]",
                Screen.width + " x " + Screen.height,
                canvasTransform.sizeDelta,
                canvasScaleX + ", " + canvasScaleY,
                safeArea.width + " x " + safeArea.height,
                safeAreaParentTransform.sizeDelta);
        }

#if UNITY_EDITOR
        // In editor, poll for new safe area size triggered by switching devices
        private IEnumerator EditorPollRoutine()
        {
            WaitForSeconds pollingWait = new WaitForSeconds(.5f);

            while (true)
            {
                if (Screen.safeArea != safeArea ||
                    canvasScaleX != canvasTransform.localScale.x ||
                    canvasScaleY != canvasTransform.localScale.y)
                {
                    UpdateSafeAreaSize();
                }
                yield return pollingWait;
            }
        }
#endif
    }
}
