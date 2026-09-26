using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.UI;
using UnderwaterGliderTwin.Visualization;
using UnderwaterGliderTwin.Bootstrap;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class UiTests
    {
        private HashSet<int> objectsBeforeTest;

        [SetUp]
        public void SetUp()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == "Assets/Scenes/Main.unity" || activeScene.path == "Assets/Scenes/Welcome.unity")
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
            }

            RuntimeUiFallback.Reset();
            RuntimeUiFallback.AllowRuntimeFallback = true;
            LogAssert.ignoreFailingMessages = true;
            objectsBeforeTest = new HashSet<int>();
            foreach (var obj in Object.FindObjectsOfType<GameObject>(true))
            {
                objectsBeforeTest.Add(obj.GetInstanceID());
            }
        }

        [TearDown]
        public void TearDown()
        {
            RuntimePredictionState.SetModelKind(PredictionModelKind.Physics);
            RuntimePredictionState.SetEnabled(true);
            foreach (var obj in Object.FindObjectsOfType<GameObject>(true))
            {
                if (!objectsBeforeTest.Contains(obj.GetInstanceID()))
                {
                    Object.DestroyImmediate(obj);
                }
            }

            RuntimeUiFallback.Reset();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void DashboardView_DisplaysCurrentTelemetry()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            Assert.That(FindText("TelemetryTitle").text, Is.EqualTo("遥测数据"));
            Assert.That(FindText("DepthValue").text, Is.EqualTo("10.0"));
            Assert.That(FindText("BatteryValue").text, Is.EqualTo("15"));
            Assert.That(FindText("VelocityXValue").text, Is.EqualTo("0.00"));
            Assert.That(FindText("VelocityYValue").text, Is.EqualTo("0.00"));
            Assert.That(FindText("VelocityZValue").text, Is.EqualTo("0.00"));
        }

        [Test]
        public void DashboardView_BindsExistingTelemetryText()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var root = new GameObject("DashboardPanel", typeof(RectTransform));
            CreateText(root.transform, "深度Label");
            CreateText(root.transform, "电量Label");
            var depth = CreateText(root.transform, "DepthValue");
            var battery = CreateText(root.transform, "BatteryValue");
            var refs = new DashboardPanelRefs { panel = root.GetComponent<RectTransform>(), depthValue = depth, batteryValue = battery };
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Bind(refs, playback, prediction);

            Assert.That(depth.text, Is.EqualTo("10.0"));
            Assert.That(battery.text, Is.EqualTo("15"));
            Assert.That(root.transform.Find("DepthValueUnit"), Is.Null);
            Assert.That(root.transform.Find("BatteryValueUnit"), Is.Null);
            Assert.That(root.transform.Find("DepthRow/DepthValueUnit"), Is.Not.Null);
            Assert.That(root.transform.Find("BatteryRow/BatteryValueUnit"), Is.Not.Null);
            AssertNoUnitColumnsOutsideRows(root.transform);
        }

        [Test]
        public void DashboardView_FallbackSeparatesTelemetryValueAndUnitColumns()
        {
            var playback = CreatePlayback(Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, null);

            Assert.That(FindText("DepthValue").text, Does.Not.Contain("m"));
            Assert.That(FindText("DepthValueUnit").text, Is.EqualTo("m"));
        }

        [Test]
        public void StatusPanelView_BindsExistingStatusText()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var root = new GameObject("StatusPanel", typeof(RectTransform));
            CreateText(root.transform, "任务来源Label");
            CreateText(root.transform, "剩余电量Label");
            var mission = CreateText(root.transform, "MissionValue");
            var battery = CreateText(root.transform, "BatteryValue");
            var refs = new StatusPanelRefs { panel = root.GetComponent<RectTransform>(), missionValue = mission, batteryValue = battery };
            var status = new GameObject("Status").AddComponent<StatusPanelView>();

            status.Bind(refs, playback, new AlarmEvaluator(1000f, 1f, 90f), null, prediction);

            Assert.That(mission.text, Is.Not.Empty);
            Assert.That(battery.text, Is.EqualTo("15"));
            Assert.That(root.transform.Find("BatteryValueUnit"), Is.Null);
            Assert.That(root.transform.Find("BatteryRow/BatteryValueUnit").GetComponent<Text>().text, Is.EqualTo("%"));
            AssertNoUnitColumnsOutsideRows(root.transform);
        }

        [Test]
        public void DataInputView_BindsExistingCsvInputAndLoadButton()
        {
            var panel = new GameObject("DataInputPanel", typeof(RectTransform));
            var input = CreateInput(panel.transform, "CsvPathInput");
            var reload = new GameObject("LoadCsvButton", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            reload.transform.SetParent(panel.transform, false);
            var refs = new DataInputPanelRefs
            {
                panel = panel.GetComponent<RectTransform>()
            };
            refs.mission.csvPathInput = input;
            refs.mission.loadCsvButton = reload;
            var requestedPath = string.Empty;
            var view = new GameObject("DataInput").AddComponent<DataInputView>();
            var csvPath = CreateTempCsv();

            view.Bind(refs, csvPath, SimulationProfile.Default, null, path => requestedPath = path, _ => { }, null);
            reload.onClick.Invoke();

            Assert.That(input.text, Is.EqualTo(csvPath));
            Assert.That(requestedPath, Is.EqualTo(csvPath));
        }

        [Test]
        public void DataInputView_ClearDynamicRuntimeUiPreservesOceanLayerRowTemplate()
        {
            var rowsRoot = new GameObject("DynamicRowsRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            var template = new GameObject("RowTemplate", typeof(RectTransform)).GetComponent<RectTransform>();
            template.SetParent(rowsRoot, false);
            var dynamicRow = new GameObject("DynamicRow", typeof(RectTransform)).GetComponent<RectTransform>();
            dynamicRow.SetParent(rowsRoot, false);
            var refs = new DataInputPanelRefs();
            refs.ocean.dynamicRowsRoot = rowsRoot;
            refs.ocean.oceanLayerRowTemplate = template;
            var view = new GameObject("DataInput").AddComponent<DataInputView>();

            view.BindDynamicContainersForTests(refs);
            view.ClearDynamicRuntimeUi();

            Assert.That(template, Is.Not.Null);
            Assert.That(template.gameObject, Is.Not.Null);
            Assert.That(dynamicRow == null, Is.True);
        }

        [Test]
        public void DataInputView_BoundModalDoesNotCreateAdditionalCanvas()
        {
            var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas));
            var modalRoot = new GameObject("ModalRoot", typeof(RectTransform));
            modalRoot.transform.SetParent(canvasObject.transform, false);
            var drawer = new GameObject("OceanCurrentDrawer", typeof(RectTransform));
            drawer.transform.SetParent(modalRoot.transform, false);
            drawer.SetActive(false);
            var panel = new GameObject("DataInputPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasObject.transform, false);
            var toggle = new GameObject("OceanCurrentDrawerButton", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            toggle.transform.SetParent(panel.transform, false);
            var refs = new DataInputPanelRefs();
            refs.ocean.oceanCurrentDrawer = drawer.GetComponent<RectTransform>();
            refs.ocean.drawerButton = toggle;
            var view = panel.AddComponent<DataInputView>();
            var canvasCountBefore = Object.FindObjectsOfType<Canvas>(true).Length;

            view.Bind(refs, string.Empty, SimulationProfile.Default, null);
            toggle.onClick.Invoke();

            Assert.That(drawer.activeSelf, Is.True);
            Assert.That(Object.FindObjectsOfType<Canvas>(true).Length, Is.EqualTo(canvasCountBefore));
            Assert.That(drawer.transform.parent, Is.EqualTo(modalRoot.transform));
        }

        [Test]
        public void OceanCurrentLayerRowView_BindWithMissingRefsDoesNotThrow()
        {
            var row = new GameObject("OceanCurrentLayerRow").AddComponent<OceanCurrentLayerRowView>();

            LogAssert.Expect(LogType.Error, "OceanCurrentLayerRow.prefab: OceanCurrentLayerRow -> titleText: Required UI reference is missing.");
            LogAssert.Expect(LogType.Error, "OceanCurrentLayerRow.prefab: OceanCurrentLayerRow -> depthRangeText: Required UI reference is missing.");
            LogAssert.Expect(LogType.Error, "OceanCurrentLayerRow.prefab: OceanCurrentLayerRow -> velocityText: Required UI reference is missing.");
            LogAssert.Expect(LogType.Error, "OceanCurrentLayerRow.prefab: OceanCurrentLayerRow -> editButton: Required UI reference is missing.");
            LogAssert.Expect(LogType.Error, "OceanCurrentLayerRow.prefab: OceanCurrentLayerRow -> removeButton: Required UI reference is missing.");
            Assert.DoesNotThrow(() => row.Bind(0, new OceanCurrentLayer(0f, 25f, 0.10f, 0.20f), null, null));
        }

        [Test]
        public void OceanCurrentLayerRowView_BindWithNullCallbacksDoesNotThrowOnClick()
        {
            var rowObject = new GameObject("OceanCurrentLayerRow");
            var row = rowObject.AddComponent<OceanCurrentLayerRowView>();
            var title = CreateText(rowObject.transform, "TitleText");
            var depth = CreateText(rowObject.transform, "DepthRangeText");
            var velocity = CreateText(rowObject.transform, "VelocityText");
            var edit = new GameObject("EditButton", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            edit.transform.SetParent(rowObject.transform, false);
            var remove = new GameObject("RemoveButton", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            remove.transform.SetParent(rowObject.transform, false);
            var serialized = new UnityEditor.SerializedObject(row);
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("depthRangeText").objectReferenceValue = depth;
            serialized.FindProperty("velocityText").objectReferenceValue = velocity;
            serialized.FindProperty("editButton").objectReferenceValue = edit;
            serialized.FindProperty("removeButton").objectReferenceValue = remove;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            row.Bind(0, new OceanCurrentLayer(0f, 25f, 0.10f, 0.20f), null, null);

            Assert.DoesNotThrow(() => edit.onClick.Invoke());
            Assert.DoesNotThrow(() => remove.onClick.Invoke());
        }

        [Test]
        public void OceanCommandToolbarView_BindsExistingCommandReferences()
        {
            var visibleCount = new GameObject("VisibleArrowCount", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var follow = new GameObject("CameraFollowCommand", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            var refs = new OceanToolbarRefs { visibleArrowCount = visibleCount, cameraFollowCommand = follow };
            var toolbar = new GameObject("OceanToolbar").AddComponent<OceanCommandToolbarView>();

            toolbar.Bind(refs, null, null);

            Assert.That(visibleCount, Is.Not.Null);
            Assert.That(follow.onClick, Is.Not.Null);
        }

        [Test]
        public void PlaybackControlsView_BindsExistingPlaybackReferences()
        {
            var playback = CreatePlayback(Frames(2));
            var play = new GameObject("PlayPauseButton", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            var refs = new PlaybackControlsRefs { panel = new GameObject("PlaybackPanel", typeof(RectTransform)).GetComponent<RectTransform>(), playPauseButton = play };
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();

            controls.Bind(refs, playback, null, null, null);
            play.onClick.Invoke();

            Assert.That(playback.Model.IsPlaying, Is.True);
        }

        [Test]
        public void DashboardView_CreatesTheCommandCenterFrame()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            Assert.That(GameObject.Find("CommandCenterHeader"), Is.Not.Null);
            Assert.That(GameObject.Find("TelemetryPanel").GetComponent<Outline>(), Is.Not.Null);
            Assert.That(FindText("CommandCenterProductName").text, Is.EqualTo("UnderwaterGliderTwin"));
        }

        [Test]
        public void Task5_FeedbackComponents_ExposeAccessibleContracts()
        {
            var tooltipType = System.Type.GetType("UnderwaterGliderTwin.UI.UiTooltip, UnderwaterGliderTwin.Runtime");
            var controllerType = System.Type.GetType("UnderwaterGliderTwin.UI.UiTooltipController, UnderwaterGliderTwin.Runtime");
            var badgeType = System.Type.GetType("UnderwaterGliderTwin.UI.UiStateBadge, UnderwaterGliderTwin.Runtime");
            var focusType = System.Type.GetType("UnderwaterGliderTwin.UI.UiFocusVisual, UnderwaterGliderTwin.Runtime");

            Assert.That(tooltipType, Is.Not.Null, "UiTooltip must be a runtime component");
            Assert.That(controllerType, Is.Not.Null, "UiTooltipController must be a runtime component");
            Assert.That(badgeType, Is.Not.Null, "UiStateBadge must be a runtime component");
            Assert.That(focusType, Is.Not.Null, "UiFocusVisual must be a runtime component");
            Assert.That(tooltipType.GetProperty("Message"), Is.Not.Null);
            Assert.That(tooltipType.GetProperty("Host"), Is.Not.Null);
            Assert.That(controllerType.GetMethod("Show"), Is.Not.Null);
            Assert.That(controllerType.GetMethod("Hide"), Is.Not.Null);
            Assert.That(controllerType.GetMethod("RefreshPosition"), Is.Not.Null);
            Assert.That(badgeType.GetMethod("SetState"), Is.Not.Null);
            Assert.That(typeof(UnityEngine.EventSystems.ISelectHandler).IsAssignableFrom(focusType), Is.True);
            Assert.That(typeof(UnityEngine.EventSystems.IDeselectHandler).IsAssignableFrom(focusType), Is.True);
        }

        [Test]
        public void Task5_FeedbackDecorations_DoNotInterceptRaycastsOrResizeHost()
        {
            var canvasObject = new GameObject("Task5Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var uiRoot = UiFactory.EnsureResponsiveRuntimeLayout(canvas);
            var popup = uiRoot.parent.Find("ModalRoot/TooltipPopup");
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(popup.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);

            var buttonObject = new GameObject("Task5Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(uiRoot, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(140f, 36f);
            var originalSize = buttonRect.sizeDelta;
            var focusType = System.Type.GetType("UnderwaterGliderTwin.UI.UiFocusVisual, UnderwaterGliderTwin.Runtime");
            var badgeType = System.Type.GetType("UnderwaterGliderTwin.UI.UiStateBadge, UnderwaterGliderTwin.Runtime");
            var focus = buttonObject.AddComponent(focusType);
            focus = UiFactory.EnsureFocusVisual(buttonObject.GetComponent<Button>());
            var badgeObject = new GameObject("Task5Badge", typeof(RectTransform));
            badgeObject.transform.SetParent(uiRoot, false);
            var badge = badgeObject.AddComponent(badgeType);
            var stateKind = System.Enum.Parse(System.Type.GetType("UnderwaterGliderTwin.UI.UiStateKind, UnderwaterGliderTwin.Runtime"), "Warning");
            badgeType.GetMethod("SetState").Invoke(badge, new[] { stateKind, (object)"需要关注" });

            Assert.That(focus.GetComponent<Outline>(), Is.Not.Null);
            foreach (var image in focus.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject == buttonObject)
                {
                    continue;
                }

                Assert.That(image.raycastTarget, Is.False, image.name + " must be decorative");
            }

            var marker = badge.GetType().GetProperty("MarkerImage")?.GetValue(badge, null) as Image;
            var stateBar = badge.GetType().GetProperty("StateBar")?.GetValue(badge, null) as Image;
            Assert.That(marker, Is.Not.Null);
            Assert.That(stateBar, Is.Not.Null);
            Assert.That(marker.raycastTarget, Is.False);
            Assert.That(stateBar.raycastTarget, Is.False);
            Assert.That(buttonRect.sizeDelta, Is.EqualTo(originalSize));
        }

        [Test]
        public void Task5_AccessibleFeedback_PreservesManualHorizontalNavigation()
        {
            var root = new GameObject("Task5NavigationCanvas", typeof(RectTransform), typeof(Canvas));
            var playbackBar = new GameObject("PlaybackBar", typeof(RectTransform));
            playbackBar.transform.SetParent(root.transform, false);
            var speed1 = CreateNavigationButton(playbackBar.transform, "Speed1Button");
            var speed2 = CreateNavigationButton(playbackBar.transform, "Speed2Button");
            var toolbar = new GameObject("OceanCommandToolbar", typeof(RectTransform));
            toolbar.transform.SetParent(root.transform, false);
            var cameraFollow = CreateNavigationButton(toolbar.transform, "CameraFollowCommand");
            var cameraGlobal = CreateNavigationButton(toolbar.transform, "CameraGlobalCommand");
            SetHorizontalNavigation(speed1, speed2, Navigation.Mode.Explicit);
            SetHorizontalNavigation(cameraFollow, cameraGlobal, Navigation.Mode.Explicit);

            UiFactory.ApplyAccessibleFeedback(root.transform);

            AssertHorizontalNavigationPreserved(speed1, speed2);
            AssertHorizontalNavigationPreserved(cameraFollow, cameraGlobal);
        }

        [Test]
        public void DashboardView_CreatesNavigationReferenceCard()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            Assert.That(GameObject.Find("NavigationReferenceCard"), Is.Not.Null);
            Assert.That(FindText("NavigationCardNorth").text, Is.EqualTo("N"));
        }

        [Test]
        public void DashboardView_ThrottlesPlaybackButAllowsSeekUpdate()
        {
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            Assert.That(dashboard.ShouldUpdateForFrame(0f, FrameUpdateReason.Initial), Is.True);
            Assert.That(dashboard.ShouldUpdateForFrame(0.01f, FrameUpdateReason.Playback), Is.False);
            Assert.That(dashboard.ShouldUpdateForFrame(0.01f, FrameUpdateReason.Seek), Is.True);
        }

        [Test]
        public void DashboardView_AllowsRebuildRefreshWithinThePlaybackThrottleWindow()
        {
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            Assert.That(dashboard.ShouldUpdateForFrame(1f, FrameUpdateReason.Initial), Is.True);
            Assert.That(dashboard.ShouldUpdateForFrame(1.01f, FrameUpdateReason.Rebuild), Is.True);
        }

        [Test]
        public void DashboardView_RebuildsDistanceCacheWhenFramesAreReplacedAndExtended()
        {
            var originalFrames = Frames(2);
            var playback = CreatePlayback(originalFrames);
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();
            dashboard.Initialize(playback, null);

            var replacementFrames = new List<TelemetryFrame>
            {
                originalFrames[0],
                WithCoordinates(originalFrames[1], 120.001, 25, 1000f),
                WithCoordinates(originalFrames[1], 120.002, 25, 2000f)
            };
            playback.Model.ReplaceFrames(replacementFrames, 0);
            playback.Seek(1f);

            Assert.That(FindText("DistanceValue").text, Is.Not.EqualTo("0.00"));
        }

        [Test]
        public void DashboardView_MarksUnavailableSampleCoordinatesInsteadOfShowingZero()
        {
            var framesWithNoCoordinates = Frames(2);
            var missingCoordinateFrames = new List<TelemetryFrame>
            {
                WithCoordinates(framesWithNoCoordinates[0], 0, 0),
                WithCoordinates(framesWithNoCoordinates[1], 0, 0)
            };
            var playback = CreatePlayback(missingCoordinateFrames);
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, null);

            Assert.That(FindText("LatitudeValue").text, Is.EqualTo("-"));
            Assert.That(FindText("LongitudeValue").text, Is.EqualTo("-"));
        }

        [Test]
        public void StatusPanelView_DisplaysAlarmAndWritesLog()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-log-" + System.Guid.NewGuid().ToString("N"));
            var logger = new TwinLogger(logDirectory);
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(playback, new AlarmEvaluator(5f, 20f, 20f), logger, prediction);

            Assert.That(FindText("AlarmValue").text, Does.Contain("深度"));
            Assert.That(File.ReadAllText(Path.Combine(logDirectory, "alarm.log")), Does.Contain("深度"));
        }

        [Test]
        public void PlaybackControlsView_ButtonsAndSliderDrivePlayback()
        {
            var frames = Frames(10);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();
            var exitRequested = false;
            controls.Initialize(playback, cameraController, environment, trajectory, () => exitRequested = true);
            GameObject.Find("PlayPauseButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Speed2Button").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("ProgressSlider").GetComponent<Slider>().value = 0.5f;
            GameObject.Find("TrajectoryToggle").GetComponent<Toggle>().isOn = true;
            GameObject.Find("ReverseButton").GetComponent<Button>().onClick.Invoke();
            playback.Step(1f);
            GameObject.Find("ExitButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(playback.Model.IsPlaying, Is.True);
            Assert.That(playback.Model.Speed, Is.EqualTo(2f));
            Assert.That(playback.Model.Direction, Is.EqualTo(-1));
            Assert.That(playback.Model.CurrentIndex, Is.EqualTo(2).Or.EqualTo(3));
            Assert.That(exitRequested, Is.True);
        }

        [Test]
        public void OceanCommandToolbarView_ChangesOnlyTheCameraMode()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("ToolbarCamera").AddComponent<TwinCameraController>();
            var target = new GameObject("ToolbarTarget");
            cameraController.Initialize(target.transform, new[] { Vector3.zero, Vector3.one });
            var trajectory = new GameObject("ToolbarTrajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var toolbar = new GameObject("Toolbar").AddComponent<OceanCommandToolbarView>();

            toolbar.Initialize(cameraController, trajectory);
            GameObject.Find("CameraTopCommand").GetComponent<Button>().onClick.Invoke();

            Assert.That(cameraController.CurrentMode, Is.EqualTo(CameraMode.Top));
            Assert.That(playback.Model.IsPlaying, Is.False);
            Assert.That(GameObject.Find("OceanVisibleArrowCount"), Is.Not.Null);
            Assert.That(GameObject.Find("OceanVisibleArrowCount").GetComponent<Text>().text, Does.Contain("/ 360"));
            Assert.That(GameObject.Find("OceanViewportFrame").GetComponent<Image>().color.a, Is.LessThan(0.05f));
            Assert.That(GameObject.Find("OceanViewportBorderTop"), Is.Not.Null);
            Assert.That(FindText("OceanToolbarTitle").rectTransform.anchorMin.x, Is.EqualTo(0f));
        }

        [Test]
        public void OceanCommandToolbarView_ReservesGuttersAroundTheCentralViewport()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("ToolbarCamera").AddComponent<TwinCameraController>();
            var target = new GameObject("ToolbarTarget");
            cameraController.Initialize(target.transform, new[] { Vector3.zero, Vector3.one });
            var trajectory = new GameObject("ToolbarTrajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            new GameObject("Toolbar").AddComponent<OceanCommandToolbarView>().Initialize(cameraController, trajectory);

            var viewport = GameObject.Find("OceanViewportFrame").GetComponent<RectTransform>();
            Assert.That(viewport.sizeDelta.x, Is.LessThanOrEqualTo(-760f));
            Assert.That(viewport.sizeDelta.y, Is.LessThanOrEqualTo(-520f));
        }

        [Test]
        public void CurrentLookupFailureMessage_DescribesTheRetainedNetworkField()
        {
            var profile = SimulationProfile.Default;
            profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
            profile.OceanCurrentField = new OceanCurrentField(new[]
            {
                new OceanCurrentFieldSample(140d, 15d, 10f, 0f, 0.1f, 0.2f)
            });

            var message = DataInputView.BuildCurrentLookupFailureMessage(profile, "timeout");

            Assert.That(message, Does.Contain("保留当前联网格点场"));
            Assert.That(message, Does.Contain("可重试"));
        }

        [Test]
        public void CurrentLookupFailureMessage_DescribesTheRetainedLayeredField()
        {
            var profile = SimulationProfile.Default;
            profile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.LayeredPreferred;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 100f, 0.1f, 0.2f),
                new OceanCurrentLayer(100f, 300f, 0.2f, 0.1f)
            });

            var message = DataInputView.BuildCurrentLookupFailureMessage(profile, "timeout");

            Assert.That(message, Does.Contain("保留当前 2 层分层场"));
            Assert.That(message, Does.Contain("可重试"));
        }

        [Test]
        public void PlaybackControlsView_ExportUsesSuppliedScreenshotCallback()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();
            var callbackWasCalled = false;

            controls.Initialize(playback, cameraController, environment, trajectory,
                onScreenshotRequested: () => { callbackWasCalled = true; return @"C:\\capture\\ui.png"; });
            GameObject.Find("ExportButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(callbackWasCalled, Is.True);
            Assert.That(FindText("PlaybackStatus").text, Does.Contain("ui.png"));
        }

        [Test]
        public void DashboardView_DisplaysComputedAxisVelocities()
        {
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120.0, 25.0, 10f, 100f, 30f, 4f, -3f, 28.5f, 0.3f, 15f, "mode", "state", 2f, 44f, 120f, 80f, 300f, 17f, 32f),
                new TelemetryFrame(1, "t1", 10f, 120.0001, 25.0001, 13f, 100f, 31f, 4f, -3f, 28.5f, 0.3f, 15f, "mode", "state", 2f, 44f, 120f, 80f, 300f, 17f, 32f)
            };
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);
            playback.Seek(1f);

            Assert.That(FindText("VelocityXValue").text, Is.EqualTo("1.01"));
            Assert.That(FindText("VelocityYValue").text, Is.EqualTo("-0.30"));
            Assert.That(FindText("VelocityZValue").text, Is.EqualTo("1.11"));
            Assert.That(FindText("HorizontalDisplacementValue").text, Is.EqualTo("E 10.1 N 11.1 |15.0| m"));
        }

        [Test]
        public void DashboardView_DisplaysCurrentForSimulationDepth()
        {
            var profile = SimulationProfile.Default;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[] { new OceanCurrentLayer(0f, 50f, 0.2f, -0.4f) });
            RuntimeDataSourceState.UseSimulation(profile);
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            Assert.That(FindText("OceanCurrentValue").text, Is.EqualTo("东 0.20 北 -0.40"));
        }

        [Test]
        public void DataInputView_CreatesCsvInputAndInvokesLoad()
        {
            var tempCsv = Path.Combine(Application.temporaryCachePath, "reload.csv");
            File.WriteAllText(tempCsv, "x");
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            var loadedPath = string.Empty;

            dataInput.Initialize(tempCsv, SimulationProfile.Default, null, path => loadedPath = path);
            GameObject.Find("LoadCsvButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(GameObject.Find("CsvPathInput").GetComponent<InputField>().text, Is.EqualTo(tempCsv));
            Assert.That(loadedPath, Is.EqualTo(tempCsv));
            Assert.That(GameObject.Find("CsvPathInputText").GetComponent<Text>().raycastTarget, Is.False);
            Assert.That(GameObject.Find("CsvPathInputPlaceholder").GetComponent<Text>().raycastTarget, Is.False);
            Assert.That(GameObject.Find("SimulationApplyButton"), Is.Not.Null);
            Assert.That(GameObject.Find("PredictionHorizonInput"), Is.Not.Null);
            Assert.That(GameObject.Find("XGBoostModelButton"), Is.Not.Null);
            Assert.That(GameObject.Find("DataSourceConfigurationGroup"), Is.Not.Null);
            Assert.That(GameObject.Find("OceanConfigurationGroup"), Is.Not.Null);
        }

        [Test]
        public void DataInputView_ReinitializeReplacesThePanelOnce()
        {
            var view = new GameObject("DataInput").AddComponent<DataInputView>();
            view.Initialize(CreateTempCsv(), SimulationProfile.Default, null);
            var firstCount = FindChildrenNamed(view.transform, "MissionConfigurationPanel").Count;

            view.Initialize(CreateTempCsv(), SimulationProfile.Default, null);
            var secondCount = FindChildrenNamed(view.transform, "MissionConfigurationPanel").Count;

            Assert.That(firstCount, Is.EqualTo(1));
            Assert.That(secondCount, Is.EqualTo(1));
        }

        [Test]
        public void DataInputView_ReinitializeDoesNotDuplicateLoadListeners()
        {
            var view = new GameObject("DataInput").AddComponent<DataInputView>();
            var count = 0;
            view.Initialize(CreateTempCsv(), SimulationProfile.Default, null, onLoadRequested: _ => count++);
            view.Initialize(CreateTempCsv(), SimulationProfile.Default, null, onLoadRequested: _ => count++);

            FindChildNamed(view.transform, "LoadCsvButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void DataInputView_RebindDoesNotDuplicateDrawerToggleOrLoadListeners()
        {
            var panel = new GameObject("MissionConfigurationPanel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            var csvInput = new GameObject("CsvPathInput", typeof(RectTransform), typeof(InputField)).GetComponent<InputField>();
            csvInput.transform.SetParent(panel, false);
            var loadButton = new GameObject("LoadCsvButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            loadButton.transform.SetParent(panel, false);
            var status = CreateText(panel, "MissionConfigurationStatus");
            var refs = new DataInputPanelRefs
            {
                configurationPanel = panel,
                statusText = status
            };
            refs.mission.csvPathInput = csvInput;
            refs.mission.loadCsvButton = loadButton;
            var view = new GameObject("DataInput").AddComponent<DataInputView>();
            var loadCount = 0;
            var csvPath = CreateTempCsv();

            view.Bind(refs, csvPath, SimulationProfile.Default, null, onLoadRequested: _ => loadCount++);
            view.Bind(refs, csvPath, SimulationProfile.Default, null, onLoadRequested: _ => loadCount++);

            var toggleButton = FindChildNamed(panel, "MissionConfigurationDrawerToggleButton").GetComponent<Button>();
            Assert.That(view.ConfigurationExpandedForTests, Is.False);
            toggleButton.onClick.Invoke();
            Assert.That(view.ConfigurationExpandedForTests, Is.True);
            toggleButton.onClick.Invoke();
            Assert.That(view.ConfigurationExpandedForTests, Is.False);

            csvInput.text = csvPath;
            loadButton.onClick.Invoke();
            Assert.That(loadCount, Is.EqualTo(1));
        }

        [Test]
        public void DataInputView_DoesNotLeaveDuplicatePanelsInTheScene()
        {
            var view = new GameObject("DataInput").AddComponent<DataInputView>();
            view.Initialize(CreateTempCsv(), SimulationProfile.Default, null);
            view.Initialize(CreateTempCsv(), SimulationProfile.Default, null);

            Assert.That(FindChildrenNamed(view.transform, "MissionConfigurationPanel").Count, Is.EqualTo(1));
        }

        [Test]
        public void CommandCenterConfigurationStrip_UsesFramedTopDrawer()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var strip = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            Assert.That(strip.GetComponent<Outline>(), Is.Not.Null);
            Assert.That(strip.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(strip.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(strip.anchoredPosition.y, Is.LessThanOrEqualTo(-48f));
            Assert.That(strip.sizeDelta.y, Is.LessThanOrEqualTo(48f));
        }

        [Test]
        public void MissionConfigurationDrawerReservesHeaderSafeArea()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var drawer = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            var header = GameObject.Find("CommandCenterHeader").GetComponent<RectTransform>();
            Assert.That(drawer.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(drawer.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(drawer.anchoredPosition.y, Is.LessThanOrEqualTo(-header.sizeDelta.y));
        }

        [Test]
        public void ParameterEditorKeepsEachLabelWithItsInput()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            FindChildNamed(dataInput.transform, "OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            var label = GameObject.Find("DynamicsMassInputLabel").transform;
            var input = GameObject.Find("DynamicsMassInput").transform;
            Assert.That(input.parent, Is.SameAs(label.parent));
            Assert.That(input.parent.name, Is.EqualTo("DynamicsMassInputField"));
        }

        [Test]
        public void ExpandedTopDrawerKeepsToggleButtonOutsideViewportHitArea()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var drawer = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            var summary = GameObject.Find("ConfigurationSummaryBar").GetComponent<RectTransform>();
            var expanded = GameObject.Find("ConfigurationExpandedContent").GetComponent<RectTransform>();
            var header = GameObject.Find("MissionConfigurationDrawerHeader").GetComponent<RectTransform>();
            var viewport = GameObject.Find("MissionConfigurationViewport").GetComponent<RectTransform>();
            GameObject.Find("MissionConfigurationDrawerToggleButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(drawer.sizeDelta.y, Is.GreaterThan(48f));
            Assert.That(summary.GetSiblingIndex(), Is.GreaterThan(expanded.GetSiblingIndex()));
            Assert.That(viewport.offsetMax.y, Is.LessThanOrEqualTo(-header.sizeDelta.y));
        }

        [Test]
        public void MissionConfigurationIsReachableFromCommandCenterHeader()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var button = GameObject.Find("CommandCenterMissionConfigButton");
            var panel = GameObject.Find("MissionConfigurationPanel");
            Assert.That(button, Is.Not.Null);
            Assert.That(panel.activeSelf, Is.True);
            button.GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(dataInput.ConfigurationExpandedForTests, Is.True);

            button.GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(dataInput.ConfigurationExpandedForTests, Is.False);
        }

        [Test]
        public void BottomDrawerIsCollapsedByDefaultAndCapsItsExpandedHeight()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var drawer = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            Assert.That(drawer.sizeDelta.y, Is.LessThanOrEqualTo(48f));
            GameObject.Find("MissionConfigurationDrawerToggleButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(drawer.sizeDelta.y, Is.LessThanOrEqualTo(1080f * 0.35f));
            Assert.That(GameObject.Find("MissionConfigurationViewport").GetComponent<Mask>(), Is.Not.Null);
            Assert.That(GameObject.Find("MissionConfigurationContent").GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(GameObject.Find("MissionSectionCard"), Is.Not.Null);
            Assert.That(GameObject.Find("SimulationSectionCard"), Is.Not.Null);
            Assert.That(GameObject.Find("OceanSectionCard"), Is.Not.Null);
        }

        [Test]
        public void ExpandedTaskParameterDrawerUsesCompactSpacing()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            GameObject.Find("MissionConfigurationDrawerToggleButton").GetComponent<Button>().onClick.Invoke();

            var drawer = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            var content = GameObject.Find("MissionConfigurationContent").GetComponent<VerticalLayoutGroup>();
            var missionSection = GameObject.Find("MissionSectionCard").GetComponent<VerticalLayoutGroup>();
            var missionFields = GameObject.Find("MissionSectionFields").GetComponent<GridLayoutGroup>();
            var field = GameObject.Find("SimulationCyclesInputField").GetComponent<RectTransform>();

            Assert.That(drawer.sizeDelta.y, Is.LessThanOrEqualTo(320f));
            Assert.That(content.spacing, Is.LessThanOrEqualTo(6f));
            Assert.That(missionSection.spacing, Is.LessThanOrEqualTo(4f));
            Assert.That(missionFields.spacing.y, Is.LessThanOrEqualTo(4f));
            Assert.That(field.sizeDelta.y, Is.InRange(60f, 64f));
        }

        [Test]
        public void TaskParameterFieldCardsUseFixedWidth()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            Canvas.ForceUpdateCanvases();

            var cycles = GameObject.Find("SimulationCyclesInputField").GetComponent<RectTransform>();
            var duration = GameObject.Find("SimulationDurationInputField").GetComponent<RectTransform>();
            var csv = GameObject.Find("CsvPathField").GetComponent<RectTransform>();
            Assert.That(cycles.rect.width, Is.EqualTo(280f).Within(0.1f));
            Assert.That(duration.rect.width, Is.EqualTo(280f).Within(0.1f));
            Assert.That(csv.rect.width, Is.EqualTo(280f).Within(0.1f));
        }

        [Test]
        public void TaskParameterFieldsUseSectionCardsAndResponsiveColumns()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var field = GameObject.Find("SimulationCyclesInputField").transform;
            var label = GameObject.Find("SimulationCyclesInputLabel").transform;
            var input = GameObject.Find("SimulationCyclesInput").transform;
            Assert.That(field.IsChildOf(GameObject.Find("SimulationSectionCard").transform), Is.True);
            Assert.That(label.parent, Is.SameAs(field));
            Assert.That(input.parent, Is.SameAs(field));

            Assert.That(GameObject.Find("MissionSectionFields").GetComponent<GridLayoutGroup>(), Is.Not.Null);
            Assert.That(GameObject.Find("SimulationSectionFields").GetComponent<GridLayoutGroup>(), Is.Not.Null);
            Assert.That(GameObject.Find("OceanSectionFields").GetComponent<GridLayoutGroup>(), Is.Not.Null);
            var layout = GameObject.Find("MissionSectionFields").GetComponent<ResponsiveTaskParameterLayout>();
            var grid = GameObject.Find("MissionSectionFields").GetComponent<GridLayoutGroup>();
            layout.RefreshForWidth(1920f);
            Assert.That(layout.ColumnCount, Is.EqualTo(4));
            Assert.That(grid.cellSize.x, Is.EqualTo(280f).Within(0.1f));
            layout.RefreshForWidth(1280f);
            Assert.That(layout.ColumnCount, Is.EqualTo(4));
            Assert.That(grid.cellSize.x, Is.EqualTo(280f).Within(0.1f));
            layout.RefreshForWidth(900f);
            Assert.That(layout.ColumnCount, Is.EqualTo(3));
            Assert.That(grid.cellSize.x, Is.EqualTo(280f).Within(0.1f));
            layout.RefreshForWidth(600f);
            Assert.That(layout.ColumnCount, Is.EqualTo(2));
            Assert.That(grid.cellSize.x, Is.EqualTo(280f).Within(0.1f));
            layout.RefreshForWidth(560f);
            Assert.That(layout.ColumnCount, Is.EqualTo(1));
            Assert.That(grid.cellSize.x, Is.EqualTo(280f).Within(0.1f));
        }

        [Test]
        public void OceanCurrentEditorUsesHigherSortingModalAndRaycastBlocker()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            FindChildNamed(dataInput.transform, "OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            var modal = GameObject.Find("OceanCurrentModalCanvas");
            var main = GameObject.Find("RuntimeCanvas").GetComponent<Canvas>();
            var blocker = GameObject.Find("OceanCurrentModalRaycastBlocker").GetComponent<Image>();
            Assert.That(modal.GetComponent<Canvas>(), Is.Null);
            Assert.That(modal.transform.IsChildOf(main.transform), Is.True);
            Assert.That(modal.transform.parent.name, Is.EqualTo("ModalRoot"));
            Assert.That(blocker.raycastTarget, Is.True);
            Assert.That(GameObject.Find("OceanCurrentDrawerPanel").transform.IsChildOf(modal.transform), Is.True);
        }

        [Test]
        public void InvalidDrawerInputDoesNotInvokeSimulationCallback()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile submitted = null;
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => submitted = profile);

            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("DynamicsMassInput").GetComponent<InputField>().text = "not-a-number";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(submitted, Is.Null);
            Assert.That(FindText("MissionConfigurationStatus").text, Does.Contain("质量"));
        }

        [Test]
        public void NonlinearDynamicsControls_RejectInvalidProfileWithoutInvokingSimulation()
        {
            var view = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile submitted = null;
            view.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => submitted = profile);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            var exponent = GameObject.Find("DynamicsBuoyancyExponentInput");
            Assert.That(exponent, Is.Not.Null, "The nonlinear buoyancy exponent must be exposed as a runtime control.");
            exponent.GetComponent<InputField>().text = "4";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(submitted, Is.Null);
            Assert.That(FindText("MissionConfigurationStatus").text, Does.Contain("浮力曲线"));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void NonFiniteDrawerInputDoesNotInvokeSimulationCallback(string invalidValue)
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile submitted = null;
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => submitted = profile);
            GameObject.Find("SimulationDepthInput").GetComponent<InputField>().text = invalidValue;
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(submitted, Is.Null);
        }

        [Test]
        public void CurrentAndFlightEditorsRemainInBottomSafeArea()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            var current = FindChildNamed(dataInput.transform, "OceanCurrentDrawerPanel").GetComponent<RectTransform>();
            var flight = FindChildNamed(dataInput.transform, "FlightLegDrawerPanel").GetComponent<RectTransform>();
            Assert.That(current.parent.name, Is.EqualTo("OceanCurrentModalCanvas"));
            Assert.That(current.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(current.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(flight.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(flight.sizeDelta.y, Is.LessThanOrEqualTo(1080f * .35f));
        }

        [Test]
        public void OceanEditorScrollsToEveryDynamicsControlInsideModalViewport()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            var drawer = GameObject.Find("OceanCurrentDrawerPanel").GetComponent<RectTransform>();
            var scroll = drawer.GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.viewport, Is.Not.Null);
            Assert.That(scroll.content, Is.Not.Null);
            Assert.That(GameObject.Find("DynamicsBaseDragInput").transform.IsChildOf(scroll.content), Is.True);
            Assert.That(GameObject.Find("DynamicsTurnaroundDurationInput").transform.IsChildOf(scroll.content), Is.True);
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.That(drawer.parent.name, Is.EqualTo("OceanCurrentModalCanvas"));
            Assert.That(drawer.sizeDelta.y, Is.LessThanOrEqualTo(760f));
        }

        [Test]
        public void OceanCurrentModalKeepsStatusAndFixedControlsOutsideScrollableContent()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            var drawer = GameObject.Find("OceanCurrentDrawerPanel").GetComponent<RectTransform>();
            var scroll = drawer.GetComponent<ScrollRect>();
            Assert.That(drawer.parent.name, Is.EqualTo("OceanCurrentModalCanvas"));
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.viewport, Is.Not.Null);
            Assert.That(FindText("OceanCurrentDrawerStatus").rectTransform.IsChildOf(scroll.viewport), Is.False);
            Assert.That(FindText("OceanCurrentDrawerTitle").rectTransform.IsChildOf(scroll.viewport), Is.False);
            AssertRectanglesDoNotOverlap(FindText("OceanCurrentDrawerStatus").rectTransform, scroll.viewport);
            AssertRectanglesDoNotOverlap(FindText("DynamicsParametersTitle").rectTransform, FindText("DynamicsMassInputLabel").rectTransform);
        }

        [Test]
        public void OceanCurrentModalLookupButtonTracksAcquisitionMode()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            GameObject.Find("OceanCurrentLocalFileModeButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("OceanCurrentDrawerLookupButton").GetComponentInChildren<Text>().text, Is.EqualTo("加载本地文件"));

            GameObject.Find("OceanCurrentCacheOnlyModeButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("OceanCurrentDrawerLookupButton").GetComponentInChildren<Text>().text, Is.EqualTo("读取缓存"));

            GameObject.Find("OceanCurrentOnlineModeButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("OceanCurrentDrawerLookupButton").GetComponentInChildren<Text>().text, Is.EqualTo("联网获取海流"));
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void OceanCurrentModalUsesScrollableColumnLayoutWithoutOverlap(int width, int height)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(width, height);
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Canvas.ForceUpdateCanvases();

            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            var drawer = GameObject.Find("OceanCurrentDrawerPanel").GetComponent<RectTransform>();
            var scroll = drawer.GetComponent<ScrollRect>();
            var content = GameObject.Find("EditorContent").GetComponent<RectTransform>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
            Assert.That(GameObject.Find("OceanCurrentDrawerLookupButton").transform.IsChildOf(content), Is.True);
            Assert.That(GameObject.Find("DynamicsTurnaroundDurationInput").transform.IsChildOf(content), Is.True);
            AssertRectanglesDoNotOverlap(GameObject.Find("OceanCurrentDrawerLookupButton").GetComponent<RectTransform>(), GameObject.Find("DynamicsSeaTrialPresetButton").GetComponent<RectTransform>());
            AssertRectanglesDoNotOverlap(GameObject.Find("OceanCurrentDrawerLookupButton").GetComponent<RectTransform>(), GameObject.Find("DynamicsParametersTitle").GetComponent<RectTransform>());
        }

        [Test]
        public void ParameterDrawersUseFastMouseWheelScrolling()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var taskScroll = GameObject.Find("ConfigurationExpandedContent").GetComponent<ScrollRect>();
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            var oceanScroll = GameObject.Find("OceanCurrentDrawerPanel").GetComponent<ScrollRect>();
            GameObject.Find("OceanCurrentDrawerCloseButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("FlightLegSettingsButton").GetComponent<Button>().onClick.Invoke();
            var flightScroll = GameObject.Find("FlightLegDrawerPanel").GetComponent<ScrollRect>();

            Assert.That(RootScrollIsAbsentOrDisabled(GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>()), Is.True);
            Assert.That(taskScroll.scrollSensitivity, Is.GreaterThanOrEqualTo(45f));
            Assert.That(oceanScroll.scrollSensitivity, Is.GreaterThanOrEqualTo(45f));
            Assert.That(flightScroll.scrollSensitivity, Is.GreaterThanOrEqualTo(45f));
        }

        [Test]
        public void BoundParameterDrawersUseFastMouseWheelScrolling()
        {
            var panel = new GameObject("MissionConfigurationPanel", typeof(RectTransform), typeof(ScrollRect));
            var oceanDrawer = new GameObject("OceanCurrentDrawerPanel", typeof(RectTransform), typeof(ScrollRect));
            var flightDrawer = new GameObject("FlightLegDrawerPanel", typeof(RectTransform), typeof(ScrollRect));
            var refs = new DataInputPanelRefs
            {
                configurationPanel = panel.GetComponent<RectTransform>()
            };
            refs.ocean.oceanCurrentDrawer = oceanDrawer.GetComponent<RectTransform>();
            refs.flightLeg.drawer = flightDrawer.GetComponent<RectTransform>();
            var view = new GameObject("DataInput").AddComponent<DataInputView>();

            view.Bind(refs, "D:\\telemetry.csv", SimulationProfile.Default, null);

            var taskScroll = panel.transform.Find("ConfigurationExpandedContent").GetComponent<ScrollRect>();
            Assert.That(RootScrollIsAbsentOrDisabled(panel.GetComponent<RectTransform>()), Is.True);
            Assert.That(taskScroll.scrollSensitivity, Is.GreaterThanOrEqualTo(45f));
            Assert.That(oceanDrawer.GetComponent<ScrollRect>().scrollSensitivity, Is.GreaterThanOrEqualTo(45f));
            Assert.That(flightDrawer.GetComponent<ScrollRect>().scrollSensitivity, Is.GreaterThanOrEqualTo(45f));
        }

        [Test]
        public void MissionConfigurationUsesSingleInteractiveScrollRectInExpandedContent()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var drawer = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            var summary = GameObject.Find("ConfigurationSummaryBar").GetComponent<RectTransform>();
            var expanded = GameObject.Find("ConfigurationExpandedContent").GetComponent<RectTransform>();
            var enabledScrollCount = 0;
            ScrollRect enabledScroll = null;
            foreach (var scroll in drawer.GetComponentsInChildren<ScrollRect>(true))
            {
                if (!scroll.enabled)
                {
                    continue;
                }

                enabledScrollCount++;
                enabledScroll = scroll;
            }

            Assert.That(RootScrollIsAbsentOrDisabled(drawer), Is.True);
            Assert.That(summary.GetComponentInChildren<ScrollRect>(true), Is.Null);
            Assert.That(enabledScrollCount, Is.EqualTo(1));
            Assert.That(enabledScroll.transform, Is.SameAs(expanded));
            Assert.That(enabledScroll.viewport.name, Is.EqualTo("ConfigurationScrollViewport"));
            Assert.That(enabledScroll.content.name, Is.EqualTo("MissionConfigurationContent"));
        }

        [Test]
        public void DataInputView_MigratesShippedPrefabLegacyTitlesIntoExpandedContent()
        {
            var panel = InstantiateShippedDataInputPanel();
            var legacyTitles = FindLegacyConfigurationTitles(panel);
            var refs = new DataInputPanelRefs { panel = panel };
            var view = panel.gameObject.AddComponent<DataInputView>();

            view.Bind(refs, "D:\\telemetry.csv", SimulationProfile.Default, null);

            var expanded = panel.Find("ConfigurationExpandedContent");
            Assert.That(legacyTitles, Is.Not.Empty);
            Assert.That(legacyTitles.Exists(title => title.name == "ModelLabel"), Is.True);
            foreach (var legacyTitle in legacyTitles)
            {
                Assert.That(legacyTitle.transform.IsChildOf(expanded), Is.True, legacyTitle.name);
            }
        }

        [Test]
        public void DataInputView_CollapsedShippedPrefabDoesNotRenderLegacyConfigurationTitles()
        {
            var panel = InstantiateShippedDataInputPanel();
            var legacyTitles = FindLegacyConfigurationTitles(panel);
            var refs = new DataInputPanelRefs { panel = panel };
            var dataInput = panel.gameObject.AddComponent<DataInputView>();
            dataInput.Bind(refs, "D:\\telemetry.csv", SimulationProfile.Default, null);

            dataInput.SetConfigurationExpanded(false);
            Canvas.ForceUpdateCanvases();

            var drawer = panel.gameObject;
            var summary = drawer.transform.Find("ConfigurationSummaryBar");
            var expanded = drawer.transform.Find("ConfigurationExpandedContent");
            Assert.That(dataInput.gameObject.activeInHierarchy, Is.True);
            Assert.That(drawer.activeInHierarchy, Is.True);
            Assert.That(summary.gameObject.activeInHierarchy, Is.True);
            Assert.That(expanded.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            Assert.That(expanded.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(legacyTitles, Is.Not.Empty);
            foreach (var legacyTitle in legacyTitles)
            {
                Assert.That(legacyTitle.transform.IsChildOf(summary), Is.False, legacyTitle.name);
                Assert.That(legacyTitle.transform.IsChildOf(expanded), Is.True, legacyTitle.name);
                Assert.That(legacyTitle.canvasRenderer.GetAlpha(), Is.EqualTo(0f).Within(0.001f), legacyTitle.name);
            }
        }

        [Test]
        public void DataInputPanel_RemainsUnderConfigurationAreaAfterConfigurationSetup()
        {
            var canvas = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            var uiRoot = UiFactory.EnsureResponsiveRuntimeLayout(canvas);
            var configurationArea = uiRoot.Find("ConfigurationArea");
            var panel = new GameObject("DataInputPanel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(configurationArea, false);
            var refs = new DataInputPanelRefs { panel = panel };
            var view = panel.gameObject.AddComponent<DataInputView>();

            view.Bind(refs, "D:\\telemetry.csv", SimulationProfile.Default, null);

            Assert.That(panel.parent, Is.SameAs(configurationArea));
        }

        [Test]
        public void DataInputView_RepeatedConfigurationSetupPreservesShippedPrefabMigrationAndListeners()
        {
            var panel = InstantiateShippedDataInputPanel();
            var legacyTitles = FindLegacyConfigurationTitles(panel);
            var loadButton = FindChildNamed(panel, "LoadCsvButton").GetComponent<Button>();
            var csvInput = FindChildNamed(panel, "CsvPathInput").GetComponent<InputField>();
            var refs = new DataInputPanelRefs { panel = panel };
            refs.mission.loadCsvButton = loadButton;
            refs.mission.csvPathInput = csvInput;
            var view = panel.gameObject.AddComponent<DataInputView>();
            var loadCount = 0;
            var csvPath = CreateTempCsv();

            view.Bind(refs, csvPath, SimulationProfile.Default, null, _ => loadCount++);
            view.Bind(refs, csvPath, SimulationProfile.Default, null, _ => loadCount++);

            Assert.That(CountDirectChildrenNamed(panel, "ConfigurationSummaryBar"), Is.EqualTo(1));
            Assert.That(CountDirectChildrenNamed(panel, "ConfigurationExpandedContent"), Is.EqualTo(1));
            var expanded = panel.Find("ConfigurationExpandedContent");
            foreach (var legacyTitle in legacyTitles)
            {
                Assert.That(legacyTitle.transform.IsChildOf(expanded), Is.True, legacyTitle.name);
            }

            csvInput.text = csvPath;
            loadButton.onClick.Invoke();
            Assert.That(loadCount, Is.EqualTo(1));
        }

        [Test]
        public void MissionConfigurationStatusDoesNotOverlapOceanConfigurationButton()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            AssertRectanglesDoNotOverlap(
                FindText("MissionConfigurationStatus").rectTransform,
                GameObject.Find("OceanCurrentDrawerButton").GetComponent<RectTransform>());
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void BottomEditorsKeepScrolledControlsInsideMaskedViewport(int width, int height)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(.5f, .5f);
            canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.sizeDelta = new Vector2(width, height);
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Canvas.ForceUpdateCanvases();
            Assert.That(canvasRect.rect.width, Is.EqualTo((float)width).Within(.1f));
            Assert.That(canvasRect.rect.height, Is.EqualTo((float)height).Within(.1f));
            UiFactory.EnsureCanvas(canvas.transform);
            var dataInput = canvasObject.AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            var oceanScroll = GameObject.Find("OceanCurrentDrawerPanel").GetComponent<ScrollRect>();
            var oceanRect = oceanScroll.GetComponent<RectTransform>();
            Assert.That(oceanRect.parent.name, Is.EqualTo("OceanCurrentModalCanvas"));
            Assert.That(oceanRect.rect.height, Is.LessThanOrEqualTo(760f + .1f));
            Assert.That(oceanScroll.content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(oceanScroll.content.rect.height, Is.GreaterThan(oceanScroll.viewport.rect.height));
            Assert.That(GameObject.Find("OceanCurrentDrawerMinDepthInput").transform.IsChildOf(oceanScroll.content), Is.True);
            Assert.That(GameObject.Find("DynamicsBaseDragInput").transform.IsChildOf(oceanScroll.content), Is.True);

            GameObject.Find("FlightLegSettingsButton").GetComponent<Button>().onClick.Invoke();
            var flightScroll = GameObject.Find("FlightLegDrawerPanel").GetComponent<ScrollRect>();
            var boundCanvas = flightScroll.GetComponentInParent<Canvas>();
            var boundCanvasRect = boundCanvas != null ? boundCanvas.transform as RectTransform : canvasRect;
            var expectedDrawerHeight = Mathf.Min(boundCanvasRect.rect.height, 1080f) * .35f;
            Assert.That(flightScroll.GetComponent<RectTransform>().rect.height, Is.LessThanOrEqualTo(expectedDrawerHeight + .1f));
            flightScroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            AssertRectInsideViewport(GameObject.Find("DescentNetBuoyancyInput").GetComponent<RectTransform>(), flightScroll.viewport, width, height);
            flightScroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            AssertRectInsideViewport(GameObject.Find("AscentRollInput").GetComponent<RectTransform>(), flightScroll.viewport, width, height);
        }

        [Test]
        public void OutOfRangeMissionCoordinateDoesNotInvokeSimulationCallback()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile submitted = null;
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => submitted = profile);
            GameObject.Find("MissionLongitudeInput").GetComponent<InputField>().text = "180.01";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(submitted, Is.Null);
            GameObject.Find("MissionLongitudeInput").GetComponent<InputField>().text = "120";
            GameObject.Find("MissionLatitudeInput").GetComponent<InputField>().text = "-90.01";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(submitted, Is.Null);
        }

        [Test]
        public void InvalidOceanCurrentLayerLeavesTemplateUnchanged()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile submitted = null;
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => submitted = profile);
            GameObject.Find("OceanCurrentMinDepthInput").GetComponent<InputField>().text = "100";
            GameObject.Find("OceanCurrentMaxDepthInput").GetComponent<InputField>().text = "10";
            GameObject.Find("OceanCurrentSaveLayerButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(submitted.OceanCurrentProfile.Layers, Is.Empty);
        }

        [Test]
        public void DataInputView_UsesChineseMissionCopy()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            Assert.That(FindText("MissionConfigurationTitle").text, Is.EqualTo("任务配置"));
            Assert.That(FindText("LoadCsvButtonLabel").text, Is.EqualTo("加载 CSV"));
            Assert.That(FindText("SimulationApplyButtonLabel").text, Is.EqualTo("运行仿真"));
            Assert.That(FindText("CsvPathInputPlaceholder").text, Is.EqualTo("遥测 CSV 文件路径"));
            Assert.That(FindText("MissionConfigurationStatus").text, Is.EqualTo("CSV 回放和参数仿真均可用"));
            var missionBar = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            Assert.That(missionBar.anchorMin.x, Is.EqualTo(0f));
            Assert.That(missionBar.anchorMax.x, Is.EqualTo(1f));

            GameObject.Find("CsvPathInput").GetComponent<InputField>().text = string.Empty;
            GameObject.Find("LoadCsvButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(FindText("MissionConfigurationStatus").text, Is.EqualTo("请输入 CSV 文件路径"));

            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(FindText("OceanCurrentLocalFileInputPlaceholder").text, Is.EqualTo("本地 JSON/NetCDF 文件路径"));
        }

        [Test]
        public void DataInputView_UsesChineseCapableRuntimeFont()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var placeholder = FindText("CsvPathInputPlaceholder");
            Assert.That(placeholder.font, Is.Not.Null);
            Assert.That(placeholder.font.name, Is.Not.EqualTo("LegacyRuntime"));
        }

        [Test]
        public void DataInputView_BuildsSimulationProfileAndInvokesCallback()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("SimulationDepthInput").GetComponent<InputField>().text = "220";
            GameObject.Find("SimulationCyclesInput").GetComponent<InputField>().text = "4";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile, Is.Not.Null);
            Assert.That(capturedProfile.TargetDepthM, Is.EqualTo(220f).Within(0.01f));
            Assert.That(capturedProfile.CycleCount, Is.EqualTo(4));
        }

        [Test]
        public void DataInputView_AutoCorrectsCycleDurationWhenDepthChanges()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            var cycleDuration = GameObject.Find("SimulationDurationInput").GetComponent<InputField>();
            var targetDepth = GameObject.Find("SimulationDepthInput").GetComponent<InputField>();

            cycleDuration.text = "900";
            targetDepth.text = "1600";
            targetDepth.onEndEdit.Invoke(targetDepth.text);

            Assert.That(cycleDuration.text, Is.EqualTo("32940"));
        }

        [Test]
        public void DataInputView_ShowsAndAppliesTheDepthReferenceCycle()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            var cycleDuration = GameObject.Find("SimulationDurationInput").GetComponent<InputField>();
            var targetDepth = GameObject.Find("SimulationDepthInput").GetComponent<InputField>();

            targetDepth.text = "1600";
            targetDepth.onEndEdit.Invoke(targetDepth.text);

            Assert.That(FindText("ReferenceCycleDurationValue").text, Is.EqualTo("9 h 09 min"));
            cycleDuration.text = "900";
            GameObject.Find("ApplyReferenceCycleButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(cycleDuration.text, Is.EqualTo("32940"));
        }

        [Test]
        public void DataInputView_LabelsConfigurationFieldsWithUnits()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("telemetry.csv", SimulationProfile.Default, null);

            Assert.That(FindText("SimulationDurationInputLabel").text, Is.EqualTo("单航段安全上限 (s)"));
            Assert.That(FindText("SimulationDepthInputLabel").text, Is.EqualTo("深度 (m)"));
            Assert.That(GameObject.Find("SimulationSpeedInput"), Is.Null);
            Assert.That(GameObject.Find("EstimatedWaterSpeedLabel"), Is.Null);
            Assert.That(GameObject.Find("EstimatedWaterSpeedReadout"), Is.Null);
            Assert.That(FindText("MissionLongitudeInputLabel").text, Is.EqualTo("经度 (°)"));
            Assert.That(FindText("MissionLatitudeInputLabel").text, Is.EqualTo("纬度 (°)"));
        }

        [Test]
        public void DataInputView_UsesConfiguredMissionCoordinatesForSimulation()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("MissionLongitudeInput").GetComponent<InputField>().text = "121.4737";
            GameObject.Find("MissionLatitudeInput").GetComponent<InputField>().text = "31.2304";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile, Is.Not.Null);
            Assert.That(capturedProfile.OriginLongitudeDeg, Is.EqualTo(121.4737d).Within(0.000001d));
            Assert.That(capturedProfile.OriginLatitudeDeg, Is.EqualTo(31.2304d).Within(0.000001d));
        }

        [Test]
        public void DataInputView_ExposesMissionCoordinatesInTheMainConfigurationPanel()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("telemetry.csv", SimulationProfile.Default, null);

            Assert.That(GameObject.Find("MissionLongitudeInput"), Is.Not.Null);
            Assert.That(GameObject.Find("MissionLatitudeInput"), Is.Not.Null);
        }

        [Test]
        public void DataInputView_StoresRegionalCurrentPrefetchSettingsInSimulationProfile()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            var halfWidth = GameObject.Find("OceanCurrentPrefetchHalfWidthInput").GetComponent<InputField>();
            var forecastWindow = GameObject.Find("OceanCurrentForecastWindowInput").GetComponent<InputField>();
            Assert.That(halfWidth.text, Is.EqualTo("25"));
            Assert.That(forecastWindow.text, Is.EqualTo("72"));

            halfWidth.text = "40";
            forecastWindow.text = "96";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile.OceanCurrentPrefetchHalfWidthKm, Is.EqualTo(40f));
            Assert.That(capturedProfile.OceanCurrentForecastWindowHours, Is.EqualTo(96f));
        }

        [Test]
        public void DataInputView_AcceptsSimulationValuesOutsideFormerRanges()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("SimulationCyclesInput").GetComponent<InputField>().text = "250";
            GameObject.Find("SimulationDurationInput").GetComponent<InputField>().text = "15";
            GameObject.Find("SimulationDepthInput").GetComponent<InputField>().text = "5000";
            GameObject.Find("SimulationHeadingInput").GetComponent<InputField>().text = "1080";
            GameObject.Find("SimulationHeadingDeltaInput").GetComponent<InputField>().text = "720";
            GameObject.Find("SimulationPitchInput").GetComponent<InputField>().text = "120";
            GameObject.Find("SimulationRollInput").GetComponent<InputField>().text = "-90";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile, Is.Not.Null);
            Assert.That(capturedProfile.CycleCount, Is.EqualTo(250));
            Assert.That(capturedProfile.TargetDepthM, Is.EqualTo(5000f));
            Assert.That(capturedProfile.WaterColumnDepthM, Is.GreaterThanOrEqualTo(5000f));
            Assert.That(GameObject.Find("SimulationWaterColumnInput").GetComponent<InputField>().text, Is.EqualTo("5000"));
            Assert.That(capturedProfile.RollAmplitudeDeg, Is.EqualTo(-90f));
        }

        [Test]
        public void DataInputView_StoresArbitraryOceanCurrentLayersInSimulationProfile()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("OceanCurrentAddLayerButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("OceanCurrentMinDepthInput").GetComponent<InputField>().text = "20";
            GameObject.Find("OceanCurrentMaxDepthInput").GetComponent<InputField>().text = "80";
            GameObject.Find("OceanCurrentEastwardInput").GetComponent<InputField>().text = "0.35";
            GameObject.Find("OceanCurrentNorthwardInput").GetComponent<InputField>().text = "-0.12";
            GameObject.Find("OceanCurrentSaveLayerButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile, Is.Not.Null);
            Assert.That(capturedProfile.OceanCurrentProfile.Layers, Has.Count.EqualTo(1));
            Assert.That(capturedProfile.OceanCurrentProfile.GetVelocity(40f), Is.EqualTo(new Vector2(0.35f, -0.12f)));
        }

        [Test]
        public void DataInputView_SelectsAndDisplaysExistingOceanCurrentLayer()
        {
            var profile = SimulationProfile.Default;
            profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
            {
                new OceanCurrentLayer(0f, 160f, 0.35f, -0.12f)
            });
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();

            dataInput.Initialize("D:\\telemetry.csv", profile, null);

            Assert.That(GameObject.Find("OceanCurrentEastwardInput").GetComponent<InputField>().text, Is.EqualTo("0.35"));
            Assert.That(GameObject.Find("OceanCurrentNorthwardInput").GetComponent<InputField>().text, Is.EqualTo("-0.12"));
            Assert.That(FindText("OceanCurrentLayerSummary").text, Is.EqualTo("海流层：1/1  覆盖 0-160m"));
        }

        [Test]
        public void DataInputView_OpensAndClosesDedicatedOceanCurrentDrawer()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            dataInput.BringConfigurationToFront();
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            var drawer = GameObject.Find("OceanCurrentDrawerPanel");
            Assert.That(drawer.activeSelf, Is.True);
            Assert.That(drawer.transform.parent.name, Is.EqualTo("OceanCurrentModalCanvas"));
            Assert.That(GameObject.Find("OceanCurrentDrawerSaveButton"), Is.Not.Null);
            Assert.That(GameObject.Find("DynamicsCalibrateFromCsvButton"), Is.Not.Null);
            Assert.That(GameObject.Find("OceanCurrentQualitySummary"), Is.Not.Null);
            GameObject.Find("OceanCurrentDrawerCloseButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(drawer.activeSelf, Is.False);
        }

        [Test]
        public void DataInputView_DrawerCacheLookupNotifiesRuntimeWithLoadedCurrentProfile()
        {
            const string cachedResponse = "{\"source\":\"drawer fixture\",\"datasetId\":\"test\",\"retrievedAtUtc\":\"2026-07-28T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":160,\"eastwardMps\":0.31,\"northwardMps\":-0.18}],\"fieldSamples\":[{\"longitudeDeg\":121.234567,\"latitudeDeg\":24.345678,\"depthM\":80,\"elapsedSeconds\":0,\"eastwardMps\":0.31,\"northwardMps\":-0.18,\"verticalMps\":0}]}";
            var profile = SimulationProfile.Default;
            profile.OriginLongitudeDeg = 121.234567d;
            profile.OriginLatitudeDeg = 24.345678d;
            profile.TargetDepthM = 160f;
            profile.OceanCurrentPrefetchHalfWidthKm = 12.5f;
            profile.OceanCurrentForecastWindowHours = 24f;
            var request = new CopernicusCurrentRequest(
                profile.OriginLongitudeDeg,
                profile.OriginLatitudeDeg,
                0f,
                profile.TargetDepthM,
                profile.OceanCurrentPrefetchHalfWidthKm,
                profile.OceanCurrentForecastWindowHours);
            var cachePath = Path.Combine(
                Application.persistentDataPath,
                "CopernicusCurrentCache",
                CopernicusCurrentCache.BuildCacheKey(request) + ".json");
            var existingCache = File.Exists(cachePath) ? File.ReadAllBytes(cachePath) : null;
            CopernicusCurrentCache.Store(request, cachedResponse);
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile notifiedProfile = null;
            string drawerStatusAtNotification = null;
            string eastwardAtNotification = null;

            try
            {
                dataInput.Initialize(
                    "D:\\telemetry.csv",
                    profile,
                    null,
                    onOceanCurrentSettingsApplied: value =>
                    {
                        notifiedProfile = value;
                        drawerStatusAtNotification = FindText("OceanCurrentDrawerStatus").text;
                        eastwardAtNotification = GameObject.Find("OceanCurrentDrawerEastwardInput").GetComponent<InputField>().text;
                    });
                GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
                GameObject.Find("OceanCurrentCacheOnlyModeButton").GetComponent<Button>().onClick.Invoke();
                GameObject.Find("OceanCurrentDrawerLookupButton").GetComponent<Button>().onClick.Invoke();

                Assert.That(notifiedProfile, Is.Not.Null);
                Assert.That(notifiedProfile.OceanCurrentField.Samples, Has.Count.EqualTo(1));
                Assert.That(notifiedProfile.OceanCurrentProfile.GetVelocity(80f), Is.EqualTo(new Vector2(0.31f, -0.18f)));
                Assert.That(drawerStatusAtNotification, Does.Contain("已加载 1 层"));
                Assert.That(eastwardAtNotification, Is.EqualTo("0.31"));
            }
            finally
            {
                if (existingCache == null)
                {
                    if (File.Exists(cachePath)) File.Delete(cachePath);
                }
                else
                {
                    File.WriteAllBytes(cachePath, existingCache);
                }
            }
        }

        [Test]
        public void DataInputView_AppliesDynamicsPresetToSimulationProfile()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("DynamicsCalmWaterPresetButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile.Dynamics.PresetName, Is.EqualTo("Calm Water Baseline"));
            Assert.That(capturedProfile.Dynamics.TurbulenceMps, Is.EqualTo(0f));
        }

        [Test]
        public void DataInputView_ExposesAndSavesCoreDynamicsParameters()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(GameObject.Find("DynamicsMassInput"), Is.Not.Null);
            Assert.That(GameObject.Find("DynamicsReferenceAreaInput"), Is.Not.Null);
            Assert.That(GameObject.Find("DynamicsRollInertiaInput"), Is.Not.Null);
            Assert.That(FindText("DynamicsParametersTitle").text, Is.EqualTo("动力学参数"));

            GameObject.Find("DynamicsMassInput").GetComponent<InputField>().text = "68";
            GameObject.Find("DynamicsReferenceAreaInput").GetComponent<InputField>().text = "0.31";
            GameObject.Find("DynamicsRollInertiaInput").GetComponent<InputField>().text = "35";
            GameObject.Find("DynamicsPitchInertiaInput").GetComponent<InputField>().text = "48";
            GameObject.Find("DynamicsYawInertiaInput").GetComponent<InputField>().text = "62";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile, Is.Not.Null);
            Assert.That(capturedProfile.Dynamics.MassKg, Is.EqualTo(68f));
            Assert.That(capturedProfile.Dynamics.ReferenceAreaM2, Is.EqualTo(0.31f));
            Assert.That(capturedProfile.Dynamics.RollInertiaKgM2, Is.EqualTo(35f));
            Assert.That(capturedProfile.Dynamics.PitchInertiaKgM2, Is.EqualTo(48f));
            Assert.That(capturedProfile.Dynamics.YawInertiaKgM2, Is.EqualTo(62f));
        }

        [Test]
        public void DataInputView_ExposesAndSavesTurnaroundDuration()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);
            GameObject.Find("OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            var turnaroundDuration = GameObject.Find("DynamicsTurnaroundDurationInput").GetComponent<InputField>();
            turnaroundDuration.text = "540";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile.Dynamics.TurnaroundDurationSeconds, Is.EqualTo(540f));
        }

        [Test]
        public void DataInputView_OffersOnlyXGBoostPredictionModel()
        {
            RuntimePredictionState.SetModelKind(PredictionModelKind.XGBoost);
            var frames = Frames(2);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, prediction);

            Assert.That(GameObject.Find("PhysicsModelButton"), Is.Null);
            Assert.That(GameObject.Find("LSTMModelButton"), Is.Null);
            Assert.That(GameObject.Find("XGBoostModelButton").GetComponent<Button>().interactable, Is.True);
            Assert.That(RuntimePredictionState.ModelKind, Is.EqualTo(PredictionModelKind.XGBoost));
        }

        [Test]
        public void DataInputView_HidesPredictionModelsThatAreNotRuntimeAvailable()
        {
            var prediction = new GameObject("Prediction").AddComponent<PredictionController>();
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();

            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, prediction);

            Assert.That(GameObject.Find("XGBoostModelButton"), Is.Null);
        }

        [Test]
        public void DashboardView_HidesAdvancedTelemetryUntilDetailsAreRequested()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            Assert.That(GameObject.Find("TelemetryDetailsButton"), Is.Not.Null);
            Assert.That(GameObject.Find("VelocityXValue").GetComponent<Text>().enabled, Is.False);
            GameObject.Find("TelemetryDetailsButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("VelocityXValue").GetComponent<Text>().enabled, Is.True);
        }

        [Test]
        public void DashboardView_KeepsExpandedTelemetryAbovePlaybackControls()
        {
            var playback = CreatePlayback(Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, null);
            GameObject.Find("TelemetryDetailsButton").GetComponent<Button>().onClick.Invoke();

            var panel = GameObject.Find("TelemetryPanel").GetComponent<RectTransform>();
            Assert.That(panel.sizeDelta.y, Is.LessThanOrEqualTo(640f));
        }

        [Test]
        public void DataInputView_ExposesFlightLegParametersAndSubmitsThem()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);

            var button = GameObject.Find("FlightLegSettingsButton");
            Assert.That(button, Is.Not.Null);
            button.GetComponent<Button>().onClick.Invoke();
            GameObject.Find("DescentNetBuoyancyInput").GetComponent<InputField>().text = "-12";
            GameObject.Find("AscentNetBuoyancyInput").GetComponent<InputField>().text = "8";
            GameObject.Find("DescentPitchInput").GetComponent<InputField>().text = "26";
            GameObject.Find("AscentPitchInput").GetComponent<InputField>().text = "7";
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile, Is.Not.Null);
            var descentBuoyancy = typeof(SimulationProfile).GetProperty("DescentNetBuoyancyForceN");
            var ascentPitch = typeof(SimulationProfile).GetProperty("AscentPitchDeg");
            Assert.That(descentBuoyancy.GetValue(capturedProfile), Is.EqualTo(-12f));
            Assert.That(ascentPitch.GetValue(capturedProfile), Is.EqualTo(7f));
            Assert.That(FindText("FlightLegDrawerStatus").text, Does.Contain("上浮为正、下潜为负"));
        }

        [Test]
        public void DataInputView_ClarifiesAndRestoresDefaultFlightLegControls()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            SimulationProfile capturedProfile = null;
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null, onSimulationRequested: profile => capturedProfile = profile);

            Assert.That(FindText("SimulationPitchInputLabel").text, Is.EqualTo("默认俯仰 (°)"));
            Assert.That(FindText("SimulationRollInputLabel").text, Is.EqualTo("默认横滚 (°)"));
            Assert.That(GameObject.Find("SimulationPitchInput").GetComponent<InputField>().text, Is.EqualTo("12"));
            Assert.That(GameObject.Find("SimulationRollInput").GetComponent<InputField>().text, Is.EqualTo("3"));

            GameObject.Find("FlightLegSettingsButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(FindText("FlightLegDrawerStatus").text, Does.Contain("覆盖默认值"));
            GameObject.Find("DescentPitchInput").GetComponent<InputField>().text = "18";
            GameObject.Find("FlightLegRestoreDefaultsButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("SimulationApplyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(capturedProfile, Is.Not.Null);
            Assert.That(float.IsNaN(capturedProfile.DescentPitchDeg), Is.True);
            Assert.That(float.IsNaN(capturedProfile.AscentRollDeg), Is.True);
        }

        [Test]
        public void StatusPanelView_CreatesVisibleMissionHealthBadge()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-status-" + System.Guid.NewGuid().ToString("N"));
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(playback, new AlarmEvaluator(1000f, 1f, 90f), new TwinLogger(logDirectory), prediction);

            Assert.That(GameObject.Find("MissionHealthBadgeValue"), Is.Not.Null);
            Assert.That(FindText("MissionHealthBadgeValue").text, Is.EqualTo("正常"));
            Assert.That(GameObject.Find("EngineeringValidationValue"), Is.Not.Null);
            Assert.That(FindText("AlarmValue").text, Is.EqualTo("运行正常"));
        }

        [Test]
        public void StatusPanelView_SeparatesPredictionTimeValueAndUnitColumns()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-status-units-" + System.Guid.NewGuid().ToString("N"));
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(playback, new AlarmEvaluator(1000f, 1f, 90f), new TwinLogger(logDirectory), prediction);

            Assert.That(FindObjectIncludingInactive("PredictionTimeValue").GetComponent<Text>().text, Does.Not.Contain("ms"));
            Assert.That(FindObjectIncludingInactive("PredictionTimeValueUnit").GetComponent<Text>().text, Is.EqualTo("ms"));
            Assert.That(FindObjectIncludingInactive("RemainingDistanceValue").GetComponent<Text>().text, Does.Not.Contain("km"));
            Assert.That(FindObjectIncludingInactive("RemainingDistanceValueUnit").GetComponent<Text>().text, Is.EqualTo("km"));
        }

        [Test]
        public void DataInputView_LongStatusTextUsesWrappedDedicatedHeight()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            var status = FindText("MissionConfigurationStatus");
            status.text = new string('错', 120);
            UiFactory.ConfigureWrappedStatusText(status);

            Assert.That(status.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(status.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
            Assert.That(status.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThanOrEqualTo(36f));
            Assert.That(status.rectTransform.rect.height, Is.GreaterThanOrEqualTo(36f));
            AssertRectanglesDoNotOverlap(status.rectTransform, GameObject.Find("OceanCurrentDrawerButton").GetComponent<RectTransform>());
        }

        [Test]
        public void StatusPanelView_UsesChineseMissionCopy()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-status-copy-" + System.Guid.NewGuid().ToString("N"));
            RuntimeDataSourceState.UseCsvPath("D:\\telemetry.csv");
            var playback = CreatePlayback(Frames(2));
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(playback, new AlarmEvaluator(1000f, 1f, 90f), new TwinLogger(logDirectory), null);

            Assert.That(FindText("MissionStatusTitle").text, Is.EqualTo("任务状态"));
            Assert.That(FindText("MissionValue").text, Is.EqualTo("CSV 回放"));
        }

        [Test]
        public void StatusPanelView_TranslatesSimulationModeAndState()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-status-simulation-" + System.Guid.NewGuid().ToString("N"));
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            var frames = new[]
            {
                new TelemetryFrame(0, "t", 0f, 120d, 25d, 0f, 0f, 42f, 0f, 0f,
                    0f, 0f, 96f, "Parameter Simulation", "Turnaround", 1f, 42f, 160f, 0f, 0f, 0f, 0f)
            };
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(CreatePlayback(frames), new AlarmEvaluator(1000f, 1f, 90f), new TwinLogger(logDirectory), null);

            Assert.That(FindText("ModeValue").text, Is.EqualTo("参数仿真"));
            Assert.That(FindText("StateValue").text, Is.EqualTo("转向过渡"));
        }

        [Test]
        public void StatusPanelView_ShowsCurrentSegmentAgainstTheMissionTotal()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-status-segments-" + System.Guid.NewGuid().ToString("N"));
            var simulationProfile = SimulationProfile.Default;
            simulationProfile.CycleCount = 10;
            RuntimeDataSourceState.UseSimulation(simulationProfile);
            var frames = new[]
            {
                new TelemetryFrame(0, "000d 00:00:00", 0f, 140d, 15d, 0f, 0f, 42f, 0f, 0f, 28.6f, 0.2f, 96f, "Parameter Simulation", "Surface", 1f, 42f, 160f, 0f, 0f, 0f, 0f),
                new TelemetryFrame(1, "000d 10:00:00", 36000f, 140.1d, 15d, 0f, 0f, 42f, 0f, 0f, 28.6f, 0.2f, 90f, "Parameter Simulation", "Surface", 10f, 42f, 160f, 0f, 0f, 0f, 0f)
            };
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(CreatePlayback(frames), new AlarmEvaluator(1000f, 1f, 90f), new TwinLogger(logDirectory), null);

            Assert.That(FindText("CurrentSegmentValue").text, Is.EqualTo("1 / 10"));
        }

        [Test]
        public void StatusPanelView_LeavesRoomForOperationsBar()
        {
            var logDirectory = Path.Combine(Application.temporaryCachePath, "ui-status-layout-" + System.Guid.NewGuid().ToString("N"));
            var playback = CreatePlayback(Frames(2));
            var panel = new GameObject("Status").AddComponent<StatusPanelView>();

            panel.Initialize(playback, new AlarmEvaluator(1000f, 1f, 90f), new TwinLogger(logDirectory), null);

            var statusRect = GameObject.Find("MissionStatusPanel").GetComponent<RectTransform>();
            Assert.That(statusRect.sizeDelta.y,
                Is.LessThanOrEqualTo(1080f - 112f - 142f));
        }

        [Test]
        public void StatusPanelView_StaysAboveOperationsBarAt720p()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1280f, 720f);
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Canvas.ForceUpdateCanvases();

            var panel = new GameObject("Status").AddComponent<StatusPanelView>();
            panel.Initialize(CreatePlayback(Frames(2)), new AlarmEvaluator(1000f, 1f, 90f), null, null);

            var statusRect = GameObject.Find("MissionStatusPanel").GetComponent<RectTransform>();
            Assert.That(statusRect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(statusRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(statusRect.anchoredPosition.y, Is.EqualTo(-112f).Within(.1f));
            Assert.That(statusRect.sizeDelta.y, Is.LessThanOrEqualTo(720f - 112f - 142f));
        }

        [Test]
        public void StatusPanelView_StaysBelowHeaderAndParameterBar()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            var status = new GameObject("Status").AddComponent<StatusPanelView>();
            status.Initialize(CreatePlayback(Frames(2)), new AlarmEvaluator(1000f, 1f, 90f), null, null);

            var statusRect = GameObject.Find("MissionStatusPanel").GetComponent<RectTransform>();
            var parameterHeader = GameObject.Find("MissionConfigurationDrawerHeader").GetComponent<RectTransform>();
            AssertRectanglesDoNotOverlap(statusRect, parameterHeader);
            Assert.That(statusRect.anchoredPosition.y, Is.LessThanOrEqualTo(-112f));
        }

        [Test]
        public void FlightLegDrawerCollapsesTaskParameterDrawerBeforeOpening()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);

            GameObject.Find("MissionConfigurationDrawerToggleButton").GetComponent<Button>().onClick.Invoke();
            var configuration = GameObject.Find("MissionConfigurationPanel").GetComponent<RectTransform>();
            Assert.That(configuration.sizeDelta.y, Is.GreaterThan(48f));

            GameObject.Find("FlightLegSettingsButton").GetComponent<Button>().onClick.Invoke();
            var flight = GameObject.Find("FlightLegDrawerPanel").GetComponent<RectTransform>();
            Assert.That(flight.gameObject.activeSelf, Is.True);
            Assert.That(configuration.sizeDelta.y, Is.EqualTo(48f).Within(.1f));
            AssertRectanglesDoNotOverlap(configuration, flight);

            var playback = CreatePlayback(Frames(2));
            var mapper = new GeoCoordinateMapper(playback.Model.Frames[0], horizontalScale: 1f, depthScale: 1f);
            var prediction = CreatePrediction(playback, playback.Model.Frames);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(playback.Model.Frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();
            controls.Initialize(playback, cameraController, environment, trajectory);
            AssertRectanglesDoNotOverlap(flight, GameObject.Find("PlaybackControlsPanel").GetComponent<RectTransform>());
        }

        [Test]
        public void CommandCenterPanelsDoNotOverlapAt1080p()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Canvas.ForceUpdateCanvases();

            var frames = Frames(2);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();
            dashboard.Initialize(playback, prediction);
            var status = new GameObject("Status").AddComponent<StatusPanelView>();
            status.Initialize(playback, new AlarmEvaluator(1000f, 1f, 90f), null, prediction);

            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();
            controls.Initialize(playback, cameraController, environment, trajectory);
            var toolbar = new GameObject("Toolbar").AddComponent<OceanCommandToolbarView>();
            toolbar.Initialize(cameraController, trajectory);

            Canvas.ForceUpdateCanvases();
            AssertRectanglesDoNotOverlap(GameObject.Find("TelemetryPanel").GetComponent<RectTransform>(), GameObject.Find("NavigationReferenceCard").GetComponent<RectTransform>());
            AssertRectanglesDoNotOverlap(GameObject.Find("TelemetryPanel").GetComponent<RectTransform>(), GameObject.Find("OceanCommandToolbar").GetComponent<RectTransform>());
            AssertRectanglesDoNotOverlap(GameObject.Find("MissionStatusPanel").GetComponent<RectTransform>(), GameObject.Find("OceanCommandToolbar").GetComponent<RectTransform>());
            AssertRectanglesDoNotOverlap(GameObject.Find("MissionStatusPanel").GetComponent<RectTransform>(), GameObject.Find("PlaybackControlsPanel").GetComponent<RectTransform>());
            AssertRectanglesDoNotOverlap(GameObject.Find("TelemetryPanel").GetComponent<RectTransform>(), GameObject.Find("PlaybackControlsPanel").GetComponent<RectTransform>());
        }

        [Test]
        public void OceanCurrentModalClosesFlightDrawerBeforeOpening()
        {
            var dataInput = new GameObject("DataInput").AddComponent<DataInputView>();
            dataInput.Initialize("D:\\telemetry.csv", SimulationProfile.Default, null);
            GameObject.Find("FlightLegSettingsButton").GetComponent<Button>().onClick.Invoke();
            var flightDrawer = GameObject.Find("FlightLegDrawerPanel");
            Assert.That(flightDrawer.activeSelf, Is.True);

            FindChildNamed(dataInput.transform, "OceanCurrentDrawerButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(flightDrawer.activeSelf, Is.False);
            var oceanModalCanvas = GameObject.Find("OceanCurrentModalCanvas");
            Assert.That(oceanModalCanvas, Is.Not.Null);
            Assert.That(oceanModalCanvas.transform.Find("OceanCurrentDrawerPanel").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void UiFactory_CreatesEventSystemForRuntimeUi()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();

            controls.Initialize(playback, cameraController, environment, trajectory);

            Assert.That(Object.FindObjectOfType<EventSystem>(), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<StandaloneInputModule>(), Is.Not.Null);
        }

        [Test]
        public void UiFactory_PrimaryButton_UsesAccentPaletteAccessibleStatesAndMinimumHeight()
        {
            var root = new GameObject("ButtonRoot", typeof(RectTransform)).transform;

            var button = UiFactory.PrimaryButton(
                "LaunchButton",
                root,
                "Launch",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(120f, 24f));

            var label = button.GetComponentInChildren<Text>();
            var accent = ParseColor("#39DAF4");
            var states = button.colors;

            AssertColorClose(button.image.color, accent);
            Assert.That(button.GetComponent<RectTransform>().sizeDelta.y, Is.GreaterThanOrEqualTo(32f));
            Assert.That(label, Is.Not.Null);
            Assert.That(label.color.grayscale, Is.LessThan(0.2f));
            Assert.That(states.highlightedColor, Is.Not.EqualTo(states.normalColor));
            Assert.That(states.pressedColor, Is.Not.EqualTo(states.highlightedColor));
            Assert.That(states.selectedColor, Is.Not.EqualTo(states.highlightedColor));
            Assert.That(states.disabledColor.a, Is.LessThan(states.normalColor.a));
        }

        [Test]
        public void ResponsiveUiLayoutController_RefreshForScreenKeepsNarrowModeStableWithoutCreatingObjects()
        {
            var controller = CreateResponsiveLayoutController(out var canvas);
            controller.RefreshForScreen(1280f, 720f);
            var countAfterFirstRefresh = Object.FindObjectsOfType<GameObject>(true).Length;

            for (var i = 0; i < 4; i++)
            {
                controller.RefreshForScreen(1280f, 720f);
                Assert.That(controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.Drawer));
            }

            Assert.That(Object.FindObjectsOfType<GameObject>(true).Length, Is.EqualTo(countAfterFirstRefresh));
            Assert.That(canvas.transform.Find("UiRoot/DrawerEntryLayer/TelemetryDrawerToggle").gameObject.activeSelf, Is.True);
            Assert.That(canvas.transform.Find("UiRoot/DrawerEntryLayer/StatusDrawerToggle").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ResponsiveUiLayoutController_KeepsDrawerTogglesOutsideHiddenColumns()
        {
            var controller = CreateResponsiveLayoutController(out var canvas);
            controller.RefreshForScreen(1280f, 720f);

            var mainBody = canvas.transform.Find("UiRoot/MainBody");
            var telemetryColumn = mainBody.Find("TelemetryColumn");
            var statusColumn = mainBody.Find("StatusColumn");
            var telemetryToggle = canvas.transform.Find("UiRoot/DrawerEntryLayer/TelemetryDrawerToggle");
            var statusToggle = canvas.transform.Find("UiRoot/DrawerEntryLayer/StatusDrawerToggle");

            Assert.That(telemetryColumn.gameObject.activeSelf, Is.False);
            Assert.That(statusColumn.gameObject.activeSelf, Is.False);
            Assert.That(telemetryToggle.IsChildOf(telemetryColumn), Is.False);
            Assert.That(statusToggle.IsChildOf(statusColumn), Is.False);
            Assert.That(telemetryToggle.gameObject.activeSelf, Is.True);
            Assert.That(statusToggle.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void UiFactory_RuntimeLayoutUsesLayoutHelpersAndKeepsScrollingOutOfRoot()
        {
            var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var uiRoot = UiFactory.EnsureResponsiveRuntimeLayout(canvas);
            var rootLayout = uiRoot.GetComponent<VerticalLayoutGroup>();
            var mainBody = uiRoot.Find("MainBody").GetComponent<RectTransform>();
            var bodyLayout = mainBody.GetComponent<HorizontalLayoutGroup>();
            var viewportColumn = mainBody.Find("ViewportColumn");
            var viewportElement = viewportColumn.GetComponent<LayoutElement>();
            var configurationArea = uiRoot.Find("ConfigurationArea").GetComponent<RectTransform>();
            var playbackBar = uiRoot.Find("PlaybackBar").GetComponent<RectTransform>();
            var drawerEntryLayer = uiRoot.Find("DrawerEntryLayer");
            var drawerLayout = drawerEntryLayer.GetComponent<LayoutElement>();
            var telemetryToggle = drawerEntryLayer.Find("TelemetryDrawerToggle").GetComponent<RectTransform>();
            var statusToggle = drawerEntryLayer.Find("StatusDrawerToggle").GetComponent<RectTransform>();

            Assert.That(rootLayout, Is.Not.Null);
            Assert.That(rootLayout.enabled, Is.False);
            Assert.That(uiRoot.GetComponent<ScrollRect>(), Is.Null);
            Assert.That(bodyLayout, Is.Not.Null);
            Assert.That(configurationArea.anchoredPosition.y, Is.EqualTo(92f));
            Assert.That(configurationArea.sizeDelta.y, Is.EqualTo(176f));
            Assert.That(playbackBar.anchoredPosition.y, Is.EqualTo(0f));
            Assert.That(playbackBar.sizeDelta.y, Is.EqualTo(92f));
            Assert.That(mainBody.offsetMin.y, Is.EqualTo(268f));
            Assert.That(mainBody.offsetMax.y, Is.EqualTo(-48f));
            Assert.That(viewportElement, Is.Not.Null);
            Assert.That(viewportElement.minWidth, Is.EqualTo(640f));
            Assert.That(viewportElement.flexibleWidth, Is.EqualTo(1f));
            Assert.That(drawerLayout, Is.Not.Null);
            Assert.That(drawerLayout.ignoreLayout, Is.True);
            Assert.That(telemetryToggle.sizeDelta.y, Is.GreaterThanOrEqualTo(36f));
            Assert.That(statusToggle.sizeDelta.y, Is.GreaterThanOrEqualTo(36f));
        }

        [TestCase(1920f, 1080f, false, TestName = "DataInputView_CollapsedConfiguredLayoutCompactsConfigurationArea_At1920x1080")]
        [TestCase(1280f, 720f, false, TestName = "DataInputView_CollapsedConfiguredLayoutCompactsConfigurationArea_At1280x720")]
        public void DataInputView_CollapsedConfigurationCompactsParentAreaAndExpandsMainBody(float width, float height, bool useFallback)
        {
            var view = CreateConfigurationLayoutProbe(width, height, useFallback, out var configurationArea, out var mainBody, out var playbackBar);
            view.SetConfigurationExpanded(false);
            Canvas.ForceUpdateCanvases();

            AssertConfigurationLayoutContract(configurationArea, mainBody, playbackBar, 64f, 156f);
        }

        [TestCase(1920f, 1080f, false, TestName = "DataInputView_ExpandedConfiguredLayoutRestoresConfigurationArea_At1920x1080")]
        [TestCase(1280f, 720f, false, TestName = "DataInputView_ExpandedConfiguredLayoutRestoresConfigurationArea_At1280x720")]
        public void DataInputView_ExpandedConfigurationRestoresParentAreaWithoutOverlappingPlayback(float width, float height, bool useFallback)
        {
            var view = CreateConfigurationLayoutProbe(width, height, useFallback, out var configurationArea, out var mainBody, out var playbackBar);

            view.SetConfigurationExpanded(true);
            Canvas.ForceUpdateCanvases();

            AssertConfigurationLayoutContract(configurationArea, mainBody, playbackBar, 176f, 268f);
        }

        [Test]
        public void UiFactory_CommandPaletteSeparatesPanelAndInputSurfaces()
        {
            Assert.That(UiFactory.CommandPanelFill.grayscale - UiFactory.CommandInputFill.grayscale, Is.GreaterThan(0.035f));
            Assert.That(UiFactory.CommandButtonFill.grayscale - UiFactory.CommandInputFill.grayscale, Is.GreaterThan(0.055f));
            Assert.That(UiFactory.CommandPanelEdge.grayscale, Is.GreaterThan(UiFactory.CommandPanelFill.grayscale));
        }

        [Test]
        public void UiFactory_ApplyRuntimePaletteUpdatesExistingPanelControls()
        {
            var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas));
            var uiRoot = new GameObject("UiRoot", typeof(RectTransform));
            uiRoot.transform.SetParent(canvasObject.transform, false);

            var panel = new GameObject("DashboardPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(uiRoot.transform, false);
            var viewport = new GameObject("OceanCommandToolbar", typeof(RectTransform), typeof(Image));
            viewport.transform.SetParent(uiRoot.transform, false);
            var button = new GameObject("LoadCsvButton", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(panel.transform, false);
            var input = new GameObject("CsvPathInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            input.transform.SetParent(panel.transform, false);

            UiFactory.ApplyRuntimePalette(canvasObject.transform);

            Assert.That(panel.GetComponent<Image>().color, Is.EqualTo(UiFactory.CommandPanelFill));
            Assert.That(viewport.GetComponent<Image>().color.a, Is.LessThan(UiFactory.CommandPanelFill.a));
            Assert.That(viewport.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(button.GetComponent<Image>().color, Is.EqualTo(UiFactory.CommandButtonFill));
            Assert.That(input.GetComponent<Image>().color, Is.EqualTo(UiFactory.CommandInputFill));
        }

        [Test]
        public void UiFactory_ApplyRuntimePalettePreservesCustomButtonStateColors()
        {
            var canvas = new GameObject("PaletteCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("PlaybackControlsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var button = new GameObject("UserStyledButton", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(panel.transform, false);
            var component = button.GetComponent<Button>();
            var colors = component.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.91f, 0.24f, 0.32f, 1f);
            colors.pressedColor = new Color(0.14f, 0.72f, 0.41f, 1f);
            colors.selectedColor = new Color(0.96f, 0.71f, 0.16f, 1f);
            colors.disabledColor = new Color(0.31f, 0.18f, 0.62f, 0.77f);
            component.colors = colors;
            var before = component.colors;

            UiFactory.ApplyRuntimePalette(canvas.transform);

            var after = component.colors;
            Assert.That(after.normalColor, Is.EqualTo(before.normalColor));
            Assert.That(after.highlightedColor, Is.EqualTo(before.highlightedColor));
            Assert.That(after.pressedColor, Is.EqualTo(before.pressedColor));
            Assert.That(after.selectedColor, Is.EqualTo(before.selectedColor));
            Assert.That(after.disabledColor, Is.EqualTo(before.disabledColor));
        }

        [Test]
        public void UiFactory_UsesApprovedCommandCenterPaletteAndButtonStates()
        {
            AssertColorClose(UiFactory.CommandPageBackground, ParseColor("#05121A"));
            AssertColorClose(UiFactory.CommandPanelFill, ParseColor("#092632"));
            AssertColorClose(UiFactory.CommandCardFill, ParseColor("#0C2E3B"));
            AssertColorClose(UiFactory.CommandControlFill, ParseColor("#103D4D"));
            AssertColorClose(UiFactory.CommandInputFill, ParseColor("#051720"));
            AssertColorClose(UiFactory.CommandButtonFill, ParseColor("#124759"));
            AssertColorClose(UiFactory.CommandPanelEdge, ParseColor("#185A6B"));
            AssertColorClose(UiFactory.CommandAccent, ParseColor("#39DAF4"));
            AssertColorClose(UiFactory.CommandText, ParseColor("#ECF9FB"));
            AssertColorClose(UiFactory.CommandMutedText, ParseColor("#9EC4CD"));

            var canvas = new GameObject("PaletteCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("PlaybackControlsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var title = CreateText(panel.transform, "CommandCenterProductName");
            var section = CreateText(panel.transform, "PlaybackGroupLabel");
            var value = CreateText(panel.transform, "PlaybackStatus");
            var primary = UiFactory.PrimaryButton("ApplyButton", panel.transform, "运行仿真", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 36f));
            UiFactory.ApplyRuntimePalette(canvas.transform);

            var label = primary.GetComponentInChildren<Text>();
            Assert.That(primary.image.color, Is.EqualTo(UiFactory.CommandAccent));
            Assert.That(label.color, Is.EqualTo(UiFactory.CommandDarkText));
            Assert.That(title.color, Is.EqualTo(UiFactory.CommandText));
            Assert.That(section.color, Is.EqualTo(UiFactory.CommandMutedText));
            Assert.That(value.color, Is.EqualTo(UiFactory.CommandText));
            Assert.That(primary.colors.disabledColor.grayscale, Is.LessThan(primary.colors.normalColor.grayscale));
            Assert.That(primary.colors.disabledColor.a, Is.GreaterThan(0.15f));
            Assert.That(primary.colors.selectedColor.grayscale, Is.GreaterThan(primary.colors.normalColor.grayscale * 0.75f));
        }

        [Test]
        public void ResponsiveUiTypography_ExposesStableVisualRoles()
        {
            var profile = ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode.CompressedThreeColumn, 1366f, 768f);

            Assert.That(profile.sectionTitleSize, Is.EqualTo(18));
            Assert.That(profile.labelSize, Is.EqualTo(18));
            Assert.That(profile.valueSize, Is.EqualTo(18));
            Assert.That(profile.buttonSize, Is.EqualTo(18));
            Assert.That(profile.auxiliarySize, Is.EqualTo(17));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(profile.buttonSize, 0.67f), Is.GreaterThanOrEqualTo(11f));
        }

        [Test]
        public void ResponsiveLayoutController_UsesSameCompressedTypographyForPrefabAndFallbackPaths()
        {
            const float width = 1366f;
            const float height = 768f;
            var prefabProbe = CreateTypographyProbe("PrefabTypographyCanvas", createFallbackContent: false);
            var fallbackProbe = CreateTypographyProbe("FallbackTypographyCanvas", createFallbackContent: true);

            ApplyTypography(prefabProbe.runtimeCanvas, width, height);
            ApplyTypography(fallbackProbe.runtimeCanvas, width, height);

            Assert.That(prefabProbe.title.fontSize, Is.EqualTo(fallbackProbe.title.fontSize));
            Assert.That(prefabProbe.label.fontSize, Is.EqualTo(fallbackProbe.label.fontSize));
            Assert.That(prefabProbe.value.fontSize, Is.EqualTo(fallbackProbe.value.fontSize));
            Assert.That(prefabProbe.button.fontSize, Is.EqualTo(fallbackProbe.button.fontSize));

            var effectiveScale = ResponsiveUiLayoutPolicy.GetEffectiveCanvasScale(width, height, new Vector2(1920f, 1080f), 0.5f);
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(fallbackProbe.label.fontSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(fallbackProbe.value.fontSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
            Assert.That(ResponsiveUiTypography.GetActualPixelSize(fallbackProbe.button.fontSize, effectiveScale), Is.GreaterThanOrEqualTo(11f));
        }

        [Test]
        public void PlaybackControlsView_CreatesGroupedControlLabels()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();

            controls.Initialize(playback, cameraController, environment, trajectory);

            Assert.That(GameObject.Find("PlaybackGroupLabel"), Is.Not.Null);
            Assert.That(GameObject.Find("SpeedGroupLabel"), Is.Not.Null);
        }

        [Test]
        public void PlaybackControlsView_UsesThreeVisualSegmentsWithoutCameraDuplicates()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();

            controls.Initialize(playback, cameraController, environment, trajectory);

            Assert.That(GameObject.Find("PlaybackOperationsRow"), Is.Not.Null);
            Assert.That(GameObject.Find("PlaybackTimelineRow"), Is.Not.Null);
            Assert.That(GameObject.Find("PlaybackOptionsRow"), Is.Not.Null);
            Assert.That(FindObjectIncludingInactive("CameraFollowButton"), Is.Not.Null);
            Assert.That(FindObjectIncludingInactive("CameraFollowButton").activeSelf, Is.False);
            Assert.That(FindObjectIncludingInactive("CameraGlobalButton"), Is.Not.Null);
            Assert.That(FindObjectIncludingInactive("CameraGlobalButton").activeSelf, Is.False);
            Assert.That(FindObjectIncludingInactive("CameraOrbitButton"), Is.Not.Null);
            Assert.That(FindObjectIncludingInactive("CameraOrbitButton").activeSelf, Is.False);
            Assert.That(GameObject.Find("PlayPauseButton").transform.parent.name, Is.EqualTo("PlaybackOperationsRow"));
            Assert.That(GameObject.Find("ProgressSlider").transform.parent.name, Is.EqualTo("PlaybackTimelineRow"));
            Assert.That(GameObject.Find("Speed1Button").transform.parent.name, Is.EqualTo("PlaybackOptionsRow"));
        }

        [Test]
        public void OceanCommandToolbarView_MarksCurrentCameraWithAccentEdge()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("ToolbarCamera").AddComponent<TwinCameraController>();
            var target = new GameObject("ToolbarTarget");
            cameraController.Initialize(target.transform, new[] { Vector3.zero, Vector3.one });
            var trajectory = new GameObject("ToolbarTrajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var toolbar = new GameObject("Toolbar").AddComponent<OceanCommandToolbarView>();

            toolbar.Initialize(cameraController, trajectory);
            var top = GameObject.Find("CameraTopCommand").GetComponent<Button>();
            top.onClick.Invoke();

            Assert.That(top.GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandAccent));
            Assert.That(GameObject.Find("CameraFollowCommand").GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandPanelEdge));
        }

        [Test]
        public void OceanCommandToolbarView_BindMarksExistingGlobalCameraMode()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("ToolbarCamera").AddComponent<TwinCameraController>();
            var target = new GameObject("ToolbarTarget");
            cameraController.Initialize(target.transform, new[] { Vector3.zero, Vector3.one });
            cameraController.SetMissionVolumeView(80f);
            var trajectory = new GameObject("ToolbarTrajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            trajectory.SetCameraMode(CameraMode.Global);
            var panel = new GameObject("OceanCommandToolbar", typeof(RectTransform));
            var follow = UiFactory.Button("CameraFollowCommand", panel.transform, "跟随", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(60f, 32f));
            var global = UiFactory.Button("CameraGlobalCommand", panel.transform, "全局", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(60f, 32f));
            var toolbar = new GameObject("Toolbar").AddComponent<OceanCommandToolbarView>();

            toolbar.Bind(new OceanToolbarRefs
            {
                panel = panel.GetComponent<RectTransform>(),
                cameraFollowCommand = follow,
                cameraGlobalCommand = global
            }, cameraController, trajectory);

            Assert.That(global.GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandAccent));
            Assert.That(follow.GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandPanelEdge));
        }

        [Test]
        public void BoundPrefabTypography_PreservesExistingTextFontSizes()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);

            var cameraController = new GameObject("TypographyCamera").AddComponent<TwinCameraController>();
            var target = new GameObject("TypographyTarget");
            cameraController.Initialize(target.transform, new[] { Vector3.zero, Vector3.one });
            var trajectory = new GameObject("TypographyTrajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);

            var oceanPanel = new GameObject("OceanTypographyPanel", typeof(RectTransform));
            var oceanButton = UiFactory.Button("CameraFollowCommand", oceanPanel.transform, "跟随",
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(60f, 32f));
            var oceanText = oceanButton.GetComponentInChildren<Text>();
            oceanText.fontSize = 7;
            var toolbar = new GameObject("TypographyToolbar").AddComponent<OceanCommandToolbarView>();

            toolbar.Bind(new OceanToolbarRefs
            {
                panel = oceanPanel.GetComponent<RectTransform>(),
                cameraFollowCommand = oceanButton
            }, cameraController, trajectory);

            var playbackPanel = new GameObject("PlaybackTypographyPanel", typeof(RectTransform));
            var playbackButton = UiFactory.Button("PlayPauseButton", playbackPanel.transform, "开始",
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(80f, 32f));
            var playbackText = playbackButton.GetComponentInChildren<Text>();
            playbackText.fontSize = 6;
            var playbackStatus = UiFactory.Text("PlaybackStatus", playbackPanel.transform, "状态", 12,
                TextAnchor.MiddleLeft, UiFactory.CommandText, Vector2.zero, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(120f, 24f));
            playbackStatus.fontSize = 5;
            var controls = new GameObject("TypographyControls").AddComponent<PlaybackControlsView>();

            controls.Bind(new PlaybackControlsRefs
            {
                panel = playbackPanel.GetComponent<RectTransform>(),
                playPauseButton = playbackButton,
                statusText = playbackStatus
            }, playback, null, null, null);

            Assert.That(oceanText.fontSize, Is.EqualTo(7));
            Assert.That(playbackText.fontSize, Is.EqualTo(6));
            Assert.That(playbackStatus.fontSize, Is.EqualTo(5));
        }

        [Test]
        public void OceanCommandToolbarView_ResetMarksGlobalCameraMode()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("ToolbarCamera").AddComponent<TwinCameraController>();
            var target = new GameObject("ToolbarTarget");
            cameraController.Initialize(target.transform, new[] { Vector3.zero, Vector3.one });
            var trajectory = new GameObject("ToolbarTrajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var toolbar = new GameObject("Toolbar").AddComponent<OceanCommandToolbarView>();

            toolbar.Initialize(cameraController, trajectory);
            GameObject.Find("CameraTopCommand").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("CameraResetCommand").GetComponent<Button>().onClick.Invoke();

            Assert.That(GameObject.Find("CameraGlobalCommand").GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandAccent));
            Assert.That(GameObject.Find("CameraTopCommand").GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandPanelEdge));
        }

        [Test]
        public void DashboardView_DisplaysSimulationDiagnosticsWithUnits()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            var diagnostics = new SimulationDiagnostics(
                new Vector3(0.3f, -0.1f, 0.4f),
                new Vector3(0.2f, 0f, -0.1f),
                6.5f,
                8.2f,
                12.4f,
                3.2f,
                4.5f,
                7.8f,
                -1.1f,
                new Vector3(0.02f, 0.01f, 0.03f),
                new Vector3(1.2f, 2.4f, 0.8f),
                12.5f,
                new Vector3(4.0f, -3.0f, 1.5f),
                6.7f);
            var frames = new[]
            {
                new TelemetryFrame(0, "t0", 0f, 120d, 25d, 10f, 100f, 30f, 4f, -3f,
                    28.5f, 0.3f, 90f, "Parameter Simulation", "Glide", 2f, 44f, 120f, 80f,
                    0f, 17f, 32f, diagnostics)
            };
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(CreatePlayback(frames), null);

            Assert.That(FindText("WaterSpeedValue").text, Is.EqualTo("0.51"));
            Assert.That(FindText("GroundSpeedValue").text, Is.EqualTo("0.59"));
            Assert.That(FindText("SideSlipValue").text, Is.EqualTo("12.4"));
            Assert.That(FindText("OceanCurrentValue").text, Is.EqualTo("东 0.20 北 -0.10"));
            Assert.That(FindText("NetBuoyancyValue").text, Is.EqualTo("6.5"));
            Assert.That(FindText("EnergyValue").text, Is.EqualTo("8.2"));
            Assert.That(FindText("AngleOfAttackValue").text, Is.EqualTo("3.2"));
            Assert.That(FindText("LiftForceValue").text, Is.EqualTo("4.5"));
            Assert.That(FindText("DragForceValue").text, Is.EqualTo("7.8"));
            Assert.That(FindText("AngularRateValue").text, Is.EqualTo("2.14"));
            Assert.That(FindText("PistonPositionValue").text, Is.EqualTo("12.5"));
            Assert.That(FindText("ControlSurfaceValue").text, Is.EqualTo("R 4.0 / P -3.0 / Y 1.5"));
            Assert.That(FindText("ActuatorPowerValue").text, Is.EqualTo("6.7"));
            Assert.That(FindText("DynamicsSummaryValue").text, Is.EqualTo("AoA 3.2°  L 4.5N  D 7.8N"));
        }

        [Test]
        public void PlaybackControlsView_UsesCompactOperationsBar()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("Camera").AddComponent<TwinCameraController>();
            var environment = new GameObject("Environment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("Trajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("Controls").AddComponent<PlaybackControlsView>();

            controls.Initialize(playback, cameraController, environment, trajectory);

            var operationsBar = GameObject.Find("PlaybackControlsPanel").GetComponent<RectTransform>();
            Assert.That(operationsBar.sizeDelta.y, Is.LessThanOrEqualTo(128f));
            Assert.That(operationsBar.anchorMin.x, Is.EqualTo(0f));
            Assert.That(operationsBar.anchorMax.x, Is.EqualTo(1f));
            var exitButton = GameObject.Find("ExitButton").GetComponent<RectTransform>();
            Assert.That(exitButton.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(exitButton.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void DashboardView_UsesFixedAnchorsForValueLabels()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            var depthRect = FindText("DepthValue").rectTransform;
            Assert.That(depthRect.anchorMin, Is.EqualTo(depthRect.anchorMax));
        }

        [Test]
        public void DashboardView_AnchorsValueLabelsToTopRightOfPanel()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            var depthRect = FindText("DepthValue").rectTransform;
            Assert.That(depthRect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(depthRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(depthRect.pivot, Is.EqualTo(new Vector2(1f, 1f)));
        }

        [Test]
        public void DashboardView_BuildsLayeredTelemetryCardsWithReadableNumericRoles()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var dashboard = new GameObject("DashboardHierarchy").AddComponent<DashboardView>();

            dashboard.Initialize(playback, prediction);

            var telemetryCard = GameObject.Find("TelemetrySnapshotCard");
            var dynamicsCard = GameObject.Find("TelemetryDynamicsCard");
            var navigationCard = GameObject.Find("NavigationReferenceCard");
            var divider = GameObject.Find("TelemetrySectionDivider");
            Assert.That(telemetryCard, Is.Not.Null);
            Assert.That(dynamicsCard, Is.Not.Null);
            Assert.That(navigationCard, Is.Not.Null);
            Assert.That(divider, Is.Not.Null);
            Assert.That(telemetryCard.GetComponent<Image>().raycastTarget, Is.False);
            AssertColorClose(telemetryCard.GetComponent<Image>().color, UiFactory.CommandCardFill);
            Assert.That(divider.GetComponent<Image>().raycastTarget, Is.False);

            var depth = FindText("DepthValue");
            var depthLayout = depth.GetComponent<LayoutElement>();
            Assert.That(depth.fontSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(depth.alignment, Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(depthLayout.preferredWidth, Is.EqualTo(UiFactory.FixedValueColumnWidth));
            AssertColorClose(depth.color, UiFactory.CommandText);
            AssertColorClose(FindText("深度Label").color, UiFactory.CommandMutedText);
        }

        [Test]
        public void StatusPanelView_GroupsMissionProgressPredictionAndAlarmSurfaces()
        {
            var frames = Frames(2);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var status = new GameObject("StatusHierarchy").AddComponent<StatusPanelView>();

            status.Initialize(playback, new AlarmEvaluator(1000f, 0f, 180f), null, prediction);

            var cardNames = new[]
            {
                "MissionStatusCard",
                "MissionProgressCard",
                "PredictionQualityCard",
                "AlarmStateCard"
            };
            foreach (var cardName in cardNames)
            {
                var card = GameObject.Find(cardName);
                Assert.That(card, Is.Not.Null, cardName);
                Assert.That(card.GetComponent<Image>(), Is.Not.Null, cardName);
                Assert.That(card.GetComponent<Image>().raycastTarget, Is.False, cardName);
            }

            var predictionValue = FindText("PredictionStatusValue");
            Assert.That(predictionValue.fontSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(predictionValue.GetComponent<LayoutElement>().preferredWidth,
                Is.EqualTo(UiFactory.FixedValueColumnWidth));
            Assert.That(GameObject.Find("MissionPredictionDivider"), Is.Not.Null);
            Assert.That(GameObject.Find("PredictionAlarmDivider"), Is.Not.Null);
        }

        [Test]
        public void PlaybackControlsView_UsesThreeStyledRowsAndDistinctButtonRoles()
        {
            var frames = Frames(2);
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var playback = CreatePlayback(frames);
            var prediction = CreatePrediction(playback, frames);
            var cameraController = new GameObject("PlaybackHierarchyCamera").AddComponent<TwinCameraController>();
            var environment = new GameObject("PlaybackHierarchyEnvironment").AddComponent<UnderwaterEnvironmentBuilder>();
            var trajectory = new GameObject("PlaybackHierarchyTrajectory").AddComponent<TrajectoryView>();
            trajectory.Initialize(frames, mapper, playback, prediction);
            var controls = new GameObject("PlaybackHierarchyControls").AddComponent<PlaybackControlsView>();

            controls.Initialize(playback, cameraController, environment, trajectory);

            var operations = GameObject.Find("PlaybackOperationsRow").transform;
            var timeline = GameObject.Find("PlaybackTimelineRow").transform;
            var options = GameObject.Find("PlaybackOptionsRow").transform;
            Assert.That(operations.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(timeline.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(options.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(GameObject.Find("PlayPauseButton").transform.parent, Is.SameAs(operations));
            Assert.That(GameObject.Find("ProgressSlider").transform.parent, Is.SameAs(timeline));
            Assert.That(GameObject.Find("FogToggle").transform.parent, Is.SameAs(options));
            Assert.That(GameObject.Find("PlaybackOperationsDivider"), Is.Not.Null);
            Assert.That(GameObject.Find("PlaybackTimelineDivider"), Is.Not.Null);

            var primary = GameObject.Find("PlayPauseButton").GetComponent<Button>();
            var secondary = GameObject.Find("ReverseButton").GetComponent<Button>();
            AssertColorClose(primary.image.color, UiFactory.CommandAccent);
            AssertColorClose(secondary.image.color, UiFactory.CommandButtonFill);
            Assert.That(primary.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(FindObjectIncludingInactive("CameraFollowButton").activeSelf, Is.False);
            Assert.That(FindObjectIncludingInactive("CameraGlobalButton").activeSelf, Is.False);
            Assert.That(FindObjectIncludingInactive("CameraOrbitButton").activeSelf, Is.False);
        }

        [Test]
        public void VisualHierarchySetup_PreservesExistingAuthoredCardValues()
        {
            var root = new GameObject("DashboardPanel", typeof(RectTransform));
            var card = new GameObject("TelemetrySnapshotCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var authoredColor = new Color(0.41f, 0.12f, 0.32f, 0.77f);
            var authoredPosition = new Vector2(37f, -91f);
            card.GetComponent<Image>().color = authoredColor;
            card.GetComponent<RectTransform>().anchoredPosition = authoredPosition;
            var depth = CreateText(root.transform, "DepthValue");
            var dashboard = new GameObject("DashboardPreservation").AddComponent<DashboardView>();

            dashboard.Bind(new DashboardPanelRefs
            {
                panel = root.GetComponent<RectTransform>(),
                depthValue = depth
            }, CreatePlayback(Frames(2)), null);

            Assert.That(CountDirectChildrenNamed(root.transform, "TelemetrySnapshotCard"), Is.EqualTo(1));
            AssertColorClose(card.GetComponent<Image>().color, authoredColor);
            Assert.That(card.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(authoredPosition));
        }

        [Test]
        public void ShippedPanelPrefabs_ContainVisualGroupingRoots()
        {
            var dashboard = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/DashboardPanel.prefab");
            var status = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/StatusPanel.prefab");
            var playback = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/PlaybackControlsPanel.prefab");

            Assert.That(dashboard.transform.Find("TelemetrySnapshotCard"), Is.Not.Null);
            Assert.That(dashboard.transform.Find("TelemetryDynamicsCard"), Is.Not.Null);
            Assert.That(status.transform.Find("MissionStatusCard"), Is.Not.Null);
            Assert.That(status.transform.Find("MissionProgressCard"), Is.Not.Null);
            Assert.That(status.transform.Find("PredictionQualityCard"), Is.Not.Null);
            Assert.That(status.transform.Find("AlarmStateCard"), Is.Not.Null);
            Assert.That(playback.transform.Find("PlaybackOperationsRow"), Is.Not.Null);
            Assert.That(playback.transform.Find("PlaybackTimelineRow"), Is.Not.Null);
            Assert.That(playback.transform.Find("PlaybackOptionsRow"), Is.Not.Null);
        }

        [Test]
        public void UiFactory_RuntimePaletteMigratesKnownLegacyColorsWithoutReplacingCustomOverrides()
        {
            var panel = new GameObject("PlaybackControlsPanel", typeof(RectTransform), typeof(Image));
            panel.GetComponent<Image>().color = new Color(0.025f, 0.12f, 0.18f, 0.92f);
            var primary = UiFactory.Button("PlayPauseButton", panel.transform, "开始", Vector2.zero, new Vector2(80f, 32f));
            primary.image.color = new Color(0.082f, 0.184f, 0.259f, 0.96f);
            var custom = UiFactory.Button("ExportButton", panel.transform, "导出", Vector2.zero, new Vector2(80f, 32f));
            var customColor = new Color(0.48f, 0.11f, 0.36f, 0.83f);
            custom.image.color = customColor;

            UiFactory.ApplyRuntimePalette(panel.transform);

            AssertColorClose(panel.GetComponent<Image>().color, UiFactory.CommandPanelFill);
            AssertColorClose(primary.image.color, UiFactory.CommandAccent);
            AssertColorClose(custom.image.color, customColor);
        }

        [Test]
        public void PlaybackControlsView_BindsShippedPrefabWithThemeRolesAndNonBlockingRows()
        {
            var panel = InstantiateShippedPanelPrefab("Assets/UI/Prefabs/PlaybackControlsPanel.prefab");
            var refs = new PlaybackControlsRefs
            {
                panel = panel,
                playPauseButton = FindChildNamed(panel, "PlayPauseButton").GetComponent<Button>(),
                reverseButton = FindChildNamed(panel, "ReverseButton").GetComponent<Button>(),
                replayButton = FindChildNamed(panel, "ReplayButton").GetComponent<Button>(),
                resetButton = FindChildNamed(panel, "ResetButton").GetComponent<Button>(),
                exportButton = FindChildNamed(panel, "ExportButton").GetComponent<Button>(),
                exitButton = FindChildNamed(panel, "ExitButton").GetComponent<Button>(),
                missionVolumeButton = FindChildNamed(panel, "MissionVolumeButton").GetComponent<Button>(),
                fogToggle = FindChildNamed(panel, "FogToggle").GetComponent<Toggle>(),
                particlesToggle = FindChildNamed(panel, "ParticlesToggle").GetComponent<Toggle>(),
                trajectoryToggle = FindChildNamed(panel, "TrajectoryToggle").GetComponent<Toggle>(),
                speed05Button = FindChildNamed(panel, "Speed05Button").GetComponent<Button>(),
                speed1Button = FindChildNamed(panel, "Speed1Button").GetComponent<Button>(),
                speed2Button = FindChildNamed(panel, "Speed2Button").GetComponent<Button>(),
                speed5Button = FindChildNamed(panel, "Speed5Button").GetComponent<Button>(),
                speed10Button = FindChildNamed(panel, "Speed10Button").GetComponent<Button>(),
                progressSlider = FindChildNamed(panel, "ProgressSlider").GetComponent<Slider>(),
                statusText = FindChildNamed(panel, "StatusText").GetComponent<Text>()
            };
            var view = new GameObject("ShippedPlaybackView").AddComponent<PlaybackControlsView>();

            view.Bind(refs, CreatePlayback(Frames(2)), null, null, null);

            AssertColorClose(refs.panel.GetComponent<Image>().color, UiFactory.CommandPanelFill);
            AssertColorClose(refs.playPauseButton.image.color, UiFactory.CommandAccent);
            AssertColorClose(refs.reverseButton.image.color, UiFactory.CommandButtonFill);
            AssertColorClose(refs.speed1Button.image.color, UiFactory.CommandAccent);
            foreach (var rowName in new[] { "PlaybackOperationsRow", "PlaybackTimelineRow", "PlaybackOptionsRow" })
            {
                var image = panel.Find(rowName).GetComponent<Image>();
                Assert.That(image, Is.Not.Null, rowName);
                Assert.That(image.raycastTarget, Is.False, rowName);
            }
        }

        [TestCase(236f, TestName = "DashboardValueRows_FitResponsiveSidebarWidth_At1280")]
        [TestCase(280f, TestName = "DashboardValueRows_FitResponsiveSidebarWidth_At1366")]
        public void DashboardValueRows_FitResponsiveSidebarWidth(float sidebarWidth)
        {
            var root = new GameObject("DashboardPanel", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(sidebarWidth, 600f);
            var depth = CreateText(root.transform, "DepthValue");
            var view = new GameObject("SidebarWidthDashboard").AddComponent<DashboardView>();

            view.Bind(new DashboardPanelRefs
            {
                panel = root.GetComponent<RectTransform>(),
                depthValue = depth
            }, CreatePlayback(Frames(2)), null);

            var row = root.transform.Find("DepthRow").GetComponent<RectTransform>();
            AssertRowWidthBudgetFits(row, sidebarWidth - 8f);
        }

        [TestCase(236f, TestName = "DashboardAndStatusViews_BindShippedPrefabsWithoutOverflow_At1280")]
        [TestCase(280f, TestName = "DashboardAndStatusViews_BindShippedPrefabsWithoutOverflow_At1366")]
        public void DashboardAndStatusViews_BindShippedPrefabsWithCompactNonBlockingCards(float sidebarWidth)
        {
            var playback = CreatePlayback(Frames(2));
            var dashboardPanel = InstantiateShippedPanelPrefab("Assets/UI/Prefabs/DashboardPanel.prefab");
            dashboardPanel.sizeDelta = new Vector2(sidebarWidth, 600f);
            var dashboard = new GameObject("ShippedDashboardView").AddComponent<DashboardView>();
            dashboard.Bind(new DashboardPanelRefs
            {
                panel = dashboardPanel,
                depthValue = FindChildNamed(dashboardPanel, "DepthValue").GetComponent<Text>(),
                detailsButton = FindChildNamed(dashboardPanel, "DetailsButton").GetComponent<Button>(),
                navigationReferenceCard = FindChildNamed(dashboardPanel, "NavigationReferenceCard").gameObject
            }, playback, null);

            var statusPanel = InstantiateShippedPanelPrefab("Assets/UI/Prefabs/StatusPanel.prefab");
            statusPanel.sizeDelta = new Vector2(sidebarWidth, 600f);
            var status = new GameObject("ShippedStatusView").AddComponent<StatusPanelView>();
            status.Bind(new StatusPanelRefs
            {
                panel = statusPanel,
                missionValue = FindChildNamed(statusPanel, "MissionValue").GetComponent<Text>()
            }, playback, null, null, null);

            AssertColorClose(dashboardPanel.GetComponent<Image>().color, UiFactory.CommandPanelFill);
            AssertColorClose(statusPanel.GetComponent<Image>().color, UiFactory.CommandPanelFill);
            Assert.That(dashboardPanel.Find("TelemetrySnapshotCard").GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(statusPanel.Find("MissionStatusCard").GetComponent<Image>().raycastTarget, Is.False);
            AssertRowWidthBudgetFits(dashboardPanel.Find("DepthRow").GetComponent<RectTransform>(), sidebarWidth - 8f);
            AssertRowWidthBudgetFits(statusPanel.Find("MissionRow").GetComponent<RectTransform>(), sidebarWidth - 8f);
        }

        private static Text FindText(string name)
        {
            return GameObject.Find(name).GetComponent<Text>();
        }

        private static RectTransform InstantiateShippedPanelPrefab(string assetPath)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(prefab, Is.Not.Null, assetPath);
            var instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null, assetPath);
            return instance.GetComponent<RectTransform>();
        }

        private static void AssertRowWidthBudgetFits(RectTransform row, float availableWidth)
        {
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Assert.That(layout, Is.Not.Null, row.name);
            var activeElements = new List<LayoutElement>();
            foreach (Transform child in row)
            {
                if (child.gameObject.activeSelf)
                {
                    var element = child.GetComponent<LayoutElement>();
                    if (element != null && !element.ignoreLayout)
                    {
                        activeElements.Add(element);
                    }
                }
            }

            var requiredWidth = (float)layout.padding.horizontal;
            if (activeElements.Count > 1)
            {
                requiredWidth += layout.spacing * (activeElements.Count - 1);
            }
            foreach (var element in activeElements)
            {
                requiredWidth += element.minWidth;
            }

            Assert.That(requiredWidth, Is.LessThanOrEqualTo(availableWidth),
                $"{row.name} requires {requiredWidth:0.#} px but only {availableWidth:0.#} px is available");
        }

        private static GameObject FindObjectIncludingInactive(string name)
        {
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static Color ParseColor(string html)
        {
            Assert.That(ColorUtility.TryParseHtmlString(html, out var color), Is.True, $"Failed to parse {html}");
            return color;
        }

        private static void AssertColorClose(Color actual, Color expected, float tolerance = 0.001f)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
        }

        private static void AssertRectInsideViewport(RectTransform control, RectTransform viewport, int width, int height)
        {
            var controlCorners = new Vector3[4];
            var viewportCorners = new Vector3[4];
            control.GetWorldCorners(controlCorners);
            viewport.GetWorldCorners(viewportCorners);
            var minimumX = viewportCorners[0].x - 0.1f;
            var minimumY = viewportCorners[0].y - 0.1f;
            var maximumX = viewportCorners[2].x + 0.1f;
            var maximumY = viewportCorners[2].y + 0.1f;
            foreach (var corner in controlCorners)
            {
                Assert.That(corner.x, Is.InRange(minimumX, maximumX), $"{control.name} x must fit {width}x{height} viewport");
                Assert.That(corner.y, Is.InRange(minimumY, maximumY), $"{control.name} y must fit {width}x{height} viewport");
            }
        }

        private static void AssertRectanglesDoNotOverlap(RectTransform first, RectTransform second)
        {
            var firstCorners = new Vector3[4];
            var secondCorners = new Vector3[4];
            first.GetWorldCorners(firstCorners);
            second.GetWorldCorners(secondCorners);
            var firstMinX = firstCorners[0].x;
            var firstMaxX = firstCorners[2].x;
            var firstMinY = firstCorners[0].y;
            var firstMaxY = firstCorners[2].y;
            var secondMinX = secondCorners[0].x;
            var secondMaxX = secondCorners[2].x;
            var secondMinY = secondCorners[0].y;
            var secondMaxY = secondCorners[2].y;
            Assert.That(firstMaxX <= secondMinX || secondMaxX <= firstMinX || firstMaxY <= secondMinY || secondMaxY <= firstMinY,
                $"{first.name} [{firstMinX:0.#},{firstMinY:0.#}]–[{firstMaxX:0.#},{firstMaxY:0.#}] must not overlap " +
                $"{second.name} [{secondMinX:0.#},{secondMinY:0.#}]–[{secondMaxX:0.#},{secondMaxY:0.#}]");
        }

        private static string CreateTempCsv()
        {
            var path = Path.Combine(Application.temporaryCachePath, "ui-test-" + System.Guid.NewGuid().ToString("N") + ".csv");
            File.WriteAllText(path, "time,depth\n0,0");
            return path;
        }

        private static Transform FindChildNamed(Transform root, string name)
        {
            foreach (var child in FindChildrenNamed(root, name))
            {
                return child;
            }

            return null;
        }

        private static List<Transform> FindChildrenNamed(Transform root, string name)
        {
            var matches = new List<Transform>();
            CollectChildrenNamed(root, name, matches);
            return matches;
        }

        private static void CollectChildrenNamed(Transform root, string name, List<Transform> matches)
        {
            foreach (Transform child in root)
            {
                if (child.name == name)
                {
                    matches.Add(child);
                }

                CollectChildrenNamed(child, name, matches);
            }
        }

        private static Text CreateText(Transform parent, string name)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<Text>();
        }

        private static void AssertNoUnitColumnsOutsideRows(Transform panel)
        {
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                if (!text.name.EndsWith("Unit", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.That(text.transform.parent, Is.Not.Null);
                Assert.That(text.transform.parent.name, Does.EndWith("Row"), text.name);
            }
        }

        private static Button CreateNavigationButton(Transform parent, string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            return buttonObject.GetComponent<Button>();
        }

        private static void SetHorizontalNavigation(Button left, Button right, Navigation.Mode mode)
        {
            var leftNavigation = left.navigation;
            leftNavigation.mode = mode;
            leftNavigation.selectOnRight = right;
            left.navigation = leftNavigation;
            var rightNavigation = right.navigation;
            rightNavigation.mode = mode;
            rightNavigation.selectOnLeft = left;
            right.navigation = rightNavigation;
        }

        private static void AssertHorizontalNavigationPreserved(Button left, Button right)
        {
            Assert.That(left.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(left.navigation.selectOnRight, Is.SameAs(right));
            Assert.That(right.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(right.navigation.selectOnLeft, Is.SameAs(left));
        }

        private static bool RootScrollIsAbsentOrDisabled(RectTransform drawer)
        {
            var rootScroll = drawer != null ? drawer.GetComponent<ScrollRect>() : null;
            return rootScroll == null || !rootScroll.enabled;
        }

        private static int CountDirectChildrenNamed(Transform parent, string name)
        {
            var count = 0;
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    count++;
                }
            }

            return count;
        }

        private static RectTransform InstantiateShippedDataInputPanel()
        {
            var canvas = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/DataInputPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            instance.transform.SetParent(canvas.transform, false);
            return instance.GetComponent<RectTransform>();
        }

        private static DataInputView CreateConfigurationLayoutProbe(
            float width,
            float height,
            bool useFallback,
            out RectTransform configurationArea,
            out RectTransform mainBody,
            out RectTransform playbackBar)
        {
            var canvasObject = new GameObject("ConfigurationLayoutCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(width, height);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var uiRoot = UiFactory.EnsureResponsiveRuntimeLayout(canvas);
            configurationArea = uiRoot.Find("ConfigurationArea").GetComponent<RectTransform>();
            mainBody = uiRoot.Find("MainBody").GetComponent<RectTransform>();
            playbackBar = uiRoot.Find("PlaybackBar").GetComponent<RectTransform>();

            var viewObject = new GameObject(useFallback ? "FallbackDataInputView" : "ConfiguredDataInputView", typeof(RectTransform));
            viewObject.transform.SetParent(uiRoot, false);
            var view = viewObject.AddComponent<DataInputView>();
            if (useFallback)
            {
                view.Initialize(CreateTempCsv(), SimulationProfile.Default, null);
            }
            else
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/DataInputPanel.prefab");
                Assert.That(prefab, Is.Not.Null);
                var instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                Assert.That(instance, Is.Not.Null);
                instance.transform.SetParent(configurationArea, false);
                view.Bind(new DataInputPanelRefs { panel = instance.GetComponent<RectTransform>() }, CreateTempCsv(), SimulationProfile.Default, null);
            }

            Canvas.ForceUpdateCanvases();
            return view;
        }

        private static void AssertConfigurationLayoutContract(
            RectTransform configurationArea,
            RectTransform mainBody,
            RectTransform playbackBar,
            float expectedConfigurationHeight,
            float expectedMainBodyBottom)
        {
            var configurationElement = configurationArea.GetComponent<LayoutElement>();
            var playbackTop = playbackBar.anchoredPosition.y + playbackBar.sizeDelta.y;
            var configurationBottom = configurationArea.anchoredPosition.y;
            var configurationTop = configurationBottom + configurationArea.sizeDelta.y;

            Assert.That(playbackBar.sizeDelta.y, Is.EqualTo(92f).Within(0.01f));
            Assert.That(configurationArea.sizeDelta.y, Is.EqualTo(expectedConfigurationHeight).Within(0.01f));
            Assert.That(configurationElement.minHeight, Is.EqualTo(expectedConfigurationHeight).Within(0.01f));
            Assert.That(configurationElement.preferredHeight, Is.EqualTo(expectedConfigurationHeight).Within(0.01f));
            Assert.That(mainBody.offsetMin.y, Is.EqualTo(expectedMainBodyBottom).Within(0.01f));
            Assert.That(configurationBottom, Is.EqualTo(playbackTop).Within(0.01f), "ConfigurationArea must start exactly above PlaybackBar.");
            Assert.That(mainBody.offsetMin.y, Is.EqualTo(configurationTop).Within(0.01f), "MainBody must start exactly above ConfigurationArea.");
            Assert.That(mainBody.rect.height, Is.GreaterThan(0f));
        }

        private static List<Text> FindLegacyConfigurationTitles(Transform panel)
        {
            var names = new HashSet<string>
            {
                "MissionConfigurationTitle",
                "TitleText",
                "ModelLabel",
                "SimulationLabel",
                "OceanCurrentLabel"
            };
            var titles = new List<Text>();
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                if (names.Contains(text.name))
                {
                    if (text.name == "TitleText" && text.transform.parent != panel)
                    {
                        continue;
                    }
                    titles.Add(text);
                }
            }

            return titles;
        }

        private static TypographyProbe CreateTypographyProbe(string canvasName, bool createFallbackContent)
        {
            var canvasObject = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var uiRoot = UiFactory.EnsureResponsiveRuntimeLayout(canvas);
            var contentRoot = uiRoot.Find("ConfigurationArea");
            Text title;
            Text label;
            Text value;
            Text button;

            if (createFallbackContent)
            {
                var dataInput = new GameObject("FallbackDataInput").AddComponent<DataInputView>();
                dataInput.Initialize(CreateTempCsv(), SimulationProfile.Default, null);
                var fallbackCanvas = Object.FindObjectsOfType<Canvas>(true);
                foreach (var candidate in fallbackCanvas)
                {
                    if (candidate.gameObject.name == "RuntimeCanvas" && candidate.transform.Find("MissionConfigurationPanel") != null)
                    {
                        canvas = candidate;
                        break;
                    }
                }

                title = FindChildNamed(canvas.transform, "MissionConfigurationTitle").GetComponent<Text>();
                label = FindChildNamed(canvas.transform, "CsvSourceLabel").GetComponent<Text>();
                value = FindChildNamed(canvas.transform, "MissionConfigurationStatus").GetComponent<Text>();
                button = FindChildNamed(canvas.transform, "LoadCsvButtonLabel").GetComponent<Text>();
            }
            else
            {
                title = CreateText(contentRoot, "PrefabMissionConfigurationTitle");
                label = CreateText(contentRoot, "PrefabCsvSourceLabel");
                value = CreateText(contentRoot, "PrefabMissionConfigurationStatus");
                var buttonRoot = new GameObject("PrefabLoadCsvButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                buttonRoot.transform.SetParent(contentRoot, false);
                button = CreateText(buttonRoot.transform, "PrefabLoadCsvButtonLabel");
                title.fontSize = 8;
                label.fontSize = 8;
                value.fontSize = 8;
                button.fontSize = 8;
            }

            return new TypographyProbe(canvas, title, label, value, button);
        }

        private static ResponsiveUiLayoutController CreateResponsiveLayoutController(out Canvas canvas)
        {
            var canvasObject = new GameObject("ResponsiveLayoutCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UiFactory.EnsureResponsiveRuntimeLayout(canvas);
            var runtimeRoot = canvasObject.AddComponent<RuntimeUiRoot>();
            runtimeRoot.ConfigureRuntimeReferences(canvas, canvas.transform.Find("ModalRoot") as RectTransform, new RuntimeUiReferences(), 0);
            var controller = canvasObject.AddComponent<ResponsiveUiLayoutController>();
            controller.SetAnimationsEnabledForTests(false);
            controller.Bind(runtimeRoot, runtimeRoot.References);
            return controller;
        }

        private static void ApplyTypography(Canvas canvas, float width, float height)
        {
            var runtimeRoot = canvas.gameObject.GetComponent<RuntimeUiRoot>() ?? canvas.gameObject.AddComponent<RuntimeUiRoot>();
            var modalRoot = canvas.transform.Find("ModalRoot") as RectTransform;
            runtimeRoot.ConfigureRuntimeReferences(canvas, modalRoot, new RuntimeUiReferences(), 0);
            var controller = canvas.gameObject.GetComponent<ResponsiveUiLayoutController>() ?? canvas.gameObject.AddComponent<ResponsiveUiLayoutController>();
            controller.SetAnimationsEnabledForTests(false);
            controller.Bind(runtimeRoot, runtimeRoot.References);
            controller.RefreshForScreen(width, height);
        }

        private readonly struct TypographyProbe
        {
            public TypographyProbe(Canvas runtimeCanvas, Text title, Text label, Text value, Text button)
            {
                this.runtimeCanvas = runtimeCanvas;
                this.title = title;
                this.label = label;
                this.value = value;
                this.button = button;
            }

            public readonly Canvas runtimeCanvas;
            public readonly Text title;
            public readonly Text label;
            public readonly Text value;
            public readonly Text button;
        }

        private static InputField CreateInput(Transform parent, string name)
        {
            var inputObject = new GameObject(name, typeof(RectTransform), typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            var input = inputObject.GetComponent<InputField>();
            input.textComponent = CreateText(inputObject.transform, "Text");
            return input;
        }

        private static PlaybackController CreatePlayback(IReadOnlyList<TelemetryFrame> frames)
        {
            var playback = new GameObject("Playback").AddComponent<PlaybackController>();
            playback.Initialize(new PlaybackModel(frames, rowsPerSecond: 1f));
            return playback;
        }

        private static PredictionController CreatePrediction(PlaybackController playback, IReadOnlyList<TelemetryFrame> frames)
        {
            var mapper = new GeoCoordinateMapper(frames[0], horizontalScale: 1f, depthScale: 1f);
            var prediction = new GameObject("Prediction").AddComponent<PredictionController>();
            prediction.Initialize(frames, mapper, playback);
            return prediction;
        }

        private static IReadOnlyList<TelemetryFrame> Frames(int count)
        {
            var frames = new List<TelemetryFrame>();
            for (var i = 0; i < count; i++)
            {
                frames.Add(new TelemetryFrame(i, $"t{i}", i, 120, 25, 10f + i, 100, 30f + i, 4f, -3f, 28.5f, 0.3f, 15f, "mode", "state", 2f, 44f, 120f, 80f, 300f, 17f, 32f));
            }

            return frames;
        }

        private static TelemetryFrame WithCoordinates(
            TelemetryFrame frame,
            double longitudeDeg,
            double latitudeDeg,
            float elapsedSeconds = float.NaN)
        {
            return new TelemetryFrame(
                frame.RowIndex,
                frame.RawTime,
                float.IsNaN(elapsedSeconds) ? frame.ElapsedSeconds : elapsedSeconds,
                longitudeDeg,
                latitudeDeg,
                frame.DepthM,
                frame.AltitudeM,
                frame.HeadingDeg,
                frame.PitchDeg,
                frame.RollDeg,
                frame.Voltage24V,
                frame.Current24A,
                frame.BatteryPercent,
                frame.WorkMode,
                frame.RunState,
                frame.TargetSegment,
                frame.TargetHeadingDeg,
                frame.TargetDepthM,
                frame.TargetAltitudeM,
                frame.PropellerRpm,
                frame.PistonMm,
                frame.TurnAngleDeg,
                frame.Diagnostics);
        }
    }
}
