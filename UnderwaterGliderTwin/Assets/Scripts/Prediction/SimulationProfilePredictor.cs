using System.Collections.Generic;
using System.Diagnostics;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class SimulationProfilePredictor : IPredictor
    {
        private IReadOnlyList<TelemetryFrame> cachedFrames = System.Array.Empty<TelemetryFrame>();
        private SimulationProfile cachedProfile;

        public string GetName()
        {
            return "SimulationProfile";
        }

        public void LoadModel(string modelDirectory)
        {
        }

        public PredictionResult Predict(PredictionContext context)
        {
            var timer = Stopwatch.StartNew();
            EnsureFrames();
            var futureFrames = context.Window.FutureFrames;
            if (futureFrames.Count == 0)
            {
                return new PredictionResult(GetName(), "Simulation prediction window exhausted", System.Array.Empty<Vector3>(), System.Array.Empty<Vector3>(), context.Window.FutureStartIndex, context.Window.FutureEndIndex, new PredictionMetrics(0f, 0f, 0f, 0f, 0f, 0f));
            }

            var predictedPoints = new Vector3[futureFrames.Count];
            var actualPoints = new Vector3[futureFrames.Count];
            for (var i = 0; i < futureFrames.Count; i++)
            {
                var frameIndex = Mathf.Clamp(context.Window.FutureStartIndex + i, 0, cachedFrames.Count - 1);
                predictedPoints[i] = context.Mapper.Map(cachedFrames[frameIndex]);
                actualPoints[i] = context.Mapper.Map(futureFrames[i]);
            }

            timer.Stop();
            var metrics = ErrorEvaluator.Evaluate(predictedPoints, actualPoints, (float)timer.Elapsed.TotalMilliseconds);
            return new PredictionResult(GetName(), "Simulation profile prediction", predictedPoints, actualPoints, context.Window.FutureStartIndex, context.Window.FutureEndIndex, metrics);
        }

        public void Release()
        {
            cachedFrames = System.Array.Empty<TelemetryFrame>();
            cachedProfile = null;
        }

        private void EnsureFrames()
        {
            var profile = RuntimeDataSourceState.SimulationProfile?.Clone() ?? SimulationProfile.Default;
            if (cachedProfile != null
                && cachedProfile.CycleCount == profile.CycleCount
                && Mathf.Approximately(cachedProfile.CycleDurationSeconds, profile.CycleDurationSeconds)
                && Mathf.Approximately(cachedProfile.SampleIntervalSeconds, profile.SampleIntervalSeconds)
                && Mathf.Approximately(cachedProfile.TargetDepthM, profile.TargetDepthM)
                && Mathf.Approximately(cachedProfile.HorizontalSpeedMps, profile.HorizontalSpeedMps)
                && Mathf.Approximately(cachedProfile.StartHeadingDeg, profile.StartHeadingDeg)
                && Mathf.Approximately(cachedProfile.HeadingDeltaPerCycleDeg, profile.HeadingDeltaPerCycleDeg)
                && Mathf.Approximately(cachedProfile.PitchAmplitudeDeg, profile.PitchAmplitudeDeg)
                && Mathf.Approximately(cachedProfile.RollAmplitudeDeg, profile.RollAmplitudeDeg)
                && System.Math.Abs(cachedProfile.OriginLongitudeDeg - profile.OriginLongitudeDeg) < 1e-9
                && System.Math.Abs(cachedProfile.OriginLatitudeDeg - profile.OriginLatitudeDeg) < 1e-9
                && Mathf.Approximately(cachedProfile.WaterColumnDepthM, profile.WaterColumnDepthM))
            {
                return;
            }

            cachedProfile = profile;
            cachedFrames = SimulationTrajectoryGenerator.GenerateFrames(profile);
        }
    }
}
