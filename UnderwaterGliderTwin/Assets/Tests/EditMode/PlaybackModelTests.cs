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

        [Test]
        public void ReplaceFrames_PreservesIndexAndPlaybackState()
        {
            var original = Frames(10);
            var replacement = Frames(16);
            var model = new PlaybackModel(original, rowsPerSecond: 10f);
            model.SeekNormalized(0.5f);
            model.SetSpeed(2.5f);
            model.SetDirection(-1);
            model.SetPlaying(true);
            var preservedIndex = model.CurrentIndex;

            model.ReplaceFrames(replacement, preservedIndex);

            Assert.That(model.Frames, Is.SameAs(replacement));
            Assert.That(model.CurrentIndex, Is.EqualTo(preservedIndex));
            Assert.That(model.Speed, Is.EqualTo(2.5f));
            Assert.That(model.Direction, Is.EqualTo(-1));
            Assert.That(model.IsPlaying, Is.True);
        }

        [Test]
        public void ReplaceFrames_RejectsReplacementThatChangesHistory()
        {
            var original = Frames(10);
            var replacement = new List<TelemetryFrame>(Frames(16));
            replacement[2] = new TelemetryFrame(
                2, "changed", 2f, 120d, 25d, 2f, 100f, 0f, 0f, 0f,
                28f, 0f, 95f, "mode", "state", 1f, 0f, 0f, 0f, 0f, 0f, 0f);
            var model = new PlaybackModel(original, rowsPerSecond: 10f);
            model.SeekNormalized(0.5f);

            Assert.Throws<System.ArgumentException>(() => model.ReplaceFrames(replacement, model.CurrentIndex));
            Assert.That(model.Frames, Is.SameAs(original));
        }

        [Test]
        public void PlaybackController_ReportsRebuildReasonWhenFramesAreReplaced()
        {
            var original = Frames(10);
            var replacement = Frames(16);
            var model = new PlaybackModel(original, rowsPerSecond: 10f);
            model.SeekNormalized(0.5f);
            var controller = new UnityEngine.GameObject("Playback").AddComponent<PlaybackController>();
            controller.Initialize(model);
            var receivedReason = FrameUpdateReason.Initial;
            controller.FrameChangedWithReason += (frame, index, progress, reason) => receivedReason = reason;

            model.ReplaceFrames(replacement, model.CurrentIndex);

            Assert.That(receivedReason, Is.EqualTo(FrameUpdateReason.Rebuild));
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
