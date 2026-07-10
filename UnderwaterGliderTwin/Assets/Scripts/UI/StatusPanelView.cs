using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed class StatusPanelView : MonoBehaviour
    {
        private const float MinUiUpdateIntervalSeconds = 1f / 15f;
        private PlaybackController playback;
        private AlarmEvaluator alarmEvaluator;
        private TwinLogger logger;
        private float nextAllowedUiTime;
        private Image alarmBackground;
        private Text modeValue;
        private Text stateValue;
        private Text targetSegmentValue;
        private Text targetHeadingValue;
        private Text targetDepthValue;
        private Text targetAltitudeValue;
        private Text alarmValue;
        private Text rowValue;
        private Text rawTimeValue;
        private string lastAlarmMessage;

        public void Initialize(PlaybackController playbackController, AlarmEvaluator evaluator, TwinLogger twinLogger)
        {
            playback = playbackController;
            alarmEvaluator = evaluator;
            logger = twinLogger;

            var canvas = UiFactory.EnsureCanvas(transform);
            var panel = UiFactory.Panel("StatusPanel", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(332f, 388f), new Color(0.02f, 0.09f, 0.12f, 0.78f));
            UiFactory.Text("StatusTitle", panel, "Status", 18, TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -20f), new Vector2(220f, 28f));

            modeValue = AddRow(panel, "Mode", "ModeValue", 64f);
            stateValue = AddRow(panel, "State", "StateValue", 94f);
            targetSegmentValue = AddRow(panel, "Segment", "TargetSegmentValue", 124f);
            targetHeadingValue = AddRow(panel, "Target Hdg", "TargetHeadingValue", 154f);
            targetDepthValue = AddRow(panel, "Target Dep", "TargetDepthValue", 184f);
            targetAltitudeValue = AddRow(panel, "Target Alt", "TargetAltitudeValue", 214f);
            rowValue = AddRow(panel, "Row", "RowValue", 244f);
            rawTimeValue = AddRow(panel, "Time", "RawTimeValue", 274f);

            var alarmRect = UiFactory.Panel("AlarmPanel", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-32f, 72f), new Color(0.02f, 0.16f, 0.15f, 0.8f));
            alarmBackground = alarmRect.GetComponent<Image>();
            alarmValue = UiFactory.Text("AlarmValue", alarmRect, "normal", 13, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 56f));

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

        private static Text AddRow(Transform panel, string label, string valueName, float topOffset)
        {
            UiFactory.Text(label + "Label", panel, label, 13, TextAnchor.MiddleLeft, new Color(0.68f, 0.88f, 0.92f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -topOffset), new Vector2(116f, 24f));
            return UiFactory.Text(valueName, panel, "-", 13, TextAnchor.MiddleRight, Color.white, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -topOffset), new Vector2(174f, 24f));
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            if (!ShouldUpdateForFrame(Time.unscaledTime, reason))
            {
                return;
            }

            modeValue.text = frame.WorkMode;
            stateValue.text = frame.RunState;
            targetSegmentValue.text = $"{frame.TargetSegment:0}";
            targetHeadingValue.text = $"{frame.TargetHeadingDeg:0.0} deg";
            targetDepthValue.text = $"{frame.TargetDepthM:0.0} m";
            targetAltitudeValue.text = $"{frame.TargetAltitudeM:0.0} m";
            rowValue.text = index.ToString();
            rawTimeValue.text = frame.RawTime;

            var alarm = alarmEvaluator.Evaluate(frame);
            alarmValue.text = alarm.HasAny ? alarm.Message : "normal";
            alarmBackground.color = alarm.HasAny ? new Color(0.75f, 0.18f, 0.05f, 0.88f) : new Color(0.02f, 0.16f, 0.15f, 0.8f);
            if (alarm.HasAny && alarm.Message != lastAlarmMessage)
            {
                logger.AppendAlarm($"row {index}: {alarm.Message}");
                lastAlarmMessage = alarm.Message;
            }
        }
    }
}
