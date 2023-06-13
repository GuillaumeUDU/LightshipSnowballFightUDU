using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Animation helper class to add positional noise in airship sequences.
    /// </summary>
    public class VpsAirshipAnimator : MonoBehaviour
    {
        [SerializeField] Transform airshipNoiseRoot;

        public float Weight { get; set; } = 1;

        [SerializeField] float noiseMagnitude = default;
        [SerializeField] float noiseSpeed = default;

        [SerializeField] float sinMagnitude = default;
        [SerializeField] float sinSpeed = default;

        void LateUpdate()
        {
            airshipNoiseRoot.localPosition =
                (Vector3.up * (Mathf.PerlinNoise(Time.time * noiseSpeed, Time.time * noiseSpeed) - .5f) * noiseMagnitude * Weight) +
                (Vector3.up * Mathf.Sin(Time.time * sinSpeed) * sinMagnitude * Weight);
        }
    }
}
