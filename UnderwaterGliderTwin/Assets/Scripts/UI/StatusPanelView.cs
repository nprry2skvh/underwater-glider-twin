using UnityEngine;
using UnityEngine.UI;
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
        private const float CompactStatusPanelHeight = 620f;
        private const float StatusPanelTopOffset = 114f;
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
        private Text depthValue;
        private Text headingValue;
        private Text pitchValue;
        private Text rollValue;
        private Text eastSpeedValue;
        private Text northSpeedValue;
        private Text verticalSpeedValue;
        private Text waterSpeedValue;
        private Text groundSpeedValue;
        private Text currentSpeedValue;
        private Text elapsedValue;
        private Text netBuoyancyValue;
        private Text energyValue;
        private Text sideSlipValue;
        private Text angleOfAttackValue;
        private Text liftValue;
        private Text dragValue;
        private Text sideForceValue;
        private Text angularRateValue;
        private Text pistonValue;
        private Text actuatorPowerValue;
        private string lastAlarmMessage;
        private bool minimalBoundReferences;

        public static float CalculateStatusPanelHeight(float canvasHeight)
        {
            var availableHeight = Mathf.Max(0f, canvasHeight - StatusPanelTopOffset - UiFactory.CommandCenterOperationsTopOffset);
            return Mathf.Min(CompactStatusPanelHeight, availableHeight);
        }

        [System.Obsolete("Use Bind(...) with editable UI references.")]
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
            var panelHeight = CalculateStatusPanelHeight(canvasHeight);
            var panel = UiFactory.CommandPanel("MissionStatusPanel", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -StatusPanelTopOffset), new Vector2(440f, panelHeight));
            UiFactory.Text("MissionStatusTitle", panel, "任务状态", 18, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -20f), new Vector2(220f, 28f));
            var healthBadge = UiFactory.Panel("MissionHealthBadge", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -20f), new Vector2(118f, 28f), new Color(0.02f, 0.28f, 0.22f, 0.96f));
            missionHealthValue = UiFactory.Text("MissionHealthBadgeValue", healthBadge, "正常", 12, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(106f, 22f));

            UiFactory.Text("MissionProgressLabel", panel, "任务进度", 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.97f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -59f), new Vector2(116f, 20f));
            progressFill = UiFactory.ProgressBar("MissionProgressBar", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -82f), new Vector2(408f, 14f));

            var viewport = UiFactory.Panel("MissionStatusViewport", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear);
            viewport.offsetMin = new Vector2(12f, 88f);
            viewport.offsetMax = new Vector2(-12f, -108f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var contentObject = new GameObject("MissionStatusContent", typeof(RectTransform));
            var content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var y = 8f;
            AddSection(content, "任务", ref y);
            missionValue = AddMetric(content, "任务来源", "MissionValue", 0, y);
            modeValue = AddMetric(content, "工作模式", "ModeValue", 1, y); y += 24f;
            stateValue = AddMetric(content, "运行状态", "StateValue", 0, y);
            segmentValue = AddMetric(content, "当前航段", "CurrentSegmentValue", 1, y); y += 24f;
            remainingDistanceValue = AddMetric(content, "剩余距离", "RemainingDistanceValue", 0, y);
            etaValue = AddMetric(content, "预计时间", "EtaValue", 1, y); y += 24f;
            batteryValue = AddMetric(content, "剩余电量", "RemainingBatteryValue", 0, y);
            elapsedValue = AddMetric(content, "已运行", "StatusElapsedValue", 1, y); y += 24f;
            engineeringValidationValue = AddFullMetric(content, "工程校核", "EngineeringValidationValue", y); y += 24f;

            AddSection(content, "运动", ref y);
            depthValue = AddMetric(content, "深度", "StatusDepthValue", 0, y);
            headingValue = AddMetric(content, "航向", "StatusHeadingValue", 1, y); y += 24f;
            pitchValue = AddMetric(content, "俯仰", "StatusPitchValue", 0, y);
            rollValue = AddMetric(content, "横滚", "StatusRollValue", 1, y); y += 24f;
            eastSpeedValue = AddMetric(content, "东向速度", "StatusEastSpeedValue", 0, y);
            northSpeedValue = AddMetric(content, "北向速度", "StatusNorthSpeedValue", 1, y); y += 24f;
            verticalSpeedValue = AddMetric(content, "垂向速度", "StatusVerticalSpeedValue", 0, y);
            waterSpeedValue = AddMetric(content, "水中速度", "StatusWaterSpeedValue", 1, y); y += 24f;
            groundSpeedValue = AddMetric(content, "对地速度", "StatusGroundSpeedValue", 0, y);
            currentSpeedValue = AddMetric(content, "海流速度", "StatusCurrentSpeedValue", 1, y); y += 24f;

            AddSection(content, "动力", ref y);
            netBuoyancyValue = AddMetric(content, "净浮力", "StatusNetBuoyancyValue", 0, y);
            energyValue = AddMetric(content, "能耗", "StatusEnergyValue", 1, y); y += 24f;
            sideSlipValue = AddMetric(content, "侧滑角", "StatusSideSlipValue", 0, y);
            angleOfAttackValue = AddMetric(content, "攻角", "StatusAngleOfAttackValue", 1, y); y += 24f;
            liftValue = AddMetric(content, "升力", "StatusLiftValue", 0, y);
            dragValue = AddMetric(content, "阻力", "StatusDragValue", 1, y); y += 24f;
            sideForceValue = AddMetric(content, "侧向力", "StatusSideForceValue", 0, y);
            angularRateValue = AddMetric(content, "角速度", "StatusAngularRateValue", 1, y); y += 24f;
            pistonValue = AddMetric(content, "活塞位置", "StatusPistonValue", 0, y);
            actuatorPowerValue = AddMetric(content, "执行功率", "StatusActuatorPowerValue", 1, y); y += 24f;

            AddSection(content, "预测", ref y);
            predictionStatusValue = AddMetric(content, "预测状态", "PredictionStatusValue", 0, y);
            driftValue = AddMetric(content, "漂移", "DriftValue", 1, y); y += 24f;
            rmseValue = AddMetric(content, "均方根", "RmseValue", 0, y);
            maeValue = AddMetric(content, "平均误差", "MaeValue", 1, y); y += 24f;
            confidenceValue = AddMetric(content, "置信度", "ConfidenceValue", 0, y);
            predictionTimeValue = AddMetric(content, "预测耗时", "PredictionTimeValue", 1, y); y += 24f;
            content.sizeDelta = new Vector2(0f, y + 8f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;

            var alarmRect = UiFactory.Panel("AlarmPanel", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-32f, 72f), new Color(0.02f, 0.16f, 0.15f, 0.8f));
            alarmBackground = alarmRect.GetComponent<Image>();
            alarmValue = UiFactory.Text("AlarmValue", alarmRect, "运行正常", 13, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, -8f));

            playback.FrameChangedWithReason += OnFrameChanged;
            playback.ContinuousChanged += OnContinuousChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
            RefreshLiveMetrics();
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
            playback.ContinuousChanged -= OnContinuousChanged;
            playback.ContinuousChanged += OnContinuousChanged;
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
                playback.ContinuousChanged -= OnContinuousChanged;
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

        private static void AddSection(Transform content, string title, ref float y)
        {
            UiFactory.Text("MissionStatusSection" + title, content, title, 12, TextAnchor.MiddleLeft,
                new Color(0.36f, 0.79f, 0.9f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(390f, 20f));
            y += 22f;
        }

        private static Text AddMetric(Transform content, string label, string valueName, int column, float y)
        {
            var x = column == 0 ? 8f : 212f;
            UiFactory.Text(label + "Label", content, label, 11, TextAnchor.MiddleLeft,
                new Color(0.67f, 0.8f, 0.83f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(86f, 22f));
            return UiFactory.Text(valueName, content, "—", 11, TextAnchor.MiddleRight,
                new Color(0.96f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(x + 88f, -y), new Vector2(108f, 22f));
        }

        private static Text AddFullMetric(Transform content, string label, string valueName, float y)
        {
            UiFactory.Text(label + "Label", content, label, 11, TextAnchor.MiddleLeft,
                new Color(0.67f, 0.8f, 0.83f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(86f, 22f));
            return UiFactory.Text(valueName, content, "—", 11, TextAnchor.MiddleRight,
                new Color(0.96f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(96f, -y), new Vector2(304f, 22f));
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
            var hasPredictionMetrics = snapshot.SampleCount > 1;
            driftValue.text = hasPredictionMetrics ? $"{snapshot.CurrentErrorMeters:0.00} m" : "—";
            rmseValue.text = hasPredictionMetrics ? $"{snapshot.RmseMeters:0.00} m" : "—";
            maeValue.text = hasPredictionMetrics ? $"{snapshot.MaeMeters:0.00} m" : "—";
            confidenceValue.text = hasPredictionMetrics ? $"{snapshot.Confidence01 * 100f:0} %" : "—";
            predictionTimeValue.text = hasPredictionMetrics ? $"{snapshot.ComputeMilliseconds:0.00} ms" : "—";

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
                logger?.AppendAlarm($"row {index}: {alarm.Message}");
                lastAlarmMessage = alarm.Message;
            }
        }

        private void OnContinuousChanged(float continuousIndex, float progress01, FrameUpdateReason reason)
        {
            RefreshLiveMetrics();
        }

        private void RefreshLiveMetrics()
        {
            if (depthValue == null || playback == null || playback.Model == null || playback.Model.FrameCount == 0)
            {
                return;
            }

            var sample = ContinuousMotionSampler.Sample(playback.Model.Frames, playback.Model.ContinuousElapsedSeconds);
            var lower = playback.Model.Frames[sample.LowerIndex];
            var upper = playback.Model.Frames[sample.UpperIndex];
            var hasPosition = sample.HasUsableCoordinates;
            var waterValid = sample.HasDiagnostics
                && IsFiniteVector(lower.Diagnostics.Value.WaterVelocityEndMps)
                && IsFiniteVector(upper.Diagnostics.Value.WaterVelocityEndMps);
            var currentValid = sample.HasDiagnostics
                && IsFiniteVector(lower.Diagnostics.Value.CurrentVelocityEndMps)
                && IsFiniteVector(upper.Diagnostics.Value.CurrentVelocityEndMps);
            var hasVelocity = hasPosition && (sample.HasDiagnostics
                ? waterValid && currentValid
                : sample.LowerIndex > 0 || sample.UpperIndex > 0);
            depthValue.text = hasPosition && IsFinite(lower.DepthM) && IsFinite(upper.DepthM)
                ? FormatValue(sample.DepthM, "0.0", "m") : "—";
            headingValue.text = IsFinite(lower.HeadingDeg) && IsFinite(upper.HeadingDeg)
                ? FormatValue(sample.HeadingDeg, "0.0", "°") : "—";
            pitchValue.text = IsFinite(lower.PitchDeg) && IsFinite(upper.PitchDeg)
                ? FormatValue(sample.PitchDeg, "0.0", "°") : "—";
            rollValue.text = IsFinite(lower.RollDeg) && IsFinite(upper.RollDeg)
                ? FormatValue(sample.RollDeg, "0.0", "°") : "—";
            eastSpeedValue.text = hasVelocity ? FormatValue(sample.DisplayVelocityEnuMps.x, "0.00", "m/s") : "—";
            northSpeedValue.text = hasVelocity ? FormatValue(sample.DisplayVelocityEnuMps.z, "0.00", "m/s") : "—";
            verticalSpeedValue.text = hasVelocity ? FormatValue(sample.DisplayVelocityEnuMps.y, "0.00", "m/s") : "—";
            groundSpeedValue.text = hasVelocity ? FormatValue(sample.DisplayVelocityEnuMps.magnitude, "0.00", "m/s") : "—";
            waterSpeedValue.text = waterValid ? FormatValue(sample.WaterVelocityEndMps.magnitude, "0.00", "m/s") : "—";
            currentSpeedValue.text = currentValid ? FormatValue(sample.CurrentVelocityEndMps.magnitude, "0.00", "m/s") : "—";
            var seconds = Mathf.Max(0, Mathf.RoundToInt(sample.ElapsedSeconds));
            elapsedValue.text = $"{seconds / 3600:00}:{(seconds % 3600) / 60:00}:{seconds % 60:00}";

            var diagnostics = playback.Model.CurrentFrame.Diagnostics;
            netBuoyancyValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.NetBuoyancyForceN, "0.0", "N") : "—";
            energyValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.EnergyWatts, "0.0", "W") : "—";
            sideSlipValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.SideSlipDeg, "0.0", "°") : "—";
            angleOfAttackValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.AngleOfAttackDeg, "0.0", "°") : "—";
            liftValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.LiftForceN, "0.0", "N") : "—";
            dragValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.DragForceN, "0.0", "N") : "—";
            sideForceValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.SideForceN, "0.0", "N") : "—";
            angularRateValue.text = diagnostics.HasValue
                ? FormatValue(diagnostics.Value.AngularVelocityRadPerSecond.magnitude * Mathf.Rad2Deg, "0.00", "°/s")
                : "—";
            pistonValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.PistonPositionMm, "0.0", "mm") : "—";
            actuatorPowerValue.text = diagnostics.HasValue ? FormatValue(diagnostics.Value.ActuatorPowerWatts, "0.0", "W") : "—";
        }

        private static string FormatValue(float value, string format, string unit)
        {
            return !IsFinite(value)
                ? "—"
                : $"{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {unit}".Replace(" °", "°");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
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
