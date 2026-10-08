using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public readonly struct WaterWakePathSample
    {
        public readonly Vector2 PositionXZ;
        public readonly float DepthM;
        public readonly float AgeSeconds;
        public readonly float DistanceAlongPath;

        public WaterWakePathSample(Vector2 positionXZ, float depthM, float ageSeconds, float distanceAlongPath = 0f)
        {
            PositionXZ = positionXZ;
            DepthM = depthM;
            AgeSeconds = ageSeconds;
            DistanceAlongPath = distanceAlongPath;
        }
    }

    public static class WaterWakePathSampler
    {
        public const int MaximumSampleCount = 64;
        private const float MinimumDistance = 0.000001f;

        public static int FillAnchored(
            FittedTrajectory trajectory,
            IReadOnlyList<TelemetryFrame> frames,
            GeoCoordinateMapper mapper,
            float currentElapsedSeconds,
            int direction,
            float maximumDistanceWorld,
            float packetSpacingWorld,
            WaterWakePathSample[] destination)
        {
            if (trajectory == null || mapper == null || destination == null || destination.Length == 0
                || trajectory.Times.Count == 0 || trajectory.Points.Count == 0
                || !IsFinite(currentElapsedSeconds) || !IsFinite(maximumDistanceWorld)
                || !IsFinite(packetSpacingWorld) || maximumDistanceWorld <= 0f || packetSpacingWorld <= 0f)
            {
                return 0;
            }

            var times = trajectory.Times;
            var points = trajectory.Points;
            var pointCount = Mathf.Min(times.Count, points.Count);
            if (!HasStrictlyIncreasingFiniteTimes(times, pointCount) || !HasFinitePoints(points, pointCount))
            {
                return 0;
            }

            var currentTime = Mathf.Clamp(currentElapsedSeconds, times[0], times[pointCount - 1]);
            var currentPosition = trajectory.PositionAt(currentTime);
            var currentDistance = trajectory.HorizontalDistanceAt(currentTime);
            var travelDirection = direction < 0 ? -1 : 1;
            var endDistance = Mathf.Clamp(
                currentDistance - travelDirection * maximumDistanceWorld,
                0f,
                trajectory.TotalHorizontalDistance);
            destination[0] = CreateSample(currentPosition, currentTime, currentTime, frames, mapper, trajectory);
            var sampleCount = 1;
            if (Mathf.Abs(currentDistance - endDistance) <= MinimumDistance)
            {
                return sampleCount;
            }

            var halfSpacing = packetSpacingWorld * 0.5f;
            var tick = travelDirection > 0
                ? Mathf.CeilToInt(currentDistance / halfSpacing) - 1
                : Mathf.FloorToInt(currentDistance / halfSpacing) + 1;
            var limit = Mathf.Min(destination.Length, MaximumSampleCount);
            while (sampleCount < limit)
            {
                var distance = tick * halfSpacing;
                if ((travelDirection > 0 && distance < endDistance - MinimumDistance)
                    || (travelDirection < 0 && distance > endDistance + MinimumDistance))
                {
                    break;
                }

                trajectory.SampleAtHorizontalDistance(
                    distance, travelDirection, out var position, out var sampleTime);
                destination[sampleCount++] = CreateSample(
                    position, sampleTime, currentTime, frames, mapper, trajectory);
                tick -= travelDirection;
            }

            if (sampleCount < limit
                && Mathf.Abs(destination[sampleCount - 1].DistanceAlongPath - endDistance) > MinimumDistance)
            {
                trajectory.SampleAtHorizontalDistance(
                    endDistance, travelDirection, out var position, out var sampleTime);
                destination[sampleCount++] = CreateSample(
                    position, sampleTime, currentTime, frames, mapper, trajectory);
            }

            var distances = trajectory.HorizontalDistances;
            for (var vertex = travelDirection > 0 ? pointCount - 2 : 1;
                 vertex > 0 && vertex < pointCount - 1 && sampleCount < limit;
                 vertex -= travelDirection)
            {
                var distance = distances[vertex];
                if (distance <= Mathf.Min(currentDistance, endDistance) + MinimumDistance
                    || distance >= Mathf.Max(currentDistance, endDistance) - MinimumDistance
                    || !IsSharpTurn(points[vertex - 1], points[vertex], points[vertex + 1]))
                {
                    continue;
                }

                var insertion = 1;
                while (insertion < sampleCount
                       && travelDirection * destination[insertion].DistanceAlongPath
                       > travelDirection * distance)
                {
                    insertion++;
                }

                if (Mathf.Abs(destination[insertion - 1].DistanceAlongPath - distance) <= MinimumDistance
                    || (insertion < sampleCount
                        && Mathf.Abs(destination[insertion].DistanceAlongPath - distance) <= MinimumDistance))
                {
                    continue;
                }

                for (var move = sampleCount; move > insertion; move--)
                {
                    destination[move] = destination[move - 1];
                }

                destination[insertion] = CreateSample(
                    points[vertex], times[vertex], currentTime, frames, mapper, trajectory);
                sampleCount++;
            }

            return sampleCount;
        }

        private static bool IsSharpTurn(Vector3 previous, Vector3 vertex, Vector3 next)
        {
            var incoming = new Vector2(vertex.x - previous.x, vertex.z - previous.z);
            var outgoing = new Vector2(next.x - vertex.x, next.z - vertex.z);
            return incoming.sqrMagnitude > MinimumDistance * MinimumDistance
                && outgoing.sqrMagnitude > MinimumDistance * MinimumDistance
                && Vector2.Dot(incoming.normalized, outgoing.normalized) < 0.98f;
        }

        public static int Fill(
            FittedTrajectory trajectory,
            IReadOnlyList<TelemetryFrame> frames,
            GeoCoordinateMapper mapper,
            float currentElapsedSeconds,
            int direction,
            float maximumDistanceWorld,
            WaterWakePathSample[] destination)
        {
            if (trajectory == null || mapper == null || destination == null || destination.Length == 0
                || trajectory.Times.Count == 0 || trajectory.Points.Count == 0
                || !IsFinite(currentElapsedSeconds))
            {
                return 0;
            }

            var times = trajectory.Times;
            var points = trajectory.Points;
            var count = Mathf.Min(times.Count, points.Count);
            if (!HasStrictlyIncreasingFiniteTimes(times, count) || !HasFinitePoints(points, count))
            {
                return 0;
            }

            var currentTime = Mathf.Clamp(currentElapsedSeconds, times[0], times[count - 1]);
            var travelDirection = direction < 0 ? -1 : 1;
            var historyStep = -travelDirection;
            var currentPosition = trajectory.PositionAt(currentTime);
            if (!IsFinite(currentPosition))
            {
                return 0;
            }

            var requestedDistance = IsFinite(maximumDistanceWorld)
                ? Mathf.Max(0f, maximumDistanceWorld)
                : 0f;
            var currentSample = CreateSample(currentPosition, currentTime, currentTime, frames, mapper, trajectory);
            destination[0] = currentSample;
            if (requestedDistance <= MinimumDistance || count == 1 || destination.Length == 1)
            {
                return 1;
            }

            var firstHistoryIndex = FindHistoryIndex(times, count, currentTime, historyStep);
            var totalDistance = MeasureHistoryDistance(
                times, points, count, firstHistoryIndex, historyStep, currentPosition, requestedDistance);
            if (totalDistance <= MinimumDistance)
            {
                return 1;
            }

            var outputCount = Mathf.Min(
                Mathf.Min(destination.Length, MaximumSampleCount),
                Mathf.CeilToInt(totalDistance) + 1);
            outputCount = Mathf.Max(2, outputCount);
            FillEvenlyByDistance(
                destination,
                outputCount,
                frames,
                mapper,
                times,
                points,
                count,
                firstHistoryIndex,
                historyStep,
                currentTime,
                currentPosition,
                totalDistance,
                trajectory);
            return outputCount;
        }

        private static void FillEvenlyByDistance(
            WaterWakePathSample[] destination,
            int outputCount,
            IReadOnlyList<TelemetryFrame> frames,
            GeoCoordinateMapper mapper,
            IReadOnlyList<float> times,
            IReadOnlyList<Vector3> points,
            int pointCount,
            int firstHistoryIndex,
            int historyStep,
            float currentTime,
            Vector3 currentPosition,
            float totalDistance,
            FittedTrajectory trajectory)
        {
            var currentSample = CreateSample(currentPosition, currentTime, currentTime, frames, mapper, trajectory);
            destination[0] = currentSample;
            var segmentStartPosition = currentPosition;
            var segmentStartTime = currentTime;
            var traversedDistance = 0f;
            var nextOutput = 1;

            for (var index = firstHistoryIndex;
                 index >= 0 && index < pointCount && nextOutput < outputCount;
                 index += historyStep)
            {
                var segmentEndPosition = points[index];
                var segmentEndTime = times[index];
                var segmentLength = HorizontalDistance(segmentStartPosition, segmentEndPosition);
                if (segmentLength <= MinimumDistance)
                {
                    segmentStartPosition = segmentEndPosition;
                    segmentStartTime = segmentEndTime;
                    continue;
                }

                var segmentEndDistance = traversedDistance + segmentLength;
                while (nextOutput < outputCount)
                {
                    var targetDistance = totalDistance * nextOutput / (outputCount - 1f);
                    if (targetDistance > segmentEndDistance + MinimumDistance)
                    {
                        break;
                    }

                    var segmentProgress = Mathf.Clamp01((targetDistance - traversedDistance) / segmentLength);
                    var samplePosition = Vector3.LerpUnclamped(segmentStartPosition, segmentEndPosition, segmentProgress);
                    var sampleTime = Mathf.LerpUnclamped(segmentStartTime, segmentEndTime, segmentProgress);
                    destination[nextOutput] = CreateSample(
                        samplePosition, sampleTime, currentTime, frames, mapper, trajectory);
                    nextOutput++;
                }

                traversedDistance = segmentEndDistance;
                segmentStartPosition = segmentEndPosition;
                segmentStartTime = segmentEndTime;
            }

            while (nextOutput < outputCount)
            {
                destination[nextOutput] = destination[nextOutput - 1];
                nextOutput++;
            }
        }

        private static float MeasureHistoryDistance(
            IReadOnlyList<float> times,
            IReadOnlyList<Vector3> points,
            int pointCount,
            int firstHistoryIndex,
            int historyStep,
            Vector3 currentPosition,
            float maximumDistance)
        {
            var totalDistance = 0f;
            var startPosition = currentPosition;
            for (var index = firstHistoryIndex;
                 index >= 0 && index < pointCount && totalDistance < maximumDistance;
                 index += historyStep)
            {
                if (!IsFinite(times[index]) || !IsFinite(points[index]))
                {
                    return 0f;
                }

                var segmentLength = HorizontalDistance(startPosition, points[index]);
                totalDistance += segmentLength;
                startPosition = points[index];
            }

            return Mathf.Min(totalDistance, maximumDistance);
        }

        private static int FindHistoryIndex(
            IReadOnlyList<float> times,
            int count,
            float currentTime,
            int historyStep)
        {
            var lower = 0;
            var upper = count;
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (times[middle] < currentTime)
                {
                    lower = middle + 1;
                }
                else
                {
                    upper = middle;
                }
            }

            return historyStep < 0 ? lower - 1 : lower;
        }

        private static WaterWakePathSample CreateSample(
            Vector3 position,
            float sampleTime,
            float currentElapsedSeconds,
            IReadOnlyList<TelemetryFrame> frames,
            GeoCoordinateMapper mapper,
            FittedTrajectory trajectory)
        {
            var depth = TryInterpolateDepth(frames, sampleTime, out var interpolatedDepth)
                ? interpolatedDepth
                : -mapper.UnmapPosition(position).y;
            depth = IsFinite(depth) ? Mathf.Max(0f, depth) : 0f;
            return new WaterWakePathSample(
                new Vector2(position.x, position.z),
                depth,
                Mathf.Max(0f, Mathf.Abs(currentElapsedSeconds - sampleTime)),
                trajectory.HorizontalDistanceAt(sampleTime));
        }

        private static bool TryInterpolateDepth(
            IReadOnlyList<TelemetryFrame> frames,
            float elapsedSeconds,
            out float depthM)
        {
            depthM = 0f;
            if (frames == null || frames.Count == 0 || !IsFinite(elapsedSeconds))
            {
                return false;
            }

            var lower = 0;
            var upper = frames.Count;
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (!IsFinite(frames[middle].ElapsedSeconds) || frames[middle].ElapsedSeconds < elapsedSeconds)
                {
                    lower = middle + 1;
                }
                else
                {
                    upper = middle;
                }
            }

            if (lower < frames.Count && Mathf.Abs(frames[lower].ElapsedSeconds - elapsedSeconds) <= MinimumDistance)
            {
                for (var index = lower; index < frames.Count && Mathf.Abs(frames[index].ElapsedSeconds - elapsedSeconds) <= MinimumDistance; index++)
                {
                    if (IsFinite(frames[index].DepthM))
                    {
                        depthM = frames[index].DepthM;
                        return true;
                    }
                }

                return false;
            }

            var leftIndex = lower - 1;
            var rightIndex = lower;
            if (leftIndex < 0 || rightIndex >= frames.Count
                || !IsFinite(frames[leftIndex].DepthM) || !IsFinite(frames[rightIndex].DepthM))
            {
                return false;
            }

            var leftTime = frames[leftIndex].ElapsedSeconds;
            var rightTime = frames[rightIndex].ElapsedSeconds;
            var timeSpan = rightTime - leftTime;
            if (!IsFinite(leftTime) || !IsFinite(rightTime) || timeSpan <= MinimumDistance)
            {
                depthM = frames[leftIndex].DepthM;
                return true;
            }

            var progress = Mathf.Clamp01((elapsedSeconds - leftTime) / timeSpan);
            depthM = Mathf.Lerp(frames[leftIndex].DepthM, frames[rightIndex].DepthM, progress);
            return IsFinite(depthM);
        }

        private static bool HasStrictlyIncreasingFiniteTimes(IReadOnlyList<float> times, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (!IsFinite(times[index]) || (index > 0 && times[index] <= times[index - 1]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasFinitePoints(IReadOnlyList<Vector3> points, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (!IsFinite(points[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            return Vector2.Distance(new Vector2(left.x, left.z), new Vector2(right.x, right.z));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
