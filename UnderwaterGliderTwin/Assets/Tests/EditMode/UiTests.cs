using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.UI;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class UiTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var obj in Object.FindObjectsOfType<GameObject>())
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void DashboardView_DisplaysCurrentTelemetry()
        {
            var playback = CreatePlayback(Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback);

            Assert.That(FindText("DepthValue").text, Is.EqualTo("10.0 m"));
            Assert.That(FindText("BatteryValue").text, Is.EqualTo("15%"));
        }

        [Test]
        public void DashboardView_ThrottlesPlaybackButAllowsSeekUpdate()
        {
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            Assert.That(dashboard.ShouldUpdateForFrame(0f, FrameUpdateReason.Initial), Is.True);
            Assert.That(dashboard.ShouldUpdateForFrame(0.01f, FrameUpdateReason.Playback), Is.False);
            Assert.That(dashboard.ShouldUpdateForFrame(0.01f, FrameUpdateReason.Seek), Is.True);
        }

        [Test]
        public void StatusPanelView_DisplaysAlarmAndWritesLog()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-log-" + System.Guid.NewGuid().ToString("N"));
            var logger = new TwinLogger(logDirectory);
            var playback = CreatePlayback(Frames(2));
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(playback, new AlarmEvaluator(5f, 20f, 20f), logger);

            Assert.That(FindText("AlarmValue").text, Does.Contain("depth"));
            Assert.That(File.ReadAllText(Path.Combine(logDirectory, "alarm.log")), Does.Contain("depth"));
        }

        [Test]
        public void PlaybackControlsView_ButtonsAndSliderDrivePlayback()
        {
            var playback = CreatePlayback(Frames(10));
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();

            controls.Initialize(playback, cameraController, environment, trajectory);
            GameObject.Find("PlayPauseButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Speed2Button").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("ProgressSlider").GetComponent<Slider>().value = 0.5f;

            Assert.That(playback.Model.IsPlaying, Is.True);
            Assert.That(playback.Model.Speed, Is.EqualTo(2f));
            Assert.That(playback.Model.CurrentIndex, Is.EqualTo(4).Or.EqualTo(5));
        }

        private static Text FindText(string name)
        {
            return GameObject.Find(name).GetComponent<Text>();
        }

        private static PlaybackController CreatePlayback(IReadOnlyList<TelemetryFrame> frames)
        {
            var playback = new GameObject("Playback").AddComponent<PlaybackController>();
            playback.Initialize(new PlaybackModel(frames, rowsPerSecond: 1f));
            return playback;
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(i, $"t{i}", 120, 25, 10f + i, 100, 30f + i, 4f, -3f, 28.5f, 0.3f, 15f, "mode", "state", 2f, 44f, 120f, 80f, 300f, 17f, 32f));
            }

            return frames;
        }
    }
}
