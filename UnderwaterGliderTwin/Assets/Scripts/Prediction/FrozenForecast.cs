using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class FrozenForecast
    {
        internal FrozenForecast(string runId, string requestKey, TelemetryFrame origin,
            IReadOnlyList<TelemetryFrame> frames, string branchId, bool simulation,
            int profileSequence, string modelHash, string inputDigest, string currentVersion, string failure,
            float timeToleranceSeconds, float maximumInterpolationGapSeconds, float waitDeadlineSeconds)
        {
            ForecastId = Guid.NewGuid().ToString("N");
            RunId = runId;
            RequestKey = requestKey;
            Origin = origin;
            Frames = new List<TelemetryFrame>(frames).AsReadOnly();
            BranchId = branchId;
            IsSimulation = simulation;
            ProfileSequence = profileSequence;
            ModelHash = modelHash;
            InputDigest = inputDigest;
            CurrentVersion = currentVersion;
            Failure = failure ?? string.Empty;
            TimeToleranceSeconds = timeToleranceSeconds;
            MaximumInterpolationGapSeconds = maximumInterpolationGapSeconds;
            WaitDeadlineSeconds = waitDeadlineSeconds;
        }

        public string ForecastId { get; }
        public string RunId { get; }
        public string RequestKey { get; }
        public TelemetryFrame Origin { get; }
        public IReadOnlyList<TelemetryFrame> Frames { get; }
        public string BranchId { get; }
        public bool IsSimulation { get; }
        public int ProfileSequence { get; }
        public string ModelHash { get; }
        public string InputDigest { get; }
        public string CurrentVersion { get; }
        public string Failure { get; }
        public float TimeToleranceSeconds { get; }
        public float MaximumInterpolationGapSeconds { get; }
        public float WaitDeadlineSeconds { get; }
        // TelemetryFrame has no receipt clock; do not invent operational provenance.
        public string InputMode => "sample_time_replay";
        public string AlgorithmVersion => "causal-forecast-v1";
    }

    public sealed class ForecastScore
    {
        public const string MetricVersion = "physical-local-end-v1";
        internal ForecastScore(FrozenForecast forecast, int targetIndex, string status, string truthGrade,
            double horizontal = double.NaN, double position = double.NaN, double depth = double.NaN,
            double heading = double.NaN, double pitch = double.NaN, double roll = double.NaN,
            float availableAtSeconds = float.NaN)
        {
            ForecastId = forecast.ForecastId;
            RunId = forecast.RunId;
            TargetIndex = targetIndex;
            TargetElapsedSeconds = forecast.Frames[targetIndex].ElapsedSeconds;
            Status = status;
            TruthGrade = truthGrade;
            HorizontalErrorMeters = horizontal;
            PositionErrorMeters = position;
            DepthErrorMeters = depth;
            HeadingErrorDegrees = heading;
            PitchErrorDegrees = pitch;
            RollErrorDegrees = roll;
            AvailableAtSeconds = availableAtSeconds;
        }

        public string ForecastId { get; }
        public string RunId { get; }
        public int TargetIndex { get; }
        public float TargetElapsedSeconds { get; }
        public float AvailableAtSeconds { get; }
        public string Status { get; }
        public string TruthGrade { get; }
        public double HorizontalErrorMeters { get; }
        public double PositionErrorMeters { get; }
        public double DepthErrorMeters { get; }
        public double HeadingErrorDegrees { get; }
        public double PitchErrorDegrees { get; }
        public double RollErrorDegrees { get; }
    }
}
