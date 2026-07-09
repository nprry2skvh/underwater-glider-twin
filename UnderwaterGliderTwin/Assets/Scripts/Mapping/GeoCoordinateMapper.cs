using System;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public sealed class GeoCoordinateMapper
    {
        private const double MetersPerDegreeLatitude = 111320.0;
        private readonly double originLongitudeDeg;
        private readonly double originLatitudeDeg;
        private readonly double metersPerDegreeLongitude;
        private readonly float horizontalScale;
        private readonly float depthScale;

        public GeoCoordinateMapper(TelemetryFrame originFrame, float horizontalScale, float depthScale)
        {
            originLongitudeDeg = originFrame.LongitudeDeg;
            originLatitudeDeg = originFrame.LatitudeDeg;
            metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(originLatitudeDeg * Math.PI / 180.0);
            this.horizontalScale = horizontalScale;
            this.depthScale = depthScale;
        }

        public Vector3 Map(TelemetryFrame frame)
        {
            var eastMeters = (frame.LongitudeDeg - originLongitudeDeg) * metersPerDegreeLongitude;
            var northMeters = (frame.LatitudeDeg - originLatitudeDeg) * MetersPerDegreeLatitude;
            return new Vector3((float)eastMeters * horizontalScale, -frame.DepthM * depthScale, (float)northMeters * horizontalScale);
        }
    }
}
