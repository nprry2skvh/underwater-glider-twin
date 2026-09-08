using System;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationTimelineSegment
    {
        private readonly SimulationProfile profile;

        public int ProfileSequence { get; }
        public long RequestId { get; }
        public int StartRowIndex { get; }
        public float StartElapsedSeconds { get; }
        public SimulationProfile Profile => profile.Clone();
        public DateTime CommittedAtUtc { get; }

        public SimulationTimelineSegment(
            int profileSequence,
            long requestId,
            int startRowIndex,
            float startElapsedSeconds,
            SimulationProfile profile,
            DateTime committedAtUtc)
        {
            if (profileSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(profileSequence));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            ProfileSequence = profileSequence;
            RequestId = requestId;
            StartRowIndex = startRowIndex;
            StartElapsedSeconds = startElapsedSeconds;
            this.profile = profile.Clone();
            CommittedAtUtc = committedAtUtc.Kind == DateTimeKind.Utc
                ? committedAtUtc
                : committedAtUtc.ToUniversalTime();
        }
    }
}
