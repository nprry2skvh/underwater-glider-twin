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
        public int CurrentIndex { get; private set; }
        public int FrameCount => frames.Count;
        public float Progress01 => frames.Count <= 1 ? 0f : CurrentIndex / (float)(frames.Count - 1);
        public TelemetryFrame CurrentFrame => frames[CurrentIndex];

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

            continuousIndex += rowsPerSecond * Speed * deltaSeconds;
            var nextIndex = Math.Min(frames.Count - 1, (int)continuousIndex);
            var changed = nextIndex != CurrentIndex;
            CurrentIndex = nextIndex;

            if (CurrentIndex >= frames.Count - 1)
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
    }
}
