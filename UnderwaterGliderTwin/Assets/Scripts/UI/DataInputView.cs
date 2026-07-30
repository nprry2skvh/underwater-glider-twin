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
    public sealed partial class DataInputView : MonoBehaviour
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
        private InputField oceanCurrentLocalFileInput;
        private Text oceanCurrentAcquisitionModeText;
        private Text oceanCurrentActualSourceText;
        private OceanCurrentAcquisitionMode oceanCurrentAcquisitionMode = OceanCurrentAcquisitionMode.Online;
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
        private InputField dynamicsBuoyancyExponentInput;
        private InputField dynamicsBuoyancyDeadbandInput;
        private InputField dynamicsPistonHysteresisInput;
        private InputField dynamicsRollExponentInput;
        private InputField dynamicsRollDeadbandInput;
        private InputField dynamicsRollRestoringGainInput;
        private InputField dynamicsMaxRollMomentInput;
        private InputField missionLongitudeInput;
        private InputField missionLatitudeInput;
        private Button predictionToggleButton;
        private Action<string> loadRequested;
        private Action<SimulationProfile> simulationRequested;
        private Action<SimulationProfile> oceanCurrentSettingsApplied;
        private PredictionController predictionController;
        private string initialPredictionStatus;
        private SimulationProfile simulationProfileTemplate;
        private int selectedOceanCurrentLayerIndex = -1;
        private CopernicusCurrentResult lastOceanCurrentResult;
        private bool flightLegSettingsEdited;
        private bool refreshingFlightLegInputs;
        private SimulationRuntimeSession subscribedRuntimeSession;
        private RectTransform configurationPanel;

        public void Initialize(
            string currentCsvPath,
            SimulationProfile currentProfile,
            PredictionController controller,
            Action<string> onLoadRequested = null,
            Action<SimulationProfile> onSimulationRequested = null,
            Action<SimulationProfile> onOceanCurrentSettingsApplied = null)
        {
            loadRequested = onLoadRequested;
            simulationRequested = onSimulationRequested;
            oceanCurrentSettingsApplied = onOceanCurrentSettingsApplied;
            predictionController = controller;
            currentProfile ??= SimulationProfile.Default;
            simulationProfileTemplate = currentProfile.Clone();
            flightLegSettingsEdited = HasExplicitFlightLegSettings(simulationProfileTemplate);
            selectedOceanCurrentLayerIndex = simulationProfileTemplate.OceanCurrentProfile?.Layers.Count > 0 ? 0 : -1;
            ClearRuntimeUi();
            EnsureRuntimeModelAvailable();

            var canvas = UiFactory.EnsureCanvas(transform);
            var header = UiFactory.EnsureCommandCenterHeader(canvas.transform);
            var panel = UiFactory.CommandPanel(
                "MissionConfigurationPanel",
                canvas.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 48f));
            configurationPanel = panel;
            var headerButton = header.Find("CommandCenterMissionConfigButton")?.GetComponent<Button>();
            if (headerButton == null)
            {
                headerButton = UiFactory.Button("CommandCenterMissionConfigButton", header, "任务参数",
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-332f, 0f), new Vector2(112f, 28f));
            }
            headerButton.onClick.RemoveAllListeners();
            headerButton.onClick.AddListener(ToggleConfigurationPanel);
            CreateConfigurationGroups(panel);

            UiFactory.Text("MissionConfigurationTitle", panel, "任务配置", 18, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -22f), new Vector2(300f, 28f));

            UiFactory.Text("CsvSourceLabel", panel, "CSV 数据源", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -58f), new Vector2(90f, 22f));
            csvPathInput = UiFactory.InputField("CsvPathInput", panel, currentCsvPath ?? RuntimeDataSourceState.LastCsvPath, "遥测 CSV 文件路径", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(114f, -52f), new Vector2(1118f, 34f));
            UiFactory.PrimaryButton("LoadCsvButton", panel, "加载 CSV", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1246f, -52f), new Vector2(116f, 34f)).onClick.AddListener(OnLoadClicked);

            UiFactory.Text("PredictionModelLabel", panel, "预测模型", 13, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -102f), new Vector2(120f, 22f));
            if (ShouldShowModelButton(PredictionModelKind.XGBoost))
            {
                CreateModelButton(panel, PredictionModelKind.XGBoost, "XGBoost", 146f);
            }

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
            ConfigureResponsiveBottomDrawer(panel);
            ConfigureOceanCurrentModalDrawer(oceanCurrentDrawer);
            ConfigureInlineDrawer(flightLegDrawer);
            AttachRuntimeSession(SimulationRuntimeRegistry.Active);
        }

        public void BringConfigurationToFront()
        {
            configurationPanel?.SetAsLastSibling();
        }

        private void ToggleConfigurationPanel()
        {
            if (configurationPanel == null)
            {
                return;
            }

            var visible = !configurationPanel.gameObject.activeSelf;
            configurationPanel.gameObject.SetActive(visible);
            if (visible)
            {
                SetBottomDrawerExpanded(false);
                BringConfigurationToFront();
            }
        }

        private void OnEnable()
        {
            SimulationRuntimeRegistry.ActiveChanged -= OnActiveRuntimeSessionChanged;
            SimulationRuntimeRegistry.ActiveChanged += OnActiveRuntimeSessionChanged;
            AttachRuntimeSession(SimulationRuntimeRegistry.Active);
        }

        private void OnDisable()
        {
            SimulationRuntimeRegistry.ActiveChanged -= OnActiveRuntimeSessionChanged;
            AttachRuntimeSession(null);
        }

        private void OnLoadClicked()
        {
            var path = csvPathInput != null ? csvPathInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("请输入 CSV 文件路径", new Color(1f, 0.5f, 0.5f));
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

        private bool TryParseFloat(InputField inputField, string label, float minValue, float maxValue, out float value)
        {
            if (!float.TryParse(inputField.text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.IsNaN(value) || float.IsInfinity(value) || value < minValue || value > maxValue)
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
            if (!float.TryParse(inputField.text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.IsNaN(value) || float.IsInfinity(value))
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

        private bool ShouldShowModelButton(PredictionModelKind modelKind)
        {
            return predictionController == null || predictionController.IsModelRuntimeAvailable(modelKind);
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

        private void ClearRuntimeUi()
        {
            modelButtons.Clear();
            bottomDrawerContent = null;
            bottomDrawerViewport = null;
            bottomDrawerScrollRect = null;
            bottomDrawerToggleButton = null;
            oceanCurrentModalCanvas = null;
            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                var child = transform.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static InputField AddLabeledInput(Transform panel, string label, string inputName, string value, float x, float y, float width)
        {
            var field = UiFactory.Panel(inputName + "Field", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, 48f), Color.clear);
            UiFactory.Text(inputName + "Label", field, label, 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(-8f, 18f));
            return UiFactory.InputField(inputName, field, value, label, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -20f), new Vector2(-8f, 28f));
        }
    }
}
