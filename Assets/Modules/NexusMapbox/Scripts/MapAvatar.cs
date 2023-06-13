using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Map
{
    /// <summary>
    /// Representation of user on map.
    /// </summary>    
    public class MapAvatar : MonoBehaviour
    {
        [SerializeField]
        public Transform directionIndicator;

        public void SetDirection(float direction)
        {
            directionIndicator.localRotation = Quaternion.Euler(0, 0, -direction);
        }

    }
}
