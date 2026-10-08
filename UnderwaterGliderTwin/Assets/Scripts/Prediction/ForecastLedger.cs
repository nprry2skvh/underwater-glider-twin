using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    // Immutable archives are separate from the indexed active scoring workload.
    public sealed class ForecastLedger
    {
        private readonly Dictionary<string, FrozenForecast> requests = new Dictionary<string, FrozenForecast>();
        private readonly List<FrozenForecast> forecasts = new List<FrozenForecast>();
        private readonly List<ForecastScore> scores = new List<ForecastScore>();
        private readonly Dictionary<string, List<ForecastScore>> scoresByForecast = new Dictionary<string, List<ForecastScore>>();
        private readonly SortedList<float, List<PendingTarget>> pending = new SortedList<float, List<PendingTarget>>();
        private readonly SortedList<float, List<Observation>> observations = new SortedList<float, List<Observation>>();
        private readonly HashSet<(float, string, bool, string)> observationKeys = new HashSet<(float, string, bool, string)>();
        private readonly IReadOnlyList<FrozenForecast> forecastView;
        private readonly IReadOnlyList<ForecastScore> scoreView;
        private float latestReceipt = float.NegativeInfinity;
        private long observationOrder;

        public ForecastLedger(string runId, float timeToleranceSeconds = .5f,
            float maximumInterpolationGapSeconds = 20f, float waitDeadlineSeconds = 60f)
        {
            if (string.IsNullOrWhiteSpace(runId) || !Finite(timeToleranceSeconds) || timeToleranceSeconds < 0
                || !Finite(maximumInterpolationGapSeconds) || maximumInterpolationGapSeconds <= 0
                || !Finite(waitDeadlineSeconds) || waitDeadlineSeconds < 0) throw new ArgumentException("Invalid scoring configuration");
            RunId = runId; TimeToleranceSeconds = timeToleranceSeconds;
            MaximumInterpolationGapSeconds = maximumInterpolationGapSeconds; WaitDeadlineSeconds = waitDeadlineSeconds;
            forecastView = forecasts.AsReadOnly(); scoreView = scores.AsReadOnly();
        }
        public string RunId { get; }
        public float TimeToleranceSeconds { get; }
        public float MaximumInterpolationGapSeconds { get; }
        public float WaitDeadlineSeconds { get; }
        public IReadOnlyList<FrozenForecast> Forecasts => forecastView;
        public IReadOnlyList<ForecastScore> Scores => scoreView;

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
            requests.Add(requestKey, record); forecasts.Add(record);
            scoresByForecast.Add(record.ForecastId, new List<ForecastScore>());
            for (var index = 0; index < record.Frames.Count; index++)
            {
                var target = new PendingTarget(record, index);
                // First backward visits replay original receipt events before expiry.
                if (TryFinalize(target, latestReceipt)) continue;
                var time = record.Frames[index].ElapsedSeconds;
                if (!pending.TryGetValue(time, out var list)) pending.Add(time, list = new List<PendingTarget>());
                list.Add(target);
            }
            return record;
        }
        public bool TryGet(string requestKey, out FrozenForecast forecast) => requests.TryGetValue(requestKey, out forecast);
        public PredictionMetrics GetMetrics(string forecastId, float computeMilliseconds)
            => GetMetricsThrough(forecastId, computeMilliseconds, float.PositiveInfinity);
        public PredictionMetrics GetMetricsThrough(string forecastId, float computeMilliseconds, float throughTargetSeconds)
        {
            var count = 0; var square = 0d; var absolute = 0d; var first = double.NaN; var maximum = 0d;
            if (scoresByForecast.TryGetValue(forecastId, out var selected))
                foreach (var score in selected)
                {
                    if (score.Status != "scored" || score.AvailableAtSeconds > throughTargetSeconds) continue;
                    if (count == 0) first = score.PositionErrorMeters;
                    count++; square += score.PositionErrorMeters * score.PositionErrorMeters;
                    absolute += score.PositionErrorMeters; maximum = Math.Max(maximum, score.PositionErrorMeters);
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
            if (observationKeys.Add((frame.ElapsedSeconds, branchId, isSimulation, truthGrade)))
            {
                if (!observations.TryGetValue(frame.ElapsedSeconds, out var list))
                    observations.Add(frame.ElapsedSeconds, list = new List<Observation>());
                list.Add(new Observation(frame, receivedSeconds, branchId, isSimulation,
                    hasPositionReference && ValidPosition(frame), truthGrade, observationOrder++));
            }
            latestReceipt = Math.Max(latestReceipt, receivedSeconds); ProcessPending(latestReceipt);
        }
        public void Expire(float receivedSeconds)
        {
            if (!Finite(receivedSeconds)) return;
            latestReceipt = Math.Max(latestReceipt, receivedSeconds); ProcessPending(latestReceipt);
        }
        private void ProcessPending(float now)
        {
            for (var timeIndex = UpperBound(pending.Keys, now) - 1; timeIndex >= 0; timeIndex--)
            {
                var targets = pending.Values[timeIndex];
                for (var index = targets.Count - 1; index >= 0; index--)
                    if (TryFinalize(targets[index], now)) targets.RemoveAt(index);
                if (targets.Count == 0) pending.RemoveAt(timeIndex);
            }
        }
        private bool TryFinalize(PendingTarget target, float now)
        {
            var record = target.Record; var predicted = record.Frames[target.Index];
            if (predicted.ElapsedSeconds > now) return false;
            if (TryFirstAlignment(record, predicted.ElapsedSeconds, now, out var actual, out var availability))
            {
                if (!actual.HasPosition)
                    Add(new ForecastScore(record, target.Index, "no_position_reference", actual.TruthGrade,
                        availableAtSeconds: availability));
                else
                {
                    var east = (actual.Frame.LongitudeDeg - predicted.LongitudeDeg) * 111320d * Math.Cos(record.Origin.LatitudeDeg * Math.PI / 180d);
                    var north = (actual.Frame.LatitudeDeg - predicted.LatitudeDeg) * 111320d;
                    var depth = Math.Abs(actual.Frame.DepthM - predicted.DepthM);
                    var horizontal = Math.Sqrt(east * east + north * north);
                    Add(new ForecastScore(record, target.Index, "scored", actual.TruthGrade, horizontal,
                        Math.Sqrt(horizontal * horizontal + depth * depth), depth,
                        Math.Abs(Mathf.DeltaAngle(predicted.HeadingDeg, actual.Frame.HeadingDeg)),
                        Math.Abs(actual.Frame.PitchDeg - predicted.PitchDeg),
                        Math.Abs(actual.Frame.RollDeg - predicted.RollDeg), availability));
                }
                return true;
            }
            var deadline = predicted.ElapsedSeconds + WaitDeadlineSeconds;
            if (now < deadline) return false;
            Add(new ForecastScore(record, target.Index, "missing", "unavailable", availableAtSeconds: deadline));
            return true;
        }
        private bool TryFirstAlignment(FrozenForecast record, float target, float now,
            out Observation actual, out float availability)
        {
            actual = default; availability = float.NaN;
            var radius = Math.Max(MaximumInterpolationGapSeconds, TimeToleranceSeconds);
            var candidates = new List<Observation>();
            for (var index = LowerBound(observations.Keys, target - radius);
                index < observations.Count && observations.Keys[index] <= target + radius; index++)
                foreach (var observed in observations.Values[index])
                    if (observed.ReceivedSeconds <= now && observed.ReceivedSeconds < target + WaitDeadlineSeconds
                        && (!observed.IsSimulation || observed.BranchId == record.BranchId)) candidates.Add(observed);
            candidates.Sort((a, b) => a.ReceivedSeconds != b.ReceivedSeconds
                ? a.ReceivedSeconds.CompareTo(b.ReceivedSeconds) : a.Order.CompareTo(b.Order));
            var visible = new List<Observation>(); var cursor = 0;
            while (cursor < candidates.Count)
            {
                var receipt = candidates[cursor].ReceivedSeconds;
                do { visible.Add(candidates[cursor++]); } while (cursor < candidates.Count && candidates[cursor].ReceivedSeconds == receipt);
                var eventTime = Math.Max(target, receipt);
                if (eventTime > now || eventTime >= target + WaitDeadlineSeconds) continue;
                if (!TryAlign(visible, target, out actual)) continue;
                availability = eventTime; return true;
            }
            return false;
        }
        private bool TryAlign(List<Observation> visible, float target, out Observation actual)
        {
            actual = default; Observation? before = null, after = null, nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var observed in visible)
            {
                var seconds = observed.Frame.ElapsedSeconds; var distance = Math.Abs(seconds - target);
                if (distance <= TimeToleranceSeconds && distance < nearestDistance) { nearest = observed; nearestDistance = distance; }
                if (seconds <= target && (!before.HasValue || seconds > before.Value.Frame.ElapsedSeconds)) before = observed;
                if (seconds >= target && (!after.HasValue || seconds < after.Value.Frame.ElapsedSeconds)) after = observed;
            }
            if (nearest.HasValue) { actual = nearest.Value; return true; }
            if (!before.HasValue || !after.HasValue) return false;
            var lower = before.Value; var upper = after.Value; var gap = upper.Frame.ElapsedSeconds - lower.Frame.ElapsedSeconds;
            if (gap <= 0f || gap > MaximumInterpolationGapSeconds || lower.BranchId != upper.BranchId
                || lower.IsSimulation != upper.IsSimulation || lower.TruthGrade != upper.TruthGrade
                || !lower.HasPosition || !upper.HasPosition) return false;
            var alpha = (target - lower.Frame.ElapsedSeconds) / gap; var first = lower.Frame; var last = upper.Frame;
            var frame = new TelemetryFrame(first.RowIndex, "scoring-interpolation", target,
                first.LongitudeDeg + (last.LongitudeDeg - first.LongitudeDeg) * alpha,
                first.LatitudeDeg + (last.LatitudeDeg - first.LatitudeDeg) * alpha,
                Mathf.Lerp(first.DepthM, last.DepthM, alpha), first.AltitudeM,
                Mathf.LerpAngle(first.HeadingDeg, last.HeadingDeg, alpha),
                Mathf.Lerp(first.PitchDeg, last.PitchDeg, alpha), Mathf.Lerp(first.RollDeg, last.RollDeg, alpha),
                first.Voltage24V, first.Current24A, first.BatteryPercent, first.WorkMode, first.RunState,
                first.TargetSegment, first.TargetHeadingDeg, first.TargetDepthM, first.TargetAltitudeM,
                first.PropellerRpm, first.PistonMm, first.TurnAngleDeg);
            actual = new Observation(frame, Math.Max(lower.ReceivedSeconds, upper.ReceivedSeconds), lower.BranchId,
                lower.IsSimulation, true, lower.TruthGrade, upper.Order); return true;
        }
        private void Add(ForecastScore score) { scores.Add(score); scoresByForecast[score.ForecastId].Add(score); }
        private static int LowerBound(IList<float> keys, float target)
        {
            var lower = 0; var upper = keys.Count;
            while (lower < upper) { var mid = lower + (upper - lower) / 2; if (keys[mid] < target) lower = mid + 1; else upper = mid; }
            return lower;
        }
        private static int UpperBound(IList<float> keys, float target)
        {
            var lower = 0; var upper = keys.Count;
            while (lower < upper) { var mid = lower + (upper - lower) / 2; if (keys[mid] <= target) lower = mid + 1; else upper = mid; }
            return lower;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool ValidPosition(TelemetryFrame frame) => TelemetryPositionUtility.HasUsableCoordinates(frame) && Finite(frame.DepthM);
        private readonly struct PendingTarget
        {
            public PendingTarget(FrozenForecast record, int index) { Record = record; Index = index; }
            public FrozenForecast Record { get; }
            public int Index { get; }
        }
        private readonly struct Observation
        {
            public Observation(TelemetryFrame frame, float received, string branch, bool simulation, bool hasPosition, string grade, long order)
            { Frame = frame; ReceivedSeconds = received; BranchId = branch; IsSimulation = simulation; HasPosition = hasPosition; TruthGrade = grade; Order = order; }
            public TelemetryFrame Frame { get; }
            public float ReceivedSeconds { get; }
            public string BranchId { get; }
            public bool IsSimulation { get; }
            public bool HasPosition { get; }
            public string TruthGrade { get; }
            public long Order { get; }
        }
    }
}
