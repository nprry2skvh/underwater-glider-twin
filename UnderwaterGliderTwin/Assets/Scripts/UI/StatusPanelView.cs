using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed class StatusPanelView : MonoBehaviour
    {
        private PlaybackController playback;
        private AlarmEvaluator alarmEvaluator;
        private TwinLogger logger;
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
            var panel = UiFactory.Panel("StatusPanel", canvas.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(295f, 360f), new Color(0.02f, 0.09f, 0.12f, 0.72f));
            UiFactory.Text("StatusTitle", panel, "Status", 18, TextAnchor.MiddleLeft, Color.white, new Vector2(-260f, 150f), new Vector2(240f, 28f));

            modeValue = AddRow(panel, "Mode", "ModeValue", 112f);
            stateValue = AddRow(panel, "State", "StateValue", 82f);
            targetSegmentValue = AddRow(panel, "Segment", "TargetSegmentValue", 52f);
            targetHeadingValue = AddRow(panel, "Target Hdg", "TargetHeadingValue", 22f);
            targetDepthValue = AddRow(panel, "Target Dep", "TargetDepthValue", -8f);
            targetAltitudeValue = AddRow(panel, "Target Alt", "TargetAltitudeValue", -38f);
            rowValue = AddRow(panel, "Row", "RowValue", -68f);
            rawTimeValue = AddRow(panel, "Time", "RawTimeValue", -98f);

            var alarmRect = UiFactory.Panel("AlarmPanel", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(260f, 54f), new Color(0.02f, 0.16f, 0.15f, 0.8f));
            alarmBackground = alarmRect.GetComponent<Image>();
            alarmValue = UiFactory.Text("AlarmValue", alarmRect, "normal", 13, TextAnchor.MiddleCenter, Color.white, Vector2.zero, new Vector2(245f, 46f));

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

        private static Text AddRow(Transform panel, string label, string valueName, float y)
        {
            UiFactory.Text(label + "Label", panel, label, 13, TextAnchor.MiddleLeft, new Color(0.68f, 0.88f, 0.92f), new Vector2(-238f, y), new Vector2(105f, 24f));
            return UiFactory.Text(valueName, panel, "-", 13, TextAnchor.MiddleRight, Color.white, new Vector2(-82f, y), new Vector2(150f, 24f));
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01)
        {
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
