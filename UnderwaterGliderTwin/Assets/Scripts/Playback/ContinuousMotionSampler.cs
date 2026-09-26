using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Playback
{
    public static class ContinuousMotionSampler
    {
        private const float TimeEpsilon = 0.0001f;

        public static ContinuousMotionSample Sample(IReadOnlyList<TelemetryFrame> frames, float elapsedSeconds)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("Continuous motion sampling requires at least one frame.", nameof(frames));
            }

            if (!IsFinite(elapsedSeconds) || elapsedSeconds <= frames[0].ElapsedSeconds)
            {
                return SampleExact(frames, 0);
            }

            var lastIndex = frames.Count - 1;
            if (elapsedSeconds >= frames[lastIndex].ElapsedSeconds)
            {
                return SampleExact(frames, lastIndex);
            }

            var lowerIndex = 0;
            var upperIndex = lastIndex;
            while (lowerIndex + 1 < upperIndex)
            {
                var middleIndex = (lowerIndex + upperIndex) / 2;
                var middleElapsed = frames[middleIndex].ElapsedSeconds;
                if (Mathf.Abs(elapsedSeconds - middleElapsed) <= TimeEpsilon)
                {
                    return SampleExact(frames, middleIndex);
                }

                if (middleElapsed < elapsedSeconds)
                {
                    lowerIndex = middleIndex;
                }
                else
                {
                    upperIndex = middleIndex;
                }
            }

            if (Mathf.Abs(elapsedSeconds - frames[lowerIndex].ElapsedSeconds) <= TimeEpsilon)
            {
                return SampleExact(frames, lowerIndex);
            }

            if (Mathf.Abs(elapsedSeconds - frames[upperIndex].ElapsedSeconds) <= TimeEpsilon)
            {
                return SampleExact(frames, upperIndex);
            }

            return SampleBetween(frames, lowerIndex, upperIndex, elapsedSeconds);
        }

        private static ContinuousMotionSample SampleExact(IReadOnlyList<TelemetryFrame> frames, int index)
        {
            var frame = frames[index];
            var hasCoordinates = TelemetryPositionUtility.HasUsableCoordinates(frame);
            var hasDiagnostics = frame.Diagnostics.HasValue;
            var waterVelocity = hasDiagnostics
                ? Sanitize(frame.Diagnostics.Value.WaterVelocityEndMps)
                : Vector3.zero;
            var currentVelocity = hasDiagnostics
                ? Sanitize(frame.Diagnostics.Value.CurrentVelocityEndMps)
                : Vector3.zero;
            var groundVelocity = Sanitize(waterVelocity + currentVelocity);
            var displayVelocity = hasDiagnostics
                ? DynamicsToEnu(groundVelocity)
                : index > 0
                    ? EstimateVelocityEnu(frames[index - 1], frame)
                    : Vector3.zero;
            var controlDeflection = hasDiagnostics
                ? Sanitize(frame.Diagnostics.Value.ControlSurfaceDeflectionDeg)
                : Vector3.zero;

            return new ContinuousMotionSample(
                index,
                index,
                0f,
                Sanitize(frame.ElapsedSeconds),
                Sanitize(frame.LongitudeDeg),
                Sanitize(frame.LatitudeDeg),
                Sanitize(frame.DepthM),
                Normalize360(Sanitize(frame.HeadingDeg)),
                NormalizeSigned(Sanitize(frame.PitchDeg)),
                NormalizeSigned(Sanitize(frame.RollDeg)),
                hasCoordinates,
                hasDiagnostics,
                waterVelocity,
                currentVelocity,
                groundVelocity,
                Sanitize(displayVelocity),
                controlDeflection);
        }

        private static ContinuousMotionSample SampleBetween(
            IReadOnlyList<TelemetryFrame> frames,
            int lowerIndex,
            int upperIndex,
            float elapsedSeconds)
        {
            var lower = frames[lowerIndex];
            var upper = frames[upperIndex];
            var deltaSeconds = upper.ElapsedSeconds - lower.ElapsedSeconds;
            var interpolation01 = deltaSeconds <= TimeEpsilon
                ? 0f
                : Mathf.Clamp01((elapsedSeconds - lower.ElapsedSeconds) / deltaSeconds);
            var lowerHasCoordinates = TelemetryPositionUtility.HasUsableCoordinates(lower);
            var upperHasCoordinates = TelemetryPositionUtility.HasUsableCoordinates(upper);
            var hasCoordinates = lowerHasCoordinates && upperHasCoordinates;
            var longitude = InterpolateCoordinate(
                lower.LongitudeDeg,
                lowerHasCoordinates,
                upper.LongitudeDeg,
                upperHasCoordinates,
                interpolation01);
            var latitude = InterpolateCoordinate(
                lower.LatitudeDeg,
                lowerHasCoordinates,
                upper.LatitudeDeg,
                upperHasCoordinates,
                interpolation01);

            var hasDiagnostics = lower.Diagnostics.HasValue && upper.Diagnostics.HasValue;
            var waterVelocity = hasDiagnostics
                ? LerpSanitized(
                    lower.Diagnostics.Value.WaterVelocityEndMps,
                    upper.Diagnostics.Value.WaterVelocityEndMps,
                    interpolation01)
                : Vector3.zero;
            var currentVelocity = hasDiagnostics
                ? LerpSanitized(
                    lower.Diagnostics.Value.CurrentVelocityEndMps,
                    upper.Diagnostics.Value.CurrentVelocityEndMps,
                    interpolation01)
                : Vector3.zero;
            var groundVelocity = Sanitize(waterVelocity + currentVelocity);
            var displayVelocity = hasDiagnostics
                ? DynamicsToEnu(groundVelocity)
                : EstimateVelocityEnu(lower, upper);
            var controlDeflection = hasDiagnostics
                ? LerpSanitized(
                    lower.Diagnostics.Value.ControlSurfaceDeflectionDeg,
                    upper.Diagnostics.Value.ControlSurfaceDeflectionDeg,
                    interpolation01)
                : Vector3.zero;

            return new ContinuousMotionSample(
                lowerIndex,
                upperIndex,
                interpolation01,
                Mathf.Lerp(Sanitize(lower.ElapsedSeconds), Sanitize(upper.ElapsedSeconds), interpolation01),
                longitude,
                latitude,
                Mathf.Lerp(Sanitize(lower.DepthM), Sanitize(upper.DepthM), interpolation01),
                Normalize360(Mathf.LerpAngle(Sanitize(lower.HeadingDeg), Sanitize(upper.HeadingDeg), interpolation01)),
                NormalizeSigned(Mathf.LerpAngle(Sanitize(lower.PitchDeg), Sanitize(upper.PitchDeg), interpolation01)),
                NormalizeSigned(Mathf.LerpAngle(Sanitize(lower.RollDeg), Sanitize(upper.RollDeg), interpolation01)),
                hasCoordinates,
                hasDiagnostics,
                waterVelocity,
                currentVelocity,
                groundVelocity,
                Sanitize(displayVelocity),
                controlDeflection);
        }

        private static Vector3 EstimateVelocityEnu(TelemetryFrame lower, TelemetryFrame upper)
        {
            var deltaSeconds = upper.ElapsedSeconds - lower.ElapsedSeconds;
            if (!IsFinite(deltaSeconds)
                || deltaSeconds <= TimeEpsilon
                || !TelemetryKinematicsUtility.TryGetHorizontalDisplacementMeters(lower, upper, out var horizontalMeters))
            {
                return Vector3.zero;
            }

            var depthDelta = Sanitize(upper.DepthM) - Sanitize(lower.DepthM);
            return Sanitize(new Vector3(
                horizontalMeters.x / deltaSeconds,
                -depthDelta / deltaSeconds,
                horizontalMeters.y / deltaSeconds));
        }

        private static Vector3 DynamicsToEnu(Vector3 eastDownNorth)
        {
            return Sanitize(new Vector3(eastDownNorth.x, -eastDownNorth.y, eastDownNorth.z));
        }

        private static Vector3 LerpSanitized(Vector3 lower, Vector3 upper, float amount)
        {
            return Vector3.Lerp(Sanitize(lower), Sanitize(upper), amount);
        }

        private static double InterpolateCoordinate(
            double lower,
            bool lowerUsable,
            double upper,
            bool upperUsable,
            float amount)
        {
            if (lowerUsable && upperUsable)
            {
                return lower + (upper - lower) * amount;
            }

            if (lowerUsable)
            {
                return lower;
            }

            return upperUsable ? upper : 0d;
        }

        private static Vector3 Sanitize(Vector3 value)
        {
            return new Vector3(Sanitize(value.x), Sanitize(value.y), Sanitize(value.z));
        }

        private static float Sanitize(float value)
        {
            return IsFinite(value) ? value : 0f;
        }

        private static double Sanitize(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0d;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Normalize360(float degrees)
        {
            var normalized = degrees % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static float NormalizeSigned(float degrees)
        {
            return Mathf.DeltaAngle(0f, degrees);
        }
    }
}
