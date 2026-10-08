using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    // Owns frozen outputs and append-only scoring. Rendering units never enter this boundary.
    public sealed class ForecastLedger
    {
        private readonly Dictionary<string, FrozenForecast> requests = new Dictionary<string, FrozenForecast>();
        private readonly List<FrozenForecast> forecasts = new List<FrozenForecast>();
        private readonly List<ForecastScore> scores = new List<ForecastScore>();
        private readonly HashSet<string> scoredKeys = new HashSet<string>();
        private readonly List<Observation> observations = new List<Observation>();

        public ForecastLedger(string runId, float timeToleranceSeconds = .5f,
            float maximumInterpolationGapSeconds = 20f, float waitDeadlineSeconds = 60f)
        {
            if (string.IsNullOrWhiteSpace(runId) || !Finite(timeToleranceSeconds) || timeToleranceSeconds < 0
                || !Finite(maximumInterpolationGapSeconds) || maximumInterpolationGapSeconds <= 0
                || !Finite(waitDeadlineSeconds) || waitDeadlineSeconds < 0) throw new ArgumentException("Invalid scoring configuration");
            RunId = runId;
            TimeToleranceSeconds = timeToleranceSeconds;
            MaximumInterpolationGapSeconds = maximumInterpolationGapSeconds;
            WaitDeadlineSeconds = waitDeadlineSeconds;
        }

        public string RunId { get; }
        public float TimeToleranceSeconds { get; }
        public float MaximumInterpolationGapSeconds { get; }
        public float WaitDeadlineSeconds { get; }
        public IReadOnlyList<FrozenForecast> Forecasts => forecasts.AsReadOnly();
        public IReadOnlyList<ForecastScore> Scores => scores.AsReadOnly();

        public FrozenForecast Publish(string requestKey, TelemetryFrame origin, IReadOnlyList<TelemetryFrame> predicted,
            string branchId, bool isSimulation, int profileSequence, string modelHash,
            string inputDigest, string currentVersion, string failure)
        {
            if (requests.TryGetValue(requestKey, out var previous)) return previous;
            if (predicted == null || string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentException("Invalid forecast request");
            var lastTime = origin.ElapsedSeconds;
            foreach (var point in predicted)
            {
                if (!Finite(point.ElapsedSeconds) || point.ElapsedSeconds <= lastTime || !ValidPosition(point))
                    throw new ArgumentException("Forecast points require ordered finite physical coordinates");
                lastTime = point.ElapsedSeconds;
            }
            var record = new FrozenForecast(RunId, requestKey, origin, predicted, branchId, isSimulation,
                profileSequence, modelHash, inputDigest, currentVersion, failure,
                TimeToleranceSeconds, MaximumInterpolationGapSeconds, WaitDeadlineSeconds);
            requests.Add(requestKey, record);
            forecasts.Add(record);
            return record;
        }

        public bool TryGet(string requestKey, out FrozenForecast forecast) => requests.TryGetValue(requestKey, out forecast);

        public PredictionMetrics GetMetrics(string forecastId, float computeMilliseconds)
            => GetMetricsThrough(forecastId, computeMilliseconds, float.PositiveInfinity);

        public PredictionMetrics GetMetricsThrough(string forecastId, float computeMilliseconds,
            float throughTargetSeconds)
        {
            var count = 0;
            var square = 0d;
            var absolute = 0d;
            var first = double.NaN;
            var maximum = 0d;
            foreach (var score in scores)
            {
                if (score.ForecastId != forecastId || score.Status != "scored"
                    || score.TargetElapsedSeconds > throughTargetSeconds) continue;
                if (count == 0) first = score.PositionErrorMeters;
                count++;
                square += score.PositionErrorMeters * score.PositionErrorMeters;
                absolute += score.PositionErrorMeters;
                maximum = Math.Max(maximum, score.PositionErrorMeters);
            }
            return count == 0
                ? new PredictionMetrics(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, computeMilliseconds)
                : new PredictionMetrics((float)Math.Sqrt(square / count), (float)(absolute / count),
                    (float)first, (float)maximum, float.NaN, computeMilliseconds);
        }

        public void Observe(TelemetryFrame frame, float receivedSeconds, string branchId,
            bool isSimulation, bool hasPositionReference, string truthGrade)
        {
            if (!Finite(frame.ElapsedSeconds) || !Finite(receivedSeconds) || frame.ElapsedSeconds > receivedSeconds) return;
            // Replay duplicates do not grow the observation buffer.
            var duplicate = observations.Exists(o => o.Frame.ElapsedSeconds == frame.ElapsedSeconds
                && o.BranchId == branchId && o.IsSimulation == isSimulation && o.TruthGrade == truthGrade);
            if (!duplicate) observations.Add(new Observation(frame, receivedSeconds, branchId,
                isSimulation, hasPositionReference && ValidPosition(frame), truthGrade));
            ScoreAvailable(receivedSeconds);
            Expire(receivedSeconds);
        }

        public void Expire(float receivedSeconds)
        {
            foreach (var record in forecasts)
                for (var index = 0; index < record.Frames.Count; index++)
                    if (receivedSeconds >= record.Frames[index].ElapsedSeconds + WaitDeadlineSeconds)
                        Add(new ForecastScore(record, index, "missing", "unavailable"));
        }

        private void ScoreAvailable(float now)
        {
            foreach (var record in forecasts)
            {
                for (var index = 0; index < record.Frames.Count; index++)
                {
                    var predicted = record.Frames[index];
                    if (scoredKeys.Contains(Key(record.ForecastId, index)) || predicted.ElapsedSeconds > now
                        || now >= predicted.ElapsedSeconds + WaitDeadlineSeconds) continue;
                    if (!TryAlign(record, predicted.ElapsedSeconds, now, out var actual)) continue;
                    if (!actual.HasPosition)
                    {
                        Add(new ForecastScore(record, index, "no_position_reference", actual.TruthGrade));
                        continue;
                    }
                    var earthScale = 111320d * Math.Cos(record.Origin.LatitudeDeg * Math.PI / 180d);
                    var east = (actual.Frame.LongitudeDeg - predicted.LongitudeDeg) * earthScale;
                    var north = (actual.Frame.LatitudeDeg - predicted.LatitudeDeg) * 111320d;
                    var depth = Math.Abs(actual.Frame.DepthM - predicted.DepthM);
                    var horizontal = Math.Sqrt(east * east + north * north);
                    Add(new ForecastScore(record, index, "scored", actual.TruthGrade, horizontal,
                        Math.Sqrt(horizontal * horizontal + depth * depth), depth,
                        Math.Abs(Mathf.DeltaAngle(predicted.HeadingDeg, actual.Frame.HeadingDeg)),
                        Math.Abs(actual.Frame.PitchDeg - predicted.PitchDeg),
                        Math.Abs(actual.Frame.RollDeg - predicted.RollDeg)));
                }
            }
        }

        private bool TryAlign(FrozenForecast record, float target, float now, out Observation actual)
        {
            actual = default;
            Observation? before = null, after = null, nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var observed in observations)
            {
                if (observed.ReceivedSeconds > now || (observed.IsSimulation && observed.BranchId != record.BranchId)) continue;
                var seconds = observed.Frame.ElapsedSeconds;
                var distance = Math.Abs(seconds - target);
                if (distance <= TimeToleranceSeconds && distance < nearestDistance)
                {
                    nearest = observed;
                    nearestDistance = distance;
                }
                if (seconds <= target && (!before.HasValue || seconds > before.Value.Frame.ElapsedSeconds)) before = observed;
                if (seconds >= target && (!after.HasValue || seconds < after.Value.Frame.ElapsedSeconds)) after = observed;
            }
            if (nearest.HasValue) { actual = nearest.Value; return true; }
            if (!before.HasValue || !after.HasValue) return false;
            var lower = before.Value;
            var upper = after.Value;
            var gap = upper.Frame.ElapsedSeconds - lower.Frame.ElapsedSeconds;
            // Do not interpolate across truth products, branches or a missing position.
            if (gap <= 0f || gap > MaximumInterpolationGapSeconds || lower.BranchId != upper.BranchId
                || lower.IsSimulation != upper.IsSimulation || lower.TruthGrade != upper.TruthGrade
                || !lower.HasPosition || !upper.HasPosition) return false;
            var alpha = (target - lower.Frame.ElapsedSeconds) / gap;
            var first = lower.Frame;
            var last = upper.Frame;
            var frame = new TelemetryFrame(first.RowIndex, "scoring-interpolation", target,
                first.LongitudeDeg + (last.LongitudeDeg - first.LongitudeDeg) * alpha,
                first.LatitudeDeg + (last.LatitudeDeg - first.LatitudeDeg) * alpha,
                Mathf.Lerp(first.DepthM, last.DepthM, alpha), first.AltitudeM,
                Mathf.LerpAngle(first.HeadingDeg, last.HeadingDeg, alpha),
                Mathf.Lerp(first.PitchDeg, last.PitchDeg, alpha), Mathf.Lerp(first.RollDeg, last.RollDeg, alpha),
                first.Voltage24V, first.Current24A, first.BatteryPercent, first.WorkMode, first.RunState,
                first.TargetSegment, first.TargetHeadingDeg, first.TargetDepthM, first.TargetAltitudeM,
                first.PropellerRpm, first.PistonMm, first.TurnAngleDeg);
            actual = new Observation(frame, now, lower.BranchId, lower.IsSimulation, true, lower.TruthGrade);
            return true;
        }

        private void Add(ForecastScore score)
        {
            if (scoredKeys.Add(Key(score.ForecastId, score.TargetIndex))) scores.Add(score);
        }
        private string Key(string forecastId, int index) => RunId + ":" + forecastId + ":" + index + ":" + ForecastScore.MetricVersion;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool ValidPosition(TelemetryFrame frame) => TelemetryPositionUtility.HasUsableCoordinates(frame) && Finite(frame.DepthM);

        private readonly struct Observation
        {
            public Observation(TelemetryFrame frame, float received, string branch, bool simulation, bool hasPosition, string grade)
            { Frame = frame; ReceivedSeconds = received; BranchId = branch; IsSimulation = simulation; HasPosition = hasPosition; TruthGrade = grade; }
            public TelemetryFrame Frame { get; }
            public float ReceivedSeconds { get; }
            public string BranchId { get; }
            public bool IsSimulation { get; }
            public bool HasPosition { get; }
            public string TruthGrade { get; }
        }
    }
}
