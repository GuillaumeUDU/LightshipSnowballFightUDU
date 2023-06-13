using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Utilities
{
    /// <summary>
    /// Bridge class to fire an AppEvent when OnRectTransformDimensionsChange is
    /// called by Unity. Used by the DynamicCameraSizer for the VPS map.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RectTransformEventBridge : MonoBehaviour
    {
        public AppEvent DimensionsChanged;

        public RectTransform RectTransform
        {
            get { return this.transform as RectTransform; }
        }

        void Update()
        {
            if (transform.hasChanged)
            {
                DimensionsChanged?.Invoke();
                transform.hasChanged = false;
            }
        }

        void OnRectTransformDimensionsChange()
        {
            DimensionsChanged?.Invoke();
        }
    }
}
