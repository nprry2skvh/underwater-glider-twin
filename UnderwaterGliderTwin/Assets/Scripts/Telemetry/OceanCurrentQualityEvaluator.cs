using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public enum OceanCurrentQualityLevel
    {
        Missing,
        PartialCoverage,
        Stale,
        Ready
    }

    public readonly struct OceanCurrentQualityReport
    {
        public readonly OceanCurrentQualityLevel Level;
        public readonly bool IsDepthRangeCovered;
        public readonly bool HasCoverageGaps;
        public readonly bool IsCached;
        public readonly bool IsStale;
        public readonly float MinimumDepthM;
        public readonly float MaximumDepthM;
        public readonly float RequestedMinimumDepthM;
        public readonly float RequestedMaximumDepthM;
        public readonly float AgeHours;

        public OceanCurrentQualityReport(
            OceanCurrentQualityLevel level,
            bool isDepthRangeCovered,
            bool hasCoverageGaps,
            bool isCached,
            bool isStale,
            float minimumDepthM,
            float maximumDepthM,
            float requestedMinimumDepthM,
            float requestedMaximumDepthM,
            float ageHours)
        {
            Level = level;
            IsDepthRangeCovered = isDepthRangeCovered;
            HasCoverageGaps = hasCoverageGaps;
            IsCached = isCached;
            IsStale = isStale;
            MinimumDepthM = minimumDepthM;
            MaximumDepthM = maximumDepthM;
            RequestedMinimumDepthM = requestedMinimumDepthM;
            RequestedMaximumDepthM = requestedMaximumDepthM;
            AgeHours = ageHours;
        }
    }

    public static class OceanCurrentQualityEvaluator
    {
        public const float MaximumFreshAgeHours = 6f;
        private const float CoverageToleranceM = 0.1f;

        public static OceanCurrentQualityReport Evaluate(
            OceanCurrentProfile profile,
            float requestedMinimumDepthM,
            float requestedMaximumDepthM,
            CopernicusCurrentResult source,
            DateTime utcNow)
        {
            var requestedMinimum = Mathf.Max(0f, Mathf.Min(requestedMinimumDepthM, requestedMaximumDepthM));
            var requestedMaximum = Mathf.Max(requestedMinimum, Mathf.Max(requestedMinimumDepthM, requestedMaximumDepthM));
            var isCached = source != null
                && source.Source.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0;
            var ageHours = GetAgeHours(source?.RetrievedAtUtc, utcNow);
            var isStale = ageHours > MaximumFreshAgeHours;

            if (profile == null || !profile.TryGetDepthCoverage(out var minimumDepth, out var maximumDepth))
            {
                return new OceanCurrentQualityReport(
                    OceanCurrentQualityLevel.Missing,
                    false,
                    false,
                    isCached,
                    isStale,
                    0f,
                    0f,
                    requestedMinimum,
                    requestedMaximum,
                    ageHours);
            }

            var hasCoverageGaps = HasCoverageGap(profile.Layers, requestedMinimum, requestedMaximum);
            var isDepthRangeCovered = !hasCoverageGaps
                && minimumDepth <= requestedMinimum + CoverageToleranceM
                && maximumDepth + CoverageToleranceM >= requestedMaximum;
            var level = !isDepthRangeCovered
                ? OceanCurrentQualityLevel.PartialCoverage
                : isStale
                    ? OceanCurrentQualityLevel.Stale
                    : OceanCurrentQualityLevel.Ready;

            return new OceanCurrentQualityReport(
                level,
                isDepthRangeCovered,
                hasCoverageGaps,
                isCached,
                isStale,
                minimumDepth,
                maximumDepth,
                requestedMinimum,
                requestedMaximum,
                ageHours);
        }

        private static bool HasCoverageGap(IReadOnlyList<OceanCurrentLayer> layers, float requestedMinimum, float requestedMaximum)
        {
            var intervals = new List<Vector2>();
            if (layers != null)
            {
                for (var i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (layer == null
                        || float.IsNaN(layer.MinDepthM)
                        || float.IsNaN(layer.MaxDepthM)
                        || float.IsInfinity(layer.MinDepthM)
                        || float.IsInfinity(layer.MaxDepthM))
                    {
                        continue;
                    }

                    intervals.Add(new Vector2(
                        Mathf.Min(layer.MinDepthM, layer.MaxDepthM),
                        Mathf.Max(layer.MinDepthM, layer.MaxDepthM)));
                }
            }

            if (intervals.Count == 0)
            {
                return true;
            }

            intervals.Sort((left, right) => left.x.CompareTo(right.x));
            var coveredEnd = requestedMinimum;
            var coveredStartFound = false;
            for (var i = 0; i < intervals.Count; i++)
            {
                var interval = intervals[i];
                if (interval.y < requestedMinimum - CoverageToleranceM || interval.x > requestedMaximum + CoverageToleranceM)
                {
                    continue;
                }

                if (!coveredStartFound)
                {
                    if (interval.x > requestedMinimum + CoverageToleranceM)
                    {
                        return true;
                    }

                    coveredStartFound = true;
                    coveredEnd = interval.y;
                }
                else
                {
                    if (interval.x > coveredEnd + CoverageToleranceM)
                    {
                        return true;
                    }

                    coveredEnd = Mathf.Max(coveredEnd, interval.y);
                }

                if (coveredEnd + CoverageToleranceM >= requestedMaximum)
                {
                    return false;
                }
            }

            return true;
        }

        private static float GetAgeHours(string retrievedAtUtc, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(retrievedAtUtc)
                || !DateTimeOffset.TryParse(retrievedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var retrievedAt))
            {
                return 0f;
            }

            return Mathf.Max(0f, (float)(utcNow.ToUniversalTime() - retrievedAt.UtcDateTime).TotalHours);
        }
    }
}
