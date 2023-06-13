using Niantic.ARVoyage.Utilities;

using System.Collections;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Helper class used when mocking localization of content
    /// </summary>
    public class VpsMockLocalizable : MonoBehaviour
    {
        [Tooltip("When mocking localization, this transform will be placed such that it faces that camera at the camera's height.")]
        [SerializeField] public Transform mockCameraHeightStagingPoint;
    }
}