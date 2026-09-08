using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class SimulationTrajectoryTimelineTests
    {
        [Test]
        public void ReplaceFutureCommitsOneSegmentAndPreservesHistory()
        {
            var history = new[] { Frame(0, 0f, 0), Frame(1, 5f, 0) };
            var timeline = new SimulationTrajectoryTimeline(
                history,
                new SimulationTimelineSegment(0, 0, 0, 0f, Profile(), DateTime.UtcNow));
            var future = new[] { Frame(2, 10f, 1), Frame(3, 15f, 1) };

            timeline.ReplaceFutureFrom(
                1,
                future,
                new SimulationTimelineSegment(1, 42, 2, 10f, Profile(), DateTime.UtcNow));

            Assert.That(timeline.CommittedSnapshot.Frames.Count, Is.EqualTo(4));
            Assert.That(timeline.CommittedSnapshot.Frames[0].ProfileSequence, Is.EqualTo(0));
            Assert.That(timeline.CommittedSnapshot.Frames[2].ProfileSequence, Is.EqualTo(1));
            Assert.That(timeline.CommittedSnapshot.Segments.Count, Is.EqualTo(2));
        }

        [Test]
        public void AppendFutureReusesCurrentProfileSequenceAndIncrementsRevision()
        {
            var timeline = TimelineWithFrames(4, profileSequence: 2);
            var revision = timeline.Revision;

            timeline.AppendFuture(new[] { Frame(4, 20f, 2) });

            Assert.That(timeline.CommittedSnapshot.Frames[4].ProfileSequence, Is.EqualTo(2));
            Assert.That(timeline.Revision, Is.EqualTo(revision + 1));
            Assert.That(timeline.CommittedSnapshot.Segments.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReplaceFutureRejectsInvalidFutureWithoutPublishingPartialState()
        {
            var timeline = TimelineWithFrames(3, profileSequence: 0);
            var originalSnapshot = timeline.CommittedSnapshot;
            var originalRevision = timeline.Revision;
            var invalidFuture = new[] { Frame(3, 10f, 0), Frame(2, 15f, 0) };

            Assert.Throws<ArgumentException>(() => timeline.ReplaceFutureFrom(
                1,
                invalidFuture,
                new SimulationTimelineSegment(0, 7, 3, 10f, Profile(), DateTime.UtcNow)));

            Assert.That(timeline.CommittedSnapshot, Is.SameAs(originalSnapshot));
            Assert.That(timeline.Revision, Is.EqualTo(originalRevision));
        }

        [Test]
        public void ReplaceFutureDropsSegmentsThatStartedInReplacedFuture()
        {
            var timeline = TimelineWithFrames(2, profileSequence: 0);
            timeline.ReplaceFutureFrom(
                1,
                new[] { Frame(2, 10f, 1), Frame(3, 15f, 1) },
                new SimulationTimelineSegment(1, 1, 2, 10f, Profile(), DateTime.UtcNow));

            timeline.ReplaceFutureFrom(
                1,
                new[] { Frame(2, 10f, 2) },
                new SimulationTimelineSegment(2, 2, 2, 10f, Profile(), DateTime.UtcNow));

            Assert.That(timeline.CommittedSnapshot.Segments.Count, Is.EqualTo(2));
            Assert.That(timeline.CommittedSnapshot.Segments[1].ProfileSequence, Is.EqualTo(2));
            Assert.That(timeline.CommittedSnapshot.Frames.Count, Is.EqualTo(3));
        }

        [Test]
        public void ConstructorRejectsEmptyTimelineWithArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new SimulationTrajectoryTimeline(
                Array.Empty<TelemetryFrame>(),
                new SimulationTimelineSegment(0, 0, 0, 0f, Profile(), DateTime.UtcNow)));
        }

        [Test]
        public void CommittedSnapshotCopiesInputListsIntoReadOnlyViews()
        {
            var frames = new List<TelemetryFrame> { Frame(0, 0f, 0) };
            var segments = new List<SimulationTimelineSegment>
            {
                new SimulationTimelineSegment(0, 0, 0, 0f, Profile(), DateTime.UtcNow)
            };

            var snapshot = new SimulationTimelineSnapshot(
                frames,
                segments,
                revision: 3,
                SimulationTimelineStatus.Committed);
            frames.Add(Frame(1, 5f, 0));
            segments.Clear();

            Assert.That(snapshot.Frames.Count, Is.EqualTo(1));
            Assert.That(snapshot.Segments.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<TelemetryFrame>)snapshot.Frames).Add(Frame(2, 10f, 0)));
        }

        [Test]
        public void LegacyTelemetryFrameConstructorDefaultsProfileSequenceToZero()
        {
            var frame = new TelemetryFrame(
                0, "t0", 0f, 120d, 25d, 0f, 100f, 0f, 0f, 0f,
                28f, 1f, 95f, "Simulation", "Running", 1f, 0f, 100f, 5f, 0f, 0f, 0f);

            Assert.That(frame.ProfileSequence, Is.EqualTo(0));
        }

        private static SimulationTrajectoryTimeline TimelineWithFrames(int frameCount, int profileSequence)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < frameCount; i++)
            {
                frames.Add(Frame(i, i * 5f, profileSequence));
            }

            return new SimulationTrajectoryTimeline(
                frames,
                new SimulationTimelineSegment(
                    profileSequence,
                    0,
                    0,
                    0f,
                    Profile(),
                    DateTime.UtcNow));
        }

        private static TelemetryFrame Frame(int rowIndex, float elapsedSeconds, int profileSequence)
        {
            return new TelemetryFrame(
                rowIndex,
                $"t{rowIndex}",
                elapsedSeconds,
                120d + rowIndex,
                25d + rowIndex,
                rowIndex,
                100f - rowIndex,
                10f + rowIndex,
                2f,
                3f,
                28f,
                1f,
                95f,
                "Simulation",
                "Running",
                1f,
                10f,
                100f,
                5f,
                0f,
                0f,
                0f,
                profileSequence: profileSequence);
        }

        private static SimulationProfile Profile()
        {
            return new SimulationProfile
            {
                CycleCount = 1,
                CycleDurationSeconds = 30f,
                SampleIntervalSeconds = 5f,
                TargetDepthM = 10f,
                WaterColumnDepthM = 20f
            };
        }
    }
}
