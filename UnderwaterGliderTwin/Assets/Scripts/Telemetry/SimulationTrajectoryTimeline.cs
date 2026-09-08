using System;
using System.Collections.Generic;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationTrajectoryTimeline
    {
        private SimulationTimelineSnapshot committedSnapshot;

        public event Action<SimulationTimelineSnapshot> Changed;

        public SimulationTimelineSnapshot CommittedSnapshot => committedSnapshot;
        public int Revision => committedSnapshot.Revision;

        public SimulationTrajectoryTimeline(
            IReadOnlyList<TelemetryFrame> frames,
            SimulationTimelineSegment initialSegment)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (initialSegment == null)
            {
                throw new ArgumentNullException(nameof(initialSegment));
            }

            var initialFrames = new List<TelemetryFrame>(frames);
            ValidateFrames(initialFrames, 0, default(TelemetryFrame), false);
            ValidateInitialSegment(initialFrames, initialSegment);
            committedSnapshot = new SimulationTimelineSnapshot(
                initialFrames,
                new[] { initialSegment },
                revision: 0,
                status: SimulationTimelineStatus.Committed,
                missionState: initialFrames[initialFrames.Count - 1].MissionState);
        }

        public void ReplaceFutureFrom(
            int preservedIndex,
            IReadOnlyList<TelemetryFrame> future,
            SimulationTimelineSegment segment)
        {
            if (future == null)
            {
                throw new ArgumentNullException(nameof(future));
            }

            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            var currentFrames = committedSnapshot.Frames;
            if (preservedIndex < 0 || preservedIndex >= currentFrames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(preservedIndex));
            }

            var nextFrames = new List<TelemetryFrame>(preservedIndex + 1 + future.Count);
            for (var i = 0; i <= preservedIndex; i++)
            {
                nextFrames.Add(currentFrames[i]);
            }

            ValidateFrames(future, 0, nextFrames[preservedIndex], true);
            ValidateSegmentStart(future[0], segment);
            ValidateFutureProfileSequence(future, segment.ProfileSequence);
            nextFrames.AddRange(future);

            var preservedRowIndex = currentFrames[preservedIndex].RowIndex;
            var nextSegments = new List<SimulationTimelineSegment>();
            for (var i = 0; i < committedSnapshot.Segments.Count; i++)
            {
                var existingSegment = committedSnapshot.Segments[i];
                if (existingSegment.StartRowIndex <= preservedRowIndex)
                {
                    nextSegments.Add(existingSegment);
                }
            }

            if (nextSegments.Count > 0
                && segment.ProfileSequence <= nextSegments[nextSegments.Count - 1].ProfileSequence)
            {
                throw new ArgumentException(
                    "A replacement segment must advance ProfileSequence beyond preserved segments.",
                    nameof(segment));
            }

            nextSegments.Add(segment);
            Publish(nextFrames, nextSegments);
        }

        public void AppendFuture(IReadOnlyList<TelemetryFrame> future)
        {
            if (future == null)
            {
                throw new ArgumentNullException(nameof(future));
            }

            var currentFrames = committedSnapshot.Frames;
            var profileSequence = currentFrames[currentFrames.Count - 1].ProfileSequence;
            ValidateFrames(future, 0, currentFrames[currentFrames.Count - 1], true);
            ValidateFutureProfileSequence(future, profileSequence);

            var nextFrames = new List<TelemetryFrame>(currentFrames.Count + future.Count);
            nextFrames.AddRange(currentFrames);
            nextFrames.AddRange(future);
            Publish(nextFrames, committedSnapshot.Segments);
        }

        private void Publish(
            IReadOnlyList<TelemetryFrame> frames,
            IReadOnlyList<SimulationTimelineSegment> segments)
        {
            var snapshot = new SimulationTimelineSnapshot(
                frames,
                segments,
                committedSnapshot.Revision + 1,
                SimulationTimelineStatus.Committed,
                frames[frames.Count - 1].MissionState);
            committedSnapshot = snapshot;
            NotifyChanged(snapshot);
        }

        private void NotifyChanged(SimulationTimelineSnapshot snapshot)
        {
            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<SimulationTimelineSnapshot> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(snapshot);
                }
                catch (Exception)
                {
                    // A subscriber cannot undo an already-committed timeline publication.
                }
            }
        }

        private static void ValidateInitialSegment(
            IReadOnlyList<TelemetryFrame> frames,
            SimulationTimelineSegment segment)
        {
            ValidateSegmentStart(frames[0], segment);
            for (var i = 0; i < frames.Count; i++)
            {
                if (frames[i].ProfileSequence != segment.ProfileSequence)
                {
                    throw new ArgumentException("Initial frames must use the initial segment ProfileSequence.", nameof(frames));
                }
            }
        }

        private static void ValidateSegmentStart(TelemetryFrame firstFutureFrame, SimulationTimelineSegment segment)
        {
            if (firstFutureFrame.RowIndex != segment.StartRowIndex
                || !firstFutureFrame.ElapsedSeconds.Equals(segment.StartElapsedSeconds))
            {
                throw new ArgumentException(
                    "The segment start must match the first future frame.",
                    nameof(segment));
            }
        }

        private static void ValidateFutureProfileSequence(
            IReadOnlyList<TelemetryFrame> future,
            int profileSequence)
        {
            for (var i = 0; i < future.Count; i++)
            {
                if (future[i].ProfileSequence != profileSequence)
                {
                    throw new ArgumentException(
                        "Future frames must use the committed ProfileSequence.",
                        nameof(future));
                }
            }
        }

        private static void ValidateFrames(
            IReadOnlyList<TelemetryFrame> frames,
            int startIndex,
            TelemetryFrame precedingFrame,
            bool hasPrecedingFrame)
        {
            if (frames.Count == 0)
            {
                throw new ArgumentException("Timeline frames cannot be empty.", nameof(frames));
            }

            var previous = precedingFrame;
            var hasPrevious = hasPrecedingFrame;
            for (var i = startIndex; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (hasPrevious
                    && (frame.RowIndex <= previous.RowIndex
                        || frame.ElapsedSeconds <= previous.ElapsedSeconds
                        || frame.ProfileSequence < previous.ProfileSequence))
                {
                    throw new ArgumentException(
                        "Timeline RowIndex and ElapsedSeconds must increase while ProfileSequence never decreases.",
                        nameof(frames));
                }

                previous = frame;
                hasPrevious = true;
            }
        }
    }
}
