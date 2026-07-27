namespace UnderwaterGliderTwin.Prediction
{
    public readonly struct PredictionMetrics
    {
        public PredictionMetrics(float rmseMeters, float maeMeters, float currentErrorMeters, float maximumErrorMeters, float confidence01, float computeMilliseconds)
        {
            RmseMeters = rmseMeters;
            MaeMeters = maeMeters;
            CurrentErrorMeters = currentErrorMeters;
            MaximumErrorMeters = maximumErrorMeters;
            Confidence01 = confidence01;
            ComputeMilliseconds = computeMilliseconds;
        }

        public float RmseMeters { get; }
        public float MaeMeters { get; }
        public float CurrentErrorMeters { get; }
        public float MaximumErrorMeters { get; }
        public float Confidence01 { get; }
        public float ComputeMilliseconds { get; }
    }
}
