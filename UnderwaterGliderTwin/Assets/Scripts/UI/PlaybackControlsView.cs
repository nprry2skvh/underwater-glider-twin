using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.UI
{
    public sealed class PlaybackControlsView : MonoBehaviour
    {
        private PlaybackController playback;
        private Slider progressSlider;
        private bool updatingSlider;

        public void Initialize(PlaybackController playbackController, TwinCameraController cameraController, UnderwaterEnvironmentBuilder environmentBuilder, TrajectoryView trajectoryView)
        {
            playback = playbackController;
            var canvas = UiFactory.EnsureCanvas(transform);
            var panel = UiFactory.Panel("PlaybackControlsPanel", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1080f, 120f), new Color(0.02f, 0.09f, 0.12f, 0.78f));

            UiFactory.Button("PlayPauseButton", panel, "Play", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(88f, 34f)).onClick.AddListener(playback.TogglePlaying);
            AddSpeedButton(panel, "Speed05Button", "0.5x", 118f, 0.5f);
            AddSpeedButton(panel, "Speed1Button", "1x", 176f, 1f);
            AddSpeedButton(panel, "Speed2Button", "2x", 234f, 2f);
            AddSpeedButton(panel, "Speed5Button", "5x", 292f, 5f);
            AddSpeedButton(panel, "Speed10Button", "10x", 350f, 10f);

            progressSlider = UiFactory.Slider("ProgressSlider", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, -18f), new Vector2(620f, 32f));
            progressSlider.onValueChanged.AddListener(OnSliderChanged);

            UiFactory.Button("CameraFollowButton", panel, "Follow", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -62f), new Vector2(88f, 30f)).onClick.AddListener(() => cameraController.SetMode(CameraMode.Follow));
            UiFactory.Button("CameraGlobalButton", panel, "Global", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(114f, -62f), new Vector2(88f, 30f)).onClick.AddListener(() => cameraController.SetMode(CameraMode.Global));
            UiFactory.Button("CameraFreeButton", panel, "Free", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -62f), new Vector2(88f, 30f)).onClick.AddListener(() => cameraController.SetMode(CameraMode.Free));

            UiFactory.Toggle("FogToggle", panel, "Fog", true, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(336f, -62f), new Vector2(120f, 28f)).onValueChanged.AddListener(environmentBuilder.SetFogEnabled);
            UiFactory.Toggle("ParticlesToggle", panel, "Particles", true, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, -62f), new Vector2(140f, 28f)).onValueChanged.AddListener(environmentBuilder.SetParticlesEnabled);
            UiFactory.Toggle("TrajectoryToggle", panel, "Trajectory", true, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(626f, -62f), new Vector2(150f, 28f)).onValueChanged.AddListener(trajectoryView.SetVisible);

            playback.FrameChanged += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChanged -= OnFrameChanged;
            }
        }

        private void AddSpeedButton(Transform panel, string name, string label, float x, float speed)
        {
            UiFactory.Button(name, panel, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -18f), new Vector2(50f, 34f)).onClick.AddListener(() => playback.SetSpeed(speed));
        }

        private void OnSliderChanged(float value)
        {
            if (updatingSlider)
            {
                return;
            }

            playback.Seek(value);
        }

        private void OnFrameChanged(UnderwaterGliderTwin.Telemetry.TelemetryFrame frame, int index, float progress01)
        {
            updatingSlider = true;
            progressSlider.value = progress01;
            updatingSlider = false;
        }
    }
}
