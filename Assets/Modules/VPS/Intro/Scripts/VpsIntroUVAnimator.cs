using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Animation utility class to animate the UVs of the 2D cloud 
    /// cards in the animated airship intro sequence.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class VpsIntroUVAnimator : MonoBehaviour
    {
        [SerializeField] float speed = 1;
        private MeshRenderer meshRenderer;
        private Vector2 mainTextureOffset = new Vector2(0, 0);

        void Start()
        {
            mainTextureOffset.x = Random.value;
            meshRenderer = GetComponent<MeshRenderer>();
        }

        void Update()
        {
            mainTextureOffset.x += Time.deltaTime * speed;
            meshRenderer.material.mainTextureOffset = mainTextureOffset;
        }
    }

}