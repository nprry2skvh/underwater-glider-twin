using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.Bootstrap;

namespace UnderwaterGliderTwin.UI
{
    public sealed class DashboardView : MonoBehaviour
    {
        private const float MinUiUpdateIntervalSeconds = 1f / 15f;
        private PlaybackController playback;
        private PredictionController prediction;
        private float nextAllowedUiTime;
        private float[] cumulativeDistanceMeters;
        private Text depthValue;
        private Text headingValue;
        private Text pitchValue;
        private Text rollValue;
        private Text yawValue;
        private Text batteryValue;
        private Text latitudeValue;
        private Text longitudeValue;
        private Text velocityXValue;
        private Text velocityYValue;
        private Text velocityZValue;
        private Text speedValue;
        private Text verticalSpeedValue;
        private Text horizontalSpeedValue;
        private Text missionTimeValue;
        private Text distanceValue;
        private Text predictionErrorValue;
        private Text oceanCurrentValue;
        private Text waterSpeedValue;
        private Text groundSpeedValue;
        private Text sideSlipValue;
        private Text netBuoyancyValue;
        private Text energyValue;
        private Text angleOfAttackValue;
        private Text liftForceValue;
        private Text dragForceValue;
        private Text angularRateValue;
        private Text hydrodynamicMomentValue;
        private Text inertiaValue;
        private Text pistonPositionValue;
        private Text controlSurfaceValue;
        private Text actuatorPowerValue;
        private Text dynamicsSummaryValue;
        private readonly List<GameObject> advancedRows = new List<GameObject>();
        private RectTransform panel;
        private Button detailsButton;
        private GameObject navigationReferenceCard;
        private bool showingDetails;
        private bool minimalBoundReferences;

        public void Initialize(PlaybackController playbackController, PredictionController predictionController)
        {
            playback = playbackController;
            prediction = predictionController;
            cumulativeDistanceMeters = BuildDistanceCache(playback.Model);

            var canvas = UiFactory.EnsureCanvas(transform);
            UiFactory.EnsureCommandCenterHeader(canvas.transform);
            panel = UiFactory.CommandPanel("TelemetryPanel", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -302f), new Vector2(328f, 366f));
            UiFactory.Text("TelemetryTitle", panel, "遥测数据", 18, TextAnchor.MiddleLeft, new Color(0.92f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -20f), new Vector2(220f, 28f));

            depthValue = AddRow(panel, "深度", "DepthValue", 62f);
            headingValue = AddRow(panel, "航向", "HeadingValue", 94f);
            pitchValue = AddRow(panel, "俯仰", "PitchValue", 126f);
            rollValue = AddRow(panel, "横滚", "RollValue", 158f);
            speedValue = AddRow(panel, "水平位移", "HorizontalDisplacementValue", 190f);
            batteryValue = AddRow(panel, "电量", "BatteryValue", 222f);
            detailsButton = UiFactory.Button("TelemetryDetailsButton", panel, "显示详情", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -260f), new Vector2(132f, 28f));
            detailsButton.onClick.AddListener(ToggleDetails);
            yawValue = AddAdvancedRow(panel, "偏航", "YawValue", 300f, 0);
            latitudeValue = AddAdvancedRow(panel, "纬度", "LatitudeValue", 324f, 0);
            longitudeValue = AddAdvancedRow(panel, "经度", "LongitudeValue", 348f, 0);
            velocityXValue = AddAdvancedRow(panel, "东向速度", "VelocityXValue", 372f, 0);
            velocityYValue = AddAdvancedRow(panel, "垂向速度", "VelocityYValue", 396f, 0);
            velocityZValue = AddAdvancedRow(panel, "北向速度", "VelocityZValue", 420f, 0);
            verticalSpeedValue = AddAdvancedRow(panel, "升沉速度", "VerticalSpeedValue", 444f, 0);
            horizontalSpeedValue = AddAdvancedRow(panel, "水平速度", "HorizontalSpeedValue", 468f, 0);
            missionTimeValue = AddAdvancedRow(panel, "任务时间", "MissionTimeValue", 300f, 1);
            distanceValue = AddAdvancedRow(panel, "航行距离", "DistanceValue", 324f, 1);
            predictionErrorValue = AddAdvancedRow(panel, "预测误差", "PredictionErrorValue", 348f, 1);
            oceanCurrentValue = AddAdvancedRow(panel, "当前海流", "OceanCurrentValue", 372f, 1);
            waterSpeedValue = AddAdvancedRow(panel, "对水速度", "WaterSpeedValue", 396f, 1);
            groundSpeedValue = AddAdvancedRow(panel, "对地速度", "GroundSpeedValue", 420f, 1);
            sideSlipValue = AddAdvancedRow(panel, "侧滑角", "SideSlipValue", 444f, 1);
            netBuoyancyValue = AddAdvancedRow(panel, "净浮力", "NetBuoyancyValue", 468f, 1);
            energyValue = AddAdvancedRow(panel, "瞬时功耗", "EnergyValue", 492f, 1);

