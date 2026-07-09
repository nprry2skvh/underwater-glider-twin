using System;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Playback
{
    public enum FrameUpdateReason
    {
        Initial,
        Playback,
        Seek
    }

    public sealed class PlaybackController : MonoBehaviour
    {
        private PlaybackModel model;

        public event Action<TelemetryFrame, int, float> FrameChanged;
        public event Action<TelemetryFrame, int, float, FrameUpdateReason> FrameChangedWithReason;

        public PlaybackModel Model => model;

        public void Initialize(PlaybackModel playbackModel)
        {
            model = playbackModel;
            Publish(FrameUpdateReason.Initial);
        }

        private void Update()
        {
            if (model != null && model.Tick(Time.deltaTime))
            {
                Publish(FrameUpdateReason.Playback);
            }
        }

        public void TogglePlaying()
        {
            model.TogglePlaying();
        }

        public void SetPlaying(bool playing)
        {
            model.SetPlaying(playing);
        }

        public void SetSpeed(float speed)
        {
            model.SetSpeed(speed);
        }

        public void Seek(float progress01)
        {
            model.SeekNormalized(progress01);
            Publish(FrameUpdateReason.Seek);
        }

        private void Publish(FrameUpdateReason reason)
        {
            FrameChanged?.Invoke(model.CurrentFrame, model.CurrentIndex, model.Progress01);
            FrameChangedWithReason?.Invoke(model.CurrentFrame, model.CurrentIndex, model.Progress01, reason);
        }
    }
}
