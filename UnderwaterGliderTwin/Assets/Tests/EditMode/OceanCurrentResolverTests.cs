using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class OceanCurrentResolverTests
    {
        [Test]
        public void SimulationProfile_DefaultVolumetricCurrentBudgetIsExplicitAndStable()
        {
            var profile = SimulationProfile.Default;

            Assert.That(profile.GliderClearanceRadiusM, Is.EqualTo(500f));
            Assert.That(profile.TrajectorySafetyCorridorRadiusM, Is.EqualTo(750f));
            Assert.That(profile.MinimumArrowSpeedMps, Is.EqualTo(0.02f));
            Assert.That(profile.MinimumStreamlineSpeedMps, Is.EqualTo(0.05f));
            Assert.That(profile.IrregularFieldIdwRadiusKm, Is.EqualTo(50f));
            Assert.That(profile.CandidateDepthLayerCount, Is.EqualTo(12));
            Assert.That(profile.CandidateHorizontalColumnCount, Is.EqualTo(12));
            Assert.That(profile.CandidateHorizontalRowCount, Is.EqualTo(10));
        }

        [Test]
        public void SimulationProfile_DefaultCurrentSourcePreferenceIsLayeredPreferred()
        {
            var profile = SimulationProfile.Default;
            var property = typeof(SimulationProfile).GetProperty("OceanCurrentSourcePreference");

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType.IsEnum, Is.True);
            Assert.That(property.GetValue(profile).ToString(), Is.EqualTo("LayeredPreferred"));
            Assert.That(System.Enum.GetNames(property.PropertyType), Does.Contain("NetworkPreferred"));
        }

        [Test]
        public void SimulationTelemetry_UsesLayeredCurrentWhenLayeredSourceIsPreferred()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 180f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 20f;
            profile.HorizontalSpeedMps = 0f;
            profile.OriginLongitudeDeg = 120d;
            profile.OriginLatitudeDeg = 25d;
            profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.LayeredPreferred;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 30f, 0.1f, 0f)
            });
            profile.OceanCurrentField = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(120d, 25d, 0f, 0f, 0.9f, 0f)
            });

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            Assert.That(frames[1].Diagnostics.Value.CurrentVelocityEndMps.x, Is.EqualTo(0.1f).Within(0.01f));
        }

        [Test]
        public void LayeredCurrent_InterpolatesBetweenAdjacentLayerCenters()
        {
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 10f, 0f, 0f),
                new OceanCurrentLayer(10f, 30f, 1f, -0.5f)
            });
            var method = typeof(OceanCurrentProfile).GetMethod("TryGetInterpolatedVelocity");

            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { 12.5f, Vector2.zero };

            Assert.That((bool)method.Invoke(profile, arguments), Is.True);
            var velocity = (Vector2)arguments[1];
            Assert.That(velocity.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(velocity.y, Is.EqualTo(-0.25f).Within(0.001f));
        }

        [Test]
        public void OceanCurrentFieldSample_ExposesOptionalVerticalVelocity()
        {
            Assert.That(new OceanCurrentFieldSample().VerticalMps, Is.EqualTo(0f));
        }

        [Test]
        public void Resolver_NetworkPreferenceReturnsThreeDimensionalNetworkVector()
        {
            var profile = SimulationProfile.Default;
            profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 100f, 0.1f, 0.2f)
            });
            profile.OceanCurrentField = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(140d, 15d, 200f, 30f, 0.7f, -0.2f, 0.04f)
            });

            var resolver = new OceanCurrentResolver(profile);
            var found = resolver.TryGetVector(
                new OceanCurrentQuery(140d, 15d, Vector3.zero, 200f, 30f),
                out var vector);

            Assert.That(found, Is.True);
            Assert.That(vector.DataKind, Is.EqualTo(OceanCurrentDataKind.NetworkGrid));
            Assert.That(vector.VelocityMps.x, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(vector.VelocityMps.y, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(vector.VelocityMps.z, Is.EqualTo(-0.2f).Within(0.0001f));
        }

        [Test]
        public void NetworkField_RejectsDepthAndTimeOutsideItsMultiTimeCoverage()
        {
            var field = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(140d, 15d, 100f, 0f, 0.1f, 0f),
                new OceanCurrentFieldSample(140d, 15d, 100f, 60f, 0.2f, 0f),
                new OceanCurrentFieldSample(140d, 15d, 300f, 0f, 0.1f, 0f),
                new OceanCurrentFieldSample(140d, 15d, 300f, 60f, 0.2f, 0f)
            });

            Assert.That(field.TryGetVector(140d, 15d, 50f, 30f, 50f, out _), Is.False);
            Assert.That(field.TryGetVector(140d, 15d, 200f, 120f, 50f, out _), Is.False);
            Assert.That(field.TryGetVector(140d, 15d, 200f, 30f, 50f, out _), Is.True);
        }
    }
}
