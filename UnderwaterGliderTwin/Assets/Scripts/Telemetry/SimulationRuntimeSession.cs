using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Playback;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationRuntimeSession
    {
        public const int MaximumTimelineFrameCount = 200000;
        private const double EstimatedSecondsPerFutureSlice = 0.1d;
        private static readonly TimeSpan MaximumPendingTimeout = TimeSpan.FromMinutes(5);

        private readonly PlaybackModel playback;
        private readonly ISimulationFutureGenerator generator;
        private readonly int frameSliceBudget;
        private readonly TimeSpan timeout;
        private readonly Func<DateTime> utcNow;
        private readonly SimulationTrajectoryTimeline timeline;
        private SimulationProfile activeProfile;
        private SimulationProfile queuedProfile;
        private ISimulationRebuildOperation pendingOperation;
        private DateTime pendingDeadline;
        private int requestVersion;
        private long requestIdCounter;
        private long generationToken;
        private int pendingSeedRowIndex;
        private float pendingSeedElapsedSeconds;
        private bool pendingIsRefill;
        private long pendingRequestId;

        public event Action StatusChanged;

        public bool IsRebuildPending { get; private set; }
        public string LastError { get; private set; }
        public SimulationProfile ActiveProfile => activeProfile.Clone();
        public SimulationProfile QueuedProfile => queuedProfile?.Clone();
        public SimulationTrajectoryTimeline Timeline => timeline;
        public SimulationTimelineStatus Status { get; private set; } = SimulationTimelineStatus.Idle;

        public SimulationRuntimeSession(
            PlaybackModel playback,
            SimulationProfile activeProfile,
            ISimulationFutureGenerator generator,
            int frameSliceBudget = 128,
            TimeSpan? timeout = null,
            Func<DateTime> utcNow = null,
            SimulationTrajectoryTimeline timeline = null)
        {
            this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.activeProfile = (activeProfile ?? SimulationProfile.Default).Clone();
            this.frameSliceBudget = Math.Max(1, frameSliceBudget);
            this.timeout = timeout ?? TimeSpan.FromSeconds(30);
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            this.timeline = timeline ?? SimulationTrajectoryTimeline.CreateInitial(playback.Frames, this.activeProfile, this.utcNow());
            if (playback.IsTimelineBound)
            {
                if (!ReferenceEquals(playback.Frames, this.timeline.CommittedSnapshot.Frames))
                {
                    throw new ArgumentException("Playback is bound to a different timeline.", nameof(timeline));
                }
            }
        }

        public bool RequestProfileUpdate(SimulationProfile candidate)
        {
            if (!TryValidateProfile(candidate, out var profileError))
            {
                SetError(profileError);
                return false;
            }

            var candidateSnapshot = candidate.Clone();
            var version = ++requestVersion;
            var requestId = ++requestIdCounter;
            LastError = null;
            IsRebuildPending = true;
            Status = SimulationTimelineStatus.Queued;
            queuedProfile = candidateSnapshot;
            pendingIsRefill = false;
            pendingRequestId = requestId;
            pendingDeadline = utcNow().Add(CalculatePendingTimeout(candidateSnapshot));
            StatusChanged?.Invoke();

            try
            {
                pendingOperation?.Cancel();
                StartGeneration(version, candidateSnapshot, requestId, false);
                return true;
            }
            catch (Exception ex)
            {
                FailRequest(version, "Simulation rebuild failed: " + ex.Message);
                return false;
            }
        }

        public void CancelPendingRebuild()
        {
            if (!IsRebuildPending)
            {
                return;
            }

            pendingOperation?.Cancel();
            requestVersion++;
                FailRequest(requestVersion, "Simulation rebuild was cancelled.", SimulationTimelineStatus.Cancelled);
        }

        public void Tick()
        {
            if (IsRebuildPending && utcNow() >= pendingDeadline)
            {
                pendingOperation?.Cancel();
                requestVersion++;
                FailRequest(requestVersion, "Simulation rebuild timed out.", SimulationTimelineStatus.Failed);
            }
            else if (!IsRebuildPending)
            {
                TryEnsureFutureHorizon();
            }
        }

        private void CompleteRequest(
            int version,
            SimulationProfile candidate,
            long requestId,
            long operationToken,
            bool isRefill,
            SimulationRebuildResult result)
        {
            if (!IsRebuildPending
                || version != requestVersion
                || operationToken != generationToken)
            {
                return;
            }

            if (result == null || !result.Succeeded)
            {
                var error = result?.Error;
                FailRequest(
                    version,
                    string.IsNullOrWhiteSpace(error)
                        ? result != null && result.WasCancelled
                            ? "Simulation rebuild was cancelled."
                            : "Simulation rebuild failed."
                        : error,
                    result != null && result.WasCancelled
                        ? SimulationTimelineStatus.Cancelled
                        : SimulationTimelineStatus.Failed);
                return;
            }

            try
            {
                if (!isRefill
                    && (playback.CurrentFrame.RowIndex != pendingSeedRowIndex
                    || Math.Abs(playback.CurrentFrame.ElapsedSeconds - pendingSeedElapsedSeconds) > 0.0001f)
                )
                {
                    pendingOperation = null;
                    StartGeneration(version, candidate, requestId, isRefill);
                    return;
                }

                var preservedIndex = playback.CurrentIndex;
                if (!TryValidateGeneratedFuture(result.Frames, out var futureError))
                {
                    FailRequest(version, futureError);
                    return;
                }

                if (isRefill)
                {
                    var refillFrames = StampProfileSequence(result.Frames, CurrentProfileSequence());
                    if (!TryValidateAppend(refillFrames, out var appendError))
                    {
                        FailRequest(version, appendError);
                        return;
                    }

                    timeline.AppendFuture(refillFrames);
                    pendingOperation = null;
                    IsRebuildPending = false;
                    queuedProfile = null;
                    LastError = null;
                    Status = IsMissionComplete() ? SimulationTimelineStatus.Completed : SimulationTimelineStatus.Committed;
                    timeline.SetStatus(Status);
                    StatusChanged?.Invoke();
                    return;
                }

                var nextProfileSequence = CurrentProfileSequence() + 1;
                var stampedFuture = StampProfileSequence(result.Frames, nextProfileSequence);
                var stagedFrames = BuildStagingFrames(stampedFuture, preservedIndex);
                if (!TryValidateStaging(stagedFrames, preservedIndex, candidate, out var validationError))
                {
                    FailRequest(version, validationError);
                    return;
                }

                var segment = new SimulationTimelineSegment(
                    nextProfileSequence,
                    requestId,
                    stampedFuture[0].RowIndex,
                    stampedFuture[0].ElapsedSeconds,
                    candidate,
                    utcNow());
                timeline.ReplaceFutureFrom(preservedIndex, stampedFuture, segment);
                if (!playback.IsTimelineBound)
                {
                    playback.BindTimeline(timeline, notify: true);
                }
                activeProfile = candidate.Clone();
                pendingOperation = null;
                IsRebuildPending = false;
                queuedProfile = null;
                LastError = null;
                Status = IsMissionComplete() ? SimulationTimelineStatus.Completed : SimulationTimelineStatus.Committed;
                timeline.SetStatus(Status);
                StatusChanged?.Invoke();
            }
            catch (Exception ex)
            {
                FailRequest(version, "Simulation rebuild failed: " + ex.Message);
            }
        }

        private void StartGeneration(int version, SimulationProfile candidate, long requestId, bool isRefill)
        {
            var seedFrame = isRefill
                ? timeline.CommittedSnapshot.Frames[timeline.CommittedSnapshot.Frames.Count - 1]
                : playback.CurrentFrame;
            pendingSeedRowIndex = seedFrame.RowIndex;
            pendingSeedElapsedSeconds = seedFrame.ElapsedSeconds;
            var seed = SimulationStateSnapshot.FromFrame(seedFrame, isRefill ? activeProfile : activeProfile);
            var token = ++generationToken;
            pendingIsRefill = isRefill;
            pendingRequestId = requestId;
            Status = SimulationTimelineStatus.Generating;
            StatusChanged?.Invoke();
            var maximumFrameCount = Math.Max(0, MaximumTimelineFrameCount - timeline.CommittedSnapshot.Frames.Count);
            var operation = generator.GenerateFuture(
                seed,
                candidate,
                frameSliceBudget,
                maximumFrameCount,
                result => CompleteRequest(version, candidate, requestId, token, isRefill, result));
            if (IsRebuildPending && version == requestVersion)
            {
                pendingOperation = operation;
            }
            else
            {
                operation?.Cancel();
            }
        }

        public bool TryEnsureFutureHorizon()
        {
            if (IsRebuildPending
                || Status == SimulationTimelineStatus.Completed
                || timeline.CommittedSnapshot.Frames.Count >= MaximumTimelineFrameCount
                || IsMissionComplete())
            {
                return false;
            }

            var remainingFrames = timeline.CommittedSnapshot.Frames.Count - playback.CurrentIndex - 1;
            var remainingSeconds = timeline.CommittedSnapshot.Frames[timeline.CommittedSnapshot.Frames.Count - 1].ElapsedSeconds
                - playback.CurrentElapsedSeconds;
            if (remainingSeconds >= 120f && remainingFrames >= 128)
            {
                return false;
            }

            var version = ++requestVersion;
            var requestId = 0L;
            IsRebuildPending = true;
            queuedProfile = null;
            LastError = null;
            pendingDeadline = utcNow().Add(CalculatePendingTimeout(activeProfile));
            StartGeneration(version, activeProfile.Clone(), requestId, true);
            return true;
        }

        private TimeSpan CalculatePendingTimeout(SimulationProfile candidate)
        {
            var estimatedFutureFrames = Math.Ceiling(
                (double)candidate.CycleCount * candidate.CycleDurationSeconds / candidate.SampleIntervalSeconds);
            var estimatedSlices = Math.Ceiling(estimatedFutureFrames / frameSliceBudget);
            var workloadSeconds = Math.Min(
                MaximumPendingTimeout.TotalSeconds,
                Math.Max(0d, estimatedSlices * EstimatedSecondsPerFutureSlice));
            var deadlineSeconds = Math.Min(
                MaximumPendingTimeout.TotalSeconds,
                Math.Max(timeout.TotalSeconds, workloadSeconds));
            return TimeSpan.FromSeconds(deadlineSeconds);
        }

        internal SimulationProfile ActiveProfileReference => activeProfile;

        private IReadOnlyList<TelemetryFrame> BuildStagingFrames(
            IReadOnlyList<TelemetryFrame> future,
            int preservedIndex)
        {
            if (future == null || future.Count == 0)
            {
                throw new ArgumentException("Generated future is empty.", nameof(future));
            }

            var staging = new List<TelemetryFrame>(preservedIndex + 1 + future.Count);
            for (var i = 0; i <= preservedIndex; i++)
            {
                staging.Add(playback.Frames[i]);
            }

            for (var i = 0; i < future.Count; i++)
            {
                if (future[i].ElapsedSeconds > playback.Frames[preservedIndex].ElapsedSeconds)
                {
                    staging.Add(future[i]);
                }
            }

            if (staging.Count == preservedIndex + 1)
            {
                throw new ArgumentException(
                    "Generated future does not extend beyond the current playback frame.",
                    nameof(future));
            }

            return staging;
        }

        private static bool TryValidateStaging(
            IReadOnlyList<TelemetryFrame> frames,
            int preservedIndex,
            SimulationProfile profile,
            out string error)
        {
            var maximumDepth = Math.Max(profile.TargetDepthM, profile.WaterColumnDepthM);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (!IsFrameFinite(frame))
                {
                    error = $"Generated frame {i} must contain finite values.";
                    return false;
                }

                if (i > 0 && frame.ElapsedSeconds <= frames[i - 1].ElapsedSeconds)
                {
                    error = $"Generated frame {i} elapsed time must be strictly monotonic.";
                    return false;
                }

                if (i > preservedIndex && (frame.DepthM < 0f || frame.DepthM > maximumDepth + 0.01f))
                {
                    error = $"Generated frame {i} depth is outside profile bounds.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryValidateGeneratedFuture(
            IReadOnlyList<TelemetryFrame> future,
            out string error)
        {
            if (future == null || future.Count == 0)
            {
                error = "Generated future is empty.";
                return false;
            }

            for (var i = 0; i < future.Count; i++)
            {
                var frame = future[i];
                if (!IsFrameFinite(frame))
                {
                    error = $"Generated frame {i} must contain finite values.";
                    return false;
                }

                if (i > 0 && frame.ElapsedSeconds <= future[i - 1].ElapsedSeconds)
                {
                    error = $"Generated frame {i} elapsed time must be strictly monotonic.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TryValidateProfile(SimulationProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "SimulationProfile is required.";
                return false;
            }

            if (!GliderDynamicsProfileValidator.TryValidate(profile.Dynamics, out error))
            {
                return false;
            }

            if (profile.CycleCount <= 0)
            {
                error = "CycleCount must be positive.";
                return false;
            }

            if (!IsFinite(profile.CycleDurationSeconds) || profile.CycleDurationSeconds <= 0f)
            {
                error = "CycleDurationSeconds must be finite and positive.";
                return false;
            }

            if (!IsFinite(profile.SampleIntervalSeconds) || profile.SampleIntervalSeconds <= 0f)
            {
                error = "SampleIntervalSeconds must be finite and positive.";
                return false;
            }

            if (!IsFinite(profile.TargetDepthM)
                || !IsFinite(profile.WaterColumnDepthM)
                || profile.TargetDepthM < 0f
                || profile.WaterColumnDepthM < profile.TargetDepthM)
            {
                error = "TargetDepthM must fit inside WaterColumnDepthM.";
                return false;
            }

            if (!IsFinite(profile.HorizontalSpeedMps) || profile.HorizontalSpeedMps < 0f)
            {
                error = "HorizontalSpeedMps must be finite and non-negative.";
                return false;
            }

            if (!IsFinite(profile.OriginLongitudeDeg)
                || !IsFinite(profile.OriginLatitudeDeg)
                || profile.OriginLongitudeDeg < -180d
                || profile.OriginLongitudeDeg > 180d
                || profile.OriginLatitudeDeg < -90d
                || profile.OriginLatitudeDeg > 90d)
            {
                error = "Simulation origin must contain valid finite coordinates.";
                return false;
            }

            if (profile.CycleCount < 1)
            {
                error = "CycleCount must be positive.";
                return false;
            }

            var estimatedFrames = (double)profile.CycleCount
                * profile.CycleDurationSeconds
                / profile.SampleIntervalSeconds;
            if (estimatedFrames > MaximumTimelineFrameCount)
            {
                error = $"Projected timeline frame count {estimatedFrames:0} exceeds {MaximumTimelineFrameCount}.";
                return false;
            }

            if (profile.CycleCount < activeProfile.CycleCount)
            {
                error = "CycleCount cannot be reduced while a simulation is running.";
                return false;
            }

            if (Math.Abs(profile.OriginLongitudeDeg - activeProfile.OriginLongitudeDeg) > 0.0000001d
                || Math.Abs(profile.OriginLatitudeDeg - activeProfile.OriginLatitudeDeg) > 0.0000001d)
            {
                error = "Simulation origin cannot change while a simulation is running.";
                return false;
            }

            error = null;
            return true;
        }

        private void FailRequest(
            int version,
            string error,
            SimulationTimelineStatus status = SimulationTimelineStatus.Failed)
        {
            if (!IsRebuildPending || version != requestVersion)
            {
                return;
            }

            pendingOperation = null;
            IsRebuildPending = false;
            queuedProfile = null;
            LastError = error;
            Status = status;
            timeline.SetStatus(status);
            StatusChanged?.Invoke();
        }

        private int CurrentProfileSequence()
        {
            var frames = timeline.CommittedSnapshot.Frames;
            return frames.Count == 0 ? 0 : frames[frames.Count - 1].ProfileSequence;
        }

        private bool IsMissionComplete()
        {
            var frame = timeline.CommittedSnapshot.Frames[timeline.CommittedSnapshot.Frames.Count - 1];
            if (!frame.MissionState.HasValue)
            {
                return false;
            }

            var mission = frame.MissionState.Value;
            return mission.CompletedCycles >= activeProfile.CycleCount
                || mission.Phase == SimulationMissionPhase.LegTimeout;
        }

        private static IReadOnlyList<TelemetryFrame> StampProfileSequence(
            IReadOnlyList<TelemetryFrame> frames,
            int profileSequence)
        {
            var stamped = new List<TelemetryFrame>(frames.Count);
            for (var i = 0; i < frames.Count; i++)
            {
                stamped.Add(frames[i].WithProfileSequence(profileSequence));
            }

            return stamped;
        }

        private bool TryValidateAppend(
            IReadOnlyList<TelemetryFrame> future,
            out string error)
        {
            var previous = timeline.CommittedSnapshot.Frames[timeline.CommittedSnapshot.Frames.Count - 1];
            if (future == null || future.Count == 0)
            {
                error = "Generated append future is empty.";
                return false;
            }

            for (var i = 0; i < future.Count; i++)
            {
                if (!IsFrameFinite(future[i])
                    || future[i].RowIndex <= previous.RowIndex
                    || future[i].ElapsedSeconds <= previous.ElapsedSeconds
                    || future[i].ProfileSequence != previous.ProfileSequence)
                {
                    error = $"Generated append frame {i} is not a valid continuation.";
                    return false;
                }

                previous = future[i];
            }

            error = null;
            return true;
        }

        private void SetError(string error)
        {
            LastError = error;
            StatusChanged?.Invoke();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteOrNaNPlanned(double longitude, double latitude)
        {
            return (double.IsNaN(longitude) && double.IsNaN(latitude))
                || (IsFinite(longitude) && IsFinite(latitude));
        }

        private static bool IsFrameFinite(TelemetryFrame frame)
        {
            if (!IsFinite(frame.ElapsedSeconds)
                || !IsFinite(frame.LongitudeDeg)
                || !IsFinite(frame.LatitudeDeg)
                || !IsFinite(frame.DepthM)
                || !IsFinite(frame.AltitudeM)
                || !IsFinite(frame.HeadingDeg)
                || !IsFinite(frame.PitchDeg)
                || !IsFinite(frame.RollDeg)
                || !IsFinite(frame.Voltage24V)
                || !IsFinite(frame.Current24A)
                || !IsFinite(frame.BatteryPercent)
                || !IsFinite(frame.TargetSegment)
                || !IsFinite(frame.TargetHeadingDeg)
                || !IsFinite(frame.TargetDepthM)
                || !IsFinite(frame.TargetAltitudeM)
                || !IsFinite(frame.PropellerRpm)
                || !IsFinite(frame.PistonMm)
                || !IsFinite(frame.TurnAngleDeg)
                || !IsFiniteOrNaNPlanned(frame.PlannedLongitudeDeg, frame.PlannedLatitudeDeg))
            {
                return false;
            }

            if (frame.MissionState.HasValue)
            {
                var mission = frame.MissionState.Value;
                if ((int)mission.Phase < (int)SimulationMissionPhase.Surface
                    || (int)mission.Phase > (int)SimulationMissionPhase.LegTimeout
                    || mission.CompletedCycles < 0
                    || !IsFinite(mission.LegElapsedSeconds)
                    || !IsFinite(mission.TurnaroundElapsedSeconds))
                {
                    return false;
                }
            }

            if (!frame.Diagnostics.HasValue)
            {
                return true;
            }

            var diagnostics = frame.Diagnostics.Value;

            return IsFinite(diagnostics.WaterVelocityEndMps)
                && IsFinite(diagnostics.CurrentVelocityEndMps)
                && IsFinite(diagnostics.NetBuoyancyForceN)
                && IsFinite(diagnostics.EnergyWatts)
                && IsFinite(diagnostics.SideSlipDeg)
                && IsFinite(diagnostics.AngleOfAttackDeg)
                && IsFinite(diagnostics.LiftForceN)
                && IsFinite(diagnostics.DragForceN)
                && IsFinite(diagnostics.SideForceN)
                && IsFinite(diagnostics.AngularVelocityRadPerSecond)
                && IsFinite(diagnostics.HydrodynamicMomentNm)
                && IsFinite(diagnostics.PistonPositionMm)
                && IsFinite(diagnostics.ControlSurfaceDeflectionDeg)
                && IsFinite(diagnostics.ActuatorPowerWatts);
        }

        private static bool IsFinite(UnityEngine.Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
