using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CausalForecastIntegrationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private SimulationProfile oldProfile;
        private RuntimeDataSourceMode oldMode;
        private PredictionModelKind oldKind;
        private float oldHorizon;
        private bool oldEnabled;

        [SetUp]
        public void SetUp()
        {
            oldProfile = RuntimeDataSourceState.SimulationProfile.Clone();
            oldMode = RuntimeDataSourceState.CurrentMode;
            oldKind = RuntimePredictionState.ModelKind;
            oldHorizon = RuntimePredictionState.HorizonSeconds;
            oldEnabled = RuntimePredictionState.PredictionEnabled;
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            RuntimePredictionState.SetModelKind(PredictionModelKind.XGBoost);
            RuntimePredictionState.SetHorizonSeconds(30f);
            RuntimePredictionState.SetEnabled(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in objects) if (item != null) UnityEngine.Object.Destroy(item);
            objects.Clear();
            yield return null;
            RuntimePredictionState.SetModelKind(oldKind);
            RuntimePredictionState.SetHorizonSeconds(oldHorizon);
            RuntimePredictionState.SetEnabled(oldEnabled);
            RuntimeDataSourceState.UseSimulation(oldProfile);
            if (oldMode != RuntimeDataSourceMode.Simulation) RuntimeDataSourceState.UseCsvPath(RuntimeDataSourceState.LastCsvPath);
        }

        [UnityTest]
        public IEnumerator PublishedForecastStillScoresWhenPredictionDisplayIsDisabled()
        {
            var controller = Create(1f, out var playback, out _);
            var forecast = controller.Ledger.Forecasts.Single();
            controller.SetPredictionEnabled(false);
            playback.Seek(42f / 79f);
            yield return null;
            Assert.That(controller.Ledger.Scores.Count(score => score.ForecastId == forecast.ForecastId), Is.EqualTo(3));
            Assert.That(controller.Ledger.Forecasts.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HotUpdatePreservesOldForecastAndSeekUsesHistoricalBranch()
        {
            var controller = Create(1f, out var playback, out var timeline);
            playback.Seek(20f / 79f);
            var historical = controller.Ledger.Forecasts.Last();
            playback.Seek(39f / 79f);
            var old = controller.Ledger.Forecasts.First();
            var points = old.Frames.ToArray();
            var future = Frames().Skip(40).Select(frame => frame.WithProfileSequence(1)).ToArray();
            var changed = SimulationProfile.Default.Clone();
            changed.TargetDepthM += 10f;
            timeline.ReplaceFutureFrom(39, future, new SimulationTimelineSegment(1, 99,
                future[0].RowIndex, future[0].ElapsedSeconds, changed, DateTime.UtcNow));
            yield return null;
            var updated = controller.Ledger.Forecasts.Last();
            Assert.That(updated.BranchId, Is.Not.EqualTo(old.BranchId));
            Assert.That(updated.ProfileSequence, Is.EqualTo(1));
            Assert.That(old.Frames, Is.EqualTo(points));
            var count = controller.Ledger.Forecasts.Count;
            playback.Seek(20f / 79f);
            playback.Seek(20f / 79f);
            Assert.That(controller.Ledger.Forecasts.Count, Is.EqualTo(count), "historical seek must reuse original branch record");
            Assert.That(controller.Ledger.Forecasts.Any(record => record.ForecastId == historical.ForecastId), Is.True);
        }

        [UnityTest]
        public IEnumerator PhysicalScoresIgnoreDisplayScaleAndRepeatedSeekDoesNotAccumulate()
        {
            var one = Create(1f, out var firstPlayback, out _);
            var two = Create(25f, out var secondPlayback, out _);
            var first = one.Ledger.Forecasts.Single();
            var second = two.Ledger.Forecasts.Single();
            firstPlayback.Seek(42f / 79f);
            secondPlayback.Seek(42f / 79f);
            yield return null;
            var score1 = one.Ledger.GetMetrics(first.ForecastId, 0f);
            var score2 = two.Ledger.GetMetrics(second.ForecastId, 0f);
            Assert.That(score1.RmseMeters, Is.Not.NaN);
            Assert.That(score1.RmseMeters, Is.EqualTo(score2.RmseMeters));
            var count = one.Ledger.Scores.Count;
            firstPlayback.Seek(39f / 79f);
            firstPlayback.Seek(42f / 79f);
            Assert.That(one.Ledger.Scores.Count, Is.EqualTo(count));
        }

        private PredictionController Create(float scale, out PlaybackController playback,
            out SimulationTrajectoryTimeline timeline)
        {
            var frames = Frames();
            timeline = SimulationTrajectoryTimeline.CreateInitial(frames, RuntimeDataSourceState.SimulationProfile);
            var host = new GameObject("CausalForecastIntegration");
            objects.Add(host);
            playback = host.AddComponent<PlaybackController>();
            var model = new PlaybackModel(frames, 1f);
            model.BindTimeline(timeline);
            model.SeekNormalized(39f / 79f);
            model.SetPlaying(false);
            playback.Initialize(model);
            var controller = host.AddComponent<PredictionController>();
            controller.Initialize(frames, new GeoCoordinateMapper(frames[0], scale, scale), playback);
            Assert.That(controller.IsModelRuntimeAvailable(PredictionModelKind.XGBoost), Is.True);
            Assert.That(controller.Ledger.Forecasts.Single().Frames.Count, Is.EqualTo(3));
            return controller;
        }

        private static List<TelemetryFrame> Frames()
        {
            var frames = new List<TelemetryFrame>();
            for (var index = 0; index < 80; index++)
                frames.Add(new TelemetryFrame(index, "t" + index, index * 10f,
                    120.0 + index * .00001, 25.0 + index * .00002, 10f + index * .1f,
                    80f, 359f, 2f, -1f, 28f, .6f, 90f, "simulation", "descent",
                    1f, 359f, 100f, 0f, 0f, 0f, 0f));
            return frames;
        }
    }
}
