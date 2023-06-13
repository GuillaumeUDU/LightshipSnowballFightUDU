using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// VFX and animation helper class for the standing point indicator in
    /// "bespoke" VPS scenes. It handles both the blue animated footprints
    /// and the 3D purple arrow that points toward them.
    /// </summary>
    public class WorldStandingPointIndicator : MonoBehaviour
    {
        public AppEvent ReachedStandingPoint = new AppEvent();

        [SerializeField] MeshRenderer[] fadeMeshRenderers;

        [SerializeField] Transform bubbleScaleRoot;
        [SerializeField] Transform animationRoot;
        [SerializeField] Transform arrowRoot;

        public float FadeDistance { get; set; } = 3f;
        public float SuccessDistance { get; set; } = .7f;
        public float SuccessDot { get; set; } = -.5f;

        public float ArrowHideDistance { get; set; } = 2f;
        public float ArrowShowDistance { get; set; } = 3f;


        public float CurrentDistance { get; private set; } = 0;
        public float CurrentDot { get; private set; } = 0;

        private Vector3 zeroY = new Vector3(1, 0, 1);
        private bool reachedStandingPointTriggered = false;
        private bool arrowHideTriggered = false;

        void Awake()
        {
            animationRoot.gameObject.SetActive(false);
            arrowRoot.gameObject.SetActive(false);
        }

        public void AnimateIn()
        {
            // Reset indicator.
            BubbleScaleUtil.StopRunningScale(bubbleScaleRoot.gameObject);
            bubbleScaleRoot.localScale = Vector3.one;

            // Reset and show arrow.
            arrowRoot.localScale = Vector3.zero;
            ShowArrow();

            // Reset state.
            arrowHideTriggered = false;
            reachedStandingPointTriggered = false;

            animationRoot.gameObject.SetActive(true);
            arrowRoot.gameObject.SetActive(true);
        }

        public void AnimateOut()
        {
            reachedStandingPointTriggered = true;
            BubbleScaleUtil.ScaleDown(bubbleScaleRoot.gameObject, 0, 1, onComplete: () =>
            {
                Hide();
            });
        }

        public void Hide()
        {
            animationRoot.gameObject.SetActive(false);
            arrowRoot.gameObject.SetActive(false);
        }

        public void ShowArrow()
        {
            arrowHideTriggered = false;
            arrowRoot.gameObject.SetActive(true);
            BubbleScaleUtil.StopRunningScale(arrowRoot.gameObject);
            BubbleScaleUtil.ScaleUp(arrowRoot.gameObject, 1, 1);
        }

        public void HideArrow()
        {
            arrowHideTriggered = true;
            BubbleScaleUtil.StopRunningScale(arrowRoot.gameObject);
            BubbleScaleUtil.ScaleDown(arrowRoot.gameObject, 0, .5f, onComplete: () =>
            {
                arrowRoot.gameObject.SetActive(false);
            });
        }

        void Update()
        {
            // Check position.
            if (Camera.main == null) return;
            Transform cameraTransform = Camera.main.transform;

            Vector3 indicatorZeroY = Vector3.Scale(transform.position, zeroY);
            Vector3 cameraZeroY = Vector3.Scale(cameraTransform.position, zeroY);
            Vector3 arrowZeroY = Vector3.Scale(arrowRoot.position, zeroY);

            Vector3 indicatorForwadZeroY = Vector3.Scale(transform.forward, zeroY);
            Vector3 cameraForwardZeroY = Vector3.Scale(cameraTransform.forward, zeroY);

            CurrentDistance = Vector3.Distance(indicatorZeroY, cameraZeroY);
            CurrentDot = Vector3.Dot(indicatorForwadZeroY, cameraForwardZeroY);

            // Fade mesh renderers.
            foreach (MeshRenderer meshRenderer in fadeMeshRenderers)
            {
                float alpha = Mathf.Clamp01(CurrentDistance / FadeDistance);
                meshRenderer.material.SetFloat("_MasterAlpha", alpha);
            }

            // Check angle and distance.
            if (!reachedStandingPointTriggered && CurrentDistance < SuccessDistance && CurrentDot < SuccessDot)
            {
                Debug.Log("Standing point success.");
                ReachedStandingPoint?.Invoke();
                AnimateOut();
            }

            // Handle arrow.
            float arrowDistance = Vector3.Distance(indicatorZeroY, arrowZeroY);

            // Position arrow.
            arrowRoot.transform.position = cameraTransform.position + cameraTransform.forward + (-Vector3.up * .30f);

            // Aim arrow.
            Vector3 lookPosition = transform.position;
            Vector3 lookPositionRaised = lookPosition;
            lookPositionRaised.y = arrowRoot.position.y;
            float progress = Mathf.Clamp01(arrowDistance / 2f);
            arrowRoot.LookAt(Vector3.Lerp(lookPositionRaised, lookPosition, 1 - progress));

            if (!arrowHideTriggered && CurrentDistance < ArrowHideDistance)
            {
                Debug.Log("Hide arrow.");
                HideArrow();
            }

            if (arrowHideTriggered && !reachedStandingPointTriggered && CurrentDistance > ArrowShowDistance)
            {
                Debug.Log("Show arrow.");
                ShowArrow();
            }
        }

    }
}

