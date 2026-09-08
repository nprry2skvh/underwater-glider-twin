using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ContinuousTrajectoryExportPlayModeTests
    {
        [UnityTest]
        public IEnumerator ContinuousUpdatesProduceReplayableFourFilePackage()
        {
            RuntimeDataSourceState.UseSimulation(CreateSmallProfile());
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var bootstrap = UnityEngine.Object.FindObjectOfType<TwinBootstrap>();
            var camera = Camera.main;
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.SimulationSession, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            var sceneHandle = SceneManager.GetActiveScene().handle;
            var playbackId = bootstrap.PlaybackController.GetInstanceID();
            var cameraId = camera.GetInstanceID();
            var reload = typeof(TwinBootstrap).GetMethod("ReloadFromSimulationProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(reload, Is.Not.Null);

            var root = Path.Combine(Application.temporaryCachePath, "continuous-trajectory-export-test");
            try
            {
                yield return ApplyAndWait(bootstrap, reload, ProfileWithDepth(20f, 4));
                bootstrap.PlaybackController.Seek(1f);
                yield return ApplyAndWait(bootstrap, reload, ProfileWithDepth(25f, 5));
                bootstrap.PlaybackController.Seek(1f);
                yield return ApplyAndWait(bootstrap, reload, ProfileWithDepth(30f, 6));
                Assert.That(bootstrap.SimulationSession.Timeline.CommittedSnapshot.Segments.Count, Is.EqualTo(4));
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
                Assert.That(bootstrap.PlaybackController.GetInstanceID(), Is.EqualTo(playbackId));
                Assert.That(Camera.main.GetInstanceID(), Is.EqualTo(cameraId));

                var renderView = UnityEngine.Object.FindObjectOfType<TrajectoryExportRenderView>();
                Assert.That(renderView, Is.Not.Null);
                var snapshot = TrajectoryExportSnapshot.Capture(bootstrap.SimulationSession.Timeline.CommittedSnapshot, bootstrap.PlaybackController.Model, null, new CameraSnapshot(Camera.main));
                var service = new TrajectoryExportService(root, renderView);
                TrajectoryExportResult result = null;
                service.BeginExport(snapshot, root, null, completed => result = completed);
                var guard = 0;
                while (result == null && guard++ < 240) yield return null;

                Assert.That(result, Is.Not.Null);
                Assert.That(result.Succeeded, Is.True, result.Error);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "trajectory.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "trajectory.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.PublishedDirectory, "trajectory.glb")), Is.True);
                Assert.That(new FileInfo(Path.Combine(result.PublishedDirectory, "trajectory.png")).Length, Is.GreaterThan(100));
                var manifest = JsonUtility.FromJson<TrajectoryExportManifest>(File.ReadAllText(Path.Combine(result.PublishedDirectory, "manifest.json")));
                Assert.That(manifest.Validate(result.PublishedDirectory, out var manifestError), Is.True, manifestError);
                Assert.That(TrajectoryJsonImporter.TryRestore(result.PublishedDirectory, out var restored, out var restoredPlayback, out var error), Is.True, error);
                Assert.That(restored.CommittedSnapshot.Segments.Count, Is.EqualTo(4));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            }
        }

        private static IEnumerator ApplyAndWait(TwinBootstrap bootstrap, MethodInfo reload, SimulationProfile profile)
        {
            reload.Invoke(bootstrap, new object[] { profile });
            var guard = 0;
            while (bootstrap.SimulationSession.IsRebuildPending && guard++ < 1200) yield return null;
            Assert.That(bootstrap.SimulationSession.IsRebuildPending, Is.False, bootstrap.SimulationSession.LastError);
            Assert.That(bootstrap.SimulationSession.LastError, Is.Null.Or.Empty);
        }

        private static SimulationProfile ProfileWithDepth(float depth, int cycleCount)
        {
            var profile = CreateSmallProfile();
            profile.TargetDepthM = depth;
            profile.CycleCount = cycleCount;
            return profile;
        }

        private static SimulationProfile CreateSmallProfile()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 3;
            profile.CycleDurationSeconds = 30f;
            profile.SampleIntervalSeconds = 1f;
            profile.TargetDepthM = 20f;
            profile.WaterColumnDepthM = 60f;
            profile.HorizontalSpeedMps = 0f;
            profile.Dynamics.CruiseSpeedMps = 0f;
            profile.Dynamics.TurnaroundDurationSeconds = 10f;
            profile.DescentNetBuoyancyForceN = -12f;
            profile.AscentNetBuoyancyForceN = 12f;
            return profile;
        }
    }
}
