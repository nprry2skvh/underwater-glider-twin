using NUnit.Framework;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class WaterWaveMathTests
    {
        [Test]
        public void Evaluate_IsDeterministicForSamePositionAndTime()
        {
            var settings = WaterSurfaceSettings.CreateDefault().ValidatedCopy();
            var position = new Vector2(13.25f, -47.5f);

            var first = WaterWaveMath.Evaluate(settings, position, 82.4f);
            var second = WaterWaveMath.Evaluate(settings, position, 82.4f);

            Assert.That(second.HeightM, Is.EqualTo(first.HeightM));
            Assert.That(second.Normal, Is.EqualTo(first.Normal));
        }

        [Test]
        public void Evaluate_HeightStaysWithinConfiguredAmplitudeBound()
        {
            var settings = WaterSurfaceSettings.CreateDefault().ValidatedCopy();
            for (var index = 0; index < 200; index++)
            {
                var sample = WaterWaveMath.Evaluate(
                    settings,
                    new Vector2(index * 1.37f, index * -0.73f),
                    index * 2.1f);
                Assert.That(
                    Mathf.Abs(sample.HeightM - settings.SurfaceHeightM),
                    Is.LessThanOrEqualTo(settings.MaximumWaveHeightM + 0.0001f));
            }
        }

        [Test]
        public void Evaluate_ReturnsUnitNormalAndRespondsToTime()
        {
            var settings = WaterSurfaceSettings.CreateDefault().ValidatedCopy();
            var position = new Vector2(3f, 7f);
            var start = WaterWaveMath.Evaluate(settings, position, 0f);
            var later = WaterWaveMath.Evaluate(settings, position, 3f);

            Assert.That(start.Normal.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(later.Normal.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(later.HeightM, Is.Not.EqualTo(start.HeightM).Within(0.0001f));
        }

        [Test]
        public void ValidatedCopy_ClampsUnsafeValuesWithoutMutatingSource()
        {
            var source = WaterSurfaceSettings.CreateDefault();
            source.PatchSizeM = -5f;
            source.Alpha = 3f;
            source.LongWave = new WaterWaveBand(-2f, 0f, -90f, -1f);
            source.InteractionStrengthM = 8f;
            source.InteractionRadiusM = -4f;
            source.InteractionDepthFadeM = 0f;
            source.WakeLengthM = 900f;

            var validated = source.ValidatedCopy();

            Assert.That(source.PatchSizeM, Is.EqualTo(-5f));
            Assert.That(validated.PatchSizeM, Is.EqualTo(32f));
            Assert.That(validated.Alpha, Is.EqualTo(1f));
            Assert.That(validated.LongWave.AmplitudeM, Is.EqualTo(0f));
            Assert.That(validated.LongWave.WavelengthM, Is.EqualTo(0.25f));
            Assert.That(validated.LongWave.DirectionDegrees, Is.EqualTo(270f));
            Assert.That(validated.InteractionStrengthM, Is.EqualTo(2f));
            Assert.That(validated.InteractionRadiusM, Is.EqualTo(1f));
            Assert.That(validated.InteractionDepthFadeM, Is.EqualTo(0.1f));
            Assert.That(validated.WakeLengthM, Is.EqualTo(400f));
        }

        [Test]
        public void DefaultSettings_UseDenseCrossWaveSurface()
        {
            var settings = WaterSurfaceSettings.CreateDefault().ValidatedCopy();

            Assert.That(settings.Quality, Is.EqualTo(WaterSurfaceQuality.High));
            Assert.That(settings.CrossWave.AmplitudeM, Is.GreaterThan(0f));
            Assert.That(settings.DetailWave.AmplitudeM, Is.GreaterThan(0f));
            Assert.That(settings.GridSegments, Is.EqualTo(512));
            Assert.That(settings.InteractionStrengthM, Is.GreaterThan(0f));
        }
    }
}
