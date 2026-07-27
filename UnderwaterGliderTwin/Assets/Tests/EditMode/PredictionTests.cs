using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class PredictionTests
    {
        [Test]
        public void PredictionWindowBuilder_BuildsRollingWindowFromCurrentIndex()
        {
            var frames = Frames(100);
            var request = new PredictionRequest(currentIndex: 40, windowSize: 30, horizonPoints: 10, horizonSeconds: 300f);

            var window = PredictionWindowBuilder.Build(frames, request);

            Assert.That(window.WindowStartIndex, Is.EqualTo(11));
            Assert.That(window.WindowEndIndex, Is.EqualTo(40));
            Assert.That(window.FutureStartIndex, Is.EqualTo(41));
            Assert.That(window.FutureEndIndex, Is.EqualTo(50));
            Assert.That(window.WindowFrames, Has.Count.EqualTo(30));
            Assert.That(window.FutureFrames, Has.Count.EqualTo(10));
        }

        [Test]
        public void PhysicsPredictor_PredictsRequestedNumberOfFutureSamples()
        {
            var frames = Frames(80, secondsStep: 10f);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex: 39, windowSize: 30, horizonPoints: 8, horizonSeconds: 120f));
            var predictor = new PhysicsPredictor();

            var result = predictor.Predict(new PredictionContext(frames, mapper, window));

            Assert.That(result.Status, Is.EqualTo("Physics prediction"));
            Assert.That(result.PredictedPoints, Has.Length.EqualTo(8));
            Assert.That(result.ActualPoints, Has.Length.EqualTo(8));
            Assert.That(result.PredictedPoints[0], Is.Not.EqualTo(Vector3.zero));
        }

        [Test]
        public void PhysicsPredictor_GeneratesSawtoothDepthTrackInsteadOfSingleStraightSegment()
        {
            var frames = OscillatingFrames(80, secondsStep: 15f);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex: 49, windowSize: 30, horizonPoints: 16, horizonSeconds: 360f));
            var predictor = new PhysicsPredictor();

            var result = predictor.Predict(new PredictionContext(frames, mapper, window));

            var hasDescent = false;
            var hasClimb = false;
            for (var i = 1; i < result.PredictedPoints.Length; i++)
            {
                var deltaY = result.PredictedPoints[i].y - result.PredictedPoints[i - 1].y;
                if (deltaY < -0.001f)
                {
                    hasDescent = true;
                }

                if (deltaY > 0.001f)
                {
                    hasClimb = true;
                }
            }

            Assert.That(hasDescent, Is.True);
            Assert.That(hasClimb, Is.True);
        }

        [Test]
        public void PhysicsPredictor_ConstrainsHorizontalPredictionWhenRecentTelemetryZigzags()
        {
            var frames = NoisyZigZagFrames(80, secondsStep: 10f);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex: 49, windowSize: 30, horizonPoints: 8, horizonSeconds: 120f));
            var predictor = new PhysicsPredictor();

            var result = predictor.Predict(new PredictionContext(frames, mapper, window));
            var origin = mapper.Map(frames[49]);
            var horizontalDisplacement = new Vector2(
                result.PredictedPoints[0].x - origin.x,
                result.PredictedPoints[0].z - origin.z).magnitude;

            Assert.That(horizontalDisplacement, Is.LessThan(20f));
        }

        [Test]
        public void PhysicsPredictor_ReusesHistoricalAnalogueForRepeatingCycles()
        {
            var frames = RepeatingCycleFrames(cycleCount: 5, samplesPerCycle: 24, secondsStep: 10f);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex: 83, windowSize: 24, horizonPoints: 12, horizonSeconds: 120f));
            var predictor = new PhysicsPredictor();

            var result = predictor.Predict(new PredictionContext(frames, mapper, window));

            Assert.That(result.Status, Is.EqualTo("Physics analogue prediction"));
            Assert.That(result.Metrics.RmseMeters, Is.LessThan(0.5f));
        }

        [Test]
        public void ErrorEvaluator_ComputesExpectedMetrics()
        {
            var predicted = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 0f, 0f)
            };
            var actual = new[]
            {
                new Vector3(1f, 0f, 0f),
                new Vector3(2f, 0f, 0f)
            };

            var metrics = ErrorEvaluator.Evaluate(predicted, actual, 12.5f);

            Assert.That(metrics.CurrentErrorMeters, Is.EqualTo(1f).Within(0.001f));
            Assert.That(metrics.MaximumErrorMeters, Is.EqualTo(1f).Within(0.001f));
            Assert.That(metrics.MaeMeters, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(metrics.RmseMeters, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.001f));
            Assert.That(metrics.ComputeMilliseconds, Is.EqualTo(12.5f).Within(0.001f));
        }

        [Test]
        public void SimulationProfilePredictor_MatchesSimulationTrajectory()
        {
            var profile = new SimulationProfile
            {
                CycleCount = 4,
                CycleDurationSeconds = 600f,
                SampleIntervalSeconds = 10f,
                TargetDepthM = 120f,
                HorizontalSpeedMps = 0.65f,
                StartHeadingDeg = 42f,
                HeadingDeltaPerCycleDeg = 14f,
                PitchAmplitudeDeg = 24f,
                RollAmplitudeDeg = 7f
            };
            RuntimeDataSourceState.UseSimulation(profile);
            var frames = SimulationTrajectoryGenerator.GenerateFrames(profile);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex: 80, windowSize: 30, horizonPoints: 12, horizonSeconds: 120f));
            var predictor = new SimulationProfilePredictor();

            var result = predictor.Predict(new PredictionContext(frames, mapper, window));

            Assert.That(result.Status, Is.EqualTo("Simulation profile prediction"));
            Assert.That(result.PredictedPoints, Has.Length.EqualTo(window.FutureFrames.Count));
            Assert.That(result.Metrics.RmseMeters, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Metrics.CurrentErrorMeters, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void XGBoostArtifact_LoadsOutputSourcesFromManifest()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "xgboost-artifact-" + System.Guid.NewGuid().ToString("N"));
            var modelsDirectory = Path.Combine(directory, "models");
            Directory.CreateDirectory(modelsDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                "{\"artifact_version\":1,\"models\":[\"models/east_displacement_m.json\"],\"output_sources\":{\"east_displacement_m\":\"xgboost\",\"depth_delta_m\":\"stable\"}}");
            File.WriteAllText(Path.Combine(modelsDirectory, "east_displacement_m.json"),
                "{\"tree_count\":1,\"trees\":[{\"nodes\":[{\"node_index\":0,\"feature_index\":-1,\"threshold\":0,\"yes_index\":-1,\"no_index\":-1,\"missing_index\":-1,\"leaf_value\":0}]}]}");

            var loaded = XGBoostArtifact.TryLoad(directory, out var artifact, out var error);

            Assert.That(loaded, Is.True, error);
            Assert.That(artifact.GetOutputSource("east_displacement_m"), Is.EqualTo("xgboost"));
            Assert.That(artifact.GetOutputSource("depth_delta_m"), Is.EqualTo("stable"));
        }

        [Test]
        public void XGBoostArtifact_LoadsTreeModelDeclaredByManifest()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "xgboost-artifact-" + System.Guid.NewGuid().ToString("N"));
            var modelsDirectory = Path.Combine(directory, "models");
            Directory.CreateDirectory(modelsDirectory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                "{\"artifact_version\":1,\"models\":[\"models/east_displacement_m.json\"],\"output_sources\":{\"east_displacement_m\":\"xgboost\"}}");
            File.WriteAllText(Path.Combine(modelsDirectory, "east_displacement_m.json"),
                "{\"tree_count\":1,\"trees\":[{\"nodes\":[{\"node_index\":0,\"feature_index\":-1,\"threshold\":0,\"yes_index\":-1,\"no_index\":-1,\"missing_index\":-1,\"leaf_value\":2.5}]}]}");

            var loaded = XGBoostArtifact.TryLoad(directory, out var artifact, out var error);

            Assert.That(loaded, Is.True, error);
            Assert.That(XGBoostTreeEvaluator.Evaluate(artifact.GetModel("east_displacement_m"), new[] { 0f }), Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void XGBoostArtifact_LoadsFeatureSchemaWhenPresent()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "xgboost-artifact-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                "{\"artifact_version\":1,\"output_sources\":{\"east_displacement_m\":\"xgboost\"}}");
            File.WriteAllText(Path.Combine(directory, "feature_schema.json"),
                "{\"history_length\":30,\"sample_interval_seconds\":10,\"horizons_seconds\":[30,900],\"feature_names\":[\"depth_m\",\"forecast_horizon_seconds\"],\"mean\":[0,0],\"scale\":[1,1]}");

            var loaded = XGBoostArtifact.TryLoad(directory, out var artifact, out var error);

            Assert.That(loaded, Is.True, error);
            Assert.That(artifact.Schema.HistoryLength, Is.EqualTo(30));
            Assert.That(artifact.Schema.FeatureNames, Has.Length.EqualTo(2));
        }

        [Test]
        public void XGBoostTreeEvaluator_FollowsThresholdBranchesAndSumsLeaves()
        {
            var model = new XGBoostTreeModel
            {
                trees = new[]
                {
                    new XGBoostTree { nodes = new[] { new XGBoostTreeNode { feature_index = 0, threshold = 1f, yes_index = 1, no_index = 2, missing_index = 1 }, new XGBoostTreeNode { feature_index = -1, leaf_value = 0.25f }, new XGBoostTreeNode { feature_index = -1, leaf_value = 1f } } },
                    new XGBoostTree { nodes = new[] { new XGBoostTreeNode { feature_index = -1, leaf_value = -0.5f } } }
                }
            };

            var value = XGBoostTreeEvaluator.Evaluate(model, new[] { 2f });

            Assert.That(value, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void XGBoostTreeEvaluator_AddsModelBaseScore()
        {
            var model = new XGBoostTreeModel
            {
                base_score = 0.5f,
                trees = new[] { new XGBoostTree { nodes = new[] { new XGBoostTreeNode { feature_index = -1, leaf_value = 0.25f } } } }
            };

            Assert.That(XGBoostTreeEvaluator.Evaluate(model, new[] { 0f }), Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void XGBoostFeatureBuilder_UsesThirtyFrameHistoryAndRequestedHorizon()
        {
            var frames = Frames(50, secondsStep: 10f);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex: 39, windowSize: 30, horizonPoints: 5, horizonSeconds: 60f));
            var schema = new XGBoostFeatureSchema
            {
                history_length = 30,
                feature_names = new[] { "depth_m", "heading_sin", "heading_cos", "depth_mean", "forecast_horizon_seconds" },
                mean = new float[5],
                scale = new[] { 1f, 1f, 1f, 1f, 1f },
            };

            var built = XGBoostFeatureBuilder.TryBuild(new PredictionContext(frames, mapper, window), schema, 300f, out var values, out var error);

            Assert.That(built, Is.True, error);
            Assert.That(values, Has.Length.EqualTo(5));
            Assert.That(values[0], Is.EqualTo(frames[39].DepthM).Within(0.001f));
            Assert.That(values[1], Is.EqualTo(Mathf.Sin(frames[39].HeadingDeg * Mathf.Deg2Rad)).Within(0.001f));
            Assert.That(values[4], Is.EqualTo(300f));
        }

        [Test]
        public void XGBoostPredictor_InterpolatesThirtySecondAnchorToTenSecondPoints()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "xgboost-artifact-" + System.Guid.NewGuid().ToString("N"));
            var modelsDirectory = Path.Combine(directory, "models");
            Directory.CreateDirectory(modelsDirectory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                "{\"artifact_version\":1,\"models\":[\"models/east_displacement_m.json\"],\"output_sources\":{\"east_displacement_m\":\"xgboost\",\"north_displacement_m\":\"stable\",\"depth_delta_m\":\"stable\",\"heading_delta_deg\":\"stable\",\"pitch_delta_deg\":\"stable\",\"roll_delta_deg\":\"stable\"}}");
            File.WriteAllText(Path.Combine(directory, "feature_schema.json"),
                "{\"history_length\":30,\"sample_interval_seconds\":10,\"horizons_seconds\":[30,60],\"feature_names\":[\"forecast_horizon_seconds\"],\"mean\":[0],\"scale\":[1]}");
            File.WriteAllText(Path.Combine(modelsDirectory, "east_displacement_m.json"),
                "{\"tree_count\":1,\"trees\":[{\"nodes\":[{\"node_index\":0,\"feature_index\":-1,\"threshold\":0,\"yes_index\":-1,\"no_index\":-1,\"missing_index\":-1,\"leaf_value\":30}]}]}");
            var predictor = new XGBoostPredictor();
            predictor.LoadModel(directory);
            var frames = Frames(50, secondsStep: 10f);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex: 39, windowSize: 30, horizonPoints: 6, horizonSeconds: 60f));

            var result = predictor.Predict(new PredictionContext(frames, mapper, window));

            Assert.That(result.Status, Does.StartWith("XGBoost"));
            Assert.That(result.PredictedPoints, Has.Length.EqualTo(6));
            Assert.That(result.PredictedPoints[2].x - mapper.Map(frames[39]).x, Is.EqualTo(30f).Within(0.01f));
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count, float secondsStep = 1f)
        {
            var frames = new List<TelemetryFrame>(count);
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i * secondsStep,
                    120.0 + i * 0.00005,
                    25.0 + i * 0.00008,
                    10f + i * 0.5f,
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

        private static IReadOnlyList<TelemetryFrame> OscillatingFrames(int count, float secondsStep)
        {
            var frames = new List<TelemetryFrame>(count);
            for (var i = 0; i < count; i++)
            {
                var phase = (i % 10) / 10f;
                var descending = phase < 0.5f;
                var localPhase = descending ? phase / 0.5f : (phase - 0.5f) / 0.5f;
                var depth = descending ? Mathf.Lerp(15f, 60f, localPhase) : Mathf.Lerp(60f, 15f, localPhase);
                var pitch = descending ? -18f : 18f;
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i * secondsStep,
                    120.0 + i * 0.00005,
                    25.0 + i * 0.00008,
                    depth,
                    80f,
                    32f,
                    pitch,
                    descending ? -8f : 8f,
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

        private static IReadOnlyList<TelemetryFrame> NoisyZigZagFrames(int count, float secondsStep)
        {
            var frames = new List<TelemetryFrame>(count);
            for (var i = 0; i < count; i++)
            {
                var longitude = 120.0 + (i * 0.00001) + ((i % 2 == 0) ? 0.0035 : -0.0035);
                var latitude = 25.0 + (i * 0.00001);
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i * secondsStep,
                    longitude,
                    latitude,
                    20f + (i % 6),
                    80f,
                    40f,
                    6f,
                    2f,
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

        private static IReadOnlyList<TelemetryFrame> RepeatingCycleFrames(int cycleCount, int samplesPerCycle, float secondsStep)
        {
            var frames = new List<TelemetryFrame>(cycleCount * samplesPerCycle);
            var index = 0;
            for (var cycle = 0; cycle < cycleCount; cycle++)
            {
                var heading = 48f + cycle * 6f;
                var headingRad = heading * Mathf.Deg2Rad;
                for (var sample = 0; sample < samplesPerCycle; sample++)
                {
                    var phase = sample / (float)(samplesPerCycle - 1);
                    var depth = Mathf.Sin(phase * Mathf.PI) * 120f;
                    var longitude = 120.0 + cycle * 0.01 + sample * 0.00003 * Mathf.Sin(headingRad);
                    var latitude = 25.0 + cycle * 0.01 + sample * 0.00003 * Mathf.Cos(headingRad);
                    frames.Add(new TelemetryFrame(
                        index,
                        $"t{index}",
                        index * secondsStep,
                        longitude,
                        latitude,
                        depth,
                        400f - depth,
                        heading,
                        phase <= 0.5f ? -22f : 22f,
                        Mathf.Sin(phase * Mathf.PI * 2f) * 6f,
                        28f,
                        0.6f,
                        92f,
                        "mode",
                        "state",
                        cycle + 1,
                        heading,
                        120f,
                        280f,
                        0f,
                        0f,
                        0f));
                    index++;
                }
            }

            return frames;
        }
    }
}
