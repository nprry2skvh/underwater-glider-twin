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

        public static Vector3[] SampleSmooth(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, int maxPoints)
        {
            var fitted = Fit(frames, mapper, maxPoints);
            return fitted.Points as Vector3[] ?? new List<Vector3>(fitted.Points).ToArray();
        }

        public static FittedTrajectory Fit(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, int maxPoints)
        {
            if (frames == null || mapper == null || frames.Count == 0)
            {
                return new FittedTrajectory(new[] { 0f }, new[] { Vector3.zero });
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
                return new FittedTrajectory(new[] { 0f }, new[] { Vector3.zero });
            }

            var count = Math.Min(Math.Max(1, maxPoints), validFrames.Count);
            var preservedIndexes = BuildPreservedIndexes(validFrames);
            var sourceIndexes = BuildSampleIndexes(validFrames.Count, count, preservedIndexes);
            var samples = new List<ControlSample>(sourceIndexes.Count);
            var step = validFrames.Count <= 1 ? 1f : (validFrames.Count - 1f) / Math.Max(1, count - 1);
            var baseRadius = Mathf.Clamp(Mathf.CeilToInt(step * 0.35f), 1, 48);

            for (var i = 0; i < sourceIndexes.Count; i++)
            {
                var sourceIndex = sourceIndexes[i];
                var radius = IsStateBoundary(validFrames, sourceIndex) ? Mathf.Max(1, baseRadius / 2) : baseRadius;
                var point = RobustNeighborhoodPoint(validFrames, sourceIndex, radius, mapper);
                if (sourceIndex == 0)
                {
                    point = mapper.Map(validFrames[0]);
                }
                else if (sourceIndex == validFrames.Count - 1)
                {
                    point = mapper.Map(validFrames[validFrames.Count - 1]);
                }

                samples.Add(new ControlSample(validFrames[sourceIndex].ElapsedSeconds, point));
            }

            if (!HasStrictlyIncreasingTimes(samples))
            {
                return BuildLinearTrajectory(samples);
            }

            var smoothedSamples = new List<ControlSample>(samples.Count);
            for (var i = 0; i < samples.Count; i++)
            {
                var smoothedPoint = EvaluateLowess(samples, samples[i].Time);
                smoothedSamples.Add(new ControlSample(samples[i].Time, smoothedPoint));
            }

            // Keep mission endpoints exact while using a single continuous cubic
            // curve between them. Evaluating a fresh local LOWESS polynomial for
            // every rendered point creates visible kinks when its neighborhood
            // changes from one source sample to the next.
            smoothedSamples[0] = samples[0];
            smoothedSamples[smoothedSamples.Count - 1] = samples[samples.Count - 1];

            var outputCount = Mathf.Min(Mathf.Max(2, maxPoints), Mathf.Max(2, (samples.Count - 1) * 4 + 1));
            var outputTimes = new float[outputCount];
            var outputPoints = new Vector3[outputCount];
            for (var outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                var normalized = outputCount == 1 ? 0f : outputIndex / (float)(outputCount - 1);
                var targetTime = Mathf.Lerp(samples[0].Time, samples[samples.Count - 1].Time, normalized);
                outputTimes[outputIndex] = targetTime;
                outputPoints[outputIndex] = EvaluateCubic(smoothedSamples, targetTime);
            }

            outputPoints[0] = samples[0].Point;
            outputPoints[outputPoints.Length - 1] = samples[samples.Count - 1].Point;
            return new FittedTrajectory(outputTimes, outputPoints);
        }

        private static Vector3 EvaluateCubic(IReadOnlyList<ControlSample> samples, float targetTime)
        {
            if (samples.Count == 1 || targetTime <= samples[0].Time)
            {
                return samples[0].Point;
            }

            if (targetTime >= samples[samples.Count - 1].Time)
            {
                return samples[samples.Count - 1].Point;
            }

            var upper = 1;
            while (upper < samples.Count && samples[upper].Time < targetTime)
            {
                upper++;
            }

            var lower = upper - 1;
            var left = samples[lower];
            var right = samples[upper];
            var segmentDuration = Mathf.Max(0.000001f, right.Time - left.Time);
            var u = Mathf.Clamp01((targetTime - left.Time) / segmentDuration);
            var previous = samples[Mathf.Max(0, lower - 1)];
            var next = samples[Mathf.Min(samples.Count - 1, upper + 1)];
            var leftTangent = lower == 0
                ? right.Point - left.Point
                : (right.Point - previous.Point)
                    * (segmentDuration / Mathf.Max(0.000001f, right.Time - previous.Time));
            var rightTangent = upper == samples.Count - 1
                ? right.Point - left.Point
                : (next.Point - left.Point)
                    * (segmentDuration / Mathf.Max(0.000001f, next.Time - left.Time));

            var u2 = u * u;
            var u3 = u2 * u;
            var h00 = 2f * u3 - 3f * u2 + 1f;
            var h10 = u3 - 2f * u2 + u;
            var h01 = -2f * u3 + 3f * u2;
            var h11 = u3 - u2;
            var result = h00 * left.Point
                + h10 * leftTangent
                + h01 * right.Point
                + h11 * rightTangent;

            // Do not allow the interpolator to invent a large overshoot between
            // two physically generated samples, especially at a turnaround.
            result.x = Mathf.Clamp(result.x, Mathf.Min(left.Point.x, right.Point.x), Mathf.Max(left.Point.x, right.Point.x));
            result.y = Mathf.Clamp(result.y, Mathf.Min(left.Point.y, right.Point.y), Mathf.Max(left.Point.y, right.Point.y));
            result.z = Mathf.Clamp(result.z, Mathf.Min(left.Point.z, right.Point.z), Mathf.Max(left.Point.z, right.Point.z));
            return result;
        }

        private readonly struct ControlSample
        {
            public readonly float Time;
            public readonly Vector3 Point;

            public ControlSample(float time, Vector3 point)
            {
                Time = time;
                Point = point;
            }
        }

        private static Vector3 RobustNeighborhoodPoint(IReadOnlyList<TelemetryFrame> frames, int centerIndex, int radius, GeoCoordinateMapper mapper)
        {
            var start = Mathf.Max(0, centerIndex - radius);
            var end = Mathf.Min(frames.Count - 1, centerIndex + radius);
            var points = new List<Vector3>(end - start + 1);
            for (var i = start; i <= end; i++)
            {
                points.Add(mapper.Map(frames[i]));
            }

            points.Sort((left, right) => left.x.CompareTo(right.x));
            var x = points[points.Count / 2].x;
            points.Sort((left, right) => left.y.CompareTo(right.y));
            var y = points[points.Count / 2].y;
            points.Sort((left, right) => left.z.CompareTo(right.z));
            var z = points[points.Count / 2].z;
            return new Vector3(x, y, z);
        }

        private static bool IsStateBoundary(IReadOnlyList<TelemetryFrame> frames, int index)
        {
            return index > 0
                && !string.Equals(frames[index].RunState, frames[index - 1].RunState, StringComparison.Ordinal);
        }

        private static bool HasStrictlyIncreasingTimes(IReadOnlyList<ControlSample> samples)
        {
            for (var i = 1; i < samples.Count; i++)
            {
                if (samples[i].Time <= samples[i - 1].Time)
                {
                    return false;
                }
            }

            return true;
        }

        private static FittedTrajectory BuildLinearTrajectory(IReadOnlyList<ControlSample> samples)
        {
            var count = Mathf.Max(1, samples.Count);
            var times = new float[count];
            var points = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                times[i] = i;
                points[i] = samples[i].Point;
            }

            return new FittedTrajectory(times, points);
        }

        private static Vector3 EvaluateLowess(IReadOnlyList<ControlSample> samples, float targetTime)
        {
            if (samples.Count == 1)
            {
                return samples[0].Point;
            }

            var span = Mathf.Min(samples.Count, Mathf.Max(9, 21));
            var center = FindNearestSample(samples, targetTime);
            var start = Mathf.Clamp(center - span / 2, 0, samples.Count - span);
            var end = start + span - 1;
            var robustWeights = new double[span];
            for (var i = 0; i < robustWeights.Length; i++) robustWeights[i] = 1d;

            var polynomial = default(LowessPolynomial);
            for (var iteration = 0; iteration < 3; iteration++)
            {
                polynomial = FitPolynomial(samples, start, end, targetTime, robustWeights);
                if (iteration == 2) break;

                var residuals = new double[span];
                for (var local = 0; local < span; local++)
                {
                    residuals[local] = Vector3.Distance(samples[start + local].Point, polynomial.Evaluate(samples[start + local].Time));
                }

                var median = Median(residuals);
                var deviations = new double[span];
                for (var local = 0; local < span; local++) deviations[local] = Math.Abs(residuals[local] - median);
                var scale = Math.Max(0.001d, 1.4826d * Median(deviations));
                var cutoff = 6d * scale;
                for (var local = 0; local < span; local++)
                {
                    var normalized = residuals[local] / cutoff;
                    robustWeights[local] = normalized >= 1d ? 0d : Math.Pow(1d - normalized * normalized, 2d);
                }
            }

            return polynomial.Evaluate(targetTime);
        }

        private static int FindNearestSample(IReadOnlyList<ControlSample> samples, float targetTime)
        {
            var best = 0;
            var bestDistance = Mathf.Abs(samples[0].Time - targetTime);
            for (var i = 1; i < samples.Count; i++)
            {
                var distance = Mathf.Abs(samples[i].Time - targetTime);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static LowessPolynomial FitPolynomial(IReadOnlyList<ControlSample> samples, int start, int end, float targetTime, IReadOnlyList<double> robustWeights)
        {
            var bandwidth = Math.Max(0.000001d, samples[end].Time - samples[start].Time);
            var matrix = new double[3, 3];
            var rhsX = new double[3];
            var rhsY = new double[3];
            var rhsZ = new double[3];
            for (var index = start; index <= end; index++)
            {
                var local = index - start;
                var normalized = (samples[index].Time - targetTime) / bandwidth;
                var distance = Math.Abs(normalized);
                var tricube = distance >= 1d ? 0d : Math.Pow(1d - distance * distance * distance, 3d);
                var weight = tricube * robustWeights[local];
                var x = normalized;
                var x2 = x * x;
                var basis = new[] { 1d, x, x2 };
                for (var row = 0; row < 3; row++)
                {
                    for (var column = 0; column < 3; column++) matrix[row, column] += weight * basis[row] * basis[column];
                    rhsX[row] += weight * basis[row] * samples[index].Point.x;
                    rhsY[row] += weight * basis[row] * samples[index].Point.y;
                    rhsZ[row] += weight * basis[row] * samples[index].Point.z;
                }
            }

            if (!Solve3x3(matrix, rhsX, out var coefficientsX)
                || !Solve3x3(matrix, rhsY, out var coefficientsY)
                || !Solve3x3(matrix, rhsZ, out var coefficientsZ))
            {
                var weighted = Vector3.zero;
                var total = 0f;
                for (var index = start; index <= end; index++)
                {
                    var weight = (float)robustWeights[index - start];
                    weighted += samples[index].Point * weight;
                    total += weight;
                }

                return LowessPolynomial.Constant(total > 0.000001f ? weighted / total : samples[Mathf.Clamp(start, 0, samples.Count - 1)].Point);
            }

            return new LowessPolynomial(
                new Vector3((float)coefficientsX[0], (float)coefficientsY[0], (float)coefficientsZ[0]),
                new Vector3((float)coefficientsX[1], (float)coefficientsY[1], (float)coefficientsZ[1]),
                new Vector3((float)coefficientsX[2], (float)coefficientsY[2], (float)coefficientsZ[2]),
                targetTime,
                (float)bandwidth);
        }

        private readonly struct LowessPolynomial
        {
            private readonly Vector3 constant;
            private readonly Vector3 linear;
            private readonly Vector3 quadratic;
            private readonly float origin;
            private readonly float bandwidth;

            public LowessPolynomial(Vector3 constant, Vector3 linear, Vector3 quadratic, float origin, float bandwidth)
            {
                this.constant = constant;
                this.linear = linear;
                this.quadratic = quadratic;
                this.origin = origin;
                this.bandwidth = bandwidth;
            }

            public static LowessPolynomial Constant(Vector3 point) => new LowessPolynomial(point, Vector3.zero, Vector3.zero, 0f, 1f);

            public Vector3 Evaluate(float time)
            {
                var x = (time - origin) / Mathf.Max(0.000001f, bandwidth);
                return constant + linear * x + quadratic * x * x;
            }
        }

        private static double Median(IReadOnlyList<double> values)
        {
            var sorted = new double[values.Count];
            for (var i = 0; i < values.Count; i++) sorted[i] = values[i];
            Array.Sort(sorted);
            var middle = sorted.Length / 2;
            return sorted.Length % 2 == 0 ? 0.5d * (sorted[middle - 1] + sorted[middle]) : sorted[middle];
        }

        private static bool Solve3x3(double[,] input, double[] right, out double[] result)
        {
            var matrix = new double[3, 4];
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++) matrix[row, column] = input[row, column];
                matrix[row, 3] = right[row];
            }

            for (var pivot = 0; pivot < 3; pivot++)
            {
                var best = pivot;
                for (var row = pivot + 1; row < 3; row++)
                {
                    if (Math.Abs(matrix[row, pivot]) > Math.Abs(matrix[best, pivot])) best = row;
                }

                if (Math.Abs(matrix[best, pivot]) < 0.000000001d)
                {
                    result = null;
                    return false;
                }

                if (best != pivot)
                {
                    for (var column = pivot; column < 4; column++)
                    {
                        var value = matrix[pivot, column];
                        matrix[pivot, column] = matrix[best, column];
                        matrix[best, column] = value;
                    }
                }

                var divisor = matrix[pivot, pivot];
                for (var column = pivot; column < 4; column++) matrix[pivot, column] /= divisor;
                for (var row = 0; row < 3; row++)
                {
                    if (row == pivot) continue;
                    var factor = matrix[row, pivot];
                    for (var column = pivot; column < 4; column++) matrix[row, column] -= factor * matrix[pivot, column];
                }
            }

            result = new[] { matrix[0, 3], matrix[1, 3], matrix[2, 3] };
            return true;
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
