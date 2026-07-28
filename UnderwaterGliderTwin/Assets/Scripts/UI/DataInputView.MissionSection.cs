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
        private static void CreateConfigurationGroups(Transform panel)
        {
            var groupColor = new Color(0.03f, 0.15f, 0.2f, 0.44f);
            UiFactory.Panel("DataSourceConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -42f), new Vector2(1268f, 42f), groupColor);
            UiFactory.Panel("PredictionConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -86f), new Vector2(1196f, 42f), groupColor);
            UiFactory.Panel("SimulationConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -128f), new Vector2(1332f, 50f), groupColor);
            UiFactory.Panel("OceanConfigurationGroup", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -180f), new Vector2(1600f, 52f), groupColor);
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
    }
}
