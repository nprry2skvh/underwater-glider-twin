using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public static class PoseMapper
    {
        public static Quaternion ToRotation(TelemetryFrame frame)
        {
            return ToRotation(frame, AttitudeSettings.Default);
        }

        public static Quaternion ToRotation(TelemetryFrame frame, AttitudeSettings settings)
        {
            var headingDeg = NormalizeHeading(frame.HeadingDeg);
            var pitchDeg = Mathf.Clamp(frame.PitchDeg * settings.PitchScale, -settings.MaxAbsPitchDeg, settings.MaxAbsPitchDeg);
            var rollDeg = Mathf.Clamp(frame.RollDeg * settings.RollScale, -settings.MaxAbsRollDeg, settings.MaxAbsRollDeg);

            var yaw = Quaternion.AngleAxis(headingDeg, Vector3.up);
            var pitch = Quaternion.AngleAxis(pitchDeg, Vector3.right);
            var roll = Quaternion.AngleAxis(-rollDeg, Vector3.forward);
            return yaw * pitch * roll;
        }

        private static float NormalizeHeading(float headingDeg)
        {
            headingDeg %= 360f;
            return headingDeg < 0f ? headingDeg + 360f : headingDeg;
        }
    }
}
