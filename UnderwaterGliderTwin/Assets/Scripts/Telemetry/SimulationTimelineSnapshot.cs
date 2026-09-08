using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationTimelineSnapshot
    {
        public IReadOnlyList<TelemetryFrame> Frames { get; }
        public IReadOnlyList<SimulationTimelineSegment> Segments { get; }
        public int Revision { get; }
        public SimulationTimelineStatus Status { get; }
        public SimulationMissionState? MissionState { get; }
        public TrajectoryPlaybackState Playback { get; }

        public SimulationTimelineSnapshot(
            IReadOnlyList<TelemetryFrame> frames,
            IReadOnlyList<SimulationTimelineSegment> segments,
            int revision,
            SimulationTimelineStatus status,
            SimulationMissionState? missionState = null,
            TrajectoryPlaybackState playback = null)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            Frames = new ReadOnlyCollection<TelemetryFrame>(new List<TelemetryFrame>(frames));
            Segments = new ReadOnlyCollection<SimulationTimelineSegment>(
                new List<SimulationTimelineSegment>(segments));
            Revision = revision;
            Status = status;
            MissionState = missionState;
            Playback = playback;
        }
    }

    public sealed class TrajectoryPlaybackState
    {
        public int CurrentFrameIndex { get; }
        public float ContinuousIndex { get; }
        public float CurrentElapsedSeconds { get; }
        public bool IsPlaying { get; }
        public float Speed { get; }
        public int Direction { get; }

        public TrajectoryPlaybackState(
            int currentFrameIndex,
            float continuousIndex,
            float currentElapsedSeconds,
            bool isPlaying,
            float speed,
            int direction)
        {
            CurrentFrameIndex = currentFrameIndex;
            ContinuousIndex = continuousIndex;
            CurrentElapsedSeconds = currentElapsedSeconds;
            IsPlaying = isPlaying;
            Speed = speed;
            Direction = direction < 0 ? -1 : 1;
        }
    }
}
