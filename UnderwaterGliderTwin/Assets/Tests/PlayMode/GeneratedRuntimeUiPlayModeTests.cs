using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class GeneratedRuntimeUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator GeneratedRuntimeUi_CreatesCanonicalHierarchy_AndSameSizeRefreshDoesNotDuplicateNodes()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            var bootstrapObject = new GameObject("GeneratedTwinBootstrap");
            bootstrapObject.SetActive(false);
            var bootstrap = bootstrapObject.AddComponent<TwinBootstrap>();
            SetPrivateField(bootstrap, "useGeneratedRuntimeUi", true);
            SetPrivateField(bootstrap, "allowRuntimeFallback", false);
            SetPrivateField(bootstrap, "strictUiValidation", false);

            bootstrapObject.SetActive(true);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(GameObject.Find("UiRoot"), Is.Not.Null);
            Assert.That(GameObject.Find("SystemBar"), Is.Not.Null);
            Assert.That(GameObject.Find("ConfigurationArea"), Is.Not.Null);
            Assert.That(GameObject.Find("MainBody"), Is.Not.Null);
            Assert.That(GameObject.Find("ViewportColumn"), Is.Not.Null);
            Assert.That(GameObject.Find("PlaybackBar"), Is.Not.Null);
            Assert.That(GameObject.Find("UiRoot/DrawerEntryLayer/TelemetryDrawerToggle"), Is.Not.Null);
            Assert.That(GameObject.Find("UiRoot/DrawerEntryLayer/StatusDrawerToggle"), Is.Not.Null);
            Assert.That(Object.FindObjectsOfType<Canvas>(true).Length, Is.EqualTo(1));
            Assert.That(CountObjectsNamed("ModalRoot"), Is.EqualTo(1));

            var runtimeRoot = Object.FindObjectOfType<RuntimeUiRoot>(true);
            Assert.That(runtimeRoot, Is.Not.Null);
            Assert.That(runtimeRoot.DrawerLayer, Is.SameAs(runtimeRoot.ModalRoot));

            var controller = Object.FindObjectOfType<ResponsiveUiLayoutController>(true);
            Assert.That(controller, Is.Not.Null);

            controller.RefreshForScreen(1279f, 720f);
            controller.RefreshForScreen(1279f, 720f);
            controller.RefreshForScreen(1279f, 720f);
            yield return null;

            Assert.That(CountObjectsNamed("TelemetryDrawerToggle"), Is.EqualTo(1));
            Assert.That(CountObjectsNamed("StatusDrawerToggle"), Is.EqualTo(1));
            Assert.That(CountObjectsNamed("DrawerScrim"), Is.EqualTo(1));
            Assert.That(CountObjectsNamed("OceanCurrentDrawer"), Is.EqualTo(1));
            Assert.That(CountObjectsNamed("FlightLegDrawer"), Is.EqualTo(1));

            Object.Destroy(bootstrapObject);
        }

        [UnityTest]
        public IEnumerator MissingConfiguredRuntimeUi_WithFallbackDisabled_DoesNotRun()
        {
            RuntimeDataSourceState.UseSimulation(SimulationProfile.Default);
            var bootstrapObject = new GameObject("ConfiguredTwinBootstrap");
            bootstrapObject.SetActive(false);
            var bootstrap = bootstrapObject.AddComponent<TwinBootstrap>();
            SetPrivateField(bootstrap, "useGeneratedRuntimeUi", false);
            SetPrivateField(bootstrap, "allowRuntimeFallback", false);

            LogAssert.Expect(LogType.Error, "TwinBootstrap requires a serialized RuntimeUiRoot when runtime fallback is disabled.");
            bootstrapObject.SetActive(true);
            yield return null;

            Assert.That(bootstrap.enabled, Is.False);
            // The test runner may still have the serialized Main scene loaded;
            // verify that fallback did not create the generated owner instead
            // of asserting globally against that scene's legitimate canvas.
            Assert.That(GameObject.Find("RuntimeUI"), Is.Null);

            Object.Destroy(bootstrapObject);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(instance, value);
        }

        private static int CountObjectsNamed(string objectName)
        {
            var count = 0;
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.name == objectName)
                {
                    count++;
                }
            }

            return count;
        }

    }
}
