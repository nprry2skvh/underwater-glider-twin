using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class MotionDataConsistencyPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            RuntimePredictionState.SetModelKind(PredictionModelKind.XGBoost);
            RuntimePredictionState.SetEnabled(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProfileUpdate_PreservesCurrentContinuousPoseAndDashboardState()
        {
            var bootstrap = default(TwinBootstrap);
            yield return LoadSimulation(value => bootstrap = value);
            MoveByFractionalRows(bootstrap.PlaybackController, 0.5f, reverse: false);

            var model = bootstrap.PlaybackController.Model;
            var glider = Object.FindObjectOfType<GliderTransformDriver>().transform;
            var elapsedBefore = model.ContinuousElapsedSeconds;
            var positionBefore = glider.position;
            var rotationBefore = glider.rotation;
            var depthBefore = FindText("DepthValue").text;
            var headingBefore = FindText("HeadingValue").text;
            var missionTimeBefore = FindText("MissionTimeValue").text;
            var framesBefore = model.Frames;
            var candidate = bootstrap.SimulationSession.ActiveProfile;
            candidate.TargetDepthM += 5f;
            candidate.HeadingDeltaPerCycleDeg += 7f;
            candidate.CycleCount += 1;

            Assert.That(bootstrap.SimulationSession.RequestProfileUpdate(candidate), Is.True);
            var guard = 0;
            while (bootstrap.SimulationSession.IsRebuildPending && guard++ < 1200)
            {
                yield return null;
            }

            Assert.That(bootstrap.SimulationSession.IsRebuildPending, Is.False, bootstrap.SimulationSession.LastError);
            Assert.That(bootstrap.SimulationSession.LastError, Is.Null.Or.Empty);
            Assert.That(model.ContinuousElapsedSeconds, Is.EqualTo(elapsedBefore).Within(0.0001f));
            Assert.That(Vector3.Distance(glider.position, positionBefore), Is.LessThan(0.000001f));
            Assert.That(Quaternion.Angle(glider.rotation, rotationBefore), Is.LessThan(0.001f));
            Assert.That(FindText("DepthValue").text, Is.EqualTo(depthBefore));
            Assert.That(FindText("HeadingValue").text, Is.EqualTo(headingBefore));
            Assert.That(FindText("MissionTimeValue").text, Is.EqualTo(missionTimeBefore));
            Assert.That(model.Frames, Is.Not.SameAs(framesBefore));
            Assert.That(bootstrap.SimulationSession.Timeline.CommittedSnapshot.Segments.Count, Is.EqualTo(2));
            Assert.That(model.Frames.Any(frame => frame.ProfileSequence == 1), Is.True);
            AssertMotionConsumersAgree(bootstrap);
        }

        [UnityTest]
        public IEnumerator ContinuousPlayback_KeepsGliderVectorsAndDashboardOnOneMotionSample()
        {
            var bootstrap = default(TwinBootstrap);
            yield return LoadSimulation(value => bootstrap = value);

            MoveByFractionalRows(bootstrap.PlaybackController, 0.5f, reverse: false);

            Assert.That(bootstrap.PlaybackController.Model.ContinuousIndex % 1f, Is.EqualTo(0.5f).Within(0.001f));
            AssertMotionConsumersAgree(bootstrap);
        }

        [UnityTest]
        public IEnumerator ReversePlayback_UsesTheSameTelemetryAttitudeAndPosition()
        {
            var bootstrap = default(TwinBootstrap);
            yield return LoadSimulation(value => bootstrap = value);
            bootstrap.PlaybackController.Seek(0.5f);
            yield return new WaitForSecondsRealtime(0.08f);

            MoveByFractionalRows(bootstrap.PlaybackController, 0.5f, reverse: true);

            Assert.That(bootstrap.PlaybackController.Model.Direction, Is.EqualTo(-1));
            Assert.That(bootstrap.PlaybackController.Model.ContinuousIndex % 1f, Is.EqualTo(0.5f).Within(0.001f));
            AssertMotionConsumersAgree(bootstrap);
        }

        private static IEnumerator LoadSimulation(System.Action<TwinBootstrap> assign)
        {
            RuntimeDataSourceState.UseSimulation(CreateProfile());
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var bootstrap = Object.FindObjectOfType<TwinBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.PlaybackController, Is.Not.Null);
            Assert.That(bootstrap.SimulationSession, Is.Not.Null);
            Assert.That(Object.FindObjectOfType<GliderTransformDriver>(), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<GliderVisualController>(), Is.Not.Null);
            assign(bootstrap);
        }

        private static void MoveByFractionalRows(
            PlaybackController playback,
            float rowFraction,
            bool reverse)
        {
            if (reverse)
            {
                playback.PlayReverse();
            }
            else
            {
                playback.PlayForward();
            }

            playback.Step(rowFraction / playback.Model.RowsPerSecond);
            playback.SetPlaying(false);
        }

        private static void AssertMotionConsumersAgree(TwinBootstrap bootstrap)
        {
            var model = bootstrap.PlaybackController.Model;
            var sample = ContinuousMotionSampler.Sample(model.Frames, model.ContinuousElapsedSeconds);
            var driver = Object.FindObjectOfType<GliderTransformDriver>();
            var visuals = Object.FindObjectOfType<GliderVisualController>();
            var expectedPosition = ExpectedPosition(model, bootstrap.Mapper, sample);
            var expectedRotation = PoseMapper.ToRotation(
                sample.HeadingDeg,
                sample.PitchDeg,
                sample.RollDeg,
                AttitudeSettings.Default);

            Assert.That(Vector3.Distance(driver.transform.position, expectedPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(driver.transform.rotation, expectedRotation), Is.LessThan(0.001f));
            AssertReadout("DepthValue", $"{sample.DepthM:0.0}", "m");
            AssertReadout("HeadingValue", $"{sample.HeadingDeg:0.0}", "°");
            AssertReadout("PitchValue", $"{sample.PitchDeg:0.0}", "°");
            AssertReadout("RollValue", $"{sample.RollDeg:0.0}", "°");
            AssertReadout("VelocityXValue", $"{sample.DisplayVelocityEnuMps.x:0.00}", "m/s");
            AssertReadout("VelocityYValue", $"{sample.DisplayVelocityEnuMps.y:0.00}", "m/s");
            AssertReadout("VelocityZValue", $"{sample.DisplayVelocityEnuMps.z:0.00}", "m/s");
            Assert.That(
                FindText("MissionTimeValue").text,
                Is.EqualTo(FormatDuration(sample.ElapsedSeconds - model.StartElapsedSeconds)));

            AssertVector(visuals.CurrentVector, bootstrap.Mapper.MapDynamicsVelocity(sample.CurrentVelocityEndMps));
            AssertVector(visuals.WaterVelocityVector, bootstrap.Mapper.MapDynamicsVelocity(sample.WaterVelocityEndMps));
            AssertVector(visuals.GroundVelocityVector, bootstrap.Mapper.MapDynamicsVelocity(sample.GroundVelocityEndMps));

            if (sample.HasDiagnostics)
            {
                Assert.That(
                    Mathf.DeltaAngle(0f, visuals.PortControlSurface.localEulerAngles.x),
                    Is.EqualTo(Mathf.Clamp(sample.ControlSurfaceDeflectionDeg.y + sample.ControlSurfaceDeflectionDeg.x, -30f, 30f)).Within(0.01f));
                Assert.That(
                    Mathf.DeltaAngle(0f, visuals.StarboardControlSurface.localEulerAngles.x),
                    Is.EqualTo(Mathf.Clamp(sample.ControlSurfaceDeflectionDeg.y - sample.ControlSurfaceDeflectionDeg.x, -30f, 30f)).Within(0.01f));
                Assert.That(
                    Mathf.DeltaAngle(0f, visuals.VerticalTail.localEulerAngles.y),
                    Is.EqualTo(Mathf.Clamp(sample.ControlSurfaceDeflectionDeg.z, -30f, 30f)).Within(0.01f));
            }
        }

        private static Vector3 ExpectedPosition(
            PlaybackModel model,
            GeoCoordinateMapper mapper,
            ContinuousMotionSample sample)
        {
            var lower = mapper.Map(model.Frames[sample.LowerIndex]);
            return sample.LowerIndex == sample.UpperIndex
                ? lower
                : Vector3.Lerp(lower, mapper.Map(model.Frames[sample.UpperIndex]), sample.Interpolation01);
        }

        private static void AssertVector(LineRenderer line, Vector3 expectedWorldVelocity)
        {
            if (expectedWorldVelocity.sqrMagnitude <= 0.0001f)
            {
                Assert.That(line.gameObject.activeSelf, Is.False);
                return;
            }

            Assert.That(line.gameObject.activeSelf, Is.True);
            Assert.That(line.useWorldSpace, Is.False);
            var worldStart = line.transform.TransformPoint(line.GetPosition(0));
            var worldEnd = line.transform.TransformPoint(line.GetPosition(1));
            Assert.That(Vector3.Distance(worldEnd - worldStart, expectedWorldVelocity * 2.5f), Is.LessThan(0.001f));
        }

        private static Text FindText(string name)
        {
            return Object.FindObjectsOfType<Text>(true).First(text => text.name == name);
        }

        private static void AssertReadout(string valueName, string expectedValue, string unit)
        {
            var value = FindText(valueName).text;
            Assert.That(
                value == expectedValue || value == expectedValue + unit || value == expectedValue + " " + unit,
                Is.True,
                valueName + " displayed an unexpected value: " + value);
            var separateUnit = Object.FindObjectsOfType<Text>(true)
                .FirstOrDefault(text => text.name == valueName + "Unit");
            if (separateUnit != null)
            {
                Assert.That(separateUnit.text, Is.EqualTo(unit));
            }
        }

        private static string FormatDuration(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var remainingSeconds = totalSeconds % 60;
            return $"{hours:00}:{minutes:00}:{remainingSeconds:00}";
        }

        private static SimulationProfile CreateProfile()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 3;
            profile.CycleDurationSeconds = 120f;
            profile.SampleIntervalSeconds = 1f;
            profile.TargetDepthM = 20f;
            profile.WaterColumnDepthM = 60f;
            profile.HorizontalSpeedMps = 0.6f;
            profile.Dynamics.CruiseSpeedMps = 0.6f;
            profile.Dynamics.TurnaroundDurationSeconds = 10f;
            profile.DescentNetBuoyancyForceN = -12f;
            profile.AscentNetBuoyancyForceN = 12f;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 60f, 0.2f, 0.1f)
            });
            return profile;
        }
    }
}
