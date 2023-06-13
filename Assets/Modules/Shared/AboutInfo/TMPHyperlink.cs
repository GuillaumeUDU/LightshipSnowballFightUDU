// Copyright 2022 Niantic, Inc. All Rights Reserved.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Functionality for including hyperlinks in the text for the homeland settings UI
    /// </summary>
    public class TMPHyperlink : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMPro.TMP_Text tmpText;

        public void OnPointerClick(PointerEventData eventData)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmpText, eventData.position, null);
            if (linkIndex != -1)
            {
                TMP_LinkInfo tmpLinkInfo = tmpText.textInfo.linkInfo[linkIndex];
                Application.OpenURL(tmpLinkInfo.GetLinkID());
            }
        }
    }
}
