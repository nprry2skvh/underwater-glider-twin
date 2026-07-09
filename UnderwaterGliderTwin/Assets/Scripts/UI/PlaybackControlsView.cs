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
            var panel = UiFactory.Panel("PlaybackControlsPanel", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(760f, 96f), new Color(0.02f, 0.09f, 0.12f, 0.72f));

            UiFactory.Button("PlayPauseButton", panel, "Play", new Vector2(-330f, 22f), new Vector2(68f, 34f)).onClick.AddListener(playback.TogglePlaying);
            AddSpeedButton(panel, "Speed05Button", "0.5x", -254f, 0.5f);
            AddSpeedButton(panel, "Speed1Button", "1x", -196f, 1f);
            AddSpeedButton(panel, "Speed2Button", "2x", -138f, 2f);
            AddSpeedButton(panel, "Speed5Button", "5x", -80f, 5f);
            AddSpeedButton(panel, "Speed10Button", "10x", -22f, 10f);

            progressSlider = UiFactory.Slider("ProgressSlider", panel, new Vector2(164f, 22f), new Vector2(320f, 32f));
            progressSlider.onValueChanged.AddListener(OnSliderChanged);

            UiFactory.Button("CameraFollowButton", panel, "Follow", new Vector2(-330f, -24f), new Vector2(68f, 30f)).onClick.AddListener(() => cameraController.SetMode(CameraMode.Follow));
            UiFactory.Button("CameraGlobalButton", panel, "Global", new Vector2(-254f, -24f), new Vector2(68f, 30f)).onClick.AddListener(() => cameraController.SetMode(CameraMode.Global));
            UiFactory.Button("CameraFreeButton", panel, "Free", new Vector2(-178f, -24f), new Vector2(68f, 30f)).onClick.AddListener(() => cameraController.SetMode(CameraMode.Free));

            UiFactory.Toggle("FogToggle", panel, "Fog", true, new Vector2(-76f, -24f), new Vector2(120f, 28f)).onValueChanged.AddListener(environmentBuilder.SetFogEnabled);
            UiFactory.Toggle("ParticlesToggle", panel, "Particles", true, new Vector2(64f, -24f), new Vector2(140f, 28f)).onValueChanged.AddListener(environmentBuilder.SetParticlesEnabled);
            UiFactory.Toggle("TrajectoryToggle", panel, "Trajectory", true, new Vector2(218f, -24f), new Vector2(150f, 28f)).onValueChanged.AddListener(trajectoryView.SetVisible);

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
            UiFactory.Button(name, panel, label, new Vector2(x, 22f), new Vector2(50f, 34f)).onClick.AddListener(() => playback.SetSpeed(speed));
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
