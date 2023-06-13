using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Niantic.ARVoyage.Utilities
{
    /// <summary>
    /// Geography-based utilities, primarily related to creating and
    /// managing geohashes. Also includes utility functions for
    /// geographic distances.
    /// </summary>
    public class Geography
    {
        public readonly static char[] Base32 = new char[] {
            '0','1','2','3','4','5','6','7','8','9','b','c','d','e','f','g',
            'h','j','k','m','n','p','q','r','s','t','u','v','w','x','y','z'
        };

        // https://gist.github.com/KamChanLiu/16e27ebd77586c236289
        public static string GetGeohash(float latitude, float longitude, int precision = 12)
        {
            if (precision < 1 || precision > 20) precision = 12;

            string geohash = string.Empty;
            int bits = 0, totalBits = 0, hashValue = 0;
            float maxLat = 90, minLat = -90, maxLon = 180, minLon = -180;

            while (geohash.Length < precision)
            {
                float mid;
                if (totalBits % 2 == 0)
                {
                    mid = (maxLon + minLon) / 2;

                    if (longitude > mid)
                    {
                        hashValue = (hashValue << 1) + 1;
                        minLon = mid;
                    }
                    else
                    {
                        hashValue = (hashValue << 1) + 0;
                        maxLon = mid;
                    }
                }
                else
                {
                    mid = (maxLat + minLat) / 2;
                    if (latitude > mid)
                    {
                        hashValue = (hashValue << 1) + 1;
                        minLat = mid;
                    }
                    else
                    {
                        hashValue = (hashValue << 1) + 0;
                        maxLat = mid;
                    }
                }

                bits++;
                totalBits++;

                if (bits == 5)
                {
                    var code = Base32[hashValue];
                    geohash += code;
                    bits = 0;
                    hashValue = 0;
                }
            }

            return geohash;
        }

        public static float GetDistanceBetweenLatLongs(float latitudeA, float longitudeA, float latitudeB, float longitudeB, Mapbox.CheapRulerCs.CheapRulerUnits units = Mapbox.CheapRulerCs.CheapRulerUnits.Meters)
        {
            return GetDistanceBetweenLatLongs(
                new Mapbox.Utils.Vector2d(latitudeA, longitudeA),
                new Mapbox.Utils.Vector2d(latitudeB, longitudeB),
                units
            );
        }

        public static float GetDistanceBetweenLatLongs(Mapbox.Utils.Vector2d latLong1, Mapbox.Utils.Vector2d latLong2, Mapbox.CheapRulerCs.CheapRulerUnits units = Mapbox.CheapRulerCs.CheapRulerUnits.Meters)
        {
            Mapbox.CheapRulerCs.CheapRuler cheapRuler = new Mapbox.CheapRulerCs.CheapRuler(latLong1.x, units);
            double distance = cheapRuler.Distance(
                new double[] { latLong1.y, latLong1.x },
                new double[] { latLong2.y, latLong2.x }
            );
            return (float)distance;
        }

    }
}