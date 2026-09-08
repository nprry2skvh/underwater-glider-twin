using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class GeoCoordinateMapperTests
    {
        [Test]
        public void Map_UsesOriginAndDepthScale()
        {
            var origin = Frame(120.0, 25.0, 0f, 0f, 0f, 0f);
            var mapper = new GeoCoordinateMapper(origin, horizontalScale: 0.01f, depthScale: 0.05f);
            var point = Frame(120.001, 25.001, 100f, 0f, 0f, 0f);

            var mapped = mapper.Map(point);

            Assert.That(mapped.x, Is.EqualTo(1.009f).Within(0.02f));
            Assert.That(mapped.z, Is.EqualTo(1.113f).Within(0.02f));
            Assert.That(mapped.y, Is.EqualTo(-5f).Within(0.001f));
        }

        [Test]
        public void PoseMapper_ConvertsHeadingPitchRoll()
        {
            var frame = Frame(120.0, 25.0, 0f, heading: 90f, pitch: 10f, roll: -5f);

            var rotation = PoseMapper.ToRotation(frame);

            var forward = rotation * Vector3.forward;
            Assert.That(forward.x, Is.GreaterThan(0.9f));
            Assert.That(Mathf.Abs(forward.z), Is.LessThan(0.2f));
        }

        [Test]
        public void PoseMapper_AppliesRollAroundGliderForwardAxis()
        {
            var frame = Frame(120.0, 25.0, 0f, heading: 0f, pitch: 0f, roll: 25f);

            var rotation = PoseMapper.ToRotation(frame);

            var rightWing = rotation * Vector3.right;
            Assert.That(rightWing.y, Is.LessThan(-0.35f));
        }

        [Test]
        public void PoseMapper_ClampsUnrealisticPitchAndRollForStableVisualization()
        {
            var frame = Frame(120.0, 25.0, 0f, heading: 0f, pitch: 120f, roll: 0f);
            var settings = new AttitudeSettings(1f, 1f, maxAbsPitchDeg: 30f, maxAbsRollDeg: 40f);

            var rotation = PoseMapper.ToRotation(frame, settings);

            var forward = rotation * Vector3.forward;
            Assert.That(forward.y, Is.EqualTo(-0.5f).Within(0.02f));

            frame = Frame(120.0, 25.0, 0f, heading: 0f, pitch: 0f, roll: 160f);
            rotation = PoseMapper.ToRotation(frame, settings);
            var rightWing = rotation * Vector3.right;
            Assert.That(rightWing.y, Is.EqualTo(-Mathf.Sin(40f * Mathf.Deg2Rad)).Within(0.03f));
        }

        private static TelemetryFrame Frame(double lon, double lat, float depth, float heading, float pitch, float roll)
        {
            return new TelemetryFrame(0, "t", 0f, lon, lat, depth, 100f, heading, pitch, roll, 28f, 0.2f, 95f, "mode", "state", 1f, 40f, 100f, 100f, 0f, 0f, 0f);
        }
    }
}
