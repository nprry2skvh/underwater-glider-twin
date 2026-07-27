using System.Collections.Generic;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class TelemetryPositionUtility
    {
        public static bool HasUsableCoordinates(TelemetryFrame frame)
        {
            return !double.IsNaN(frame.LongitudeDeg)
                && !double.IsInfinity(frame.LongitudeDeg)
                && !double.IsNaN(frame.LatitudeDeg)
                && !double.IsInfinity(frame.LatitudeDeg)
                && frame.LongitudeDeg >= -180.0
                && frame.LongitudeDeg <= 180.0
                && frame.LatitudeDeg >= -90.0
                && frame.LatitudeDeg <= 90.0
                && (System.Math.Abs(frame.LongitudeDeg) > 0.000001 || System.Math.Abs(frame.LatitudeDeg) > 0.000001);
        }

        public static int FindFirstUsableCoordinateIndex(IReadOnlyList<TelemetryFrame> frames)
        {
            if (frames == null)
            {
                return -1;
            }

            for (var index = 0; index < frames.Count; index++)
            {
                if (HasUsableCoordinates(frames[index]))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
