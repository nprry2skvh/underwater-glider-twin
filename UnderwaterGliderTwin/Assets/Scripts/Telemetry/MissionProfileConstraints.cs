using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class MissionProfileConstraints
    {
        public const float MaximumEngineeringGlideAngleDeg = 32f;
        public const float ReferenceVerticalSpeedMps = 0.1f;
        public const float ReferenceTurnaroundAllowanceSeconds = 900f;

        public static float ComputeRequiredGlideAngleDeg(float targetDepthM, float cycleDurationSeconds, float horizontalSpeedMps)
        {
            if (targetDepthM <= 0f)
            {
                return 0f;
            }

            var descentDurationSeconds = Mathf.Max(0.001f, cycleDurationSeconds * 0.5f);
            var horizontalSpeed = Mathf.Max(0.001f, horizontalSpeedMps);
            var verticalSpeed = targetDepthM / descentDurationSeconds;
            return Mathf.Atan2(verticalSpeed, horizontalSpeed) * Mathf.Rad2Deg;
        }

        public static float ComputeMinimumCycleDurationSeconds(float targetDepthM, float horizontalSpeedMps, float maximumGlideAngleDeg = MaximumEngineeringGlideAngleDeg)
        {
            if (targetDepthM <= 0f)
            {
                return 0f;
            }

            var horizontalSpeed = Mathf.Max(0.001f, horizontalSpeedMps);
            var glideAngleRadians = Mathf.Clamp(maximumGlideAngleDeg, 1f, 80f) * Mathf.Deg2Rad;
            var verticalSpeed = horizontalSpeed * Mathf.Tan(glideAngleRadians);
            return 2f * targetDepthM / Mathf.Max(0.001f, verticalSpeed);
        }

        public static float ApplyEngineeringMinimumCycleDuration(float requestedCycleDurationSeconds, float targetDepthM, float horizontalSpeedMps)
        {
            if (horizontalSpeedMps < 0.05f)
            {
                return Mathf.Max(0f, requestedCycleDurationSeconds);
            }

            var minimumDuration = ComputeMinimumCycleDurationSeconds(targetDepthM, horizontalSpeedMps);
            var referenceDuration = ComputeReferenceCycleDurationSeconds(targetDepthM);
            return Mathf.Max(Mathf.Max(0f, requestedCycleDurationSeconds), minimumDuration, referenceDuration);
        }

        public static float ComputeReferenceCycleDurationSeconds(float targetDepthM)
        {
            if (targetDepthM <= 0f)
            {
                return 0f;
            }

            var verticalTransitSeconds = 2f * targetDepthM / ReferenceVerticalSpeedMps;
            return verticalTransitSeconds + ReferenceTurnaroundAllowanceSeconds;
        }

        public static float NormalizeEngineeringCycleDuration(float requestedCycleDurationSeconds, float targetDepthM, float horizontalSpeedMps)
        {
            var engineeringDuration = ApplyEngineeringMinimumCycleDuration(requestedCycleDurationSeconds, targetDepthM, horizontalSpeedMps);
            return engineeringDuration > requestedCycleDurationSeconds + 0.5f
                ? Mathf.Ceil(engineeringDuration / 60f) * 60f
                : Mathf.Max(0f, requestedCycleDurationSeconds);
        }
    }
}
