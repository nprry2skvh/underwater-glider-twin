using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CausalPredictionTests
    {
        private string directory;
        private XGBoostPredictor predictor;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "causal-forecast-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "models"));
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                "{\"artifact_version\":1,\"models\":[\"models/east_displacement_m.json\"],\"output_sources\":{\"east_displacement_m\":\"xgboost\",\"north_displacement_m\":\"stable\",\"depth_delta_m\":\"stable\",\"heading_delta_deg\":\"stable\",\"pitch_delta_deg\":\"stable\",\"roll_delta_deg\":\"stable\"}}");
            File.WriteAllText(Path.Combine(directory, "feature_schema.json"),
                "{\"history_length\":30,\"sample_interval_seconds\":10,\"horizons_seconds\":[30,60,300,900],\"feature_names\":[\"forecast_horizon_seconds\"],\"mean\":[0],\"scale\":[1]}");
            File.WriteAllText(Path.Combine(directory, "models/east_displacement_m.json"),
                "{\"tree_count\":1,\"trees\":[{\"nodes\":[{\"node_index\":0,\"feature_index\":-1,\"leaf_value\":30}]}]}");
            predictor = new XGBoostPredictor();
            predictor.LoadModel(directory);
        }

        [TearDown]
        public void TearDown()
        {
            predictor?.Release();
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ForecastAtLastObservationDoesNotRequireFutureTruth()
        {
            var frames = Frames(40);
            var result = Predict(frames, 60f);
            Assert.That(result.PredictedPoints, Has.Length.EqualTo(6));
            Assert.That(result.ActualPoints, Is.Empty, "inference must not expose unseen observations");
            var times = typeof(PredictionResult).GetProperty("TargetElapsedSeconds");
            Assert.That(times, Is.Not.Null, "forecast target times must be saved");
            Assert.That(times.GetValue(result), Is.EqualTo(new[] { 400f, 410f, 420f, 430f, 440f, 450f }));
        }

        [Test]
        public void MutatingOrDeletingFutureObservationsCannotChangePublishedPrediction()
        {
            var before = Predict(Frames(50), 60f);
            var changed = Frames(50);
            for (var i = 40; i < changed.Count; i++) changed[i] = Frame(i, 10000f + i * 60f, 100f);
            var after = Predict(changed, 60f);
            var deleted = Predict(Frames(40), 60f);
            Assert.That(after.PredictedPoints, Is.EqualTo(before.PredictedPoints));
            Assert.That(deleted.PredictedPoints, Is.EqualTo(before.PredictedPoints));
        }

        [TestCase(7200f)]
        [TestCase(45f)]
        [TestCase(float.NaN)]
        public void UnsupportedHorizonIsRejectedInsteadOfSilentlyClipped(float seconds)
        {
            var result = Predict(Frames(50), seconds);
            Assert.That(result.PredictedPoints, Is.Empty);
            Assert.That(result.Status, Does.Contain("unsupported"));
        }

        private PredictionResult Predict(IReadOnlyList<TelemetryFrame> frames, float seconds)
        {
            var mapper = new GeoCoordinateMapper(frames[0], 1f, 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(39, 30, 6, seconds));
            return predictor.Predict(new PredictionContext(frames, mapper, window));
        }

        internal static List<TelemetryFrame> Frames(int count)
        {
            var result = new List<TelemetryFrame>();
            for (var i = 0; i < count; i++) result.Add(Frame(i, i * 10f, 10f));
            return result;
        }

        internal static TelemetryFrame Frame(int index, float seconds, float depth)
        {
            return new TelemetryFrame(index, "t" + index, seconds, 120 + index * .00001,
                25 + index * .00001, depth, 80f, 359f, 8f, 3f, 28f, .6f, 92f,
                "mode", "state", 1f, 35f, 200f, 80f, 0f, 0f, 0f);
        }
    }
}
