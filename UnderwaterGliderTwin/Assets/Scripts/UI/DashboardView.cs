using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed class DashboardView : MonoBehaviour
    {
        private const float MinUiUpdateIntervalSeconds = 1f / 15f;
        private PlaybackController playback;
        private float nextAllowedUiTime;
        private Text depthValue;
        private Text headingValue;
        private Text pitchValue;
        private Text rollValue;
        private Text batteryValue;
        private Text voltageValue;
        private Text currentValue;
        private Text rpmValue;
        private Text pistonValue;

        public void Initialize(PlaybackController playbackController)
        {
            playback = playbackController;
            var canvas = UiFactory.EnsureCanvas(transform);
            var panel = UiFactory.Panel("DashboardPanel", canvas.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(250f, 330f), new Color(0.02f, 0.09f, 0.12f, 0.72f));
            UiFactory.Text("DashboardTitle", panel, "Telemetry", 18, TextAnchor.MiddleLeft, Color.white, new Vector2(16f, 136f), new Vector2(210f, 28f));

            depthValue = AddRow(panel, "Depth", "DepthValue", 96f);
            headingValue = AddRow(panel, "Heading", "HeadingValue", 66f);
            pitchValue = AddRow(panel, "Pitch", "PitchValue", 36f);
            rollValue = AddRow(panel, "Roll", "RollValue", 6f);
            batteryValue = AddRow(panel, "Battery", "BatteryValue", -24f);
            voltageValue = AddRow(panel, "24V", "VoltageValue", -54f);
            currentValue = AddRow(panel, "Current", "CurrentValue", -84f);
            rpmValue = AddRow(panel, "RPM", "RpmValue", -114f);
            pistonValue = AddRow(panel, "Piston", "PistonValue", -144f);

            playback.FrameChangedWithReason += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChanged;
            }
        }

        public bool ShouldUpdateForFrame(float timeSeconds, FrameUpdateReason reason)
        {
            if (reason == FrameUpdateReason.Initial || reason == FrameUpdateReason.Seek)
            {
                nextAllowedUiTime = timeSeconds + MinUiUpdateIntervalSeconds;
                return true;
            }

            if (timeSeconds < nextAllowedUiTime)
            {
                return false;
            }

            nextAllowedUiTime = timeSeconds + MinUiUpdateIntervalSeconds;
            return true;
        }

        private static Text AddRow(Transform panel, string label, string valueName, float y)
        {
            UiFactory.Text(label + "Label", panel, label, 13, TextAnchor.MiddleLeft, new Color(0.68f, 0.88f, 0.92f), new Vector2(16f, y), new Vector2(95f, 24f));
            return UiFactory.Text(valueName, panel, "-", 14, TextAnchor.MiddleRight, Color.white, new Vector2(156f, y), new Vector2(135f, 24f));
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            if (!ShouldUpdateForFrame(Time.unscaledTime, reason))
            {
                return;
            }

            depthValue.text = $"{frame.DepthM:0.0} m";
            headingValue.text = $"{frame.HeadingDeg:0.0} deg";
            pitchValue.text = $"{frame.PitchDeg:0.0} deg";
            rollValue.text = $"{frame.RollDeg:0.0} deg";
            batteryValue.text = $"{frame.BatteryPercent:0}%";
            voltageValue.text = $"{frame.Voltage24V:0.0} V";
            currentValue.text = $"{frame.Current24A:0.0} A";
            rpmValue.text = $"{frame.PropellerRpm:0} rpm";
            pistonValue.text = $"{frame.PistonMm:0.0} mm";
        }
    }
}
