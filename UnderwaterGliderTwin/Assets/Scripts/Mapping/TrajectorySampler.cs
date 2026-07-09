using System;
using System.Collections.Generic;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public static class TrajectorySampler
    {
        public static Vector3[] Sample(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, int maxPoints)
        {
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<Vector3>();
            }

            var count = Math.Min(maxPoints, frames.Count);
            var points = new Vector3[count];
            var step = frames.Count <= 1 ? 1f : (frames.Count - 1f) / Math.Max(1, count - 1);

            for (var i = 0; i < count; i++)
            {
                var sourceIndex = Mathf.Clamp(Mathf.RoundToInt(i * step), 0, frames.Count - 1);
                points[i] = mapper.Map(frames[sourceIndex]);
            }

            return points;
        }
    }
}
