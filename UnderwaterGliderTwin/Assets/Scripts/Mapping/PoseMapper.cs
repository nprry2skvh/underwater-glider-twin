using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public static class PoseMapper
    {
        public static Quaternion ToRotation(TelemetryFrame frame)
        {
            return Quaternion.Euler(frame.PitchDeg, frame.HeadingDeg, -frame.RollDeg);
        }
    }
}
