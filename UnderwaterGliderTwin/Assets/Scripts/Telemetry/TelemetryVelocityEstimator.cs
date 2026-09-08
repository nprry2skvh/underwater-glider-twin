using System;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class TelemetryVelocityEstimator
    {
        private const double MetersPerDegreeLatitude = 111320.0;
        public const float MaxReasonableHorizontalSpeedMetersPerSecond = 1.6f;

        public static Vector3 Estimate(TelemetryFrame previousFrame, TelemetryFrame currentFrame)
        {
            var deltaSeconds = Math.Max(0.001f, currentFrame.ElapsedSeconds - previousFrame.ElapsedSeconds);
            var averageLatitudeRad = (previousFrame.LatitudeDeg + currentFrame.LatitudeDeg) * 0.5 * Math.PI / 180.0;
            var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(averageLatitudeRad);

            var eastMeters = (currentFrame.LongitudeDeg - previousFrame.LongitudeDeg) * metersPerDegreeLongitude;
            var northMeters = (currentFrame.LatitudeDeg - previousFrame.LatitudeDeg) * MetersPerDegreeLatitude;
            var verticalMeters = -(currentFrame.DepthM - previousFrame.DepthM);

            return new Vector3(
                (float)(eastMeters / deltaSeconds),
                verticalMeters / deltaSeconds,
                (float)(northMeters / deltaSeconds));
        }

        public static Vector3 ClampHorizontalSpeed(Vector3 velocity)
        {
            var horizontal = new Vector2(velocity.x, velocity.z);
            if (horizontal.sqrMagnitude <= MaxReasonableHorizontalSpeedMetersPerSecond * MaxReasonableHorizontalSpeedMetersPerSecond)
            {
                return velocity;
            }

            var clampedHorizontal = horizontal.normalized * MaxReasonableHorizontalSpeedMetersPerSecond;
            return new Vector3(clampedHorizontal.x, velocity.y, clampedHorizontal.y);
        }
    }
}
