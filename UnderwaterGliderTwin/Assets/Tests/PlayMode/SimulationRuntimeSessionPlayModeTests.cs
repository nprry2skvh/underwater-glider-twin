using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class SimulationRuntimeSessionPlayModeTests
    {
        private GameObject host;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null)
            {
                Object.Destroy(host);
                yield return null;
            }

            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            RuntimePredictionState.SetModelKind(PredictionModelKind.XGBoost);
            RuntimePredictionState.SetEnabled(true);
        }

        [UnityTest]
        public IEnumerator ProductionFutureGenerator_YieldsBetweenSlicesAndCompletes()
        {
            var profile = CreateSmallProfile();
            var frame = CreateSeedFrame(profile);
            var snapshot = SimulationStateSnapshot.FromFrame(frame, profile);
            host = new GameObject("SimulationFutureGeneratorHost");
            var generator = new SimulationFutureTrajectoryGenerator(host.AddComponent<CoroutineHost>());
            SimulationRebuildResult result = null;

            var operation = generator.GenerateFuture(snapshot, profile, 1, 128, completed => result = completed);
            yield return null;

            Assert.That(operation.IsCancelled, Is.False);
            Assert.That(result, Is.Null, "The first slice must yield control before completion.");

            var frameGuard = 0;
            while (result == null && frameGuard++ < 1000)
            {
                yield return null;
            }

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, result?.Error);
            Assert.That(result.Frames, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator ProductionFutureGenerator_CancellationCompletesWithCancelledResult()
        {
            var profile = CreateSmallProfile();
            var snapshot = SimulationStateSnapshot.FromFrame(CreateSeedFrame(profile), profile);
            host = new GameObject("SimulationFutureGeneratorCancelHost");
            var generator = new SimulationFutureTrajectoryGenerator(host.AddComponent<CoroutineHost>());
            SimulationRebuildResult result = null;

            var operation = generator.GenerateFuture(snapshot, profile, 1, 128, completed => result = completed);
            yield return null;
            operation.Cancel();
            yield return null;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.WasCancelled, Is.True);
        }

        [UnityTest]
        public IEnumerator ProductionFutureGenerator_ReportsCoroutineInputErrors()
        {
            var profile = CreateSmallProfile();
            var snapshot = SimulationStateSnapshot.FromFrame(CreateSeedFrame(profile), profile);
            host = new GameObject("SimulationFutureGeneratorErrorHost");
            var generator = new SimulationFutureTrajectoryGenerator(host.AddComponent<CoroutineHost>());
            SimulationRebuildResult result = null;

            generator.GenerateFuture(snapshot, null, 1, 128, completed => result = completed);
            yield return null;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("profile"));
        }

        [UnityTest]
        public IEnumerator BootstrapSimulationHotUpdate_KeepsScenePlaybackAndCameraIdentity()
        {
            RuntimeDataSourceState.UseSimulation(CreateSmallProfile());
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var bootstrap = Object.FindObjectOfType<TwinBootstrap>();
            var camera = Camera.main;
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.SimulationSession, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            var sceneHandle = SceneManager.GetActiveScene().handle;
            var playback = bootstrap.PlaybackController;
            var cameraId = camera.GetInstanceID();
            var cameraPosition = camera.transform.position;
            var cameraRotation = camera.transform.rotation;
            var cameraFov = camera.fieldOfView;
            var candidate = CreateSmallProfile();
            var reload = typeof(TwinBootstrap).GetMethod(
                "ReloadFromSimulationProfile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(reload, Is.Not.Null);

            reload.Invoke(bootstrap, new object[] { candidate });
            var waitFrames = 0;
            while (bootstrap.SimulationSession.IsRebuildPending && waitFrames++ < 1000)
            {
                yield return null;
            }

            Assert.That(bootstrap.SimulationSession.IsRebuildPending, Is.False, bootstrap.SimulationSession.LastError);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
            Assert.That(bootstrap.PlaybackController, Is.SameAs(playback));
            Assert.That(Camera.main.GetInstanceID(), Is.EqualTo(cameraId));
            Assert.That(Camera.main.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(Camera.main.transform.rotation, Is.EqualTo(cameraRotation));
            Assert.That(Camera.main.fieldOfView, Is.EqualTo(cameraFov));
        }

        private static SimulationProfile CreateSmallProfile()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 120f;
            profile.SampleIntervalSeconds = 1f;
            profile.TargetDepthM = 20f;
            profile.WaterColumnDepthM = 30f;
            profile.HorizontalSpeedMps = 0f;
            profile.Dynamics.CruiseSpeedMps = 0f;
            profile.Dynamics.TurnaroundDurationSeconds = 10f;
            profile.DescentNetBuoyancyForceN = -12f;
            profile.AscentNetBuoyancyForceN = 12f;
            return profile;
        }

        private static TelemetryFrame CreateSeedFrame(SimulationProfile profile)
        {
            return new TelemetryFrame(
                0,
                "seed",
                0f,
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                0f,
                profile.WaterColumnDepthM,
                profile.StartHeadingDeg,
                0f,
                0f,
                28.6f,
                0.2f,
                96f,
                "Parameter Simulation",
                "Surface",
                1f,
                profile.StartHeadingDeg,
                0f,
                profile.WaterColumnDepthM,
                0f,
                0f,
                profile.HeadingDeltaPerCycleDeg);
        }

        private sealed class CoroutineHost : MonoBehaviour
        {
        }
    }
}
