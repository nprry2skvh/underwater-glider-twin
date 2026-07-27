using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public readonly struct OceanCurrentCandidate
    {
        public OceanCurrentCandidate(Vector3 enuPositionM, double longitudeDeg, double latitudeDeg, float depthM)
        {
            EnuPositionM = enuPositionM;
            LongitudeDeg = longitudeDeg;
            LatitudeDeg = latitudeDeg;
            DepthM = depthM;
        }

        public Vector3 EnuPositionM { get; }
        public double LongitudeDeg { get; }
        public double LatitudeDeg { get; }
        public float DepthM { get; }
    }

    public readonly struct OceanCurrentSampledCandidate
    {
        public OceanCurrentSampledCandidate(OceanCurrentCandidate candidate, OceanCurrentVector vector)
        {
            Candidate = candidate;
            Vector = vector;
        }

        public OceanCurrentCandidate Candidate { get; }
        public OceanCurrentVector Vector { get; }
    }

    public readonly struct OceanCurrentVisibleCandidate
    {
        public OceanCurrentVisibleCandidate(OceanCurrentSampledCandidate sampledCandidate, float cameraDistanceSquared)
        {
            Candidate = sampledCandidate.Candidate;
            Vector = sampledCandidate.Vector;
            CameraDistanceSquared = cameraDistanceSquared;
        }

        public OceanCurrentCandidate Candidate { get; }
        public OceanCurrentVector Vector { get; }
        public float CameraDistanceSquared { get; }
    }

    public sealed class OceanVolumeSamplingCache
    {
        public const int MaximumCandidateCount = 1440;
        public const int MaximumVisibleArrowCount = 360;
        private const double MetersPerDegreeLatitude = 111320d;

        private readonly SimulationProfile profile;
        private readonly List<OceanCurrentCandidate> candidates = new List<OceanCurrentCandidate>(MaximumCandidateCount);
        private readonly List<OceanCurrentSampledCandidate> sampledCandidates = new List<OceanCurrentSampledCandidate>(MaximumCandidateCount);
        private readonly List<OceanCurrentVisibleCandidate> visibleCandidates = new List<OceanCurrentVisibleCandidate>(MaximumVisibleArrowCount);

        public OceanVolumeSamplingCache(SimulationProfile profile)
        {
            this.profile = profile ?? SimulationProfile.Default;
        }

        public IReadOnlyList<OceanCurrentCandidate> Candidates => candidates;
        public IReadOnlyList<OceanCurrentSampledCandidate> SampledCandidates => sampledCandidates;
        public IReadOnlyList<OceanCurrentVisibleCandidate> VisibleCandidates => visibleCandidates;

        public void RebuildCandidateCache(Vector3 centerEnuPositionM, Vector2 horizontalExtentsM, float missionDepthM)
        {
            candidates.Clear();
            sampledCandidates.Clear();
            visibleCandidates.Clear();

            var depthCount = Mathf.Clamp(profile.CandidateDepthLayerCount, 1, 12);
            var columnCount = Mathf.Clamp(profile.CandidateHorizontalColumnCount, 1, 12);
            var rowCount = Mathf.Clamp(profile.CandidateHorizontalRowCount, 1, 10);
            var maximumDepth = Mathf.Max(0f, missionDepthM);
            var width = Mathf.Max(1f, horizontalExtentsM.x);
            var height = Mathf.Max(1f, horizontalExtentsM.y);
            var longitudeScale = Math.Max(1d, MetersPerDegreeLatitude * Math.Cos(profile.OriginLatitudeDeg * Math.PI / 180d));

            for (var depthIndex = 0; depthIndex < depthCount && candidates.Count < MaximumCandidateCount; depthIndex++)
            {
                var depthRatio = depthCount == 1 ? 0f : depthIndex / (float)(depthCount - 1);
                var depthM = maximumDepth * depthRatio;
                for (var row = 0; row < rowCount && candidates.Count < MaximumCandidateCount; row++)
                {
                    var zRatio = rowCount == 1 ? 0.5f : row / (float)(rowCount - 1);
                    var northM = Mathf.Lerp(-height * 0.5f, height * 0.5f, zRatio);
                    for (var column = 0; column < columnCount && candidates.Count < MaximumCandidateCount; column++)
                    {
                        var xRatio = columnCount == 1 ? 0.5f : column / (float)(columnCount - 1);
                        var eastM = Mathf.Lerp(-width * 0.5f, width * 0.5f, xRatio);
                        var enuPosition = centerEnuPositionM + new Vector3(eastM, -depthM, northM);
                        var longitude = profile.OriginLongitudeDeg + enuPosition.x / longitudeScale;
                        var latitude = profile.OriginLatitudeDeg + enuPosition.z / MetersPerDegreeLatitude;
                        candidates.Add(new OceanCurrentCandidate(enuPosition, longitude, latitude, depthM));
                    }
                }
            }
        }

        public void RefreshForTime(
            OceanCurrentResolver resolver,
            float elapsedSeconds,
            Vector3 gliderEnuPositionM,
            IReadOnlyList<Vector3> trajectoryEnuPoints)
        {
            sampledCandidates.Clear();
            visibleCandidates.Clear();
            if (resolver == null)
            {
                return;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (IsInsideSafetyChannel(candidate.EnuPositionM, gliderEnuPositionM, trajectoryEnuPoints)
                    || !resolver.TryGetVector(
                        new OceanCurrentQuery(candidate.LongitudeDeg, candidate.LatitudeDeg, candidate.EnuPositionM, candidate.DepthM, elapsedSeconds),
                        out var vector)
                    || vector.VelocityMps.magnitude < profile.MinimumArrowSpeedMps)
                {
                    continue;
                }

                sampledCandidates.Add(new OceanCurrentSampledCandidate(candidate, vector));
            }
        }

        public void RefreshForCamera(Vector3 cameraEnuPositionM)
        {
            visibleCandidates.Clear();
            for (var index = 0; index < sampledCandidates.Count; index++)
            {
                var sampledCandidate = sampledCandidates[index];
                var distanceSquared = (sampledCandidate.Candidate.EnuPositionM - cameraEnuPositionM).sqrMagnitude;
                var insertIndex = visibleCandidates.Count;
                while (insertIndex > 0 && distanceSquared < visibleCandidates[insertIndex - 1].CameraDistanceSquared)
                {
                    insertIndex--;
                }

                if (insertIndex >= MaximumVisibleArrowCount)
                {
                    continue;
                }

                visibleCandidates.Insert(insertIndex, new OceanCurrentVisibleCandidate(sampledCandidate, distanceSquared));
                if (visibleCandidates.Count > MaximumVisibleArrowCount)
                {
                    visibleCandidates.RemoveAt(visibleCandidates.Count - 1);
                }
            }
        }

        private bool IsInsideSafetyChannel(Vector3 candidateEnuPositionM, Vector3 gliderEnuPositionM, IReadOnlyList<Vector3> trajectoryEnuPoints)
        {
            if (IsFinite(gliderEnuPositionM)
                && Vector3.Distance(candidateEnuPositionM, gliderEnuPositionM) < profile.GliderClearanceRadiusM)
            {
                return true;
            }

            if (trajectoryEnuPoints == null)
            {
                return false;
            }

            for (var index = 0; index < trajectoryEnuPoints.Count; index++)
            {
                if (Vector3.Distance(candidateEnuPositionM, trajectoryEnuPoints[index]) < profile.TrajectorySafetyCorridorRadiusM)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
