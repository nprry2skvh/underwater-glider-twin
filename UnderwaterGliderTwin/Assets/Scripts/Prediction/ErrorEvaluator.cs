using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    public static class ErrorEvaluator
    {
        public static PredictionMetrics Evaluate(Vector3[] predictedPoints, Vector3[] actualPoints, float computeMilliseconds)
        {
            if (predictedPoints == null || actualPoints == null || predictedPoints.Length == 0 || actualPoints.Length == 0)
            {
                return new PredictionMetrics(0f, 0f, 0f, 0f, 0f, computeMilliseconds);
            }

            var count = Mathf.Min(predictedPoints.Length, actualPoints.Length);
            var squaredErrorSum = 0f;
            var absoluteErrorSum = 0f;
            var currentError = 0f;
            var maxError = 0f;

            for (var i = 0; i < count; i++)
            {
                var error = Vector3.Distance(predictedPoints[i], actualPoints[i]);
                if (i == 0)
                {
                    currentError = error;
                }

                squaredErrorSum += error * error;
                absoluteErrorSum += error;
                maxError = Mathf.Max(maxError, error);
            }

            var rmse = Mathf.Sqrt(squaredErrorSum / count);
            var mae = absoluteErrorSum / count;
            var confidence = 1f / (1f + rmse / 6f);
            return new PredictionMetrics(rmse, mae, currentError, maxError, confidence, computeMilliseconds);
        }
    }
}
