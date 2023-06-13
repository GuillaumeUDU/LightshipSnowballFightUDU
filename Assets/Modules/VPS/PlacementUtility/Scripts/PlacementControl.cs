using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Simple UI control for drag-based events. Used with PlacementUtility. 
    /// </summary>
    public class PlacementControl : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public AppEvent PlacementBegin = new AppEvent();
        public AppEvent<Vector2> PlacementUpdate = new AppEvent<Vector2>();
        public AppEvent PlacmentEnd = new AppEvent();

        public void OnBeginDrag(PointerEventData eventData)
        {
            //Debug.Log("OnBeingDrag");
            PlacementBegin.Invoke();
        }

        public void OnDrag(PointerEventData data)
        {
            Vector2 delta = data.position - data.pressPosition;
            PlacementUpdate.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData data)
        {
            //Debug.Log("OnEndDrag");
            PlacmentEnd.Invoke();
        }

    }

}
