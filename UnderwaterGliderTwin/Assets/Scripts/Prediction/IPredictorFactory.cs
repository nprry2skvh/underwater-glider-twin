namespace UnderwaterGliderTwin.Prediction
{
    public interface IPredictorFactory
    {
        bool TryCreate(string root, out IPredictor predictor, out string error);
    }

    public sealed class XGBoostPredictorFactory : IPredictorFactory
    {
        public bool TryCreate(string root, out IPredictor predictor, out string error)
        {
            return XGBoostArtifactValidator.TryCreatePredictor(root, out predictor, out error);
        }
    }
}
