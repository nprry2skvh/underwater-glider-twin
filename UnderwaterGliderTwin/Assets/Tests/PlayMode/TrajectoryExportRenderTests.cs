using System.Collections;
using System.IO;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class TrajectoryExportRenderTests
    {
        private GameObject host;
        private string pngPath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (!string.IsNullOrWhiteSpace(pngPath) && File.Exists(pngPath)) File.Delete(pngPath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapshotRenderReportsCompletionOnlyAfterPngExists()
        {
            host = new GameObject("TrajectoryExportRenderHost");
            var view = host.AddComponent<TrajectoryExportRenderView>();
            var frame = new TelemetryFrame(0, "0", 0f, 120d, 25d, 1f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Surface", 1f, 0f, 10f, 500f, 0f, 0f, 0f);
            var timeline = new SimulationTrajectoryTimeline(new[] { frame }, new SimulationTimelineSegment(0, 0, 0, 0f, SimulationProfile.Default, System.DateTime.UtcNow));
            var snapshot = new TrajectoryExportSnapshot(timeline.CommittedSnapshot, new TrajectoryPlaybackState(0, 0f, 0f, false, 1f, 1));
            pngPath = Path.Combine(Application.temporaryCachePath, "trajectory-export-test.png");
            var completed = false;
            var success = false;
            view.CaptureAsync(snapshot, pngPath, (ok, error) => { completed = true; success = ok; });
            var guard = 0;
            while (!completed && guard++ < 60) yield return null;
            Assert.That(completed, Is.True);
            Assert.That(success, Is.True);
            Assert.That(File.Exists(pngPath), Is.True);
            Assert.That(new FileInfo(pngPath).Length, Is.GreaterThan(100));
        }

        [UnityTest]
        public IEnumerator RuntimeExportPublishesRenderedPngAndManifest()
        {
            host = new GameObject("TrajectoryExportServiceHost");
            var view = host.AddComponent<TrajectoryExportRenderView>();
            var frame = new TelemetryFrame(0, "0", 0f, 120d, 25d, 1f, 500f, 0f, 0f, 0f, 28f, 0f, 100f, "Simulation", "Surface", 1f, 0f, 10f, 500f, 0f, 0f, 0f);
            var timeline = new SimulationTrajectoryTimeline(new[] { frame }, new SimulationTimelineSegment(0, 0, 0, 0f, SimulationProfile.Default, System.DateTime.UtcNow));
            var snapshot = new TrajectoryExportSnapshot(timeline.CommittedSnapshot, new TrajectoryPlaybackState(0, 0f, 0f, false, 1f, 1));
            var root = Path.Combine(Application.temporaryCachePath, "trajectory-export-service-test");
            var service = new TrajectoryExportService(root, view);
            TrajectoryExportResult result = null;
            service.BeginExport(snapshot, root, null, completed => result = completed);
            var guard = 0;
            while (result == null && guard++ < 120) yield return null;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(new FileInfo(Path.Combine(result.PublishedDirectory, "trajectory.png")).Length, Is.GreaterThan(100));
            Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "manifest.json")), Is.True);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
