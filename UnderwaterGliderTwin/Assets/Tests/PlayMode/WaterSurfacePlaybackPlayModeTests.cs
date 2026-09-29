using System.Collections.Generic;
using NUnit.Framework;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class WaterSurfacePlaybackPlayModeTests
    {
        private GameObject testRoot;

        [TearDown]
        public void TearDown()
        {
            if (testRoot != null)
            {
                Object.DestroyImmediate(testRoot);
            }
        }

        [Test]
        public void InteractionVelocity_FollowsPlaybackDirectionAndFreezesOnPause()
        {
            testRoot = new GameObject("WaterSurfacePlaybackTest");
            var frames = StraightTrack();
            var playbackObject = new GameObject("Playback");
            playbackObject.transform.SetParent(testRoot.transform, false);
            var playback = playbackObject.AddComponent<PlaybackController>();
            playback.Initialize(new PlaybackModel(frames, rowsPerSecond: 1f));
            playback.Seek(0.5f);

            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var gliderObject = new GameObject("Glider");
            gliderObject.transform.SetParent(testRoot.transform, false);
            var driver = gliderObject.AddComponent<GliderTransformDriver>();
            driver.Initialize(playback, mapper);

            var waterObject = new GameObject("WaterSurface");
            waterObject.transform.SetParent(testRoot.transform, false);
            var water = waterObject.AddComponent<WaterSurfaceView>();
            water.Build(WaterSurfaceSettings.CreateDefault());
            water.Bind(playback, null, gliderObject.transform);

            var forward = mapper.Map(frames[2]) - mapper.Map(frames[0]);
            playback.PlayForward();
            playback.Step(0.25f);
            Assert.That(Vector3.Dot(driver.PlaybackVelocity, forward), Is.GreaterThan(0f));
            Assert.That(water.InteractionSpeedMps, Is.GreaterThan(0f));

            playback.PlayReverse();
            playback.Step(0.25f);
            Assert.That(Vector3.Dot(driver.PlaybackVelocity, forward), Is.LessThan(0f));
            Assert.That(water.InteractionSpeedMps, Is.GreaterThan(0f));

            playback.SetPlaying(false);
            water.SetSimulationTime(playback.Model.ContinuousElapsedSeconds - playback.Model.StartElapsedSeconds);
            Assert.That(water.InteractionSpeedMps, Is.EqualTo(0f).Within(0.0001f));
        }

        private static IReadOnlyList<TelemetryFrame> StraightTrack()
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < 3; i++)
            {
                frames.Add(new TelemetryFrame(
                    i,
                    $"t{i}",
                    i * 10f,
                    120d + i * 0.0001d,
                    25d,
                    0.28f,
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
                    0.28f,
                    0f,
                    0f,
                    0f,
                    0f));
            }

            return frames;
        }
    }
}
