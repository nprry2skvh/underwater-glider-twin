using System;
using System.Collections.Generic;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Playback
{
    public enum FrameUpdateReason
    {
        Initial,
        Playback,
        Seek,
        Rebuild
    }

    public sealed class PlaybackController : MonoBehaviour
    {
        private PlaybackModel model;

        public event Action<TelemetryFrame, int, float> FrameChanged;
        public event Action<TelemetryFrame, int, float, FrameUpdateReason> FrameChangedWithReason;
        public event Action<SimulationTimelineSnapshot> TimelineChanged;

        public PlaybackModel Model => model;

        public void Initialize(PlaybackModel playbackModel)
        {
            if (model != null)
            {
                model.FramesReplaced -= OnFramesReplaced;
                model.TimelineChanged -= OnTimelineChanged;
                model.TimelineRebuildReady -= OnTimelineRebuildReady;
            }

            model = playbackModel;
            model.FramesReplaced += OnFramesReplaced;
            model.TimelineChanged += OnTimelineChanged;
            model.TimelineRebuildReady += OnTimelineRebuildReady;
            Publish(FrameUpdateReason.Initial);
        }

        private void Update()
        {
            Step(Time.deltaTime);
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

        public void PlayReverse()
        {
            model.SetDirection(-1);
            model.SetPlaying(true);
        }

        public void PlayForward()
        {
            model.SetDirection(1);
            model.SetPlaying(true);
        }

        public void Seek(float progress01)
        {
            model.SeekNormalized(progress01);
            Publish(FrameUpdateReason.Seek);
        }

        public void Restart(bool playImmediately)
        {
            model.Restart(playImmediately);
            Publish(FrameUpdateReason.Seek);
        }

        public void Step(float deltaSeconds)
        {
            if (model != null && model.Tick(deltaSeconds))
            {
                Publish(FrameUpdateReason.Playback);
            }
        }

        private void OnDestroy()
        {
            if (model != null)
            {
                model.FramesReplaced -= OnFramesReplaced;
                model.TimelineChanged -= OnTimelineChanged;
                model.TimelineRebuildReady -= OnTimelineRebuildReady;
            }
        }

        private void OnFramesReplaced(IReadOnlyList<TelemetryFrame> frames, int preservedIndex)
        {
            if (!model.IsTimelineBound)
            {
                Publish(FrameUpdateReason.Rebuild);
            }
        }

        private void OnTimelineChanged(SimulationTimelineSnapshot snapshot, int preservedIndex)
        {
            TimelineChanged?.Invoke(snapshot);
        }

        private void OnTimelineRebuildReady()
        {
            Publish(FrameUpdateReason.Rebuild);
        }

        private void Publish(FrameUpdateReason reason)
        {
            FrameChangedWithReason?.Invoke(model.CurrentFrame, model.CurrentIndex, model.Progress01, reason);
            FrameChanged?.Invoke(model.CurrentFrame, model.CurrentIndex, model.Progress01);
        }
    }
}
