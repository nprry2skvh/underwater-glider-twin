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
        }
    }
}
