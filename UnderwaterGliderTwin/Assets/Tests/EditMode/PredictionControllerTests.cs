using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class PredictionControllerTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            RuntimePredictionState.SetModelKind(PredictionModelKind.XGBoost);
            RuntimePredictionState.SetEnabled(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in objects)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            objects.Clear();
            RuntimePredictionState.SetModelKind(PredictionModelKind.XGBoost);
            RuntimePredictionState.SetEnabled(true);
        }

        [Test]
        public void Controller_UsesFactoryCreatedPredictorForRuntimeAvailability()
        {
            var controller = CreateController(new FakePredictorFactory(succeeds: true));

            Assert.That(controller.IsModelRuntimeAvailable(PredictionModelKind.XGBoost), Is.True);
            Assert.That(controller.TrySetModelKind(PredictionModelKind.XGBoost, out var error), Is.True, error);
        }

        [Test]
        public void Controller_ReportsUnavailableModelAndKeepsPreviousState()
        {
            var controller = CreateController(new FakePredictorFactory(succeeds: true));
            var previous = controller.ModelKind;

            var changed = controller.TrySetModelKind(PredictionModelKind.Lstm, out var error);

            Assert.That(changed, Is.False);
            Assert.That(error, Does.Contain("unavailable"));
            Assert.That(controller.ModelKind, Is.EqualTo(previous));
        }

        [Test]
        public void Controller_ReportsLoaderFailureWithoutRegisteringModel()
        {
            var controller = CreateController(new FakePredictorFactory(succeeds: false, error: "fake load failed"));

            Assert.That(controller.IsModelRuntimeAvailable(PredictionModelKind.XGBoost), Is.False);
            Assert.That(controller.PredictionEnabled, Is.False);
            Assert.That(controller.TrySetModelKind(PredictionModelKind.XGBoost, out var error), Is.False);
            Assert.That(error, Does.Contain("fake load failed"));
        }

        private PredictionController CreateController(IPredictorFactory factory)
        {
            var frames = Frames(12);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playbackObject = new GameObject("Playback");
            objects.Add(playbackObject);
            var playback = playbackObject.AddComponent<PlaybackController>();
            playback.Initialize(new PlaybackModel(frames, rowsPerSecond: 1f));

            var controllerObject = new GameObject("PredictionController");
            objects.Add(controllerObject);
            var controller = controllerObject.AddComponent<PredictionController>();
            controller.PredictorFactory = factory;
            controller.Initialize(frames, mapper, playback);
            return controller;
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count)
        {
            var frames = new List<TelemetryFrame>(count);
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i * 10f,
                    120.0 + i * 0.00005,
                    25.0 + i * 0.00008,
                    10f,
                    80f,
                    35f,
                    8f,
                    3f,
                    28f,
                    0.6f,
                    92f,
                    "mode",
                    "state",
                    1f,
                    35f,
                    200f,
                    80f,
                    0f,
                    0f,
                    0f));
            }

            return frames;
        }

        private sealed class FakePredictorFactory : IPredictorFactory
        {
            private readonly bool succeeds;
            private readonly string error;

            public FakePredictorFactory(bool succeeds, string error = "")
            {
                this.succeeds = succeeds;
                this.error = error;
            }

            public bool TryCreate(string root, out IPredictor predictor, out string errorMessage)
            {
                if (!succeeds)
                {
                    predictor = null;
                    errorMessage = error;
                    return false;
                }

                predictor = new FakePredictor();
                errorMessage = string.Empty;
                return true;
            }
        }

        private sealed class FakePredictor : IPredictor
        {
            public string GetName() => "Fake";

            public void LoadModel(string modelDirectory)
            {
            }

            public PredictionResult Predict(PredictionContext context)
            {
                return new PredictionResult(
                    GetName(),
                    "Fake prediction",
                    Array.Empty<Vector3>(),
                    Array.Empty<Vector3>(),
                    context.Window.FutureStartIndex,
                    context.Window.FutureEndIndex,
                    new PredictionMetrics(0f, 0f, 0f, 0f, 0f, 0f));
            }

            public void Release()
            {
            }
        }
    }
}
