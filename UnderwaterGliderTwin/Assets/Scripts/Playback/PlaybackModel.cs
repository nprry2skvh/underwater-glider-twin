using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Playback
{
    public sealed class PlaybackModel
    {
        private readonly IReadOnlyList<TelemetryFrame> frames;
        private readonly float rowsPerSecond;
        private float continuousIndex;

        public bool IsPlaying { get; private set; }
        public float Speed { get; private set; } = 1f;
        public int Direction { get; private set; } = 1;
        public int CurrentIndex { get; private set; }
        public int FrameCount => frames.Count;
        public IReadOnlyList<TelemetryFrame> Frames => frames;
        public float RowsPerSecond => rowsPerSecond;
        public float Progress01 => frames.Count <= 1 ? 0f : CurrentIndex / (float)(frames.Count - 1);
        public TelemetryFrame CurrentFrame => frames[CurrentIndex];
        public float StartElapsedSeconds => frames[0].ElapsedSeconds;
        public float EndElapsedSeconds => frames[frames.Count - 1].ElapsedSeconds;
        public float TotalElapsedSeconds => Math.Max(0f, EndElapsedSeconds - StartElapsedSeconds);
        public float CurrentElapsedSeconds => CurrentFrame.ElapsedSeconds;

        public PlaybackModel(IReadOnlyList<TelemetryFrame> frames, float rowsPerSecond)
        {
            this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
            if (frames.Count == 0)
            {
                throw new ArgumentException("Playback requires at least one frame.", nameof(frames));
            }

            this.rowsPerSecond = rowsPerSecond;
        }

        public void SetPlaying(bool isPlaying)
        {
            IsPlaying = isPlaying;
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

            continuousIndex += rowsPerSecond * Speed * deltaSeconds * Direction;
            continuousIndex = Math.Max(0f, Math.Min(frames.Count - 1, continuousIndex));
            var nextIndex = Direction >= 0
                ? Math.Min(frames.Count - 1, (int)Math.Floor(continuousIndex))
                : Math.Max(0, (int)Math.Ceiling(continuousIndex));
            var changed = nextIndex != CurrentIndex;
            CurrentIndex = nextIndex;

            if ((Direction >= 0 && CurrentIndex >= frames.Count - 1) || (Direction < 0 && CurrentIndex <= 0))
            {
                IsPlaying = false;
            }

            return changed;
        }

        public void SeekNormalized(float progress01)
        {
            var clamped = Math.Max(0f, Math.Min(1f, progress01));
            CurrentIndex = (int)Math.Round(clamped * (frames.Count - 1));
            continuousIndex = CurrentIndex;
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
    }
}
