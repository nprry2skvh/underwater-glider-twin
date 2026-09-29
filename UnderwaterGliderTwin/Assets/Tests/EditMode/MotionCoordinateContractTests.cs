using NUnit.Framework;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class MotionCoordinateContractTests
    {
        [Test]
        public void LocalMissionCoordinateConverter_RoundTripsAtHighLatitude()
        {
            const double originLongitudeDeg = 18.25d;
            const double originLatitudeDeg = 75d;
            var expected = new Vector3(12345f, 321f, 23456f);

            LocalMissionCoordinateConverter.ToGeodetic(
                expected,
                originLongitudeDeg,
                originLatitudeDeg,
                out var longitudeDeg,
                out var latitudeDeg);
            var actual = LocalMissionCoordinateConverter.ToLocalPosition(
                longitudeDeg,
                latitudeDeg,
                expected.y,
                originLongitudeDeg,
                originLatitudeDeg);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        [Test]
        public void GeoCoordinateMapper_MapDynamicsVelocityFlipsDownAndAppliesScale()
        {
            var mapper = new GeoCoordinateMapper(
                Frame(0f, 120d, 25d, 0f),
                horizontalScale: 2f,
                depthScale: 4f);

            var mapped = mapper.MapDynamicsVelocity(new Vector3(1f, 2f, 3f));

            Assert.That(mapped, Is.EqualTo(new Vector3(2f, -8f, 6f)));
        }

        [Test]
        public void SnapshotFromGeneratedFrame_PreservesDynamicsPosition()
        {
            var profile = SimulationProfile.Default;
            profile.OriginLongitudeDeg = 18.25d;
            profile.OriginLatitudeDeg = 75d;
            var expected = new Vector3(12345f, 321f, 23456f);
            LocalMissionCoordinateConverter.ToGeodetic(
                expected,
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                out var longitudeDeg,
                out var latitudeDeg);
            var frame = Frame(10f, longitudeDeg, latitudeDeg, expected.y);

            var snapshot = SimulationStateSnapshot.FromFrame(frame, profile);

            Assert.That(snapshot.DynamicsState.PositionEndM.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(snapshot.DynamicsState.PositionEndM.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(snapshot.DynamicsState.PositionEndM.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private static TelemetryFrame Frame(float elapsedSeconds, double longitudeDeg, double latitudeDeg, float depthM)
        {
            return new TelemetryFrame(
                0,
                elapsedSeconds.ToString(),
                elapsedSeconds,
                longitudeDeg,
                latitudeDeg,
                depthM,
                1000f - depthM,
                45f,
                5f,
                -3f,
                28f,
                0.5f,
                90f,
                "Simulation",
                "Glide",
                1f,
                45f,
                500f,
                500f,
                0f,
                0f,
                0f);
        }
    }
}
