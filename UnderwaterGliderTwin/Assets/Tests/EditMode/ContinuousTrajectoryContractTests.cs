using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ContinuousTrajectoryContractTests
    {
        [Test]
        public void TimelineRetainsSupersededCommittedSegments()
        {
            var timeline = new SimulationTrajectoryTimeline(
                new[] { Frame(0, 0f, 0), Frame(1, 1f, 0), Frame(2, 2f, 0) },
                new SimulationTimelineSegment(0, 0, 0, 0f, SimulationProfile.Default, DateTime.UtcNow));

            timeline.ReplaceFutureFrom(0, new[] { Frame(1, 1f, 1), Frame(2, 2f, 1) },
                new SimulationTimelineSegment(1, 1, 1, 1f, SimulationProfile.Default, DateTime.UtcNow));
            timeline.ReplaceFutureFrom(0, new[] { Frame(1, 1f, 2), Frame(2, 2f, 2) },
                new SimulationTimelineSegment(2, 2, 1, 1f, SimulationProfile.Default, DateTime.UtcNow));

            Assert.That(timeline.CommittedSnapshot.SupersededSegments.Any(x => x.ProfileSequence == 1), Is.True);
            Assert.That(timeline.CommittedSnapshot.Segments[1].ProfileSequence, Is.EqualTo(2));
        }

        [Test]
        public void TimelinePlaybackUsesElapsedTimeForUnevenSamples()
        {
            var frames = new[] { Frame(0, 0f, 0), Frame(1, 2f, 0), Frame(2, 5f, 0) };
            var timeline = SimulationTrajectoryTimeline.CreateInitial(frames, SimulationProfile.Default);
            var model = new PlaybackModel(frames, 1f);
            model.BindTimeline(timeline);
            model.SetPlaying(true);

            model.Tick(1f);

            Assert.That(model.ContinuousElapsedSeconds, Is.EqualTo(2f).Within(0.001f));
            Assert.That(model.CurrentIndex, Is.EqualTo(1));
        }

        [Test]
        public void JsonRoundTripPreservesDiagnosticsAndEnuContract()
        {
            var diagnostics = new SimulationDiagnostics(
                UnityEngine.Vector3.forward,
                UnityEngine.Vector3.right,
                1f,
                2f,
                3f,
                4f,
                5f,
                6f,
                7f,
                UnityEngine.Vector3.one,
                UnityEngine.Vector3.one * 2f,
                8f,
                UnityEngine.Vector3.one * 3f,
                9f);
            var frames = new[]
            {
                new TelemetryFrame(0, "0", 0f, 120d, 25d, 3f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Surface", 1f, 0f, 20f, 500f, 0f, 0f, 0f, diagnostics, double.NaN, double.NaN, SimulationMissionState.AtSurface(), 0),
                new TelemetryFrame(1, "1", 1f, 120.001d, 25.001d, 4f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Glide", 1f, 0f, 20f, 500f, 0f, 0f, 0f, diagnostics, double.NaN, double.NaN, SimulationMissionState.AtSurface(), 0)
            };
            var timeline = SimulationTrajectoryTimeline.CreateInitial(frames, SimulationProfile.Default);
            var snapshot = new TrajectoryExportSnapshot(timeline.CommittedSnapshot, new TrajectoryPlaybackState(0, 0f, 0f, false, 1f, 1));

            var json = TrajectoryJsonCodec.Serialize(snapshot);

            Assert.That(json, Does.Contain("\"enu\""));
            Assert.That(json, Does.Contain("\"diagnostics\""));
            Assert.That(TrajectoryJsonImporter.TryRestoreJson(json, out var restored, out var error), Is.True, error);
            Assert.That(restored.Frames[0].Diagnostics.HasValue, Is.True);
        }

        [Test]
        public void GlbUsesDistinctMeshesForProfileSegments()
        {
            var frames = new[] { Frame(0, 0f, 0), Frame(1, 1f, 1), Frame(2, 2f, 1), Frame(3, 3f, 2), Frame(4, 4f, 2) };
            var timeline = SimulationTrajectoryTimeline.CreateInitial(new[] { frames[0] }, SimulationProfile.Default);
            timeline.ReplaceFutureFrom(0, new[] { frames[1], frames[2] }, new SimulationTimelineSegment(1, 1, 1, 1f, SimulationProfile.Default, DateTime.UtcNow));
            timeline.ReplaceFutureFrom(2, new[] { frames[3], frames[4] }, new SimulationTimelineSegment(2, 2, 3, 3f, SimulationProfile.Default, DateTime.UtcNow));
            var snapshot = new TrajectoryExportSnapshot(timeline.CommittedSnapshot, new TrajectoryPlaybackState(0, 0f, 0f, false, 1f, 1));

            using (var stream = new MemoryStream())
            {
                GlbTrajectoryWriter.Write(stream, snapshot);
                var text = Encoding.UTF8.GetString(stream.ToArray());
                Assert.That(text, Does.Contain("ProfileSequence-1"));
                Assert.That(text, Does.Contain("ProfileSequence-2"));
                Assert.That(text, Does.Contain("\"mesh\":1"));
            }
        }

        private static TelemetryFrame Frame(int row, float elapsed, int sequence)
        {
            return new TelemetryFrame(row, row.ToString(), elapsed, 120d + row * 0.001d, 25d, 3f + row, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Glide", 1f, 0f, 20f, 500f, 0f, 0f, 0f, null, double.NaN, double.NaN, SimulationMissionState.AtSurface(), sequence);
        }
    }
}
