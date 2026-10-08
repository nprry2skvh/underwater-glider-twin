using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests.EditMode
{
    public sealed class EventDrivenPhysicsForecasterTests
    {
        private static MethodInfo Api()
        {
            var type = typeof(SimulationStateSnapshot).Assembly.GetType(
                "UnderwaterGliderTwin.Prediction.EventDrivenPhysicsForecaster");
            Assert.That(type, Is.Not.Null, "event-driven physical baseline is missing");
            return type.GetMethod("Forecast", BindingFlags.Public | BindingFlags.Static);
        }

        [Test]
        public void Forecast_MatchesProductionStepperFromSameSeedWithoutFutureObservations()
        {
            var profile = SimulationProfile.Default.Clone();
            profile.SampleIntervalSeconds = 10f;
            var seed = SimulationTrajectoryGenerator.GenerateFrames(profile)[10];
            var snapshot = SimulationStateSnapshot.FromFrame(seed, profile);
            var times = new[] { seed.ElapsedSeconds + 10f, seed.ElapsedSeconds + 20f, seed.ElapsedSeconds + 30f };
            var expected = SimulationTrajectoryGenerator.GenerateFutureSlices(snapshot, profile, 3, 3)
                .SelectMany(slice => slice).ToArray();
            var actual = (IReadOnlyList<TelemetryFrame>)Api().Invoke(null, new object[] { snapshot, profile, times });
            Assert.That(actual.Count, Is.EqualTo(3));
            for (var i = 0; i < 3; i++)
            {
                Assert.That(actual[i].ElapsedSeconds, Is.EqualTo(times[i]).Within(.01f));
                Assert.That(actual[i].LongitudeDeg, Is.EqualTo(expected[i].LongitudeDeg).Within(1e-10));
                Assert.That(actual[i].DepthM, Is.EqualTo(expected[i].DepthM).Within(1e-5f));
                Assert.That(actual[i].MissionState, Is.EqualTo(expected[i].MissionState));
            }
        }

        [Test]
        public void Forecast_RejectsMissingStateOrProfileInsteadOfInventingDefaults()
        {
            var method = Api();
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null,
                new object[] { default(SimulationStateSnapshot), SimulationProfile.Default, new[] { 10f } }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null,
                new object[] { default(SimulationStateSnapshot), null, new[] { 10f } }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Forecast_RejectsUnorderedTargets()
        {
            var profile = SimulationProfile.Default;
            var seed = SimulationTrajectoryGenerator.GenerateFrames(profile)[1];
            var snapshot = SimulationStateSnapshot.FromFrame(seed, profile);
            var error = Assert.Throws<TargetInvocationException>(() => Api().Invoke(null,
                new object[] { snapshot, profile, new[] { seed.ElapsedSeconds + 20f, seed.ElapsedSeconds + 10f } }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
        }
    }
}
