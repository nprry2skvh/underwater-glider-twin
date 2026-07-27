using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class OceanCurrentProfileTests
    {
        [Test]
        public void GetVelocity_UsesLayerContainingRequestedDepth()
        {
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 40f, 0.2f, -0.1f),
                new OceanCurrentLayer(40f, 200f, -0.35f, 0.6f)
            });

            var velocity = profile.GetVelocity(100f);

            Assert.That(velocity, Is.EqualTo(new Vector2(-0.35f, 0.6f)));
        }

        [Test]
        public void GetVelocity_UsesNearestLayerOutsideConfiguredDepths()
        {
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(10f, 30f, 0.15f, 0.25f),
                new OceanCurrentLayer(80f, 120f, -0.4f, 0.05f)
            });

            Assert.That(profile.GetVelocity(0f), Is.EqualTo(new Vector2(0.15f, 0.25f)));
            Assert.That(profile.GetVelocity(180f), Is.EqualTo(new Vector2(-0.4f, 0.05f)));
        }

        [Test]
        public void TryGetDepthCoverage_ReturnsTheFullRangeAcrossFetchedLayers()
        {
            var profile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 1.018f, 0.1f, 0f),
                new OceanCurrentLayer(80f, 160f, 0.2f, 0f)
            });

            Assert.That(profile.TryGetDepthCoverage(out var minimumDepth, out var maximumDepth), Is.True);
            Assert.That(minimumDepth, Is.EqualTo(0f));
            Assert.That(maximumDepth, Is.EqualTo(160f));
        }

        [Test]
        public void OceanCurrentField_InterpolatesVelocityFromTheGliderPosition()
        {
            var field = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(140d, 15d, 100f, 0f, 0.1f, 0.2f),
                new OceanCurrentFieldSample(140.1d, 15d, 100f, 0f, 0.5f, -0.3f)
            });

            Assert.That(field.TryGetVelocity(140d, 15d, 100f, 0f, out var originVelocity), Is.True);
            Assert.That(field.TryGetVelocity(140.1d, 15d, 100f, 0f, out var displacedVelocity), Is.True);
            Assert.That(originVelocity, Is.EqualTo(new Vector2(0.1f, 0.2f)));
            Assert.That(displacedVelocity, Is.EqualTo(new Vector2(0.5f, -0.3f)));
        }

        [Test]
        public void OceanCurrentField_RejectsQueriesOutsideDownloadedHorizontalCoverage()
        {
            var field = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(140d, 15d, 100f, 0f, 0.1f, 0.2f),
                new OceanCurrentFieldSample(140.1d, 15d, 100f, 0f, 0.5f, -0.3f)
            });

            Assert.That(field.TryGetVelocity(141d, 15d, 100f, 0f, out _), Is.False);
        }
    }
}
