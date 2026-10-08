using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class PredictionResult
    {
        public PredictionResult(
            string predictorName,
            string status,
            Vector3[] predictedPoints,
            Vector3[] actualPoints,
            int startIndex,
            int endIndex,
            PredictionMetrics metrics,
            TelemetryFrame[] forecastFrames = null,
            float[] targetElapsedSeconds = null)
        {
            PredictorName = predictorName ?? string.Empty;
            Status = status ?? string.Empty;
            PredictedPoints = predictedPoints ?? System.Array.Empty<Vector3>();
            ActualPoints = actualPoints ?? System.Array.Empty<Vector3>();
            StartIndex = startIndex;
            EndIndex = endIndex;
            Metrics = metrics;
            ForecastFrames = forecastFrames ?? System.Array.Empty<TelemetryFrame>();
            TargetElapsedSeconds = targetElapsedSeconds ?? System.Array.Empty<float>();
        }

        public string PredictorName { get; }
        public string Status { get; }
        public Vector3[] PredictedPoints { get; }
        public Vector3[] ActualPoints { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }
        public PredictionMetrics Metrics { get; }
        public TelemetryFrame[] ForecastFrames { get; }
        public float[] TargetElapsedSeconds { get; }
    }
}
