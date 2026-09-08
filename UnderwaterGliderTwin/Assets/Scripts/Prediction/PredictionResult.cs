using UnityEngine;

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
            PredictionMetrics metrics)
        {
            PredictorName = predictorName ?? string.Empty;
            Status = status ?? string.Empty;
            PredictedPoints = predictedPoints ?? System.Array.Empty<Vector3>();
            ActualPoints = actualPoints ?? System.Array.Empty<Vector3>();
            StartIndex = startIndex;
            EndIndex = endIndex;
            Metrics = metrics;
        }

        public string PredictorName { get; }
        public string Status { get; }
        public Vector3[] PredictedPoints { get; }
        public Vector3[] ActualPoints { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }
        public PredictionMetrics Metrics { get; }
    }
}
