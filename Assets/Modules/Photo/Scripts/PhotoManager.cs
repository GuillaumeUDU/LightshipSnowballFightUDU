using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Manager class for handling various tasks related to taking photos
    /// of Doty and Frost Flowers in VPS experiences. It handles both the
    /// visual/audio effects and the underlying compositing and saving
    /// of the instant photos. 
    /// </summary>
    public class PhotoManager : MonoBehaviour, ISceneDependency
    {
        private const string shareSubjectText = "Captain Doty at a POI!";
        private const string shareBodyText = "Captain Doty at a POI!";
        private const string shareUrl = "https://lightship.dev";

        [SerializeField] public PhotoStack photoStack;

        [Header("Viewfinder")]
        [SerializeField] Canvas viewfinderCanvas;

        [Header("Overlay Canvas")]
        [SerializeField] Canvas overlayCanvas;
        [SerializeField] Camera overlayCamera;
        [SerializeField] TMPro.TMP_Text overlayCaption;
        [SerializeField] RawImage overlayRawImage;

        [Header("Viewfinder")]
        [SerializeField] Image flashImage;

        [Header("Debug")]
        [SerializeField] RenderTexture renderTexture;
        [SerializeField] public RenderTexture photoTexture;
        [SerializeField] Texture2D savePhotoTexture;

        // private float targetAspectRatio = 9f / 16f;
        private int renderTextureWidth, renderTextureHeight;
        private int maxRenderTextureHeight = 1080;
        //        private int textureWidth, textureHeight;

        int photoTextureWidth;
        int photoTextureHeight;

        private List<Texture> photoTextures = new List<Texture>();

        private AudioManager audioManager;

        public void Awake()
        {
            audioManager = SceneLookup.Get<AudioManager>();
        }

        public void Start()
        {
            // Disable photo canvas.
            overlayCanvas.gameObject.SetActive(false);

            // Prepare intermediate render texture.
            renderTextureHeight = maxRenderTextureHeight;
            renderTextureWidth = Mathf.CeilToInt(maxRenderTextureHeight * (Screen.width / (float)Screen.height));
            renderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 24, RenderTextureFormat.ARGB32, 0);

            photoTextureWidth = overlayCamera.targetTexture.width;
            photoTextureHeight = overlayCamera.targetTexture.height;

            savePhotoTexture = new Texture2D(photoTextureWidth, photoTextureHeight);
        }

        public void OnDestroy()
        {
            UnityEngine.Rendering.AsyncGPUReadback.WaitAllRequests();

            Destroy(renderTexture);
            Destroy(savePhotoTexture);
            foreach (Texture photoTexture in photoTextures) Destroy(photoTexture);
        }

        public void SetCaption(string caption)
        {
            overlayCaption.text = caption;
        }

        public void TakePhoto(System.Action<Texture> callback = null)
        {
            StartCoroutine(TakePhotoRoutine(callback));
        }

        public IEnumerator TakePhotoRoutine(System.Action<Texture> callback)
        {
            audioManager.PlayAudioNonSpatial(AudioKeys.SFX_General_TakePicture);

            // Animate flash up.
            {
                Color flashColor = flashImage.color;
                flashColor.a = 0;
                flashImage.gameObject.SetActive(true);

                yield return InterpolationUtil.LinearInterpolation(this, this, .05f,
                onUpdate: (t) =>
                {
                    flashColor.a = t;
                    flashImage.color = flashColor;
                });
            }

            // Wait to end of frame.
            yield return new WaitForEndOfFrame();

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            // Camera to intermediate render texture.
            Camera.main.targetTexture = renderTexture;
            Camera.main.Render();
            Camera.main.targetTexture = null;

            stopwatch.Stop();
            Debug.Log("TakePhotoRoutine: Stopwatch: Camera to intermediate render texture: " + stopwatch.ElapsedMilliseconds);

            // Wait a frame after main camera render.
            yield return null;

            // Add and scale image in photo canvas.
            {
                float sourceAspectRatio = renderTexture.width / (float)renderTexture.height;
                float destinationAspectRatio = overlayRawImage.rectTransform.sizeDelta.x / (float)overlayRawImage.rectTransform.sizeDelta.y;

                if (sourceAspectRatio < destinationAspectRatio)
                {
                    // Width is point of truth.
                    float height = sourceAspectRatio / destinationAspectRatio;
                    overlayRawImage.uvRect = new Rect(0, (1 - height) / 2f, 1, height);
                }
                else
                {
                    // Height is point of truth.
                    float width = destinationAspectRatio / sourceAspectRatio;
                    overlayRawImage.uvRect = new Rect((1 - width) / 2f, 0, width, 1);
                }

                Debug.Log($"Source Aspect Ratio: {sourceAspectRatio}, Destination Aspect Ratio: {destinationAspectRatio}");
                overlayRawImage.texture = renderTexture;
            }

            stopwatch.Reset();
            stopwatch.Start();

            // Render overlay canvas.
            overlayCanvas.gameObject.SetActive(true);
            overlayCamera.Render();
            overlayCanvas.gameObject.SetActive(false);

            stopwatch.Stop();
            Debug.Log("TakePhotoRoutine: Stopwatch: Render overlay canvas: " + stopwatch.ElapsedMilliseconds);

            // Wait a frame after overlay camera render.
            yield return null;

            // Create photo render texture
            photoTexture = new RenderTexture(photoTextureWidth, photoTextureHeight, overlayCamera.targetTexture.depth, overlayCamera.targetTexture.format);

            stopwatch.Reset();
            stopwatch.Start();

            // Render texture to texture.
            Graphics.Blit(overlayCamera.targetTexture, photoTexture);

            stopwatch.Stop();
            Debug.Log("TakePhotoRoutine: Stopwatch: Rendertexture to texture: " + stopwatch.ElapsedMilliseconds);

            // Wait a frame after grabbing texture from GPU.
            yield return null;

            stopwatch.Reset();
            stopwatch.Start();

            // Get pixels to save
            RenderTexture.active = overlayCamera.targetTexture;
            savePhotoTexture.ReadPixels(new Rect(0, 0, photoTextureWidth, photoTextureHeight), 0, 0);
            savePhotoTexture.Apply();
            RenderTexture.active = null;

            stopwatch.Stop();
            Debug.Log("TakePhotoRoutine: Stopwatch: Get texture pixels: " + stopwatch.ElapsedMilliseconds);

            // Wait a frame after grabbing photo pixels
            yield return null;

            stopwatch.Reset();
            stopwatch.Start();

            byte[] bytes = savePhotoTexture.EncodeToPNG();

            stopwatch.Stop();
            Debug.Log("TakePhotoRoutine: Stopwatch: Encode to PNG: " + stopwatch.ElapsedMilliseconds);

            // Wait a frame after encoding photo.
            yield return null;

            stopwatch.Reset();
            stopwatch.Start();

            NativeGallery.Permission permission = NativeGallery.SaveImageToGallery(bytes, "ARVoyage", "Doty.png", (success, path) => Debug.Log("Media save result: " + success + " " + path));

            stopwatch.Stop();
            Debug.Log("TakePhotoRoutine: Stopwatch: Save to gallery: " + stopwatch.ElapsedMilliseconds);

            // Wait a frame after saving photo.
            yield return null;

            // Add to list to be destroyed later.
            photoTextures.Add(photoTexture);

            // Add to photo stack (cloning no longer needed).
            if (photoStack != null) photoStack.AddPhoto(photoTexture);

            // Animate flash down.
            {
                Color flashColor = flashImage.color;

                yield return InterpolationUtil.LinearInterpolation(this, this, .45f,
                onUpdate: (t) =>
                {
                    flashColor.a = 1 - t;
                    flashImage.color = flashColor;
                });

                flashImage.gameObject.SetActive(false);
            }

            // Trigger callback.
            if (callback != null) callback(photoTexture);

            audioManager.PlayAudioNonSpatial(AudioKeys.SFX_General_Picture_Wind);
        }

        public void ShowViewFinder(bool show)
        {
            viewfinderCanvas.gameObject.SetActive(show);
        }

    }
}
