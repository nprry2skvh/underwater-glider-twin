namespace UnderwaterGliderTwin.Prediction
{
    public interface IPredictor
    {
        string GetName();
        void LoadModel(string modelDirectory);
        PredictionResult Predict(PredictionContext context);
        void Release();
    }
}
