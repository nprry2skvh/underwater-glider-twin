using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class ArtifactPredictor : IPredictor
    {
        private readonly string name;
        private string modelDirectory = string.Empty;

        public ArtifactPredictor(string name)
        {
            this.name = name;
        }

        public string GetName()
        {
            return name;
        }

        public void LoadModel(string modelDirectoryPath)
        {
            modelDirectory = modelDirectoryPath ?? string.Empty;
        }

        public PredictionResult Predict(PredictionContext context)
        {
            var metadataPath = Path.Combine(modelDirectory, "metadata.json");
            var predictionsPath = Path.Combine(modelDirectory, "validation_predictions.npz");
            var available = File.Exists(metadataPath) || File.Exists(predictionsPath);
            var status = available
                ? $"{name} artifacts detected; runtime inference pending integration"
                : $"{name} artifacts missing in {modelDirectory}";
            return new PredictionResult(
                name,
                status,
                System.Array.Empty<Vector3>(),
                System.Array.Empty<Vector3>(),
                context.Window.FutureStartIndex,
                context.Window.FutureEndIndex,
                new PredictionMetrics(0f, 0f, 0f, 0f, 0f, 0f));
        }

        public void Release()
        {
        }
    }
}
