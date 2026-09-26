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
            return ToRotation(frame.HeadingDeg, frame.PitchDeg, frame.RollDeg, settings);
        }

        public static Quaternion ToRotation(
            float headingDeg,
            float pitchDeg,
            float rollDeg,
            AttitudeSettings settings)
        {
            headingDeg = NormalizeHeading(headingDeg);
            pitchDeg = Mathf.Clamp(pitchDeg * settings.PitchScale, -settings.MaxAbsPitchDeg, settings.MaxAbsPitchDeg);
            rollDeg = Mathf.Clamp(rollDeg * settings.RollScale, -settings.MaxAbsRollDeg, settings.MaxAbsRollDeg);

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
