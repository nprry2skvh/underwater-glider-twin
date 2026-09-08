using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class GlbTrajectoryWriterTests
    {
        [Test]
        public void GlbCoordinateMappingUsesRightHandedYUpContract()
        {
            var result = GlbCoordinateMapper.ToGlbPosition(1d, 2d, 3d);
            Assert.That(result.x, Is.EqualTo(1f));
            Assert.That(result.y, Is.EqualTo(-3f));
            Assert.That(result.z, Is.EqualTo(-2f));
        }

        [Test]
        public void GlbWriterEmitsValidHeaderAndSegmentNodeNames()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "0", 0f, 120d, 25d, 3f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Glide", 1f, 0f, 20f, 500f, 0f, 0f, 0f, null, double.NaN, double.NaN, null, 0),
                new TelemetryFrame(1, "1", 1f, 120.001d, 25.001d, 4f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Glide", 1f, 0f, 20f, 500f, 0f, 0f, 0f, null, double.NaN, double.NaN, null, 1)
            };
            var timeline = new SimulationTrajectoryTimeline(new[] { frames[0] }, new SimulationTimelineSegment(0, 0, 0, 0f, SimulationProfile.Default, System.DateTime.UtcNow));
            timeline.ReplaceFutureFrom(0, new[] { frames[1] }, new SimulationTimelineSegment(1, 4, 1, 1f, SimulationProfile.Default, System.DateTime.UtcNow));
            var snapshot = new TrajectoryExportSnapshot(timeline.CommittedSnapshot, new TrajectoryPlaybackState(0, 0f, 0f, false, 1f, 1));
            using (var stream = new MemoryStream())
            {
                GlbTrajectoryWriter.Write(stream, snapshot, 32);
                var bytes = stream.ToArray();
                Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("glTF"));
                Assert.That(BitConverter.ToInt32(bytes, 4), Is.EqualTo(2));
                Assert.That(Encoding.UTF8.GetString(bytes), Does.Contain("ProfileSequence-1"));
            }
        }
    }
}
