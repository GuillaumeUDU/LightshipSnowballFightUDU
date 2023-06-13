using Niantic.ARVoyage.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// UI displaying a hint image for this location of what to point the device at to localize.
    /// </summary>
    public class VpsLocalizationPanel : MonoBehaviour
    {
        public TMPro.TMP_Text titleText;
        public TMPro.TMP_Text subTitleText;
        public TMPro.TMP_Text bodyText;
        public RawImage hintImage;

    }
}