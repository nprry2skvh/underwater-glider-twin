using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public readonly struct SeaTrialCalibrationResult
    {
        public readonly int SampleCount;
        public readonly float DurationSeconds;
        public readonly float MedianWaterSpeedMps;
        public readonly float SpeedRmseBeforeMps;
        public readonly float SpeedRmseAfterMps;
        public readonly float RecommendedPitchAmplitudeDeg;
        public readonly float RecommendedRollAmplitudeDeg;
        public readonly bool CurrentCorrectionApplied;
        public readonly float CurrentCoverageMaxDepthM;

        public SeaTrialCalibrationResult(
            int sampleCount,
            float durationSeconds,
            float medianWaterSpeedMps,
            float speedRmseBeforeMps,
            float speedRmseAfterMps,
            float recommendedPitchAmplitudeDeg,
            float recommendedRollAmplitudeDeg,
            bool currentCorrectionApplied,
            float currentCoverageMaxDepthM)
        {
            SampleCount = sampleCount;
            DurationSeconds = durationSeconds;
            MedianWaterSpeedMps = medianWaterSpeedMps;
            SpeedRmseBeforeMps = speedRmseBeforeMps;
            SpeedRmseAfterMps = speedRmseAfterMps;
            RecommendedPitchAmplitudeDeg = recommendedPitchAmplitudeDeg;
            RecommendedRollAmplitudeDeg = recommendedRollAmplitudeDeg;
            CurrentCorrectionApplied = currentCorrectionApplied;
            CurrentCoverageMaxDepthM = currentCoverageMaxDepthM;
        }
    }

    public static class SeaTrialCalibrator
    {
        private const double EarthRadiusM = 6371000d;

        public static bool TryCalibrate(
            IReadOnlyList<TelemetryFrame> frames,
            OceanCurrentProfile oceanCurrentProfile,
            GliderDynamicsProfile currentDynamics,
            out SeaTrialCalibrationResult result,
            out string error)
        {
            result = default;
            error = null;
            if (frames == null || frames.Count < 2)
            {
                error = "海试标定至少需要两个有效遥测点。";
                return false;
            }

            var waterSpeeds = new List<float>();
            var pitchAbs = new List<float>();
            var rollAbs = new List<float>();
            var durationSeconds = 0f;
            var currentCorrectionApplied = false;
            var currentCoverageMaxDepthM = 0f;

            if (oceanCurrentProfile != null && oceanCurrentProfile.TryGetDepthCoverage(out _, out currentCoverageMaxDepthM))
            {
                currentCorrectionApplied = true;
            }

            for (var i = 1; i < frames.Count; i++)
            {
                var previous = frames[i - 1];
                var current = frames[i];
                var deltaSeconds = current.ElapsedSeconds - previous.ElapsedSeconds;
                if (deltaSeconds <= 0.01f
                    || double.IsNaN(previous.LongitudeDeg)
                    || double.IsNaN(previous.LatitudeDeg)
                    || double.IsNaN(current.LongitudeDeg)
                    || double.IsNaN(current.LatitudeDeg))
                {
                    continue;
                }

                var averageLatitudeRad = (float)(0.5d * (previous.LatitudeDeg + current.LatitudeDeg) * Mathf.Deg2Rad);
                var eastwardMps = (float)((current.LongitudeDeg - previous.LongitudeDeg)
                    * Math.Cos(averageLatitudeRad) * EarthRadiusM * Mathf.Deg2Rad / deltaSeconds);
                var northwardMps = (float)((current.LatitudeDeg - previous.LatitudeDeg)
                    * EarthRadiusM * Mathf.Deg2Rad / deltaSeconds);
                var currentVelocity = oceanCurrentProfile?.GetVelocity(current.DepthM) ?? Vector2.zero;
                var waterEastwardMps = eastwardMps - currentVelocity.x;
                var waterNorthwardMps = northwardMps - currentVelocity.y;

                waterSpeeds.Add(new Vector2(waterEastwardMps, waterNorthwardMps).magnitude);
                pitchAbs.Add(Mathf.Abs(current.PitchDeg));
                rollAbs.Add(Mathf.Abs(current.RollDeg));
                durationSeconds += deltaSeconds;
            }

            if (waterSpeeds.Count < 2)
            {
                error = "海试 CSV 中没有足够的连续经纬度和时间数据。";
                return false;
            }

            var medianWaterSpeed = Median(waterSpeeds);
            var referenceSpeed = currentDynamics?.CruiseSpeedMps ?? medianWaterSpeed;
            var rmseBefore = RootMeanSquareError(waterSpeeds, referenceSpeed);
            var rmseAfter = RootMeanSquareError(waterSpeeds, medianWaterSpeed);
            var recommendedPitch = Mathf.Clamp(Percentile(pitchAbs, 0.9f), 1f, 35f);
            var recommendedRoll = Mathf.Clamp(Percentile(rollAbs, 0.9f), 0.5f, 20f);

            result = new SeaTrialCalibrationResult(
                waterSpeeds.Count,
                durationSeconds,
                medianWaterSpeed,
                rmseBefore,
                rmseAfter,
                recommendedPitch,
                recommendedRoll,
                currentCorrectionApplied,
                currentCoverageMaxDepthM);
            return true;
        }

        private static float Median(List<float> values)
        {
            values.Sort();
            var middle = values.Count / 2;
            return values.Count % 2 == 0
                ? 0.5f * (values[middle - 1] + values[middle])
                : values[middle];
        }

        private static float Percentile(List<float> values, float percentile)
        {
            values.Sort();
            var index = Mathf.Clamp(Mathf.CeilToInt((values.Count - 1) * percentile), 0, values.Count - 1);
            return values[index];
        }

        private static float RootMeanSquareError(List<float> values, float reference)
        {
            var sum = 0f;
            for (var i = 0; i < values.Count; i++)
            {
                var delta = values[i] - reference;
                sum += delta * delta;
            }

            return Mathf.Sqrt(sum / values.Count);
        }
    }
}
