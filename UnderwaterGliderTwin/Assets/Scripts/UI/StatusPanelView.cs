using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed class StatusPanelView : MonoBehaviour
    {
        private const float MinUiUpdateIntervalSeconds = 1f / 15f;
        private PlaybackController playback;
        private PredictionController prediction;
        private AlarmEvaluator alarmEvaluator;
        private TwinLogger logger;
        private float[] cumulativeDistanceMeters;
        private float nextAllowedUiTime;
        private Image alarmBackground;
        private Image progressFill;
        private Text missionValue;
        private Text modeValue;
        private Text stateValue;
        private Text segmentValue;
        private Text remainingDistanceValue;
        private Text etaValue;
        private Text predictionStatusValue;
        private Text batteryValue;
        private Text driftValue;
        private Text rmseValue;
        private Text maeValue;
        private Text confidenceValue;
        private Text predictionTimeValue;
        private Text engineeringValidationValue;
        private Text alarmValue;
        private Text missionHealthValue;
        private string lastAlarmMessage;
        private readonly List<GameObject> predictionMetricRows = new List<GameObject>();
        private bool minimalBoundReferences;

        public void Initialize(PlaybackController playbackController, AlarmEvaluator evaluator, TwinLogger twinLogger, PredictionController predictionController)
        {
            playback = playbackController;
            alarmEvaluator = evaluator;
            logger = twinLogger;
            prediction = predictionController;
            cumulativeDistanceMeters = BuildDistanceCache(playback.Model);

            var canvas = UiFactory.EnsureCanvas(transform);
            var canvasRect = canvas.transform as RectTransform;
            var canvasHeight = canvasRect != null && canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;
            var panelHeight = Mathf.Max(420f, canvasHeight - UiFactory.CommandCenterContentTopOffset - UiFactory.CommandCenterOperationsTopOffset);
            var panel = UiFactory.CommandPanel("MissionStatusPanel", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -UiFactory.CommandCenterContentTopOffset), new Vector2(352f, panelHeight));
            UiFactory.Text("MissionStatusTitle", panel, "任务状态", 18, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -20f), new Vector2(220f, 28f));
            var healthBadge = UiFactory.Panel("MissionHealthBadge", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -20f), new Vector2(118f, 28f), new Color(0.02f, 0.28f, 0.22f, 0.96f));
            missionHealthValue = UiFactory.Text("MissionHealthBadgeValue", healthBadge, "正常", 12, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(106f, 22f));

            missionValue = AddRow(panel, "任务来源", "MissionValue", 62f);
            modeValue = AddRow(panel, "工作模式", "ModeValue", 94f);
            stateValue = AddRow(panel, "运行状态", "StateValue", 126f);
            segmentValue = AddRow(panel, "当前航段", "CurrentSegmentValue", 158f);
            remainingDistanceValue = AddRow(panel, "剩余距离", "RemainingDistanceValue", 230f);
            etaValue = AddRow(panel, "预计时间", "EtaValue", 262f);
            predictionStatusValue = AddRow(panel, "预测状态", "PredictionStatusValue", 294f);
            batteryValue = AddRow(panel, "剩余电量", "RemainingBatteryValue", 326f);
            driftValue = AddRow(panel, "漂移", "DriftValue", 358f);
            rmseValue = AddRow(panel, "均方根误差", "RmseValue", 390f);
            maeValue = AddRow(panel, "平均绝对误差", "MaeValue", 422f);
            confidenceValue = AddRow(panel, "置信度", "ConfidenceValue", 454f);
            predictionTimeValue = AddRow(panel, "预测耗时", "PredictionTimeValue", 486f);
            engineeringValidationValue = AddRow(panel, "工程校核", "EngineeringValidationValue", 518f);
            RegisterPredictionMetric(panel, "漂移Label", driftValue);
            RegisterPredictionMetric(panel, "均方根误差Label", rmseValue);
            RegisterPredictionMetric(panel, "平均绝对误差Label", maeValue);
            RegisterPredictionMetric(panel, "置信度Label", confidenceValue);
            RegisterPredictionMetric(panel, "预测耗时Label", predictionTimeValue);

            UiFactory.Text("MissionProgressLabel", panel, "任务进度", 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.97f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -190f), new Vector2(116f, 20f));
            progressFill = UiFactory.ProgressBar("MissionProgressBar", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -214f), new Vector2(320f, 14f));

            var alarmRect = UiFactory.Panel("AlarmPanel", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-32f, 72f), new Color(0.02f, 0.16f, 0.15f, 0.8f));
            alarmBackground = alarmRect.GetComponent<Image>();
            alarmValue = UiFactory.Text("AlarmValue", alarmRect, "运行正常", 13, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 56f));

            playback.FrameChangedWithReason += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
        }

        public void Bind(StatusPanelRefs refs, PlaybackController playbackController, AlarmEvaluator evaluator, TwinLogger twinLogger, PredictionController predictionController)
        {
            playback = playbackController;
            alarmEvaluator = evaluator;
            logger = twinLogger;
            prediction = predictionController;
            if (refs == null || playback == null || playback.Model == null)
            {
                return;
            }

            alarmBackground = refs.alarmBackground;
            progressFill = refs.progressFill;
            missionValue = refs.missionValue;
            modeValue = refs.modeValue;
            stateValue = refs.stateValue;
            segmentValue = refs.segmentValue;
            remainingDistanceValue = refs.remainingDistanceValue;
            etaValue = refs.etaValue;
            predictionStatusValue = refs.predictionStatusValue;
            batteryValue = refs.batteryValue;
            driftValue = refs.driftValue;
            rmseValue = refs.rmseValue;
            maeValue = refs.maeValue;
            confidenceValue = refs.confidenceValue;
            predictionTimeValue = refs.predictionTimeValue;
            engineeringValidationValue = refs.engineeringValidationValue;
            alarmValue = refs.alarmValue;
            missionHealthValue = refs.missionHealthValue;
            minimalBoundReferences = modeValue == null || stateValue == null || alarmValue == null;
            cumulativeDistanceMeters = BuildDistanceCache(playback.Model);
            playback.FrameChangedWithReason -= OnFrameChanged;
            playback.FrameChangedWithReason += OnFrameChanged;
            RefreshFromCurrentFrame();
        }

        internal void RefreshFromCurrentFrame()
        {
            if (playback == null || playback.Model == null)
            {
                return;
            }

            if (minimalBoundReferences)
            {
                if (missionValue != null)
                {
                    missionValue.text = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Csv ? "CSV" : "Simulation";
                }

                if (batteryValue != null)
                {
                    batteryValue.text = $"{playback.Model.CurrentFrame.BatteryPercent:0} %";
                }

                return;
            }

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
            UiFactory.Text(label + "Label", panel, label, 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.97f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -topOffset), new Vector2(134f, 22f));
            return UiFactory.Text(valueName, panel, "-", 13, TextAnchor.MiddleRight, new Color(0.96f, 0.99f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -topOffset), new Vector2(170f, 22f));
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            if (!ShouldUpdateForFrame(Time.unscaledTime, reason))
            {
                return;
            }

            var snapshot = prediction != null ? prediction.CurrentSnapshot : PredictionSnapshot.Empty;
            missionValue.text = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Csv ? "CSV 回放" : "参数仿真";
            modeValue.text = TranslateOperationalValue(frame.WorkMode);
            stateValue.text = TranslateOperationalValue(frame.RunState);
            segmentValue.text = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                ? $"{frame.TargetSegment:0} / {Mathf.Max(1, RuntimeDataSourceState.SimulationProfile?.CycleCount ?? 1)}"
                : $"{frame.TargetSegment:0}";
            remainingDistanceValue.text = $"{GetRemainingDistance(index) / 1000f:0.00} km";
            etaValue.text = EstimateEta(index);
            predictionStatusValue.text = FormatPredictionStatus(snapshot);
            predictionStatusValue.color = snapshot.SampleCount > 1 ? new Color(0.74f, 0.95f, 1f) : new Color(1f, 0.72f, 0.32f);
            batteryValue.text = $"{frame.BatteryPercent:0} %";
            driftValue.text = $"{snapshot.CurrentErrorMeters:0.00} m";
            rmseValue.text = $"{snapshot.RmseMeters:0.00} m";
            maeValue.text = $"{snapshot.MaeMeters:0.00} m";
            confidenceValue.text = $"{snapshot.Confidence01 * 100f:0} %";
            predictionTimeValue.text = $"{snapshot.ComputeMilliseconds:0.00} ms";
            SetPredictionMetricsVisible(snapshot.SampleCount > 1);

            var validation = MissionValidationEvaluator.Evaluate(
                RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                    ? RuntimeDataSourceState.SimulationProfile
                    : null,
                playback.Model.Frames,
                System.DateTime.UtcNow);
            engineeringValidationValue.text = FormatEngineeringValidation(validation);
            engineeringValidationValue.color = validation.IsReady
                ? new Color(0.58f, 0.94f, 0.74f)
                : new Color(1f, 0.68f, 0.36f);

            if (progressFill != null)
            {
                var parentWidth = ((RectTransform)progressFill.transform.parent).sizeDelta.x;
                ((RectTransform)progressFill.transform).sizeDelta = new Vector2(parentWidth * Mathf.Clamp01(progress01), 0f);
            }

            var alarm = alarmEvaluator.Evaluate(frame);
            var needsAttention = alarm.HasAny || !validation.IsReady;
            var attentionMessage = alarm.HasAny ? alarm.Message : FormatEngineeringValidation(validation);
            alarmValue.text = needsAttention ? attentionMessage : "运行正常";
            alarmBackground.color = needsAttention ? new Color(0.75f, 0.18f, 0.05f, 0.88f) : new Color(0.02f, 0.16f, 0.15f, 0.8f);
            missionHealthValue.text = needsAttention ? (alarm.HasAny ? "告警" : "注意") : "正常";
            missionHealthValue.transform.parent.GetComponent<Image>().color = needsAttention ? new Color(0.78f, 0.22f, 0.08f, 0.96f) : new Color(0.02f, 0.28f, 0.22f, 0.96f);
            if (alarm.HasAny && alarm.Message != lastAlarmMessage)
            {
                logger.AppendAlarm($"row {index}: {alarm.Message}");
                lastAlarmMessage = alarm.Message;
            }
        }

        private void RegisterPredictionMetric(Transform panel, string labelName, Text value)
        {
            predictionMetricRows.Add(panel.Find(labelName).gameObject);
            predictionMetricRows.Add(value.gameObject);
        }

        private void SetPredictionMetricsVisible(bool visible)
        {
            foreach (var row in predictionMetricRows)
            {
                row.SetActive(visible);
            }
        }

        private static string FormatEngineeringValidation(MissionValidationReport report)
        {
            if (!report.IsDepthWithinLimit)
            {
                return $"深度超限 {report.MaximumObservedDepthM:0}/{report.AlarmDepthLimitM:0} m";
            }

            if (!report.IsCurrentConfigurationReady)
            {
                return $"海流覆盖不足 0-{report.MissionDepthM:0} m";
            }

            return report.UsesStaticWater
                ? $"静水基准 | 上限 {report.AlarmDepthLimitM:0} m"
                : $"校核通过 | 上限 {report.AlarmDepthLimitM:0} m";
        }

        private static string FormatPredictionStatus(PredictionSnapshot snapshot)
        {
            if (snapshot.SampleCount > 1)
            {
                return "运行中";
            }

            if (snapshot.Status.Contains("artifacts missing")
                || snapshot.Status.Contains("manifest")
                || snapshot.Status.Contains("artifact"))
            {
                return "不可用";
            }

            switch (snapshot.Status)
            {
                case "":
                case "Idle":
                    return "待机";
                case "Awaiting valid telemetry position":
                    return "等待有效遥测位置";
                case "Prediction window exhausted":
                case "Simulation prediction window exhausted":
                    return "预测窗口已结束";
                case "Predictor not registered":
                    return "预测器未注册";
                default:
                    return snapshot.Status.Contains("artifacts detected") ? "模型文件已就绪" : "状态待确认";
            }
        }

        private static string TranslateOperationalValue(string value)
        {
            switch (value)
            {
                case "Parameter Simulation":
                    return "参数仿真";
                case "Surface":
                    return "水面";
                case "Glide":
                    return "滑翔";
                case "Turnaround":
                    return "转向过渡";
                default:
                    return value;
            }
        }

        private float GetRemainingDistance(int index)
        {
            if (cumulativeDistanceMeters.Length == 0)
            {
                return 0f;
            }

            index = Mathf.Clamp(index, 0, cumulativeDistanceMeters.Length - 1);
            return cumulativeDistanceMeters[cumulativeDistanceMeters.Length - 1] - cumulativeDistanceMeters[index];
        }

        private string EstimateEta(int index)
        {
            var remainingSeconds = playback.Model.EndElapsedSeconds - playback.Model.GetFrame(index).ElapsedSeconds;
            remainingSeconds = Mathf.Max(0f, remainingSeconds / Mathf.Max(playback.Model.Speed, 0.1f));
            var totalSeconds = Mathf.RoundToInt(remainingSeconds);
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var seconds = totalSeconds % 60;
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        private static float[] BuildDistanceCache(PlaybackModel model)
        {
            var distances = new float[model.FrameCount];
            for (var i = 1; i < model.FrameCount; i++)
            {
                var deltaSeconds = Mathf.Max(0f, model.GetFrame(i).ElapsedSeconds - model.GetFrame(i - 1).ElapsedSeconds);
                var velocity = TelemetryKinematicsUtility.EstimateWindowVelocity(model.Frames, i);
                velocity = TelemetryVelocityEstimator.ClampHorizontalSpeed(velocity);
                distances[i] = distances[i - 1] + velocity.magnitude * deltaSeconds;
            }

            return distances;
        }
    }
}
