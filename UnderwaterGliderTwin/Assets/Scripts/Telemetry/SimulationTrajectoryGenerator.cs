using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class SimulationTrajectoryGenerator
    {
        private const double MetersPerDegreeLatitude = 111320.0;

        public static IReadOnlyList<TelemetryFrame> GenerateFrames(SimulationProfile profile)
        {
            return GenerateEventDrivenFrames(profile ?? SimulationProfile.Default);
        }

        public static IEnumerable<IReadOnlyList<TelemetryFrame>> GenerateFutureSlices(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var budget = Math.Max(1, frameSliceBudget);
            using (var frames = GenerateSeededFutureFrames(snapshot, profile.Clone()).GetEnumerator())
            {
                while (true)
                {
                    var slice = new List<TelemetryFrame>(budget);
                    while (slice.Count < budget && frames.MoveNext())
                    {
                        slice.Add(frames.Current);
                    }

                    if (slice.Count == 0)
                    {
                        yield break;
                    }

                    yield return slice;
                }
            }
        }

        private static IEnumerable<TelemetryFrame> GenerateSeededFutureFrames(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile)
        {
            var seed = snapshot.Frame;
            var stateOrigin = snapshot.Profile ?? profile;
            var dynamics = profile.Dynamics?.Clone() ?? GliderDynamicsProfile.Default;
            dynamics.CruiseSpeedMps = profile.HorizontalSpeedMps <= 0f
                ? 0f
                : Mathf.Max(0f, dynamics.CruiseSpeedMps);
            var state = snapshot.DynamicsState;
            var plannedState = state;
            // The actual state is local to the source profile origin, whereas planned
            // coordinates are emitted relative to the current planned seed position.
            // Rebase only the planned horizontal state so its first generated point
            // continues from that seed instead of adding the actual history offset twice.
            plannedState.PositionEndM = new Vector3(0f, state.PositionEndM.y, 0f);
            plannedState.EarthVelocityEndMps = plannedState.WaterVelocityEndMps;
            var sampleInterval = Mathf.Max(0.1f, profile.SampleIntervalSeconds);
            var cycleDuration = Mathf.Max(sampleInterval, profile.CycleDurationSeconds);
            var totalDuration = cycleDuration * Mathf.Max(1, profile.CycleCount);
            var frameCount = Mathf.Max(1, Mathf.CeilToInt(totalDuration / sampleInterval));
            var waterColumnDepthM = Mathf.Max(profile.TargetDepthM, profile.WaterColumnDepthM);
            var rowIndex = seed.RowIndex + 1;
            var basePlannedLongitude = seed.HasPlannedPosition ? seed.PlannedLongitudeDeg : seed.LongitudeDeg;
            var basePlannedLatitude = seed.HasPlannedPosition ? seed.PlannedLatitudeDeg : seed.LatitudeDeg;

            for (var frameOffset = 1; frameOffset <= frameCount; frameOffset++)
            {
                var relativeSeconds = frameOffset * sampleInterval;
                var cyclePosition = relativeSeconds / cycleDuration;
                var cycleIndex = Mathf.Min(profile.CycleCount - 1, Mathf.FloorToInt(cyclePosition));
                var phase = cyclePosition - Mathf.Floor(cyclePosition);
                var descending = phase <= 0.5f;
                var targetDepthM = Mathf.Max(
                    0f,
                    profile.TargetDepthM * 0.5f * (1f - Mathf.Cos(phase * Mathf.PI * 2f)));
                var targetHeadingDeg = profile.StartHeadingDeg
                    + (cycleIndex + phase) * profile.HeadingDeltaPerCycleDeg;
                var legPhase = descending ? phase * 2f : (phase - 0.5f) * 2f;
                var envelope = Mathf.Sin(Mathf.Clamp01(legPhase) * Mathf.PI);
                var turnDirection = Mathf.Abs(profile.HeadingDeltaPerCycleDeg) > 0.0001f
                    ? Mathf.Sign(profile.HeadingDeltaPerCycleDeg)
                    : 0f;
                var commandedRollDeg = turnDirection
                    * (descending ? profile.ResolveDescentRollDeg() : -profile.ResolveAscentRollDeg())
                    * envelope;
                var commandedPitchDeg = (descending
                        ? profile.ResolveDescentPitchDeg()
                        : -profile.ResolveAscentPitchDeg())
                    * envelope;
                var commandedNetBuoyancyForceN = profile.HasDirectionalBuoyancyCommands()
                    ? (descending
                        ? profile.ResolveDescentNetBuoyancyForceN(-dynamics.MaxBuoyancyForceN * 0.7f)
                        : profile.ResolveAscentNetBuoyancyForceN(dynamics.MaxBuoyancyForceN * 0.7f)) * envelope
                    : float.NaN;
                var elapsedSeconds = seed.ElapsedSeconds + relativeSeconds;
                var latitudeDeg = stateOrigin.OriginLatitudeDeg + state.PositionEndM.z / MetersPerDegreeLatitude;
                var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latitudeDeg * Math.PI / 180.0);
                var longitudeDeg = Math.Abs(metersPerDegreeLongitude) > 0.001
                    ? stateOrigin.OriginLongitudeDeg + state.PositionEndM.x / metersPerDegreeLongitude
                    : stateOrigin.OriginLongitudeDeg;
                var current = ResolveCurrentVelocity(
                    profile,
                    longitudeDeg,
                    latitudeDeg,
                    Mathf.Clamp(state.PositionEndM.y, 0f, waterColumnDepthM),
                    elapsedSeconds);
                var currentEndMps = new Vector3(current.x, 0f, current.y);
                state = GliderDynamicsIntegrator.Step(
                    state,
                    dynamics,
                    currentEndMps,
                    targetDepthM,
                    targetHeadingDeg,
                    sampleInterval,
                    commandedRollDeg,
                    commandedPitchDeg,
                    commandedNetBuoyancyForceN);
                plannedState = GliderDynamicsIntegrator.Step(
                    plannedState,
                    dynamics,
                    Vector3.zero,
                    targetDepthM,
                    targetHeadingDeg,
                    sampleInterval,
                    commandedRollDeg,
                    commandedPitchDeg,
                    commandedNetBuoyancyForceN);
                ConstrainAtSurface(ref state, currentEndMps);
                ConstrainAtSurface(ref plannedState, Vector3.zero);

                latitudeDeg = stateOrigin.OriginLatitudeDeg + state.PositionEndM.z / MetersPerDegreeLatitude;
                metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latitudeDeg * Math.PI / 180.0);
                longitudeDeg = Math.Abs(metersPerDegreeLongitude) > 0.001
                    ? stateOrigin.OriginLongitudeDeg + state.PositionEndM.x / metersPerDegreeLongitude
                    : stateOrigin.OriginLongitudeDeg;
                var plannedLatitudeDeg = basePlannedLatitude + plannedState.PositionEndM.z / MetersPerDegreeLatitude;
                var plannedMetersPerDegreeLongitude =
                    MetersPerDegreeLatitude * Math.Cos(plannedLatitudeDeg * Math.PI / 180.0);
                var plannedLongitudeDeg = Math.Abs(plannedMetersPerDegreeLongitude) > 0.001
                    ? basePlannedLongitude + plannedState.PositionEndM.x / plannedMetersPerDegreeLongitude
                    : basePlannedLongitude;
                var frame = new List<TelemetryFrame>(1);
                AppendFrame(
                    frame,
                    ref rowIndex,
                    profile,
                    dynamics,
                    state,
                    plannedState,
                    longitudeDeg,
                    latitudeDeg,
                    plannedLongitudeDeg,
                    plannedLatitudeDeg,
                    elapsedSeconds,
                    targetDepthM,
                    waterColumnDepthM,
                    Mathf.Max(1, Mathf.RoundToInt(seed.TargetSegment)) + cycleIndex,
                    targetHeadingDeg,
                    state.PositionEndM.y <= 0.001f ? "Surface" : "Glide");
                yield return frame[0];
            }
        }

        private static IReadOnlyList<TelemetryFrame> GenerateEventDrivenFrames(SimulationProfile profile)
        {
            const float arrivalToleranceM = 1f;
            const float emergencyLegSafetyMultiplier = 3f;
            profile ??= SimulationProfile.Default;

            var frames = new List<TelemetryFrame>();
            var rowIndex = 0;
            var longitudeDeg = profile.OriginLongitudeDeg;
            var latitudeDeg = profile.OriginLatitudeDeg;
            var dynamics = profile.Dynamics?.Clone() ?? GliderDynamicsProfile.Default;
            dynamics.CruiseSpeedMps = Mathf.Max(0f, dynamics.CruiseSpeedMps);
            if (profile.HorizontalSpeedMps <= 0f)
            {
                dynamics.CruiseSpeedMps = 0f;
            }

            var dynamicsState = GliderDynamicsState.AtSurface(profile.StartHeadingDeg);
            var headingRadians = profile.StartHeadingDeg * Mathf.Deg2Rad;
            dynamicsState.WaterVelocityEndMps = new Vector3(
                Mathf.Sin(headingRadians) * dynamics.CruiseSpeedMps,
                0f,
                Mathf.Cos(headingRadians) * dynamics.CruiseSpeedMps);
            var initialCurrent = ResolveCurrentVelocity(profile, longitudeDeg, latitudeDeg, 0f, 0f);
            dynamicsState.EarthVelocityEndMps = dynamicsState.WaterVelocityEndMps
                + new Vector3(initialCurrent.x, 0f, initialCurrent.y);
            var plannedState = dynamicsState;
            plannedState.EarthVelocityEndMps = plannedState.WaterVelocityEndMps;

            var cycleCount = Mathf.Max(1, profile.CycleCount);
            var safetyLegDurationSeconds = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                Mathf.Max(60f, profile.CycleDurationSeconds),
                profile.TargetDepthM,
                dynamics.CruiseSpeedMps);
            var sampleInterval = Mathf.Clamp(profile.SampleIntervalSeconds, 1f, safetyLegDurationSeconds);
            var waterColumnDepthM = Mathf.Max(0f, profile.WaterColumnDepthM, profile.TargetDepthM);
            var elapsedSeconds = 0f;
            var legElapsedSeconds = 0f;
            var completedCycles = 0;
            var descending = true;
            var turningAround = false;
            var turnaroundElapsedSeconds = 0f;
            var stoppedBySafetyLimit = false;
            var safetyWarningRaised = false;

            AppendFrame(
                frames,
                ref rowIndex,
                profile,
                dynamics,
                dynamicsState,
                plannedState,
                longitudeDeg,
                latitudeDeg,
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                0f,
                0f,
                waterColumnDepthM,
                completedCycles + 1,
                profile.StartHeadingDeg,
                "Surface");

            while (completedCycles < cycleCount && !stoppedBySafetyLimit)
            {
                var activeSegment = completedCycles + 1;
                var currentDepthM = Mathf.Clamp(dynamicsState.PositionEndM.y, 0f, waterColumnDepthM);
                var turnaroundDurationSeconds = Mathf.Max(1f, dynamics.TurnaroundDurationSeconds);
                if (descending
                    && !turningAround
                    && ShouldBeginBottomTurn(profile.TargetDepthM, currentDepthM, dynamicsState.EarthVelocityEndMps.y, turnaroundDurationSeconds))
                {
                    turningAround = true;
                    turnaroundElapsedSeconds = 0f;
                    legElapsedSeconds = 0f;
                }

                var depthProgress = profile.TargetDepthM <= 0.001f
                    ? 1f
                    : Mathf.Clamp01(currentDepthM / profile.TargetDepthM);
                var headingProgress = turningAround
                    ? 0.5f
                    : descending ? depthProgress * 0.5f : 0.5f + (1f - depthProgress) * 0.5f;
                var targetHeadingDeg = profile.StartHeadingDeg
                    + (completedCycles + headingProgress) * profile.HeadingDeltaPerCycleDeg;
                var targetDepthM = descending || turningAround ? profile.TargetDepthM : 0f;
                var turnDirection = Mathf.Abs(profile.HeadingDeltaPerCycleDeg) > 0.0001f
                    ? Mathf.Sign(profile.HeadingDeltaPerCycleDeg)
                    : 0f;
                var turnaroundBlend = turningAround
                    ? Mathf.SmoothStep(0f, 1f, turnaroundElapsedSeconds / turnaroundDurationSeconds)
                    : descending ? 0f : 1f;
                var descentRollDeg = turnDirection * profile.ResolveDescentRollDeg();
                var ascentRollDeg = -turnDirection * profile.ResolveAscentRollDeg();
                var descentPitchDeg = profile.ResolveDescentPitchDeg();
                var ascentPitchDeg = -profile.ResolveAscentPitchDeg();
                var commandedRollDeg = Mathf.Lerp(descentRollDeg, ascentRollDeg, turnaroundBlend);
                var commandedPitchDeg = Mathf.Lerp(descentPitchDeg, ascentPitchDeg, turnaroundBlend);
                var commandedNetBuoyancyForceN = profile.HasDirectionalBuoyancyCommands()
                    ? Mathf.Lerp(
                        profile.ResolveDescentNetBuoyancyForceN(-dynamics.MaxBuoyancyForceN * 0.7f),
                        profile.ResolveAscentNetBuoyancyForceN(dynamics.MaxBuoyancyForceN * 0.7f),
                        turnaroundBlend)
                    : float.NaN;

                elapsedSeconds += sampleInterval;
                legElapsedSeconds += sampleInterval;
                var currentVelocity = ResolveCurrentVelocity(
                    profile,
                    longitudeDeg,
                    latitudeDeg,
                    currentDepthM,
                    elapsedSeconds);
                var currentEndMps = new Vector3(currentVelocity.x, 0f, currentVelocity.y);
                dynamicsState = GliderDynamicsIntegrator.Step(
                    dynamicsState,
                    dynamics,
                    currentEndMps,
                    targetDepthM,
                    targetHeadingDeg,
                    sampleInterval,
                    commandedRollDeg,
                    commandedPitchDeg,
                    commandedNetBuoyancyForceN);
                plannedState = GliderDynamicsIntegrator.Step(
                    plannedState,
                    dynamics,
                    Vector3.zero,
                    targetDepthM,
                    targetHeadingDeg,
                    sampleInterval,
                    commandedRollDeg,
                    commandedPitchDeg,
                    commandedNetBuoyancyForceN);

                var runState = turningAround ? "Turnaround" : safetyWarningRaised ? "Safety Warning" : "Glide";
                var resultingDepthM = Mathf.Clamp(dynamicsState.PositionEndM.y, 0f, waterColumnDepthM);
                if (descending && !turningAround && resultingDepthM >= profile.TargetDepthM - arrivalToleranceM)
                {
                    turningAround = true;
                    turnaroundElapsedSeconds = 0f;
                    legElapsedSeconds = 0f;
                    safetyWarningRaised = false;
                    runState = "Turnaround";
                }
                else if (turningAround)
                {
                    turnaroundElapsedSeconds += sampleInterval;
                    runState = "Turnaround";
                    if (turnaroundElapsedSeconds >= turnaroundDurationSeconds)
                    {
                        turningAround = false;
                        descending = false;
                        legElapsedSeconds = 0f;
                        safetyWarningRaised = false;
                    }
                }
                else if (!descending && dynamicsState.PositionEndM.y <= arrivalToleranceM)
                {
                    HoldAtSurface(ref dynamicsState, currentEndMps);
                    HoldAtSurface(ref plannedState, Vector3.zero);
                    completedCycles++;
                    descending = true;
                    legElapsedSeconds = 0f;
                    safetyWarningRaised = false;
                    resultingDepthM = 0f;
                    runState = "Surface";
                }
                else if (legElapsedSeconds >= safetyLegDurationSeconds * emergencyLegSafetyMultiplier)
                {
                    stoppedBySafetyLimit = true;
                    runState = "Leg Timeout";
                }
                else if (legElapsedSeconds >= safetyLegDurationSeconds)
                {
                    safetyWarningRaised = true;
                    runState = "Safety Warning";
                }

                latitudeDeg = profile.OriginLatitudeDeg + dynamicsState.PositionEndM.z / MetersPerDegreeLatitude;
                var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latitudeDeg * Math.PI / 180.0);
                longitudeDeg = Math.Abs(metersPerDegreeLongitude) > 0.001
                    ? profile.OriginLongitudeDeg + dynamicsState.PositionEndM.x / metersPerDegreeLongitude
                    : profile.OriginLongitudeDeg;
                var plannedLatitudeDeg = profile.OriginLatitudeDeg + plannedState.PositionEndM.z / MetersPerDegreeLatitude;
                var plannedMetersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(plannedLatitudeDeg * Math.PI / 180.0);
                var plannedLongitudeDeg = Math.Abs(plannedMetersPerDegreeLongitude) > 0.001
                    ? profile.OriginLongitudeDeg + plannedState.PositionEndM.x / plannedMetersPerDegreeLongitude
                    : profile.OriginLongitudeDeg;

                AppendFrame(
                    frames,
                    ref rowIndex,
                    profile,
                    dynamics,
                    dynamicsState,
                    plannedState,
                    longitudeDeg,
                    latitudeDeg,
                    plannedLongitudeDeg,
                    plannedLatitudeDeg,
                    elapsedSeconds,
                    targetDepthM,
                    waterColumnDepthM,
                    activeSegment,
                    targetHeadingDeg,
                    runState);
            }

            return frames;
        }

        private static bool ShouldBeginBottomTurn(float targetDepthM, float currentDepthM, float downwardVelocityMps, float turnaroundDurationSeconds)
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

        private static IReadOnlyList<TelemetryFrame> GenerateLegacyFrames(SimulationProfile profile)
        {
            profile ??= SimulationProfile.Default;

            var frames = new List<TelemetryFrame>();
            var rowIndex = 0;
            var longitudeDeg = profile.OriginLongitudeDeg;
            var latitudeDeg = profile.OriginLatitudeDeg;
            var dynamics = profile.Dynamics?.Clone() ?? GliderDynamicsProfile.Default;
            dynamics.CruiseSpeedMps = Mathf.Max(0f, dynamics.CruiseSpeedMps);
            if (profile.HorizontalSpeedMps <= 0f)
            {
                // Retain the explicit zero-speed profile as a current-only drift scenario.
                dynamics.CruiseSpeedMps = 0f;
            }
            var dynamicsState = GliderDynamicsState.AtSurface(profile.StartHeadingDeg);
            var initialHeadingRadians = profile.StartHeadingDeg * Mathf.Deg2Rad;
            dynamicsState.WaterVelocityEndMps = new Vector3(
                Mathf.Sin(initialHeadingRadians) * dynamics.CruiseSpeedMps,
                0f,
                Mathf.Cos(initialHeadingRadians) * dynamics.CruiseSpeedMps);
            var initialCurrent = ResolveCurrentVelocity(
                profile,
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                0f,
                0f);
            dynamicsState.EarthVelocityEndMps = dynamicsState.WaterVelocityEndMps
                + new Vector3(initialCurrent.x, 0f, initialCurrent.y);
            var plannedState = dynamicsState;
            plannedState.EarthVelocityEndMps = plannedState.WaterVelocityEndMps;
            var cycleCount = Math.Max(1, profile.CycleCount);
            var cycleDuration = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                Mathf.Max(60f, profile.CycleDurationSeconds),
                profile.TargetDepthM,
                dynamics.CruiseSpeedMps);
            var sampleInterval = Mathf.Clamp(profile.SampleIntervalSeconds, 1f, cycleDuration);
            var samplesPerCycle = Mathf.Max(2, Mathf.RoundToInt(cycleDuration / sampleInterval));
            var waterColumnDepthM = Mathf.Max(0f, profile.WaterColumnDepthM, profile.TargetDepthM);
            var previousElapsedSeconds = 0f;
            var hasPreviousFrame = false;

            for (var cycle = 0; cycle < cycleCount; cycle++)
            {
                for (var sample = 0; sample <= samplesPerCycle; sample++)
                {
                    if (cycle > 0 && sample == 0)
                    {
                        continue;
                    }

                    var phase = sample / (float)samplesPerCycle;
                    var headingDeg = profile.StartHeadingDeg
                        + (cycle + phase) * profile.HeadingDeltaPerCycleDeg;
                    var frameElapsedSeconds = cycle * cycleDuration + phase * cycleDuration;
                    // Raised cosine gives zero vertical command slope at surface and turnaround.
                    var targetDepthM = Mathf.Max(0f, profile.TargetDepthM * 0.5f * (1f - Mathf.Cos(phase * Mathf.PI * 2f)));
                    var descending = phase <= 0.5f;
                    var legPhase = descending ? phase * 2f : (phase - 0.5f) * 2f;
                    var legEnvelope = Mathf.Sin(Mathf.Clamp01(legPhase) * Mathf.PI);
                    var turnDirection = Mathf.Abs(profile.HeadingDeltaPerCycleDeg) > 0.0001f
                        ? Mathf.Sign(profile.HeadingDeltaPerCycleDeg)
                        : 0f;
                    var legRollDeg = descending ? profile.ResolveDescentRollDeg() : -profile.ResolveAscentRollDeg();
                    var commandedRollDeg = turnDirection * legRollDeg * legEnvelope;
                    var legPitchDeg = descending ? profile.ResolveDescentPitchDeg() : -profile.ResolveAscentPitchDeg();
                    var commandedPitchDeg = legPitchDeg * legEnvelope;
                    var commandedNetBuoyancyForceN = profile.HasDirectionalBuoyancyCommands()
                        ? (descending
                            ? profile.ResolveDescentNetBuoyancyForceN(-dynamics.MaxBuoyancyForceN * 0.7f)
                            : profile.ResolveAscentNetBuoyancyForceN(dynamics.MaxBuoyancyForceN * 0.7f)) * legEnvelope
                        : float.NaN;
                    var currentSampleDepthM = Mathf.Clamp(
                        dynamicsState.PositionEndM.y,
                        0f,
                        waterColumnDepthM);
                    var currentVelocity = ResolveCurrentVelocity(
                        profile,
                        longitudeDeg,
                        latitudeDeg,
                        currentSampleDepthM,
                        frameElapsedSeconds);
                    var currentEndMps = new Vector3(currentVelocity.x, 0f, currentVelocity.y);
                    var isSurfaceConstrained = false;
                    if (hasPreviousFrame)
                    {
                        var deltaSeconds = frameElapsedSeconds - previousElapsedSeconds;
                        dynamicsState = GliderDynamicsIntegrator.Step(
                            dynamicsState,
                            dynamics,
                            currentEndMps,
                            targetDepthM,
                            headingDeg,
                            deltaSeconds,
                            commandedRollDeg,
                            commandedPitchDeg,
                            commandedNetBuoyancyForceN);
                        plannedState = GliderDynamicsIntegrator.Step(
                            plannedState,
                            dynamics,
                            Vector3.zero,
                            targetDepthM,
                            headingDeg,
                            deltaSeconds,
                            commandedRollDeg,
                            commandedPitchDeg,
                            commandedNetBuoyancyForceN);

                        isSurfaceConstrained = ConstrainAtSurface(ref dynamicsState, currentEndMps);
                        ConstrainAtSurface(ref plannedState, Vector3.zero);
                    }

                    if (sample == 0 || sample == samplesPerCycle)
                    {
                        dynamicsState.PositionEndM.y = targetDepthM;
                        dynamicsState.EarthVelocityEndMps.y = 0f;
                        dynamicsState.WaterVelocityEndMps.y = 0f;
                        plannedState.PositionEndM.y = targetDepthM;
                        plannedState.EarthVelocityEndMps.y = 0f;
                        plannedState.WaterVelocityEndMps.y = 0f;
                        if (targetDepthM <= 0.001f)
                        {
                            HoldAtSurface(ref dynamicsState, currentEndMps);
                            HoldAtSurface(ref plannedState, Vector3.zero);
                            isSurfaceConstrained = true;
                        }
                    }

                    var depthM = Mathf.Clamp(dynamicsState.PositionEndM.y, 0f, waterColumnDepthM);
                    latitudeDeg = profile.OriginLatitudeDeg + dynamicsState.PositionEndM.z / MetersPerDegreeLatitude;
                    var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latitudeDeg * Math.PI / 180.0);
                    longitudeDeg = Math.Abs(metersPerDegreeLongitude) > 0.001
                        ? profile.OriginLongitudeDeg + dynamicsState.PositionEndM.x / metersPerDegreeLongitude
                        : profile.OriginLongitudeDeg;
                    var plannedLatitudeDeg = profile.OriginLatitudeDeg + plannedState.PositionEndM.z / MetersPerDegreeLatitude;
                    var plannedMetersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(plannedLatitudeDeg * Math.PI / 180.0);
                    var plannedLongitudeDeg = Math.Abs(plannedMetersPerDegreeLongitude) > 0.001
                        ? profile.OriginLongitudeDeg + plannedState.PositionEndM.x / plannedMetersPerDegreeLongitude
                        : profile.OriginLongitudeDeg;

                    var altitudeM = Mathf.Max(0f, waterColumnDepthM - depthM);
                    var runState = isSurfaceConstrained || sample == 0 || sample == samplesPerCycle ? "Surface" : "Glide";
                    var waterHorizontalVelocity = new Vector2(dynamicsState.WaterVelocityEndMps.x, dynamicsState.WaterVelocityEndMps.z);
                    var groundVelocity = dynamicsState.EarthVelocityEndMps;
                    var groundHorizontalVelocity = new Vector2(groundVelocity.x, groundVelocity.z);
                    var sideSlipDeg = waterHorizontalVelocity.sqrMagnitude > 0.0001f && groundHorizontalVelocity.sqrMagnitude > 0.0001f
                        ? Vector2.SignedAngle(waterHorizontalVelocity, groundHorizontalVelocity)
                        : 0f;
                    var energyWatts = dynamics.BasePowerWatts
                        + Mathf.Abs(dynamicsState.NetBuoyancyForceN) * dynamics.BuoyancyPowerWattsPerNewton
                        + dynamicsState.ActuatorPowerWatts;
                    var diagnostics = new SimulationDiagnostics(
                        dynamicsState.WaterVelocityEndMps,
                        currentEndMps,
                        dynamicsState.NetBuoyancyForceN,
                        energyWatts,
                        sideSlipDeg,
                        dynamicsState.AngleOfAttackDeg,
                        dynamicsState.LiftForceN,
                        dynamicsState.DragForceN,
                        dynamicsState.SideForceN,
                        dynamicsState.AngularVelocityRadPerSecond,
                        dynamicsState.HydrodynamicMomentNm,
                        dynamicsState.PistonPositionMm,
                        new Vector3(
                            dynamicsState.RollControlSurfaceDeflectionDeg,
                            dynamicsState.PitchControlSurfaceDeflectionDeg,
                            dynamicsState.YawControlSurfaceDeflectionDeg),
                        dynamicsState.ActuatorPowerWatts);

                    frames.Add(new TelemetryFrame(
                        rowIndex: rowIndex,
                        rawTime: FormatTelemetryTime(frameElapsedSeconds),
                        elapsedSeconds: frameElapsedSeconds,
                        longitudeDeg: longitudeDeg,
                        latitudeDeg: latitudeDeg,
                        depthM: depthM,
                        altitudeM: altitudeM,
                        headingDeg: NormalizeHeading(dynamicsState.HeadingDeg),
                        pitchDeg: dynamicsState.PitchDeg,
                        rollDeg: dynamicsState.RollDeg,
                        voltage24V: 28.6f,
                        current24A: runState == "Surface" ? 0.2f : 0.6f,
                        batteryPercent: dynamicsState.BatteryPercent,
                        workMode: "Parameter Simulation",
                        runState: runState,
                        targetSegment: cycle + 1,
                        targetHeadingDeg: NormalizeHeading(headingDeg),
                        targetDepthM: targetDepthM,
                        targetAltitudeM: altitudeM,
                        propellerRpm: runState == "Surface" ? 0f : 285f,
                        pistonMm: dynamicsState.PistonPositionMm,
                        turnAngleDeg: profile.HeadingDeltaPerCycleDeg,
                        diagnostics: diagnostics,
                        plannedLongitudeDeg: plannedLongitudeDeg,
                        plannedLatitudeDeg: plannedLatitudeDeg));

                    rowIndex++;
                    previousElapsedSeconds = frameElapsedSeconds;
                    hasPreviousFrame = true;
                }
            }

            return frames;
        }

        private static void AppendFrame(
            List<TelemetryFrame> frames,
            ref int rowIndex,
            SimulationProfile profile,
            GliderDynamicsProfile dynamics,
            GliderDynamicsState dynamicsState,
            GliderDynamicsState plannedState,
            double longitudeDeg,
            double latitudeDeg,
            double plannedLongitudeDeg,
            double plannedLatitudeDeg,
            float elapsedSeconds,
            float targetDepthM,
            float waterColumnDepthM,
            int targetSegment,
            float targetHeadingDeg,
            string runState)
        {
            var depthM = Mathf.Clamp(dynamicsState.PositionEndM.y, 0f, waterColumnDepthM);
            var altitudeM = Mathf.Max(0f, waterColumnDepthM - depthM);
            var waterHorizontalVelocity = new Vector2(dynamicsState.WaterVelocityEndMps.x, dynamicsState.WaterVelocityEndMps.z);
            var groundVelocity = dynamicsState.EarthVelocityEndMps;
            var groundHorizontalVelocity = new Vector2(groundVelocity.x, groundVelocity.z);
            var sideSlipDeg = waterHorizontalVelocity.sqrMagnitude > 0.0001f && groundHorizontalVelocity.sqrMagnitude > 0.0001f
                ? Vector2.SignedAngle(waterHorizontalVelocity, groundHorizontalVelocity)
                : 0f;
            var energyWatts = dynamics.BasePowerWatts
                + Mathf.Abs(dynamicsState.NetBuoyancyForceN) * dynamics.BuoyancyPowerWattsPerNewton
                + dynamicsState.ActuatorPowerWatts;
            var diagnostics = new SimulationDiagnostics(
                dynamicsState.WaterVelocityEndMps,
                dynamicsState.EarthVelocityEndMps - dynamicsState.WaterVelocityEndMps,
                dynamicsState.NetBuoyancyForceN,
                energyWatts,
                sideSlipDeg,
                dynamicsState.AngleOfAttackDeg,
                dynamicsState.LiftForceN,
                dynamicsState.DragForceN,
                dynamicsState.SideForceN,
                dynamicsState.AngularVelocityRadPerSecond,
                dynamicsState.HydrodynamicMomentNm,
                dynamicsState.PistonPositionMm,
                new Vector3(
                    dynamicsState.RollControlSurfaceDeflectionDeg,
                    dynamicsState.PitchControlSurfaceDeflectionDeg,
                    dynamicsState.YawControlSurfaceDeflectionDeg),
                dynamicsState.ActuatorPowerWatts);

            frames.Add(new TelemetryFrame(
                rowIndex: rowIndex++,
                rawTime: FormatTelemetryTime(elapsedSeconds),
                elapsedSeconds: elapsedSeconds,
                longitudeDeg: longitudeDeg,
                latitudeDeg: latitudeDeg,
                depthM: depthM,
                altitudeM: altitudeM,
                headingDeg: NormalizeHeading(dynamicsState.HeadingDeg),
                pitchDeg: dynamicsState.PitchDeg,
                rollDeg: dynamicsState.RollDeg,
                voltage24V: 28.6f,
                current24A: runState == "Surface" ? 0.2f : 0.6f,
                batteryPercent: dynamicsState.BatteryPercent,
                workMode: "Parameter Simulation",
                runState: runState,
                targetSegment: targetSegment,
                targetHeadingDeg: NormalizeHeading(targetHeadingDeg),
                targetDepthM: targetDepthM,
                targetAltitudeM: altitudeM,
                propellerRpm: runState == "Surface" ? 0f : 285f,
                pistonMm: dynamicsState.PistonPositionMm,
                turnAngleDeg: profile.HeadingDeltaPerCycleDeg,
                diagnostics: diagnostics,
                plannedLongitudeDeg: plannedLongitudeDeg,
                plannedLatitudeDeg: plannedLatitudeDeg));
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

        private static bool ConstrainAtSurface(ref GliderDynamicsState state, Vector3 currentEndMps)
        {
            if (state.PositionEndM.y >= 0f)
            {
                return false;
            }

            HoldAtSurface(ref state, currentEndMps);
            return true;
        }

        private static void HoldAtSurface(ref GliderDynamicsState state, Vector3 currentEndMps)
        {
            state.PositionEndM.y = 0f;
            state.WaterVelocityEndMps = Vector3.zero;
            state.EarthVelocityEndMps = currentEndMps;
        }

        private static float NormalizeHeading(float headingDeg)
        {
            var normalized = headingDeg % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }
    }

    public sealed class SimulationFutureTrajectoryGenerator : ISimulationFutureGenerator
    {
        private readonly MonoBehaviour coroutineHost;

        public SimulationFutureTrajectoryGenerator(MonoBehaviour coroutineHost)
        {
            this.coroutineHost = coroutineHost != null
                ? coroutineHost
                : throw new ArgumentNullException(nameof(coroutineHost));
        }

        public ISimulationRebuildOperation GenerateFuture(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget,
            Action<SimulationRebuildResult> onCompleted)
        {
            if (onCompleted == null)
            {
                throw new ArgumentNullException(nameof(onCompleted));
            }

            var operation = new CoroutineRebuildOperation();
            operation.Coroutine = coroutineHost.StartCoroutine(
                Generate(snapshot, profile, frameSliceBudget, onCompleted, operation));
            return operation;
        }

        private static IEnumerator Generate(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget,
            Action<SimulationRebuildResult> onCompleted,
            CoroutineRebuildOperation operation)
        {
            var stagedFuture = new List<TelemetryFrame>();
            IEnumerator<IReadOnlyList<TelemetryFrame>> slices;
            try
            {
                slices = SimulationTrajectoryGenerator
                    .GenerateFutureSlices(snapshot, profile, frameSliceBudget)
                    .GetEnumerator();
            }
            catch (Exception ex)
            {
                onCompleted(SimulationRebuildResult.Failure(ex.Message));
                yield break;
            }

            using (slices)
            {
                while (!operation.IsCancelled)
                {
                    bool hasNext;
                    IReadOnlyList<TelemetryFrame> slice = null;
                    try
                    {
                        hasNext = slices.MoveNext();
                        if (hasNext)
                        {
                            slice = slices.Current;
                        }
                    }
                    catch (Exception ex)
                    {
                        onCompleted(SimulationRebuildResult.Failure(ex.Message));
                        yield break;
                    }

                    if (!hasNext)
                    {
                        onCompleted(SimulationRebuildResult.Success(stagedFuture));
                        yield break;
                    }

                    stagedFuture.AddRange(slice);
                    yield return null;
                }
            }

            onCompleted(SimulationRebuildResult.Cancelled());
        }

        private sealed class CoroutineRebuildOperation : ISimulationRebuildOperation
        {
            public Coroutine Coroutine { get; set; }
            public bool IsCancelled { get; private set; }

            public void Cancel()
            {
                IsCancelled = true;
            }
        }
    }
}
