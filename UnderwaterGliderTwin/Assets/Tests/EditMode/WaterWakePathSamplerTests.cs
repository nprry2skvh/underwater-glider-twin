using System.Collections.Generic;
using NUnit.Framework;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class WaterWakePathSamplerTests
    {
        [Test]
        public void Fill_UsesPlaybackHistoryOnTheCorrectSideOfCurrentTime()
        {
            var trajectory = StraightTrajectory(0f, 10f, 1f);
            var frames = StraightFrames(0, 10, 1f);
            var mapper = CreateMapper(frames[0]);
            var forward = new WaterWakePathSample[4];
            var reverse = new WaterWakePathSample[4];

            var forwardCount = WaterWakePathSampler.Fill(trajectory, frames, mapper, 5f, 1, 3f, forward);
            var reverseCount = WaterWakePathSampler.Fill(trajectory, frames, mapper, 5f, -1, 3f, reverse);

            Assert.That(forwardCount, Is.EqualTo(4));
            Assert.That(reverseCount, Is.EqualTo(4));
            Assert.That(forward[0].PositionXZ.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(forward[3].PositionXZ.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(forward[3].AgeSeconds, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(reverse[0].PositionXZ.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(reverse[3].PositionXZ.x, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(reverse[3].AgeSeconds, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Fill_DistributesSamplesByHorizontalDistanceOnCurvedNonuniformTrack()
        {
            var trajectory = new FittedTrajectory(
                new[] { 0f, 1f, 3f },
                new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) });
            var frames = new List<TelemetryFrame>
            {
                CreateFrame(0f, 10f),
                CreateFrame(1f, 20f),
                CreateFrame(3f, 40f)
            };
            var samples = new WaterWakePathSample[3];

            var count = WaterWakePathSampler.Fill(
                trajectory, frames, CreateMapper(frames[0]), 3f, 1, 5f, samples);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(Vector2.Distance(samples[0].PositionXZ, new Vector2(3f, 4f)), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(samples[1].PositionXZ, new Vector2(3f, 1.5f)), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(samples[2].PositionXZ, new Vector2(2f, 0f)), Is.LessThan(0.0001f));
            Assert.That(samples[1].AgeSeconds, Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void HorizontalDistanceAt_InterpolatesCumulativeDistanceAcrossTurns()
        {
            var trajectory = new FittedTrajectory(
                new[] { 0f, 2f, 6f },
                new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) });

            Assert.That(trajectory.HorizontalDistanceAt(-1f), Is.Zero);
            Assert.That(trajectory.HorizontalDistanceAt(1f), Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(trajectory.HorizontalDistanceAt(4f), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(trajectory.HorizontalDistanceAt(8f), Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void HorizontalMovingDurationBetween_ExcludesStationaryTime()
        {
            var trajectory = new FittedTrajectory(
                new[] { 0f, 10f, 1010f, 1020f },
                new[]
                {
                    Vector3.zero,
                    new Vector3(10f, 0f, 0f),
                    new Vector3(10f, 0f, 0f),
                    new Vector3(20f, 0f, 0f)
                });

            Assert.That(trajectory.HorizontalMovingDurationBetween(0f, 1020f), Is.EqualTo(20f).Within(0.0001f));
            Assert.That(trajectory.HorizontalMovingDurationBetween(5f, 1015f), Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void Fill_UsesStableAbsoluteDistanceForForwardReverseAndSeek()
        {
            var trajectory = StraightTrajectory(0f, 10f, 1f);
            var frames = StraightFrames(0, 10, 1f);
            var mapper = CreateMapper(frames[0]);
            var early = new WaterWakePathSample[4];
            var late = new WaterWakePathSample[4];
            var reverse = new WaterWakePathSample[4];

            WaterWakePathSampler.Fill(trajectory, frames, mapper, 5f, 1, 3f, early);
            WaterWakePathSampler.Fill(trajectory, frames, mapper, 6f, 1, 3f, late);
            WaterWakePathSampler.Fill(trajectory, frames, mapper, 5f, -1, 3f, reverse);

            Assert.That(early[0].DistanceAlongPath, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(early[2].DistanceAlongPath, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(late[3].DistanceAlongPath, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(reverse[2].DistanceAlongPath, Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void FillAnchored_KeepsPacketLocationsOnTheFittedTurnAcrossSeekAndReverse()
        {
            var trajectory = new FittedTrajectory(
                new[] { 0f, 3f, 7f },
                new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) });
            var frames = StraightFrames(0, 7, 1f);
            var mapper = CreateMapper(frames[0]);
            var samples = new WaterWakePathSample[WaterWakePathSampler.MaximumSampleCount];

            var count = WaterWakePathSampler.FillAnchored(
                trajectory, frames, mapper, 7f, 1, 5f, 2f, samples);
            AssertPacket(samples, count, 2f, new Vector2(2f, 0f));
            AssertPacket(samples, count, 4f, new Vector2(3f, 1f));

            count = WaterWakePathSampler.FillAnchored(
                trajectory, frames, mapper, 6.5f, 1, 5f, 2f, samples);
            AssertPacket(samples, count, 2f, new Vector2(2f, 0f));
            AssertPacket(samples, count, 4f, new Vector2(3f, 1f));

            count = WaterWakePathSampler.FillAnchored(
                trajectory, frames, mapper, 1f, -1, 5f, 2f, samples);
            AssertPacket(samples, count, 2f, new Vector2(2f, 0f));
            AssertPacket(samples, count, 4f, new Vector2(3f, 1f));
        }

        [Test]
        public void FillAnchored_PreservesSharpTurnBetweenPacketTicks()
        {
            var trajectory = new FittedTrajectory(
                new[] { 0f, 2.5f, 5.5f },
                new[] { Vector3.zero, new Vector3(2.5f, 0f, 0f), new Vector3(2.5f, 0f, 3f) });
            var frames = StraightFrames(0, 6, 1f);
            var samples = new WaterWakePathSample[WaterWakePathSampler.MaximumSampleCount];

            var count = WaterWakePathSampler.FillAnchored(
                trajectory, frames, CreateMapper(frames[0]), 5.5f, 1, 5.5f, 2f, samples);

            AssertPacket(samples, count, 2.5f, new Vector2(2.5f, 0f));
        }

        [Test]
        public void Fill_InterpolatesDepthAndUsesMappedDepthWhenTelemetryDepthIsInvalid()
        {
            var trajectory = StraightTrajectory(0f, 4f, 1f);
            var frames = new List<TelemetryFrame>
            {
                CreateFrame(0f, 10f),
                CreateFrame(4f, 40f)
            };
            var samples = new WaterWakePathSample[3];

            var count = WaterWakePathSampler.Fill(
                trajectory, frames, CreateMapper(frames[0]), 4f, 1, 2f, samples);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(samples[1].DepthM, Is.EqualTo(32.5f).Within(0.0001f));

            frames[1] = CreateFrame(4f, float.NaN);
            trajectory = new FittedTrajectory(
                new[] { 0f, 4f },
                new[] { new Vector3(0f, -10f, 0f), new Vector3(4f, -6f, 0f) });
            count = WaterWakePathSampler.Fill(
                trajectory, frames, CreateMapper(frames[0]), 4f, 1, 2f, samples);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(samples[0].DepthM, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(samples[1].DepthM, Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void Fill_RespectsDestinationBoundsAndTrackBoundaries()
        {
            var trajectory = StraightTrajectory(0f, 2f, 1f);
            var frames = StraightFrames(0, 2, 1f);
            var mapper = CreateMapper(frames[0]);
            var empty = new WaterWakePathSample[0];
            var single = new WaterWakePathSample[1];
            var bounded = new WaterWakePathSample[64];

            Assert.That(WaterWakePathSampler.Fill(trajectory, frames, mapper, 0f, 1, 20f, empty), Is.Zero);
            Assert.That(WaterWakePathSampler.Fill(trajectory, frames, mapper, 0f, 1, 20f, single), Is.EqualTo(1));
            Assert.That(single[0].AgeSeconds, Is.Zero);
            Assert.That(WaterWakePathSampler.Fill(trajectory, frames, mapper, 0f, 1, 20f, bounded), Is.EqualTo(1));
        }

        private static void AssertPacket(
            WaterWakePathSample[] samples,
            int count,
            float distance,
            Vector2 position)
        {
            for (var index = 0; index < count; index++)
            {
                if (Mathf.Abs(samples[index].DistanceAlongPath - distance) < 0.0001f)
                {
                    Assert.That(Vector2.Distance(samples[index].PositionXZ, position), Is.LessThan(0.0001f));
                    return;
                }
            }

            Assert.Fail($"No sample at absolute path distance {distance}.");
        }

        private static FittedTrajectory StraightTrajectory(float start, float end, float unitsPerSecond)
        {
            return new FittedTrajectory(
                new[] { start, end },
                new[] { new Vector3(start * unitsPerSecond, 0f, 0f), new Vector3(end * unitsPerSecond, 0f, 0f) });
        }

        private static List<TelemetryFrame> StraightFrames(int start, int end, float depthStep)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = start; i <= end; i++)
            {
                frames.Add(CreateFrame(i, i * depthStep));
            }

            return frames;
        }

        private static GeoCoordinateMapper CreateMapper(TelemetryFrame origin)
        {
            return new GeoCoordinateMapper(origin, horizontalScale: 1f, depthScale: 1f);
        }

        private static TelemetryFrame CreateFrame(float elapsedSeconds, float depthM)
        {
            return new TelemetryFrame(
                Mathf.RoundToInt(elapsedSeconds),
                $"t{elapsedSeconds}",
                elapsedSeconds,
                120d + elapsedSeconds * 0.0001d,
                25d,
                depthM,
                0f,
                90f,
                0f,
                0f,
                28f,
                0f,
                95f,
                "mode",
                "state",
                1f,
                90f,
                depthM,
                0f,
                0f,
                0f,
                0f);
        }
    }
}
