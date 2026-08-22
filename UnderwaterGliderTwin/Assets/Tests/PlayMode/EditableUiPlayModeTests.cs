using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class EditableUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_HasSingleEditableUiRootAtRuntime()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var runtimeRoot = Object.FindObjectOfType<UnderwaterGliderTwin.UI.RuntimeUiRoot>(true);
            var canvasCount = 0;
            var eventSystemCount = 0;
            foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
            {
                if (canvas.gameObject.scene == scene)
                {
                    canvasCount++;
                }
            }

            foreach (var eventSystem in Object.FindObjectsOfType<EventSystem>(true))
            {
                if (eventSystem.gameObject.scene == scene)
                {
                    eventSystemCount++;
                }
            }

            Assert.That(canvasCount, Is.EqualTo(1));
            Assert.That(eventSystemCount, Is.EqualTo(1));
            Assert.That(FindSceneObject(scene, "RuntimeUiRoot"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "RuntimeCanvas"), Is.Not.Null);
            Assert.That(runtimeRoot, Is.Not.Null);
            Assert.That(runtimeRoot.ModalRoot.name, Is.EqualTo("ModalRoot"));
            Assert.That(runtimeRoot.DrawerLayer, Is.SameAs(runtimeRoot.ModalRoot));
            Assert.That(FindSceneObject(scene, "UiRoot"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "SystemBar"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "ConfigurationArea"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "MainBody"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "PlaybackBar"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "DrawerEntryLayer"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "DrawerScrim"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "TelemetryDrawerToggle").transform.parent.name, Is.EqualTo("DrawerEntryLayer"));
            Assert.That(FindSceneObject(scene, "StatusDrawerToggle").transform.parent.name, Is.EqualTo("DrawerEntryLayer"));
            Assert.That(FindSceneObjects(scene, "DrawerLayer").Count, Is.EqualTo(0));
            Assert.That(FindSceneObject(scene, "RuntimeUI"), Is.Null);
            Assert.That(Object.FindObjectsOfType<Button>(true).Length, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator MainScene_DrawersRemainUnderModalRootAndCanOpen()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var oceanDrawer = FindSceneObject(scene, "OceanCurrentDrawer");
            var flightDrawer = FindSceneObject(scene, "FlightLegDrawer");
            Assert.That(oceanDrawer, Is.Not.Null);
            Assert.That(flightDrawer, Is.Not.Null);
            Assert.That(oceanDrawer.transform.parent.name, Is.EqualTo("ModalRoot"));
            Assert.That(flightDrawer.transform.parent.name, Is.EqualTo("ModalRoot"));

            var oceanToggle = FindSceneObject(scene, "DrawerButton");
            Assert.That(oceanToggle, Is.Not.Null);
            var oceanButton = oceanToggle.GetComponent<Button>();
            Assert.That(oceanButton, Is.Not.Null);
            oceanButton.onClick.Invoke();
            yield return null;
            Assert.That(oceanDrawer.activeSelf, Is.True);

            var flightToggle = FindSceneObject(scene, "FlightLegSettingsButton");
            Assert.That(flightToggle, Is.Not.Null);
            var flightButton = flightToggle.GetComponent<Button>();
            Assert.That(flightButton, Is.Not.Null);
            flightButton.onClick.Invoke();
            yield return null;
            Assert.That(flightDrawer.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator MainScene_ConfigurationSummaryAndExpandedContent_KeepDataInputViewAliveAcrossRepeatedToggles()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            RuntimePredictionState.SetEnabled(false);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var dataInputView = Object.FindObjectOfType<DataInputView>(true);
            var dataInputPanel = FindSceneObject(scene, "DataInputPanel");
            Assert.That(dataInputView, Is.Not.Null);
            Assert.That(dataInputPanel, Is.Not.Null);

            var summaryBar = dataInputPanel.transform.Find("ConfigurationSummaryBar") as RectTransform;
            var expandedContent = dataInputPanel.transform.Find("ConfigurationExpandedContent") as RectTransform;
            Assert.That(summaryBar, Is.Not.Null);
            Assert.That(expandedContent, Is.Not.Null);

            var toggleButtonObject = FindDescendant(dataInputPanel.transform, "MissionConfigurationDrawerToggleButton");
            Assert.That(toggleButtonObject, Is.Not.Null);
            var toggleButton = toggleButtonObject.GetComponent<Button>();
            Assert.That(toggleButton, Is.Not.Null);

            var statusText = FindSceneComponent<Text>(scene, "MissionConfigurationStatus");
            var csvPathInput = FindSceneComponent<InputField>(scene, "CsvPathInput");
            var loadCsvButton = FindSceneComponent<Button>(scene, "LoadCsvButton");
            var predictionToggleButton = FindSceneComponent<Button>(scene, "PredictionToggleButton");
            var simulationCyclesInput = FindSceneComponent<InputField>(scene, "SimulationCyclesInput");
            var simulationApplyButton = FindSceneComponent<Button>(scene, "SimulationApplyButton");
            Assert.That(statusText, Is.Not.Null);
            Assert.That(csvPathInput, Is.Not.Null);
            Assert.That(loadCsvButton, Is.Not.Null);
            Assert.That(predictionToggleButton, Is.Not.Null);
            Assert.That(simulationCyclesInput, Is.Not.Null);
            Assert.That(simulationApplyButton, Is.Not.Null);

            csvPathInput.text = string.Empty;
            loadCsvButton.onClick.Invoke();
            yield return null;
            Assert.That(statusText.text, Is.EqualTo("请输入 CSV 文件路径"));

            RuntimePredictionState.SetEnabled(false);
            predictionToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(RuntimePredictionState.PredictionEnabled, Is.True);

            simulationCyclesInput.text = string.Empty;
            simulationApplyButton.onClick.Invoke();
            yield return null;
            Assert.That(statusText.text, Is.EqualTo("循环次数必须是整数"));

            var expandedContentGroup = expandedContent.GetComponent<CanvasGroup>();
            var expandedContentLayout = expandedContent.GetComponent<LayoutElement>();
            Assert.That(expandedContentGroup, Is.Not.Null);
            Assert.That(expandedContentLayout, Is.Not.Null);

            for (var toggleIndex = 0; toggleIndex < 8; toggleIndex++)
            {
                toggleButton.onClick.Invoke();
                yield return null;

                var expectedExpanded = toggleIndex % 2 == 0;
                Assert.That(dataInputView.ConfigurationExpandedForTests, Is.EqualTo(expectedExpanded));
                Assert.That(expandedContentGroup.alpha, Is.EqualTo(expectedExpanded ? 1f : 0f));
                Assert.That(expandedContentGroup.interactable, Is.EqualTo(expectedExpanded));
                Assert.That(expandedContentGroup.blocksRaycasts, Is.EqualTo(expectedExpanded));
                Assert.That(expandedContentLayout.preferredHeight, Is.EqualTo(expectedExpanded ? expandedContentLayout.minHeight : 0f));
            }

            Assert.That(dataInputPanel.activeSelf, Is.True);
            Assert.That(dataInputView.enabled, Is.True);
            Assert.That(dataInputView.ConfigurationExpandedForTests, Is.False);
            Assert.That(CountNamedChildren(dataInputPanel.transform, "ConfigurationSummaryBar"), Is.EqualTo(1));
            Assert.That(CountNamedChildren(dataInputPanel.transform, "ConfigurationExpandedContent"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "MissionConfigurationDrawerToggleButton"), Is.EqualTo(1));

            csvPathInput.text = string.Empty;
            loadCsvButton.onClick.Invoke();
            yield return null;
            Assert.That(statusText.text, Is.EqualTo("请输入 CSV 文件路径"));

            RuntimePredictionState.SetEnabled(true);
            predictionToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(RuntimePredictionState.PredictionEnabled, Is.False);

            simulationCyclesInput.text = string.Empty;
            simulationApplyButton.onClick.Invoke();
            yield return null;
            Assert.That(statusText.text, Is.EqualTo("循环次数必须是整数"));
        }

        [UnityTest]
        public IEnumerator MainScene_ExpandedConfiguration_RejectsUnlabeledLegacyInputFields()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var dataInputPanel = FindSceneObject(scene, "DataInputPanel");
            var expandedContent = FindDescendant(dataInputPanel != null ? dataInputPanel.transform : null, "ConfigurationExpandedContent");
            var toggle = FindDescendant(dataInputPanel != null ? dataInputPanel.transform : null, "MissionConfigurationDrawerToggleButton")?.GetComponent<Button>();
            Assert.That(expandedContent, Is.Not.Null);
            Assert.That(toggle, Is.Not.Null);

            toggle.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var activeLegacyInputs = new System.Collections.Generic.List<string>();
            foreach (var input in expandedContent.GetComponentsInChildren<InputField>(true))
            {
                if (!input.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Debug.Log($"[Task7-DIAG] active InputField path={GetTransformPath(input.transform)} parent={input.transform.parent?.name ?? "<root>"} sibling={input.transform.GetSiblingIndex()} rect={DescribeRect(input.transform as RectTransform)} text='{input.text}'");
                if (input.transform.parent == null
                    || !input.transform.parent.name.EndsWith("Field", System.StringComparison.Ordinal)
                    || input.transform.parent.GetComponent<InputField>() != null)
                {
                    activeLegacyInputs.Add(GetTransformPath(input.transform));
                }
            }

            foreach (var text in expandedContent.GetComponentsInChildren<Text>(true))
            {
                if (text.gameObject.activeInHierarchy)
                {
                    Debug.Log($"[Task7-DIAG] active Text path={GetTransformPath(text.transform)} parent={text.transform.parent?.name ?? "<root>"} sibling={text.transform.GetSiblingIndex()} rect={DescribeRect(text.transform as RectTransform)} text='{text.text}'");
                }
            }

            foreach (var image in expandedContent.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject.activeInHierarchy)
                {
                    Debug.Log($"[Task7-DIAG] active Image path={GetTransformPath(image.transform)} parent={image.transform.parent?.name ?? "<root>"} sibling={image.transform.GetSiblingIndex()} rect={DescribeRect(image.transform as RectTransform)} enabled={image.enabled} color={image.color}");
                }
            }

            var referenceValue = FindDescendant(expandedContent.transform, "ReferenceCycleDurationValue");
            Assert.That(referenceValue, Is.Not.Null, "Expanded configuration must expose the reference duration value.");
            Assert.That(referenceValue.transform.parent.name.EndsWith("Field", System.StringComparison.Ordinal), Is.True,
                "ReferenceCycleDurationValue must be inside its labeled field row.");
            Assert.That(referenceValue.transform.parent.GetComponent<InputField>(), Is.Null,
                "ReferenceCycleDurationValue wrapper must be separate from the legacy Text/InputField object.");
            Assert.That(referenceValue.GetComponent<Text>().text, Is.Not.EqualTo("ReferenceCycleDurationValue"),
                "ReferenceCycleDurationValue must be bound to a formatted duration, not the prefab placeholder name.");
            Assert.That(FindSceneObjects(scene, "ReferenceCycleDurationValue").Count, Is.EqualTo(1),
                "ReferenceCycleDurationValue must not be duplicated as a stale legacy object.");
            Assert.That(activeLegacyInputs, Is.Empty,
                "Expanded configuration must not render active legacy InputField controls outside labeled *Field rows.");

            var missionSection = FindDescendant(expandedContent.transform, "MissionSectionCard");
            var configurationStatus = FindSceneComponent<Text>(scene, "MissionConfigurationStatus");
            Assert.That(missionSection, Is.Not.Null);
            Assert.That(configurationStatus, Is.Not.Null);
            Assert.That(configurationStatus.transform.parent, Is.EqualTo(missionSection.transform),
                "Configuration status must be an independent row inside MissionSectionCard.");
            Assert.That(configurationStatus.transform.GetSiblingIndex(), Is.EqualTo(missionSection.transform.childCount - 1),
                "Configuration status must be the final row in MissionSectionCard.");
            var statusLayout = configurationStatus.GetComponent<LayoutElement>();
            Assert.That(statusLayout, Is.Not.Null);
            Assert.That(statusLayout.ignoreLayout, Is.False,
                "Configuration status must participate in the mission section VerticalLayoutGroup.");
            Assert.That(statusLayout.preferredHeight, Is.InRange(24f, 28f));
            Assert.That(configurationStatus.rectTransform.rect.height, Is.InRange(24f, 28f));
        }

        [UnityTest]
        public IEnumerator MainScene_TelemetryEmptyStateShowsGuidanceInsteadOfBlankDarkBar()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var dashboard = Object.FindObjectOfType<DashboardView>(true);
            Assert.That(dashboard, Is.Not.Null);
            dashboard.gameObject.SendMessage("RefreshTelemetryStateForTests", new System.Collections.Generic.List<TelemetryFrame>(), SendMessageOptions.DontRequireReceiver);

            var emptyState = FindDescendant(dashboard.transform, "TelemetryEmptyState");
            Assert.That(emptyState, Is.Not.Null);
            Assert.That(emptyState.activeSelf, Is.True);

            var title = emptyState.transform.Find("TelemetryEmptyStateTitle")?.GetComponent<Text>();
            var hint = emptyState.transform.Find("TelemetryEmptyStateHint")?.GetComponent<Text>();
            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Is.EqualTo("尚未加载有效轨迹"));
            Assert.That(hint, Is.Not.Null);
            Assert.That(hint.text, Is.Not.Empty);

            dashboard.gameObject.SendMessage("RefreshTelemetryStateForTests", new System.Collections.Generic.List<TelemetryFrame> { default(TelemetryFrame) }, SendMessageOptions.DontRequireReceiver);
            Assert.That(emptyState.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator MainScene_NumericRowsKeepFixedValueColumnAndStableWidthAcrossRefresh()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var depthValue = FindSceneComponent<Text>(SceneManager.GetActiveScene(), "DepthValue");
            Assert.That(depthValue, Is.Not.Null);
            var row = depthValue.transform.parent as RectTransform;
            Assert.That(row, Is.Not.Null);
            var unit = FindDescendant(row, "DepthValueUnit");
            Assert.That(unit, Is.Not.Null);
            Assert.That(depthValue.text, Does.Not.Contain("m"));
            Assert.That(unit.GetComponent<Text>().text, Is.EqualTo("m"));
            AssertNoUnitColumnsOutsideRows(SceneManager.GetActiveScene());

            var valueLayout = depthValue.GetComponent<LayoutElement>();
            Assert.That(valueLayout, Is.Not.Null);
            Assert.That(valueLayout.flexibleWidth, Is.EqualTo(0f));
            Assert.That(valueLayout.preferredWidth, Is.GreaterThan(0f));

            Canvas.ForceUpdateCanvases();
            var widthBefore = row.rect.width;
            depthValue.text = "1.0";
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            depthValue.text = "123456789.0";
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            yield return null;
            Assert.That(row.rect.width, Is.EqualTo(widthBefore).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator MainScene_StatusBadgeKeepsPredictionAndWarningTextVisibleTogether()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var badgeObject = FindSceneObject(scene, "MissionHealthBadge");
            Assert.That(badgeObject, Is.Not.Null);
            var badge = badgeObject.GetComponent<UiStateBadge>();
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.StateText, Is.Not.Null);
            Assert.That(badge.Label, Is.Not.Empty);

            var predictionStatus = FindSceneComponent<Text>(scene, "PredictionStatusValue");
            Assert.That(predictionStatus, Is.Not.Null);
            Assert.That(predictionStatus.text, Is.Not.Empty);

            var predictionTime = FindSceneComponent<Text>(scene, "PredictionTimeValue");
            var predictionTimeUnit = FindSceneComponent<Text>(scene, "PredictionTimeValueUnit");
            Assert.That(predictionTime, Is.Not.Null);
            Assert.That(predictionTimeUnit, Is.Not.Null);
            Assert.That(predictionTime.text, Does.Not.Contain("ms"));
            Assert.That(predictionTimeUnit.text, Is.EqualTo("ms"));
        }

        [UnityTest]
        public IEnumerator MainScene_LongCsvPathUsesBoundedDisplayAndKeepsFullTooltip()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var csvInput = FindSceneComponent<InputField>(scene, "CsvPathInput");
            Assert.That(csvInput, Is.Not.Null);
            var inputRect = csvInput.transform as RectTransform;
            var widthBefore = inputRect.rect.width;
            var longPath = @"E:\mission-data\2026\august\north-pacific\deep-water\very-long-telemetry-export-file-name-with-diagnostics.csv";
            csvInput.text = longPath;
            csvInput.onValueChanged.Invoke(longPath);
            yield return null;

            var display = FindSceneComponent<Text>(scene, "CsvPathDisplay");
            Assert.That(display, Is.Not.Null);
            Assert.That(display.text, Does.Contain("…"));
            Assert.That(display.text, Is.Not.EqualTo(longPath));
            Assert.That(inputRect.rect.width, Is.EqualTo(widthBefore).Within(0.1f));

            var tooltip = csvInput.GetComponent<UiTooltip>();
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.Message, Is.EqualTo(longPath));
        }

        [UnityTest]
        public IEnumerator MainScene_HasSingleVisibleCameraCommandOwner()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            Assert.That(CountActiveSceneObjects(scene, "CameraFollowCommand"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "CameraGlobalCommand"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "CameraTopCommand"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "CameraSideCommand"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "CameraOrbitCommand"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "CameraResetCommand"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "CameraFollowButton"), Is.EqualTo(0));
            Assert.That(CountActiveSceneObjects(scene, "CameraGlobalButton"), Is.EqualTo(0));
            Assert.That(CountActiveSceneObjects(scene, "CameraOrbitButton"), Is.EqualTo(0));
            Assert.That(CountActiveSceneObjects(scene, "MissionVolumeButton"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "Speed1Button"), Is.EqualTo(1));
            Assert.That(CountActiveSceneObjects(scene, "TrajectoryToggle"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MainScene_UsesThreePlaybackSegmentsAndVisibleSystemBarHierarchy()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var playback = FindSceneObject(scene, "PlaybackControlsPanel");
            Assert.That(playback, Is.Not.Null);
            Assert.That(FindDescendant(playback.transform, "PlaybackOperationsRow"), Is.Not.Null);
            Assert.That(FindDescendant(playback.transform, "PlaybackTimelineRow"), Is.Not.Null);
            Assert.That(FindDescendant(playback.transform, "PlaybackOptionsRow"), Is.Not.Null);
            Assert.That(CountActiveSceneObjects(scene, "CameraFollowButton"), Is.EqualTo(0));
            Assert.That(CountActiveSceneObjects(scene, "CameraGlobalButton"), Is.EqualTo(0));
            Assert.That(CountActiveSceneObjects(scene, "CameraOrbitButton"), Is.EqualTo(0));
            Assert.That(FindSceneObject(scene, "CommandCenterProductName"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "CommandCenterExit"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MainScene_BoundTelemetryAndStatusRowsHaveDistinctVerticalPositions()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var root = Object.FindObjectOfType<RuntimeUiRoot>(true).transform;
            AssertRowsHaveDistinctVerticalPositions(FindDescendant(root, "DashboardPanel").transform);
            AssertRowsHaveDistinctVerticalPositions(FindDescendant(root, "StatusPanel").transform);
        }

        [UnityTest]
        public IEnumerator MainScene_CollapsedConfigurationDoesNotRenderUnmovedLegacyInputFields()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var dataInputPanel = FindSceneObject(SceneManager.GetActiveScene(), "DataInputPanel");
            var expandedContent = FindDescendant(dataInputPanel.transform, "ConfigurationExpandedContent");
            var activeLegacyInputs = new System.Collections.Generic.List<string>();
            foreach (var input in dataInputPanel.GetComponentsInChildren<InputField>(true))
            {
                if (input.gameObject.activeInHierarchy && !IsDescendantOf(input.transform, expandedContent.transform))
                {
                    activeLegacyInputs.Add(input.name);
                }
            }

            Assert.That(activeLegacyInputs, Is.Empty,
                "Collapsed DataInputPanel must not leave active legacy InputField/Image controls outside ConfigurationExpandedContent.");
        }

        [UnityTest]
        public IEnumerator MainScene_BoundPlaybackStatusIsPlacedInTimelineRowWithReadableHeight()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var playback = FindSceneObject(SceneManager.GetActiveScene(), "PlaybackControlsPanel");
            var statusText = FindSceneComponent<Text>(SceneManager.GetActiveScene(), "PlaybackStatus");
            var timeline = FindDescendant(playback.transform, "PlaybackTimelineRow").GetComponent<RectTransform>();

            Assert.That(statusText.transform.parent.name, Is.EqualTo("PlaybackTimelineRow"));
            Assert.That(statusText.rectTransform.rect.height, Is.GreaterThanOrEqualTo(24f));
            Assert.That(statusText.fontSize, Is.GreaterThanOrEqualTo(14));
            Assert.That(timeline.rect.height, Is.GreaterThanOrEqualTo(statusText.rectTransform.rect.height));
        }

        [UnityTest]
        public IEnumerator MainScene_BoundPrefabLegacyGraphicsAreHiddenOrReflowed()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var root = Object.FindObjectOfType<RuntimeUiRoot>(true).transform;
            var dashboard = FindDescendant(root, "DashboardPanel").transform;
            foreach (var unit in dashboard.GetComponentsInChildren<Text>(true))
            {
                if (unit.name.EndsWith("Unit", System.StringComparison.Ordinal))
                {
                    Assert.That(unit.gameObject.activeInHierarchy, Is.False,
                        $"Collapsed Dashboard advanced unit {unit.name} must not remain visible outside its detail state.");
                }
            }

            var alarm = FindSceneObject(scene, "AlarmValue");
            Assert.That(alarm == null || !alarm.activeInHierarchy, Is.True);
            var dataPanel = FindSceneObject(scene, "DataInputPanel");
            var dataBackground = dataPanel != null ? dataPanel.GetComponent<Image>() : null;
            Assert.That(dataBackground == null || !dataBackground.enabled, Is.True);

            var oceanRow = FindDescendant(root, "OceanToolbarCommandsRow").transform;
            foreach (var name in new[] { "CameraFollowCommand", "CameraGlobalCommand", "CameraTopCommand", "CameraSideCommand", "CameraOrbitCommand", "CameraResetCommand" })
            {
                Assert.That(FindDescendant(oceanRow, name), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator MainScene_SystemBarHeaderUsesNonOverlappingAbsoluteSlots()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var systemBar = FindSceneObject(scene, "SystemBar");
            var header = FindDescendant(systemBar != null ? systemBar.transform : null, "CommandCenterHeader");
            Assert.That(header, Is.Not.Null, "SystemBar must own the runtime command header.");
            Assert.That(header.transform.parent.name, Is.EqualTo("SystemBar"));
            var headerRect = header.GetComponent<RectTransform>();

            var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
            Assert.That(headerLayout == null || !headerLayout.enabled, Is.True,
                "SystemBar header uses fixed RectTransform slots; a legacy HorizontalLayoutGroup must not rewrite them.");

            var product = FindDescendant(header.transform, "CommandCenterProductName").GetComponent<RectTransform>();
            var health = FindDescendant(header.transform, "CommandCenterSystemHealth").GetComponent<RectTransform>();
            var runtime = FindDescendant(header.transform, "CommandCenterRuntime").GetComponent<RectTransform>();
            var exit = FindDescendant(header.transform, "CommandCenterExit").GetComponent<RectTransform>();

            Assert.That(product.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(product.anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(product.pivot, Is.EqualTo(Vector2.zero));
            Assert.That(product.rect.width, Is.GreaterThan(0f));
            var productBounds = GetHorizontalBoundsInParent(product, headerRect);
            var healthBounds = GetHorizontalBoundsInParent(health, headerRect);
            var runtimeBounds = GetHorizontalBoundsInParent(runtime, headerRect);
            var exitBounds = GetHorizontalBoundsInParent(exit, headerRect);
            Assert.That(productBounds.y, Is.LessThan(healthBounds.x));
            Assert.That(healthBounds.y, Is.LessThan(runtimeBounds.x));
            Assert.That(runtimeBounds.y, Is.LessThan(exitBounds.x));
            Assert.That(exitBounds.y, Is.LessThanOrEqualTo(headerRect.rect.width));
        }

        [UnityTest]
        public IEnumerator MainScene_CommandToolbarSelectionFollowsGlobalInitialization()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var global = FindSceneComponent<Button>(scene, "CameraGlobalCommand");
            var follow = FindSceneComponent<Button>(scene, "CameraFollowCommand");
            Assert.That(global, Is.Not.Null);
            Assert.That(follow, Is.Not.Null);
            Assert.That(global.GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandAccent));
            Assert.That(follow.GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandPanelEdge));
        }

        [UnityTest]
        public IEnumerator MainScene_CommandToolbarResetKeepsGlobalSelection()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var top = FindSceneComponent<Button>(scene, "CameraTopCommand");
            var reset = FindSceneComponent<Button>(scene, "CameraResetCommand");
            var global = FindSceneComponent<Button>(scene, "CameraGlobalCommand");
            var follow = FindSceneComponent<Button>(scene, "CameraFollowCommand");
            Assert.That(top, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);
            Assert.That(global, Is.Not.Null);
            Assert.That(follow, Is.Not.Null);

            top.onClick.Invoke();
            reset.onClick.Invoke();
            yield return null;

            Assert.That(global.GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandAccent));
            Assert.That(follow.GetComponent<Outline>().effectColor, Is.EqualTo(UiFactory.CommandPanelEdge));
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            var names = new System.Collections.Generic.List<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Add(current.name);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static string DescribeRect(RectTransform rect)
        {
            return rect == null ? "<no-rect>" : $"pos={rect.anchoredPosition} size={rect.rect.size} delta={rect.sizeDelta}";
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.gameObject.scene == scene && transform.name == objectName)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static System.Collections.Generic.List<GameObject> FindSceneObjects(Scene scene, string objectName)
        {
            var matches = new System.Collections.Generic.List<GameObject>();
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.gameObject.scene == scene && transform.name == objectName)
                {
                    matches.Add(transform.gameObject);
                }
            }

            return matches;
        }

        private static int CountActiveSceneObjects(Scene scene, string objectName)
        {
            var count = 0;
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.gameObject.scene == scene
                    && transform.name == objectName
                    && transform.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
        {
            var gameObject = FindSceneObject(scene, objectName);
            return gameObject != null ? gameObject.GetComponent<T>() : null;
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static void AssertRowsHaveDistinctVerticalPositions(Transform root)
        {
            var rowPositions = new System.Collections.Generic.List<float>();
            foreach (var row in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (!row.name.EndsWith("Row", System.StringComparison.Ordinal)
                    || row.GetComponentsInChildren<Text>(true).Length == 0)
                {
                    continue;
                }

                foreach (var existingPosition in rowPositions)
                {
                    Assert.That(Mathf.Abs(existingPosition - row.position.y), Is.GreaterThan(0.1f),
                        $"{root.name}/{row.name} must not share a vertical position with another active row.");
                }

                rowPositions.Add(row.position.y);
            }

            Assert.That(rowPositions.Count, Is.GreaterThan(1), $"{root.name} must expose multiple active rows for the diagnostic.");
        }

        private static bool IsDescendantOf(Transform child, Transform ancestor)
        {
            if (child == null || ancestor == null)
            {
                return false;
            }

            var current = child;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Vector2 GetHorizontalBoundsInParent(RectTransform rect, RectTransform parent)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var left = parent.InverseTransformPoint(corners[0]).x;
            var right = parent.InverseTransformPoint(corners[3]).x;
            return new Vector2(Mathf.Min(left, right), Mathf.Max(left, right));
        }

        private static int CountNamedChildren(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < parent.childCount; index++)
            {
                if (parent.GetChild(index).name == objectName)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertNoUnitColumnsOutsideRows(Scene scene)
        {
            foreach (var text in Object.FindObjectsOfType<Text>(true))
            {
                if (text.gameObject.scene != scene
                    || !text.name.EndsWith("Unit", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.That(text.transform.parent, Is.Not.Null);
                Assert.That(text.transform.parent.name, Does.EndWith("Row"), text.name);
            }
        }
    }
}
