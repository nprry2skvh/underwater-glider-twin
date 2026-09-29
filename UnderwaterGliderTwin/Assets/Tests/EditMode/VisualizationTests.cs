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
        public void GliderTransformDriver_UsesContinuousRawTelemetryPose()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120d, 25d, 10f, 90f, 350f, -10f, 170f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 10f, 120.001d, 25.002d, 30f, 70f, 10f, 10f, -170f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);
            playback.SetPlaying(true);
            playback.Step(0.5f);

            Assert.That(glider.transform.position.y, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(glider.transform.position.x, Is.EqualTo((mapper.Map(frames[1]).x + mapper.Map(frames[0]).x) * 0.5f).Within(0.001f));
            var expectedRotation = PoseMapper.ToRotation(0f, 0f, 180f, AttitudeSettings.Default);
            Assert.That(Quaternion.Angle(glider.transform.rotation, expectedRotation), Is.LessThan(0.01f));
        }

        [Test]
        public void GliderTransformDriver_UsesTelemetryAttitudeInsteadOfPathTangent()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120.0000, 25.0000, 0f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 10f, 120.0001, 25.0000, 10f, 90f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);

            Assert.That(Vector3.Dot(glider.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(glider.transform.forward, Vector3.right), Is.LessThan(0.01f));
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
        public void GliderTransformDriver_MissingCoordinateSeekDoesNotReuseFuturePosition()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120.0, 25.0, 5f, 100f, 0f, 0f, 0f, 28f, 0f, 90f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 10f, 0.0, 0.0, 20f, 100f, 0f, 0f, 0f, 28f, 0f, 90f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(2, "t2", 20f, 120.001, 25.0, 30f, 100f, 0f, 0f, 0f, 28f, 0f, 90f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var glider = new GameObject("Glider");
            glider.AddComponent<GliderTransformDriver>().Initialize(playback, mapper);

            playback.Seek(1f);
            playback.Seek(0.5f);

            Assert.That(glider.transform.position.x, Is.EqualTo(mapper.Map(frames[0]).x).Within(0.001f));
            Assert.That(glider.transform.position.z, Is.EqualTo(mapper.Map(frames[0]).z).Within(0.001f));
            Assert.That(glider.transform.position.y, Is.EqualTo(-20f).Within(0.001f));
        }

        [Test]
        public void GliderTransformDriver_UsesCommittedRawPositionEvenWhenItIsAnOutlier()
        {
            var frames = new List<TelemetryFrame>();
            var metersPerDegreeLongitude = 111320d * System.Math.Cos(25d * System.Math.PI / 180d);
            var eastMeters = new[] { 0d, 1d, 2d, 60d, 4d, 5d, 6d };
            for (var i = 0; i < eastMeters.Length; i++)
            {
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i,
                    120d + eastMeters[i] / metersPerDegreeLongitude,
                    25d,
                    i,
                    100f,
                    0f,
                    0f,
                    0f,
                    28f,
                    0f,
                    95f,
                    "mode",
                    "state",
                    1f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f));
            }

            var mapper = new GeoCoordinateMapper(frames[0], 1f, 1f);
            var playback = CreatePlayback(frames);
            playback.Seek(0.5f);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();

            driver.Initialize(playback, mapper);

            Assert.That(glider.transform.position.x, Is.EqualTo(mapper.Map(frames[3]).x).Within(0.001f));
        }

        [Test]
        public void GliderTransformDriver_DoesNotTeleportWhenSimulationFutureFramesAreRebuilt()
        {
            var original = new List<TelemetryFrame>();
            for (var i = 0; i < 25; i++)
            {
                original.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i,
                    120d + i * 0.00001d,
                    25d + i * 0.00001d,
                    i,
                    100f,
                    0f,
                    0f,
                    0f,
                    28f,
                    0f,
                    95f,
                    "Parameter Simulation",
                    "Glide",
                    1f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f));
            }

            var replacement = new List<TelemetryFrame>(original.GetRange(0, 13));
            for (var i = 13; i < 50; i++)
            {
                var eastMeters = i + (i - 12f) * 5f;
                var northMeters = i + (i - 12f) * 5f;
                replacement.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i,
                    120d + eastMeters / (111320d * System.Math.Cos(25d * System.Math.PI / 180d)),
                    25d + northMeters / 111320d,
                    i,
                    100f,
                    0f,
                    0f,
                    0f,
                    28f,
                    0f,
                    95f,
                    "Parameter Simulation",
                    "Glide",
                    1f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f));
            }

            var mapper = new GeoCoordinateMapper(original[0], 1f, 1f);
            var playback = CreatePlayback(original);
            playback.Seek(0.5f);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();
            driver.Initialize(playback, mapper);
            var before = glider.transform.position;
            var beforeFit = TrajectorySampler.Fit(original, mapper, 1200).PositionAt(playback.Model.ContinuousElapsedSeconds);
            var afterFit = TrajectorySampler.Fit(replacement, mapper, 1200).PositionAt(playback.Model.ContinuousElapsedSeconds);
            Assert.That(Vector3.Distance(beforeFit, afterFit), Is.GreaterThan(0.5f));

            playback.Model.ReplaceFrames(replacement, playback.Model.CurrentIndex);

            Assert.That(Vector3.Distance(glider.transform.position, before), Is.LessThan(0.5f));
        }

        [Test]
        public void TrajectorySampler_FitKeepsSmoothTurnCurvatureContinuous()
        {
            var frames = new List<TelemetryFrame>();
            var metersPerDegreeLongitude = 111320d * System.Math.Cos(25d * System.Math.PI / 180d);
            for (var i = 0; i < 48; i++)
            {
                var t = i / 47f;
                var angle = Mathf.Lerp(-Mathf.PI * 0.45f, Mathf.PI * 0.45f, t);
                var eastMeters = 80f * Mathf.Sin(angle);
                var northMeters = 80f * (1f - Mathf.Cos(angle));
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i,
                    120d + eastMeters / metersPerDegreeLongitude,
                    25d + northMeters / 111320d,
                    20f + 4f * Mathf.Sin(angle * 0.5f),
                    100f,
                    0f,
                    0f,
                    0f,
                    28f,
                    0f,
                    95f,
                    "Parameter Simulation",
                    "Glide",
                    1f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f));
            }

            var fitted = TrajectorySampler.Fit(frames, new GeoCoordinateMapper(frames[0], 1f, 1f), 192);
            var previousDirection = Vector3.zero;
            var maximumTurnAngle = 0f;
            for (var i = 1; i < fitted.Points.Count; i++)
            {
                var direction = fitted.Points[i] - fitted.Points[i - 1];
                if (direction.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                direction.Normalize();
                if (previousDirection.sqrMagnitude > 0.000001f)
                {
                    maximumTurnAngle = Mathf.Max(maximumTurnAngle, Vector3.Angle(previousDirection, direction));
                }

                previousDirection = direction;
            }

            Assert.That(maximumTurnAngle, Is.LessThan(4f));
        }

        [Test]
        public void GliderVisualController_DeflectsSurfacesAndShowsVectorsForSimulation()
        {
            var controller = GliderVisualBuilder.Build().AddComponent<GliderVisualController>();
            var diagnostics = new SimulationDiagnostics(
                Vector3.forward,
                Vector3.right,
                4f,
                5f,
                12f,
                controlSurfaceDeflectionDeg: new Vector3(4f, 6f, 8f));
            var frame = new TelemetryFrame(0, "t", 0f, 120d, 25d, 0f, 0f, 30f, -14f, 18f,
                0f, 0f, 90f, "Parameter Simulation", "Glide", 1f, 30f, 10f, 0f, 0f, 0f, 20f, diagnostics);
            var playback = CreatePlayback(new[] { frame });

            controller.Initialize(playback, new GeoCoordinateMapper(frame, 1f, 1f));

            Assert.That(controller.PortControlSurface.localRotation.eulerAngles.x, Is.Not.EqualTo(0f).Within(0.01f));
            Assert.That(controller.GroundVelocityVector.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void GliderTransformDriver_FutureReplacementCannotMoveExactCurrentPose()
        {
            var original = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120d, 25d, 0f, 100f, 10f, 2f, 3f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 10f, 120.001d, 25.001d, 10f, 90f, 20f, 4f, 5f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(2, "t2", 20f, 120.002d, 25.002d, 20f, 80f, 30f, 6f, 7f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var replacement = new[]
            {
                original[0],
                original[1],
                new TelemetryFrame(2, "replacement", 15f, 80d, -30d, 900f, 0f, 250f, -80f, 90f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };
            var mapper = new GeoCoordinateMapper(original[0], 1f, 1f);
            var playback = CreatePlayback(original);
            playback.Seek(0.5f);
            var glider = new GameObject("Glider");
            var driver = glider.AddComponent<GliderTransformDriver>();
            driver.Initialize(playback, mapper);
            var before = glider.transform.position;
            var beforeRotation = glider.transform.rotation;

            playback.Model.ReplaceFrames(replacement, playback.Model.CurrentIndex);

            Assert.That(glider.transform.position, Is.EqualTo(before));
            Assert.That(Quaternion.Angle(glider.transform.rotation, beforeRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void GliderVisualController_MapsDynamicsVectorsIntoUnityCoordinates()
        {
            var diagnostics = new SimulationDiagnostics(Vector3.zero, new Vector3(1f, 2f, 3f), 0f, 0f, 0f);
            var frame = FrameWithDiagnostics(diagnostics);
            var playback = CreatePlayback(new[] { frame });
            var mapper = new GeoCoordinateMapper(frame, horizontalScale: 2f, depthScale: 4f);
            var glider = GliderVisualBuilder.Build();
            glider.transform.rotation = Quaternion.Euler(12f, 37f, -9f);
            var controller = glider.AddComponent<GliderVisualController>();

            controller.Initialize(playback, mapper);

            Assert.That(controller.CurrentVector.useWorldSpace, Is.False);
            var worldStart = controller.CurrentVector.transform.TransformPoint(controller.CurrentVector.GetPosition(0));
            var worldEnd = controller.CurrentVector.transform.TransformPoint(controller.CurrentVector.GetPosition(1));
            var worldVector = worldEnd - worldStart;
            Assert.That(worldVector.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(worldVector.y, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(worldVector.z, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void GliderVisualController_WorldVectorsFollowMovingParentContinuously()
        {
            var diagnostics = new SimulationDiagnostics(Vector3.zero, Vector3.right, 0f, 0f, 0f);
            var frame = FrameWithDiagnostics(diagnostics);
            var playback = CreatePlayback(new[] { frame });
            var glider = GliderVisualBuilder.Build();
            var controller = glider.AddComponent<GliderVisualController>();
            controller.Initialize(playback, new GeoCoordinateMapper(frame, 1f, 1f));
            var localStart = controller.CurrentVector.GetPosition(0);
            var localEnd = controller.CurrentVector.GetPosition(1);

            glider.transform.position = new Vector3(12f, -7f, 30f);

            Assert.That(controller.CurrentVector.GetPosition(0), Is.EqualTo(localStart));
            Assert.That(controller.CurrentVector.GetPosition(1), Is.EqualTo(localEnd));
            Assert.That(controller.CurrentVector.transform.TransformPoint(localStart), Is.EqualTo(glider.transform.position));
        }

        [Test]
        public void GliderVisualController_UsesDiagnosticControlSurfaceDeflections()
        {
            var diagnostics = new SimulationDiagnostics(
                Vector3.zero,
                Vector3.zero,
                0f,
                0f,
                0f,
                controlSurfaceDeflectionDeg: new Vector3(4f, 6f, 8f));
            var frame = FrameWithDiagnostics(diagnostics);
            var playback = CreatePlayback(new[] { frame });
            var controller = GliderVisualBuilder.Build().AddComponent<GliderVisualController>();

            controller.Initialize(playback, new GeoCoordinateMapper(frame, 1f, 1f));

            Assert.That(Mathf.DeltaAngle(0f, controller.PortControlSurface.localEulerAngles.x), Is.EqualTo(10f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(0f, controller.StarboardControlSurface.localEulerAngles.x), Is.EqualTo(2f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(0f, controller.VerticalTail.localEulerAngles.y), Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void GliderVisualController_HidesVectorsForCsvReplay()
        {
            var frame = Frames(1)[0];
            var playback = CreatePlayback(new[] { frame });
            var controller = GliderVisualBuilder.Build().AddComponent<GliderVisualController>();
            controller.Initialize(playback, new GeoCoordinateMapper(frame, 1f, 1f));

            Assert.That(controller.CurrentVector.gameObject.activeSelf, Is.False);
            Assert.That(Quaternion.Angle(controller.PortControlSurface.localRotation, Quaternion.identity), Is.LessThan(0.001f));
        }

        [Test]
        public void GliderVisualController_HidesVectorsWhenCoordinatesDropOutWithDiagnostics()
        {
            var diagnostics = new SimulationDiagnostics(Vector3.forward, Vector3.right, 0f, 0f, 0f);
            var valid = FrameWithDiagnostics(diagnostics);
            var missingCoordinates = new TelemetryFrame(1, "dropout", 10f, 0d, 0d, 10f,
                0f, 30f, 0f, 0f, 0f, 0f, 90f, "Parameter Simulation", "Glide", 1f,
                30f, 10f, 10f, 0f, 0f, 0f, diagnostics);
            var playback = CreatePlayback(new[] { valid, missingCoordinates });
            var controller = GliderVisualBuilder.Build().AddComponent<GliderVisualController>();
            controller.Initialize(playback, new GeoCoordinateMapper(valid, 1f, 1f));
            Assert.That(controller.CurrentVector.gameObject.activeSelf, Is.True);

            playback.Seek(1f);

            Assert.That(controller.CurrentVector.gameObject.activeSelf, Is.False);
            Assert.That(controller.WaterVelocityVector.gameObject.activeSelf, Is.False);
            Assert.That(controller.GroundVelocityVector.gameObject.activeSelf, Is.False);
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
        public void TrajectorySampler_RobustFitSuppressesSinglePositionSpike()
        {
            var frames = new List<TelemetryFrame>();
            var metersPerDegreeLongitude = 111320d * System.Math.Cos(25d * System.Math.PI / 180d);
            var eastMeters = new[] { 0d, 1d, 2d, 60d, 4d, 5d, 6d };
            for (var i = 0; i < eastMeters.Length; i++)
            {
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i,
                    120d + eastMeters[i] / metersPerDegreeLongitude,
                    25d,
                    i,
                    100f,
                    0f,
                    0f,
                    0f,
                    28f,
                    0f,
                    95f,
                    "mode",
                    "state",
                    1f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f));
            }

            var mapper = new GeoCoordinateMapper(frames[0], 1f, 1f);
            var fitted = TrajectorySampler.Fit(frames, mapper, 64);

            Assert.That(fitted.PositionAt(0f), Is.EqualTo(mapper.Map(frames[0])));
            Assert.That(fitted.PositionAt(6f), Is.EqualTo(mapper.Map(frames[6])));
            Assert.That(fitted.PositionAt(3f).x, Is.LessThan(12f));
            Assert.That(fitted.PositionAt(3f).x, Is.GreaterThan(1f));
        }

        [Test]
        public void TrajectorySampler_FitFallsBackToFiniteLinearPathForDegenerateTimes()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 1f, 120d, 25d, 0f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "t1", 1f, 120.0001d, 25d, 1f, 100f, 0f, 0f, 0f, 28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(2, "t2", 1f, 120.0002d, 25d, 2f, 100f, 0f, 0f, 0f, 28f, 0f, 0f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f)
            };

            var fitted = TrajectorySampler.Fit(frames, new GeoCoordinateMapper(frames[0], 1f, 1f), 32);

            Assert.That(fitted.Points.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var point in fitted.Points)
            {
                Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x), Is.False);
                Assert.That(float.IsNaN(point.y) || float.IsInfinity(point.y), Is.False);
                Assert.That(float.IsNaN(point.z) || float.IsInfinity(point.z), Is.False);
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

        private static TelemetryFrame FrameWithDiagnostics(SimulationDiagnostics diagnostics)
        {
            return new TelemetryFrame(
                0, "t", 0f, 120d, 25d, 0f, 100f, 30f, -14f, 18f,
                28f, 0f, 90f, "Parameter Simulation", "Glide", 1f, 30f, 10f, 90f,
                0f, 0f, 20f, diagnostics);
        }
    }
}