            angleOfAttackValue = AddAdvancedRow(panel, "\u653b\u89d2", "AngleOfAttackValue", 492f, 0);
            liftForceValue = AddAdvancedRow(panel, "\u5347\u529b", "LiftForceValue", 516f, 0);
            dragForceValue = AddAdvancedRow(panel, "\u963b\u529b", "DragForceValue", 516f, 1);
            angularRateValue = AddAdvancedRow(panel, "\u89d2\u901f\u5ea6", "AngularRateValue", 540f, 1);
            hydrodynamicMomentValue = AddAdvancedRow(panel, "\u6c34\u52a8\u529b\u77e9", "HydrodynamicMomentValue", 540f, 0);
            inertiaValue = AddAdvancedRow(panel, "\u8f6c\u52a8\u60ef\u91cf", "InertiaValue", 564f, 1);
            pistonPositionValue = AddAdvancedRow(panel, "\u6d3b\u585e\u4f4d\u7f6e", "PistonPositionValue", 564f, 0);
            controlSurfaceValue = AddAdvancedRow(panel, "\u63a7\u5236\u9762\u504f\u89d2", "ControlSurfaceValue", 588f, 0);
            actuatorPowerValue = AddAdvancedRow(panel, "\u6267\u884c\u673a\u6784\u529f\u7387", "ActuatorPowerValue", 588f, 1);

            dynamicsSummaryValue = UiFactory.Text("DynamicsSummaryValue", panel, "6DOF", 10, TextAnchor.MiddleRight, new Color(0.96f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -252f), new Vector2(162f, 22f));

