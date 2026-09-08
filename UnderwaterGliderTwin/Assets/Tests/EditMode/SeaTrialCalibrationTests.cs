using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class SeaTrialCalibrationTests
    {
        [Test]
        public void Calibrate_UsesCurrentCorrectedWaterSpeedAndRobustAttitudeAmplitude()
        {
            var current = new OceanCurrentProfile();
            current.AddLayer(new OceanCurrentLayer(0f, 100f, 0f, 0.1f));
            var frames = new[]
            {
                Frame(0, 0f, 120d, 25d, 10f, 4f),
                Frame(1, 10f, 120d, 25.0000633d, 10f, -4f),
                Frame(2, 20f, 120d, 25.0001266d, -10f, 4f)
            };

            var success = SeaTrialCalibrator.TryCalibrate(
                frames,
                current,
                GliderDynamicsProfile.Default,
                out var result,
                out var error);

            Assert.That(success, Is.True, error);
            Assert.That(result.SampleCount, Is.EqualTo(2));
            Assert.That(result.CurrentCorrectionApplied, Is.True);
            Assert.That(result.MedianWaterSpeedMps, Is.EqualTo(0.6f).Within(0.03f));
            Assert.That(result.RecommendedPitchAmplitudeDeg, Is.EqualTo(10f).Within(0.01f));
            Assert.That(result.RecommendedRollAmplitudeDeg, Is.EqualTo(4f).Within(0.01f));
            Assert.That(result.SpeedRmseAfterMps, Is.LessThan(result.SpeedRmseBeforeMps));
        }

        private static TelemetryFrame Frame(int index, float elapsedSeconds, double longitude, double latitude, float pitch, float roll)
        {
            return new TelemetryFrame(
                index,
                $"00:00:{index:00}",
                elapsedSeconds,
                longitude,
                latitude,
                10f,
                500f,
                42f,
                pitch,
                roll,
                24f,
                1f,
                95f,
                "Glide",
                "Running",
                1f,
                42f,
                10f,
                500f,
                0f,
                0f,
                0f);
        }
    }
}
