namespace UnderwaterGliderTwin.Prediction
{
    public readonly struct PredictionRequest
    {
        public PredictionRequest(int currentIndex, int windowSize, int horizonPoints, float horizonSeconds)
        {
            CurrentIndex = currentIndex;
            WindowSize = windowSize;
            HorizonPoints = horizonPoints;
            HorizonSeconds = horizonSeconds;
        }

        public int CurrentIndex { get; }
        public int WindowSize { get; }
        public int HorizonPoints { get; }
        public float HorizonSeconds { get; }
    }
}
