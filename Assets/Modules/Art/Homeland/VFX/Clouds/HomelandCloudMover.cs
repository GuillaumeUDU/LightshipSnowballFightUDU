// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage.Homeland
{
    /// <summary>
    /// Manages animating clouds across the homeland scene
    /// </summary>
    public class HomelandCloudMover : MonoBehaviour
    {
        [SerializeField] Transform offsetTransform;
        [SerializeField] float duration = 10;

        private Vector3 startPosition;
        private Vector3 targetPosition;

        void Start()
        {
            startPosition = transform.localPosition;
            targetPosition = -offsetTransform.localPosition;
        }

        void Update()
        {
            float t = (Time.time % duration) / duration;
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
        }

    }
}