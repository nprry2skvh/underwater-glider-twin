using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Mapping
{
    public sealed class FittedTrajectory
    {
        private readonly float[] times;
        private readonly Vector3[] points;
        private readonly float[] horizontalDistances;
        private readonly float[] horizontalMovingSeconds;

        public IReadOnlyList<float> Times => times;
        public IReadOnlyList<Vector3> Points => points;
        public IReadOnlyList<float> HorizontalDistances => horizontalDistances;
        public float StartTime => times.Length == 0 ? 0f : times[0];
        public float EndTime => times.Length == 0 ? 0f : times[times.Length - 1];
        public float TotalHorizontalDistance => horizontalDistances[horizontalDistances.Length - 1];

        public FittedTrajectory(IReadOnlyList<float> sampleTimes, IReadOnlyList<Vector3> samplePoints)
        {
            if (sampleTimes == null) throw new ArgumentNullException(nameof(sampleTimes));
            if (samplePoints == null) throw new ArgumentNullException(nameof(samplePoints));
            if (sampleTimes.Count != samplePoints.Count) throw new ArgumentException("Trajectory times and points must have the same length.");
            if (sampleTimes.Count == 0) throw new ArgumentException("Trajectory must contain at least one point.");

            times = new float[sampleTimes.Count];
            points = new Vector3[samplePoints.Count];
            horizontalDistances = new float[samplePoints.Count];
            horizontalMovingSeconds = new float[samplePoints.Count];
            for (var i = 0; i < times.Length; i++)
            {
                times[i] = sampleTimes[i];
                points[i] = samplePoints[i];
                if (i > 0)
                {
                    var step = new Vector2(points[i].x - points[i - 1].x, points[i].z - points[i - 1].z);
                    horizontalDistances[i] = horizontalDistances[i - 1] + step.magnitude;
                    horizontalMovingSeconds[i] = horizontalMovingSeconds[i - 1]
                        + (step.sqrMagnitude > 0.000001f * 0.000001f
                            ? Mathf.Max(0f, times[i] - times[i - 1])
                            : 0f);
                }
            }
        }

        public float HorizontalMovingDurationBetween(float firstElapsedSeconds, float secondElapsedSeconds)
        {
            return Mathf.Abs(
                HorizontalMovingTimeAt(secondElapsedSeconds)
                - HorizontalMovingTimeAt(firstElapsedSeconds));
        }

        private float HorizontalMovingTimeAt(float elapsedSeconds)
        {
            if (points.Length == 1 || elapsedSeconds <= times[0]) return 0f;
            if (elapsedSeconds >= times[times.Length - 1])
            {
                return horizontalMovingSeconds[horizontalMovingSeconds.Length - 1];
            }

            var upper = Array.BinarySearch(times, elapsedSeconds);
            if (upper >= 0) return horizontalMovingSeconds[upper];
            upper = ~upper;
            var lower = upper - 1;
            var span = Mathf.Max(0.000001f, times[upper] - times[lower]);
            return Mathf.LerpUnclamped(
                horizontalMovingSeconds[lower],
                horizontalMovingSeconds[upper],
                (elapsedSeconds - times[lower]) / span);
        }

        public float HorizontalDistanceAt(float elapsedSeconds)
        {
            if (points.Length == 1 || elapsedSeconds <= times[0]) return 0f;
            if (elapsedSeconds >= times[times.Length - 1]) return horizontalDistances[horizontalDistances.Length - 1];

            var upper = Array.BinarySearch(times, elapsedSeconds);
            if (upper >= 0) return horizontalDistances[upper];
            upper = ~upper;
            var lower = upper - 1;
            var span = Mathf.Max(0.000001f, times[upper] - times[lower]);
            return Mathf.LerpUnclamped(
                horizontalDistances[lower],
                horizontalDistances[upper],
                (elapsedSeconds - times[lower]) / span);
        }

        public void SampleAtHorizontalDistance(
            float requestedDistance,
            int direction,
            out Vector3 position,
            out float elapsedSeconds)
        {
            var distance = Mathf.Clamp(requestedDistance, 0f, TotalHorizontalDistance);
            var upper = Array.BinarySearch(horizontalDistances, distance);
            if (upper >= 0)
            {
                if (direction < 0)
                {
                    while (upper + 1 < horizontalDistances.Length && horizontalDistances[upper + 1] == distance)
                    {
                        upper++;
                    }
                }
                else
                {
                    while (upper > 0 && horizontalDistances[upper - 1] == distance)
                    {
                        upper--;
                    }
                }

                position = points[upper];
                elapsedSeconds = times[upper];
                return;
            }

            upper = ~upper;
            var lower = upper - 1;
            var span = horizontalDistances[upper] - horizontalDistances[lower];
            var progress = (distance - horizontalDistances[lower]) / Mathf.Max(0.000001f, span);
            position = Vector3.LerpUnclamped(points[lower], points[upper], progress);
            elapsedSeconds = Mathf.LerpUnclamped(times[lower], times[upper], progress);
        }

        public Vector3 PositionAt(float elapsedSeconds)
        {
            if (points.Length == 1 || elapsedSeconds <= times[0]) return points[0];
            if (elapsedSeconds >= times[times.Length - 1]) return points[points.Length - 1];

            var upper = Array.BinarySearch(times, elapsedSeconds);
            if (upper >= 0) return points[upper];
            upper = ~upper;
            var lower = upper - 1;
            var span = Mathf.Max(0.000001f, times[upper] - times[lower]);
            return Vector3.LerpUnclamped(points[lower], points[upper], (elapsedSeconds - times[lower]) / span);
        }
    }
}
