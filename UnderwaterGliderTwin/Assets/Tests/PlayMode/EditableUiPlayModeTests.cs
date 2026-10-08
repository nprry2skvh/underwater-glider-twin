using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class EditableUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_UsesOneActiveRuntimeUiRootAndEventSystem()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var runtimeRoot = FindSceneObject(scene, "RuntimeUI");
            var editableRoot = FindSceneObject(scene, "RuntimeUiRoot");
            Assert.That(runtimeRoot, Is.Not.Null);
            Assert.That(runtimeRoot.activeInHierarchy, Is.True);
            Assert.That(editableRoot, Is.Not.Null);
            Assert.That(editableRoot.activeInHierarchy, Is.False);

            var activeCanvasCount = 0;
            var eventSystemCount = 0;
            foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
            {
                if (canvas.gameObject.scene == scene && canvas.isActiveAndEnabled)
                {
                    activeCanvasCount++;
                    Assert.That(IsDescendantOrSelf(canvas.transform, runtimeRoot.transform), Is.True);
                }
            }

            foreach (var eventSystem in Object.FindObjectsOfType<EventSystem>(true))
            {
                if (eventSystem.gameObject.scene == scene && eventSystem.isActiveAndEnabled)
                {
                    eventSystemCount++;
                }
            }

            Assert.That(activeCanvasCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(eventSystemCount, Is.EqualTo(1));
            Assert.That(FindDescendant(runtimeRoot.transform, "RuntimeCanvas"), Is.Not.Null);
            Assert.That(runtimeRoot.GetComponentsInChildren<Button>(true).Length, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator MainScene_GeneratedDrawersKeepInputsAndCanOpen()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var runtimeRoot = FindSceneObject(scene, "RuntimeUI");
            Assert.That(runtimeRoot, Is.Not.Null);
            var oceanDrawer = FindDescendant(runtimeRoot.transform, "OceanCurrentDrawerPanel");
            var flightDrawer = FindDescendant(runtimeRoot.transform, "FlightLegDrawerPanel");
            Assert.That(oceanDrawer, Is.Not.Null);
            Assert.That(flightDrawer, Is.Not.Null);
            Assert.That(FindDescendant(oceanDrawer.transform, "OceanCurrentDrawerMinDepthInput"), Is.Not.Null);
            Assert.That(FindDescendant(flightDrawer.transform, "DescentPitchInput"), Is.Not.Null);

            var layout = runtimeRoot.GetComponentInChildren<ReferenceHudLayoutController>(true);
            Assert.That(layout, Is.Not.Null);
            layout.SetDrawerOpen(ReferenceHudLayoutController.ConfigurationDrawerId, true);
            var oceanToggle = FindDescendant(runtimeRoot.transform, "OceanCurrentDrawerButton");
            Assert.That(oceanToggle, Is.Not.Null);
            var oceanButton = oceanToggle.GetComponent<Button>();
            Assert.That(oceanButton, Is.Not.Null);
            Assert.That(oceanButton.gameObject.activeInHierarchy, Is.True);
            oceanButton.onClick.Invoke();
            yield return null;
            Assert.That(oceanDrawer.activeInHierarchy, Is.True);

            var oceanClose = FindDescendant(oceanDrawer.transform, "OceanCurrentDrawerCloseButton");
            oceanClose.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(oceanDrawer.activeInHierarchy, Is.False);

            var flightToggle = FindDescendant(runtimeRoot.transform, "FlightLegSettingsButton");
            Assert.That(flightToggle, Is.Not.Null);
            var flightButton = flightToggle.GetComponent<Button>();
            Assert.That(flightButton, Is.Not.Null);
            Assert.That(flightButton.gameObject.activeInHierarchy, Is.True);
            flightButton.onClick.Invoke();
            yield return null;
            Assert.That(flightDrawer.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator MainScene_GeneratedUiPreservesParameterAndCommandInventory()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var runtimeRoot = FindSceneObject(SceneManager.GetActiveScene(), "RuntimeUI");
            Assert.That(runtimeRoot, Is.Not.Null);

            var inputs = new[]
            {
                "CsvPathInput", "PredictionHorizonInput", "MissionLongitudeInput", "MissionLatitudeInput",
                "SimulationCyclesInput", "SimulationDurationInput", "SimulationDepthInput", "SimulationWaterColumnInput",
                "SimulationHeadingInput", "SimulationHeadingDeltaInput", "SimulationPitchInput", "SimulationRollInput",
                "DescentNetBuoyancyInput", "DescentPitchInput", "DescentRollInput",
                "AscentNetBuoyancyInput", "AscentPitchInput", "AscentRollInput",
                "OceanCurrentDrawerMinDepthInput", "OceanCurrentDrawerMaxDepthInput",
                "OceanCurrentDrawerEastwardInput", "OceanCurrentDrawerNorthwardInput",
                "OceanCurrentPrefetchHalfWidthInput", "OceanCurrentForecastWindowInput", "OceanCurrentLocalFileInput",
                "DynamicsMassInput", "DynamicsReferenceAreaInput", "DynamicsReferenceLengthInput",
                "DynamicsWingSpanInput", "DynamicsMeanChordInput", "DynamicsRollInertiaInput",
                "DynamicsPitchInertiaInput", "DynamicsYawInertiaInput", "DynamicsLiftSlopeInput",
                "DynamicsBaseDragInput", "DynamicsTurnaroundDurationInput", "DynamicsBuoyancyExponentInput",
                "DynamicsBuoyancyDeadbandInput", "DynamicsPistonHysteresisInput", "DynamicsRollExponentInput",
                "DynamicsRollDeadbandInput", "DynamicsRollRestoringGainInput", "DynamicsMaxRollMomentInput"
            };
            foreach (var name in inputs)
            {
                var item = FindDescendant(runtimeRoot.transform, name);
                Assert.That(item, Is.Not.Null, "Missing parameter: " + name);
                Assert.That(item.GetComponent<InputField>(), Is.Not.Null, "Not an input: " + name);
            }

            var commands = new[]
            {
                "LoadCsvButton", "ApplyPredictionConfigButton", "PredictionToggleButton",
                "ApplyReferenceCycleButton", "SimulationApplyButton", "FlightLegSettingsButton",
                "FlightLegRestoreDefaultsButton", "OceanCurrentDrawerPreviousButton", "OceanCurrentDrawerNextButton",
                "OceanCurrentDrawerAddButton", "OceanCurrentDrawerDeleteButton", "OceanCurrentDrawerSaveButton",
                "OceanCurrentDrawerLookupButton", "OceanCurrentOnlineModeButton", "OceanCurrentCacheOnlyModeButton",
                "OceanCurrentLocalFileModeButton", "DynamicsSeaTrialPresetButton", "DynamicsCalmWaterPresetButton",
                "DynamicsCalibrateFromCsvButton", "PlayPauseButton", "ReverseButton", "ReplayButton",
                "ResetButton", "ExportButton", "ExitButton", "CameraFollowButton", "CameraGlobalButton",
                "CameraOrbitButton", "MissionVolumeButton", "Speed05Button", "Speed1Button",
                "Speed2Button", "Speed5Button", "Speed10Button"
            };
            foreach (var name in commands)
            {
                var item = FindDescendant(runtimeRoot.transform, name);
                Assert.That(item, Is.Not.Null, "Missing command: " + name);
                Assert.That(item.GetComponent<Button>(), Is.Not.Null, "Not a button: " + name);
                if (name == "MissionVolumeButton")
                {
                    Assert.That(item.activeInHierarchy, Is.True, "Mission volume action must be reachable");
                }
            }

            Assert.That(FindDescendant(runtimeRoot.transform, "ProgressSlider")?.GetComponent<Slider>(), Is.Not.Null);
            foreach (var name in new[] { "FogToggle", "ParticlesToggle", "TrajectoryToggle" })
            {
                Assert.That(FindDescendant(runtimeRoot.transform, name)?.GetComponent<Toggle>(), Is.Not.Null, "Missing toggle: " + name);
            }
        }

        [UnityTest]
        public IEnumerator MainScene_StatusDrawerContainsLiveMetricsAndToolbarsShareCenter()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            yield return null;

            var runtimeRoot = FindSceneObject(SceneManager.GetActiveScene(), "RuntimeUI");
            Assert.That(runtimeRoot, Is.Not.Null);
            var layout = runtimeRoot.GetComponentInChildren<ReferenceHudLayoutController>(true);
            Assert.That(layout, Is.Not.Null);
            layout.SetDrawerOpen(ReferenceHudLayoutController.StatusDrawerId, true);
            Canvas.ForceUpdateCanvases();

            var panel = FindDescendant(runtimeRoot.transform, "MissionStatusPanel");
            var viewport = FindDescendant(runtimeRoot.transform, "MissionStatusViewport");
            Assert.That(panel.activeInHierarchy, Is.True);
            Assert.That(viewport.GetComponent<ScrollRect>(), Is.Not.Null);
            foreach (var name in new[]
            {
                "StatusDepthValue", "StatusHeadingValue", "StatusWaterSpeedValue", "StatusGroundSpeedValue",
                "StatusNetBuoyancyValue", "StatusLiftValue", "StatusPistonValue", "PredictionStatusValue",
                "DriftValue", "EngineeringValidationValue"
            })
            {
                Assert.That(FindDescendant(viewport.transform, name)?.GetComponent<Text>(), Is.Not.Null, name);
            }

            var playbackBar = FindDescendant(runtimeRoot.transform, "PlaybackControlsPanel").GetComponent<RectTransform>();
            var oceanBar = FindDescendant(runtimeRoot.transform, "OceanCommandToolbar").GetComponent<RectTransform>();
            Assert.That(playbackBar.anchorMin.x, Is.EqualTo(0.5f));
            Assert.That(oceanBar.anchorMin.x, Is.EqualTo(0.5f));
            Assert.That(playbackBar.anchoredPosition.x, Is.EqualTo(0f).Within(0.1f));
            Assert.That(oceanBar.anchoredPosition.x, Is.EqualTo(0f).Within(0.1f));
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static bool IsDescendantOrSelf(Transform candidate, Transform root)
        {
            for (var current = candidate; current != null; current = current.parent)
            {
                if (current == root)
                {
                    return true;
                }
            }

            return false;
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
    }
}