            playback.FrameChangedWithReason += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
            navigationReferenceCard = BuildNavigationReferenceCard(canvas.transform);
            RefreshDetails();
        }

        public void Bind(DashboardPanelRefs refs, PlaybackController playbackController, PredictionController predictionController)
        {
            playback = playbackController;
            prediction = predictionController;
            if (refs == null || playback == null || playback.Model == null)
            {
                return;
            }

            panel = refs.panel;
            detailsButton = refs.detailsButton;
            navigationReferenceCard = refs.navigationReferenceCard;
            depthValue = refs.depthValue;
            headingValue = refs.headingValue;
            pitchValue = refs.pitchValue;
            rollValue = refs.rollValue;
            yawValue = refs.yawValue;
            batteryValue = refs.batteryValue;
            latitudeValue = refs.latitudeValue;
            longitudeValue = refs.longitudeValue;
            velocityXValue = refs.velocityXValue;
            velocityYValue = refs.velocityYValue;
            velocityZValue = refs.velocityZValue;
            speedValue = refs.speedValue;
            verticalSpeedValue = refs.verticalSpeedValue;
            horizontalSpeedValue = refs.horizontalSpeedValue;
            missionTimeValue = refs.missionTimeValue;
            distanceValue = refs.distanceValue;
            predictionErrorValue = refs.predictionErrorValue;
            oceanCurrentValue = refs.oceanCurrentValue;
            waterSpeedValue = refs.waterSpeedValue;
            groundSpeedValue = refs.groundSpeedValue;
            sideSlipValue = refs.sideSlipValue;
            netBuoyancyValue = refs.netBuoyancyValue;
            energyValue = refs.energyValue;
            angleOfAttackValue = refs.angleOfAttackValue;
            liftForceValue = refs.liftForceValue;
            dragForceValue = refs.dragForceValue;
            angularRateValue = refs.angularRateValue;
            hydrodynamicMomentValue = refs.hydrodynamicMomentValue;
            inertiaValue = refs.inertiaValue;
            pistonPositionValue = refs.pistonPositionValue;
            controlSurfaceValue = refs.controlSurfaceValue;
            actuatorPowerValue = refs.actuatorPowerValue;
            dynamicsSummaryValue = refs.dynamicsSummaryValue;
            minimalBoundReferences = headingValue == null || pitchValue == null || rollValue == null;
            cumulativeDistanceMeters = BuildDistanceCache(playback.Model);
            if (detailsButton != null)
            {
                detailsButton.onClick.RemoveAllListeners();
                detailsButton.onClick.AddListener(ToggleDetails);
            }

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
                if (depthValue != null)
                {
                    depthValue.text = $"{playback.Model.CurrentFrame.DepthM:0.0} m";
                }

                if (batteryValue != null)
                {
                    batteryValue.text = $"{playback.Model.CurrentFrame.BatteryPercent:0} %";
                }

                return;
            }

            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
            RefreshDetails();
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
            UiFactory.Text(label + "Label", panel, label, 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.97f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -topOffset), new Vector2(126f, 22f));
            return UiFactory.Text(valueName, panel, "-", 13, TextAnchor.MiddleRight, new Color(0.96f, 0.99f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -topOffset), new Vector2(164f, 22f));
        }

        private Text AddAdvancedRow(Transform parent, string label, string valueName, float topOffset, int column)
        {
            var x = column == 0 ? 16f : 328f;
            var labelText = UiFactory.Text(label + "Label", parent, label, 12, TextAnchor.MiddleLeft,
                new Color(0.82f, 0.97f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -topOffset), new Vector2(116f, 22f));
            var value = UiFactory.Text(valueName, parent, "-", 13, TextAnchor.MiddleRight,
                new Color(0.96f, 0.99f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x + 118f, -topOffset), new Vector2(166f, 22f));
            advancedRows.Add(labelText.gameObject);
            advancedRows.Add(value.gameObject);
            return value;
        }

        private void ToggleDetails()
        {
            showingDetails = !showingDetails;
            RefreshDetails();
        }

        private void RefreshDetails()
        {
            foreach (var row in advancedRows)
            {
                row.GetComponent<Text>().enabled = showingDetails;
            }

            if (panel != null)
            {
                panel.sizeDelta = new Vector2(showingDetails ? 640f : 328f, showingDetails ? 640f : 366f);
            }

            if (navigationReferenceCard != null)
            {
                navigationReferenceCard.SetActive(!showingDetails);
            }

            UiFactory.SetButtonText(detailsButton, showingDetails ? "收起详情" : "显示详情");
        }

        private static GameObject BuildNavigationReferenceCard(Transform canvas)
        {
            var card = UiFactory.CommandPanel(
                "NavigationReferenceCard",
                canvas,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(18f, 160f),
                new Vector2(328f, 172f));
            UiFactory.Text("NavigationCardTitle", card, "\u59ff\u6001\u4e0e\u5bfc\u822a", 15, TextAnchor.MiddleLeft, UiFactory.CommandText,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -20f), new Vector2(168f, 24f));
            UiFactory.Text("NavigationCardNorth", card, "N", 12, TextAnchor.MiddleCenter, UiFactory.CommandText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(24f, 20f));
            UiFactory.Text("NavigationCardEast", card, "E", 12, TextAnchor.MiddleCenter, new Color(0.52f, 0.9f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 0f), new Vector2(24f, 20f));
            UiFactory.Text("NavigationCardSouth", card, "S", 12, TextAnchor.MiddleCenter, UiFactory.CommandText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(24f, 20f));
            UiFactory.Text("NavigationCardWest", card, "W", 12, TextAnchor.MiddleCenter, new Color(0.52f, 0.9f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-46f, 0f), new Vector2(24f, 20f));
            UiFactory.Text("NavigationCardMarker", card, "\u25c6", 34, TextAnchor.MiddleCenter, UiFactory.CommandAccent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            UiFactory.Text("NavigationCardHint", card, "\u5168\u5c40\u900f\u89c6  |  \u53ef\u5207\u6362\u89c6\u89d2", 11, TextAnchor.MiddleCenter, new Color(0.54f, 0.78f, 0.90f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 15f), new Vector2(260f, 22f));
            return card.gameObject;
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            if (!ShouldUpdateForFrame(Time.unscaledTime, reason))
            {
                return;
            }

            var velocity = Vector3.zero;
            if (index > 0)
            {
                velocity = TelemetryKinematicsUtility.EstimateWindowVelocity(playback.Model.Frames, index);
                velocity = TelemetryVelocityEstimator.ClampHorizontalSpeed(velocity);
            }

            depthValue.text = $"{frame.DepthM:0.0} m";
            headingValue.text = $"{frame.HeadingDeg:0.0}°";
            pitchValue.text = $"{frame.PitchDeg:0.0}°";
            rollValue.text = $"{frame.RollDeg:0.0}°";
            yawValue.text = $"{frame.HeadingDeg:0.0}°";
            latitudeValue.text = $"{frame.LatitudeDeg:0.000000}°";
            longitudeValue.text = $"{frame.LongitudeDeg:0.000000}°";
            velocityXValue.text = $"{velocity.x:0.00} m/s";
            velocityYValue.text = $"{velocity.y:0.00} m/s";
            velocityZValue.text = $"{velocity.z:0.00} m/s";
            speedValue.text = TelemetryKinematicsUtility.TryGetHorizontalDisplacementMeters(
                playback.Model.GetFrame(0), frame, out var horizontalDisplacement)
                ? FormatHorizontalDisplacement(horizontalDisplacement)
                : "-";
            verticalSpeedValue.text = $"{velocity.y:0.00} m/s";
            horizontalSpeedValue.text = $"{new Vector2(velocity.x, velocity.z).magnitude:0.00} m/s";
            missionTimeValue.text = FormatDuration(playback.Model.CurrentElapsedSeconds - playback.Model.StartElapsedSeconds);
            distanceValue.text = $"{cumulativeDistanceMeters[Mathf.Clamp(index, 0, cumulativeDistanceMeters.Length - 1)] / 1000f:0.00} km";
            predictionErrorValue.text = prediction != null ? $"{prediction.CurrentSnapshot.CurrentErrorMeters:0.00} m" : "-";
            batteryValue.text = $"{frame.BatteryPercent:0} %";
            var oceanCurrent = frame.Diagnostics.HasValue
                ? new Vector2(
                    frame.Diagnostics.Value.CurrentVelocityEndMps.x,
                    frame.Diagnostics.Value.CurrentVelocityEndMps.z)
                : RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                    ? RuntimeDataSourceState.SimulationProfile.OceanCurrentProfile?.GetVelocity(frame.DepthM) ?? Vector2.zero
                    : Vector2.zero;
            oceanCurrentValue.text = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                ? $"东 {oceanCurrent.x:0.00} m/s 北 {oceanCurrent.y:0.00} m/s"
                : "-";
            if (frame.Diagnostics.HasValue)
            {
                var diagnostics = frame.Diagnostics.Value;
                waterSpeedValue.text = $"{diagnostics.WaterVelocityEndMps.magnitude:0.00} m/s";
                groundSpeedValue.text = $"{(diagnostics.WaterVelocityEndMps + diagnostics.CurrentVelocityEndMps).magnitude:0.00} m/s";
                sideSlipValue.text = $"{diagnostics.SideSlipDeg:0.0}°";
                netBuoyancyValue.text = $"{diagnostics.NetBuoyancyForceN:0.0} N";
                energyValue.text = $"{diagnostics.EnergyWatts:0.0} W";
                angleOfAttackValue.text = $"{diagnostics.AngleOfAttackDeg:0.0} deg";
                liftForceValue.text = $"{diagnostics.LiftForceN:0.0} N";
                dragForceValue.text = $"{diagnostics.DragForceN:0.0} N";
                angularRateValue.text = $"{diagnostics.AngularVelocityRadPerSecond.magnitude * Mathf.Rad2Deg:0.00} deg/s";
                hydrodynamicMomentValue.text = $"{diagnostics.HydrodynamicMomentNm.magnitude:0.00} N*m";
                pistonPositionValue.text = $"{diagnostics.PistonPositionMm:0.0} mm";
                var deflection = diagnostics.ControlSurfaceDeflectionDeg;
                controlSurfaceValue.text = $"R {deflection.x:0.0} / P {deflection.y:0.0} / Y {deflection.z:0.0} deg";
                actuatorPowerValue.text = $"{diagnostics.ActuatorPowerWatts:0.0} W";
                dynamicsSummaryValue.text = $"AoA {diagnostics.AngleOfAttackDeg:0.0}\u00b0  L {diagnostics.LiftForceN:0.0}N  D {diagnostics.DragForceN:0.0}N";
            }
            else
            {
                waterSpeedValue.text = "-";
                groundSpeedValue.text = "-";
                sideSlipValue.text = "-";
                netBuoyancyValue.text = "-";
                energyValue.text = "-";
                angleOfAttackValue.text = "-";
                liftForceValue.text = "-";
                dragForceValue.text = "-";
                angularRateValue.text = "-";
                hydrodynamicMomentValue.text = "-";
                pistonPositionValue.text = "-";
                controlSurfaceValue.text = "-";
                actuatorPowerValue.text = "-";
                dynamicsSummaryValue.text = "-";
            }

            var dynamics = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                ? RuntimeDataSourceState.SimulationProfile?.Dynamics
                : null;
            inertiaValue.text = dynamics == null
                ? "-"
                : $"{dynamics.RollInertiaKgM2:0.#}/{dynamics.PitchInertiaKgM2:0.#}/{dynamics.YawInertiaKgM2:0.#} kg*m2";
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

        private static string FormatDuration(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var totalSeconds = Mathf.RoundToInt(seconds);
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var secs = totalSeconds % 60;
            return $"{hours:00}:{minutes:00}:{secs:00}";
        }

        private static string FormatHorizontalDisplacement(Vector2 displacementMeters)
        {
            var magnitude = displacementMeters.magnitude;
            var scale = magnitude >= 1000f ? 0.001f : 1f;
            var unit = magnitude >= 1000f ? "km" : "m";
            return $"E {displacementMeters.x * scale:0.0} N {displacementMeters.y * scale:0.0} |{magnitude * scale:0.0}| {unit}";
        }
    }
}
