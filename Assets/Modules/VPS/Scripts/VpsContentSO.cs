using UnityEngine;
using Niantic.ARDK.VPSCoverage;
namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// ScriptableObject used for associating VPS data with content prefabs
    /// These are registered with the VpsCoverageManager
    /// </summary>
    [CreateAssetMenu(fileName = "VPSContent", menuName = "ScriptableObjects/VPSContent")]
    public class VpsContentSO : ScriptableObject
    {

        [Tooltip("The VpsDataEntry for this content")]
        public VpsDataEntry vpsDataEntry;

    }
}