using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class TelemetryKinematicsUtility
    {
        private const double MetersPerDegreeLatitude = 111320.0;

        public static bool TryGetHorizontalDisplacementMeters(TelemetryFrame startFrame, TelemetryFrame endFrame, out Vector2 displacementMeters)
        {
            displacementMeters = Vector2.zero;
            if (!TelemetryPositionUtility.HasUsableCoordinates(startFrame) || !TelemetryPositionUtility.HasUsableCoordinates(endFrame))
            {
                return false;
            }

            var averageLatitudeRad = (startFrame.LatitudeDeg + endFrame.LatitudeDeg) * 0.5 * Math.PI / 180.0;
            var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(averageLatitudeRad);
            var eastMeters = (endFrame.LongitudeDeg - startFrame.LongitudeDeg) * metersPerDegreeLongitude;
            var northMeters = (endFrame.LatitudeDeg - startFrame.LatitudeDeg) * MetersPerDegreeLatitude;
            displacementMeters = new Vector2((float)eastMeters, (float)northMeters);
            return true;
        }

        public static Vector3 EstimateWindowVelocity(IReadOnlyList<TelemetryFrame> frames, int currentIndex, int lookbackFrames = 24)
        {
            if (frames == null || frames.Count == 0)
            {
                return Vector3.zero;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, frames.Count - 1);
            var startIndex = Mathf.Max(0, currentIndex - Mathf.Max(1, lookbackFrames));
            var startFrame = frames[startIndex];
            var endFrame = frames[currentIndex];
            var deltaSeconds = Mathf.Max(0.001f, endFrame.ElapsedSeconds - startFrame.ElapsedSeconds);

            if (!TryGetHorizontalDisplacementMeters(startFrame, endFrame, out var horizontalMeters))
            {
                return Vector3.zero;
            }

            var verticalMeters = -(endFrame.DepthM - startFrame.DepthM);
            return new Vector3(horizontalMeters.x / deltaSeconds, verticalMeters / deltaSeconds, horizontalMeters.y / deltaSeconds);
        }

        public static bool TryGetAveragedCoordinate(IReadOnlyList<TelemetryFrame> frames, int centerIndex, int radius, out double longitudeDeg, out double latitudeDeg, out float depthM)
        {
            longitudeDeg = 0d;
            latitudeDeg = 0d;
            depthM = 0f;
            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            centerIndex = Mathf.Clamp(centerIndex, 0, frames.Count - 1);
            radius = Mathf.Max(0, radius);
            var startIndex = Mathf.Max(0, centerIndex - radius);
            var endIndex = Mathf.Min(frames.Count - 1, centerIndex + radius);
            var longitudeSum = 0d;
            var latitudeSum = 0d;
            var depthSum = 0f;
            var count = 0;

            for (var i = startIndex; i <= endIndex; i++)
            {
                var frame = frames[i];
                if (!TelemetryPositionUtility.HasUsableCoordinates(frame))
                {
                    continue;
                }

                longitudeSum += frame.LongitudeDeg;
                latitudeSum += frame.LatitudeDeg;
                depthSum += frame.DepthM;
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            longitudeDeg = longitudeSum / count;
            latitudeDeg = latitudeSum / count;
            depthM = depthSum / count;
            return true;
        }
    }
}
