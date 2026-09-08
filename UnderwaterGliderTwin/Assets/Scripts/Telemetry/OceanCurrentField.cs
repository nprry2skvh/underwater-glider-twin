using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    [Serializable]
    public sealed class OceanCurrentFieldSample
    {
        public OceanCurrentFieldSample()
        {
        }

        public OceanCurrentFieldSample(
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float elapsedSeconds,
            float eastwardMps,
            float northwardMps)
            : this(longitudeDeg, latitudeDeg, depthM, elapsedSeconds, eastwardMps, northwardMps, 0f)
        {
        }

        public OceanCurrentFieldSample(
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float elapsedSeconds,
            float eastwardMps,
            float northwardMps,
            float verticalMps)
        {
            LongitudeDeg = longitudeDeg;
            LatitudeDeg = latitudeDeg;
            DepthM = depthM;
            ElapsedSeconds = elapsedSeconds;
            EastwardMps = eastwardMps;
            NorthwardMps = northwardMps;
            VerticalMps = verticalMps;
        }

        public double LongitudeDeg { get; set; }
        public double LatitudeDeg { get; set; }
        public float DepthM { get; set; }
        public float ElapsedSeconds { get; set; }
        public float EastwardMps { get; set; }
        public float NorthwardMps { get; set; }
        public float VerticalMps { get; set; }

        public OceanCurrentFieldSample Clone()
        {
            return new OceanCurrentFieldSample(LongitudeDeg, LatitudeDeg, DepthM, ElapsedSeconds, EastwardMps, NorthwardMps, VerticalMps);
        }
    }

    public sealed class OceanCurrentField
    {
        private const double MetersPerDegreeLatitude = 111320d;
        private const double HorizontalCoverageMarginDeg = 0.05d;
        private const float DepthScaleMeters = 100f;
        private const float TimeScaleSeconds = 21600f;
        private readonly List<OceanCurrentFieldSample> samples = new List<OceanCurrentFieldSample>();
        private double minimumLongitudeDeg = double.PositiveInfinity;
        private double maximumLongitudeDeg = double.NegativeInfinity;
        private double minimumLatitudeDeg = double.PositiveInfinity;
        private double maximumLatitudeDeg = double.NegativeInfinity;
        private float minimumDepthM = float.PositiveInfinity;
        private float maximumDepthM = float.NegativeInfinity;
        private float minimumElapsedSeconds = float.PositiveInfinity;
        private float maximumElapsedSeconds = float.NegativeInfinity;
        private bool hasMultipleElapsedTimes;

        public OceanCurrentField()
        {
        }

        public OceanCurrentField(IEnumerable<OceanCurrentFieldSample> sourceSamples)
        {
            ReplaceSamples(sourceSamples);
        }

        public IReadOnlyList<OceanCurrentFieldSample> Samples => samples;

        public bool TryGetVelocity(
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float elapsedSeconds,
            out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (!TryGetVector(longitudeDeg, latitudeDeg, depthM, elapsedSeconds, float.PositiveInfinity, out var vector))
            {
                return false;
            }

            velocity = new Vector2(vector.x, vector.z);
            return true;
        }

        public bool TryGetVector(
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float elapsedSeconds,
            float maximumHorizontalRadiusKm,
            out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!IsWithinHorizontalCoverage(longitudeDeg, latitudeDeg)
                || !IsWithinDepthAndTimeCoverage(depthM, elapsedSeconds))
            {
                return false;
            }

            var totalWeight = 0f;
            var weightedVelocity = Vector3.zero;
            var maximumRadiusMeters = Mathf.Max(0f, maximumHorizontalRadiusKm) * 1000f;
            foreach (var sample in samples)
            {
                if (!IsUsable(sample))
                {
                    continue;
                }

                var longitudeScale = MetersPerDegreeLatitude * Math.Cos(latitudeDeg * Math.PI / 180d);
                var eastMeters = (sample.LongitudeDeg - longitudeDeg) * longitudeScale;
                var northMeters = (sample.LatitudeDeg - latitudeDeg) * MetersPerDegreeLatitude;
                var horizontalDistanceSquared = (float)(eastMeters * eastMeters + northMeters * northMeters);
                if (!float.IsPositiveInfinity(maximumRadiusMeters)
                    && horizontalDistanceSquared > maximumRadiusMeters * maximumRadiusMeters)
                {
                    continue;
                }
                var depthDelta = (sample.DepthM - depthM) * DepthScaleMeters / Mathf.Max(DepthScaleMeters, 1f);
                var timeDelta = (sample.ElapsedSeconds - elapsedSeconds) * DepthScaleMeters / TimeScaleSeconds;
                var distanceSquared = horizontalDistanceSquared
                    + depthDelta * depthDelta
                    + timeDelta * timeDelta;
                if (distanceSquared <= 0.0001f)
                {
                    velocity = new Vector3(sample.EastwardMps, sample.VerticalMps, sample.NorthwardMps);
                    return true;
                }

                var weight = 1f / distanceSquared;
                weightedVelocity += new Vector3(sample.EastwardMps, sample.VerticalMps, sample.NorthwardMps) * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            velocity = weightedVelocity / totalWeight;
            return true;
        }

        public void ReplaceSamples(IEnumerable<OceanCurrentFieldSample> sourceSamples)
        {
            samples.Clear();
            ResetCoverage();
            if (sourceSamples == null)
            {
                return;
            }

            foreach (var sample in sourceSamples)
            {
                if (IsUsable(sample))
                {
                    var clone = sample.Clone();
                    samples.Add(clone);
                    IncludeInCoverage(clone);
                }
            }
        }

        public OceanCurrentField Clone()
        {
            return new OceanCurrentField(samples);
        }

        private static bool IsUsable(OceanCurrentFieldSample sample)
        {
            return sample != null
                && !double.IsNaN(sample.LongitudeDeg)
                && !double.IsInfinity(sample.LongitudeDeg)
                && !double.IsNaN(sample.LatitudeDeg)
                && !double.IsInfinity(sample.LatitudeDeg)
                && !float.IsNaN(sample.DepthM)
                && !float.IsInfinity(sample.DepthM)
                && !float.IsNaN(sample.ElapsedSeconds)
                && !float.IsInfinity(sample.ElapsedSeconds)
                && !float.IsNaN(sample.EastwardMps)
                && !float.IsInfinity(sample.EastwardMps)
                && !float.IsNaN(sample.NorthwardMps)
                && !float.IsInfinity(sample.NorthwardMps)
                && !float.IsNaN(sample.VerticalMps)
                && !float.IsInfinity(sample.VerticalMps);
        }

        private bool IsWithinHorizontalCoverage(double longitudeDeg, double latitudeDeg)
        {
            return !double.IsPositiveInfinity(minimumLongitudeDeg)
                && longitudeDeg >= minimumLongitudeDeg - HorizontalCoverageMarginDeg
                && longitudeDeg <= maximumLongitudeDeg + HorizontalCoverageMarginDeg
                && latitudeDeg >= minimumLatitudeDeg - HorizontalCoverageMarginDeg
                && latitudeDeg <= maximumLatitudeDeg + HorizontalCoverageMarginDeg;
        }

        private bool IsWithinDepthAndTimeCoverage(float depthM, float elapsedSeconds)
        {
            if (float.IsPositiveInfinity(minimumDepthM)
                || depthM < minimumDepthM
                || depthM > maximumDepthM)
            {
                return false;
            }

            return !hasMultipleElapsedTimes
                || (elapsedSeconds >= minimumElapsedSeconds && elapsedSeconds <= maximumElapsedSeconds);
        }

        private void ResetCoverage()
        {
            minimumLongitudeDeg = double.PositiveInfinity;
            maximumLongitudeDeg = double.NegativeInfinity;
            minimumLatitudeDeg = double.PositiveInfinity;
            maximumLatitudeDeg = double.NegativeInfinity;
            minimumDepthM = float.PositiveInfinity;
            maximumDepthM = float.NegativeInfinity;
            minimumElapsedSeconds = float.PositiveInfinity;
            maximumElapsedSeconds = float.NegativeInfinity;
            hasMultipleElapsedTimes = false;
        }

        private void IncludeInCoverage(OceanCurrentFieldSample sample)
        {
            var hadElapsedTime = !float.IsPositiveInfinity(minimumElapsedSeconds);
            hasMultipleElapsedTimes |= hadElapsedTime && !Mathf.Approximately(sample.ElapsedSeconds, minimumElapsedSeconds);
            minimumLongitudeDeg = Math.Min(minimumLongitudeDeg, sample.LongitudeDeg);
            maximumLongitudeDeg = Math.Max(maximumLongitudeDeg, sample.LongitudeDeg);
            minimumLatitudeDeg = Math.Min(minimumLatitudeDeg, sample.LatitudeDeg);
            maximumLatitudeDeg = Math.Max(maximumLatitudeDeg, sample.LatitudeDeg);
            minimumDepthM = Mathf.Min(minimumDepthM, sample.DepthM);
            maximumDepthM = Mathf.Max(maximumDepthM, sample.DepthM);
            minimumElapsedSeconds = Mathf.Min(minimumElapsedSeconds, sample.ElapsedSeconds);
            maximumElapsedSeconds = Mathf.Max(maximumElapsedSeconds, sample.ElapsedSeconds);
        }
    }
}
