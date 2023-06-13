using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.CheapRulerCs;
using Mapbox.Directions;
using Mapbox.Unity.Location;
using Mapbox.Unity.Map;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Nexus.Map
{
    /// <summary>
    /// Manager class of the Mapbox streetmap, including:
    /// - ZoomAndPan, CenterOnLocation, RecenterOnUser
    /// - Continuously recenter on the user (auto-follow the user) if requested
    /// VPS-specific features are in the StreetMapManager class that extends this class.
    /// Some features in this class are unused in this app, including layers and routes.
    /// </summary>    
    public class MapManager : MonoBehaviour
    {
        public struct MapBoundsInfo
        {
            public Vector2d CenterLatLon;
            public double Radius;
            public Vector2dBounds Bounds;
            public float Zoom;
        }

        public delegate void BoundsChangeDelegate(MapBoundsInfo boundsInfo);

        public event Action<Location> OnLocationUpdated;

        public event BoundsChangeDelegate OnTileBoundsChanged;

        public delegate void MapItemClickDelegate(int layer, object data);

        [SerializeField]
        public Camera mainCamera;

        [SerializeField]
        public AbstractMap map;

        [SerializeField]
        protected MapItemLayer[] layers;

        [SerializeField]
        protected InputActionReference tapAction;

        [SerializeField]
        public MapMovement movement;

        [SerializeField]
        protected LocationProviderFactory locationProviderFactory;

        [SerializeField]
        protected MapAvatar mapAvatar;

        [SerializeField]
        protected MapControlPanel mapControlPanel;

        [SerializeField]
        public float initialMapZoom = 18f;

        protected float normalisedRouteProgress;

        protected double[] from = new double[2];

        protected double[] to = new double[2];

        protected int lastAbsoluteZoom = -1;

        protected bool setupDone;

        protected bool useStartingLocation;

        protected Vector2d startingLatlon;

        protected float startingZoom;

        protected bool centered;
        protected float lastCenteredTime = 0f;
        protected const float recenterPeriodSecs = 2f;
        protected float movingCameraUntilTime = 0f;

        protected LineRenderer routeLine;
        protected Route currentRoute;
        protected Vector3[] routePositions;
        protected Coroutine routeProgressCoroutine;

        private ILocationProvider locationProvider;
        public ILocationProvider LocationProvider
        {
            get
            {
                if (locationProvider == null)
                {
                    locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
                }

                return locationProvider;
            }
            set
            {
                if (locationProvider != null)
                {
                    locationProvider.OnLocationUpdated -= LocationProvider_OnLocationUpdated;

                }
                locationProvider = value;
                locationProvider.OnLocationUpdated += LocationProvider_OnLocationUpdated;
            }
        }

        public CheapRuler CheapRuler { get; private set; }

        public Vector2d MapCenterLatLon => map.CenterLatitudeLongitude;

        public float MapZoom => map.Zoom;

        public double ViewRadius { get; private set; }

        public bool MapInitialized { get; private set; }

        public bool HasLocation { get; private set; }

        public MapBoundsInfo CurrentBoundsInfo { get; private set; }

        private void Awake()
        {
            map.OnInitialized += Map_OnInitialized;
        }

        private void OnDestroy()
        {
            map.OnInitialized -= Map_OnInitialized;
            movement.OnPinchEnd -= Movement_OnPinchEnd;
            map.OnTilesStarting -= Map_OnTilesStarting;
            LocationProvider.OnLocationUpdated -= LocationProvider_OnLocationUpdated;
        }

        private void Start()
        {
            movement.OnPinchEnd += Movement_OnPinchEnd;
            map.OnTilesStarting += Map_OnTilesStarting;
            LocationProvider.OnLocationUpdated += LocationProvider_OnLocationUpdated;

            // If we already have a location.
            if (!locationProvider.CurrentLocation.LatitudeLongitude.Equals(Vector2d.zero))
            {
                LocationProvider_OnLocationUpdated(locationProvider.CurrentLocation);
            }

            map.Initialize(locationProvider.CurrentLocation.LatitudeLongitude, (int)initialMapZoom);
            map.UpdateMap(initialMapZoom);
        }

        private void Map_OnInitialized()
        {
            MapInitialized = true;

            if (HasLocation && !setupDone)
            {
                Setup();
            }
        }

        private void Setup()
        {
            if (useStartingLocation)
            {
                map.UpdateMap(startingLatlon, startingZoom);
            }
            else
            {
                map.UpdateMap(LocationProvider.CurrentLocation.LatitudeLongitude, initialMapZoom);
                SetCenteredOnUser(true);
            }

            UpdateTileBounds();

            setupDone = true;
        }

        private void Movement_OnPinchEnd()
        {
            UpdateTileBounds();
        }

        private void Map_OnTilesStarting(List<Mapbox.Map.UnwrappedTileId> startingTiles)
        {
            UpdateTileBounds();
        }

        private void UpdateTileBounds()
        {
            bool boundsCreated = false;

            Vector2dBounds bounds = new Vector2dBounds();

            foreach (var item in map.CurrentExtent)
            {
                var tileBounds = Conversions.TileBounds(item);

                if (!boundsCreated)
                {
                    bounds = Vector2dBounds.FromCoordinates(tileBounds.Min, tileBounds.Max);
                    boundsCreated = true;
                }
                else
                {
                    bounds.Extend(tileBounds.Min);
                    bounds.Extend(tileBounds.Max);
                }
            }

            var centerLatLon = Conversions.MetersToLatLon(bounds.Center);
            var northeastLatLon = Conversions.MetersToLatLon(bounds.NorthEast);

            if (CheapRuler != null)
            {
                double dist = CheapRuler.Distance(centerLatLon.ToArray(), northeastLatLon.ToArray());

                ViewRadius = dist;

                CurrentBoundsInfo = new MapBoundsInfo
                {
                    CenterLatLon = centerLatLon,
                    Radius = dist,
                    Bounds = bounds,
                    Zoom = map.Zoom,
                };

                OnTileBoundsChanged?.Invoke(CurrentBoundsInfo);
            }
        }

        private void LocationProvider_OnLocationUpdated(Location location)
        {
            HasLocation = true;

            if (CheapRuler == null)
            {
                CreateCheapRuler(location.LatitudeLongitude);
            }

            if (MapInitialized && !setupDone)
            {
                Setup();
            }

            OnLocationUpdated?.Invoke(location);

            mapAvatar.SetDirection(location.DeviceOrientation);
        }

        public void ShowMap(bool enableMovement = true, bool showAvatar = true)
        {
            map.gameObject.SetActive(true);

            mapAvatar.gameObject.SetActive(showAvatar);

            movement.enabled = enableMovement;
        }

        public void SetVisibleLayers(params string[] visibleLayers)
        {
            foreach (var layer in layers)
            {
                if (visibleLayers.Contains(layer.Name))
                {
                    layer.Show();
                }
                else
                {
                    layer.Hide();
                }
            }
        }

        public bool IsLayerVisible(string layerName)
        {
            var layer = GetLayerByName(layerName);

            return layer?.Visible ?? false;
        }

        public void HideMap()
        {
            map.gameObject.SetActive(false);

            mapAvatar.gameObject.SetActive(false);

            foreach (var layer in layers)
            {
                layer.Hide(true);
            }
        }

        public double DistanceTo(double lat, double lon)
        {
            var currentLoc = LocationProvider.CurrentLocation.LatitudeLongitude;

            from[0] = currentLoc.x;
            from[1] = currentLoc.y;

            to[0] = lat;
            to[1] = lon;

            return CheapRuler.Distance(from, to);
        }

        public void CreateCheapRuler(Vector2d latlon)
        {
            CheapRuler = new CheapRuler(latlon.x, CheapRulerUnits.Kilometers);
        }

        private MapItemLayer GetLayerByName(string name)
        {
            foreach (var layer in layers)
            {
                if (layer.Name.Equals(name))
                {
                    return layer;
                }
            }

            return null;
        }

        public void SetLayerData<T>(string name, IEnumerable<T> data)
        {
            var layer = GetLayerByName(name);

            if (layer == null)
            {
                Debug.LogError($"Can't find layer:{name}");
            }
            else
            {
                layer.SetData(data);
            }
        }

        public List<MapItem> GetLayerItems(string name)
        {
            var layer = GetLayerByName(name);

            if (layer != null)
            {
                return layer.GetItems();
            }
            else
            {
                return new List<MapItem>();
            }
        }

        protected virtual void Update()
        {
            if (!map.gameObject.activeSelf)
            {
                return;
            }

            // periodically recenter if we should remain centered, i.e. auto-follow the user
            if (centered && Time.time > lastCenteredTime + recenterPeriodSecs)
            {
                RecenterOnUser(MapZoom,
                                // but no need to refresh the map search, 
                                // since these should be small movements
                                refreshSearch: false);
            }
        }

        public void ZoomAroundPoint(Vector2d focusLatLon, float zoom, float duration = 0.5f) //, Ease ease = Ease.InOutCubic)
        {
            movement.AnimateZoomAroundPoint(focusLatLon, zoom, duration); //, ease);
        }

        public void ZoomAndPan(Vector2d targetCentreLatLon, float zoom, float duration = 0.5f) //, Ease ease = Ease.InOutCubic)
        {
            movingCameraUntilTime = Time.time + duration;
            movement.AnimateZoomAndPan(targetCentreLatLon, zoom, duration); //, ease);
        }

        private void LateUpdate()
        {
            if (!map.gameObject.activeSelf)
            {
                return;
            }

            foreach (var layer in layers)
            {
                layer.Update(map);
            }

            mapAvatar.transform.position = GetWorldPos(LocationProvider.CurrentLocation.LatitudeLongitude, 0.3f);
        }

        private Vector3 GetWorldPos(Vector2d latlon, float yPos = 0)
        {
            var pos = map.GeoToWorldPosition(latlon, false);
            pos.y = yPos;
            return pos;
        }

        public void DrawBounds(Vector2dBounds bounds, Color color, float duration = 0)
        {
            var sw = GetWorldPos(bounds.SouthWest);
            var ne = GetWorldPos(bounds.NorthEast);

            var nw = new Vector3(sw.x, 0, ne.z);
            var se = new Vector3(ne.x, 0, sw.z);

            Debug.DrawLine(sw, nw, color, duration);
            Debug.DrawLine(nw, ne, color, duration);
            Debug.DrawLine(ne, se, color, duration);
            Debug.DrawLine(se, sw, color, duration);
        }

        public float GetZoomLevelForBounds(Vector2dBounds bounds, Vector2 screenFillFactor)
        {
            Assert.IsFalse(bounds.IsEmpty(), "Bounds is empty!");

            // adjust bounds to fit screen aspect ratio
            var ne = Conversions.LatLonToMeters(bounds.NorthEast);
            var sw = Conversions.LatLonToMeters(bounds.SouthWest);

            double boundsAspect = (ne.x - sw.x) / (ne.y - sw.y);

            float screenAspect = (Screen.width * screenFillFactor.x) / (Screen.height * screenFillFactor.y);

            float screenPaddingX = screenFillFactor.x != 1 ? (1 - screenFillFactor.x) / 2f : 0;
            float screenPaddingY = screenFillFactor.y != 1 ? (1 - screenFillFactor.y) / 2f : 0;

            Vector3 screenStart;
            Vector3 screenEnd;

            Vector2d startLatLon;
            Vector2d endLatLon;

            if (boundsAspect > screenAspect)
            {
                // use width
                double lat = (bounds.NorthEast.x + bounds.SouthWest.x) * 0.5;

                startLatLon = new Vector2d(lat, bounds.West);
                endLatLon = new Vector2d(lat, bounds.East);

                screenStart = new Vector3(screenPaddingX, 0.5f);
                screenEnd = new Vector3(1f - screenPaddingX, 0.5f);
            }
            else
            {
                // use height
                double lon = (bounds.NorthEast.y + bounds.SouthWest.y) * 0.5;

                startLatLon = new Vector2d(bounds.North, lon);
                endLatLon = new Vector2d(bounds.South, lon);

                screenStart = new Vector3(0.5f, screenPaddingY);
                screenEnd = new Vector3(0.5f, 1f - screenPaddingY);
            }

            double distance = CheapRuler.Distance(startLatLon.ToArray(), endLatLon.ToArray());

            var worldStart = ViewportPointToMapWorldPoint(screenStart);
            var worldEnd = ViewportPointToMapWorldPoint(screenEnd);

            double screenSizeInKm = CheapRuler.Distance(map.WorldToGeoPosition(worldStart).ToArray(), map.WorldToGeoPosition(worldEnd).ToArray());

            double targetScale = screenSizeInKm / distance;

            double relativeZoom = Math.Log(targetScale) / Math.Log(2.0f);

            float targetZoom = (float)relativeZoom + map.Zoom;

            return Mathf.Min(movement.MaxZoom, targetZoom);
        }

        public float GetNewZoomForFrameBounds(Vector2dBounds bounds, Vector2 screenFillFactor)
        {
            float newZoom = Mathf.Min(22, GetZoomLevelForBounds(bounds, screenFillFactor));
            return newZoom;
        }

        public void AnimateToFrameBounds(Vector2dBounds bounds, Vector2 screenFillFactor, float duration = -1f)
        {
            float newZoom = GetNewZoomForFrameBounds(bounds, screenFillFactor);

            float changeInZoom = newZoom - MapZoom;

            if (duration < 0f)
            {
                duration = Mathf.Lerp(2, 5, Mathf.InverseLerp(2, 8, changeInZoom));
            }

            ZoomAndPan(bounds.Center, newZoom, duration);
        }

        public Vector3 ViewportPointToMapWorldPoint(Vector3 point)
        {
            var ray = mainCamera.ViewportPointToRay(point);

            var groundPlane = new Plane(Vector3.up, 0);

            if (!groundPlane.Raycast(ray, out float distance))
            {
                return Vector3.zero;
            }
            else
            {
                return ray.GetPoint(distance);
            }
        }

        public virtual void RecenterOnUser(float zoom, bool refreshSearch = true, float duration = 1f)
        {
            SetCenteredOnUser(true);
            ZoomAndPan(LocationProvider.CurrentLocation.LatitudeLongitude, zoom, duration);
        }

        public void CenterOnLocation(Vector2d latLon, float zoom, float duration = 1f)
        {
            SetCenteredOnUser(false);
            ZoomAndPan(latLon, zoom, duration);
        }

        public void SetCenteredOnUser(bool val)
        {
            centered = val;
            lastCenteredTime = Time.time;

            // when not centered, make recenter button interactable
            mapControlPanel.SetRecenterButtonInteractable(!val);
        }

    }
}
