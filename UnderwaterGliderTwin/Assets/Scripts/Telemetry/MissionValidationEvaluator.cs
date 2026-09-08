using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public readonly struct MissionValidationReport
    {
        public readonly float MissionDepthM;
        public readonly float AlarmDepthLimitM;
        public readonly float MaximumObservedDepthM;
        public readonly bool IsDepthWithinLimit;
        public readonly bool UsesStaticWater;
        public readonly bool IsCurrentConfigurationReady;
        public readonly OceanCurrentQualityReport CurrentQuality;

        public MissionValidationReport(
            float missionDepthM,
            float alarmDepthLimitM,
            float maximumObservedDepthM,
            bool isDepthWithinLimit,
            bool usesStaticWater,
            bool isCurrentConfigurationReady,
            OceanCurrentQualityReport currentQuality)
        {
            MissionDepthM = missionDepthM;
            AlarmDepthLimitM = alarmDepthLimitM;
            MaximumObservedDepthM = maximumObservedDepthM;
            IsDepthWithinLimit = isDepthWithinLimit;
            UsesStaticWater = usesStaticWater;
            IsCurrentConfigurationReady = isCurrentConfigurationReady;
            CurrentQuality = currentQuality;
        }

        public bool IsReady => IsDepthWithinLimit && IsCurrentConfigurationReady;
    }

    public static class MissionValidationEvaluator
    {
        private const float MinimumAlarmDepthM = 100f;
        private const float DepthSafetyMarginRatio = 0.05f;
        private const float DepthSafetyMarginMinimumM = 5f;

        public static MissionValidationReport Evaluate(
            SimulationProfile simulationProfile,
            IReadOnlyList<TelemetryFrame> frames,
            DateTime utcNow)
        {
            var maximumObservedDepth = GetMaximumObservedDepth(frames);
            var maximumCommandedDepth = GetMaximumCommandedDepth(frames);
            var missionDepth = simulationProfile == null
                ? Mathf.Max(maximumObservedDepth, maximumCommandedDepth)
                : Mathf.Max(simulationProfile.TargetDepthM, simulationProfile.WaterColumnDepthM, maximumCommandedDepth);
            var alarmDepthLimit = Mathf.Max(
                MinimumAlarmDepthM,
                missionDepth + missionDepth * DepthSafetyMarginRatio + DepthSafetyMarginMinimumM);
            var isDepthWithinLimit = maximumObservedDepth <= alarmDepthLimit + 0.01f;

            var currentProfile = simulationProfile?.OceanCurrentProfile;
            var usesStaticWater = currentProfile == null || currentProfile.Layers.Count == 0;
            var currentQuality = OceanCurrentQualityEvaluator.Evaluate(currentProfile, 0f, missionDepth, null, utcNow);
            var currentConfigurationReady = usesStaticWater || currentQuality.Level == OceanCurrentQualityLevel.Ready;

            return new MissionValidationReport(
                missionDepth,
                alarmDepthLimit,
                maximumObservedDepth,
                isDepthWithinLimit,
                usesStaticWater,
                currentConfigurationReady,
                currentQuality);
        }

        private static float GetMaximumObservedDepth(IReadOnlyList<TelemetryFrame> frames)
        {
            var maximum = 0f;
            if (frames == null)
            {
                return maximum;
            }

            for (var i = 0; i < frames.Count; i++)
            {
                maximum = Mathf.Max(maximum, Mathf.Max(0f, frames[i].DepthM));
            }

            return maximum;
        }

        private static float GetMaximumCommandedDepth(IReadOnlyList<TelemetryFrame> frames)
        {
            var maximum = 0f;
            if (frames == null)
            {
                return maximum;
            }

            for (var i = 0; i < frames.Count; i++)
            {
                maximum = Mathf.Max(maximum, Mathf.Max(0f, frames[i].TargetDepthM));
            }

            return maximum;
        }
    }
}
