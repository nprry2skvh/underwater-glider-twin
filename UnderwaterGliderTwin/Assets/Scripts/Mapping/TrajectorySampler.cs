using System;
using System.Collections.Generic;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Mapping
{
    public static class TrajectorySampler
    {
        public static Vector3[] Sample(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, int maxPoints)
        {
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<Vector3>();
            }

            var validFrames = new List<TelemetryFrame>(frames.Count);
            for (var i = 0; i < frames.Count; i++)
            {
                if (TelemetryPositionUtility.HasUsableCoordinates(frames[i]))
                {
                    validFrames.Add(frames[i]);
                }
            }

            if (validFrames.Count == 0)
            {
                return Array.Empty<Vector3>();
            }

            var count = Math.Min(Math.Max(1, maxPoints), validFrames.Count);
            var preservedIndexes = BuildPreservedIndexes(validFrames);
            var sourceIndexes = BuildSampleIndexes(validFrames.Count, count, preservedIndexes);
            var points = new Vector3[sourceIndexes.Count];
            var step = validFrames.Count <= 1 ? 1f : (validFrames.Count - 1f) / Math.Max(1, count - 1);
            var averagingRadius = Mathf.Clamp(Mathf.CeilToInt(step * 0.35f), 1, 48);

            for (var i = 0; i < sourceIndexes.Count; i++)
            {
                var sourceIndex = sourceIndexes[i];
                if (!preservedIndexes.Contains(sourceIndex)
                    && TelemetryKinematicsUtility.TryGetAveragedCoordinate(validFrames, sourceIndex, averagingRadius, out var longitudeDeg, out var latitudeDeg, out var depthM))
                {
                    var smoothedFrame = new TelemetryFrame(
                        validFrames[sourceIndex].RowIndex,
                        validFrames[sourceIndex].RawTime,
                        validFrames[sourceIndex].ElapsedSeconds,
                        longitudeDeg,
                        latitudeDeg,
                        depthM,
                        validFrames[sourceIndex].AltitudeM,
                        validFrames[sourceIndex].HeadingDeg,
                        validFrames[sourceIndex].PitchDeg,
                        validFrames[sourceIndex].RollDeg,
                        validFrames[sourceIndex].Voltage24V,
                        validFrames[sourceIndex].Current24A,
                        validFrames[sourceIndex].BatteryPercent,
                        validFrames[sourceIndex].WorkMode,
                        validFrames[sourceIndex].RunState,
                        validFrames[sourceIndex].TargetSegment,
                        validFrames[sourceIndex].TargetHeadingDeg,
                        validFrames[sourceIndex].TargetDepthM,
                        validFrames[sourceIndex].TargetAltitudeM,
                        validFrames[sourceIndex].PropellerRpm,
                        validFrames[sourceIndex].PistonMm,
                        validFrames[sourceIndex].TurnAngleDeg);
                    points[i] = mapper.Map(smoothedFrame);
                }
                else
                {
                    points[i] = mapper.Map(validFrames[sourceIndex]);
                }
            }

            return points;
        }

        private static HashSet<int> BuildPreservedIndexes(IReadOnlyList<TelemetryFrame> frames)
        {
            var indexes = new HashSet<int>();
            if (frames.Count == 0)
            {
                return indexes;
            }

            indexes.Add(0);
            indexes.Add(frames.Count - 1);
            for (var i = 0; i < frames.Count; i++)
            {
                var isTurnaround = string.Equals(frames[i].RunState, "Turnaround", StringComparison.Ordinal);
                var isStateBoundary = i > 0 && !string.Equals(frames[i].RunState, frames[i - 1].RunState, StringComparison.Ordinal);
                if (!isTurnaround && !isStateBoundary)
                {
                    continue;
                }

                indexes.Add(i);
                if (isStateBoundary)
                {
                    indexes.Add(Mathf.Max(0, i - 1));
                    indexes.Add(Mathf.Min(frames.Count - 1, i + 1));
                }
            }

            return indexes;
        }

        private static List<int> BuildSampleIndexes(int frameCount, int maximumCount, HashSet<int> preservedIndexes)
        {
            var selectedIndexes = new SortedSet<int>();
            foreach (var index in preservedIndexes)
            {
                selectedIndexes.Add(index);
            }

            if (selectedIndexes.Count > maximumCount)
            {
                var preserved = new List<int>(selectedIndexes);
                selectedIndexes.Clear();
                for (var i = 0; i < maximumCount; i++)
                {
                    var sourceIndex = Mathf.Clamp(
                        Mathf.RoundToInt(i * (preserved.Count - 1f) / Math.Max(1, maximumCount - 1)),
                        0,
                        preserved.Count - 1);
                    selectedIndexes.Add(preserved[sourceIndex]);
                }
            }

            var remainingCount = maximumCount - selectedIndexes.Count;
            for (var i = 0; i < remainingCount; i++)
            {
                var sourceIndex = remainingCount == 1
                    ? frameCount / 2
                    : Mathf.RoundToInt(i * (frameCount - 1f) / (remainingCount - 1));
                selectedIndexes.Add(Mathf.Clamp(sourceIndex, 0, frameCount - 1));
            }

            for (var i = 0; selectedIndexes.Count < maximumCount && i < frameCount; i++)
            {
                selectedIndexes.Add(i);
            }

            return new List<int>(selectedIndexes);
        }
    }
}
