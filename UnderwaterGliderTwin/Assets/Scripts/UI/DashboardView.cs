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

        [System.Obsolete("Use Bind(...) with editable UI references.")]
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
            refsAdvancedRowsRoot = refs.advancedRowsRoot;
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

            ConfigureBoundRows();

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
            var row = CreateRow(panel, valueName + "Row", topOffset, 0);
            var labelText = UiFactory.Text(label + "Label", row, label, 12, TextAnchor.MiddleLeft, new Color(0.82f, 0.97f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var valueText = UiFactory.Text(valueName, row, "-", 13, TextAnchor.MiddleRight, new Color(0.96f, 0.99f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ConfigureKeyValueChildren(labelText, valueText);
            return valueText;
        }

        private Text AddAdvancedRow(Transform parent, string label, string valueName, float topOffset, int column)
        {
            var row = CreateRow(parent, valueName + "Row", topOffset, column);
            var labelText = UiFactory.Text(label + "Label", row, label, 12, TextAnchor.MiddleLeft,
                new Color(0.82f, 0.97f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var value = UiFactory.Text(valueName, row, "-", 13, TextAnchor.MiddleRight,
                new Color(0.96f, 0.99f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ConfigureKeyValueChildren(labelText, value);
            advancedRows.Add(labelText.gameObject);
            advancedRows.Add(value.gameObject);
            return value;
        }

        private void ConfigureBoundRows()
        {
            advancedRows.Clear();
            ConfigureBoundRow("DepthRow", "深度Label", depthValue, false);
            ConfigureBoundRow("HeadingRow", "航向Label", headingValue, false);
            ConfigureBoundRow("PitchRow", "俯仰Label", pitchValue, false);
            ConfigureBoundRow("RollRow", "横滚Label", rollValue, false);
            ConfigureBoundRow("SpeedRow", "水平位移Label", speedValue, false);
            ConfigureBoundRow("BatteryRow", "电量Label", batteryValue, false);

            ConfigureBoundRow("YawRow", "偏航Label", yawValue, true);
            ConfigureBoundRow("LatitudeRow", "纬度Label", latitudeValue, true);
            ConfigureBoundRow("LongitudeRow", "经度Label", longitudeValue, true);
            ConfigureBoundRow("VelocityXRow", "东向速度Label", velocityXValue, true);
            ConfigureBoundRow("VelocityYRow", "垂向速度Label", velocityYValue, true);
            ConfigureBoundRow("VelocityZRow", "北向速度Label", velocityZValue, true);
            ConfigureBoundRow("VerticalSpeedRow", "升沉速度Label", verticalSpeedValue, true);
            ConfigureBoundRow("HorizontalSpeedRow", "水平速度Label", horizontalSpeedValue, true);
            ConfigureBoundRow("MissionTimeRow", "任务时间Label", missionTimeValue, true);
            ConfigureBoundRow("DistanceRow", "航行距离Label", distanceValue, true);
            ConfigureBoundRow("PredictionErrorRow", "预测误差Label", predictionErrorValue, true);
            ConfigureBoundRow("OceanCurrentRow", "当前海流Label", oceanCurrentValue, true);
            ConfigureBoundRow("WaterSpeedRow", "对水速度Label", waterSpeedValue, true);
            ConfigureBoundRow("GroundSpeedRow", "对地速度Label", groundSpeedValue, true);
            ConfigureBoundRow("SideSlipRow", "侧滑角Label", sideSlipValue, true);
            ConfigureBoundRow("NetBuoyancyRow", "净浮力Label", netBuoyancyValue, true);
            ConfigureBoundRow("EnergyRow", "瞬时功耗Label", energyValue, true);
            ConfigureBoundRow("AngleOfAttackRow", "攻角Label", angleOfAttackValue, true);
            ConfigureBoundRow("LiftForceRow", "升力Label", liftForceValue, true);
            ConfigureBoundRow("DragForceRow", "阻力Label", dragForceValue, true);
            ConfigureBoundRow("AngularRateRow", "角速度Label", angularRateValue, true);
            ConfigureBoundRow("HydrodynamicMomentRow", "水动力矩Label", hydrodynamicMomentValue, true);
            ConfigureBoundRow("InertiaRow", "转动惯量Label", inertiaValue, true);
            ConfigureBoundRow("PistonPositionRow", "活塞位置Label", pistonPositionValue, true);
            ConfigureBoundRow("ControlSurfaceRow", "控制面偏角Label", controlSurfaceValue, true);
            ConfigureBoundRow("ActuatorPowerRow", "执行机构功率Label", actuatorPowerValue, true);
        }

        private void ConfigureBoundRow(string rowName, string labelName, Text value, bool advanced)
        {
            if (value == null || panel == null)
            {
                return;
            }

            var label = panel.Find(labelName)?.GetComponent<Text>();
            if (label == null)
            {
                label = value.transform.parent?.Find(labelName)?.GetComponent<Text>();
            }

            if (label == null)
            {
                return;
            }

            var parent = advanced && refsAdvancedRowsRoot != null ? refsAdvancedRowsRoot : panel;
            var row = EnsureRow(parent, rowName);
            label.transform.SetParent(row, false);
            value.transform.SetParent(row, false);
            ConfigureKeyValueChildren(label, value);
            if (advanced)
            {
                advancedRows.Add(label.gameObject);
                advancedRows.Add(value.gameObject);
                label.gameObject.SetActive(showingDetails);
                value.gameObject.SetActive(showingDetails);
            }
        }

        private RectTransform refsAdvancedRowsRoot;

        private static RectTransform CreateRow(Transform parent, string name, float topOffset, int column)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = column == 0 ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            row.anchorMax = column == 0 ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -topOffset);
            row.sizeDelta = new Vector2(-8f, 26f);
            return ConfigureRow(row);
        }

        private static RectTransform EnsureRow(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            return existing != null ? ConfigureRow(existing) : ConfigureRow(new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(), parent);
        }

        private static RectTransform ConfigureRow(RectTransform row, Transform parent = null)
        {
            if (parent != null)
            {
                row.SetParent(parent, false);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = Vector2.zero;
                row.sizeDelta = new Vector2(0f, 26f);
            }

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
            ConfigureKeyText(value, 0f);
            value.rectTransform.anchorMin = Vector2.one;
            value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.pivot = Vector2.one;
            var valueLayout = value.GetComponent<LayoutElement>() ?? value.gameObject.AddComponent<LayoutElement>();
            valueLayout.minWidth = 64f;
            valueLayout.flexibleWidth = 1f;
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
