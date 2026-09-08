using System;
using System.IO;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class TrajectoryExportServiceTests
    {
        [Test]
        public void ExportPublishesFourOutputsAndManifestAfterHashesAreComputed()
        {
            var root = Path.Combine(Path.GetTempPath(), "glider-export-" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = new TrajectoryExportService(root).ExportSynchronously(BuildSnapshot());
                Assert.That(result.Succeeded, Is.True, result.Error);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "trajectory.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "trajectory.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "trajectory.glb")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "trajectory.png")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "manifest.json")), Is.True);
                Assert.That(result.Files.Count, Is.EqualTo(4));
                Assert.That(Directory.Exists(Path.Combine(root, ".staging")), Is.True);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static TrajectoryExportSnapshot BuildSnapshot()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "0", 0f, 120d, 25d, 3f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Glide", 1f, 0f, 20f, 500f, 0f, 0f, 0f, null, double.NaN, double.NaN, SimulationMissionState.AtSurface(), 0),
                new TelemetryFrame(1, "1", 1f, 120.001d, 25.001d, 4f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Glide", 1f, 0f, 20f, 500f, 0f, 0f, 0f, null, double.NaN, double.NaN, SimulationMissionState.AtSurface(), 0)
            };
            var timeline = new SimulationTrajectoryTimeline(frames, new SimulationTimelineSegment(0, 0, 0, 0f, SimulationProfile.Default, DateTime.UtcNow));
            return new TrajectoryExportSnapshot(timeline.CommittedSnapshot, new TrajectoryPlaybackState(0, 0f, 0f, false, 1f, 1));
        }
    }
}
