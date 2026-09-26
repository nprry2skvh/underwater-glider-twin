using System;
using System.Collections.Generic;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Playback
{
    public sealed class PlaybackModel
    {
        private IReadOnlyList<TelemetryFrame> frames;
        private readonly float rowsPerSecond;
        private float continuousIndex;
        private float continuousElapsedSeconds;
        private SimulationTrajectoryTimeline timeline;
        private bool resumeAfterFuture;

        public event Action<IReadOnlyList<TelemetryFrame>, int> FramesReplaced;
        public event Action<SimulationTimelineSnapshot, int> TimelineChanged;
        public event Action TimelineRebuildReady;

        public bool IsPlaying { get; private set; }
        public float Speed { get; private set; } = 1f;
        public int Direction { get; private set; } = 1;
        public int CurrentIndex { get; private set; }
        public float ContinuousIndex => continuousIndex;
        public int FrameCount => frames.Count;
        public IReadOnlyList<TelemetryFrame> Frames => frames;
        public float RowsPerSecond => rowsPerSecond;
        public float Progress01 => TotalElapsedSeconds <= 0.0001f
            ? 0f
            : Mathf.Clamp01((ContinuousElapsedSeconds - StartElapsedSeconds) / TotalElapsedSeconds);
        public TelemetryFrame CurrentFrame => frames[CurrentIndex];
        public float StartElapsedSeconds => frames[0].ElapsedSeconds;
        public float EndElapsedSeconds => frames[frames.Count - 1].ElapsedSeconds;
        public float TotalElapsedSeconds => Math.Max(0f, EndElapsedSeconds - StartElapsedSeconds);
        public float CurrentElapsedSeconds => CurrentFrame.ElapsedSeconds;
        public bool IsWaitingForFuture { get; private set; }
        public bool IsTimelineBound => timeline != null;
        public SimulationTrajectoryTimeline Timeline => timeline;
        public float ContinuousElapsedSeconds => continuousElapsedSeconds;

        public PlaybackModel(IReadOnlyList<TelemetryFrame> frames, float rowsPerSecond)
        {
            this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
            if (frames.Count == 0)
            {
                throw new ArgumentException("Playback requires at least one frame.", nameof(frames));
            }

            this.rowsPerSecond = rowsPerSecond;
            continuousElapsedSeconds = frames[0].ElapsedSeconds;
        }

        public void SetPlaying(bool isPlaying)
        {
            IsPlaying = isPlaying;
            if (!isPlaying)
            {
                resumeAfterFuture = false;
            }
        }

        public void TogglePlaying()
        {
            IsPlaying = !IsPlaying;
        }

        public void SetDirection(int direction)
        {
            Direction = direction < 0 ? -1 : 1;
        }

        public void SetSpeed(float speed)
        {
            Speed = Math.Max(0.1f, speed);
        }

        public bool Tick(float deltaSeconds)
        {
            if (!IsPlaying)
            {
                return false;
            }

            var previousContinuousIndex = ContinuousIndex;
            var previousContinuousElapsed = ContinuousElapsedSeconds;
            continuousIndex += rowsPerSecond * Speed * deltaSeconds * Direction;
            continuousIndex = Math.Max(0f, Math.Min(frames.Count - 1, continuousIndex));
            continuousElapsedSeconds = InterpolateElapsedSeconds(continuousIndex);
            var nextIndex = Direction >= 0
                ? Math.Min(frames.Count - 1, (int)Math.Floor(continuousIndex))
                : Math.Max(0, (int)Math.Ceiling(continuousIndex));
            var changed = nextIndex != CurrentIndex;
            CurrentIndex = nextIndex;

            if ((Direction >= 0 && CurrentIndex >= frames.Count - 1) || (Direction < 0 && CurrentIndex <= 0))
            {
                if (Direction >= 0
                    && timeline != null
                    && timeline.CommittedSnapshot.Status != SimulationTimelineStatus.Completed)
                {
                    resumeAfterFuture = IsPlaying;
                    IsPlaying = false;
                    IsWaitingForFuture = true;
                }
                else
                {
                    IsPlaying = false;
                }
            }

            return !Mathf.Approximately(previousContinuousIndex, ContinuousIndex)
                || !Mathf.Approximately(previousContinuousElapsed, ContinuousElapsedSeconds)
                || changed;
        }

        public void SeekNormalized(float progress01)
        {
            var clamped = Math.Max(0f, Math.Min(1f, progress01));
            CurrentIndex = (int)Math.Round(clamped * (frames.Count - 1));
            continuousIndex = CurrentIndex;
            continuousElapsedSeconds = InterpolateElapsedSeconds(continuousIndex);
        }

        public TelemetryFrame GetFrame(int index)
        {
            return frames[Math.Max(0, Math.Min(frames.Count - 1, index))];
        }

        public void Restart(bool playImmediately)
        {
            Direction = 1;
            SeekNormalized(0f);
            IsPlaying = playImmediately;
        }

        public void BindTimeline(SimulationTrajectoryTimeline replacement, bool notify = false)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (timeline != null)
            {
                timeline.Changed -= OnTimelineChanged;
            }

            timeline = replacement;
            ApplySnapshot(replacement.CommittedSnapshot);
            timeline.Changed += OnTimelineChanged;
            if (notify)
            {
                NotifyTimelineChanged(replacement.CommittedSnapshot);
                NotifyFramesReplaced();
                TimelineRebuildReady?.Invoke();
            }
        }

        public bool HasFutureHorizon(float seconds, int minimumFrames)
        {
            if (seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            if (minimumFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumFrames));
            }

            var remainingFrames = frames.Count - CurrentIndex - 1;
            var remainingSeconds = EndElapsedSeconds - ContinuousElapsedSeconds;
            return remainingFrames >= minimumFrames && remainingSeconds >= seconds;
        }

        public void ReplaceFrames(IReadOnlyList<TelemetryFrame> replacement, int preservedIndex)
        {
            if (timeline != null)
            {
                throw new InvalidOperationException(
                    "A timeline-bound PlaybackModel can only receive committed frames from its timeline.");
            }

            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (replacement.Count == 0)
            {
                throw new ArgumentException("Playback requires at least one frame.", nameof(replacement));
            }

            if (preservedIndex < 0 || preservedIndex >= frames.Count || preservedIndex >= replacement.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(preservedIndex));
            }

            for (var i = 0; i <= preservedIndex; i++)
            {
                if (!FrameValuesEqual(frames[i], replacement[i]))
                {
                    throw new ArgumentException(
                        $"Replacement changed historical frame {i}.",
                        nameof(replacement));
                }
            }

            frames = replacement;
            CurrentIndex = preservedIndex;
            continuousIndex = Math.Max(preservedIndex, Math.Min(replacement.Count - 1, continuousIndex));
            continuousElapsedSeconds = InterpolateElapsedSeconds(continuousIndex);
            NotifyFramesReplaced();
        }

        private float InterpolateElapsedSeconds(float index)
        {
            if (frames.Count == 0)
            {
                return 0f;
            }

            if (frames.Count == 1)
            {
                return frames[0].ElapsedSeconds;
            }

            var clamped = Mathf.Clamp(index, 0f, frames.Count - 1f);
            var lower = Mathf.Clamp(Mathf.FloorToInt(clamped), 0, frames.Count - 1);
            var upper = Mathf.Clamp(lower + 1, 0, frames.Count - 1);
            if (lower == upper)
            {
                return frames[lower].ElapsedSeconds;
            }

            return Mathf.Lerp(frames[lower].ElapsedSeconds, frames[upper].ElapsedSeconds, clamped - lower);
        }

        private float FindContinuousIndex(float elapsedSeconds)
        {
            if (frames.Count <= 1) return 0f;
            if (elapsedSeconds <= frames[0].ElapsedSeconds) return 0f;
            if (elapsedSeconds >= frames[frames.Count - 1].ElapsedSeconds) return frames.Count - 1f;

            var low = 0;
            var high = frames.Count - 1;
            while (low + 1 < high)
            {
                var middle = (low + high) / 2;
                if (frames[middle].ElapsedSeconds <= elapsedSeconds) low = middle;
                else high = middle;
            }

            var delta = frames[high].ElapsedSeconds - frames[low].ElapsedSeconds;
            var amount = delta <= 0.0001f ? 0f : (elapsedSeconds - frames[low].ElapsedSeconds) / delta;
            return low + Mathf.Clamp01(amount);
        }

        private void NotifyFramesReplaced()
        {
            var handlers = FramesReplaced;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<IReadOnlyList<TelemetryFrame>, int> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(frames, CurrentIndex);
                }
                catch (Exception)
                {
                    // A presentation listener must not roll back the committed playback transaction.
                }
            }
        }

        private void OnTimelineChanged(SimulationTimelineSnapshot snapshot)
        {
            ApplySnapshot(snapshot);
            NotifyTimelineChanged(snapshot);
            NotifyFramesReplaced();
            TimelineRebuildReady?.Invoke();
        }

        private void ApplySnapshot(SimulationTimelineSnapshot snapshot)
        {
            frames = snapshot.Frames;
            continuousElapsedSeconds = Mathf.Clamp(continuousElapsedSeconds, StartElapsedSeconds, EndElapsedSeconds);
            continuousIndex = FindContinuousIndex(continuousElapsedSeconds);
            continuousElapsedSeconds = InterpolateElapsedSeconds(continuousIndex);
            CurrentIndex = Direction >= 0
                ? Mathf.Clamp(Mathf.FloorToInt(continuousIndex), 0, frames.Count - 1)
                : Mathf.Clamp(Mathf.CeilToInt(continuousIndex), 0, frames.Count - 1);

            if (IsWaitingForFuture && CurrentIndex < frames.Count - 1)
            {
                IsWaitingForFuture = false;
                IsPlaying = resumeAfterFuture;
                resumeAfterFuture = false;
            }
        }

        private void NotifyTimelineChanged(SimulationTimelineSnapshot snapshot)
        {
            var handlers = TimelineChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<SimulationTimelineSnapshot, int> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(snapshot, CurrentIndex);
                }
                catch (Exception)
                {
                    // A presentation listener must not roll back the committed playback transaction.
                }
            }
        }

        private static bool FrameValuesEqual(TelemetryFrame left, TelemetryFrame right)
        {
            return left.RowIndex == right.RowIndex
                && left.RawTime == right.RawTime
                && left.ElapsedSeconds.Equals(right.ElapsedSeconds)
                && left.LongitudeDeg.Equals(right.LongitudeDeg)
                && left.LatitudeDeg.Equals(right.LatitudeDeg)
                && left.DepthM.Equals(right.DepthM)
                && left.AltitudeM.Equals(right.AltitudeM)
                && left.HeadingDeg.Equals(right.HeadingDeg)
                && left.PitchDeg.Equals(right.PitchDeg)
                && left.RollDeg.Equals(right.RollDeg)
                && left.Voltage24V.Equals(right.Voltage24V)
                && left.Current24A.Equals(right.Current24A)
                && left.BatteryPercent.Equals(right.BatteryPercent)
                && left.WorkMode == right.WorkMode
                && left.RunState == right.RunState
                && left.TargetSegment.Equals(right.TargetSegment)
                && left.TargetHeadingDeg.Equals(right.TargetHeadingDeg)
                && left.TargetDepthM.Equals(right.TargetDepthM)
                && left.TargetAltitudeM.Equals(right.TargetAltitudeM)
                && left.PropellerRpm.Equals(right.PropellerRpm)
                && left.PistonMm.Equals(right.PistonMm)
                && left.TurnAngleDeg.Equals(right.TurnAngleDeg)
                && Nullable.Equals(left.Diagnostics, right.Diagnostics)
                && left.PlannedLongitudeDeg.Equals(right.PlannedLongitudeDeg)
                && left.PlannedLatitudeDeg.Equals(right.PlannedLatitudeDeg)
                && Nullable.Equals(left.MissionState, right.MissionState)
                && left.ProfileSequence == right.ProfileSequence;
        }
    }
}
