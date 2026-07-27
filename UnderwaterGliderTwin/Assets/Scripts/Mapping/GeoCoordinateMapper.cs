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
            return MapEnuPosition(new Vector3((float)eastMeters, -frame.DepthM, (float)northMeters));
        }

        public Vector3 MapEnuPosition(Vector3 enuPositionM)
        {
            return new Vector3(enuPositionM.x * horizontalScale, enuPositionM.y * depthScale, enuPositionM.z * horizontalScale);
        }

        public Vector3 UnmapPosition(Vector3 worldPosition)
        {
            return new Vector3(
                worldPosition.x / Mathf.Max(horizontalScale, 0.000001f),
                worldPosition.y / Mathf.Max(depthScale, 0.000001f),
                worldPosition.z / Mathf.Max(horizontalScale, 0.000001f));
        }

        public float MapDepth(float depthM)
        {
            return -depthM * depthScale;
        }
    }
}
