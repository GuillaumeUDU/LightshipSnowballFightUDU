using UnityEngine;

namespace Nexus.Map
{
    /// <summary>
    /// Representation of user on map. Used in conjunction with the core MapAvatar functionality.
    /// </summary>    
    public class MapMarkerAvatar : MapMarker
    {
        public void HandleUserClicked()
        {
            Debug.Log("HandleUserClicked");
        }
    }
}
