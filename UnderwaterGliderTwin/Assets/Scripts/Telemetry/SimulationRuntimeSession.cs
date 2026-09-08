using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Playback;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationRuntimeSession
    {
        private const double EstimatedSecondsPerFutureSlice = 0.1d;
        private static readonly TimeSpan MaximumPendingTimeout = TimeSpan.FromMinutes(5);

        private readonly PlaybackModel playback;
        private readonly ISimulationFutureGenerator generator;
        private readonly int frameSliceBudget;
        private readonly TimeSpan timeout;
        private readonly Func<DateTime> utcNow;
        private SimulationProfile activeProfile;
        private ISimulationRebuildOperation pendingOperation;
        private DateTime pendingDeadline;
        private int requestVersion;
        private int pendingSeedRowIndex;
        private float pendingSeedElapsedSeconds;

        public event Action StatusChanged;

        public bool IsRebuildPending { get; private set; }
        public string LastError { get; private set; }
        public SimulationProfile ActiveProfile => activeProfile.Clone();

        public SimulationRuntimeSession(
            PlaybackModel playback,
            SimulationProfile activeProfile,
            ISimulationFutureGenerator generator,
            int frameSliceBudget = 128,
            TimeSpan? timeout = null,
            Func<DateTime> utcNow = null)
        {
            this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.activeProfile = (activeProfile ?? SimulationProfile.Default).Clone();
            this.frameSliceBudget = Math.Max(1, frameSliceBudget);
            this.timeout = timeout ?? TimeSpan.FromSeconds(30);
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public bool RequestProfileUpdate(SimulationProfile candidate)
        {
            if (IsRebuildPending)
            {
                SetError("A simulation rebuild is already pending.");
                return false;
            }

            if (!TryValidateProfile(candidate, out var profileError))
            {
                SetError(profileError);
                return false;
            }

            var candidateSnapshot = candidate.Clone();
            var version = ++requestVersion;
            LastError = null;
            IsRebuildPending = true;
            pendingDeadline = utcNow().Add(CalculatePendingTimeout(candidateSnapshot));
            StatusChanged?.Invoke();

            try
            {
                StartGeneration(version, candidateSnapshot);
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
            FailRequest(requestVersion, "Simulation rebuild was cancelled.");
        }

        public void Tick()
        {
            if (IsRebuildPending && utcNow() >= pendingDeadline)
            {
                pendingOperation?.Cancel();
                FailRequest(requestVersion, "Simulation rebuild timed out.");
            }
        }

        private void CompleteRequest(
            int version,
            SimulationProfile candidate,
            SimulationRebuildResult result)
        {
            if (!IsRebuildPending || version != requestVersion)
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
                        : error);
                return;
            }

            try
            {
                if (playback.CurrentFrame.RowIndex != pendingSeedRowIndex
                    || Math.Abs(playback.CurrentFrame.ElapsedSeconds - pendingSeedElapsedSeconds) > 0.0001f)
                {
                    pendingOperation = null;
                    StartGeneration(++requestVersion, candidate);
                    return;
                }

                var preservedIndex = playback.CurrentIndex;
                if (!TryValidateGeneratedFuture(result.Frames, out var futureError))
                {
                    FailRequest(version, futureError);
                    return;
                }

                var stagedFrames = BuildStagingFrames(result.Frames, preservedIndex);
                if (!TryValidateStaging(stagedFrames, preservedIndex, candidate, out var validationError))
                {
                    FailRequest(version, validationError);
                    return;
                }

                playback.ReplaceFrames(stagedFrames, preservedIndex);
                activeProfile = candidate.Clone();
                pendingOperation = null;
                IsRebuildPending = false;
                LastError = null;
                StatusChanged?.Invoke();
            }
            catch (Exception ex)
            {
                FailRequest(version, "Simulation rebuild failed: " + ex.Message);
            }
        }

        private void StartGeneration(int version, SimulationProfile candidate)
        {
            var seedFrame = playback.CurrentFrame;
            pendingSeedRowIndex = seedFrame.RowIndex;
            pendingSeedElapsedSeconds = seedFrame.ElapsedSeconds;
            var seed = SimulationStateSnapshot.FromFrame(seedFrame, activeProfile);
            var operation = generator.GenerateFuture(
                seed,
                candidate,
                frameSliceBudget,
                result => CompleteRequest(version, candidate, result));
            if (IsRebuildPending && version == requestVersion)
            {
                pendingOperation = operation;
            }
            else
            {
                operation?.Cancel();
            }
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

        private static bool TryValidateProfile(SimulationProfile profile, out string error)
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

            error = null;
            return true;
        }

        private void FailRequest(int version, string error)
        {
            if (!IsRebuildPending || version != requestVersion)
            {
                return;
            }

            pendingOperation = null;
            IsRebuildPending = false;
            LastError = error;
            StatusChanged?.Invoke();
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
