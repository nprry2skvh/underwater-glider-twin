namespace UnderwaterGliderTwin.Prediction
{
    public static class RuntimePredictionState
    {
        public static PredictionModelKind ModelKind { get; private set; } = PredictionModelKind.XGBoost;
        public static float HorizonSeconds { get; private set; } = 900f;
        public static bool PredictionEnabled { get; private set; } = true;

        public static void SetModelKind(PredictionModelKind modelKind)
        {
            ModelKind = modelKind;
        }

        public static void SetHorizonSeconds(float seconds)
        {
            HorizonSeconds = UnityEngine.Mathf.Clamp(seconds, 30f, 7200f);
        }

        public static void SetEnabled(bool enabled)
        {
            PredictionEnabled = enabled;
        }
    }
}
