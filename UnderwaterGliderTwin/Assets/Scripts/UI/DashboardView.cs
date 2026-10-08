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
        private const float CompactTelemetryPanelHeight = 384f;
        private const float MinUiUpdateIntervalSeconds = 1f / 15f;
        private PlaybackController playback;
        private PredictionController prediction;
        private float nextAllowedUiTime;
        private float[] cumulativeDistanceMeters;
        private IReadOnlyList<TelemetryFrame> distanceCacheFrames;
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
        private RectTransform boundAdvancedRowsRoot;
        private RectTransform panel;
        private Button detailsButton;
        private GameObject navigationReferenceCard;
        private GameObject telemetryEmptyState;
        private Text telemetryEmptyStateTitle;
        private Text telemetryEmptyStateHint;
        private bool showingDetails;
        private bool minimalBoundReferences;

        [System.Obsolete("Use Bind(...) with editable UI references.")]
        public void Initialize(PlaybackController playbackController, PredictionController predictionController)
        {
            playback = playbackController;
            prediction = predictionController;
            RefreshDistanceCache(playback.Model);

            var canvas = UiFactory.EnsureCanvas(transform);
            UiFactory.EnsureCommandCenterHeader(canvas.transform);
            panel = UiFactory.CommandPanel("TelemetryPanel", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -364f), new Vector2(328f, CompactTelemetryPanelHeight));
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

            EnsureTelemetryEmptyState();
            playback.FrameChangedWithReason += OnFrameChanged;
            playback.ContinuousChanged += OnContinuousChanged;
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
            boundAdvancedRowsRoot = refs.advancedRowsRoot;
            RegisterBoundAdvancedRows();
            RefreshDetails();
            EnsureTelemetryEmptyState();
            RefreshDistanceCache(playback.Model);
            if (detailsButton != null)
            {
                detailsButton.onClick.RemoveAllListeners();
                detailsButton.onClick.AddListener(ToggleDetails);
            }

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

            RefreshTelemetryState(playback.Model.Frames);
            if (playback.Model.Frames == null || playback.Model.Frames.Count == 0)
            {
                return;
            }

            if (minimalBoundReferences)
            {
                if (depthValue != null)
                {
                    var sample = ContinuousMotionSampler.Sample(playback.Model.Frames, playback.Model.ContinuousElapsedSeconds);
                    SetValue(depthValue, $"{sample.DepthM:0.0}", "m");
                }

                if (batteryValue != null)
                {
                    SetValue(batteryValue, $"{playback.Model.CurrentFrame.BatteryPercent:0}", "%");
                }

                return;
            }

            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
            OnContinuousChanged(playback.Model.ContinuousIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
            RefreshDetails();
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
            if (reason == FrameUpdateReason.Initial
                || reason == FrameUpdateReason.Seek
                || reason == FrameUpdateReason.Rebuild)
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

        private void RegisterBoundAdvancedRows()
        {
            advancedRows.Clear();
            if (boundAdvancedRowsRoot != null)
            {
                foreach (var text in boundAdvancedRowsRoot.GetComponentsInChildren<Text>(true))
                {
                    advancedRows.Add(text.gameObject);
                }
            }

            RegisterBoundAdvancedRow(yawValue, "偏航Label");
            RegisterBoundAdvancedRow(latitudeValue, "纬度Label");
            RegisterBoundAdvancedRow(longitudeValue, "经度Label");
            RegisterBoundAdvancedRow(velocityXValue, "东向速度Label");
            RegisterBoundAdvancedRow(velocityYValue, "垂向速度Label");
            RegisterBoundAdvancedRow(velocityZValue, "北向速度Label");
            RegisterBoundAdvancedRow(verticalSpeedValue, "升沉速度Label");
            RegisterBoundAdvancedRow(horizontalSpeedValue, "水平速度Label");
            RegisterBoundAdvancedRow(missionTimeValue, "任务时间Label");
            RegisterBoundAdvancedRow(distanceValue, "航行距离Label");
            RegisterBoundAdvancedRow(predictionErrorValue, "预测误差Label");
            RegisterBoundAdvancedRow(oceanCurrentValue, "当前海流Label");
            RegisterBoundAdvancedRow(waterSpeedValue, "对水速度Label");
            RegisterBoundAdvancedRow(groundSpeedValue, "对地速度Label");
            RegisterBoundAdvancedRow(sideSlipValue, "侧滑角Label");
            RegisterBoundAdvancedRow(netBuoyancyValue, "净浮力Label");
            RegisterBoundAdvancedRow(energyValue, "瞬时功耗Label");
            RegisterBoundAdvancedRow(angleOfAttackValue, "攻角Label");
            RegisterBoundAdvancedRow(liftForceValue, "升力Label");
            RegisterBoundAdvancedRow(dragForceValue, "阻力Label");
            RegisterBoundAdvancedRow(angularRateValue, "角速度Label");
            RegisterBoundAdvancedRow(hydrodynamicMomentValue, "水动力矩Label");
            RegisterBoundAdvancedRow(inertiaValue, "转动惯量Label");
            RegisterBoundAdvancedRow(pistonPositionValue, "活塞位置Label");
            RegisterBoundAdvancedRow(controlSurfaceValue, "控制面偏角Label");
            RegisterBoundAdvancedRow(actuatorPowerValue, "执行机构功率Label");
        }

        private void RegisterBoundAdvancedRow(Text value, string labelName)
        {
            if (value == null) return;
            if (!advancedRows.Contains(value.gameObject)) advancedRows.Add(value.gameObject);
            var unit = value.transform.parent?.Find(value.name + "Unit")?.GetComponent<Text>();
            if (unit != null && !advancedRows.Contains(unit.gameObject)) advancedRows.Add(unit.gameObject);
            if (panel == null) return;
            foreach (var label in panel.GetComponentsInChildren<Text>(true))
            {
                if (label.name == labelName && !advancedRows.Contains(label.gameObject))
                {
                    advancedRows.Add(label.gameObject);
                }
            }
        }

        private void RefreshDetails()
        {
            if (boundAdvancedRowsRoot != null)
            {
                boundAdvancedRowsRoot.gameObject.SetActive(showingDetails);
            }

            foreach (var row in advancedRows)
            {
                var text = row != null ? row.GetComponent<Text>() : null;
                if (text != null)
                {
                    text.enabled = showingDetails;
                }
            }

            if (panel != null)
            {
                panel.sizeDelta = new Vector2(showingDetails ? 640f : 328f, showingDetails ? 640f : CompactTelemetryPanelHeight);
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

        public void RefreshTelemetryStateForTests(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames)
        {
            RefreshTelemetryState(frames);
        }

        public void SetTelemetryEmptyState(bool visible)
        {
            EnsureTelemetryEmptyState();
            if (telemetryEmptyState != null)
            {
                telemetryEmptyState.SetActive(visible);
            }
        }

        private void RefreshTelemetryState(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames)
        {
            SetTelemetryEmptyState(frames == null || frames.Count == 0);
        }

        private void EnsureTelemetryEmptyState()
        {
            if (panel == null)
            {
                return;
            }

            telemetryEmptyState = panel.Find("TelemetryEmptyState")?.gameObject;
            if (telemetryEmptyState == null)
            {
                var card = UiFactory.Panel("TelemetryEmptyState", panel,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -132f), new Vector2(286f, 82f), UiFactory.CommandPanelFill);
                card.GetComponent<Image>().raycastTarget = false;
                telemetryEmptyState = card.gameObject;
            }

            telemetryEmptyStateTitle = telemetryEmptyState.transform.Find("TelemetryEmptyStateTitle")?.GetComponent<Text>();
            if (telemetryEmptyStateTitle == null)
            {
                telemetryEmptyStateTitle = UiFactory.Text("TelemetryEmptyStateTitle", telemetryEmptyState.transform,
                    "尚未加载有效轨迹", 14, TextAnchor.MiddleCenter, UiFactory.CommandText,
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 12f), new Vector2(-20f, 24f));
            }

            telemetryEmptyStateHint = telemetryEmptyState.transform.Find("TelemetryEmptyStateHint")?.GetComponent<Text>();
            if (telemetryEmptyStateHint == null)
            {
                telemetryEmptyStateHint = UiFactory.Text("TelemetryEmptyStateHint", telemetryEmptyState.transform,
                    "请加载 CSV 或运行参数仿真", 12, TextAnchor.MiddleCenter, new Color(0.54f, 0.78f, 0.90f),
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -16f), new Vector2(-20f, 22f));
            }

            telemetryEmptyStateTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            telemetryEmptyStateHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            telemetryEmptyStateTitle.verticalOverflow = VerticalWrapMode.Truncate;
            telemetryEmptyStateHint.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            if (playback == null || playback.Model == null || playback.Model.Frames == null || playback.Model.Frames.Count == 0)
            {
                RefreshTelemetryState(playback?.Model?.Frames);
                return;
            }

            RefreshDistanceCache(playback.Model);
            RefreshTelemetryState(playback.Model.Frames);
            if (minimalBoundReferences)
            {
                var minimalSample = ContinuousMotionSampler.Sample(playback.Model.Frames, playback.Model.ContinuousElapsedSeconds);
                SetValue(depthValue, $"{minimalSample.DepthM:0.0}", "m");
                SetValue(batteryValue, $"{frame.BatteryPercent:0}", "%");
                return;
            }

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

            SetValue(depthValue, $"{frame.DepthM:0.0}", "m");
            SetValue(headingValue, $"{frame.HeadingDeg:0.0}", "°");
            SetValue(pitchValue, $"{frame.PitchDeg:0.0}", "°");
            SetValue(rollValue, $"{frame.RollDeg:0.0}", "°");
            SetValue(yawValue, $"{frame.HeadingDeg:0.0}", "°");
            SetValue(latitudeValue, TelemetryPositionUtility.HasUsableCoordinates(frame)
                ? $"{frame.LatitudeDeg:0.000000}"
                : "-", "°");
            SetValue(longitudeValue, TelemetryPositionUtility.HasUsableCoordinates(frame)
                ? $"{frame.LongitudeDeg:0.000000}"
                : "-", "°");
            SetValue(velocityXValue, $"{velocity.x:0.00}", "m/s");
            SetValue(velocityYValue, $"{velocity.y:0.00}", "m/s");
            SetValue(velocityZValue, $"{velocity.z:0.00}", "m/s");
            SetValue(speedValue, TelemetryKinematicsUtility.TryGetHorizontalDisplacementMeters(
                playback.Model.GetFrame(0), frame, out var horizontalDisplacement)
                ? FormatHorizontalDisplacement(horizontalDisplacement)
                : "-", string.Empty);
            SetValue(verticalSpeedValue, $"{velocity.y:0.00}", "m/s");
            SetValue(horizontalSpeedValue, $"{new Vector2(velocity.x, velocity.z).magnitude:0.00}", "m/s");
            var missionTimeText = FormatDuration(playback.Model.CurrentElapsedSeconds - playback.Model.StartElapsedSeconds);
            SetValue(missionTimeValue, missionTimeText, "s", missionTimeText);
            SetValue(distanceValue, $"{cumulativeDistanceMeters[Mathf.Clamp(index, 0, cumulativeDistanceMeters.Length - 1)] / 1000f:0.00}", "km");
            SetValue(predictionErrorValue, prediction != null ? $"{prediction.CurrentSnapshot.CurrentErrorMeters:0.00}" : "-", "m");
            SetValue(batteryValue, $"{frame.BatteryPercent:0}", "%");
            var oceanCurrent = frame.Diagnostics.HasValue
                ? new Vector2(
                    frame.Diagnostics.Value.CurrentVelocityEndMps.x,
                    frame.Diagnostics.Value.CurrentVelocityEndMps.z)
                : RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                    ? RuntimeDataSourceState.SimulationProfile.OceanCurrentProfile?.GetVelocity(frame.DepthM) ?? Vector2.zero
                    : Vector2.zero;
            var oceanCurrentText = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                ? $"东 {oceanCurrent.x:0.00} 北 {oceanCurrent.y:0.00}"
                : "-";
            SetValue(oceanCurrentValue, oceanCurrentText, "m/s",
                RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                    ? $"东 {oceanCurrent.x:0.00} m/s 北 {oceanCurrent.y:0.00} m/s"
                    : "-");
            if (frame.Diagnostics.HasValue)
            {
                var diagnostics = frame.Diagnostics.Value;
                SetValue(waterSpeedValue, $"{diagnostics.WaterVelocityEndMps.magnitude:0.00}", "m/s");
                SetValue(groundSpeedValue, $"{(diagnostics.WaterVelocityEndMps + diagnostics.CurrentVelocityEndMps).magnitude:0.00}", "m/s");
                SetValue(sideSlipValue, $"{diagnostics.SideSlipDeg:0.0}", "°");
                SetValue(netBuoyancyValue, $"{diagnostics.NetBuoyancyForceN:0.0}", "N");
                SetValue(energyValue, $"{diagnostics.EnergyWatts:0.0}", "W");
                SetValue(angleOfAttackValue, $"{diagnostics.AngleOfAttackDeg:0.0}", "deg");
                SetValue(liftForceValue, $"{diagnostics.LiftForceN:0.0}", "N");
                SetValue(dragForceValue, $"{diagnostics.DragForceN:0.0}", "N");
                SetValue(angularRateValue, $"{diagnostics.AngularVelocityRadPerSecond.magnitude * Mathf.Rad2Deg:0.00}", "deg/s");
                SetValue(hydrodynamicMomentValue, $"{diagnostics.HydrodynamicMomentNm.magnitude:0.00}", "N*m");
                SetValue(pistonPositionValue, $"{diagnostics.PistonPositionMm:0.0}", "mm");
                var deflection = diagnostics.ControlSurfaceDeflectionDeg;
                SetValue(controlSurfaceValue, $"R {deflection.x:0.0} / P {deflection.y:0.0} / Y {deflection.z:0.0}", "deg");
                SetValue(actuatorPowerValue, $"{diagnostics.ActuatorPowerWatts:0.0}", "W");
                SetValue(dynamicsSummaryValue, $"AoA {diagnostics.AngleOfAttackDeg:0.0}\u00b0  L {diagnostics.LiftForceN:0.0}N  D {diagnostics.DragForceN:0.0}N", string.Empty);
            }
            else
            {
                SetValue(waterSpeedValue, "-", "m/s");
                SetValue(groundSpeedValue, "-", "m/s");
                SetValue(sideSlipValue, "-", "°");
                SetValue(netBuoyancyValue, "-", "N");
                SetValue(energyValue, "-", "W");
                SetValue(angleOfAttackValue, "-", "deg");
                SetValue(liftForceValue, "-", "N");
                SetValue(dragForceValue, "-", "N");
                SetValue(angularRateValue, "-", "deg/s");
                SetValue(hydrodynamicMomentValue, "-", "N*m");
                SetValue(pistonPositionValue, "-", "mm");
                SetValue(controlSurfaceValue, "-", "deg");
                SetValue(actuatorPowerValue, "-", "W");
                SetValue(dynamicsSummaryValue, "-", string.Empty);
            }

            var dynamics = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                ? RuntimeDataSourceState.SimulationProfile?.Dynamics
                : null;
            SetValue(inertiaValue, dynamics == null
                ? "-"
                : $"{dynamics.RollInertiaKgM2:0.#}/{dynamics.PitchInertiaKgM2:0.#}/{dynamics.YawInertiaKgM2:0.#}", "kg*m2");

            ApplyContinuousMotion();
        }

        private void OnContinuousChanged(float continuousIndex, float progress01, FrameUpdateReason reason)
        {
            if (playback == null || playback.Model == null || playback.Model.Frames == null || playback.Model.Frames.Count == 0)
            {
                RefreshTelemetryState(playback?.Model?.Frames);
                return;
            }

            RefreshDistanceCache(playback.Model);
            if (!ShouldUpdateForFrame(Time.unscaledTime, reason))
            {
                return;
            }

            if (minimalBoundReferences)
            {
                if (depthValue != null)
                {
                    var sample = ContinuousMotionSampler.Sample(playback.Model.Frames, playback.Model.ContinuousElapsedSeconds);
                    SetValue(depthValue, $"{sample.DepthM:0.0}", "m");
                }

                return;
            }

            ApplyContinuousMotion();
        }

        private void ApplyContinuousMotion()
        {
            var model = playback.Model;
            var sample = ContinuousMotionSampler.Sample(model.Frames, model.ContinuousElapsedSeconds);
            SetValue(depthValue, $"{sample.DepthM:0.0}", "m");
            SetValue(headingValue, $"{sample.HeadingDeg:0.0}", "°");
            SetValue(pitchValue, $"{sample.PitchDeg:0.0}", "°");
            SetValue(rollValue, $"{sample.RollDeg:0.0}", "°");
            SetValue(yawValue, $"{sample.HeadingDeg:0.0}", "°");
            SetValue(latitudeValue, sample.HasUsableCoordinates ? $"{sample.LatitudeDeg:0.000000}" : "-", "°");
            SetValue(longitudeValue, sample.HasUsableCoordinates ? $"{sample.LongitudeDeg:0.000000}" : "-", "°");

            var velocity = sample.DisplayVelocityEnuMps;
            SetValue(velocityXValue, $"{velocity.x:0.00}", "m/s");
            SetValue(velocityYValue, $"{velocity.y:0.00}", "m/s");
            SetValue(velocityZValue, $"{velocity.z:0.00}", "m/s");
            SetValue(verticalSpeedValue, $"{velocity.y:0.00}", "m/s");
            SetValue(horizontalSpeedValue, $"{new Vector2(velocity.x, velocity.z).magnitude:0.00}", "m/s");

            var origin = model.GetFrame(0);
            if (sample.HasUsableCoordinates && TelemetryPositionUtility.HasUsableCoordinates(origin))
            {
                var localPosition = LocalMissionCoordinateConverter.ToLocalPosition(
                    sample.LongitudeDeg,
                    sample.LatitudeDeg,
                    sample.DepthM,
                    origin.LongitudeDeg,
                    origin.LatitudeDeg);
                SetValue(speedValue, FormatHorizontalDisplacement(new Vector2(localPosition.x, localPosition.z)), string.Empty);
            }
            else
            {
                SetValue(speedValue, "-", string.Empty);
            }

            var missionTimeText = FormatDuration(sample.ElapsedSeconds - model.StartElapsedSeconds);
            SetValue(missionTimeValue, missionTimeText, "s", missionTimeText);
            var lowerIndex = Mathf.Clamp(sample.LowerIndex, 0, cumulativeDistanceMeters.Length - 1);
            var upperIndex = Mathf.Clamp(sample.UpperIndex, 0, cumulativeDistanceMeters.Length - 1);
            var distance = Mathf.Lerp(cumulativeDistanceMeters[lowerIndex], cumulativeDistanceMeters[upperIndex], sample.Interpolation01);
            SetValue(distanceValue, $"{distance / 1000f:0.00}", "km");

            if (sample.HasDiagnostics)
            {
                var current = sample.CurrentVelocityEndMps;
                SetValue(oceanCurrentValue, $"东 {current.x:0.00} 北 {current.z:0.00}", "m/s",
                    $"东 {current.x:0.00} m/s 北 {current.z:0.00} m/s");
                SetValue(waterSpeedValue, $"{sample.WaterVelocityEndMps.magnitude:0.00}", "m/s");
                SetValue(groundSpeedValue, $"{sample.GroundVelocityEndMps.magnitude:0.00}", "m/s");
            }
            else
            {
                var profileCurrent = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                    ? RuntimeDataSourceState.SimulationProfile?.OceanCurrentProfile?.GetVelocity(sample.DepthM)
                    : null;
                SetValue(oceanCurrentValue, profileCurrent.HasValue
                    ? $"东 {profileCurrent.Value.x:0.00} 北 {profileCurrent.Value.y:0.00}"
                    : "-", "m/s", profileCurrent.HasValue
                    ? $"东 {profileCurrent.Value.x:0.00} m/s 北 {profileCurrent.Value.y:0.00} m/s"
                    : "-");
                SetValue(waterSpeedValue, "-", "m/s");
                SetValue(groundSpeedValue, "-", "m/s");
            }
        }

        private void SetValue(Text value, string text, string unit, string inlineText = null)
        {
            if (value == null)
            {
                return;
            }

            var unitText = value.transform.parent?.Find(value.name + "Unit")?.GetComponent<Text>();
            if (unitText != null)
            {
                value.text = text;
                unitText.text = unit ?? string.Empty;
                unitText.gameObject.SetActive(!string.IsNullOrEmpty(unitText.text));
                return;
            }

            // The reference HUD and minimal bindings keep units in the value text.
            value.text = inlineText ?? (text == "-" || string.IsNullOrEmpty(unit)
                ? text
                : text + (unit == "°" ? string.Empty : " ") + unit);
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

        private void RefreshDistanceCache(PlaybackModel model)
        {
            if (model == null || model.Frames == null)
            {
                cumulativeDistanceMeters = null;
                distanceCacheFrames = null;
                return;
            }

            if (ReferenceEquals(distanceCacheFrames, model.Frames)
                && cumulativeDistanceMeters != null
                && cumulativeDistanceMeters.Length == model.FrameCount)
            {
                return;
            }

            cumulativeDistanceMeters = BuildDistanceCache(model);
            distanceCacheFrames = model.Frames;
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
