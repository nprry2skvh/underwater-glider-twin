using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ForecastLedgerTests
    {
        // Resolve the new boundary at runtime so the RED run reaches assertions,
        // instead of making the entire Unity project fail to compile.
        private object Ledger()
        {
            var type = typeof(PredictionResult).Assembly.GetType("UnderwaterGliderTwin.Prediction.ForecastLedger");
            Assert.That(type, Is.Not.Null, "frozen forecast/delayed scoring service is missing");
            return Activator.CreateInstance(type, new object[] { "run-one", .5f, 20f, 60f });
        }

        private static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name).Invoke(target, args);
        private static T Value<T>(object target, string name)
            => (T)target.GetType().GetProperty(name).GetValue(target);
        private static object[] Scores(object ledger)
            => ((IEnumerable)Value<object>(ledger, "Scores")).Cast<object>().ToArray();
        private static object Publish(object ledger, string key, TelemetryFrame[] predicted, string branch = "a")
            => Call(ledger, "Publish", key, Point(0f), predicted, branch, true, 0, "model-hash", "input-digest", "current-v1", "");
        private static void Observe(object ledger, TelemetryFrame frame, float received, string branch = "a", bool simulation = true, bool hasPosition = true)
            => Call(ledger, "Observe", frame, received, branch, simulation, hasPosition, "navigation_record");

        [Test]
        public void CallerMutationAndRepeatedSeekCannotOverwriteFrozenForecast()
        {
            var ledger = Ledger();
            var points = new[] { Point(30f) };
            var first = Publish(ledger, "origin-0-horizon30", points);
            points[0] = Point(30f, depth: 999f);
            var repeated = Publish(ledger, "origin-0-horizon30", points);
            Assert.That(repeated, Is.SameAs(first));
            Assert.That(Value<IReadOnlyList<TelemetryFrame>>(first, "Frames")[0].DepthM, Is.EqualTo(10f));
            Assert.That(Value<string>(first, "RunId"), Is.EqualTo("run-one"));
            Assert.That(Value<string>(first, "ModelHash"), Is.EqualTo("model-hash"));
        }

        [Test]
        public void FutureSampleCannotBeScoredBeforeItIsReceived()
        {
            var ledger = Ledger();
            Publish(ledger, "k", new[] { Point(30f) });
            Observe(ledger, Point(30f), 20f);
            Assert.That(Scores(ledger), Is.Empty);
        }

        [Test]
        public void RepeatedObservationScoresOnlyOnceInPhysicalMetersWithWrappedAngles()
        {
            var ledger = Ledger();
            Publish(ledger, "k", new[] { Point(30f) });
            var observed = Point(30f, east: 3f, north: 4f, depth: 15f, heading: 1f, pitch: 10f, roll: 6f);
            Observe(ledger, observed, 30f);
            Observe(ledger, observed, 31f);
            Assert.That(Scores(ledger), Has.Length.EqualTo(1));
            var score = Scores(ledger)[0];
            Assert.That(Value<double>(score, "HorizontalErrorMeters"), Is.EqualTo(5d).Within(.001));
            Assert.That(Value<double>(score, "PositionErrorMeters"), Is.EqualTo(Math.Sqrt(50d)).Within(.001));
            Assert.That(Value<double>(score, "DepthErrorMeters"), Is.EqualTo(5d));
            Assert.That(Value<double>(score, "HeadingErrorDegrees"), Is.EqualTo(2d));
            Assert.That(Value<double>(score, "PitchErrorDegrees"), Is.EqualTo(2d));
            Assert.That(Value<double>(score, "RollErrorDegrees"), Is.EqualTo(3d));
        }

        [Test]
        public void SimulationBranchCannotScoreOtherBranchButRealObservationCan()
        {
            var ledger = Ledger();
            Publish(ledger, "k", new[] { Point(30f) });
            Observe(ledger, Point(30f), 30f, branch: "b");
            Assert.That(Scores(ledger), Is.Empty);
            Observe(ledger, Point(30f), 30f, branch: "b", simulation: false);
            Assert.That(Scores(ledger), Has.Length.EqualTo(1));
        }

        [Test]
        public void TruthInterpolationWaitsForBothSamplesAndRejectsExcessiveGap()
        {
            var ledger = Ledger();
            Publish(ledger, "k", new[] { Point(30f) });
            Observe(ledger, Point(20f, depth: 8f), 20f);
            Assert.That(Scores(ledger), Is.Empty);
            Observe(ledger, Point(40f, depth: 12f), 40f);
            Assert.That(Value<double>(Scores(ledger)[0], "DepthErrorMeters"), Is.EqualTo(0d));
            var other = Ledger();
            Publish(other, "k", new[] { Point(30f) });
            Observe(other, Point(10f), 10f);
            Observe(other, Point(50f), 50f);
            Assert.That(Scores(other), Is.Empty);
        }

        [Test]
        public void DeadlineProducesMissingStatusWithoutZeroError()
        {
            var ledger = Ledger();
            Publish(ledger, "k", new[] { Point(30f) });
            Call(ledger, "Expire", 89f);
            Assert.That(Scores(ledger), Is.Empty);
            Call(ledger, "Expire", 90f);
            var score = Scores(ledger)[0];
            Assert.That(Value<string>(score, "Status"), Is.EqualTo("missing"));
            Assert.That(double.IsNaN(Value<double>(score, "PositionErrorMeters")), Is.True);
            Observe(ledger, Point(30f), 91f);
            Assert.That(Scores(ledger), Has.Length.EqualTo(1));
        }

        [Test]
        public void MissingPositionReferenceIsNotSuccessfulZeroError()
        {
            var ledger = Ledger();
            Publish(ledger, "k", new[] { Point(30f) });
            Observe(ledger, Point(30f), 30f, hasPosition: false);
            Assert.That(Value<string>(Scores(ledger)[0], "Status"), Is.EqualTo("no_position_reference"));
            Assert.That(double.IsNaN(Value<double>(Scores(ledger)[0], "HorizontalErrorMeters")), Is.True);
        }

        [Test]
        public void DelayedMetricsAreUnavailableUntilScoredAndNeverPretendProbabilityConfidence()
        {
            var ledger = Ledger();
            var record = Publish(ledger, "k", new[] { Point(30f) });
            var id = Value<string>(record, "ForecastId");
            Assert.That(ledger.GetType().GetMethod("GetMetrics"), Is.Not.Null, "delayed metric aggregation is missing");
            var before = (PredictionMetrics)Call(ledger, "GetMetrics", id, 5f);
            Assert.That(float.IsNaN(before.RmseMeters), Is.True);
            Observe(ledger, Point(30f, east: 3f, north: 4f), 30f);
            var after = (PredictionMetrics)Call(ledger, "GetMetrics", id, 5f);
            Assert.That(after.RmseMeters, Is.EqualTo(5f).Within(.001));
            Assert.That(float.IsNaN(after.Confidence01), Is.True);
        }

        internal static TelemetryFrame Point(float seconds, float east = 0f, float north = 0f,
            float depth = 10f, float heading = 359f, float pitch = 8f, float roll = 3f)
        {
            return new TelemetryFrame((int)seconds, "t", seconds,
                120d + east / (111320d * Math.Cos(25d * Math.PI / 180d)), 25d + north / 111320d,
                depth, 80f, heading, pitch, roll, 28f, .6f, 92f,
                "mode", "state", 1f, 35f, 200f, 80f, 0f, 0f, 0f);
        }
    }
}
