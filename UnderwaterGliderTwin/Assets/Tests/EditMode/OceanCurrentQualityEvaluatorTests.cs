using System;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class OceanCurrentQualityEvaluatorTests
    {
        [Test]
        public void Evaluate_ReportsReadyForContinuousDepthCoverage()
        {
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 50f, 0.1f, 0f),
                new OceanCurrentLayer(50f, 160f, 0.2f, 0.1f)
            });

            var report = OceanCurrentQualityEvaluator.Evaluate(profile, 0f, 120f, null, DateTime.UtcNow);

            Assert.That(report.Level, Is.EqualTo(OceanCurrentQualityLevel.Ready));
            Assert.That(report.IsDepthRangeCovered, Is.True);
            Assert.That(report.HasCoverageGaps, Is.False);
        }

        [Test]
        public void Evaluate_ReportsPartialWhenARequiredDepthBandHasAGap()
        {
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 20f, 0.1f, 0f),
                new OceanCurrentLayer(40f, 120f, 0.2f, 0.1f)
            });

            var report = OceanCurrentQualityEvaluator.Evaluate(profile, 0f, 120f, null, DateTime.UtcNow);

            Assert.That(report.Level, Is.EqualTo(OceanCurrentQualityLevel.PartialCoverage));
            Assert.That(report.IsDepthRangeCovered, Is.False);
            Assert.That(report.HasCoverageGaps, Is.True);
        }

        [Test]
        public void Evaluate_ReportsCachedDataAsStaleAfterMaximumAge()
        {
            var profile = new OceanCurrentProfile(new[] { new OceanCurrentLayer(0f, 160f, 0.1f, 0f) });
            var result = new CopernicusCurrentResult(
                "Copernicus Marine (local cache)",
                "dataset",
                DateTime.UtcNow.AddHours(-7).ToString("O"),
                profile);

            var report = OceanCurrentQualityEvaluator.Evaluate(profile, 0f, 120f, result, DateTime.UtcNow);

            Assert.That(report.IsCached, Is.True);
            Assert.That(report.IsStale, Is.True);
            Assert.That(report.Level, Is.EqualTo(OceanCurrentQualityLevel.Stale));
        }
    }
}
