using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed class DataInputView : MonoBehaviour
    {
        private readonly System.Collections.Generic.Dictionary<PredictionModelKind, Button> modelButtons = new System.Collections.Generic.Dictionary<PredictionModelKind, Button>();
        private InputField csvPathInput;
        private InputField predictionHorizonInput;
        private InputField simulationCyclesInput;
        private InputField simulationDurationInput;
        private InputField simulationDepthInput;
        private InputField simulationWaterColumnInput;
        private Text referenceCycleDurationValue;
        private InputField simulationHeadingInput;
        private InputField simulationHeadingDeltaInput;
        private InputField simulationPitchInput;
        private InputField simulationRollInput;
        private InputField descentNetBuoyancyInput;
        private InputField ascentNetBuoyancyInput;
        private InputField descentPitchInput;
        private InputField ascentPitchInput;
        private InputField descentRollInput;
        private InputField ascentRollInput;
        private InputField oceanCurrentMinDepthInput;
        private InputField oceanCurrentMaxDepthInput;
        private InputField oceanCurrentEastwardInput;
        private InputField oceanCurrentNorthwardInput;
        private Text statusText;
        private Text oceanCurrentLayerSummary;
        private RectTransform oceanCurrentDrawer;
        private RectTransform flightLegDrawer;
        private Text flightLegDrawerStatus;
        private Text oceanCurrentDrawerSummary;
        private Text oceanCurrentQualitySummary;
        private Text oceanCurrentDrawerStatus;
        private InputField oceanCurrentDrawerMinDepthInput;
        private InputField oceanCurrentDrawerMaxDepthInput;
        private InputField oceanCurrentDrawerEastwardInput;
        private InputField oceanCurrentDrawerNorthwardInput;
        private InputField oceanCurrentPrefetchHalfWidthInput;
        private InputField oceanCurrentForecastWindowInput;
        private Text oceanCurrentFieldSummary;
        private InputField dynamicsMassInput;
        private InputField dynamicsReferenceAreaInput;
        private InputField dynamicsReferenceLengthInput;
        private InputField dynamicsWingSpanInput;
        private InputField dynamicsMeanChordInput;
        private InputField dynamicsRollInertiaInput;
        private InputField dynamicsPitchInertiaInput;
        private InputField dynamicsYawInertiaInput;
        private InputField dynamicsLiftSlopeInput;
        private InputField dynamicsBaseDragInput;
        private InputField dynamicsTurnaroundDurationInput;
        private InputField missionLongitudeInput;
        private InputField missionLatitudeInput;
        private Button predictionToggleButton;
        private Action<string> loadRequested;
        private Action<SimulationProfile> simulationRequested;
        private Action oceanCurrentSettingsApplied;
        private PredictionController predictionController;
        private string initialPredictionStatus;
        private SimulationProfile simulationProfileTemplate;
        private int selectedOceanCurrentLayerIndex = -1;
        private CopernicusCurrentResult lastOceanCurrentResult;
        private bool flightLegSettingsEdited;
        private bool refreshingFlightLegInputs;

        public void Initialize(
            string currentCsvPath,
            SimulationProfile currentProfile,
            PredictionController controller,
            Action<string> onLoadRequested = null,
            Action<SimulationProfile> onSimulationRequested = null,
            Action onOceanCurrentSettingsApplied = null)
        {
            loadRequested = onLoadRequested;
            simulationRequested = onSimulationRequested;
            oceanCurrentSettingsApplied = onOceanCurrentSettingsApplied;
            predictionController = controller;
            currentProfile ??= SimulationProfile.Default;
            simulationProfileTemplate = currentProfile.Clone();
            flightLegSettingsEdited = HasExplicitFlightLegSettings(simulationProfileTemplate);
            selectedOceanCurrentLayerIndex = simulationProfileTemplate.OceanCurrentProfile?.Layers.Count > 0 ? 0 : -1;
            EnsureRuntimeModelAvailable();

            var canvas = UiFactory.EnsureCanvas(transform);
            var panel = UiFactory.CommandPanel(
                "MissionConfigurationPanel",
                canvas.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -56f),
                new Vector2(-100f, 250f));
            CreateConfigurationGroups(panel);

            UiFactory.Text("MissionConfigurationTitle", panel, "任务配置", 18, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -22f), new Vector2(300f, 28f));

            UiFactory.Text("CsvSourceLabel", panel, "CSV 数据源", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -58f), new Vector2(90f, 22f));
            csvPathInput = UiFactory.InputField("CsvPathInput", panel, currentCsvPath ?? RuntimeDataSourceState.LastCsvPath, "遥测 CSV 路径", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(114f, -52f), new Vector2(1118f, 34f));
            UiFactory.PrimaryButton("LoadCsvButton", panel, "加载 CSV", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1246f, -52f), new Vector2(116f, 34f)).onClick.AddListener(OnLoadClicked);

            UiFactory.Text("PredictionModelLabel", panel, "预测模型", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -102f), new Vector2(120f, 22f));
            CreateModelButton(panel, PredictionModelKind.XGBoost, "XGBoost", 146f);

            UiFactory.Text("PredictionHorizonLabel", panel, "预测时域 (s)", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(694f, -102f), new Vector2(86f, 22f));
            predictionHorizonInput = UiFactory.InputField("PredictionHorizonInput", panel, RuntimePredictionState.HorizonSeconds.ToString("0", CultureInfo.InvariantCulture), "秒数", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(788f, -96f), new Vector2(100f, 34f));
            UiFactory.Button("ApplyPredictionConfigButton", panel, "应用", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, -96f), new Vector2(84f, 34f)).onClick.AddListener(ApplyPredictionConfig);
            predictionToggleButton = UiFactory.Button("PredictionToggleButton", panel, RuntimePredictionState.PredictionEnabled ? "停止预测" : "开始预测", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(996f, -96f), new Vector2(148f, 34f));
            predictionToggleButton.onClick.AddListener(TogglePrediction);
            UiFactory.Text("PredictionRuntimeLabel", panel, "运行时：仅物理模型", 12, TextAnchor.MiddleLeft, new Color(0.92f, 0.76f, 0.3f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1160f, -102f), new Vector2(190f, 22f));

            UiFactory.Text("SimulationLabel", panel, "参数模式", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -144f), new Vector2(110f, 22f));
            simulationCyclesInput = AddLabeledInput(panel, "循环次数", "SimulationCyclesInput", currentProfile.CycleCount.ToString(CultureInfo.InvariantCulture), 136f, -142f, 76f);
            simulationDurationInput = AddLabeledInput(panel, "单航段安全上限 (s)", "SimulationDurationInput", currentProfile.CycleDurationSeconds.ToString("0", CultureInfo.InvariantCulture), 224f, -142f, 120f);
            simulationDepthInput = AddLabeledInput(panel, "深度 (m)", "SimulationDepthInput", currentProfile.TargetDepthM.ToString("0", CultureInfo.InvariantCulture), 356f, -142f, 86f);
            simulationWaterColumnInput = AddLabeledInput(panel, "水柱 (m)", "SimulationWaterColumnInput", currentProfile.WaterColumnDepthM.ToString("0", CultureInfo.InvariantCulture), 1350f, -142f, 86f);
            UiFactory.Text("ReferenceCycleDurationLabel", panel, "航段安全参考", 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(454f, -142f), new Vector2(128f, 18f));
            var referenceCycleReadout = UiFactory.Panel("ReferenceCycleDurationReadout", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(454f, -161f), new Vector2(128f, 28f), new Color(0.04f, 0.16f, 0.2f, 0.95f));
            referenceCycleDurationValue = UiFactory.Text("ReferenceCycleDurationValue", referenceCycleReadout, "-", 13, TextAnchor.MiddleCenter, new Color(0.76f, 0.95f, 1f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -4f));
            UiFactory.Button("ApplyReferenceCycleButton", panel, "采用参考", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(594f, -140f), new Vector2(96f, 34f)).onClick.AddListener(ApplyReferenceCycleDuration);
            simulationHeadingInput = AddLabeledInput(panel, "航向 (°)", "SimulationHeadingInput", currentProfile.StartHeadingDeg.ToString("0", CultureInfo.InvariantCulture), 704f, -142f, 84f);
            simulationHeadingDeltaInput = AddLabeledInput(panel, "转向 (°)", "SimulationHeadingDeltaInput", currentProfile.HeadingDeltaPerCycleDeg.ToString("0", CultureInfo.InvariantCulture), 800f, -142f, 90f);
            simulationPitchInput = AddLabeledInput(panel, "默认俯仰 (°)", "SimulationPitchInput", currentProfile.PitchAmplitudeDeg.ToString("0", CultureInfo.InvariantCulture), 902f, -142f, 78f);
            simulationRollInput = AddLabeledInput(panel, "默认横滚 (°)", "SimulationRollInput", currentProfile.RollAmplitudeDeg.ToString("0", CultureInfo.InvariantCulture), 992f, -142f, 78f);
            UiFactory.PrimaryButton("SimulationApplyButton", panel, "运行仿真", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1082f, -140f), new Vector2(136f, 34f)).onClick.AddListener(OnSimulationClicked);
            UiFactory.Button("FlightLegSettingsButton", panel, "航段参数", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1230f, -140f), new Vector2(104f, 34f)).onClick.AddListener(ToggleFlightLegDrawer);

            UiFactory.Text("OceanCurrentLabel", panel, "海流层", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -194f), new Vector2(110f, 22f));
            oceanCurrentMinDepthInput = AddLabeledInput(panel, "最小 (m)", "OceanCurrentMinDepthInput", "0", 136f, -192f, 82f);
            oceanCurrentMaxDepthInput = AddLabeledInput(panel, "最大 (m)", "OceanCurrentMaxDepthInput", currentProfile.TargetDepthM.ToString("0", CultureInfo.InvariantCulture), 230f, -192f, 82f);
            oceanCurrentEastwardInput = AddLabeledInput(panel, "东流 (m/s)", "OceanCurrentEastwardInput", "0", 324f, -192f, 82f);
            oceanCurrentNorthwardInput = AddLabeledInput(panel, "北流 (m/s)", "OceanCurrentNorthwardInput", "0", 418f, -192f, 82f);
            UiFactory.Button("OceanCurrentPreviousLayerButton", panel, "上一层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(516f, -211f), new Vector2(68f, 28f)).onClick.AddListener(() => SelectOceanCurrentLayer(selectedOceanCurrentLayerIndex - 1));
            UiFactory.Button("OceanCurrentNextLayerButton", panel, "下一层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(592f, -211f), new Vector2(68f, 28f)).onClick.AddListener(() => SelectOceanCurrentLayer(selectedOceanCurrentLayerIndex + 1));
            UiFactory.Button("OceanCurrentAddLayerButton", panel, "新增层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(668f, -211f), new Vector2(76f, 28f)).onClick.AddListener(AddOceanCurrentLayer);
            UiFactory.Button("OceanCurrentSaveLayerButton", panel, "保存层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(752f, -211f), new Vector2(76f, 28f)).onClick.AddListener(SaveOceanCurrentLayer);
            UiFactory.Button("OceanCurrentDeleteLayerButton", panel, "删除层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(836f, -211f), new Vector2(76f, 28f)).onClick.AddListener(DeleteOceanCurrentLayer);
            UiFactory.PrimaryButton("OceanCurrentLookupButton", panel, "联网获取海流", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(924f, -211f), new Vector2(122f, 28f)).onClick.AddListener(LookupOceanCurrent);
            oceanCurrentLayerSummary = UiFactory.Text("OceanCurrentLayerSummary", panel, "海流层：0", 12, TextAnchor.MiddleLeft, new Color(0.74f, 0.95f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1064f, -211f), new Vector2(132f, 28f));
            missionLongitudeInput = AddLabeledInput(panel, "经度 (°)", "MissionLongitudeInput", currentProfile.OriginLongitudeDeg.ToString("0.######", CultureInfo.InvariantCulture), 1210f, -192f, 124f);
            missionLatitudeInput = AddLabeledInput(panel, "纬度 (°)", "MissionLatitudeInput", currentProfile.OriginLatitudeDeg.ToString("0.######", CultureInfo.InvariantCulture), 1346f, -192f, 124f);
            UiFactory.Button("OceanCurrentDrawerButton", panel, "海流配置", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1484f, -211f), new Vector2(116f, 28f)).onClick.AddListener(ToggleOceanCurrentDrawer);

            statusText = UiFactory.Text("MissionConfigurationStatus", panel, string.IsNullOrEmpty(initialPredictionStatus) ? "CSV 回放和参数仿真均可用" : initialPredictionStatus, 12, TextAnchor.MiddleLeft, string.IsNullOrEmpty(initialPredictionStatus) ? new Color(0.8f, 0.96f, 1f) : new Color(1f, 0.76f, 0.3f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -24f), new Vector2(560f, 18f));
            ConfigureCycleDurationAutoCorrection();
            RefreshReferenceCycleDuration();
            BuildOceanCurrentDrawer(canvas.transform);
            BuildFlightLegDrawer(canvas.transform);
            RefreshPredictionSelection();
            RefreshOceanCurrentLayerEditor();
        }

        private static void CreateConfigurationGroups(Transform panel)
        {
            var groupColor = new Color(0.03f, 0.15f, 0.2f, 0.44f);
            UiFactory.Panel("DataSourceConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -42f), new Vector2(1268f, 42f), groupColor);
            UiFactory.Panel("PredictionConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -86f), new Vector2(1196f, 42f), groupColor);
            UiFactory.Panel("SimulationConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -128f), new Vector2(1332f, 50f), groupColor);
            UiFactory.Panel("OceanConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -180f), new Vector2(1600f, 52f), groupColor);
        }

        private void OnLoadClicked()
        {
            var path = csvPathInput != null ? csvPathInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("请输入 CSV 路径", new Color(1f, 0.5f, 0.5f));
                return;
            }

            if (!File.Exists(path))
            {
                SetStatus("未找到 CSV 文件", new Color(1f, 0.5f, 0.5f));
                return;
            }

            if (loadRequested != null)
            {
                SetStatus("正在重新加载遥测数据...", new Color(0.62f, 0.85f, 0.92f));
                loadRequested.Invoke(path);
                return;
            }

            SetStatus("正在重新加载遥测数据...", new Color(0.62f, 0.85f, 0.92f));
            RuntimePathResolver.SetCsvPathOverride(path);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnSimulationClicked()
        {
            if (!TryBuildSimulationProfile(out var profile))
            {
                return;
            }

            if (simulationRequested != null)
            {
                SetStatus("正在切换到参数仿真...", new Color(0.8f, 0.96f, 1f));
                simulationRequested.Invoke(profile);
                return;
            }

            SetStatus("正在切换到参数仿真...", new Color(0.8f, 0.96f, 1f));
            RuntimeDataSourceState.UseSimulation(profile);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ApplyPredictionConfig()
        {
            if (!TryParseFloat(predictionHorizonInput, "预测时域", 30f, 7200f, out var horizonSeconds))
            {
                return;
            }

            RuntimePredictionState.SetHorizonSeconds(horizonSeconds);
            predictionController?.SetHorizonSeconds(horizonSeconds);
            SetStatus($"预测时域已更新为 {horizonSeconds:0}", new Color(0.74f, 0.95f, 1f));
        }

        private void TogglePrediction()
        {
            var enabled = !RuntimePredictionState.PredictionEnabled;
            RuntimePredictionState.SetEnabled(enabled);
            predictionController?.SetPredictionEnabled(enabled);
            RefreshPredictionSelection();
            SetStatus(enabled ? "预测已启用" : "预测已暂停", new Color(0.74f, 0.95f, 1f));
        }

        private void SelectModel(PredictionModelKind modelKind)
        {
            if (predictionController != null && !predictionController.IsModelRuntimeAvailable(modelKind))
            {
                SetStatus($"{modelKind} 运行时推理不可用", new Color(1f, 0.62f, 0.3f));
                return;
            }

            RuntimePredictionState.SetModelKind(modelKind);
            predictionController?.SetModelKind(modelKind);
            RefreshPredictionSelection();
            SetStatus($"预测模型已切换为 {modelKind}", new Color(0.74f, 0.95f, 1f));
        }

        private void RefreshPredictionSelection()
        {
            foreach (var entry in modelButtons)
            {
                var available = predictionController == null || predictionController.IsModelRuntimeAvailable(entry.Key);
                entry.Value.interactable = available;
                entry.Value.image.color = !available
                    ? new Color(0.08f, 0.14f, 0.17f, 0.72f)
                    : entry.Key == RuntimePredictionState.ModelKind
                    ? new Color(0.08f, 0.56f, 0.72f, 0.96f)
                    : new Color(0.12f, 0.28f, 0.34f, 0.9f);
            }

            UiFactory.SetButtonText(predictionToggleButton, RuntimePredictionState.PredictionEnabled ? "停止预测" : "开始预测");
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message;
            statusText.color = color;
        }

        private void ConfigureCycleDurationAutoCorrection()
        {
            simulationDurationInput.onEndEdit.AddListener(_ => AutoCorrectCycleDuration());
            simulationDepthInput.onEndEdit.AddListener(_ => AutoCorrectCycleDuration());
        }

        private void AutoCorrectCycleDuration()
        {
            if (!TryReadPositiveFloat(simulationDurationInput, out var cycleDuration)
                || !TryReadPositiveFloat(simulationDepthInput, out var targetDepth))
            {
                RefreshReferenceCycleDuration();
                return;
            }

            var adjustedCycleDuration = MissionProfileConstraints.NormalizeEngineeringCycleDuration(cycleDuration, targetDepth, GetEngineeringSpeed());
            RefreshReferenceCycleDuration();
            if (adjustedCycleDuration <= cycleDuration + 0.5f)
            {
                return;
            }

            simulationDurationInput.text = adjustedCycleDuration.ToString("0", CultureInfo.InvariantCulture);
            SetStatus($"已按深度参考周期将单周期调整为 {adjustedCycleDuration:0} s", new Color(1f, 0.76f, 0.3f));
        }

        private void ApplyReferenceCycleDuration()
        {
            if (!TryReadPositiveFloat(simulationDepthInput, out var targetDepth))
            {
                return;
            }

            var referenceDuration = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                0f,
                targetDepth,
                GetEngineeringSpeed());
            simulationDurationInput.text = referenceDuration.ToString("0", CultureInfo.InvariantCulture);
            RefreshReferenceCycleDuration();
            SetStatus($"已采用深度参考周期 {referenceDuration:0} s", new Color(0.74f, 0.95f, 1f));
        }

        private void RefreshReferenceCycleDuration()
        {
            if (referenceCycleDurationValue == null)
            {
                return;
            }

            if (!TryReadPositiveFloat(simulationDepthInput, out var targetDepth))
            {
                referenceCycleDurationValue.text = "-";
                return;
            }

            var duration = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                0f,
                targetDepth,
                GetEngineeringSpeed());
            referenceCycleDurationValue.text = FormatCycleDuration(duration);
        }

        private static string FormatCycleDuration(float durationSeconds)
        {
            var totalMinutes = Mathf.Max(0, Mathf.RoundToInt(durationSeconds / 60f));
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return hours > 0 ? $"{hours} h {minutes:00} min" : $"{minutes} min";
        }

        private static bool TryReadPositiveFloat(InputField input, out float value)
        {
            value = 0f;
            return input != null
                && float.TryParse(input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value > 0f;
        }

        private float GetEngineeringSpeed()
        {
            return Mathf.Max(0.05f, GetDynamicsProfile().CruiseSpeedMps);
        }

        private static bool HasExplicitFlightLegSettings(SimulationProfile profile)
        {
            return profile != null
                && (!float.IsNaN(profile.DescentNetBuoyancyForceN)
                    || !float.IsNaN(profile.AscentNetBuoyancyForceN)
                    || !float.IsNaN(profile.DescentPitchDeg)
                    || !float.IsNaN(profile.AscentPitchDeg)
                    || !float.IsNaN(profile.DescentRollDeg)
                    || !float.IsNaN(profile.AscentRollDeg));
        }

        private bool TryBuildSimulationProfile(out SimulationProfile profile)
        {
            profile = null;
            if (!TryParseInt(simulationCyclesInput, "循环次数", out var cycleCount)
                || !TryParseFloat(simulationDurationInput, "单周期", out var cycleDuration)
                || !TryParseFloat(simulationDepthInput, "目标深度", out var targetDepth)
                || !TryParseFloat(simulationHeadingInput, "初始航向", out var heading)
                || !TryParseFloat(simulationHeadingDeltaInput, "周期转向", out var headingDelta)
                || !TryParseFloat(simulationPitchInput, "俯仰幅度", out var pitchAmplitude)
                || !TryParseFloat(simulationRollInput, "横滚幅度", out var rollAmplitude))
            {
                return false;
            }

            var engineeringCycleDuration = MissionProfileConstraints.NormalizeEngineeringCycleDuration(cycleDuration, targetDepth, GetEngineeringSpeed());
            if (engineeringCycleDuration > cycleDuration + 0.5f)
            {
                cycleDuration = engineeringCycleDuration;
                simulationDurationInput.text = cycleDuration.ToString("0", CultureInfo.InvariantCulture);
                SetStatus($"已按深度参考周期将单周期调整为 {cycleDuration:0} 秒", new Color(1f, 0.76f, 0.3f));
            }

            profile = simulationProfileTemplate != null ? simulationProfileTemplate.Clone() : SimulationProfile.Default;
            profile.CycleCount = cycleCount;
            profile.CycleDurationSeconds = cycleDuration;
            profile.TargetDepthM = targetDepth;
            if (simulationWaterColumnInput != null)
            {
                if (!TryParseFloat(simulationWaterColumnInput, "水柱深度", out var waterColumnDepth))
                {
                    profile = null;
                    return false;
                }
                profile.WaterColumnDepthM = Mathf.Max(targetDepth, waterColumnDepth);
            }
            else
            {
                profile.WaterColumnDepthM = Mathf.Max(profile.WaterColumnDepthM, targetDepth);
            }
            if (simulationWaterColumnInput != null)
            {
                simulationWaterColumnInput.text = profile.WaterColumnDepthM.ToString("0", CultureInfo.InvariantCulture);
            }
            profile.StartHeadingDeg = heading;
            profile.HeadingDeltaPerCycleDeg = headingDelta;
            profile.PitchAmplitudeDeg = pitchAmplitude;
            profile.RollAmplitudeDeg = rollAmplitude;
            if (!TryApplyFlightLegParameters(profile))
            {
                profile = null;
                return false;
            }
            if (!TryApplyDynamicsParameters(profile))
            {
                profile = null;
                return false;
            }
            if (!TryApplyMissionOriginToTemplate())
            {
                profile = null;
                return false;
            }
            profile.OriginLongitudeDeg = simulationProfileTemplate.OriginLongitudeDeg;
            profile.OriginLatitudeDeg = simulationProfileTemplate.OriginLatitudeDeg;
            if (!TryApplyOceanCurrentFieldSettings(profile))
            {
                profile = null;
                return false;
            }
            return true;
        }

        private bool TryApplyOceanCurrentFieldSettings(SimulationProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            if (!TryParseFloat(oceanCurrentPrefetchHalfWidthInput, "海流区域半宽", 0f, 250f, out var halfWidthKm)
                || !TryParseFloat(oceanCurrentForecastWindowInput, "海流预报时窗", 0f, 168f, out var forecastHours))
            {
                return false;
            }

            profile.OceanCurrentPrefetchHalfWidthKm = halfWidthKm;
            profile.OceanCurrentForecastWindowHours = forecastHours;
            return true;
        }

        private bool TryApplyMissionOriginToTemplate()
        {
            simulationProfileTemplate ??= SimulationProfile.Default;
            var longitudeText = missionLongitudeInput != null
                ? missionLongitudeInput.text
                : simulationProfileTemplate.OriginLongitudeDeg.ToString(CultureInfo.InvariantCulture);
            var latitudeText = missionLatitudeInput != null
                ? missionLatitudeInput.text
                : simulationProfileTemplate.OriginLatitudeDeg.ToString(CultureInfo.InvariantCulture);
            if (!double.TryParse(longitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
                || !double.TryParse(latitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
            {
                SetStatus("起点经纬度格式无效。", new Color(1f, 0.58f, 0.58f));
                return false;
            }

            if (!MissionGeoReference.TryCreate(longitude, latitude, out var reference, out var coordinateError))
            {
                SetStatus(coordinateError, new Color(1f, 0.58f, 0.58f));
                return false;
            }

            simulationProfileTemplate.OriginLongitudeDeg = reference.LongitudeDeg;
            simulationProfileTemplate.OriginLatitudeDeg = reference.LatitudeDeg;
            return true;
        }

        private OceanCurrentProfile GetOceanCurrentProfile()
        {
            if (simulationProfileTemplate == null)
            {
                simulationProfileTemplate = SimulationProfile.Default;
            }

            simulationProfileTemplate.OceanCurrentProfile ??= new OceanCurrentProfile();
            return simulationProfileTemplate.OceanCurrentProfile;
        }

        private void AddOceanCurrentLayer()
        {
            var profile = GetOceanCurrentProfile();
            profile.AddLayer(new OceanCurrentLayer());
            selectedOceanCurrentLayerIndex = profile.Layers.Count - 1;
            lastOceanCurrentResult = null;
            RefreshOceanCurrentLayerEditor();
        }

        private void SaveOceanCurrentLayer()
        {
            if (!TryParseFloat(oceanCurrentMinDepthInput, "海流最小深度", out var minimumDepth)
                || !TryParseFloat(oceanCurrentMaxDepthInput, "海流最大深度", out var maximumDepth)
                || !TryParseFloat(oceanCurrentEastwardInput, "东向海流", out var eastward)
                || !TryParseFloat(oceanCurrentNorthwardInput, "北向海流", out var northward))
            {
                return;
            }

            var profile = GetOceanCurrentProfile();
            if (selectedOceanCurrentLayerIndex < 0 || selectedOceanCurrentLayerIndex >= profile.Layers.Count)
            {
                profile.AddLayer(new OceanCurrentLayer(minimumDepth, maximumDepth, eastward, northward));
                selectedOceanCurrentLayerIndex = profile.Layers.Count - 1;
            }
            else
            {
                var layer = profile.Layers[selectedOceanCurrentLayerIndex];
                layer.MinDepthM = minimumDepth;
                layer.MaxDepthM = maximumDepth;
                layer.EastwardMps = eastward;
                layer.NorthwardMps = northward;
            }

            lastOceanCurrentResult = null;
            simulationProfileTemplate.OceanCurrentSourcePreference = OceanCurrentSourcePreference.LayeredPreferred;
            RefreshOceanCurrentLayerEditor();
            oceanCurrentSettingsApplied?.Invoke();
        }

        private void DeleteOceanCurrentLayer()
        {
            var profile = GetOceanCurrentProfile();
            if (profile.RemoveLayerAt(selectedOceanCurrentLayerIndex))
            {
                selectedOceanCurrentLayerIndex = Mathf.Min(selectedOceanCurrentLayerIndex, profile.Layers.Count - 1);
                lastOceanCurrentResult = null;
            }

            RefreshOceanCurrentLayerEditor();
            oceanCurrentSettingsApplied?.Invoke();
        }

        private void SelectOceanCurrentLayer(int index)
        {
            var layers = GetOceanCurrentProfile().Layers;
            if (index >= 0 && index < layers.Count)
            {
                selectedOceanCurrentLayerIndex = index;
                RefreshOceanCurrentLayerEditor();
            }
        }

        private void LookupOceanCurrent()
        {
            if (!TryParseFloat(simulationDepthInput, "目标深度", out var targetDepth))
            {
                return;
            }
            if (!TryApplyMissionOriginToTemplate())
            {
                return;
            }

            var profile = simulationProfileTemplate ?? SimulationProfile.Default;
            if (!TryApplyOceanCurrentFieldSettings(profile))
            {
                return;
            }

            var request = new CopernicusCurrentRequest(
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                0f,
                Mathf.Max(0f, targetDepth),
                profile.OceanCurrentPrefetchHalfWidthKm,
                profile.OceanCurrentForecastWindowHours);
            SetStatus("正在从 Copernicus 获取海流...", new Color(0.62f, 0.85f, 0.92f));
            StartCoroutine(new CopernicusCurrentClient().Fetch(
                request,
                result =>
                {
                    var downloadedProfile = result.Profile?.Clone();
                    var downloadedField = result.Field?.Clone();
                    if (downloadedProfile == null || downloadedField == null)
                    {
                        SetStatus("海流下载失败：响应不完整，可重试", new Color(1f, 0.58f, 0.58f));
                        return;
                    }

                    profile.OceanCurrentProfile = downloadedProfile;
                    profile.OceanCurrentField = downloadedField;
                    profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
                    selectedOceanCurrentLayerIndex = GetOceanCurrentProfile().Layers.Count > 0 ? 0 : -1;
                    lastOceanCurrentResult = result;
                    RefreshOceanCurrentLayerEditor();
                    oceanCurrentSettingsApplied?.Invoke();
                    SetCurrentLookupStatus(result.Profile, targetDepth, SetStatus);
                },
                error => SetStatus(BuildCurrentLookupFailureMessage(profile, error), new Color(1f, 0.58f, 0.58f)),
                message => SetStatus(message, new Color(0.62f, 0.85f, 0.92f))));
        }

        public static string BuildCurrentLookupFailureMessage(SimulationProfile profile, string error)
        {
            var reason = string.IsNullOrWhiteSpace(error) ? "未知错误" : error;
            if (profile?.OceanCurrentSourcePreference == OceanCurrentSourcePreference.NetworkPreferred
                && profile.OceanCurrentField?.Samples.Count > 0)
            {
                return $"海流下载失败：保留当前联网格点场，可重试（{reason}）";
            }

            var layerCount = profile?.OceanCurrentProfile?.Layers.Count ?? 0;
            return layerCount > 0
                ? $"海流下载失败：保留当前 {layerCount} 层分层场，可重试（{reason}）"
                : $"海流下载失败：当前没有可用海流数据，可重试（{reason}）";
        }

        private void RefreshOceanCurrentLayerEditor()
        {
            var layers = GetOceanCurrentProfile().Layers;
            if (selectedOceanCurrentLayerIndex >= 0 && selectedOceanCurrentLayerIndex < layers.Count)
            {
                var layer = layers[selectedOceanCurrentLayerIndex];
                oceanCurrentMinDepthInput.text = layer.MinDepthM.ToString("0.###", CultureInfo.InvariantCulture);
                oceanCurrentMaxDepthInput.text = layer.MaxDepthM.ToString("0.###", CultureInfo.InvariantCulture);
                oceanCurrentEastwardInput.text = layer.EastwardMps.ToString("0.###", CultureInfo.InvariantCulture);
                oceanCurrentNorthwardInput.text = layer.NorthwardMps.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (oceanCurrentLayerSummary != null)
            {
                oceanCurrentLayerSummary.text = selectedOceanCurrentLayerIndex >= 0
                    ? $"海流层：{selectedOceanCurrentLayerIndex + 1}/{layers.Count}  {FormatOceanCurrentCoverage(layers)}"
                    : "海流层：0";
            }

            RefreshOceanCurrentDrawer();
        }

        private void BuildOceanCurrentDrawer(Transform canvas)
        {
            oceanCurrentDrawer = UiFactory.Panel("OceanCurrentDrawerPanel", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 600f), new Color(0.015f, 0.075f, 0.1f, 0.98f));
            UiFactory.Text("OceanCurrentDrawerTitle", oceanCurrentDrawer, "海流配置", 20, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -26f), new Vector2(260f, 30f));
            UiFactory.Button("OceanCurrentDrawerCloseButton", oceanCurrentDrawer, "关闭", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -24f), new Vector2(72f, 28f)).onClick.AddListener(ToggleOceanCurrentDrawer);
            oceanCurrentDrawerSummary = UiFactory.Text("OceanCurrentDrawerSummary", oceanCurrentDrawer, "海流层：0", 13, TextAnchor.MiddleLeft, new Color(0.74f, 0.95f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -66f), new Vector2(300f, 24f));
            oceanCurrentQualitySummary = UiFactory.Text("OceanCurrentQualitySummary", oceanCurrentDrawer, "质量：无有效海流层", 13, TextAnchor.MiddleLeft, new Color(1f, 0.68f, 0.4f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f, -66f), new Vector2(400f, 24f));

            oceanCurrentDrawerMinDepthInput = AddLabeledInput(oceanCurrentDrawer, "最小深度 (m)", "OceanCurrentDrawerMinDepthInput", "0", 24f, -108f, 150f);
            oceanCurrentDrawerMaxDepthInput = AddLabeledInput(oceanCurrentDrawer, "最大深度 (m)", "OceanCurrentDrawerMaxDepthInput", "0", 194f, -108f, 150f);
            oceanCurrentDrawerEastwardInput = AddLabeledInput(oceanCurrentDrawer, "东向流速 (m/s)", "OceanCurrentDrawerEastwardInput", "0", 364f, -108f, 150f);
            oceanCurrentDrawerNorthwardInput = AddLabeledInput(oceanCurrentDrawer, "北向流速 (m/s)", "OceanCurrentDrawerNorthwardInput", "0", 534f, -108f, 150f);
            oceanCurrentPrefetchHalfWidthInput = AddLabeledInput(oceanCurrentDrawer, "区域半宽 (km)", "OceanCurrentPrefetchHalfWidthInput", "25", 24f, -174f, 150f);
            oceanCurrentForecastWindowInput = AddLabeledInput(oceanCurrentDrawer, "预报时窗 (h)", "OceanCurrentForecastWindowInput", "72", 194f, -174f, 150f);
            oceanCurrentFieldSummary = UiFactory.Text("OceanCurrentFieldSummary", oceanCurrentDrawer, "网格：未加载", 13, TextAnchor.MiddleLeft, new Color(0.74f, 0.95f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(364f, -181f), new Vector2(360f, 24f));

            UiFactory.Button("OceanCurrentDrawerPreviousButton", oceanCurrentDrawer, "上一层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -246f), new Vector2(88f, 32f)).onClick.AddListener(() => SelectOceanCurrentLayer(selectedOceanCurrentLayerIndex - 1));
            UiFactory.Button("OceanCurrentDrawerNextButton", oceanCurrentDrawer, "下一层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -246f), new Vector2(88f, 32f)).onClick.AddListener(() => SelectOceanCurrentLayer(selectedOceanCurrentLayerIndex + 1));
            UiFactory.Button("OceanCurrentDrawerAddButton", oceanCurrentDrawer, "新增层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(216f, -246f), new Vector2(88f, 32f)).onClick.AddListener(AddOceanCurrentLayer);
            UiFactory.Button("OceanCurrentDrawerDeleteButton", oceanCurrentDrawer, "删除层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(312f, -246f), new Vector2(88f, 32f)).onClick.AddListener(DeleteOceanCurrentLayer);
            UiFactory.Button("OceanCurrentDrawerSaveButton", oceanCurrentDrawer, "保存当前层", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(408f, -246f), new Vector2(110f, 32f)).onClick.AddListener(SaveOceanCurrentDrawerLayer);
            UiFactory.Button("OceanCurrentDrawerLookupButton", oceanCurrentDrawer, "联网获取海流", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530f, -246f), new Vector2(136f, 32f)).onClick.AddListener(LookupOceanCurrentFromDrawer);
            UiFactory.Text("DynamicsPresetLabel", oceanCurrentDrawer, "动力学预设", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -300f), new Vector2(120f, 24f));
            UiFactory.Button("DynamicsSeaTrialPresetButton", oceanCurrentDrawer, "海试近似", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(154f, -300f), new Vector2(96f, 30f)).onClick.AddListener(ApplySeaTrialDynamicsPreset);
            UiFactory.Button("DynamicsCalmWaterPresetButton", oceanCurrentDrawer, "静水基准", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, -300f), new Vector2(96f, 30f)).onClick.AddListener(ApplyCalmWaterDynamicsPreset);
            UiFactory.Button("DynamicsCalibrateFromCsvButton", oceanCurrentDrawer, "CSV 海试标定", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(366f, -300f), new Vector2(112f, 30f)).onClick.AddListener(CalibrateDynamicsFromCsv);
            UiFactory.Text("DynamicsParametersTitle", oceanCurrentDrawer, "动力学参数", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -340f), new Vector2(150f, 24f));
            dynamicsMassInput = AddLabeledInput(oceanCurrentDrawer, "质量 (kg)", "DynamicsMassInput", "52", 24f, -354f, 132f);
            dynamicsReferenceAreaInput = AddLabeledInput(oceanCurrentDrawer, "参考面积 (m²)", "DynamicsReferenceAreaInput", "0.22", 172f, -354f, 132f);
            dynamicsReferenceLengthInput = AddLabeledInput(oceanCurrentDrawer, "机长 (m)", "DynamicsReferenceLengthInput", "4.6", 320f, -354f, 132f);
            dynamicsWingSpanInput = AddLabeledInput(oceanCurrentDrawer, "翼展 (m)", "DynamicsWingSpanInput", "2.4", 468f, -354f, 132f);
            dynamicsMeanChordInput = AddLabeledInput(oceanCurrentDrawer, "平均弦长 (m)", "DynamicsMeanChordInput", "0.32", 616f, -354f, 132f);
            dynamicsRollInertiaInput = AddLabeledInput(oceanCurrentDrawer, "滚转惯量 (kg·m²)", "DynamicsRollInertiaInput", "28", 24f, -424f, 168f);
            dynamicsPitchInertiaInput = AddLabeledInput(oceanCurrentDrawer, "俯仰惯量 (kg·m²)", "DynamicsPitchInertiaInput", "42", 204f, -424f, 168f);
            dynamicsYawInertiaInput = AddLabeledInput(oceanCurrentDrawer, "偏航惯量 (kg·m²)", "DynamicsYawInertiaInput", "54", 384f, -424f, 168f);
            dynamicsLiftSlopeInput = AddLabeledInput(oceanCurrentDrawer, "升力斜率 (/rad)", "DynamicsLiftSlopeInput", "2.8", 564f, -424f, 168f);
            dynamicsBaseDragInput = AddLabeledInput(oceanCurrentDrawer, "基础阻力系数", "DynamicsBaseDragInput", "0.32", 24f, -494f, 168f);
            dynamicsTurnaroundDurationInput = AddLabeledInput(oceanCurrentDrawer, "转向过渡 (s)", "DynamicsTurnaroundDurationInput", "360", 204f, -494f, 168f);
            oceanCurrentDrawerStatus = UiFactory.Text("OceanCurrentDrawerStatus", oceanCurrentDrawer, "手动配置或按起点经纬度获取在线海流。", 13, TextAnchor.MiddleLeft, new Color(0.78f, 0.92f, 0.98f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(-44f, 48f));
            oceanCurrentDrawer.gameObject.SetActive(false);
        }

        private void ToggleOceanCurrentDrawer()
        {
            if (oceanCurrentDrawer == null)
            {
                return;
            }

            var visible = !oceanCurrentDrawer.gameObject.activeSelf;
            oceanCurrentDrawer.gameObject.SetActive(visible);
            if (visible)
            {
                RefreshOceanCurrentDrawer();
            }
        }

        private void BuildFlightLegDrawer(Transform canvas)
        {
            flightLegDrawer = UiFactory.Panel("FlightLegDrawerPanel", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 350f), new Color(0.015f, 0.075f, 0.1f, 0.98f));
            UiFactory.Text("FlightLegDrawerTitle", flightLegDrawer, "航段参数", 20, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -26f), new Vector2(200f, 30f));
            UiFactory.Button("FlightLegDrawerCloseButton", flightLegDrawer, "关闭", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -24f), new Vector2(72f, 28f)).onClick.AddListener(ToggleFlightLegDrawer);
            UiFactory.Button("FlightLegRestoreDefaultsButton", flightLegDrawer, "恢复默认", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -24f), new Vector2(92f, 28f)).onClick.AddListener(RestoreFlightLegDefaults);
            UiFactory.Text("DescentLegTitle", flightLegDrawer, "下潜航段（覆盖默认值）", 14, TextAnchor.MiddleLeft, new Color(0.3f, 0.92f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -70f), new Vector2(210f, 24f));
            descentNetBuoyancyInput = AddLabeledInput(flightLegDrawer, "净浮力 (N)", "DescentNetBuoyancyInput", "0", 24f, -104f, 180f);
            descentPitchInput = AddLabeledInput(flightLegDrawer, "俯仰 (°)", "DescentPitchInput", "0", 232f, -104f, 180f);
            descentRollInput = AddLabeledInput(flightLegDrawer, "横滚 (°)", "DescentRollInput", "0", 440f, -104f, 180f);
            UiFactory.Text("AscentLegTitle", flightLegDrawer, "上浮航段（覆盖默认值）", 14, TextAnchor.MiddleLeft, new Color(0.4f, 1f, 0.62f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -190f), new Vector2(210f, 24f));
            ascentNetBuoyancyInput = AddLabeledInput(flightLegDrawer, "净浮力 (N)", "AscentNetBuoyancyInput", "0", 24f, -224f, 180f);
            ascentPitchInput = AddLabeledInput(flightLegDrawer, "俯仰 (°)", "AscentPitchInput", "0", 232f, -224f, 180f);
            ascentRollInput = AddLabeledInput(flightLegDrawer, "横滚 (°)", "AscentRollInput", "0", 440f, -224f, 180f);
            descentNetBuoyancyInput.onValueChanged.AddListener(_ => MarkFlightLegSettingsEdited());
            ascentNetBuoyancyInput.onValueChanged.AddListener(_ => MarkFlightLegSettingsEdited());
            descentPitchInput.onValueChanged.AddListener(_ => MarkFlightLegSettingsEdited());
            ascentPitchInput.onValueChanged.AddListener(_ => MarkFlightLegSettingsEdited());
            descentRollInput.onValueChanged.AddListener(_ => MarkFlightLegSettingsEdited());
            ascentRollInput.onValueChanged.AddListener(_ => MarkFlightLegSettingsEdited());
            flightLegDrawerStatus = UiFactory.Text("FlightLegDrawerStatus", flightLegDrawer, "编辑任一项会覆盖默认值；恢复默认后继承默认俯仰与横滚。净浮力：上浮为正、下潜为负。", 12, TextAnchor.MiddleLeft, new Color(0.78f, 0.92f, 0.98f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(-44f, 40f));
            RefreshFlightLegDrawer();
            flightLegDrawer.gameObject.SetActive(false);
        }

        private void ToggleFlightLegDrawer()
        {
            if (flightLegDrawer == null)
            {
                return;
            }

            var visible = !flightLegDrawer.gameObject.activeSelf;
            flightLegDrawer.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (oceanCurrentDrawer != null)
            {
                oceanCurrentDrawer.gameObject.SetActive(false);
            }

            RefreshFlightLegDrawer();
        }

        private void RefreshFlightLegDrawer()
        {
            if (flightLegDrawer == null)
            {
                return;
            }

            var profile = simulationProfileTemplate ?? SimulationProfile.Default;
            refreshingFlightLegInputs = true;
            var maxBuoyancy = Mathf.Max(0.1f, profile.Dynamics?.MaxBuoyancyForceN ?? GliderDynamicsProfile.Default.MaxBuoyancyForceN);
            descentNetBuoyancyInput.text = profile.ResolveDescentNetBuoyancyForceN(-maxBuoyancy * 0.7f).ToString("0.##", CultureInfo.InvariantCulture);
            ascentNetBuoyancyInput.text = profile.ResolveAscentNetBuoyancyForceN(maxBuoyancy * 0.7f).ToString("0.##", CultureInfo.InvariantCulture);
            descentPitchInput.text = profile.ResolveDescentPitchDeg().ToString("0.##", CultureInfo.InvariantCulture);
            ascentPitchInput.text = profile.ResolveAscentPitchDeg().ToString("0.##", CultureInfo.InvariantCulture);
            descentRollInput.text = profile.ResolveDescentRollDeg().ToString("0.##", CultureInfo.InvariantCulture);
            ascentRollInput.text = profile.ResolveAscentRollDeg().ToString("0.##", CultureInfo.InvariantCulture);
            refreshingFlightLegInputs = false;
        }

        private void RestoreFlightLegDefaults()
        {
            simulationProfileTemplate ??= SimulationProfile.Default;
            simulationProfileTemplate.DescentNetBuoyancyForceN = float.NaN;
            simulationProfileTemplate.AscentNetBuoyancyForceN = float.NaN;
            simulationProfileTemplate.DescentPitchDeg = float.NaN;
            simulationProfileTemplate.AscentPitchDeg = float.NaN;
            simulationProfileTemplate.DescentRollDeg = float.NaN;
            simulationProfileTemplate.AscentRollDeg = float.NaN;
            flightLegSettingsEdited = false;
            RefreshFlightLegDrawer();
        }

        private bool TryApplyFlightLegParameters(SimulationProfile profile)
        {
            if (!flightLegSettingsEdited)
            {
                profile.DescentNetBuoyancyForceN = float.NaN;
                profile.AscentNetBuoyancyForceN = float.NaN;
                profile.DescentPitchDeg = float.NaN;
                profile.AscentPitchDeg = float.NaN;
                profile.DescentRollDeg = float.NaN;
                profile.AscentRollDeg = float.NaN;
                return true;
            }

            if (!TryParseFloat(descentNetBuoyancyInput, "下潜净浮力", out var descentNetBuoyancy)
                || !TryParseFloat(ascentNetBuoyancyInput, "上浮净浮力", out var ascentNetBuoyancy)
                || !TryParseFloat(descentPitchInput, "下潜俯仰", out var descentPitch)
                || !TryParseFloat(ascentPitchInput, "上浮俯仰", out var ascentPitch)
                || !TryParseFloat(descentRollInput, "下潜横滚", out var descentRoll)
                || !TryParseFloat(ascentRollInput, "上浮横滚", out var ascentRoll))
            {
                return false;
            }

            profile.DescentNetBuoyancyForceN = descentNetBuoyancy;
            profile.AscentNetBuoyancyForceN = ascentNetBuoyancy;
            profile.DescentPitchDeg = descentPitch;
            profile.AscentPitchDeg = ascentPitch;
            profile.DescentRollDeg = descentRoll;
            profile.AscentRollDeg = ascentRoll;
            return true;
        }

        private void MarkFlightLegSettingsEdited()
        {
            if (!refreshingFlightLegInputs)
            {
                flightLegSettingsEdited = true;
            }
        }

        private void RefreshOceanCurrentDrawer()
        {
            if (oceanCurrentDrawer == null)
            {
                return;
            }

            var layers = GetOceanCurrentProfile().Layers;
            if (selectedOceanCurrentLayerIndex >= 0 && selectedOceanCurrentLayerIndex < layers.Count)
            {
                var layer = layers[selectedOceanCurrentLayerIndex];
                oceanCurrentDrawerMinDepthInput.text = layer.MinDepthM.ToString("0.###", CultureInfo.InvariantCulture);
                oceanCurrentDrawerMaxDepthInput.text = layer.MaxDepthM.ToString("0.###", CultureInfo.InvariantCulture);
                oceanCurrentDrawerEastwardInput.text = layer.EastwardMps.ToString("0.###", CultureInfo.InvariantCulture);
                oceanCurrentDrawerNorthwardInput.text = layer.NorthwardMps.ToString("0.###", CultureInfo.InvariantCulture);
            }

            var profile = simulationProfileTemplate ?? SimulationProfile.Default;
            oceanCurrentPrefetchHalfWidthInput.text = profile.OceanCurrentPrefetchHalfWidthKm.ToString("0.###", CultureInfo.InvariantCulture);
            oceanCurrentForecastWindowInput.text = profile.OceanCurrentForecastWindowHours.ToString("0.###", CultureInfo.InvariantCulture);
            if (oceanCurrentFieldSummary != null)
            {
                var sampleCount = profile.OceanCurrentField?.Samples.Count ?? 0;
                oceanCurrentFieldSummary.text = sampleCount > 0
                    ? $"网格：{sampleCount} 样本 | ±{profile.OceanCurrentPrefetchHalfWidthKm:0.#} km | {profile.OceanCurrentForecastWindowHours:0.#} h"
                    : $"网格：未加载 | ±{profile.OceanCurrentPrefetchHalfWidthKm:0.#} km | {profile.OceanCurrentForecastWindowHours:0.#} h";
            }

            oceanCurrentDrawerSummary.text = selectedOceanCurrentLayerIndex >= 0
                ? $"当前层：{selectedOceanCurrentLayerIndex + 1}/{layers.Count}  {FormatOceanCurrentCoverage(layers)}"
                : "当前层：0";
            RefreshOceanCurrentQualitySummary();
            RefreshDynamicsEditor();
        }

        private static string FormatOceanCurrentCoverage(System.Collections.Generic.IReadOnlyList<OceanCurrentLayer> layers)
        {
            var profile = new OceanCurrentProfile(layers);
            return profile.TryGetDepthCoverage(out var minimumDepth, out var maximumDepth)
                ? $"覆盖 {minimumDepth:0.#}-{maximumDepth:0.#}m"
                : "无有效深度覆盖";
        }

        private void RefreshOceanCurrentQualitySummary()
        {
            if (oceanCurrentQualitySummary == null)
            {
                return;
            }

            var requestedDepth = simulationProfileTemplate?.TargetDepthM ?? 0f;
            if (simulationDepthInput != null
                && float.TryParse(simulationDepthInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var inputDepth))
            {
                requestedDepth = Mathf.Max(0f, inputDepth);
            }

            var report = OceanCurrentQualityEvaluator.Evaluate(
                GetOceanCurrentProfile(),
                0f,
                requestedDepth,
                lastOceanCurrentResult,
                DateTime.UtcNow);
            oceanCurrentQualitySummary.text = FormatOceanCurrentQuality(report);
            oceanCurrentQualitySummary.color = GetOceanCurrentQualityColor(report.Level);
        }

        private static string FormatOceanCurrentQuality(OceanCurrentQualityReport report)
        {
            switch (report.Level)
            {
                case OceanCurrentQualityLevel.Ready:
                {
                    var source = report.IsCached ? "缓存" : "在线/手动";
                    return $"质量：可用 | {source} | 覆盖任务 0-{report.RequestedMaximumDepthM:0.#} m";
                }
                case OceanCurrentQualityLevel.Stale:
                    return $"质量：数据已过期 {report.AgeHours:0.#} h | 建议刷新";
                case OceanCurrentQualityLevel.PartialCoverage:
                {
                    var issue = report.HasCoverageGaps ? "存在深度断层" : "深度不足";
                    return $"质量：{issue} | 仅 {report.MinimumDepthM:0.#}-{report.MaximumDepthM:0.#} m";
                }
                default:
                    return "质量：无有效海流层 | 请联网获取或手动配置";
            }
        }

        private static Color GetOceanCurrentQualityColor(OceanCurrentQualityLevel level)
        {
            switch (level)
            {
                case OceanCurrentQualityLevel.Ready:
                    return new Color(0.58f, 0.94f, 0.74f);
                case OceanCurrentQualityLevel.Stale:
                    return new Color(1f, 0.76f, 0.3f);
                case OceanCurrentQualityLevel.PartialCoverage:
                    return new Color(1f, 0.64f, 0.36f);
                default:
                    return new Color(1f, 0.58f, 0.58f);
            }
        }

        private static void SetCurrentLookupStatus(OceanCurrentProfile profile, float requestedDepthM, Action<string, Color> status)
        {
            if (profile == null || !profile.TryGetDepthCoverage(out var minimumDepth, out var maximumDepth))
            {
                status?.Invoke("Copernicus 未返回可用海流深度层。", new Color(1f, 0.58f, 0.58f));
                return;
            }

            var summary = $"已加载 {profile.Layers.Count} 层，实际覆盖 {minimumDepth:0.#}-{maximumDepth:0.#}m。";
            if (maximumDepth + 0.5f < requestedDepthM)
            {
                status?.Invoke(summary + " 该位置深层数据不足，仿真将只使用已返回层。", new Color(1f, 0.76f, 0.3f));
                return;
            }

            status?.Invoke(summary, new Color(0.74f, 0.95f, 1f));
        }

        private void SaveOceanCurrentDrawerLayer()
        {
            if (!TryParseFloat(oceanCurrentDrawerMinDepthInput, "海流最小深度", out var minimumDepth)
                || !TryParseFloat(oceanCurrentDrawerMaxDepthInput, "海流最大深度", out var maximumDepth)
                || !TryParseFloat(oceanCurrentDrawerEastwardInput, "东向海流", out var eastward)
                || !TryParseFloat(oceanCurrentDrawerNorthwardInput, "北向海流", out var northward))
            {
                return;
            }

            var layers = GetOceanCurrentProfile().Layers;
            if (selectedOceanCurrentLayerIndex < 0 || selectedOceanCurrentLayerIndex >= layers.Count)
            {
                GetOceanCurrentProfile().AddLayer(new OceanCurrentLayer(minimumDepth, maximumDepth, eastward, northward));
                selectedOceanCurrentLayerIndex = GetOceanCurrentProfile().Layers.Count - 1;
            }
            else
            {
                var layer = layers[selectedOceanCurrentLayerIndex];
                layer.MinDepthM = minimumDepth;
                layer.MaxDepthM = maximumDepth;
                layer.EastwardMps = eastward;
                layer.NorthwardMps = northward;
            }

            lastOceanCurrentResult = null;
            SetOceanCurrentDrawerStatus("当前海流层已保存。", new Color(0.74f, 0.95f, 1f));
            RefreshOceanCurrentLayerEditor();
        }

        private void LookupOceanCurrentFromDrawer()
        {
            if (!TryParseFloat(simulationDepthInput, "目标深度", out var targetDepth))
            {
                return;
            }
            if (!TryApplyMissionOriginToTemplate())
            {
                return;
            }

            var profile = simulationProfileTemplate ?? SimulationProfile.Default;
            if (!TryApplyOceanCurrentFieldSettings(profile))
            {
                return;
            }

            var request = new CopernicusCurrentRequest(
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                0f,
                Mathf.Max(0f, targetDepth),
                profile.OceanCurrentPrefetchHalfWidthKm,
                profile.OceanCurrentForecastWindowHours);
            SetOceanCurrentDrawerStatus("正在从 Copernicus 获取海流...", new Color(0.62f, 0.85f, 0.92f));
            StartCoroutine(new CopernicusCurrentClient().Fetch(
                request,
                result =>
                {
                    GetOceanCurrentProfile().ReplaceLayers(result.Profile.Layers);
                    profile.OceanCurrentField = result.Field.Clone();
                    selectedOceanCurrentLayerIndex = GetOceanCurrentProfile().Layers.Count > 0 ? 0 : -1;
                    lastOceanCurrentResult = result;
                    SetCurrentLookupStatus(result.Profile, targetDepth, SetOceanCurrentDrawerStatus);
                    RefreshOceanCurrentLayerEditor();
                },
                error => SetOceanCurrentDrawerStatus($"获取失败：{error}", new Color(1f, 0.58f, 0.58f)),
                message => SetOceanCurrentDrawerStatus(message, new Color(0.62f, 0.85f, 0.92f))));
        }

        private void ApplySeaTrialDynamicsPreset()
        {
            var dynamics = GetDynamicsProfile();
            dynamics.PresetName = "Sea Trial Approximation";
            dynamics.TurbulenceMps = 0.02f;
            dynamics.CurrentSideSlipGain = 0.85f;
            dynamics.BuoyancyResponseSeconds = 18f;
            RefreshDynamicsEditor();
            SetOceanCurrentDrawerStatus("已应用海试近似预设。", new Color(0.74f, 0.95f, 1f));
        }

        private void ApplyCalmWaterDynamicsPreset()
        {
            var dynamics = GetDynamicsProfile();
            dynamics.PresetName = "Calm Water Baseline";
            dynamics.TurbulenceMps = 0f;
            dynamics.CurrentSideSlipGain = 0.35f;
            dynamics.BuoyancyResponseSeconds = 12f;
            RefreshDynamicsEditor();
            SetOceanCurrentDrawerStatus("已应用静水基准预设。", new Color(0.74f, 0.95f, 1f));
        }

        private void CalibrateDynamicsFromCsv()
        {
            var path = csvPathInput != null ? csvPathInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                SetOceanCurrentDrawerStatus("请先输入有效的海试 CSV 路径。", new Color(1f, 0.58f, 0.58f));
                return;
            }

            var loadResult = new CsvTelemetrySource(path).Load();
            if (!SeaTrialCalibrator.TryCalibrate(
                    loadResult.Frames,
                    GetOceanCurrentProfile(),
                    GetDynamicsProfile(),
                    out var calibration,
                    out var error))
            {
                SetOceanCurrentDrawerStatus($"CSV 海试标定失败：{error}", new Color(1f, 0.58f, 0.58f));
                return;
            }

            var dynamics = GetDynamicsProfile();
            dynamics.CruiseSpeedMps = calibration.MedianWaterSpeedMps;
            simulationProfileTemplate.HorizontalSpeedMps = calibration.MedianWaterSpeedMps;
            simulationProfileTemplate.PitchAmplitudeDeg = calibration.RecommendedPitchAmplitudeDeg;
            simulationProfileTemplate.RollAmplitudeDeg = calibration.RecommendedRollAmplitudeDeg;
            simulationPitchInput.text = calibration.RecommendedPitchAmplitudeDeg.ToString("0.#", CultureInfo.InvariantCulture);
            simulationRollInput.text = calibration.RecommendedRollAmplitudeDeg.ToString("0.#", CultureInfo.InvariantCulture);
            RefreshDynamicsEditor();

            var coverage = calibration.CurrentCorrectionApplied
                ? $"海流覆盖至 {calibration.CurrentCoverageMaxDepthM:0.#} m"
                : "未使用海流修正";
            SetOceanCurrentDrawerStatus(
                $"标定完成：{calibration.SampleCount} 段，水速 {calibration.MedianWaterSpeedMps:0.###} m/s，RMSE {calibration.SpeedRmseBeforeMps:0.###}→{calibration.SpeedRmseAfterMps:0.###} m/s，{coverage}。",
                new Color(0.74f, 0.95f, 1f));
        }

        private bool TryApplyDynamicsParameters(SimulationProfile profile)
        {
            var dynamics = profile.Dynamics ??= GliderDynamicsProfile.Default;
            if (!TryParseFloat(dynamicsMassInput, "质量", 0.1f, 10000f, out var mass)
                || !TryParseFloat(dynamicsReferenceAreaInput, "参考面积", 0.001f, 100f, out var referenceArea)
                || !TryParseFloat(dynamicsReferenceLengthInput, "机长", 0.1f, 100f, out var referenceLength)
                || !TryParseFloat(dynamicsWingSpanInput, "翼展", 0.05f, 50f, out var wingSpan)
                || !TryParseFloat(dynamicsMeanChordInput, "平均弦长", 0.01f, 20f, out var meanChord)
                || !TryParseFloat(dynamicsRollInertiaInput, "滚转惯量", 0.001f, 100000f, out var rollInertia)
                || !TryParseFloat(dynamicsPitchInertiaInput, "俯仰惯量", 0.001f, 100000f, out var pitchInertia)
                || !TryParseFloat(dynamicsYawInertiaInput, "偏航惯量", 0.001f, 100000f, out var yawInertia)
                || !TryParseFloat(dynamicsLiftSlopeInput, "升力斜率", -20f, 20f, out var liftSlope)
                || !TryParseFloat(dynamicsBaseDragInput, "基础阻力系数", 0f, 20f, out var baseDrag)
                || !TryParseFloat(dynamicsTurnaroundDurationInput, "转向过渡时间", 10f, 1800f, out var turnaroundDuration))
            {
                return false;
            }

            dynamics.MassKg = mass;
            dynamics.ReferenceAreaM2 = referenceArea;
            dynamics.ReferenceLengthM = referenceLength;
            dynamics.WingSpanM = wingSpan;
            dynamics.MeanChordM = meanChord;
            dynamics.RollInertiaKgM2 = rollInertia;
            dynamics.PitchInertiaKgM2 = pitchInertia;
            dynamics.YawInertiaKgM2 = yawInertia;
            dynamics.LiftSlopePerRad = liftSlope;
            dynamics.BaseDragCoefficient = baseDrag;
            dynamics.TurnaroundDurationSeconds = turnaroundDuration;
            return true;
        }

        private void RefreshDynamicsEditor()
        {
            if (dynamicsMassInput == null)
            {
                return;
            }

            var dynamics = GetDynamicsProfile();
            dynamicsMassInput.text = dynamics.MassKg.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsReferenceAreaInput.text = dynamics.ReferenceAreaM2.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsReferenceLengthInput.text = dynamics.ReferenceLengthM.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsWingSpanInput.text = dynamics.WingSpanM.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsMeanChordInput.text = dynamics.MeanChordM.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsRollInertiaInput.text = dynamics.RollInertiaKgM2.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsPitchInertiaInput.text = dynamics.PitchInertiaKgM2.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsYawInertiaInput.text = dynamics.YawInertiaKgM2.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsLiftSlopeInput.text = dynamics.LiftSlopePerRad.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsBaseDragInput.text = dynamics.BaseDragCoefficient.ToString("0.###", CultureInfo.InvariantCulture);
            dynamicsTurnaroundDurationInput.text = dynamics.TurnaroundDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private GliderDynamicsProfile GetDynamicsProfile()
        {
            if (simulationProfileTemplate == null)
            {
                simulationProfileTemplate = SimulationProfile.Default;
            }

            simulationProfileTemplate.Dynamics ??= GliderDynamicsProfile.Default;
            return simulationProfileTemplate.Dynamics;
        }

        private void SetOceanCurrentDrawerStatus(string message, Color color)
        {
            if (oceanCurrentDrawerStatus != null)
            {
                oceanCurrentDrawerStatus.text = message;
                oceanCurrentDrawerStatus.color = color;
            }
        }

        private bool TryParseFloat(InputField inputField, string label, float minValue, float maxValue, out float value)
        {
            if (!float.TryParse(inputField.text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < minValue || value > maxValue)
            {
                SetStatus($"{label}必须在 {minValue:0.##} 到 {maxValue:0.##} 之间", new Color(1f, 0.58f, 0.58f));
                return false;
            }

            return true;
        }

        private bool TryParseInt(InputField inputField, string label, int minValue, int maxValue, out int value)
        {
            if (!int.TryParse(inputField.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < minValue || value > maxValue)
            {
                SetStatus($"{label}必须在 {minValue} 到 {maxValue} 之间", new Color(1f, 0.58f, 0.58f));
                return false;
            }

            return true;
        }

        private bool TryParseFloat(InputField inputField, string label, out float value)
        {
            if (!float.TryParse(inputField.text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                SetStatus($"{label}必须是数字", new Color(1f, 0.58f, 0.58f));
                return false;
            }

            return true;
        }

        private bool TryParseInt(InputField inputField, string label, out int value)
        {
            if (!int.TryParse(inputField.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                SetStatus($"{label}必须是整数", new Color(1f, 0.58f, 0.58f));
                return false;
            }

            return true;
        }

        private void CreateModelButton(Transform panel, PredictionModelKind modelKind, string label, float x)
        {
            var button = UiFactory.Button(label + "ModelButton", panel, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -96f), new Vector2(label.Length > 8 ? 108f : 86f, 34f));
            button.onClick.AddListener(() => SelectModel(modelKind));
            modelButtons[modelKind] = button;
        }

        private void EnsureRuntimeModelAvailable()
        {
            if (predictionController == null || predictionController.IsModelRuntimeAvailable(RuntimePredictionState.ModelKind))
            {
                return;
            }

            predictionController.SetModelKind(PredictionModelKind.XGBoost);
            RuntimePredictionState.SetModelKind(PredictionModelKind.XGBoost);
            initialPredictionStatus = "XGBoost 预测模型不可用";
        }

        private static InputField AddLabeledInput(Transform panel, string label, string inputName, string value, float x, float y, float width)
        {
            UiFactory.Text(inputName + "Label", panel, label, 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, 18f));
            return UiFactory.InputField(inputName, panel, value, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - 19f), new Vector2(width, 28f));
        }
    }
}
