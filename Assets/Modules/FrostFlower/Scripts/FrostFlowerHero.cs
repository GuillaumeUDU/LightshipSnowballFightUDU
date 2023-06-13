using Niantic.ARVoyage.Utilities;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Niantic.ARVoyage.FrostFlower
{
    /// <summary>
    /// VFX helper class for "hero" flowers in Frost Flower.
    /// It manages particle effects based on animation events.
    /// </summary>
    public class FrostFlowerHero : MonoBehaviour
    {
        [Header("Particles")]
        [SerializeField] ParticleSystem sprayParticles;
        [SerializeField] MeshRenderer flareRenderer;

        private Color baseColor;
        private float baseAlpha;

        void Awake()
        {
            baseColor = flareRenderer.material.color;
            baseAlpha = baseColor.a;
        }

        void OnEnable()
        {
            baseColor.a = 0;
            flareRenderer.material.color = baseColor;
        }

        // Fired by animation event.
        public void TriggerParticles()
        {
            sprayParticles.Play();

            InterpolationUtil.EasedInterpolation(this, this, InterpolationUtil.EaseOutCubic, onUpdate: (t) =>
            {
                baseColor.a = t * baseAlpha;
                flareRenderer.material.color = baseColor;
            });
        }

    }
}