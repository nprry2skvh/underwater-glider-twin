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

            for (var toggleIndex = 0; toggleIndex < 4; toggleIndex++)
            {
                toggleButton.onClick.Invoke();
                yield return null;
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
    }
}
