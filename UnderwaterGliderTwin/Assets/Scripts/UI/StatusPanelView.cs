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
        private UiStateBadge missionHealthBadge;
        private string lastAlarmMessage;
        private readonly List<GameObject> predictionMetricRows = new List<GameObject>();
        private bool minimalBoundReferences;
        private RectTransform panel;

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
            var panelHeight = Mathf.Max(420f, canvasHeight - UiFactory.CommandCenterContentTopOffset - UiFactory.CommandCenterOperationsTopOffset);
            panel = UiFactory.CommandPanel("MissionStatusPanel", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -UiFactory.CommandCenterContentTopOffset), new Vector2(352f, panelHeight));
            UiFactory.Text("MissionStatusTitle", panel, "任务状态", 18, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -20f), new Vector2(220f, 28f));
            var healthBadge = UiFactory.Panel("MissionHealthBadge", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -20f), new Vector2(118f, 28f), new Color(0.02f, 0.28f, 0.22f, 0.96f));
            missionHealthValue = UiFactory.Text("MissionHealthBadgeValue", healthBadge, "正常", 12, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(106f, 22f));
            EnsureHealthBadge(healthBadge);

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
            alarmBackground.raycastTarget = false;
            alarmValue = UiFactory.Text("AlarmValue", alarmRect, "运行正常", 13, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 56f));
            EnsureVisualHierarchy();

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

            panel = refs.panel;
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
            EnsureHealthBadge(missionHealthValue != null ? missionHealthValue.transform.parent as RectTransform : null);
            if (alarmValue != null)
            {
                alarmValue.gameObject.SetActive(false);
            }
            if (alarmBackground != null)
            {
                alarmBackground.gameObject.SetActive(false);
            }
            predictionMetricRows.Clear();
            ConfigureBoundRows();
            EnsureVisualHierarchy();
            RegisterPredictionMetric(refs.panel, "漂移Label", driftValue);
            RegisterPredictionMetric(refs.panel, "均方根误差Label", rmseValue);
            RegisterPredictionMetric(refs.panel, "平均绝对误差Label", maeValue);
            RegisterPredictionMetric(refs.panel, "置信度Label", confidenceValue);
            RegisterPredictionMetric(refs.panel, "预测耗时Label", predictionTimeValue);
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
                    SetValue(batteryValue, $"{playback.Model.CurrentFrame.BatteryPercent:0}", "%");
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
            var row = CreateRow(panel, valueName + "Row", topOffset);
            var labelText = UiFactory.Text(label + "Label", row, label, 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.97f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var valueText = UiFactory.Text(valueName, row, "-", 13, TextAnchor.MiddleRight, new Color(0.96f, 0.99f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ConfigureKeyValueChildren(labelText, valueText);
            return valueText;
        }

        private void ConfigureBoundRows()
        {
            ConfigureBoundRow("MissionRow", "任务来源Label", missionValue);
            ConfigureBoundRow("ModeRow", "工作模式Label", modeValue);
            ConfigureBoundRow("StateRow", "运行状态Label", stateValue);
            ConfigureBoundRow("SegmentRow", "当前航段Label", segmentValue);
            ConfigureBoundRow("RemainingDistanceRow", "剩余距离Label", remainingDistanceValue);
            ConfigureBoundRow("EtaRow", "预计时间Label", etaValue);
            ConfigureBoundRow("PredictionStatusRow", "预测状态Label", predictionStatusValue);
            ConfigureBoundRow("BatteryRow", "剩余电量Label", batteryValue);
            ConfigureBoundRow("DriftRow", "漂移Label", driftValue);
            ConfigureBoundRow("RmseRow", "均方根误差Label", rmseValue);
            ConfigureBoundRow("MaeRow", "平均绝对误差Label", maeValue);
            ConfigureBoundRow("ConfidenceRow", "置信度Label", confidenceValue);
            ConfigureBoundRow("PredictionTimeRow", "预测耗时Label", predictionTimeValue);
            ConfigureBoundRow("EngineeringValidationRow", "工程校核Label", engineeringValidationValue);
        }

        private void EnsureVisualHierarchy()
        {
            if (panel == null)
            {
                return;
            }

            UiFactory.EnsureCardSurface(
                panel,
                "MissionStatusCard",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(12f, -176f),
                new Vector2(-12f, -48f));
            var progressCard = UiFactory.EnsureCardSurface(
                panel,
                "MissionProgressCard",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(12f, -232f),
                new Vector2(-12f, -182f),
                UiVisualRole.ControlFill);
            UiFactory.EnsureDivider(
                progressCard,
                "MissionPredictionDivider",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(10f, 0f),
                new Vector2(-10f, 1f));

            var predictionCard = UiFactory.EnsureCardSurface(
                panel,
                "PredictionQualityCard",
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 82f),
                new Vector2(-12f, -238f));
            UiFactory.EnsureDivider(
                predictionCard,
                "PredictionAlarmDivider",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(10f, 0f),
                new Vector2(-10f, 1f));

            UiFactory.EnsureCardSurface(
                panel,
                "AlarmStateCard",
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(12f, 12f),
                new Vector2(-12f, 74f),
                UiVisualRole.ControlFill);

            var title = FindText(panel, "MissionStatusTitle") ?? FindText(panel, "TitleText");
            UiFactory.ApplyTextRole(title, UiTextRole.Title, RuntimeUiLayoutMode.CompressedThreeColumn);
            UiFactory.ApplyTextRole(FindText(panel, "MissionProgressLabel"), UiTextRole.SectionTitle, RuntimeUiLayoutMode.CompressedThreeColumn);
            UiFactory.ApplyTextRole(predictionStatusValue, UiTextRole.Value, RuntimeUiLayoutMode.CompressedThreeColumn);
            UiFactory.ApplyTextRole(alarmValue, UiTextRole.Error, RuntimeUiLayoutMode.CompressedThreeColumn);
        }

        private void ConfigureBoundRow(string rowName, string labelName, Text value)
        {
            if (value == null)
            {
                return;
            }

            UiFactory.ConfigureFixedValueColumn(value);

            var label = FindText(panel, labelName);
            if (panel == null)
            {
                return;
            }

            var row = EnsureRow(panel, rowName);
            value.transform.SetParent(row, false);
            if (label != null)
            {
                label.transform.SetParent(row, false);
                ConfigureKeyValueChildren(label, value);
            }
            else
            {
                label = CreateBoundLabel(row, labelName);
                ConfigureKeyValueChildren(label, value);
            }
        }

        private static Text CreateBoundLabel(Transform row, string labelName)
        {
            var display = labelName != null && labelName.EndsWith("Label", System.StringComparison.Ordinal)
                ? labelName.Substring(0, labelName.Length - "Label".Length)
                : labelName;
            return UiFactory.Text(
                row.name + "Label",
                row,
                display,
                13,
                TextAnchor.MiddleLeft,
                UiFactory.CommandText,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
        }

        private static RectTransform CreateRow(Transform parent, string name, float topOffset)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -topOffset);
            row.sizeDelta = new Vector2(-8f, 26f);
            return ConfigureRow(row);
        }

        private static RectTransform EnsureRow(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                return ConfigureRow(existing);
            }

            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, 26f);
            PositionNewBoundRow(row, parent);
            return ConfigureRow(row);
        }

        private static void PositionNewBoundRow(RectTransform row, Transform parent)
        {
            var rowIndex = 0;
            foreach (Transform child in parent)
            {
                if (child == row.transform || !child.name.EndsWith("Row", System.StringComparison.Ordinal))
                {
                    continue;
                }

                rowIndex++;
            }

            row.anchoredPosition = new Vector2(0f, -62f - rowIndex * 28f);
        }

        private static RectTransform ConfigureRow(RectTransform row)
        {
            var layout = row.GetComponent<HorizontalLayoutGroup>() ?? row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 2, 2);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 26f;
            element.preferredHeight = 26f;
            element.flexibleWidth = 1f;
            return row;
        }

        private static void ConfigureKeyValueChildren(Text label, Text value)
        {
            ConfigureKeyText(label, 116f);
            ConfigureKeyText(value, UiFactory.FixedValueColumnWidth);
            UiFactory.ConfigureFixedLabelColumn(label);
            UiFactory.ConfigureFixedValueColumn(value);
            EnsureUnitColumn(value);
        }

        private static void ConfigureKeyText(Text text, float preferredWidth)
        {
            if (text == null)
            {
                return;
            }

            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            var element = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            element.minWidth = preferredWidth > 0f ? preferredWidth : 0f;
            element.preferredWidth = preferredWidth;
            element.flexibleWidth = preferredWidth > 0f ? 0f : 1f;
        }

        private static Text EnsureUnitColumn(Text value)
        {
            if (value == null)
            {
                return null;
            }

            var unitName = value.name + "Unit";
            var unit = value.transform.parent.Find(unitName)?.GetComponent<Text>();
            if (unit == null)
            {
                unit = UiFactory.Text(unitName, value.transform.parent, string.Empty, 10,
                    TextAnchor.MiddleLeft, UiFactory.CommandMutedText, Vector2.zero, Vector2.zero);
            }

            unit.text = GetUnitLabel(value.name);
            UiFactory.ConfigureFixedUnitColumn(unit);
            unit.gameObject.SetActive(!string.IsNullOrEmpty(unit.text));
            return unit;
        }

        private static string GetUnitLabel(string valueName)
        {
            if (string.IsNullOrEmpty(valueName))
            {
                return string.Empty;
            }

            if (valueName.IndexOf("PredictionTime", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "ms";
            }

            if (valueName.IndexOf("Distance", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "km";
            }

            if (valueName.IndexOf("Battery", System.StringComparison.OrdinalIgnoreCase) >= 0
                || valueName.IndexOf("Confidence", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "%";
            }

            if (valueName.IndexOf("Time", System.StringComparison.OrdinalIgnoreCase) >= 0
                || valueName.IndexOf("Eta", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "s";
            }

            if (valueName.IndexOf("Drift", System.StringComparison.OrdinalIgnoreCase) >= 0
                || valueName.IndexOf("Rmse", System.StringComparison.OrdinalIgnoreCase) >= 0
                || valueName.IndexOf("Mae", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "m";
            }

            return string.Empty;
        }

        private void EnsureHealthBadge(RectTransform badgeRoot)
        {
            if (badgeRoot == null || badgeRoot.name != "MissionHealthBadge")
            {
                badgeRoot = panel != null ? panel.Find("MissionHealthBadge") as RectTransform : null;
                if (badgeRoot == null && panel != null)
                {
                    badgeRoot = UiFactory.Panel("MissionHealthBadge", panel,
                        new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-16f, -20f), new Vector2(118f, 28f), UiFactory.CommandPanelFill);
                    if (missionHealthValue != null)
                    {
                        missionHealthValue.transform.SetParent(badgeRoot, false);
                    }
                }
            }

            if (badgeRoot == null)
            {
                return;
            }

            missionHealthBadge = badgeRoot.GetComponent<UiStateBadge>() ?? badgeRoot.gameObject.AddComponent<UiStateBadge>();
            if (missionHealthBadge.StateText != null && missionHealthBadge.StateText != missionHealthValue)
            {
                UiFactory.ApplyTextRole(missionHealthBadge.StateText, UiTextRole.Value, RuntimeUiLayoutMode.CompressedThreeColumn);
                if (missionHealthValue != null)
                {
                    var legacyColor = missionHealthValue.color;
                    legacyColor.a = 0f;
                    missionHealthValue.color = legacyColor;
                }
            }
        }

        private static Text FindText(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name == name || text.text == name)
                {
                    return text;
                }
            }

            return null;
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
            SetValue(remainingDistanceValue, $"{GetRemainingDistance(index) / 1000f:0.00}", "km");
            etaValue.text = EstimateEta(index);
            predictionStatusValue.text = FormatPredictionStatus(snapshot);
            predictionStatusValue.color = snapshot.SampleCount > 1 ? new Color(0.74f, 0.95f, 1f) : new Color(1f, 0.72f, 0.32f);
            SetValue(batteryValue, $"{frame.BatteryPercent:0}", "%");
            SetValue(driftValue, $"{snapshot.CurrentErrorMeters:0.00}", "m");
            SetValue(rmseValue, $"{snapshot.RmseMeters:0.00}", "m");
            SetValue(maeValue, $"{snapshot.MaeMeters:0.00}", "m");
            SetValue(confidenceValue, $"{snapshot.Confidence01 * 100f:0}", "%");
            SetValue(predictionTimeValue, $"{snapshot.ComputeMilliseconds:0.00}", "ms");
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
            var healthLabel = needsAttention ? (alarm.HasAny ? "告警" : "注意") : snapshot.SampleCount > 1 ? "预测" : "正常";
            if (missionHealthValue != null)
            {
                missionHealthValue.text = healthLabel;
            }
            if (missionHealthBadge != null)
            {
                missionHealthBadge.SetState(needsAttention ? UiStateKind.Warning : snapshot.SampleCount > 1 ? UiStateKind.Prediction : UiStateKind.Normal, healthLabel);
            }
            missionHealthValue.transform.parent.GetComponent<Image>().color = needsAttention ? new Color(0.78f, 0.22f, 0.08f, 0.96f) : new Color(0.02f, 0.28f, 0.22f, 0.96f);
            if (alarm.HasAny && alarm.Message != lastAlarmMessage)
            {
                logger.AppendAlarm($"row {index}: {alarm.Message}");
                lastAlarmMessage = alarm.Message;
            }
        }

        private void RegisterPredictionMetric(Transform panel, string labelName, Text value)
        {
            var label = FindText(panel, labelName);
            if (label != null)
            {
                predictionMetricRows.Add(label.gameObject);
            }

            if (value != null)
            {
                predictionMetricRows.Add(value.gameObject);
            }
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

        private static void SetValue(Text value, string text, string unit)
        {
            if (value == null)
            {
                return;
            }

            value.text = text ?? string.Empty;
            var parent = value.transform.parent;
            var unitText = parent == null ? null : parent.Find(value.name + "Unit")?.GetComponent<Text>();
            if (unitText != null)
            {
                unitText.text = unit ?? string.Empty;
                unitText.gameObject.SetActive(!string.IsNullOrEmpty(unitText.text));
            }
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
