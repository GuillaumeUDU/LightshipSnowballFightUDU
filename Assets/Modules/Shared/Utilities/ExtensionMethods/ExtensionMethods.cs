using Mapbox.Utils;
using Niantic.ARDK.LocationService;
public static class ExtensionMethods
{
    // Converts an ARDK LatLng into a MapBox Vector2d.
    public static Vector2d ToVector2d(this LatLng coordinates)
    {
        return new Vector2d(coordinates.Latitude, coordinates.Longitude);
    }

    // Converts a MapBox Vector2d into an ARDK LatLng.
    public static LatLng ToLatLng(this Vector2d coordinates)
    {
        return new LatLng(coordinates.x, coordinates.y);
    }
}