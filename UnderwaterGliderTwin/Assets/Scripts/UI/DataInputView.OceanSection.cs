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
    public sealed partial class DataInputView
    {
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
            if (!TryReadValidCurrentLayer(oceanCurrentMinDepthInput, oceanCurrentMaxDepthInput, oceanCurrentEastwardInput, oceanCurrentNorthwardInput,
                    out var minimumDepth, out var maximumDepth, out var eastward, out var northward))
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
            SetStatus(GetCurrentAcquisitionPendingText(), new Color(0.62f, 0.85f, 0.92f));
            StartCoroutine(new CopernicusCurrentClient().Fetch(
                request, oceanCurrentAcquisitionMode, GetLocalCurrentPath(), DateTime.UtcNow,
                result =>
                {
                    var downloadedProfile = result.Profile?.Clone();
                    var downloadedField = result.Field?.Clone();
                    if (!TryValidateCurrentResult(result, out var currentError))
                    {
                        SetStatus("海流下载失败：" + currentError + "，已保留当前海流。", new Color(1f, 0.58f, 0.58f));
                        return;
                    }

                    profile.OceanCurrentProfile = downloadedProfile;
                    profile.OceanCurrentField = downloadedField;
                    profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
                    selectedOceanCurrentLayerIndex = GetOceanCurrentProfile().Layers.Count > 0 ? 0 : -1;
                    lastOceanCurrentResult = result;
                    SetActualCurrentSource(result);
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

            UiFactory.Text("OceanCurrentAcquisitionLabel", oceanCurrentDrawer, "获取策略", 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 92f), new Vector2(70f, 20f));
            UiFactory.Button("OceanCurrentOnlineModeButton", oceanCurrentDrawer, "在线", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(100f, 92f), new Vector2(66f, 28f)).onClick.AddListener(() => SetOceanCurrentAcquisitionMode(OceanCurrentAcquisitionMode.Online));
            UiFactory.Button("OceanCurrentCacheOnlyModeButton", oceanCurrentDrawer, "仅缓存", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(174f, 92f), new Vector2(76f, 28f)).onClick.AddListener(() => SetOceanCurrentAcquisitionMode(OceanCurrentAcquisitionMode.CacheOnly));
            UiFactory.Button("OceanCurrentLocalFileModeButton", oceanCurrentDrawer, "本地文件", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(258f, 92f), new Vector2(86f, 28f)).onClick.AddListener(() => SetOceanCurrentAcquisitionMode(OceanCurrentAcquisitionMode.LocalFile));
            oceanCurrentAcquisitionModeText = UiFactory.Text("OceanCurrentAcquisitionMode", oceanCurrentDrawer, "策略：在线", 12, TextAnchor.MiddleLeft, new Color(0.74f, 0.95f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(356f, 92f), new Vector2(130f, 22f));
            oceanCurrentLocalFileInput = UiFactory.InputField("OceanCurrentLocalFileInput", oceanCurrentDrawer, string.Empty, "本地 JSON 或 NetCDF 路径", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 54f), new Vector2(460f, 30f));
            oceanCurrentActualSourceText = UiFactory.Text("OceanCurrentActualSource", oceanCurrentDrawer, "实际来源：未加载", 12, TextAnchor.MiddleLeft, new Color(0.74f, 0.95f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(494f, 54f), new Vector2(236f, 30f));

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
            if (!TryReadValidCurrentLayer(oceanCurrentDrawerMinDepthInput, oceanCurrentDrawerMaxDepthInput, oceanCurrentDrawerEastwardInput, oceanCurrentDrawerNorthwardInput,
                    out var minimumDepth, out var maximumDepth, out var eastward, out var northward))
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
            SetOceanCurrentDrawerStatus(GetCurrentAcquisitionPendingText(), new Color(0.62f, 0.85f, 0.92f));
            StartCoroutine(new CopernicusCurrentClient().Fetch(
                request, oceanCurrentAcquisitionMode, GetLocalCurrentPath(), DateTime.UtcNow,
                result =>
                {
                    if (!TryValidateCurrentResult(result, out var currentError))
                    {
                        SetOceanCurrentDrawerStatus("海流获取失败：" + currentError + "，已保留当前海流。", new Color(1f, 0.58f, 0.58f));
                        return;
                    }
                    GetOceanCurrentProfile().ReplaceLayers(result.Profile.Layers);
                    profile.OceanCurrentField = result.Field.Clone();
                    selectedOceanCurrentLayerIndex = GetOceanCurrentProfile().Layers.Count > 0 ? 0 : -1;
                    lastOceanCurrentResult = result;
                    SetActualCurrentSource(result);
                    SetCurrentLookupStatus(result.Profile, targetDepth, SetOceanCurrentDrawerStatus);
                    RefreshOceanCurrentLayerEditor();
                },
                error => SetOceanCurrentDrawerStatus($"获取失败：{error}", new Color(1f, 0.58f, 0.58f)),
                message => SetOceanCurrentDrawerStatus(message, new Color(0.62f, 0.85f, 0.92f))));
        }

        private void SetOceanCurrentDrawerStatus(string message, Color color)
        {
            if (oceanCurrentDrawerStatus != null)
            {
                oceanCurrentDrawerStatus.text = message;
                oceanCurrentDrawerStatus.color = color;
            }
        }

        private void SetOceanCurrentAcquisitionMode(OceanCurrentAcquisitionMode mode)
        {
            oceanCurrentAcquisitionMode = mode;
            if (oceanCurrentAcquisitionModeText != null)
            {
                oceanCurrentAcquisitionModeText.text = "策略：" + GetOceanCurrentAcquisitionModeLabel(mode);
            }
        }

        private string GetLocalCurrentPath()
        {
            return oceanCurrentLocalFileInput == null ? string.Empty : oceanCurrentLocalFileInput.text.Trim();
        }

        private string GetCurrentAcquisitionPendingText()
        {
            return "正在获取海流：" + GetOceanCurrentAcquisitionModeLabel(oceanCurrentAcquisitionMode) + "…";
        }

        private static string GetOceanCurrentAcquisitionModeLabel(OceanCurrentAcquisitionMode mode)
        {
            switch (mode)
            {
                case OceanCurrentAcquisitionMode.CacheOnly: return "仅缓存";
                case OceanCurrentAcquisitionMode.LocalFile: return "本地文件";
                default: return "在线";
            }
        }

        private void SetActualCurrentSource(CopernicusCurrentResult result)
        {
            if (oceanCurrentActualSourceText == null)
            {
                return;
            }

            var source = string.IsNullOrWhiteSpace(result?.Source)
                ? GetOceanCurrentAcquisitionModeLabel(oceanCurrentAcquisitionMode)
                : result.Source;
            oceanCurrentActualSourceText.text = "实际来源：" + source;
        }

        private bool TryReadValidCurrentLayer(InputField minimumInput, InputField maximumInput, InputField eastwardInput, InputField northwardInput,
            out float minimumDepth, out float maximumDepth, out float eastward, out float northward)
        {
            minimumDepth = maximumDepth = eastward = northward = 0f;
            if (!TryParseFloat(minimumInput, "海流最小深度", 0f, 11000f, out minimumDepth)
                || !TryParseFloat(maximumInput, "海流最大深度", 0f, 11000f, out maximumDepth)
                || !TryParseFloat(eastwardInput, "东向海流", -20f, 20f, out eastward)
                || !TryParseFloat(northwardInput, "北向海流", -20f, 20f, out northward))
            {
                return false;
            }
            if (maximumDepth < minimumDepth)
            {
                SetStatus("海流最大深度必须不小于最小深度", new Color(1f, 0.58f, 0.58f));
                return false;
            }
            return true;
        }

        private static bool TryValidateCurrentResult(CopernicusCurrentResult result, out string error)
        {
            error = "响应不完整";
            if (result?.Profile == null || result.Field == null || result.Field.Samples == null || result.Field.Samples.Count == 0)
            {
                return false;
            }
            foreach (var layer in result.Profile.Layers)
            {
                if (layer == null || !IsFinite(layer.MinDepthM) || !IsFinite(layer.MaxDepthM)
                    || !IsFinite(layer.EastwardMps) || !IsFinite(layer.NorthwardMps)
                    || layer.MinDepthM < 0f || layer.MaxDepthM < layer.MinDepthM || layer.MaxDepthM > 11000f
                    || Mathf.Abs(layer.EastwardMps) > 20f || Mathf.Abs(layer.NorthwardMps) > 20f)
                {
                    error = "海流层数据无效";
                    return false;
                }
            }
            foreach (var sample in result.Field.Samples)
            {
                if (sample == null || !IsFinite(sample.DepthM) || !IsFinite(sample.ElapsedSeconds)
                    || !IsFinite(sample.EastwardMps) || !IsFinite(sample.NorthwardMps) || !IsFinite(sample.VerticalMps)
                    || double.IsNaN(sample.LongitudeDeg) || double.IsInfinity(sample.LongitudeDeg)
                    || double.IsNaN(sample.LatitudeDeg) || double.IsInfinity(sample.LatitudeDeg)
                    || sample.DepthM < 0f || sample.DepthM > 11000f || Mathf.Abs(sample.EastwardMps) > 20f || Mathf.Abs(sample.NorthwardMps) > 20f)
                {
                    error = "海流网格数据无效";
                    return false;
                }
            }
            error = null;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
