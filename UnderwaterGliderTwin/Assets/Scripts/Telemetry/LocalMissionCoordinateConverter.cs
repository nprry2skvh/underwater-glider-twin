using System;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class LocalMissionCoordinateConverter
    {
        private const double MetersPerDegreeLatitude = 111320d;

        public static Vector3 ToLocalPosition(
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            double originLongitudeDeg,
            double originLatitudeDeg)
        {
            var metersPerDegreeLongitude = LongitudeScaleAt(originLatitudeDeg);
            var eastM = Math.Abs(metersPerDegreeLongitude) > 0.001d
                ? (longitudeDeg - originLongitudeDeg) * metersPerDegreeLongitude
                : 0d;
            var northM = (latitudeDeg - originLatitudeDeg) * MetersPerDegreeLatitude;
            return new Vector3((float)eastM, depthM, (float)northM);
        }

        public static void ToGeodetic(
            Vector3 eastDownNorthM,
            double originLongitudeDeg,
            double originLatitudeDeg,
            out double longitudeDeg,
            out double latitudeDeg)
        {
            var metersPerDegreeLongitude = LongitudeScaleAt(originLatitudeDeg);
            longitudeDeg = Math.Abs(metersPerDegreeLongitude) > 0.001d
                ? originLongitudeDeg + eastDownNorthM.x / metersPerDegreeLongitude
                : originLongitudeDeg;
            latitudeDeg = originLatitudeDeg + eastDownNorthM.z / MetersPerDegreeLatitude;
        }

        private static double LongitudeScaleAt(double originLatitudeDeg)
        {
            return MetersPerDegreeLatitude * Math.Cos(originLatitudeDeg * Math.PI / 180d);
        }
    }
}
