using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Mapping;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ForecastReviewRegressionTests
    {
        private static FrozenForecast Publish(ForecastLedger ledger, string key, params TelemetryFrame[] frames)
            => ledger.Publish(key, ForecastLedgerTests.Point(0), frames, "a", true, 0, "model", "input", "current", "");

        [Test]
        public void FirstBackwardPublicationReconcilesAlreadyReceivedTruth()
        {
            var ledger = new ForecastLedger("replay");
            ledger.Observe(ForecastLedgerTests.Point(30, depth: 15), 30, "a", true, true, "simulation");
            ledger.Observe(ForecastLedgerTests.Point(100), 100, "a", true, true, "simulation");
            var record = Publish(ledger, "backward", ForecastLedgerTests.Point(30));
            Assert.That(ledger.Scores.Single().Status, Is.EqualTo("scored"));
            Assert.That(ledger.GetMetrics(record.ForecastId, 0).RmseMeters, Is.EqualTo(5));
        }

        [Test]
        public void ReplayMetricsWaitForActualScoreAvailabilityNotOnlyTargetTime()
        {
            var ledger = new ForecastLedger("clock");
            var record = Publish(ledger, "late", ForecastLedgerTests.Point(30));
            ledger.Observe(ForecastLedgerTests.Point(30, depth: 15), 50, "a", true, true, "simulation");
            Assert.That(float.IsNaN(ledger.GetMetricsThrough(record.ForecastId, 0, 40).RmseMeters), Is.True);
            Assert.That(ledger.GetMetricsThrough(record.ForecastId, 0, 50).RmseMeters, Is.EqualTo(5));
        }

        [Test]
        public void FinalizedArchiveDoesNotAllocateAgainWhenDeadlineIsChecked()
        {
            var ledger = new ForecastLedger("long-run");
            var points = Enumerable.Range(1, 90).Select(index => ForecastLedgerTests.Point(index * 10)).ToArray();
            for (var index = 0; index < 1000; index++) Publish(ledger, "request-" + index, points);
            var cold = Stopwatch.StartNew();
            ledger.Expire(2000);
            cold.Stop();
            var warm = Stopwatch.StartNew();
            ledger.Expire(2000);
            ledger.Expire(3000);
            warm.Stop();
            Assert.That(warm.Elapsed.TotalMilliseconds, Is.LessThan(Math.Max(20, cold.Elapsed.TotalMilliseconds * .1)),
                "finalized targets must leave the active scoring/deadline index");
            Assert.That(ledger.Scores.Count, Is.EqualTo(90000));
        }

        [Test]
        public void FiveSecondSourceUsesThirtyPastHeldTenSecondSamples()
        {
            var frames = Enumerable.Range(0, 101).Select(index => ForecastLedgerTests.Point(index * 5, depth: index * 5)).ToArray();
            var window = PredictionWindowBuilder.BuildForecast(frames, new PredictionRequest(100, 30, 3, 30));
            Assert.That(window.WindowFrames.First().ElapsedSeconds, Is.EqualTo(210));
            Assert.That(window.WindowFrames.Average(frame => frame.DepthM), Is.EqualTo(355));
            Assert.That(window.WindowFrames.Select(frame => frame.ElapsedSeconds),
                Is.EqualTo(Enumerable.Range(0, 30).Select(index => 210f + index * 10)));
            var fixture = JsonUtility.FromJson<FeatureFixture>(File.ReadAllText(Path.Combine(
                Application.dataPath, "..", "..", "PredictionTraining", "tests", "fixtures", "runtime_feature_parity.json")));
            var schema = new XGBoostFeatureSchema { history_length = 30, feature_names = fixture.feature_names };
            Assert.That(XGBoostFeatureBuilder.TryBuild(new PredictionContext(frames,
                new GeoCoordinateMapper(frames[0], 1, 1), window), schema, 30, out var features, out var error), Is.True, error);
            for (var index = 0; index < features.Length; index++)
                Assert.That(features[index], Is.EqualTo(fixture.expected_features[index]).Within(2e-4f), fixture.feature_names[index]);
        }

        [Test]
        public void CurrentVersionIncludesResolverPreferenceAndRadius()
        {
            var profile = SimulationProfile.Default;
            var original = ForecastLineage.HashCurrent(profile);
            profile.IrregularFieldIdwRadiusKm += 10;
            Assert.That(ForecastLineage.HashCurrent(profile), Is.Not.EqualTo(original));
            profile.IrregularFieldIdwRadiusKm -= 10;
            profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
            Assert.That(ForecastLineage.HashCurrent(profile), Is.Not.EqualTo(original));
        }

        [TestCase("profile")]
        [TestCase("dynamics")]
        [TestCase("current")]
        public void PhysicalBaselineRejectsMissingExplicitDependencies(string missing)
        {
            var profile = SimulationProfile.Default;
            var seed = SimulationTrajectoryGenerator.GenerateFrames(profile)[10];
            var snapshot = SimulationStateSnapshot.FromFrame(seed, missing == "profile" ? null : profile);
            if (missing == "dynamics") profile.Dynamics = null;
            if (missing == "current") { profile.OceanCurrentProfile = null; profile.OceanCurrentField = null; }
            Assert.Throws<InvalidOperationException>(() => EventDrivenPhysicsForecaster.Forecast(snapshot, profile,
                new[] { seed.ElapsedSeconds + 10 }));
        }

        [Serializable]
        private sealed class FeatureFixture
        {
            public string[] feature_names;
            public float[] expected_features;
        }
    }
}
