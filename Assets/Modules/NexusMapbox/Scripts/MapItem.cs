using System.Collections;
using System.Collections.Generic;
using Mapbox.Utils;
using UnityEngine;

namespace Nexus.Map
{
    /// <summary>
    /// Unused in this app.
    /// </summary>    
    public class MapItem : MonoBehaviour
    {
        public Vector2d LatLon;

        public bool HasData => Data != null;

        public int Index;

        public virtual object Data { get; set; }

        [SerializeField]
        protected SpriteRenderer spriteRenderer;

        public void Recycle()
        {
            Data = null;
            gameObject.SetActive(false);
        }

        public virtual float GetScale(float mapScale)
        {
            return 1;
        }

        public virtual void SetAlpha(float alpha)
        {
            spriteRenderer.color = new Color(1, 1, 1, alpha);
        }
    }
}
