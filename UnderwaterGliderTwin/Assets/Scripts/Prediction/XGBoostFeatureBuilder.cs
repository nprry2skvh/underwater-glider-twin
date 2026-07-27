using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    public static class XGBoostFeatureBuilder
    {
        private const float MetersPerDegreeLatitude = 111320f;

        public static bool TryBuild(
            PredictionContext context,
            XGBoostFeatureSchema schema,
            float horizonSeconds,
            out float[] values,
            out string error)
        {
            values = Array.Empty<float>();
            error = string.Empty;
            if (context?.Window?.WindowFrames == null || schema?.feature_names == null)
            {
                error = "XGBoost context or feature schema is missing.";
                return false;
            }

            var history = context.Window.WindowFrames;
            var historyLength = Mathf.Max(1, schema.history_length);
            if (history.Count < historyLength)
            {
                error = "XGBoost requires " + historyLength + " history frames.";
                return false;
            }

            var startIndex = history.Count - historyLength;
            var first = history[startIndex];
            var current = history[history.Count - 1];
            var duration = Mathf.Max(0.001f, current.ElapsedSeconds - first.ElapsedSeconds);
            var depths = new float[historyLength];
            var pitches = new float[historyLength];
            var velocities = new float[historyLength];
            for (var index = 0; index < historyLength; index++)
            {
                var frameIndex = startIndex + index;
                var frame = history[frameIndex];
                depths[index] = frame.DepthM;
                pitches[index] = frame.PitchDeg;
                velocities[index] = frameIndex == startIndex ? 0f : EstimateSpeed(history[frameIndex - 1], frame);
            }

            var lastVelocity = historyLength > 1 ? EstimateVelocity(history[history.Count - 2], current) : Vector3.zero;
            values = new float[schema.feature_names.Length];
            for (var index = 0; index < schema.feature_names.Length; index++)
            {
                if (!TryResolveFeature(
                        schema.feature_names[index], current, first, depths, pitches, velocities,
                        lastVelocity, duration, horizonSeconds, out values[index]))
                {
                    error = "Unsupported XGBoost feature: " + schema.feature_names[index];
                    values = Array.Empty<float>();
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveFeature(
            string name,
            TelemetryFrame current,
            TelemetryFrame first,
            float[] depths,
            float[] pitches,
            float[] velocities,
            Vector3 lastVelocity,
            float duration,
            float horizonSeconds,
            out float value)
        {
            switch (name)
            {
                case "depth_m": value = current.DepthM; return true;
                case "heading_deg": value = current.HeadingDeg; return true;
                case "heading_sin": value = Mathf.Sin(current.HeadingDeg * Mathf.Deg2Rad); return true;
                case "heading_cos": value = Mathf.Cos(current.HeadingDeg * Mathf.Deg2Rad); return true;
                case "pitch_deg": value = current.PitchDeg; return true;
                case "roll_deg": value = current.RollDeg; return true;
                case "velocity_x": value = lastVelocity.x; return true;
                case "velocity_y": value = lastVelocity.y; return true;
                case "velocity_z": value = lastVelocity.z; return true;
                case "current_speed": value = lastVelocity.magnitude; return true;
                case "target_heading_residual_deg": value = Mathf.DeltaAngle(current.HeadingDeg, current.TargetHeadingDeg); return true;
                case "target_depth_residual_m": value = current.TargetDepthM - current.DepthM; return true;
                case "turn_angle_deg": value = current.TurnAngleDeg; return true;
                case "piston_mm": value = current.PistonMm; return true;
                case "depth_mean": value = Mean(depths); return true;
                case "depth_std": value = StandardDeviation(depths); return true;
                case "pitch_mean": value = Mean(pitches); return true;
                case "pitch_std": value = StandardDeviation(pitches); return true;
                case "velocity_mean": value = Mean(velocities); return true;
                case "velocity_std": value = StandardDeviation(velocities); return true;
                case "depth_trend_mps": value = (current.DepthM - first.DepthM) / duration; return true;
                case "heading_trend_degps": value = Mathf.DeltaAngle(first.HeadingDeg, current.HeadingDeg) / duration; return true;
                case "descending_flag": value = current.DepthM >= first.DepthM ? 1f : 0f; return true;
                case "forecast_horizon_seconds": value = horizonSeconds; return true;
                default: value = 0f; return false;
            }
        }

        private static Vector3 EstimateVelocity(TelemetryFrame previous, TelemetryFrame current)
        {
            var seconds = Mathf.Max(0.001f, current.ElapsedSeconds - previous.ElapsedSeconds);
            var averageLatitudeRadians = (float)((previous.LatitudeDeg + current.LatitudeDeg) * 0.5 * Math.PI / 180.0);
            var east = (float)(current.LongitudeDeg - previous.LongitudeDeg) * MetersPerDegreeLatitude * Mathf.Cos(averageLatitudeRadians);
            var north = (float)(current.LatitudeDeg - previous.LatitudeDeg) * MetersPerDegreeLatitude;
            return new Vector3(east / seconds, -(current.DepthM - previous.DepthM) / seconds, north / seconds);
        }

        private static float EstimateSpeed(TelemetryFrame previous, TelemetryFrame current)
        {
            return EstimateVelocity(previous, current).magnitude;
        }

        private static float Mean(IReadOnlyList<float> values)
        {
            var sum = 0f;
            for (var index = 0; index < values.Count; index++)
            {
                sum += values[index];
            }

            return values.Count == 0 ? 0f : sum / values.Count;
        }

        private static float StandardDeviation(IReadOnlyList<float> values)
        {
            var mean = Mean(values);
            var sum = 0f;
            for (var index = 0; index < values.Count; index++)
            {
                var delta = values[index] - mean;
                sum += delta * delta;
            }

            return values.Count == 0 ? 0f : Mathf.Sqrt(sum / values.Count);
        }
    }
}
