using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class TrajectoryExportCodecTests
    {
        [Test]
        public void JsonRoundTripPreservesSegmentsFramesMissionAndPlayback()
        {
            var snapshot = BuildExportSnapshotWithTwoSuccessfulSegments();
            var json = TrajectoryJsonCodec.Serialize(snapshot);

            Assert.That(json, Does.Contain("\"profileSequence\": 1"));
            Assert.That(json, Does.Contain("dynamicsValues"));
            Assert.That(json, Does.Contain("oceanFieldSamples"));
            Assert.That(json, Does.Not.Contain("NaN"));
            Assert.That(json, Does.Not.Contain("Infinity"));
            Assert.That(TrajectoryJsonImporter.TryRestoreJson(json, out var restored, out var error), Is.True, error);
            Assert.That(restored.Frames.Count, Is.EqualTo(snapshot.Timeline.Frames.Count));
            Assert.That(restored.Segments.Count, Is.EqualTo(snapshot.Timeline.Segments.Count));
            Assert.That(restored.Frames[2].ProfileSequence, Is.EqualTo(1));
            Assert.That(restored.Segments[1].Profile.TargetDepthM, Is.EqualTo(177f));
            Assert.That(restored.Segments[1].Profile.OceanCurrentField.Samples.Count, Is.EqualTo(1));
            Assert.That(restored.Playback.CurrentFrameIndex, Is.EqualTo(snapshot.Playback.CurrentFrameIndex));
        }

        [Test]
        public void CsvUsesFixedHeaderAndUtf8BomContract()
        {
            var snapshot = BuildExportSnapshotWithMissingPlannedCoordinates();
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true), 1024, true))
            {
                TrajectoryCsvWriter.Write(writer, snapshot);
                writer.Flush();
                var bytes = stream.ToArray();
                Assert.That(bytes[0], Is.EqualTo(0xEF));
                Assert.That(bytes[1], Is.EqualTo(0xBB));
                Assert.That(bytes[2], Is.EqualTo(0xBF));
                Assert.That(Encoding.UTF8.GetString(bytes), Does.Contain("EastM,NorthM,UpM,DepthM"));
            }
        }

        private static TrajectoryExportSnapshot BuildExportSnapshotWithTwoSuccessfulSegments()
        {
            var profile = SimulationProfile.Default;
            profile.TargetDepthM = 177f;
            profile.OceanCurrentProfile.AddLayer(new OceanCurrentLayer(0f, 100f, 0.1f, 0.2f));
            profile.OceanCurrentField.ReplaceSamples(new[] { new OceanCurrentFieldSample(120d, 25d, 10f, 0f, 0.1f, 0.2f, 0.01f) });
            var frames = new List<TelemetryFrame> { Frame(0, 0f, 0), Frame(1, 1f, 0) };
            var timeline = new SimulationTrajectoryTimeline(
                frames,
                new SimulationTimelineSegment(0, 0, 0, 0f, SimulationProfile.Default, System.DateTime.UtcNow));
            timeline.ReplaceFutureFrom(
                1,
                new[] { Frame(2, 2f, 1), Frame(3, 3f, 1) },
                new SimulationTimelineSegment(1, 12, 2, 2f, profile, System.DateTime.UtcNow));
            return new TrajectoryExportSnapshot(
                timeline.CommittedSnapshot,
                new TrajectoryPlaybackState(2, 2.25f, 2f, true, 1.5f, 1));
        }

        private static TrajectoryExportSnapshot BuildExportSnapshotWithMissingPlannedCoordinates()
        {
            return new TrajectoryExportSnapshot(
                new SimulationTrajectoryTimeline(
                    new[] { Frame(0, 0f, 0), Frame(1, 1f, 0) },
                    new SimulationTimelineSegment(0, 0, 0, 0f, SimulationProfile.Default, System.DateTime.UtcNow)).CommittedSnapshot,
                new TrajectoryPlaybackState(0, 0f, 0f, false, 1f, 1));
        }

        private static TelemetryFrame Frame(int row, float elapsed, int sequence)
        {
            return new TelemetryFrame(row, "t" + row, elapsed, 120d + row * 0.001d, 25d + row * 0.001d, 10f + row, 500f, 0f, 0f, 0f, 28f, 0.5f, 95f, "Simulation", "Glide", 1f, 0f, 20f, 500f, 0f, 0f, 0f, null, double.NaN, double.NaN, SimulationMissionState.AtSurface(), sequence);
        }
    }
}
