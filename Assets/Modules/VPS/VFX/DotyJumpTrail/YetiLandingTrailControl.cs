using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Animations;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// VFX helper class for Doty in airship arrival sequences.
    /// It manages various trail and particle effects based on animation events.
    /// </summary>
    public class YetiLandingTrailControl : MonoBehaviour
    {
        [SerializeField] Transform trailTransform;
        [SerializeField] Transform targetTransform;
        [SerializeField] TrailRenderer trailRenderer;
        [SerializeField] VpsAirshipAnimator airshipAnimator;
        [SerializeField] GameObject poofLaunch;
        [SerializeField] GameObject poofImpact;

        private bool followTarget = false;

        void OnEnable()
        {
            // Disable effects.
            poofLaunch.SetActive(false);
            poofImpact.SetActive(false);
            trailTransform.gameObject.SetActive(false);

            // Enable noise animation.
            airshipAnimator.Weight = 1;
        }

        public void OnYetiLandingInLaunch()
        {
            Debug.Log("Doty Launch.");
            followTarget = true;
            trailTransform.gameObject.SetActive(true);

            poofLaunch.SetActive(true);

            InterpolationUtil.LinearInterpolation(this, this, .5f, onUpdate: (t) =>
            {
                airshipAnimator.Weight = 1 - t;
            });
        }

        public void OnYetiLandingInImpact()
        {
            Debug.Log("Doty Impact.");
            followTarget = false;

            Color trailColor = trailRenderer.startColor;
            float trailAlpha = trailColor.a;

            InterpolationUtil.LinearInterpolation(trailTransform.gameObject, trailTransform.gameObject, .25f, onUpdate: (t) =>
            {
                trailColor.a = trailAlpha * (1 - t);
                trailRenderer.startColor = trailColor;
                trailRenderer.endColor = trailColor;
            });

            poofImpact.transform.position = trailTransform.position - new Vector3(0, .5f, 0);
            poofImpact.SetActive(true);
        }

        void LateUpdate()
        {
            if (followTarget)
            {
                trailTransform.position = targetTransform.position;
            }
        }
    }
}