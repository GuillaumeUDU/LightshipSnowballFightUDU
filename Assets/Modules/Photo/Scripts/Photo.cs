using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Helper class for the individual photo objects managed by the
    /// PhotoAndShareManager. It's primary purpose is to handle the
    /// photo development effect and photo clicks by the user.
    /// </summary>
    public class Photo : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] RawImage photoImage;
        private Texture texture;

        public AppEvent onClick;

        public Texture GetPhotoTexture()
        {
            return texture;
        }

        public void SetPhotoTexture(Texture texture)
        {
            photoImage.texture = texture;
            this.texture = texture;
        }

        public void Develop()
        {
            StopAllCoroutines();
            StartCoroutine(DevelopRoutine());
        }

        IEnumerator DevelopRoutine(float duration = 1.5f)
        {
            yield return InterpolationUtil.LinearInterpolation(
                gameObject, gameObject,
                duration, 0, 0, null,
                (t) =>
                {
                    photoImage.color = new Color(1, 1, 1, t);
                    //photoImage.material.SetFloat("_Progress", t);
                }, null);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("Photo click");
            onClick?.Invoke();
        }
    }

}
