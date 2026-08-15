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
            Assert.That(FindText("DepthValue").text, Is.EqualTo("10.0 m"));
            Assert.That(FindText("BatteryValue").text, Is.EqualTo("15 %"));
            Assert.That(FindText("VelocityXValue").text, Is.EqualTo("0.00 m/s"));
            Assert.That(FindText("VelocityYValue").text, Is.EqualTo("0.00 m/s"));
            Assert.That(FindText("VelocityZValue").text, Is.EqualTo("0.00 m/s"));
        }

        [Test]
        public void DashboardView_BindsExistingTelemetryText()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var root = new GameObject("DashboardPanel", typeof(RectTransform));
            var depth = CreateText(root.transform, "DepthValue");
            var battery = CreateText(root.transform, "BatteryValue");
            var refs = new DashboardPanelRefs { panel = root.GetComponent<RectTransform>(), depthValue = depth, batteryValue = battery };
            var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

            dashboard.Bind(refs, playback, prediction);

            Assert.That(depth.text, Is.EqualTo("10.0 m"));
            Assert.That(battery.text, Is.EqualTo("15 %"));
        }

        [Test]
        public void StatusPanelView_BindsExistingStatusText()
        {
            var playback = CreatePlayback(Frames(2));
            var prediction = CreatePrediction(playback, Frames(2));
            var root = new GameObject("StatusPanel", typeof(RectTransform));
            var mission = CreateText(root.transform, "MissionValue");
            var battery = CreateText(root.transform, "BatteryValue");
            var refs = new StatusPanelRefs { panel = root.GetComponent<RectTransform>(), missionValue = mission, batteryValue = battery };
            var status = new GameObject("Status").AddComponent<StatusPanelView>();

            status.Bind(refs, playback, new AlarmEvaluator(1000f, 1f, 90f), null, prediction);

            Assert.That(mission.text, Is.Not.Empty);
            Assert.That(battery.text, Is.EqualTo("15 %"));
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

            Assert.That(FindText("VelocityXValue").text, Is.EqualTo("1.01 m/s"));
            Assert.That(FindText("VelocityYValue").text, Is.EqualTo("-0.30 m/s"));
            Assert.That(FindText("VelocityZValue").text, Is.EqualTo("1.11 m/s"));
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

            Assert.That(FindText("OceanCurrentValue").text, Is.EqualTo("东 0.20 m/s 北 -0.40 m/s"));
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
            var header = GameObject.Find("MissionConfigurationDrawerHeader").GetComponent<RectTransform>();
            var viewport = GameObject.Find("MissionConfigurationViewport").GetComponent<RectTransform>();
            GameObject.Find("MissionConfigurationDrawerToggleButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(drawer.sizeDelta.y, Is.GreaterThan(48f));
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
            Assert.That(panel.activeSelf, Is.False);
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
            Assert.That(field.sizeDelta.y, Is.LessThanOrEqualTo(54f));
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

            var modal = GameObject.Find("OceanCurrentModalCanvas").GetComponent<Canvas>();
            var main = GameObject.Find("RuntimeCanvas").GetComponent<Canvas>();
            var blocker = GameObject.Find("OceanCurrentModalRaycastBlocker").GetComponent<Image>();
            Assert.That(modal.overrideSorting, Is.True);
            Assert.That(modal.sortingOrder, Is.GreaterThan(main.sortingOrder));
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

            Assert.That(FindText("WaterSpeedValue").text, Is.EqualTo("0.51 m/s"));
            Assert.That(FindText("GroundSpeedValue").text, Is.EqualTo("0.59 m/s"));
            Assert.That(FindText("SideSlipValue").text, Is.EqualTo("12.4°"));
            Assert.That(FindText("OceanCurrentValue").text, Is.EqualTo("东 0.20 m/s 北 -0.10 m/s"));
            Assert.That(FindText("NetBuoyancyValue").text, Is.EqualTo("6.5 N"));
            Assert.That(FindText("EnergyValue").text, Is.EqualTo("8.2 W"));
            Assert.That(FindText("AngleOfAttackValue").text, Is.EqualTo("3.2 deg"));
            Assert.That(FindText("LiftForceValue").text, Is.EqualTo("4.5 N"));
            Assert.That(FindText("DragForceValue").text, Is.EqualTo("7.8 N"));
            Assert.That(FindText("AngularRateValue").text, Is.EqualTo("2.14 deg/s"));
            Assert.That(FindText("PistonPositionValue").text, Is.EqualTo("12.5 mm"));
            Assert.That(FindText("ControlSurfaceValue").text, Is.EqualTo("R 4.0 / P -3.0 / Y 1.5 deg"));
            Assert.That(FindText("ActuatorPowerValue").text, Is.EqualTo("6.7 W"));
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

        private static Text FindText(string name)
        {
            return GameObject.Find(name).GetComponent<Text>();
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
                $"{first.name} must not overlap {second.name}");
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
    }
}
