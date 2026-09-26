using NUnit.Framework;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ContinuousMotionSamplerTests
    {
        [Test]
        public void Sample_InterpolatesPositionDepthAndShortestAttitudeAtElapsedTime()
        {
            var frames = new[]
            {
                Frame(0, 0f, 120d, 25d, 2f, 359f, -10f, 170f,
                    new SimulationDiagnostics(
                        new Vector3(1f, 2f, 3f),
                        new Vector3(0.5f, -0.5f, 1f),
                        0f,
                        0f,
                        0f,
                        controlSurfaceDeflectionDeg: new Vector3(2f, 4f, 6f))),
                Frame(1, 10f, 122d, 27d, 6f, 1f, 10f, -170f,
                    new SimulationDiagnostics(
                        new Vector3(3f, 4f, 5f),
                        new Vector3(1.5f, 0.5f, 3f),
                        0f,
                        0f,
                        0f,
                        controlSurfaceDeflectionDeg: new Vector3(4f, 8f, 12f)))
            };

            var sample = ContinuousMotionSampler.Sample(frames, 5f);

            Assert.That(sample.LowerIndex, Is.EqualTo(0));
            Assert.That(sample.UpperIndex, Is.EqualTo(1));
            Assert.That(sample.Interpolation01, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(sample.ElapsedSeconds, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(sample.LongitudeDeg, Is.EqualTo(121d).Within(0.000001d));
            Assert.That(sample.LatitudeDeg, Is.EqualTo(26d).Within(0.000001d));
            Assert.That(sample.DepthM, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(Mathf.DeltaAngle(sample.HeadingDeg, 0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sample.PitchDeg, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Mathf.Abs(sample.RollDeg), Is.EqualTo(180f).Within(0.0001f));
            Assert.That(sample.HasUsableCoordinates, Is.True);
            Assert.That(sample.HasDiagnostics, Is.True);
            Assert.That(sample.WaterVelocityEndMps, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(sample.CurrentVelocityEndMps, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(sample.GroundVelocityEndMps, Is.EqualTo(new Vector3(3f, 3f, 6f)));
            Assert.That(sample.DisplayVelocityEnuMps, Is.EqualTo(new Vector3(3f, -3f, 6f)));
            Assert.That(sample.ControlSurfaceDeflectionDeg, Is.EqualTo(new Vector3(3f, 6f, 9f)));
        }

        [Test]
        public void Sample_ExactTimestampDoesNotDependOnReplacedFuture()
        {
            var historical = new[]
            {
                Frame(0, 0f, 120d, 25d, 10f, 20f, 2f, 3f),
                Frame(1, 5f, 120.001d, 25.002d, 12f, 30f, 4f, 5f)
            };
            var firstFuture = new[]
            {
                historical[0],
                historical[1],
                Frame(2, 10f, 120.002d, 25.004d, 14f, 40f, 6f, 7f)
            };
            var replacedFuture = new[]
            {
                historical[0],
                historical[1],
                Frame(2, 8f, 80d, -30d, 900f, 250f, -80f, 90f)
            };

            var first = ContinuousMotionSampler.Sample(firstFuture, 5f);
            var replaced = ContinuousMotionSampler.Sample(replacedFuture, 5f);

            Assert.That(first.LowerIndex, Is.EqualTo(1));
            Assert.That(first.UpperIndex, Is.EqualTo(1));
            Assert.That(first.Interpolation01, Is.EqualTo(0f));
            Assert.That(replaced.LongitudeDeg, Is.EqualTo(first.LongitudeDeg));
            Assert.That(replaced.LatitudeDeg, Is.EqualTo(first.LatitudeDeg));
            Assert.That(replaced.DepthM, Is.EqualTo(first.DepthM));
            Assert.That(replaced.HeadingDeg, Is.EqualTo(first.HeadingDeg));
            Assert.That(replaced.DisplayVelocityEnuMps, Is.EqualTo(first.DisplayVelocityEnuMps));
        }

        [Test]
        public void Sample_ClampsBeforeStartAndAfterEnd()
        {
            var frames = new[]
            {
                Frame(0, 10f, 120d, 25d, 4f, 15f, 2f, 3f),
                Frame(1, 20f, 121d, 26d, 8f, 45f, 6f, 7f)
            };

            var before = ContinuousMotionSampler.Sample(frames, -100f);
            var after = ContinuousMotionSampler.Sample(frames, 100f);

            Assert.That(before.LowerIndex, Is.EqualTo(0));
            Assert.That(before.UpperIndex, Is.EqualTo(0));
            Assert.That(before.ElapsedSeconds, Is.EqualTo(10f));
            Assert.That(before.LongitudeDeg, Is.EqualTo(120d));
            Assert.That(after.LowerIndex, Is.EqualTo(1));
            Assert.That(after.UpperIndex, Is.EqualTo(1));
            Assert.That(after.ElapsedSeconds, Is.EqualTo(20f));
            Assert.That(after.LongitudeDeg, Is.EqualTo(121d));
        }

        [Test]
        public void Sample_MissingCoordinatesAndDiagnosticsNeverProducesNonFiniteValues()
        {
            var invalidDiagnostics = new SimulationDiagnostics(
                new Vector3(float.NaN, float.PositiveInfinity, 3f),
                new Vector3(float.NegativeInfinity, 2f, float.NaN),
                0f,
                0f,
                0f,
                controlSurfaceDeflectionDeg: new Vector3(float.NaN, 5f, float.PositiveInfinity));
            var frames = new[]
            {
                Frame(0, 0f, double.NaN, double.PositiveInfinity, float.NaN, float.NaN, float.PositiveInfinity, float.NegativeInfinity, invalidDiagnostics),
                Frame(1, 5f, double.NegativeInfinity, 200d, float.PositiveInfinity, float.PositiveInfinity, float.NaN, float.NegativeInfinity, invalidDiagnostics)
            };

            var sample = ContinuousMotionSampler.Sample(frames, 2.5f);

            Assert.That(sample.HasUsableCoordinates, Is.False);
            Assert.That(IsFinite(sample.ElapsedSeconds), Is.True);
            Assert.That(IsFinite(sample.LongitudeDeg), Is.True);
            Assert.That(IsFinite(sample.LatitudeDeg), Is.True);
            Assert.That(IsFinite(sample.DepthM), Is.True);
            Assert.That(IsFinite(sample.HeadingDeg), Is.True);
            Assert.That(IsFinite(sample.PitchDeg), Is.True);
            Assert.That(IsFinite(sample.RollDeg), Is.True);
            Assert.That(IsFinite(sample.WaterVelocityEndMps), Is.True);
            Assert.That(IsFinite(sample.CurrentVelocityEndMps), Is.True);
            Assert.That(IsFinite(sample.GroundVelocityEndMps), Is.True);
            Assert.That(IsFinite(sample.DisplayVelocityEnuMps), Is.True);
            Assert.That(IsFinite(sample.ControlSurfaceDeflectionDeg), Is.True);
        }

        private static TelemetryFrame Frame(
            int row,
            float elapsedSeconds,
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float headingDeg,
            float pitchDeg,
            float rollDeg,
            SimulationDiagnostics? diagnostics = null)
        {
            return new TelemetryFrame(
                row,
                row.ToString(),
                elapsedSeconds,
                longitudeDeg,
                latitudeDeg,
                depthM,
                100f,
                headingDeg,
                pitchDeg,
                rollDeg,
                28f,
                0f,
                100f,
                "Simulation",
                "Glide",
                1f,
                headingDeg,
                depthM,
                100f,
                0f,
                0f,
                0f,
                diagnostics);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
