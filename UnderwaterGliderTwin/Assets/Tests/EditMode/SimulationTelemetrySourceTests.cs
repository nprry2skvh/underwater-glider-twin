using System.Linq;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class SimulationTelemetrySourceTests
    {
        [Test]
        public void GeneratedFrame_TargetAltitudeComesFromTargetDepth()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 600f;
            profile.SampleIntervalSeconds = 10f;
            profile.TargetDepthM = 40f;
            profile.WaterColumnDepthM = 100f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var commandedFrame = frames[1];

            Assert.That(commandedFrame.TargetDepthM, Is.EqualTo(40f));
            Assert.That(commandedFrame.DepthM, Is.Not.EqualTo(commandedFrame.TargetDepthM).Within(0.1f));
            Assert.That(commandedFrame.TargetAltitudeM, Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void Load_RejectsInvalidInitialDynamicsProfileBeforeGeneratingFrames()
        {
            var profile = SimulationProfile.Default;
            profile.Dynamics.BuoyancyCurveExponent = 4f;

            var result = new SimulationTelemetrySource(profile).Load();

            Assert.That(result.Frames, Is.Empty);
            Assert.That(result.Errors, Has.Some.Contains("BuoyancyCurveExponent"));
        }

        [Test]
        public void Load_GeneratesSurfaceToSurfaceCyclesFromProfile()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 2;
            profile.CycleDurationSeconds = 600f;
            profile.SampleIntervalSeconds = 10f;
            profile.TargetDepthM = 120f;
            profile.HorizontalSpeedMps = 0.8f;

            var result = new SimulationTelemetrySource(profile).Load();

            Assert.That(result.Frames.Count, Is.GreaterThan(100));
            Assert.That(result.SkippedRows, Is.EqualTo(0));
            Assert.That(result.Frames[0].DepthM, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.Frames[^1].DepthM, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.Frames[0].WorkMode, Is.EqualTo("Parameter Simulation"));
            var safetyLegDuration = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                profile.CycleDurationSeconds,
                profile.TargetDepthM,
                profile.HorizontalSpeedMps);
            Assert.That(result.Frames[^1].ElapsedSeconds, Is.GreaterThan(0f));
            Assert.That(result.Frames[^1].ElapsedSeconds, Is.LessThan(safetyLegDuration * profile.CycleCount * 2f));
        }

        [Test]
        public void Load_StillLeavesOnlyOneSurfaceFrameBetweenCycles()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 2;
            profile.CycleDurationSeconds = 900f;
            profile.SampleIntervalSeconds = 5f;
            profile.TargetDepthM = 160f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var maxSurfaceRun = 0;
            var currentSurfaceRun = 0;
            var maxDepth = 0f;
            for (var i = 0; i < frames.Count; i++)
            {
                maxDepth = Mathf.Max(maxDepth, frames[i].DepthM);
                if (frames[i].DepthM <= 0.01f)
                {
                    currentSurfaceRun++;
                    maxSurfaceRun = Mathf.Max(maxSurfaceRun, currentSurfaceRun);
                }
                else
                {
                    currentSurfaceRun = 0;
                }
            }

            Assert.That(maxSurfaceRun, Is.LessThanOrEqualTo(1));
            Assert.That(maxDepth, Is.GreaterThan(profile.TargetDepthM * 0.75f));
        }

        [Test]
        public void TryAdvance_SurfaceRemainderDisplacementMatchesReportedEarthVelocity()
        {
            var profile = SurfaceRemainderProfile();
            var seed = SurfaceRemainderSeed(profile);
            var snapshot = SimulationStateSnapshot.FromFrame(seed, profile);

            var generated = SimulationTrajectoryGenerator
                .GenerateFutureSlices(snapshot, profile, frameSliceBudget: 1, maximumFrameCount: 1)
                .SelectMany(slice => slice)
                .Single();

            Assert.That(TelemetryKinematicsUtility.TryGetHorizontalDisplacementMeters(seed, generated, out var displacement), Is.True);
            var deltaSeconds = generated.ElapsedSeconds - seed.ElapsedSeconds;
            var finiteDifference = displacement / deltaSeconds;
            var diagnostics = generated.Diagnostics.Value;
            var reportedGround = diagnostics.WaterVelocityEndMps + diagnostics.CurrentVelocityEndMps;

            Assert.That(finiteDifference.x, Is.EqualTo(reportedGround.x).Within(0.03f));
            Assert.That(finiteDifference.y, Is.EqualTo(reportedGround.z).Within(0.03f));
        }

        [Test]
        public void TryAdvance_SurfaceRemainderKeepsDepthAndVerticalVelocityZero()
        {
            var profile = SurfaceRemainderProfile();
            var snapshot = SimulationStateSnapshot.FromFrame(SurfaceRemainderSeed(profile), profile);

            var generated = SimulationTrajectoryGenerator
                .GenerateFutureSlices(snapshot, profile, frameSliceBudget: 1, maximumFrameCount: 1)
                .SelectMany(slice => slice)
                .Single();
            var diagnostics = generated.Diagnostics.Value;

            Assert.That(generated.DepthM, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(diagnostics.WaterVelocityEndMps.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(diagnostics.CurrentVelocityEndMps.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That((diagnostics.WaterVelocityEndMps + diagnostics.CurrentVelocityEndMps).y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Load_UsesClosedLoopBuoyancyWhenDirectionalCommandsCannotDriveBothLegs()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 1200f;
            profile.SampleIntervalSeconds = 10f;
            profile.TargetDepthM = 120f;
            profile.DescentNetBuoyancyForceN = 0f;
            profile.AscentNetBuoyancyForceN = 0f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var maximumDepth = 0f;
            foreach (var frame in frames)
            {
                maximumDepth = Mathf.Max(maximumDepth, frame.DepthM);
            }

            Assert.That(maximumDepth, Is.GreaterThan(profile.TargetDepthM * 0.5f));
            Assert.That(frames[^1].DepthM, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Load_AllowsTargetDepthBeyondLegacyWaterColumnDefault()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 7200f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 1200f;

            var result = new SimulationTelemetrySource(profile).Load();

            Assert.That(result.Frames, Is.Not.Empty);
            Assert.That(result.Frames[result.Frames.Count / 2].DepthM, Is.GreaterThan(520f));
        }

        [Test]
        public void Load_ExtendsAnUnrealisticDeepShortCycleToTheEngineeringMinimum()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 900f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 1600f;
            profile.HorizontalSpeedMps = 0.65f;

            var result = new SimulationTelemetrySource(profile).Load();

            Assert.That(result.Frames[^1].ElapsedSeconds, Is.GreaterThanOrEqualTo(7800f));
        }

        [Test]
        public void Load_CompletesTheCycleFromActualDepthEventsInsteadOfEqualTimeHalves()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 36000f;
            profile.SampleIntervalSeconds = 20f;
            profile.TargetDepthM = 80f;
            profile.WaterColumnDepthM = 80f;
            profile.DescentNetBuoyancyForceN = -12f;
            profile.AscentNetBuoyancyForceN = 12f;
            profile.DescentPitchDeg = 18f;
            profile.AscentPitchDeg = 18f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            Assert.That(frames[^1].DepthM, Is.EqualTo(0f).Within(0.01f));
            Assert.That(frames[^1].ElapsedSeconds, Is.LessThan(36000f));
        }

        [Test]
        public void Load_DoesNotStopAtTheFirstLegSafetyEstimateForADeepMultiCycleMission()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 10;
            profile.CycleDurationSeconds = 40920f;
            profile.SampleIntervalSeconds = 600f;
            profile.TargetDepthM = 2000f;
            profile.WaterColumnDepthM = 2000f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            Assert.That(frames[^1].TargetSegment, Is.EqualTo(10));
            Assert.That(frames[^1].DepthM, Is.EqualTo(0f).Within(1f));
        }

        [Test]
        public void Load_CompletesSixDefaultProfilesAtFifteenHundredMeters()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 6;
            profile.CycleDurationSeconds = 30900f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 1500f;
            profile.WaterColumnDepthM = 1500f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            Assert.That(frames[^1].TargetSegment, Is.EqualTo(6));
            Assert.That(frames[^1].DepthM, Is.EqualTo(0f).Within(1f));
        }

        [Test]
        public void Load_CompletesProfileWhenActualBuoyancyNeedsMoreThanTheReferenceLegLimit()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 30900f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 1500f;
            profile.WaterColumnDepthM = 1500f;
            profile.DescentNetBuoyancyForceN = -5f;
            profile.AscentNetBuoyancyForceN = 11.2f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            Assert.That(frames[^1].TargetSegment, Is.EqualTo(1));
            Assert.That(frames[^1].DepthM, Is.EqualTo(0f).Within(1f));
        }

        [Test]
        public void Load_CompletesSixProfilesForConfiguredShallowPitchLegs()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 6;
            profile.CycleDurationSeconds = 30900f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 1500f;
            profile.WaterColumnDepthM = 1500f;
            profile.DescentNetBuoyancyForceN = -2f;
            profile.AscentNetBuoyancyForceN = 8f;
            profile.DescentPitchDeg = 1f;
            profile.AscentPitchDeg = 3f;
            profile.DescentRollDeg = 3f;
            profile.AscentRollDeg = 3f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            Assert.That(frames[^1].TargetSegment, Is.EqualTo(6));
            Assert.That(frames[^1].DepthM, Is.EqualTo(0f).Within(1f));
        }

        [Test]
        public void Load_UsesMultipleFramesForTheBuoyancyTurnaround()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 36000f;
            profile.SampleIntervalSeconds = 10f;
            profile.TargetDepthM = 80f;
            profile.WaterColumnDepthM = 100f;
            profile.DescentNetBuoyancyForceN = -12f;
            profile.AscentNetBuoyancyForceN = 12f;
            profile.Dynamics.TurnaroundDurationSeconds = 120f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var turnaroundCount = 0;
            foreach (var frame in frames)
            {
                if (frame.RunState == "Turnaround")
                {
                    turnaroundCount++;
                }
            }

            Assert.That(turnaroundCount, Is.GreaterThanOrEqualTo(12));
        }

        [Test]
        public void DefaultDynamics_UsesAVisibleMultiMinuteTurnaround()
        {
            Assert.That(GliderDynamicsProfile.Default.TurnaroundDurationSeconds, Is.GreaterThanOrEqualTo(360f));
        }

        [Test]
        public void Load_BeginsTheBottomTurnBeforeReachingTheDepthLimit()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 36000f;
            profile.SampleIntervalSeconds = 10f;
            profile.TargetDepthM = 80f;
            profile.WaterColumnDepthM = 80f;
            profile.DescentNetBuoyancyForceN = -12f;
            profile.AscentNetBuoyancyForceN = 12f;
            profile.Dynamics.TurnaroundDurationSeconds = 360f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            float? firstTurnaroundDepth = null;
            foreach (var frame in frames)
            {
                if (frame.RunState == "Turnaround")
                {
                    firstTurnaroundDepth = frame.DepthM;
                    break;
                }
            }

            Assert.That(firstTurnaroundDepth, Is.Not.Null);
            Assert.That(firstTurnaroundDepth.Value, Is.LessThan(profile.TargetDepthM - 5f));
        }

        [Test]
        public void Load_AdvancesPositionUsingCurrentAtFrameDepth()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 120f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 20f;
            profile.HorizontalSpeedMps = 0f;
            profile.OriginLongitudeDeg = 120.0;
            profile.OriginLatitudeDeg = 25.0;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 30f, 0.5f, 0f)
            });

            var result = new SimulationTelemetrySource(profile).Load();

            Assert.That(result.Frames[^1].LongitudeDeg, Is.GreaterThan(profile.OriginLongitudeDeg));
            Assert.That(result.Frames[^1].LatitudeDeg, Is.EqualTo(profile.OriginLatitudeDeg).Within(0.00005));
        }

        [Test]
        public void Load_SelectsLayeredCurrentAtVehicleDepth()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 120f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 20f;
            profile.HorizontalSpeedMps = 0f;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 10f, 0.1f, 0f),
                new OceanCurrentLayer(10.01f, 30f, 0.9f, 0f)
            });

            var result = new SimulationTelemetrySource(profile).Load();
            var firstDiveStep = result.Frames[1];

            Assert.That(firstDiveStep.Diagnostics.Value.CurrentVelocityEndMps.x, Is.GreaterThan(0.1f));
            Assert.That(firstDiveStep.Diagnostics.Value.CurrentVelocityEndMps.x, Is.LessThan(0.9f));
        }

        [Test]
        public void Load_ResamplesCurrentFieldAsTheVehicleMovesAcrossTheRegion()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 180f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 20f;
            profile.HorizontalSpeedMps = 0f;
            profile.OriginLongitudeDeg = 120d;
            profile.OriginLatitudeDeg = 25d;
            profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 30f, 0.05f, 0f)
            });
            profile.OceanCurrentField = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(120d, 25d, 0f, 0f, 0.1f, 0f),
                new OceanCurrentFieldSample(120.0001d, 25d, 0f, 0f, 0.9f, 0f),
                new OceanCurrentFieldSample(120d, 25d, 20f, 0f, 0.1f, 0f),
                new OceanCurrentFieldSample(120.0001d, 25d, 20f, 0f, 0.9f, 0f)
            });

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            Assert.That(frames[1].Diagnostics.Value.CurrentVelocityEndMps.x, Is.GreaterThan(0.1f));
            Assert.That(frames[^1].Diagnostics.Value.CurrentVelocityEndMps.x, Is.GreaterThan(0.2f));
        }

        [Test]
        public void Load_UsesRollCommandToChangeCrossCurrentDrift()
        {
            var levelProfile = CreateCrossCurrentProfile(0f);
            var bankingProfile = CreateCrossCurrentProfile(25f);

            var levelResult = new SimulationTelemetrySource(levelProfile).Load();
            var bankingResult = new SimulationTelemetrySource(bankingProfile).Load();

            Assert.That(
                System.Math.Abs(bankingResult.Frames[^1].LongitudeDeg - levelResult.Frames[^1].LongitudeDeg),
                Is.GreaterThan(0.000001));
        }

        [Test]
        public void Load_UsesPitchAmplitudeToChangeVehicleAttitude()
        {
            var lowPitchProfile = SimulationProfile.Default;
            lowPitchProfile.CycleCount = 1;
            lowPitchProfile.CycleDurationSeconds = 240f;
            lowPitchProfile.SampleIntervalSeconds = 10f;
            lowPitchProfile.TargetDepthM = 80f;
            lowPitchProfile.PitchAmplitudeDeg = 4f;
            var highPitchProfile = lowPitchProfile.Clone();
            highPitchProfile.PitchAmplitudeDeg = 28f;

            var lowPitchFrames = new SimulationTelemetrySource(lowPitchProfile).Load().Frames;
            var highPitchFrames = new SimulationTelemetrySource(highPitchProfile).Load().Frames;
            var lowMaximum = MaximumAbsolutePitch(lowPitchFrames);
            var highMaximum = MaximumAbsolutePitch(highPitchFrames);

            Assert.That(highMaximum, Is.GreaterThan(lowMaximum + 5f));
        }

        [Test]
        public void Load_UsesIndependentAscentAndDescentFlightLegSettings()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 1800f;
            profile.SampleIntervalSeconds = 15f;
            profile.TargetDepthM = 120f;

            var type = typeof(SimulationProfile);
            var descentPitch = type.GetProperty("DescentPitchDeg");
            var ascentPitch = type.GetProperty("AscentPitchDeg");
            var descentBuoyancy = type.GetProperty("DescentNetBuoyancyForceN");
            var ascentBuoyancy = type.GetProperty("AscentNetBuoyancyForceN");
            Assert.That(descentPitch, Is.Not.Null);
            Assert.That(ascentPitch, Is.Not.Null);
            Assert.That(descentBuoyancy, Is.Not.Null);
            Assert.That(ascentBuoyancy, Is.Not.Null);

            descentPitch.SetValue(profile, 26f);
            ascentPitch.SetValue(profile, 7f);
            descentBuoyancy.SetValue(profile, -12f);
            ascentBuoyancy.SetValue(profile, 8f);

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var midpoint = frames.Count / 2;
            var descentMaximum = MaximumAbsolutePitch(frames, 0, midpoint);
            var ascentMaximum = MaximumAbsolutePitch(frames, midpoint, frames.Count);

            Assert.That(descentMaximum, Is.GreaterThan(ascentMaximum + 5f));
            Assert.That(MinimumNetBuoyancy(frames, 0, midpoint), Is.LessThan(0f));
            Assert.That(MaximumNetBuoyancy(frames, midpoint, frames.Count), Is.GreaterThan(0f));
        }

        [Test]
        public void Load_PreservesHorizontalWaterMotionWhenReachingTheSurface()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 36000f;
            profile.SampleIntervalSeconds = 15f;
            profile.TargetDepthM = 60f;
            profile.DescentNetBuoyancyForceN = -12f;
            profile.AscentNetBuoyancyForceN = 16f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            Assert.That(frames[^1].DepthM, Is.EqualTo(0f).Within(0.001f));
            var horizontalWaterVelocity = frames[^1].Diagnostics.Value.WaterVelocityEndMps;
            Assert.That(new Vector2(horizontalWaterVelocity.x, horizontalWaterVelocity.z).magnitude, Is.GreaterThan(0.01f));
        }

        [Test]
        public void Load_ChangesTargetHeadingContinuouslyAcrossCycleBoundaries()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 2;
            profile.CycleDurationSeconds = 600f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 80f;
            profile.HeadingDeltaPerCycleDeg = 20f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var maximumStep = 0f;
            for (var index = 1; index < frames.Count; index++)
            {
                maximumStep = Mathf.Max(maximumStep, Mathf.Abs(Mathf.DeltaAngle(
                    frames[index - 1].TargetHeadingDeg,
                    frames[index].TargetHeadingDeg)));
            }

            Assert.That(maximumStep, Is.LessThan(6f));
        }

        [Test]
        public void Load_DoesNotBankWhenThereIsNoTurnCommand()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 600f;
            profile.SampleIntervalSeconds = 30f;
            profile.TargetDepthM = 80f;
            profile.HeadingDeltaPerCycleDeg = 0f;
            profile.RollAmplitudeDeg = 25f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var maximumRoll = MaximumAbsoluteRoll(frames);

            Assert.That(maximumRoll, Is.LessThan(1f));
        }

        [Test]
        public void Load_6DofStateRemainsFiniteWithinEngineeringAttitudeLimits()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 3600f;
            profile.SampleIntervalSeconds = 60f;
            profile.TargetDepthM = 1200f;
            profile.HorizontalSpeedMps = 0.65f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;

            foreach (var frame in frames)
            {
                Assert.That(float.IsNaN(frame.DepthM) || float.IsInfinity(frame.DepthM), Is.False);
                Assert.That(Mathf.Abs(frame.PitchDeg), Is.LessThanOrEqualTo(35f));
                Assert.That(Mathf.Abs(frame.RollDeg), Is.LessThanOrEqualTo(30f));
                Assert.That(frame.Diagnostics.Value.DragForceN, Is.GreaterThanOrEqualTo(0f));
                Assert.That(float.IsNaN(frame.Diagnostics.Value.AngleOfAttackDeg), Is.False);
            }
        }

        [Test]
        public void Load_StoresDiagnosticsAndNoCurrentPlannedPosition()
        {
            var profile = CreateCrossCurrentProfile(20f);

            var result = new SimulationTelemetrySource(profile).Load();
            var last = result.Frames[^1];

            Assert.That(last.Diagnostics.HasValue, Is.True);
            Assert.That(last.HasPlannedPosition, Is.True);
            Assert.That(last.LongitudeDeg, Is.Not.EqualTo(last.PlannedLongitudeDeg).Within(0.0000001));
        }

        [Test]
        public void Load_PopulatesFiniteDiagnosticsForNonlinearDynamics()
        {
            var profile = CreateCrossCurrentProfile(20f);
            profile.Dynamics.BuoyancyCurveExponent = 2f;
            profile.Dynamics.BuoyancyDeadbandFraction = 0.1f;
            profile.Dynamics.PistonHysteresisFraction = 0.05f;
            profile.Dynamics.RollCurveExponent = 2f;
            profile.Dynamics.RollDeadbandFraction = 0.1f;
            profile.Dynamics.NonlinearRollRestoringGain = 3f;
            profile.Dynamics.MaxRollMomentNm = 6f;

            var frames = new SimulationTelemetrySource(profile).Load().Frames;
            var diagnostic = frames[^1].Diagnostics;

            Assert.That(diagnostic.HasValue, Is.True);
            Assert.That(float.IsNaN(diagnostic.Value.NetBuoyancyForceN) || float.IsInfinity(diagnostic.Value.NetBuoyancyForceN), Is.False);
            Assert.That(float.IsNaN(diagnostic.Value.HydrodynamicMomentNm.x) || float.IsInfinity(diagnostic.Value.HydrodynamicMomentNm.x), Is.False);
        }

        [Test]
        public void TelemetryFrame_UsesSafeSimulationDefaults()
        {
            var frame = new TelemetryFrame(0, "t", 0f, 120d, 25d, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, "", "", 0f, 0f, 0f, 0f, 0f, 0f, 0f);

            Assert.That(frame.Diagnostics.HasValue, Is.False);
            Assert.That(frame.HasPlannedPosition, Is.False);
        }

        private static SimulationProfile CreateCrossCurrentProfile(float rollAmplitude)
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = 240f;
            profile.SampleIntervalSeconds = 10f;
            profile.TargetDepthM = 80f;
            profile.HorizontalSpeedMps = 0.7f;
            profile.RollAmplitudeDeg = rollAmplitude;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 100f, 0.6f, 0f)
            });
            return profile;
        }

        private static SimulationProfile SurfaceRemainderProfile()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 1;
            profile.SampleIntervalSeconds = 20f;
            profile.TargetDepthM = 20f;
            profile.WaterColumnDepthM = 100f;
            profile.HorizontalSpeedMps = 0.5f;
            profile.StartHeadingDeg = 90f;
            profile.Dynamics.IntegrationStepSeconds = 0.1f;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 100f, 0.2f, 0f)
            });
            return profile;
        }

        private static TelemetryFrame SurfaceRemainderSeed(SimulationProfile profile)
        {
            var waterVelocity = new Vector3(0.5f, -1f, 0f);
            var currentVelocity = new Vector3(0.2f, 0f, 0f);
            var diagnostics = new SimulationDiagnostics(waterVelocity, currentVelocity, 8f, 0f, 0f);
            return new TelemetryFrame(
                0,
                "seed",
                0f,
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                0.1f,
                profile.WaterColumnDepthM - 0.1f,
                90f,
                -10f,
                0f,
                28f,
                0f,
                90f,
                "Parameter Simulation",
                "Glide",
                1f,
                90f,
                0f,
                profile.WaterColumnDepthM,
                0f,
                0f,
                0f,
                diagnostics,
                missionState: new SimulationMissionState
                {
                    Phase = SimulationMissionPhase.Ascent,
                    CompletedCycles = 0
                });
        }

        private static float MaximumAbsolutePitch(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames)
        {
            var maximum = 0f;
            foreach (var frame in frames)
            {
                maximum = UnityEngine.Mathf.Max(maximum, UnityEngine.Mathf.Abs(frame.PitchDeg));
            }

            return maximum;
        }

        private static float MaximumAbsolutePitch(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames, int startIndex, int endExclusive)
        {
            var maximum = 0f;
            for (var index = startIndex; index < endExclusive; index++)
            {
                maximum = UnityEngine.Mathf.Max(maximum, UnityEngine.Mathf.Abs(frames[index].PitchDeg));
            }

            return maximum;
        }

        private static float MaximumNetBuoyancy(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames, int startIndex, int endExclusive)
        {
            var maximum = float.MinValue;
            for (var index = startIndex; index < endExclusive; index++)
            {
                maximum = Mathf.Max(maximum, frames[index].Diagnostics.Value.NetBuoyancyForceN);
            }

            return maximum;
        }

        private static float MinimumNetBuoyancy(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames, int startIndex, int endExclusive)
        {
            var minimum = float.MaxValue;
            for (var index = startIndex; index < endExclusive; index++)
            {
                minimum = Mathf.Min(minimum, frames[index].Diagnostics.Value.NetBuoyancyForceN);
            }

            return minimum;
        }

        private static float MaximumAbsoluteRoll(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames)
        {
            var maximum = 0f;
            foreach (var frame in frames)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(frame.RollDeg));
            }

            return maximum;
        }
    }
}
