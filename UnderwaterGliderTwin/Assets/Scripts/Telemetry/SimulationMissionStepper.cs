using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    internal sealed class SimulationMissionStepper
    {
        private const float ArrivalToleranceM = 1f;
        private const float EmergencyLegSafetyMultiplier = 3f;

        private readonly SimulationProfile profile;
        private readonly SimulationProfile coordinateProfile;
        private readonly GliderDynamicsProfile dynamics;
        private readonly float waterColumnDepthM;
        private readonly float safetyLegDurationSeconds;
        private readonly int cycleCount;
        private readonly double plannedOriginLongitudeDeg;
        private readonly double plannedOriginLatitudeDeg;
        private GliderDynamicsState state;
        private GliderDynamicsState plannedState;
        private SimulationMissionState missionState;
        private float elapsedSeconds;
        private int rowIndex;

        private SimulationMissionStepper(
            SimulationProfile profile,
            SimulationProfile coordinateProfile,
            GliderDynamicsState state,
            GliderDynamicsState plannedState,
            SimulationMissionState missionState,
            float elapsedSeconds,
            int rowIndex,
            double plannedOriginLongitudeDeg,
            double plannedOriginLatitudeDeg)
        {
            this.profile = profile ?? SimulationProfile.Default;
            this.coordinateProfile = coordinateProfile ?? this.profile;
            dynamics = this.profile.Dynamics?.Clone() ?? GliderDynamicsProfile.Default;
            dynamics.CruiseSpeedMps = Mathf.Max(0f, this.profile.HorizontalSpeedMps);
            waterColumnDepthM = Mathf.Max(0f, this.profile.WaterColumnDepthM, this.profile.TargetDepthM);
            safetyLegDurationSeconds = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                Mathf.Max(60f, this.profile.CycleDurationSeconds),
                this.profile.TargetDepthM,
                dynamics.CruiseSpeedMps);
            cycleCount = Mathf.Max(1, this.profile.CycleCount);
            this.state = state;
            this.plannedState = plannedState;
            this.missionState = missionState;
            this.elapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            this.rowIndex = rowIndex;
            this.plannedOriginLongitudeDeg = plannedOriginLongitudeDeg;
            this.plannedOriginLatitudeDeg = plannedOriginLatitudeDeg;
        }

        public static SimulationMissionStepper CreateInitial(SimulationProfile profile)
        {
            var effectiveProfile = (profile ?? SimulationProfile.Default).Clone();
            var dynamics = effectiveProfile.Dynamics?.Clone() ?? GliderDynamicsProfile.Default;
            dynamics.CruiseSpeedMps = Mathf.Max(0f, effectiveProfile.HorizontalSpeedMps);
            var headingRadians = effectiveProfile.StartHeadingDeg * Mathf.Deg2Rad;
            var waterVelocity = new Vector3(
                Mathf.Sin(headingRadians) * dynamics.CruiseSpeedMps,
                0f,
                Mathf.Cos(headingRadians) * dynamics.CruiseSpeedMps);
            var initialCurrent = ResolveCurrentVelocity(
                effectiveProfile,
                effectiveProfile.OriginLongitudeDeg,
                effectiveProfile.OriginLatitudeDeg,
                0f,
                0f);
            var currentEndMps = new Vector3(initialCurrent.x, 0f, initialCurrent.y);
            var state = GliderDynamicsState.AtSurface(effectiveProfile.StartHeadingDeg);
            state.WaterVelocityEndMps = waterVelocity;
            state.EarthVelocityEndMps = waterVelocity + currentEndMps;
            var plannedState = state;
            plannedState.EarthVelocityEndMps = plannedState.WaterVelocityEndMps;
            return new SimulationMissionStepper(
                effectiveProfile,
                effectiveProfile,
                state,
                plannedState,
                SimulationMissionState.AtSurface(),
                0f,
                0,
                effectiveProfile.OriginLongitudeDeg,
                effectiveProfile.OriginLatitudeDeg);
        }

        public static SimulationMissionStepper FromSnapshot(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var effectiveProfile = profile.Clone();
            var sourceProfile = snapshot.Profile ?? effectiveProfile;
            var state = snapshot.DynamicsState;
            var plannedState = state;
            plannedState.PositionEndM = new Vector3(0f, state.PositionEndM.y, 0f);
            plannedState.EarthVelocityEndMps = plannedState.WaterVelocityEndMps;
            var seed = snapshot.Frame;
            var plannedLongitude = seed.HasPlannedPosition ? seed.PlannedLongitudeDeg : seed.LongitudeDeg;
            var plannedLatitude = seed.HasPlannedPosition ? seed.PlannedLatitudeDeg : seed.LatitudeDeg;
            return new SimulationMissionStepper(
                effectiveProfile,
                sourceProfile,
                state,
                plannedState,
                snapshot.MissionState,
                seed.ElapsedSeconds,
                seed.RowIndex + 1,
                plannedLongitude,
                plannedLatitude);
        }

        public TelemetryFrame CurrentFrame()
        {
            var current = ResolveCurrentAtState(state, elapsedSeconds);
            return BuildFrame(
                current,
                elapsedSeconds,
                profile.StartHeadingDeg,
                0f,
                "Surface");
        }

        public bool TryAdvance(float sampleIntervalSeconds, out TelemetryFrame frame)
        {
            frame = default;
            if (missionState.Phase == SimulationMissionPhase.LegTimeout
                || missionState.CompletedCycles >= cycleCount)
            {
                return false;
            }

            var remainingSeconds = Mathf.Max(0.1f, sampleIntervalSeconds);
            var runState = RunStateForPhase(missionState);
            var currentEndMps = ResolveCurrentAtState(state, elapsedSeconds);
            var targetHeadingDeg = profile.StartHeadingDeg;
            var targetDepthM = 0f;
            var reachedSurfaceThisSample = false;

            while (remainingSeconds > 0.0001f)
            {
                if (!reachedSurfaceThisSample)
                {
                    PrepareMissionState();
                }
                else
                {
                    AdvanceSurfaceRemainder(remainingSeconds);
                    remainingSeconds = 0f;
                    currentEndMps = state.EarthVelocityEndMps - state.WaterVelocityEndMps;
                    break;
                }
                var commands = EvaluateCommands();
                targetHeadingDeg = commands.TargetHeadingDeg;
                targetDepthM = commands.TargetDepthM;
                var stepSeconds = Mathf.Min(
                    remainingSeconds,
                    Mathf.Clamp(dynamics.IntegrationStepSeconds, 0.01f, 1f));
                currentEndMps = ResolveCurrentAtState(state, elapsedSeconds + stepSeconds);
                var effectiveDynamics = dynamics.Clone();
                effectiveDynamics.CruiseSpeedMps = Mathf.Max(0f, commands.TargetSpeedMps);
                state = GliderDynamicsIntegrator.Step(
                    state,
                    effectiveDynamics,
                    currentEndMps,
                    commands.TargetDepthM,
                    commands.TargetHeadingDeg,
                    stepSeconds,
                    commands.CommandedRollDeg,
                    commands.CommandedPitchDeg,
                    commands.CommandedNetBuoyancyForceN);
                plannedState = GliderDynamicsIntegrator.Step(
                    plannedState,
                    effectiveDynamics,
                    Vector3.zero,
                    commands.TargetDepthM,
                    commands.TargetHeadingDeg,
                    stepSeconds,
                    commands.CommandedRollDeg,
                    commands.CommandedPitchDeg,
                    commands.CommandedNetBuoyancyForceN);
                ConstrainAtSurface(ref state, currentEndMps);
                ConstrainAtSurface(ref plannedState, Vector3.zero);
                elapsedSeconds += stepSeconds;
                AdvanceMissionState(stepSeconds);
                if (missionState.Phase == SimulationMissionPhase.Surface)
                {
                    reachedSurfaceThisSample = true;
                }

                if (missionState.Phase == SimulationMissionPhase.BottomTurn
                    || commands.Phase == SimulationMissionPhase.BottomTurn)
                {
                    runState = "Turnaround";
                }
                else if (missionState.Phase == SimulationMissionPhase.Surface)
                {
                    runState = "Surface";
                }
                else if (missionState.Phase == SimulationMissionPhase.LegTimeout)
                {
                    runState = "Leg Timeout";
                }
                else if (missionState.SafetyWarningRaised)
                {
                    runState = "Safety Warning";
                }
                else if (runState != "Turnaround")
                {
                    runState = "Glide";
                }

                remainingSeconds -= stepSeconds;
            }

            frame = BuildFrame(currentEndMps, elapsedSeconds, targetHeadingDeg, targetDepthM, runState);
            return true;
        }

        private void PrepareMissionState()
        {
            if (missionState.Phase == SimulationMissionPhase.Surface)
            {
                if (missionState.CompletedCycles < cycleCount)
                {
                    missionState.Phase = SimulationMissionPhase.Descent;
                    missionState.LegElapsedSeconds = 0f;
                    missionState.SafetyWarningRaised = false;
                }
                return;
            }

            if (missionState.Phase == SimulationMissionPhase.Descent
                && ShouldBeginBottomTurn(
                    profile.TargetDepthM,
                    Mathf.Clamp(state.PositionEndM.y, 0f, waterColumnDepthM),
                    state.EarthVelocityEndMps.y,
                    dynamics.TurnaroundDurationSeconds))
            {
                missionState.Phase = SimulationMissionPhase.BottomTurn;
                missionState.TurnaroundElapsedSeconds = 0f;
                missionState.LegElapsedSeconds = 0f;
                missionState.SafetyWarningRaised = false;
            }
        }

        private MissionCommands EvaluateCommands()
        {
            var currentDepthM = Mathf.Clamp(state.PositionEndM.y, 0f, waterColumnDepthM);
            var depthProgress = profile.TargetDepthM <= 0.001f
                ? 1f
                : Mathf.Clamp01(currentDepthM / profile.TargetDepthM);
            var headingProgress = missionState.Phase == SimulationMissionPhase.BottomTurn
                ? 0.5f
                : missionState.Phase == SimulationMissionPhase.Ascent
                    ? 0.5f + (1f - depthProgress) * 0.5f
                    : depthProgress * 0.5f;
            var targetHeadingDeg = profile.StartHeadingDeg
                + (missionState.CompletedCycles + headingProgress) * profile.HeadingDeltaPerCycleDeg;
            var targetDepthM = missionState.Phase == SimulationMissionPhase.Descent
                || missionState.Phase == SimulationMissionPhase.BottomTurn
                ? profile.TargetDepthM
                : 0f;
            var turnaroundDurationSeconds = Mathf.Max(1f, dynamics.TurnaroundDurationSeconds);
            var turnaroundBlend = missionState.Phase == SimulationMissionPhase.BottomTurn
                ? Mathf.SmoothStep(0f, 1f, missionState.TurnaroundElapsedSeconds / turnaroundDurationSeconds)
                : missionState.Phase == SimulationMissionPhase.Ascent ? 1f : 0f;
            var turnDirection = Mathf.Abs(profile.HeadingDeltaPerCycleDeg) > 0.0001f
                ? Mathf.Sign(profile.HeadingDeltaPerCycleDeg)
                : 0f;
            var descentRollDeg = turnDirection * profile.ResolveDescentRollDeg();
            var ascentRollDeg = -turnDirection * profile.ResolveAscentRollDeg();
            var descentPitchDeg = profile.ResolveDescentPitchDeg();
            var ascentPitchDeg = -profile.ResolveAscentPitchDeg();
            var descentSpeedMps = profile.ResolveDescentSpeedMps();
            var ascentSpeedMps = profile.ResolveAscentSpeedMps();
            var commandedNetBuoyancyForceN = profile.HasDirectionalBuoyancyCommands()
                ? Mathf.Lerp(
                    profile.ResolveDescentNetBuoyancyForceN(-dynamics.MaxBuoyancyForceN * 0.7f),
                    profile.ResolveAscentNetBuoyancyForceN(dynamics.MaxBuoyancyForceN * 0.7f),
                    turnaroundBlend)
                : float.NaN;
            return new MissionCommands
            {
                Phase = missionState.Phase,
                TargetHeadingDeg = targetHeadingDeg,
                TargetDepthM = targetDepthM,
                TargetSpeedMps = Mathf.Lerp(descentSpeedMps, ascentSpeedMps, turnaroundBlend),
                CommandedRollDeg = Mathf.Lerp(descentRollDeg, ascentRollDeg, turnaroundBlend),
                CommandedPitchDeg = Mathf.Lerp(descentPitchDeg, ascentPitchDeg, turnaroundBlend),
                CommandedNetBuoyancyForceN = commandedNetBuoyancyForceN
            };
        }

        private void AdvanceMissionState(float stepSeconds)
        {
            if (missionState.Phase == SimulationMissionPhase.BottomTurn)
            {
                missionState.TurnaroundElapsedSeconds += stepSeconds;
                if (missionState.TurnaroundElapsedSeconds >= Mathf.Max(1f, dynamics.TurnaroundDurationSeconds))
                {
                    missionState.Phase = SimulationMissionPhase.Ascent;
                    missionState.LegElapsedSeconds = 0f;
                    missionState.SafetyWarningRaised = false;
                }
                return;
            }

            if (missionState.Phase == SimulationMissionPhase.Descent)
            {
                missionState.LegElapsedSeconds += stepSeconds;
                if (Mathf.Clamp(state.PositionEndM.y, 0f, waterColumnDepthM)
                    >= profile.TargetDepthM - ArrivalToleranceM)
                {
                    missionState.Phase = SimulationMissionPhase.BottomTurn;
                    missionState.TurnaroundElapsedSeconds = 0f;
                    missionState.LegElapsedSeconds = 0f;
                    missionState.SafetyWarningRaised = false;
                }
                else
                {
                    UpdateSafetyState();
                }
                return;
            }

            if (missionState.Phase == SimulationMissionPhase.Ascent)
            {
                missionState.LegElapsedSeconds += stepSeconds;
                if (state.PositionEndM.y <= ArrivalToleranceM)
                {
                    HoldAtSurface(ref state, ResolveCurrentAtState(state, elapsedSeconds));
                    HoldAtSurface(ref plannedState, Vector3.zero);
                    missionState.CompletedCycles++;
                    missionState.Phase = SimulationMissionPhase.Surface;
                    missionState.LegElapsedSeconds = 0f;
                    missionState.TurnaroundElapsedSeconds = 0f;
                    missionState.SafetyWarningRaised = false;
                }
                else
                {
                    UpdateSafetyState();
                }
            }
        }

        private void UpdateSafetyState()
        {
            if (missionState.LegElapsedSeconds >= safetyLegDurationSeconds * EmergencyLegSafetyMultiplier)
            {
                missionState.Phase = SimulationMissionPhase.LegTimeout;
                missionState.SafetyWarningRaised = true;
            }
            else if (missionState.LegElapsedSeconds >= safetyLegDurationSeconds)
            {
                missionState.SafetyWarningRaised = true;
            }
        }

        private TelemetryFrame BuildFrame(
            Vector3 currentEndMps,
            float frameElapsedSeconds,
            float targetHeadingDeg,
            float targetDepthM,
            string runState)
        {
            var depthM = Mathf.Clamp(state.PositionEndM.y, 0f, waterColumnDepthM);
            var altitudeM = Mathf.Max(0f, waterColumnDepthM - depthM);
            LocalMissionCoordinateConverter.ToGeodetic(
                state.PositionEndM,
                coordinateProfile.OriginLongitudeDeg,
                coordinateProfile.OriginLatitudeDeg,
                out var longitudeDeg,
                out var latitudeDeg);
            LocalMissionCoordinateConverter.ToGeodetic(
                plannedState.PositionEndM,
                plannedOriginLongitudeDeg,
                plannedOriginLatitudeDeg,
                out var plannedLongitudeDeg,
                out var plannedLatitudeDeg);
            var waterHorizontalVelocity = new Vector2(state.WaterVelocityEndMps.x, state.WaterVelocityEndMps.z);
            var groundHorizontalVelocity = new Vector2(state.EarthVelocityEndMps.x, state.EarthVelocityEndMps.z);
            var sideSlipDeg = waterHorizontalVelocity.sqrMagnitude > 0.0001f && groundHorizontalVelocity.sqrMagnitude > 0.0001f
                ? Vector2.SignedAngle(waterHorizontalVelocity, groundHorizontalVelocity)
                : 0f;
            var energyWatts = dynamics.BasePowerWatts
                + Mathf.Abs(state.NetBuoyancyForceN) * dynamics.BuoyancyPowerWattsPerNewton
                + state.ActuatorPowerWatts;
            var diagnostics = new SimulationDiagnostics(
                state.WaterVelocityEndMps,
                currentEndMps,
                state.NetBuoyancyForceN,
                energyWatts,
                sideSlipDeg,
                state.AngleOfAttackDeg,
                state.LiftForceN,
                state.DragForceN,
                state.SideForceN,
                state.AngularVelocityRadPerSecond,
                state.HydrodynamicMomentNm,
                state.PistonPositionMm,
                new Vector3(
                    state.RollControlSurfaceDeflectionDeg,
                    state.PitchControlSurfaceDeflectionDeg,
                    state.YawControlSurfaceDeflectionDeg),
                state.ActuatorPowerWatts);
            return new TelemetryFrame(
                rowIndex: rowIndex++,
                rawTime: FormatTelemetryTime(frameElapsedSeconds),
                elapsedSeconds: frameElapsedSeconds,
                longitudeDeg: longitudeDeg,
                latitudeDeg: latitudeDeg,
                depthM: depthM,
                altitudeM: altitudeM,
                headingDeg: NormalizeHeading(state.HeadingDeg),
                pitchDeg: state.PitchDeg,
                rollDeg: state.RollDeg,
                voltage24V: 28.6f,
                current24A: runState == "Surface" ? 0.2f : 0.6f,
                batteryPercent: state.BatteryPercent,
                workMode: "Parameter Simulation",
                runState: runState,
                targetSegment: missionState.CompletedCycles
                    + (missionState.Phase == SimulationMissionPhase.Surface && missionState.CompletedCycles > 0 ? 0 : 1),
                targetHeadingDeg: NormalizeHeading(targetHeadingDeg),
                targetDepthM: targetDepthM,
                targetAltitudeM: waterColumnDepthM - Mathf.Clamp(targetDepthM, 0f, waterColumnDepthM),
                propellerRpm: runState == "Surface" ? 0f : 285f,
                pistonMm: state.PistonPositionMm,
                turnAngleDeg: profile.HeadingDeltaPerCycleDeg,
                diagnostics: diagnostics,
                plannedLongitudeDeg: plannedLongitudeDeg,
                plannedLatitudeDeg: plannedLatitudeDeg,
                missionState: missionState);
        }

        private Vector3 ResolveCurrentAtState(GliderDynamicsState currentState, float timeSeconds)
        {
            LocalMissionCoordinateConverter.ToGeodetic(
                currentState.PositionEndM,
                coordinateProfile.OriginLongitudeDeg,
                coordinateProfile.OriginLatitudeDeg,
                out var longitudeDeg,
                out var latitudeDeg);
            var current = ResolveCurrentVelocity(
                profile,
                longitudeDeg,
                latitudeDeg,
                Mathf.Clamp(currentState.PositionEndM.y, 0f, waterColumnDepthM),
                timeSeconds);
            return new Vector3(current.x, 0f, current.y);
        }

        private static bool ShouldBeginBottomTurn(
            float targetDepthM,
            float currentDepthM,
            float downwardVelocityMps,
            float turnaroundDurationSeconds)
        {
            if (targetDepthM <= 0f)
            {
                return true;
            }

            var maximumBufferM = Mathf.Max(2f, targetDepthM * 0.35f);
            var minimumBufferM = Mathf.Min(12f, maximumBufferM);
            var estimatedBufferM = Mathf.Max(
                minimumBufferM,
                Mathf.Max(0f, downwardVelocityMps) * turnaroundDurationSeconds * 0.8f);
            var turnInitiationDepthM = Mathf.Max(0f, targetDepthM - Mathf.Min(estimatedBufferM, maximumBufferM));
            return currentDepthM >= turnInitiationDepthM;
        }

        private static string RunStateForPhase(SimulationMissionState mission)
        {
            if (mission.Phase == SimulationMissionPhase.BottomTurn)
            {
                return "Turnaround";
            }

            if (mission.Phase == SimulationMissionPhase.LegTimeout)
            {
                return "Leg Timeout";
            }

            if (mission.Phase == SimulationMissionPhase.Surface)
            {
                return "Surface";
            }

            return mission.SafetyWarningRaised ? "Safety Warning" : "Glide";
        }

        private static Vector2 ResolveCurrentVelocity(
            SimulationProfile profile,
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float elapsedSeconds)
        {
            var resolver = new OceanCurrentResolver(profile);
            if (resolver.TryGetVector(
                    new OceanCurrentQuery(longitudeDeg, latitudeDeg, Vector3.zero, depthM, elapsedSeconds),
                    out var current))
            {
                return new Vector2(current.VelocityMps.x, current.VelocityMps.z);
            }

            return Vector2.zero;
        }

        private void AdvanceSurfaceRemainder(float seconds)
        {
            var remainingSeconds = Mathf.Max(0f, seconds);
            var integrationStepSeconds = Mathf.Clamp(dynamics.IntegrationStepSeconds, 0.01f, 1f);
            while (remainingSeconds > 0.0001f)
            {
                var stepSeconds = Mathf.Min(remainingSeconds, integrationStepSeconds);
                var currentEndMps = ResolveCurrentAtState(state, elapsedSeconds + stepSeconds);
                HoldAtSurface(ref state, currentEndMps);
                state.PositionEndM += state.EarthVelocityEndMps * stepSeconds;
                state.PositionEndM.y = 0f;

                HoldAtSurface(ref plannedState, Vector3.zero);
                plannedState.PositionEndM += plannedState.WaterVelocityEndMps * stepSeconds;
                plannedState.PositionEndM.y = 0f;

                elapsedSeconds += stepSeconds;
                remainingSeconds -= stepSeconds;
            }
        }

        private static void ConstrainAtSurface(ref GliderDynamicsState state, Vector3 currentEndMps)
        {
            if (state.PositionEndM.y < 0f)
            {
                HoldAtSurface(ref state, currentEndMps);
            }
        }

        private static void HoldAtSurface(ref GliderDynamicsState state, Vector3 currentEndMps)
        {
            var horizontalWaterVelocity = new Vector3(
                state.WaterVelocityEndMps.x,
                0f,
                state.WaterVelocityEndMps.z);
            state.PositionEndM.y = 0f;
            state.WaterVelocityEndMps = horizontalWaterVelocity;
            state.EarthVelocityEndMps = horizontalWaterVelocity + currentEndMps;
        }

        private static string FormatTelemetryTime(float elapsedSeconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds));
            var days = totalSeconds / 86400;
            totalSeconds -= days * 86400;
            var hours = totalSeconds / 3600;
            totalSeconds -= hours * 3600;
            var minutes = totalSeconds / 60;
            totalSeconds -= minutes * 60;
            return string.Format(CultureInfo.InvariantCulture, "{0:000}d {1:00}:{2:00}:{3:00}", days, hours, minutes, totalSeconds);
        }

        private static float NormalizeHeading(float headingDeg)
        {
            var normalized = headingDeg % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private struct MissionCommands
        {
            public SimulationMissionPhase Phase;
            public float TargetHeadingDeg;
            public float TargetDepthM;
            public float TargetSpeedMps;
            public float CommandedRollDeg;
            public float CommandedPitchDeg;
            public float CommandedNetBuoyancyForceN;
        }
    }
}
