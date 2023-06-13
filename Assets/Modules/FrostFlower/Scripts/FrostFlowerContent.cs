using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;

using Niantic.ARVoyage.Vps;

using UnityEngine;

namespace Niantic.ARVoyage.FrostFlower
{
    /// <summary>
    /// The root of the FrostFlower content prefab that's instantiated when localized at a FrostFlower wayspot
    /// Monitors for the AR session to end or for localization to become destabilized
    /// </summary>
    public class FrostFlowerContent : MonoBehaviour
    {
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
