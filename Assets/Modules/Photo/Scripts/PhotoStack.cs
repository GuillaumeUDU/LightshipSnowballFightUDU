using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Helper class for managing the stack of instant photos taken
    /// during bespoke experiences and the single photo taken during
    /// frost flower. Primarily responsible for handling the stack
    /// shuffling animation when multiple photos are present.
    /// </summary>
    public class PhotoStack : MonoBehaviour
    {
        [SerializeField] public Photo photoPrefab;
        [SerializeField] public List<Photo> photos = new List<Photo>();

        [SerializeField] Transform photoContainer;

        [SerializeField] public GameObject gui;
        [SerializeField] public Button xCloseButton;

        private bool animating = false;

        //int foregroundIndex = 0;

        private AudioManager audioManager;

        public void Awake()
        {
            audioManager = SceneLookup.Get<AudioManager>();
        }

        public void AddPhoto(Texture photoTexture)
        {
            Debug.Log("PhotoStack: AddPhoto.");

            Photo photo = Instantiate(photoPrefab, photoContainer);
            RectTransform rectTransform = photo.GetComponentInChildren<RectTransform>();
            //Canvas canvas = photo.GetComponentInChildren<Canvas>();

            // Positionally stack the photo
            int xOffset = Random.Range(-32, 32);
            int yOffset = Random.Range(-32, 32);
            //rectTransform.localPosition = new Vector3(xOffset + (-1080 / 2), yOffset + (-1920 / 2), 0);
            rectTransform.anchoredPosition = new Vector2(xOffset, yOffset);

            // Don't rotate the first photo.
            if (photos.Count > 0) rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(-8, 8));

            photo.gameObject.SetActive(false);
            photo.SetPhotoTexture(photoTexture);
            photos.Add(photo);

            photo.onClick.AddListener(() =>
            {
                StartCoroutine(AnimateNextPhotoRoutine());
            });
        }

        public void ClearStack()
        {
            foreach (Photo photo in photos)
            {
                Destroy(photo.gameObject);
            }
            photos.Clear();
        }

        public void DisplayPhotos(bool val)
        {
            Debug.Log("PhotoStack: DisplayPhotos: " + photos.Count);

            // Activate photos.
            foreach (Photo photo in photos)
            {
                photo.gameObject.SetActive(val);
            }

            xCloseButton.gameObject.SetActive(val);
            if (val)
            {
                // Develop top photo.
                photos[photos.Count - 1].Develop();

                audioManager.PlayAudioNonSpatial(AudioKeys.SFX_General_Polaroid_Extrusion);

                xCloseButton.gameObject.transform.localScale = new Vector3(3.5f, 3.5f, 3.5f);
            }
        }

        public Photo GetTopStackPhoto()
        {
            return photoContainer.GetChild(photoContainer.childCount - 1).GetComponentInChildren<Photo>();
        }

        private IEnumerator AnimateNextPhotoRoutine()
        {
            // Bail if we're already animating or if there is only one photo
            if (animating || photos.Count <= 1) yield break;

            animating = true;

            RectTransform topPhoto = (RectTransform)photoContainer.GetChild(photoContainer.childCount - 1);
            Vector2 startPosition = topPhoto.anchoredPosition;
            Vector2 offsetPosition = startPosition + new Vector2(Screen.width, 0);

            Quaternion startRotation = topPhoto.rotation;
            Quaternion newRotation = Quaternion.Euler(0, 0, Random.Range(-8, 8));

            yield return InterpolationUtil.EasedInterpolation(topPhoto, topPhoto, InterpolationUtil.EaseInOutCubic, .35f, onUpdate: (t) =>
            {
                topPhoto.anchoredPosition = Vector3.Lerp(startPosition, offsetPosition, t);
                topPhoto.rotation = Quaternion.Lerp(startRotation, newRotation, t);
            });

            photoContainer.GetChild(photoContainer.childCount - 1).SetSiblingIndex(0);

            yield return InterpolationUtil.EasedInterpolation(topPhoto, topPhoto, InterpolationUtil.EaseInOutCubic, .35f, onUpdate: (t) =>
            {
                topPhoto.anchoredPosition = Vector3.Lerp(offsetPosition, startPosition, t);
            });

            animating = false;
        }

    }
}
