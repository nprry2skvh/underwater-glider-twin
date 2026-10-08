using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class PredictionSnapshot
    {
        public static readonly PredictionSnapshot Empty = new PredictionSnapshot(
            Vector3.zero,
            System.Array.Empty<Vector3>(),
            System.Array.Empty<Vector3>(),
            0f,
            0f,
            0f,
            0f,
            0f,
            0,
            0,
            "Idle",
            0f);

        public PredictionSnapshot(
            Vector3 originPoint,
            Vector3[] predictedPoints,
            Vector3[] actualPoints,
            float rmseMeters,
            float maeMeters,
            float currentErrorMeters,
            float maxErrorMeters,
            float confidence01,
            int startIndex,
            int endIndex,
            string status,
            float computeMilliseconds)
        {
            OriginPoint = originPoint;
            PredictedPoints = predictedPoints ?? System.Array.Empty<Vector3>();
            ActualPoints = actualPoints ?? System.Array.Empty<Vector3>();
            RmseMeters = rmseMeters;
            MaeMeters = maeMeters;
            CurrentErrorMeters = currentErrorMeters;
            MaxErrorMeters = maxErrorMeters;
            Confidence01 = confidence01;
            StartIndex = startIndex;
            EndIndex = endIndex;
            Status = status ?? string.Empty;
            ComputeMilliseconds = computeMilliseconds;
        }

        public Vector3 OriginPoint { get; }
        public Vector3[] PredictedPoints { get; }
        public Vector3[] ActualPoints { get; }
        public float RmseMeters { get; }
        public float MaeMeters { get; }
        public float CurrentErrorMeters { get; }
        public float MaxErrorMeters { get; }
        public float Confidence01 { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }
        public string Status { get; }
        public float ComputeMilliseconds { get; }
        public int SampleCount => PredictedPoints.Length;
        public bool HasScoredMetrics => !float.IsNaN(RmseMeters) && !float.IsInfinity(RmseMeters) && SampleCount > 1;
    }
}
