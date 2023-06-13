// Copyright 2022 Niantic, Inc. All Rights Reserved.
using UnityEngine;

namespace Niantic.ARVoyage.Walkabout
{
    /// <summary>
    /// For placing footprint decals for Doty
    /// </summary>
    public class DecalStamper : MonoBehaviour
    {
        [SerializeField] Transform leftFootTransform;
        [SerializeField] Transform rightFootTransform;
        [SerializeField] GameObject prefab;

        private int layerMask;
        private RaycastHit[] results = new RaycastHit[4];

        private void Awake()
        {
            layerMask = LayerMask.GetMask("AR Mesh");
        }

        public void LeftStamp(AnimationEvent animationEvent)
        {
            //Debug.Log("Left");
            Vector3 position = leftFootTransform.position;
            Vector3 scale = new Vector3(-1, 1, 1);
            Stamp(position, scale);
        }

        public void RightStamp(AnimationEvent animationEvent)
        {
            //Debug.Log("Right");
            Vector3 position = rightFootTransform.position;
            Vector3 scale = Vector3.one;
            Stamp(position, scale);
        }

        void Stamp(Vector3 position, Vector3 scale)
        {
            Quaternion rotation = Quaternion.LookRotation(transform.forward);

            // Adjust height to be off surface.
            position.y = transform.position.y + .01f;

            // Raycast down to look for tiles.
            int hitCount = Physics.RaycastNonAlloc(position, -Vector3.up, results, .25f,
                                                   layerMask, QueryTriggerInteraction.UseGlobal);
            if (hitCount > 0)
            {
                //Vector3 decalPosition = results[0].point;
                Vector3 decalPosition = position;

                GameObject decalInstance = Instantiate(prefab, decalPosition, rotation);
                decalInstance.transform.localScale = scale;
            }

            // Fire footstep event.
            WalkaboutActor.EventFootstep.Invoke();
        }

        public void Loop()
        {
            //NOP, needed for looping clip.
        }
    }
}