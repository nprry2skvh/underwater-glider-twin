using NUnit.Framework;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class MissionMapOverlayTests
    {
        [Test]
        public void BuildActualPoints_MapsEveryTelemetryFrame()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "0", 0f, 120d, 25d, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "1", 1f, 120.01d, 25.01d, 20f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            Assert.That(MissionMapOverlay.BuildActualPoints(frames, new GeoCoordinateMapper(frames[0], 1f, 1f)), Has.Length.EqualTo(2));
        }

        [Test]
        public void BuildActualPoints_PreservesMappedDepthForThreeDimensionalMissionView()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "0", 0f, 120d, 25d, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "1", 1f, 120.01d, 25.01d, 80f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f)
            };

            var points = MissionMapOverlay.BuildActualPoints(frames, new GeoCoordinateMapper(frames[0], 1f, 0.1f));

            Assert.That(points[1].y, Is.EqualTo(-8f).Within(0.001f));
        }

        [Test]
        public void ComputeHorizontalExtent_EnclosesTheTrajectoryWithPadding()
        {
            var extent = MissionMapOverlay.ComputeHorizontalExtent(new[]
            {
                new UnityEngine.Vector3(-15f, 0f, -4f),
                new UnityEngine.Vector3(25f, -2f, 12f)
            });

            Assert.That(extent, Is.EqualTo(52f).Within(0.001f));
        }

        [Test]
        public void ComputeHorizontalExtent_ReservesAnEngineeringMissionCorridor()
        {
            var extent = MissionMapOverlay.ComputeHorizontalExtent(new[]
            {
                new UnityEngine.Vector3(2f, 0f, 1f),
                new UnityEngine.Vector3(5f, -2f, 4f)
            });

            Assert.That(extent, Is.EqualTo(36f).Within(0.001f));
        }

        [Test]
        public void ComputeHorizontalExtents_ExpandsLongRoutePerAxisWithEngineeringMargin()
        {
            var extents = MissionMapOverlay.ComputeHorizontalExtents(new[]
            {
                new UnityEngine.Vector3(-200f, 0f, -20f),
                new UnityEngine.Vector3(300f, -10f, 80f)
            });

            Assert.That(extents.x, Is.EqualTo(580f).Within(0.001f));
            Assert.That(extents.y, Is.EqualTo(116f).Within(0.001f));
        }

        [Test]
        public void ComputeHorizontalCenter_CentersTheMissionVolumeOnTheRoute()
        {
            var center = MissionMapOverlay.ComputeHorizontalCenter(new[]
            {
                new UnityEngine.Vector3(-10f, 0f, 2f),
                new UnityEngine.Vector3(30f, -2f, 18f)
            });

            Assert.That(center.x, Is.EqualTo(10f));
            Assert.That(center.z, Is.EqualTo(10f));
            Assert.That(center.y, Is.EqualTo(0f));
        }

        [Test]
        public void BuildNoCurrentBaselinePoints_UsesOnlyPlannedCoordinates()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "0", 0f, 120d, 25d, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f, plannedLongitudeDeg: 120.02d, plannedLatitudeDeg: 25.01d),
                new TelemetryFrame(1, "1", 1f, 120.01d, 25.01d, 20f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f)
            };

            var points = MissionMapOverlay.BuildNoCurrentBaselinePoints(frames, new GeoCoordinateMapper(frames[0], 1f, 1f));

            Assert.That(points, Has.Length.EqualTo(1));
            Assert.That(points[0].x, Is.GreaterThan(1000f));
        }
    }
}
