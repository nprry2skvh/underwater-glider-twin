using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public sealed class GeoCoordinateMapper
    {
        private readonly double originLongitudeDeg;
        private readonly double originLatitudeDeg;
        private readonly float horizontalScale;
        private readonly float depthScale;

        public float HorizontalScale => horizontalScale;

        public GeoCoordinateMapper(TelemetryFrame originFrame, float horizontalScale, float depthScale)
        {
            originLongitudeDeg = originFrame.LongitudeDeg;
            originLatitudeDeg = originFrame.LatitudeDeg;
            this.horizontalScale = horizontalScale;
            this.depthScale = depthScale;
        }

        public Vector3 Map(TelemetryFrame frame)
        {
            return MapDynamicsPosition(LocalMissionCoordinateConverter.ToLocalPosition(
                frame.LongitudeDeg,
                frame.LatitudeDeg,
                frame.DepthM,
                originLongitudeDeg,
                originLatitudeDeg));
        }

        public Vector3 MapDynamicsPosition(Vector3 eastDownNorthM)
        {
            return new Vector3(
                eastDownNorthM.x * horizontalScale,
                -eastDownNorthM.y * depthScale,
                eastDownNorthM.z * horizontalScale);
        }

        public Vector3 MapDynamicsVelocity(Vector3 eastDownNorthMps)
        {
            return MapDynamicsPosition(eastDownNorthMps);
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
