using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class SimulationRuntimeSessionTests
    {
        private SimulationRuntimeSession session;

        [TearDown]
        public void TearDown()
        {
            session?.CancelPendingRebuild();
            SimulationRuntimeRegistry.SetActive(null);
        }

        [Test]
        public void RequestProfileUpdate_StaysPendingWhileOldFramesContinuePlaying()
        {
            var frames = BuildFrames(8);
            var model = new PlaybackModel(frames, 1f);
            model.SetPlaying(true);
            var generator = new ManualFakeFutureGenerator();
            session = CreateSessionForTest(model, generator);
            var oldFrames = model.Frames;

            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);

            Assert.That(session.IsRebuildPending, Is.True);
            Assert.That(model.Frames, Is.SameAs(oldFrames));
            Assert.That(model.Tick(1f), Is.True);
            Assert.That(model.CurrentIndex, Is.EqualTo(1));
        }

        [Test]
        public void ApplyProfile_KeepsCompleteHistoryValuesAndCurrentIndex()
        {
            var oldFrames = BuildFrames(8);
            var model = new PlaybackModel(oldFrames, 1f);
            model.SeekNormalized(0.5f);
            var currentIndex = model.CurrentIndex;
            var history = new List<FrameSnapshot>();
            for (var i = 0; i <= currentIndex; i++)
            {
                history.Add(CaptureFrameSnapshot(model.Frames[i]));
            }

            var generator = new ManualFakeFutureGenerator();
            session = CreateSessionForTest(model, generator);
            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);

            generator.CompleteWithDeterministicFuture();

            Assert.That(model.CurrentIndex, Is.EqualTo(currentIndex));
            for (var i = 0; i <= currentIndex; i++)
            {
                Assert.That(CaptureFrameSnapshot(model.Frames[i]), Is.EqualTo(history[i]), $"History frame {i}");
            }
        }

        [Test]
        public void SuccessfulUpdate_CommitsFutureAndPreservesPlaybackControls()
        {
            var model = new PlaybackModel(BuildFrames(8), 1f);
            model.SeekNormalized(0.5f);
            model.SetSpeed(3f);
            model.SetDirection(-1);
            model.SetPlaying(true);
            var currentIndex = model.CurrentIndex;
            var generator = new ManualFakeFutureGenerator();
            session = CreateSessionForTest(model, generator);
            var candidate = ChangedProfile();

            Assert.That(session.RequestProfileUpdate(candidate), Is.True);
            generator.CompleteWithDeterministicFuture();

            Assert.That(session.IsRebuildPending, Is.False);
            Assert.That(session.LastError, Is.Null);
            Assert.That(session.ActiveProfile.TargetDepthM, Is.EqualTo(candidate.TargetDepthM));
            Assert.That(model.FrameCount, Is.EqualTo(currentIndex + 3));
            Assert.That(model.CurrentIndex, Is.EqualTo(currentIndex));
            Assert.That(model.Speed, Is.EqualTo(3f));
            Assert.That(model.Direction, Is.EqualTo(-1));
            Assert.That(model.IsPlaying, Is.True);
        }

        [Test]
        public void SuccessfulUpdate_PreservesHistoryAccumulatedWhileRebuildWasPending()
        {
            var frames = BuildFrames(10);
            var model = new PlaybackModel(frames, 1f);
            model.SeekNormalized(0.2f);
            model.SetPlaying(true);
            var generator = new ManualFakeFutureGenerator();
            session = CreateSessionForTest(model, generator);

            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);
            model.Tick(2f);
            var commitIndex = model.CurrentIndex;
            var historyAtCommit = new List<FrameSnapshot>();
            for (var i = 0; i <= commitIndex; i++)
            {
                historyAtCommit.Add(CaptureFrameSnapshot(model.Frames[i]));
            }
            var seed = frames[2];
            generator.CompleteWith(new[]
            {
                FutureFrame(seed, 1, 3f),
                FutureFrame(seed, 2, 4f),
                FutureFrame(seed, 3, 5f),
                FutureFrame(seed, 4, 6f)
            });

            Assert.That(model.CurrentIndex, Is.EqualTo(commitIndex));
            for (var i = 0; i <= commitIndex; i++)
            {
                Assert.That(CaptureFrameSnapshot(model.Frames[i]), Is.EqualTo(historyAtCommit[i]));
            }
            Assert.That(model.Frames[commitIndex + 1].ElapsedSeconds, Is.GreaterThan(model.CurrentFrame.ElapsedSeconds));
        }

        [Test]
        public void GeneratorFailure_DiscardsStagingAndKeepsActiveProfile()
        {
            var model = new PlaybackModel(BuildFrames(8), 1f);
            model.SeekNormalized(0.5f);
            var originalFrames = model.Frames;
            var originalProfile = SimulationProfile.Default;
            var generator = new ManualFakeFutureGenerator();
            session = new SimulationRuntimeSession(model, originalProfile, generator);

            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);
            generator.Fail("generator failed");

            Assert.That(session.IsRebuildPending, Is.False);
            Assert.That(session.LastError, Does.Contain("generator failed"));
            Assert.That(session.ActiveProfile.TargetDepthM, Is.EqualTo(originalProfile.TargetDepthM));
            Assert.That(model.Frames, Is.SameAs(originalFrames));
        }

        [Test]
        public void InvalidGeneratedFrame_DiscardsStaging()
        {
            var model = new PlaybackModel(BuildFrames(8), 1f);
            model.SeekNormalized(0.5f);
            var originalFrames = model.Frames;
            var generator = new ManualFakeFutureGenerator();
            session = CreateSessionForTest(model, generator);

            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);
            generator.CompleteWith(new[]
            {
                FutureFrame(model.CurrentFrame, 1, model.CurrentFrame.ElapsedSeconds + 1f),
                FutureFrame(model.CurrentFrame, 2, float.NaN)
            });

            Assert.That(session.LastError, Does.Contain("finite"));
            Assert.That(model.Frames, Is.SameAs(originalFrames));
        }

        [Test]
        public void InvalidCandidate_IsRejectedBeforeGeneration()
        {
            var model = new PlaybackModel(BuildFrames(8), 1f);
            var generator = new ManualFakeFutureGenerator();
            session = CreateSessionForTest(model, generator);
            var invalid = ChangedProfile();
            invalid.Dynamics.BuoyancyResponseSeconds = 0f;

            Assert.That(session.RequestProfileUpdate(invalid), Is.False);

            Assert.That(session.IsRebuildPending, Is.False);
            Assert.That(session.LastError, Does.Contain("BuoyancyResponseSeconds"));
            Assert.That(generator.StartCount, Is.EqualTo(0));
        }

        [Test]
        public void CancelPendingRebuild_KeepsOldFramesAndProfile()
        {
            var model = new PlaybackModel(BuildFrames(8), 1f);
            model.SeekNormalized(0.5f);
            var oldFrames = model.Frames;
            var generator = new ManualFakeFutureGenerator();
            session = CreateSessionForTest(model, generator);

            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);
            session.CancelPendingRebuild();
            generator.CompleteWithDeterministicFuture();

            Assert.That(session.IsRebuildPending, Is.False);
            Assert.That(session.LastError, Does.Contain("cancel"));
            Assert.That(model.Frames, Is.SameAs(oldFrames));
        }

        [Test]
        public void Timeout_DiscardsLateCompletion()
        {
            var now = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
            var model = new PlaybackModel(BuildFrames(8), 1f);
            var oldFrames = model.Frames;
            var generator = new ManualFakeFutureGenerator();
            session = new SimulationRuntimeSession(
                model,
                SimulationProfile.Default,
                generator,
                timeout: TimeSpan.FromSeconds(5),
                utcNow: () => now);

            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);
            now = now.AddSeconds(6);
            session.Tick();
            generator.CompleteWithDeterministicFuture();

            Assert.That(session.IsRebuildPending, Is.False);
            Assert.That(session.LastError, Does.Contain("timed out"));
            Assert.That(model.Frames, Is.SameAs(oldFrames));
        }

        [Test]
        public void GeneratorReceivesConfiguredFrameSliceBudget()
        {
            var generator = new ManualFakeFutureGenerator();
            session = new SimulationRuntimeSession(
                new PlaybackModel(BuildFrames(20000), 1f),
                SimulationProfile.Default,
                generator,
                frameSliceBudget: 37);

            Assert.That(session.RequestProfileUpdate(ChangedProfile()), Is.True);

            Assert.That(generator.FrameSliceBudget, Is.EqualTo(37));
        }

        [Test]
        public void RuntimeRegistry_FiresExactlyOnceForReplacementAndClear()
        {
            var first = CreateSessionForTest(new PlaybackModel(BuildFrames(3), 1f), new ManualFakeFutureGenerator());
            var second = CreateSessionForTest(new PlaybackModel(BuildFrames(3), 1f), new ManualFakeFutureGenerator());
            var changes = new List<SimulationRuntimeSession>();
            Action<SimulationRuntimeSession> handler = active => changes.Add(active);
            SimulationRuntimeRegistry.ActiveChanged += handler;
            try
            {
                SimulationRuntimeRegistry.SetActive(first);
                SimulationRuntimeRegistry.SetActive(first);
                SimulationRuntimeRegistry.SetActive(second);
                SimulationRuntimeRegistry.SetActive(null);
            }
            finally
            {
                SimulationRuntimeRegistry.ActiveChanged -= handler;
            }

            Assert.That(changes, Is.EqualTo(new[] { first, second, null }));
            Assert.That(SimulationRuntimeRegistry.Active, Is.Null);
        }

        [Test]
        public void SeededFutureGeneration_YieldsBetweenConfiguredFrameSlices()
        {
            var profile = ChangedProfile();
            profile.CycleCount = 2;
            profile.CycleDurationSeconds = 60f;
            profile.SampleIntervalSeconds = 1f;
            var seedFrame = BuildFrames(8)[4];
            var snapshot = SimulationStateSnapshot.FromFrame(seedFrame, SimulationProfile.Default);
            var slices = new List<IReadOnlyList<TelemetryFrame>>();

            foreach (var slice in SimulationTrajectoryGenerator.GenerateFutureSlices(snapshot, profile, 7))
            {
                slices.Add(slice);
            }

            Assert.That(slices.Count, Is.GreaterThan(1));
            Assert.That(slices, Has.All.Count.LessThanOrEqualTo(7));
            Assert.That(slices[0][0].RowIndex, Is.EqualTo(seedFrame.RowIndex + 1));
            Assert.That(slices[0][0].ElapsedSeconds, Is.GreaterThan(seedFrame.ElapsedSeconds));
        }

        [Test]
        public void TrajectoryView_ReplaceFutureTrajectoryLeavesRenderedHistoryUntouched()
        {
            var original = BuildFrames(8);
            var model = new PlaybackModel(original, 1f);
            var controller = new GameObject("Playback").AddComponent<PlaybackController>();
            controller.Initialize(model);
            var mapper = new GeoCoordinateMapper(original[0], 1f, 1f);
            var view = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            view.Initialize(original, mapper, controller, null);
            controller.Seek(0.5f);
            var preservedIndex = model.CurrentIndex;
            var actual = view.transform.Find("ActualTrajectoryLine").GetComponent<LineRenderer>();
            var before = new Vector3[actual.positionCount];
            actual.GetPositions(before);
            var replacement = new List<TelemetryFrame>();
            for (var i = 0; i <= preservedIndex; i++)
            {
                replacement.Add(original[i]);
            }
            replacement.Add(FutureFrame(original[preservedIndex], 1, original[preservedIndex].ElapsedSeconds + 1f));
            replacement.Add(FutureFrame(original[preservedIndex], 2, original[preservedIndex].ElapsedSeconds + 2f));

            view.ReplaceFutureTrajectory(replacement, preservedIndex);

            var after = new Vector3[actual.positionCount];
            actual.GetPositions(after);
            Assert.That(after, Is.EqualTo(before));
            Assert.That(
                view.transform.Find("RemainingTrajectoryLine").GetComponent<LineRenderer>().positionCount,
                Is.EqualTo(3));
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        private SimulationRuntimeSession CreateSessionForTest(
            PlaybackModel model,
            ManualFakeFutureGenerator generator)
        {
            return new SimulationRuntimeSession(model, SimulationProfile.Default, generator, frameSliceBudget: 16);
        }

        private static SimulationProfile ChangedProfile()
        {
            var profile = SimulationProfile.Default;
            profile.TargetDepthM = 180f;
            profile.CycleCount = 4;
            return profile;
        }

        private static IReadOnlyList<TelemetryFrame> BuildFrames(int count)
        {
            var frames = new List<TelemetryFrame>(count);
            for (var i = 0; i < count; i++)
            {
                var diagnostics = new SimulationDiagnostics(
                    new Vector3(0.1f + i, -0.2f, 0.3f),
                    new Vector3(0.01f, 0f, -0.02f),
                    2f + i,
                    4f + i,
                    6f + i,
                    1f + i,
                    2f + i,
                    3f + i,
                    4f + i,
                    new Vector3(0.1f, 0.2f, 0.3f),
                    new Vector3(1f, 2f, 3f),
                    10f + i,
                    new Vector3(4f, 5f, 6f),
                    7f + i);
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i,
                    120d + i * 0.0001d,
                    25d + i * 0.0001d,
                    i,
                    520f - i,
                    42f + i,
                    -10f + i,
                    2f + i,
                    28.6f,
                    0.6f,
                    96f - i,
                    "Parameter Simulation",
                    "Glide",
                    1f,
                    45f,
                    160f,
                    360f,
                    285f,
                    8f,
                    14f,
                    diagnostics,
                    121d + i * 0.0001d,
                    26d + i * 0.0001d));
            }

            return frames;
        }

        private static TelemetryFrame FutureFrame(TelemetryFrame seed, int offset, float elapsedSeconds)
        {
            return new TelemetryFrame(
                seed.RowIndex + offset,
                "future" + offset,
                elapsedSeconds,
                seed.LongitudeDeg + offset * 0.001d,
                seed.LatitudeDeg + offset * 0.001d,
                Mathf.Min(180f, seed.DepthM + offset),
                Mathf.Max(0f, 520f - seed.DepthM - offset),
                seed.HeadingDeg,
                seed.PitchDeg,
                seed.RollDeg,
                seed.Voltage24V,
                seed.Current24A,
                seed.BatteryPercent,
                seed.WorkMode,
                "Glide",
                seed.TargetSegment,
                seed.TargetHeadingDeg,
                180f,
                seed.TargetAltitudeM,
                seed.PropellerRpm,
                seed.PistonMm,
                seed.TurnAngleDeg,
                seed.Diagnostics,
                seed.PlannedLongitudeDeg + offset * 0.001d,
                seed.PlannedLatitudeDeg + offset * 0.001d);
        }

        private static FrameSnapshot CaptureFrameSnapshot(TelemetryFrame frame)
        {
            var diagnostics = frame.Diagnostics;
            var diagnosticValues = diagnostics.HasValue
                ? string.Join("|",
                    Vector(diagnostics.Value.WaterVelocityEndMps),
                    Vector(diagnostics.Value.CurrentVelocityEndMps),
                    Float(diagnostics.Value.NetBuoyancyForceN),
                    Float(diagnostics.Value.EnergyWatts),
                    Float(diagnostics.Value.SideSlipDeg),
                    Float(diagnostics.Value.AngleOfAttackDeg),
                    Float(diagnostics.Value.LiftForceN),
                    Float(diagnostics.Value.DragForceN),
                    Float(diagnostics.Value.SideForceN),
                    Vector(diagnostics.Value.AngularVelocityRadPerSecond),
                    Vector(diagnostics.Value.HydrodynamicMomentNm),
                    Float(diagnostics.Value.PistonPositionMm),
                    Vector(diagnostics.Value.ControlSurfaceDeflectionDeg),
                    Float(diagnostics.Value.ActuatorPowerWatts))
                : "<null>";
            return new FrameSnapshot(string.Join("|",
                frame.RowIndex.ToString(CultureInfo.InvariantCulture),
                frame.RawTime ?? "<null>",
                Float(frame.ElapsedSeconds),
                Double(frame.LongitudeDeg),
                Double(frame.LatitudeDeg),
                Float(frame.DepthM),
                Float(frame.AltitudeM),
                Float(frame.HeadingDeg),
                Float(frame.PitchDeg),
                Float(frame.RollDeg),
                Float(frame.Voltage24V),
                Float(frame.Current24A),
                Float(frame.BatteryPercent),
                frame.WorkMode ?? "<null>",
                frame.RunState ?? "<null>",
                Float(frame.TargetSegment),
                Float(frame.TargetHeadingDeg),
                Float(frame.TargetDepthM),
                Float(frame.TargetAltitudeM),
                Float(frame.PropellerRpm),
                Float(frame.PistonMm),
                Float(frame.TurnAngleDeg),
                diagnosticValues,
                Double(frame.PlannedLongitudeDeg),
                Double(frame.PlannedLatitudeDeg)));
        }

        private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => $"{Float(value.x)},{Float(value.y)},{Float(value.z)}";

        private readonly struct FrameSnapshot : IEquatable<FrameSnapshot>
        {
            private readonly string value;

            public FrameSnapshot(string value)
            {
                this.value = value;
            }

            public bool Equals(FrameSnapshot other) => value == other.value;
            public override bool Equals(object obj) => obj is FrameSnapshot other && Equals(other);
            public override int GetHashCode() => value?.GetHashCode() ?? 0;
            public override string ToString() => value;
        }

        private sealed class ManualFakeFutureGenerator : ISimulationFutureGenerator
        {
            private Action<SimulationRebuildResult> completion;
            private SimulationStateSnapshot seed;

            public int StartCount { get; private set; }
            public int FrameSliceBudget { get; private set; }

            public ISimulationRebuildOperation GenerateFuture(
                SimulationStateSnapshot snapshot,
                SimulationProfile profile,
                int frameSliceBudget,
                Action<SimulationRebuildResult> onCompleted)
            {
                StartCount++;
                seed = snapshot;
                FrameSliceBudget = frameSliceBudget;
                completion = onCompleted;
                return new ManualOperation();
            }

            public void CompleteWithDeterministicFuture()
            {
                CompleteWith(new[]
                {
                    FutureFrame(seed.Frame, 1, seed.Frame.ElapsedSeconds + 1f),
                    FutureFrame(seed.Frame, 2, seed.Frame.ElapsedSeconds + 2f)
                });
            }

            public void CompleteWith(IReadOnlyList<TelemetryFrame> future)
            {
                completion?.Invoke(SimulationRebuildResult.Success(future));
            }

            public void Fail(string error)
            {
                completion?.Invoke(SimulationRebuildResult.Failure(error));
            }
        }

        private sealed class ManualOperation : ISimulationRebuildOperation
        {
            public bool IsCancelled { get; private set; }
            public void Cancel() => IsCancelled = true;
        }
    }
}
