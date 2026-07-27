using System;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class MissionValidationEvaluatorTests
    {
        [Test]
        public void Evaluate_UsesMissionWaterColumnForDeepMissionAlarmLimit()
        {
            var profile = SimulationProfile.Default;
            profile.TargetDepthM = 1200f;
            profile.WaterColumnDepthM = 1200f;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 1200f, 0.2f, 0f)
            });
            var frames = new[] { Frame(1200f, 1200f) };

            var report = MissionValidationEvaluator.Evaluate(profile, frames, DateTime.UtcNow);

            Assert.That(report.AlarmDepthLimitM, Is.EqualTo(1265f).Within(0.01f));
            Assert.That(report.IsDepthWithinLimit, Is.True);
            Assert.That(report.IsCurrentConfigurationReady, Is.True);
            Assert.That(report.IsReady, Is.True);
        }

        [Test]
        public void Evaluate_ReportsIncompleteCurrentCoverageForTheMissionDepth()
        {
            var profile = SimulationProfile.Default;
            profile.TargetDepthM = 800f;
            profile.WaterColumnDepthM = 800f;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 80f, 0.2f, 0f)
            });

            var report = MissionValidationEvaluator.Evaluate(profile, new[] { Frame(100f, 800f) }, DateTime.UtcNow);

            Assert.That(report.UsesStaticWater, Is.False);
            Assert.That(report.IsCurrentConfigurationReady, Is.False);
            Assert.That(report.IsReady, Is.False);
        }

        [Test]
        public void Evaluate_DoesNotRaiseTheDepthLimitToHideAnObservedOverdepth()
        {
            var profile = SimulationProfile.Default;
            profile.TargetDepthM = 1200f;
            profile.WaterColumnDepthM = 1200f;

            var report = MissionValidationEvaluator.Evaluate(profile, new[] { Frame(1400f, 1200f) }, DateTime.UtcNow);

            Assert.That(report.AlarmDepthLimitM, Is.EqualTo(1265f).Within(0.01f));
            Assert.That(report.IsDepthWithinLimit, Is.False);
        }

        private static TelemetryFrame Frame(float depthM, float targetDepthM)
        {
            return new TelemetryFrame(
                0, "t", 0f, 140d, 15d, depthM, 500f, 42f, 0f, 0f,
                24f, 1f, 95f, "Parameter Simulation", "Glide", 1f, 42f, targetDepthM, 500f, 0f, 0f, 0f);
        }
    }
}
