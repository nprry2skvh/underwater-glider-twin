using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
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
        public void GliderVisualBuilder_BuildsYellowModularGlider()
        {
            var glider = GliderVisualBuilder.Build();

            Assert.That(glider.name, Is.EqualTo("Glider"));
            Assert.That(glider.transform.localScale.x, Is.InRange(0.5f, 0.56f));
            Assert.That(glider.transform.Find("PressureHull"), Is.Not.Null);
            Assert.That(glider.transform.Find("MainWing/PortControlSurface"), Is.Not.Null);
            Assert.That(glider.transform.Find("MainWing/StarboardControlSurface"), Is.Not.Null);
            Assert.That(glider.transform.Find("TailBoom/HorizontalTail"), Is.Not.Null);
            Assert.That(glider.transform.Find("TailBoom/VerticalTail"), Is.Not.Null);
            Assert.That(glider.transform.Find("NoseSensorCover"), Is.Not.Null);
            Assert.That(glider.transform.Find("RollReferenceLine"), Is.Not.Null);
            Assert.That(glider.transform.Find("TopAttitudeStripe"), Is.Not.Null);
            Assert.That(glider.transform.Find("MainWing/PortWingTip"), Is.Not.Null);
            Assert.That(glider.transform.Find("MainWing/StarboardWingTip"), Is.Not.Null);
            Assert.That(glider.transform.Find("PressureHull").GetComponent<Renderer>().sharedMaterial.color.g, Is.GreaterThan(0.55f));
        }

        [Test]
        public void GliderVisualBuilder_HullNormalsFaceOutward()
        {
            var glider = GliderVisualBuilder.Build();
            var mesh = glider.transform.Find("PressureHull").GetComponent<MeshFilter>().sharedMesh;
            var vertexIndex = 2 * 16;
            var radialDirection = new Vector3(mesh.vertices[vertexIndex].x, mesh.vertices[vertexIndex].y, 0f).normalized;

            Assert.That(Vector3.Dot(mesh.normals[vertexIndex], radialDirection), Is.GreaterThan(0f));
        }

        [Test]
        public void GliderTransformDriver_AppliesCurrentFramePose()
        {
            var frames = Frames(3);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            playback.Seek(1f);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);

            Assert.That(glider.transform.position.y, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(glider.transform.position.z, Is.GreaterThan(0f));
        }

        [Test]
        public void GliderTransformDriver_AlignsTheVisualFuselageWithTheGroundTrack()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120.0000, 25.0000, 0f, 100f, 0f, 0f, 3f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 10f, 120.0001, 25.0000, 10f, 90f, 0f, 0f, 3f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(2, "t2", 20f, 120.0002, 25.0000, 20f, 80f, 0f, 0f, 3f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            playback.Seek(0.5f);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);

            var groundTrack = (mapper.Map(frames[2]) - mapper.Map(frames[0])).normalized;
            Assert.That(Vector3.Dot(glider.transform.forward, groundTrack), Is.GreaterThan(0.99f));
        }

        [Test]
        public void GliderTransformDriver_SmoothsPlaybackPoseChanges()
        {
            var frames = Frames(3);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);
            playback.SetPlaying(true);
            playback.Step(1f);

            Assert.That(glider.transform.position.y, Is.LessThan(0f));
            Assert.That(glider.transform.position.y, Is.GreaterThan(-10f));
        }

        [Test]
        public void GliderTransformDriver_ReusesLastValidPositionWhenCoordinatesDropOut()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120.0, 25.0, 5f, 100f, 30f, -10f, 0f, 28.5f, 0.2f, 90f, "mode", "state", 1f, 30f, 10f, 100f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 10f, 0.0, 0.0, 20f, 100f, 30f, 10f, 0f, 28.5f, 0.2f, 90f, "mode", "state", 1f, 30f, 10f, 100f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);
            var firstPosition = glider.transform.position;
            playback.Seek(1f);

            Assert.That(glider.transform.position.x, Is.EqualTo(firstPosition.x).Within(0.001f));
            Assert.That(glider.transform.position.z, Is.EqualTo(firstPosition.z).Within(0.001f));
            Assert.That(glider.transform.position.y, Is.EqualTo(-20f).Within(0.001f));
        }

        [Test]
        public void GliderVisualController_DeflectsSurfacesAndShowsVectorsForSimulation()
        {
            var controller = GliderVisualBuilder.Build().AddComponent<GliderVisualController>();
            var diagnostics = new SimulationDiagnostics(Vector3.forward, Vector3.right, 4f, 5f, 12f);
            var frame = new TelemetryFrame(0, "t", 0f, 120d, 25d, 0f, 0f, 30f, -14f, 18f,
                0f, 0f, 90f, "Parameter Simulation", "Glide", 1f, 30f, 10f, 0f, 0f, 0f, 20f, diagnostics);

            controller.ApplyFrame(frame);

            Assert.That(controller.PortControlSurface.localRotation.eulerAngles.x, Is.Not.EqualTo(0f).Within(0.01f));
            Assert.That(controller.GroundVelocityVector.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void GliderVisualController_HidesVectorsForCsvReplay()
        {
            var controller = GliderVisualBuilder.Build().AddComponent<GliderVisualController>();
            controller.ApplyFrame(Frames(1)[0]);

            Assert.That(controller.CurrentVector.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void TrajectoryView_StoresFullTrajectoryPointsAndCreatesThreeTrajectoryLayers()
        {
            var frames = Frames(12);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(frames, mapper, playback);
            var view = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();

            view.Initialize(frames, mapper, playback, prediction);

            Assert.That(view.FullTrajectoryPoints, Has.Length.GreaterThan(12));
            Assert.That(view.transform.Find("ActualBackdropLine"), Is.Not.Null);
            Assert.That(view.transform.Find("ActualTrajectoryLine"), Is.Not.Null);
            Assert.That(view.transform.Find("RemainingTrajectoryLine"), Is.Not.Null);
            Assert.That(view.transform.Find("PredictedTrajectoryLine"), Is.Not.Null);
            Assert.That(view.transform.Find("PredictedHistoryLine"), Is.Not.Null);
            Assert.That(view.transform.Find("PlannedTrajectoryLine"), Is.Not.Null);
            Assert.That(view.transform.Find("CurrentPositionMarker"), Is.Not.Null);
        }

        [Test]
        public void TrajectoryView_HidesBackdropInFollowMode()
        {
            var frames = Frames(12);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(frames, mapper, playback);
            var view = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();

            view.Initialize(frames, mapper, playback, prediction);
            view.SetVisible(true);
            view.SetCameraMode(CameraMode.Follow);

            Assert.That(view.transform.Find("ActualTrajectoryLine").gameObject.activeSelf, Is.True);
            Assert.That(view.transform.Find("ActualBackdropLine").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void TrajectoryView_ShowsPredictedAndPlannedTrajectories()
        {
            var frames = Frames(24);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(frames, mapper, playback);
            var view = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();

            view.Initialize(frames, mapper, playback, prediction);
            view.SetVisible(true);
            view.SetCameraMode(CameraMode.Global);
            playback.Seek(0.5f);
            playback.Seek(0.8f);

            Assert.That(view.transform.Find("ActualTrajectoryLine").GetComponent<LineRenderer>().positionCount, Is.GreaterThan(1));
            Assert.That(view.transform.Find("RemainingTrajectoryLine").GetComponent<LineRenderer>().positionCount, Is.GreaterThan(1));
            Assert.That(view.transform.Find("PredictedTrajectoryLine").GetComponent<LineRenderer>().positionCount, Is.EqualTo(0));
            Assert.That(view.transform.Find("PredictedHistoryLine").GetComponent<LineRenderer>().positionCount, Is.EqualTo(0));
            Assert.That(view.transform.Find("PlannedTrajectoryLine").GetComponent<LineRenderer>().positionCount, Is.EqualTo(2));
        }

        [Test]
        public void TrajectoryView_ShowsIntendedPathForSimulationFrames()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120d, 25d, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 90f, "Parameter Simulation", "Glide", 1f, 0f, 0f, 0f, 0f, 0f, 0f, null, 120d, 25d),
                new TelemetryFrame(1, "t1", 1f, 120.0001d, 25.0001d, 5f, 0f, 0f, 0f, 0f, 0f, 0f, 90f, "Parameter Simulation", "Glide", 1f, 0f, 0f, 0f, 0f, 0f, 0f, null, 120.00005d, 25.0001d),
                new TelemetryFrame(2, "t2", 2f, 120.0002d, 25.0002d, 10f, 0f, 0f, 0f, 0f, 0f, 0f, 90f, "Parameter Simulation", "Glide", 1f, 0f, 0f, 0f, 0f, 0f, 0f, null, 120.0001d, 25.0002d)
            };
            var playback = CreatePlayback(frames);
            var view = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();

            view.Initialize(frames, new GeoCoordinateMapper(frames[0], 1f, 1f), playback, null);

            Assert.That(view.transform.Find("IntendedTrajectoryLine").GetComponent<LineRenderer>().positionCount, Is.EqualTo(3));
        }

        [Test]
        public void TrajectorySampler_SkipsFramesWithInvalidCoordinates()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 0.0, 0.0, 0f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 10f, 120.0, 25.0, 10f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(2, "t2", 20f, 120.0001, 25.0001, 20f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(frames[1], horizontalScale: 1f, depthScale: 1f);

            var points = TrajectorySampler.Sample(frames, mapper, 8);

            Assert.That(points, Has.Length.EqualTo(2));
        }

        [Test]
        public void TrajectorySampler_SmoothSamplingAddsCubicIntermediatePointsWithoutMovingEndpoints()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120d, 25d, 0f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 1f, 120.00001d, 25d, 10f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(2, "t2", 2f, 120.00001d, 25.00001d, 20f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(3, "t3", 3f, 120.00002d, 25.00001d, 30f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(frames[0], 1f, 1f);

            var points = TrajectorySampler.SampleSmooth(frames, mapper, 16);

            Assert.That(points.Length, Is.GreaterThan(frames.Length));
            Assert.That(points[0], Is.EqualTo(mapper.Map(frames[0])));
            Assert.That(points[^1], Is.EqualTo(mapper.Map(frames[^1])));
            for (var i = 0; i < points.Length; i++)
            {
                Assert.That(float.IsNaN(points[i].x) || float.IsInfinity(points[i].x), Is.False);
                Assert.That(float.IsNaN(points[i].y) || float.IsInfinity(points[i].y), Is.False);
                Assert.That(float.IsNaN(points[i].z) || float.IsInfinity(points[i].z), Is.False);
            }
        }

        [Test]
        public void TrajectorySampler_PreservesEveryTurnaroundFrameWhenReducingALongMission()
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < 200; i++)
            {
                var runState = i >= 90 && i < 96 ? "Turnaround" : "Glide";
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i,
                    120d + i * 0.00001d,
                    25d,
                    i,
                    100f,
                    0f,
                    0f,
                    0f,
                    28f,
                    0f,
                    95f,
                    "Parameter Simulation",
                    runState,
                    1f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f));
            }

            var points = TrajectorySampler.Sample(frames, new GeoCoordinateMapper(frames[0], 1f, 1f), 12);

            for (var depth = 90; depth < 96; depth++)
            {
                Assert.That(points, Has.Some.Matches<Vector3>(point => Mathf.Abs(point.y + depth) < 0.001f));
            }
        }

        [Test]
        public void UnderwaterEnvironmentBuilder_CreatesEnvironmentAndTogglesEffects()
        {
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();

            environment.Build();
            environment.SetFogEnabled(false);
            environment.SetParticlesEnabled(false);

            Assert.That(GameObject.Find("Seabed"), Is.Not.Null);
            Assert.That(GameObject.Find("GodRay1"), Is.Null);
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

        [Test]
        public void TwinCameraController_FirstFollowFrameKeepsTargetInView()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 12f, -20f),
                Quaternion.Euler(25f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 58f;
            var controller = cameraObject.AddComponent<TwinCameraController>();
            var target = new GameObject("Target").transform;
            target.SetPositionAndRotation(new Vector3(0f, -8f, 0f), Quaternion.Euler(0f, 112f, 0f));

            controller.Initialize(target, new[] { target.position });

            var viewportPoint = camera.WorldToViewportPoint(target.position);
            Assert.That(viewportPoint.z, Is.GreaterThan(0f));
            Assert.That(viewportPoint.x, Is.InRange(0.15f, 0.85f));
            Assert.That(viewportPoint.y, Is.InRange(0.15f, 0.85f));
        }

        [Test]
        public void TwinCameraController_GlobalModeFramesAboveTrajectoryBounds()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.AddComponent<Camera>();
            var controller = cameraObject.AddComponent<TwinCameraController>();
            var target = new GameObject("Target").transform;
            var points = new[]
            {
                new Vector3(0f, -10f, 0f),
                new Vector3(80f, -60f, 120f),
                new Vector3(-40f, -25f, 180f)
            };

            controller.Initialize(target, points);
            controller.SetMode(CameraMode.Global);

            Assert.That(cameraObject.transform.position.y, Is.GreaterThan(-5f));
            Assert.That(controller.CurrentMode, Is.EqualTo(CameraMode.Global));
        }

        [Test]
        public void TwinCameraController_GlobalModeDoesNotClampLongMissionFraming()
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 58f;
            var controller = cameraObject.AddComponent<TwinCameraController>();
            var target = new GameObject("Target").transform;
            var points = new[]
            {
                new Vector3(-420f, 0f, -40f),
                new Vector3(420f, -60f, 40f)
            };

            controller.Initialize(target, points);
            controller.SetMode(CameraMode.Global);

            Assert.That(Vector3.Distance(cameraObject.transform.position, new Vector3(0f, -30f, 0f)), Is.GreaterThan(320f));
        }

        [Test]
        public void FollowCameraRig_ComputesHorizontalOffsetWithoutPitchJitter()
        {
            var rig = new FollowCameraRig();

            var desired = rig.ComputeDesiredPosition(Vector3.zero, new Vector3(0f, -1f, 1f).normalized);

            Assert.That(desired.y, Is.EqualTo(FollowCameraRig.FollowHeight).Within(0.001f));
            Assert.That(desired.x, Is.GreaterThan(2f));
            Assert.That(desired.z, Is.InRange(-9f, -6f));
        }

        private static PlaybackController CreatePlayback(IReadOnlyList<TelemetryFrame> frames)
        {
            var playback = new GameObject("Playback").AddComponent<PlaybackController>();
            playback.Initialize(new PlaybackModel(frames, rowsPerSecond: 1f));
            return playback;
        }

        private static PredictionController CreatePrediction(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, PlaybackController playback)
        {
            var prediction = new GameObject("Prediction").AddComponent<PredictionController>();
            prediction.Initialize(frames, mapper, playback);
            return prediction;
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(i, $"t{i}", i * 10f, 120, 25 + i * 0.0001, i * 10f, 100, i * 5f, 0, 0, 28, 0, 95, "mode", "state", 1, 0, 0, 0, 0, 0, 0));
            }

            return frames;
        }
    }
}
