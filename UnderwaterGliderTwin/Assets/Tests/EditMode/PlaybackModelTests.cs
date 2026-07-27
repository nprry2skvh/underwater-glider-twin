using System.Collections.Generic;
using NUnit.Framework;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class PlaybackModelTests
    {
        [Test]
        public void Tick_AdvancesByRowsPerSecondAndSpeed()
        {
            var model = new PlaybackModel(Frames(100), rowsPerSecond: 10f);
            model.SetPlaying(true);
            model.SetSpeed(2f);

            model.Tick(1f);

            Assert.That(model.CurrentIndex, Is.EqualTo(20));
        }

        [Test]
        public void SeekNormalized_ClampsToValidIndex()
        {
            var model = new PlaybackModel(Frames(100), rowsPerSecond: 10f);

            model.SeekNormalized(0.5f);

            Assert.That(model.CurrentIndex, Is.EqualTo(50).Within(1));
            Assert.That(model.CurrentFrame.RowIndex, Is.EqualTo(model.CurrentIndex));
        }

        [Test]
        public void Tick_RewindsWhenDirectionIsReverse()
        {
            var model = new PlaybackModel(Frames(100), rowsPerSecond: 10f);
            model.SeekNormalized(0.5f);
            model.SetDirection(-1);
            model.SetPlaying(true);

            model.Tick(1f);

            Assert.That(model.CurrentIndex, Is.EqualTo(40));
        }

        [Test]
        public void Tick_StopsAtFirstFrameWhenReversingPastStart()
        {
            var model = new PlaybackModel(Frames(100), rowsPerSecond: 10f);
            model.SeekNormalized(0.1f);
            model.SetDirection(-1);
            model.SetPlaying(true);

            model.Tick(2f);

            Assert.That(model.CurrentIndex, Is.EqualTo(0));
            Assert.That(model.IsPlaying, Is.False);
        }

        [Test]
        public void PlaybackController_ReportsSeekReason()
        {
            var controller = new UnityEngine.GameObject("Playback").AddComponent<PlaybackController>();
            var receivedReason = FrameUpdateReason.Initial;
            controller.Initialize(new PlaybackModel(Frames(10), rowsPerSecond: 10f));
            controller.FrameChangedWithReason += (frame, index, progress, reason) => receivedReason = reason;

            controller.Seek(0.5f);

            Assert.That(receivedReason, Is.EqualTo(FrameUpdateReason.Seek));
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(i, $"t{i}", i, 120, 25, i, 100, 0, 0, 0, 28, 0, 95, "mode", "state", 1, 0, 0, 0, 0, 0, 0));
            }

            return frames;
        }
    }
}
