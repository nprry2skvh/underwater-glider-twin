using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;

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
    }
}
