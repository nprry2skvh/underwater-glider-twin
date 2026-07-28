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
    }
}
