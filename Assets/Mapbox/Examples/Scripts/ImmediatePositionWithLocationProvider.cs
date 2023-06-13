namespace Mapbox.Examples
{
    using Mapbox.Utils;
    using Mapbox.Unity.Location;
    using Mapbox.Unity.Map;
    using UnityEngine;

    public class ImmediatePositionWithLocationProvider : MonoBehaviour
    {

        bool _isInitialized;

        ILocationProvider _locationProvider;
        ILocationProvider LocationProvider
        {
            get
            {
                if (_locationProvider == null)
                {
                    RefreshLocationProvider();
                }

                return _locationProvider;
            }
        }

        Vector3 _targetPosition;

        void Start()
        {
            LocationProviderFactory.Instance.mapManager.OnInitialized += () => _isInitialized = true;
        }

        void LateUpdate()
        {
            if (_isInitialized)
            {
                var map = LocationProviderFactory.Instance.mapManager;
                transform.localPosition = map.GeoToWorldPosition(LocationProvider.CurrentLocation.LatitudeLongitude);
            }
        }

        public Vector2d GetLocation()
        {
            return LocationProvider.CurrentLocation.LatitudeLongitude;
        }

        public void RefreshLocationProvider()
        {
            _locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
        }
    }
}