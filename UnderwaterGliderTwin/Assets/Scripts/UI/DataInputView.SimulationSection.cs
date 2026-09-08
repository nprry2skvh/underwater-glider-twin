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
                if (bottomDrawerExpandedBeforeFlightLeg)
                {
                    SetBottomDrawerExpanded(true);
                }
                return;
            }

            bottomDrawerExpandedBeforeFlightLeg = bottomDrawerExpanded;
            SetBottomDrawerExpanded(false);

            if (oceanCurrentDrawer != null)
            {
                oceanCurrentDrawer.gameObject.SetActive(false);
            }

            flightLegDrawer.SetAsLastSibling();

            RefreshFlightLegDrawer();
        }

        private void RefreshFlightLegDrawer()
        {
            if (flightLegDrawer == null)
            {
                return;
            }

            if (descentNetBuoyancyInput == null
                || descentPitchInput == null
                || descentRollInput == null
                || ascentNetBuoyancyInput == null
                || ascentPitchInput == null
                || ascentRollInput == null)
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
    }
}
