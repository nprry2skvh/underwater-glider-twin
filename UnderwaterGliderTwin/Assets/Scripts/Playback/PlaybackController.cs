using System;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Playback
{
    public sealed class PlaybackController : MonoBehaviour
    {
        private PlaybackModel model;

        public event Action<TelemetryFrame, int, float> FrameChanged;

        public PlaybackModel Model => model;

        public void Initialize(PlaybackModel playbackModel)
        {
            model = playbackModel;
            Publish();
        }

        private void Update()
        {
            if (model != null && model.Tick(Time.deltaTime))
            {
                Publish();
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
            Publish();
        }

        private void Publish()
        {
            FrameChanged?.Invoke(model.CurrentFrame, model.CurrentIndex, model.Progress01);
        }
    }
}
