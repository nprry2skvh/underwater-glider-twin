using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class VisualizationTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var obj in Object.FindObjectsOfType<GameObject>())
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void GliderVisualBuilder_BuildsNamedGliderWithBodyAndWings()
        {
            var glider = GliderVisualBuilder.Build();

            Assert.That(glider.name, Is.EqualTo("Glider"));
            Assert.That(glider.transform.Find("Body"), Is.Not.Null);
            Assert.That(glider.transform.Find("LeftWing"), Is.Not.Null);
            Assert.That(glider.transform.Find("RightWing"), Is.Not.Null);
            Assert.That(glider.transform.Find("NoseMarker"), Is.Not.Null);
            Assert.That(glider.transform.Find("Body").GetComponent<Renderer>().sharedMaterial.shader, Is.Not.Null);
        }

        [Test]
        public void GliderTransformDriver_AppliesCurrentFramePose()
        {
            var frames = Frames(3);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = new GameObject("Playback").AddComponent<PlaybackController>();
            playback.Initialize(new PlaybackModel(frames, rowsPerSecond: 1f));
            playback.Seek(1f);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);

            Assert.That(glider.transform.position.y, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(glider.transform.position.z, Is.GreaterThan(0f));
        }

        [Test]
        public void TrajectoryView_StoresFullTrajectoryPoints()
        {
            var frames = Frames(12);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = new GameObject("Playback").AddComponent<PlaybackController>();
            playback.Initialize(new PlaybackModel(frames, rowsPerSecond: 1f));
            var view = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();

            view.Initialize(frames, mapper, playback);

            Assert.That(view.FullTrajectoryPoints, Has.Length.EqualTo(12));
            Assert.That(GameObject.Find("FullTrajectory"), Is.Not.Null);
            Assert.That(GameObject.Find("TravelledTrajectory"), Is.Not.Null);
        }

        [Test]
        public void UnderwaterEnvironmentBuilder_CreatesEnvironmentAndTogglesEffects()
        {
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();

            environment.Build();
            environment.SetFogEnabled(false);
            environment.SetParticlesEnabled(false);

            Assert.That(GameObject.Find("Seabed"), Is.Not.Null);
            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(environment.ParticlesEnabled, Is.False);
        }

        [Test]
        public void TwinCameraController_SetModeUpdatesCurrentMode()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.AddComponent<Camera>();
            var controller = cameraObject.AddComponent<TwinCameraController>();

            controller.SetMode(CameraMode.Global);

            Assert.That(controller.CurrentMode, Is.EqualTo(CameraMode.Global));
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(i, $"t{i}", 120, 25 + i * 0.0001, i * 10f, 100, i * 5f, 0, 0, 28, 0, 95, "mode", "state", 1, 0, 0, 0, 0, 0, 0));
            }

            return frames;
        }
    }
}
