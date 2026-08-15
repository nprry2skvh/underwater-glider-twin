using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Editor;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class EditableUiReferenceTests
    {
        private UiTestObjectScope scope;
        private bool previousIgnoreFailingMessages;

        [SetUp]
        public void SetUp()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            scope = new UiTestObjectScope();
        }

        [TearDown]
        public void TearDown()
        {
            RuntimeUiFallback.Reset();
            scope.Dispose();
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
        }

        [Test]
        public void Validator_ReportsPrefabObjectPathAndFieldName()
        {
            var root = scope.CreateRoot("DashboardPanel");
            var child = new GameObject("DepthValue");
            child.transform.SetParent(root.transform, false);
            var issues = new List<UiReferenceIssue>();

            UiReferenceValidator.Require(null, child.AddComponent<UnityEngine.UI.Text>(), "DashboardPanel.prefab", "DepthValue", issues);

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].PrefabName, Is.EqualTo("DashboardPanel.prefab"));
            Assert.That(issues[0].ObjectPath, Is.EqualTo("DashboardPanel/DepthValue"));
            Assert.That(issues[0].FieldName, Is.EqualTo("DepthValue"));
        }

        [Test]
        public void RuntimeUiFallback_DefaultsToDisabled()
        {
            Assert.That(RuntimeUiFallback.AllowRuntimeFallback, Is.False);
        }

        [Test]
        public void Validator_HandlesMissingOwnerAndPrefabName()
        {
            var issues = new List<UiReferenceIssue>();

            UiReferenceValidator.Require(null, null, null, null, issues);

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].PrefabName, Is.EqualTo("<unknown ui>"));
            Assert.That(issues[0].ObjectPath, Is.EqualTo("<missing owner>"));
            Assert.That(issues[0].FieldName, Is.EqualTo("<unnamed field>"));
        }

        [Test]
        public void RuntimeUiRoot_RequiresCanvasAndModalRoot()
        {
            var root = AddRuntimeUiRoot(scope.CreateRoot("RuntimeUiRoot"));
            var issues = root.ValidateReferences(RuntimeUiValidationProfile.BootstrapOnly);

            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("runtimeCanvas"));
            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("modalRoot"));
        }

        [Test]
        public void RuntimeUiRoot_ReportsDuplicateEventSystemsAndLongLivedUi()
        {
            scope.CreateRoot("EventSystemA").AddComponent<EventSystem>();
            scope.CreateRoot("EventSystemB").AddComponent<EventSystem>();
            var rootObject = scope.CreateRoot("RuntimeUiRoot");
            var firstPanel = scope.CreateRoot("DashboardPanel");
            var secondPanel = scope.CreateRoot("DashboardPanel");
            firstPanel.transform.SetParent(rootObject.transform, false);
            secondPanel.transform.SetParent(rootObject.transform, false);
            var root = AddRuntimeUiRoot(rootObject);

            var issues = root.ValidateReferences(RuntimeUiValidationProfile.BootstrapOnly);

            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("EventSystem"));
            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("DashboardPanel"));
        }

        [Test]
        public void RuntimeUiRoot_ReportsMissingGroupedReferences()
        {
            var rootObject = scope.CreateRoot("RuntimeUiRoot");
            var canvas = new GameObject("RuntimeCanvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(rootObject.transform, false);
            var modalRoot = new GameObject("ModalRoot").AddComponent<RectTransform>();
            modalRoot.transform.SetParent(canvas.transform, false);
            var root = AddRuntimeUiRoot(rootObject);
            var serialized = new UnityEditor.SerializedObject(root);
            serialized.FindProperty("runtimeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            ApplySerialized(serialized);

            var issues = root.ValidateReferences(RuntimeUiValidationProfile.Strict);

            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.dashboard.depthValue"));
            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.playback.playPauseButton"));
            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.dataInput.mission.csvPathInput"));
        }

        [Test]
        public void RuntimeUiRoot_EnabledPanelsUsesExplicitPanelMask()
        {
            var rootObject = scope.CreateRoot("RuntimeUiRoot");
            var canvas = new GameObject("RuntimeCanvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(rootObject.transform, false);
            var modalRoot = new GameObject("ModalRoot").AddComponent<RectTransform>();
            modalRoot.transform.SetParent(canvas.transform, false);
            var hiddenDashboard = new GameObject("DashboardPanel").AddComponent<RectTransform>();
            hiddenDashboard.gameObject.SetActive(false);
            hiddenDashboard.transform.SetParent(canvas.transform, false);
            var root = AddRuntimeUiRoot(rootObject);
            var serialized = new UnityEditor.SerializedObject(root);
            serialized.FindProperty("runtimeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            serialized.FindProperty("enabledPanelValidationMask").intValue = (int)RuntimeUiPanelFlags.Dashboard;
            serialized.FindProperty("references.dashboard.panel").objectReferenceValue = hiddenDashboard;
            ApplySerialized(serialized);

            var issues = root.ValidateReferences(RuntimeUiValidationProfile.EnabledPanels);

            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.dashboard.depthValue"));
            Assert.That(issues, Has.None.Property("FieldName").EqualTo("references.dataInput.mission.csvPathInput"));
        }

        [Test]
        public void RuntimeUiRoot_EnabledPanelsWithNoneSkipsPanelInternals()
        {
            var rootObject = scope.CreateRoot("RuntimeUiRoot");
            var canvas = new GameObject("RuntimeCanvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(rootObject.transform, false);
            var modalRoot = new GameObject("ModalRoot").AddComponent<RectTransform>();
            modalRoot.transform.SetParent(canvas.transform, false);
            var dashboard = new GameObject("DashboardPanel").AddComponent<RectTransform>();
            dashboard.transform.SetParent(canvas.transform, false);
            var root = AddRuntimeUiRoot(rootObject);
            var serialized = new UnityEditor.SerializedObject(root);
            serialized.FindProperty("runtimeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            serialized.FindProperty("enabledPanelValidationMask").intValue = (int)RuntimeUiPanelFlags.None;
            serialized.FindProperty("references.dashboard.panel").objectReferenceValue = dashboard;
            ApplySerialized(serialized);

            var issues = root.ValidateReferences(RuntimeUiValidationProfile.EnabledPanels);

            Assert.That(issues, Has.None.Property("FieldName").EqualTo("references.dashboard.depthValue"));
        }

        [Test]
        public void RuntimeUiRoot_StrictIgnoresPanelMaskAndValidatesAllRequiredGroups()
        {
            var rootObject = scope.CreateRoot("RuntimeUiRoot");
            var canvas = new GameObject("RuntimeCanvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(rootObject.transform, false);
            var modalRoot = new GameObject("ModalRoot").AddComponent<RectTransform>();
            modalRoot.transform.SetParent(canvas.transform, false);
            var root = AddRuntimeUiRoot(rootObject);
            var serialized = new UnityEditor.SerializedObject(root);
            serialized.FindProperty("runtimeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            serialized.FindProperty("enabledPanelValidationMask").intValue = (int)RuntimeUiPanelFlags.None;
            ApplySerialized(serialized);

            var issues = root.ValidateReferences(RuntimeUiValidationProfile.Strict);

            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.dashboard.depthValue"));
            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.dataInput.mission.csvPathInput"));
        }

        [Test]
        public void UiFactory_RejectsRuntimeCanvasCreationWhenFallbackDisabled()
        {
            RuntimeUiFallback.AllowRuntimeFallback = false;
            var owner = scope.CreateRoot("Dashboard").transform;

            var ex = Assert.Throws<System.InvalidOperationException>(() => UiFactory.EnsureCanvas(owner, "DashboardPanel", null));

            Assert.That(ex.Message, Does.Contain("Runtime UI fallback is disabled"));
        }

        [Test]
        public void UiFactory_DoesNotUseUnrelatedGlobalCanvas()
        {
            RuntimeUiFallback.AllowRuntimeFallback = true;
            var unrelated = scope.CreateRoot("UnrelatedCanvas").AddComponent<Canvas>();
            var owner = scope.CreateRoot("Dashboard").transform;
            var previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Canvas canvas;
            try
            {
                canvas = UiFactory.EnsureCanvas(owner, "DashboardPanel", null);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
            }

            Assert.That(canvas, Is.Not.SameAs(unrelated));
            Assert.That(canvas.transform.parent, Is.EqualTo(owner));
        }

        [Test]
        public void ClearDynamicRuntimeUi_DoesNotDestroyRowTemplate()
        {
            var view = scope.CreateRoot("DataInput").AddComponent<DataInputView>();
            var contentRoot = new GameObject("OceanLayerContent").AddComponent<RectTransform>();
            contentRoot.transform.SetParent(view.transform, false);
            var template = new GameObject("OceanCurrentLayerRowTemplate").AddComponent<RectTransform>();
            template.transform.SetParent(contentRoot.transform, false);
            var dynamicRows = new GameObject("DynamicRowsRoot").AddComponent<RectTransform>();
            dynamicRows.transform.SetParent(contentRoot.transform, false);
            var dynamicRow = new GameObject("OceanCurrentLayerRow1").AddComponent<RectTransform>();
            dynamicRow.transform.SetParent(dynamicRows.transform, false);
            var refs = new DataInputPanelRefs();
            refs.ocean.oceanLayerRowTemplate = template;
            refs.ocean.dynamicRowsRoot = dynamicRows;

            view.BindDynamicContainersForTests(refs);
            view.ClearDynamicRuntimeUi();

            Assert.That(template, Is.Not.Null);
            Assert.That(dynamicRows.childCount, Is.EqualTo(0));
        }

        [Test]
        public void WelcomeBootstrap_ReportsMissingSerializedUi()
        {
            var bootstrap = scope.CreateRoot("WelcomeBootstrap").AddComponent<WelcomeBootstrap>();

            var issues = bootstrap.ValidateReferences();

            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("welcomeCanvas"));
            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("csvInput"));
        }

        [Test]
        public void EditableUiSceneBuilder_BuildWelcomeSceneIsIdempotentAndBackfillsReferences()
        {
            var previous = SceneManager.GetActiveScene().path;
            try
            {
                EditableUiSceneBuilder.BuildWelcomeScene();
                EditableUiSceneBuilder.BuildWelcomeScene();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Welcome.unity");

                Assert.That(FindObjectsNamed("WelcomeCanvas"), Is.EqualTo(1));
                Assert.That(FindObjectsNamed("BackgroundImage"), Is.EqualTo(1));
                Assert.That(FindObjectsNamed("LaunchPanel"), Is.EqualTo(1));
                Assert.That(FindObjectsNamed("CsvPathInput"), Is.EqualTo(1));
                Assert.That(FindObjectsNamed("ConfirmCsvButton"), Is.EqualTo(1));
                Assert.That(FindObjectsNamed("StartCsvButton"), Is.EqualTo(1));
                Assert.That(FindObjectsNamed("SimulationButton"), Is.EqualTo(1));
                var bootstrap = Object.FindObjectOfType<WelcomeBootstrap>();
                Assert.That(bootstrap, Is.Not.Null);
                Assert.That(bootstrap.ValidateReferences(), Is.Empty);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                {
                    RestorePreviousScene(previous);
                }
            }
        }

        [Test]
        public void EditableUiSceneBuilder_BuildWelcomeScenePreservesExistingVisualOverrides()
        {
            var previous = SceneManager.GetActiveScene().path;
            try
            {
                EditableUiSceneBuilder.BuildWelcomeScene();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Welcome.unity");
                var title = GameObject.Find("TitleText").GetComponent<UnityEngine.UI.Text>();
                var panel = GameObject.Find("LaunchPanel").GetComponent<UnityEngine.UI.Image>();
                var panelRect = panel.GetComponent<RectTransform>();
                title.text = "Custom Welcome Title";
                title.fontSize = 41;
                panel.color = new Color(0.40f, 0.10f, 0.70f, 0.90f);
                panelRect.anchorMin = new Vector2(0.20f, 0.10f);
                panelRect.anchorMax = new Vector2(0.80f, 0.90f);
                UnityEditor.EditorUtility.SetDirty(title);
                UnityEditor.EditorUtility.SetDirty(panel);
                UnityEditor.EditorUtility.SetDirty(panelRect);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                EditableUiSceneBuilder.BuildWelcomeScene();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Welcome.unity");
                title = GameObject.Find("TitleText").GetComponent<UnityEngine.UI.Text>();
                panel = GameObject.Find("LaunchPanel").GetComponent<UnityEngine.UI.Image>();
                panelRect = panel.GetComponent<RectTransform>();

                Assert.That(title.text, Is.EqualTo("Custom Welcome Title"));
                Assert.That(title.fontSize, Is.EqualTo(41));
                Assert.That(panel.color, Is.EqualTo(new Color(0.40f, 0.10f, 0.70f, 0.90f)));
                Assert.That(panelRect.anchorMin, Is.EqualTo(new Vector2(0.20f, 0.10f)));
                Assert.That(panelRect.anchorMax, Is.EqualTo(new Vector2(0.80f, 0.90f)));
            }
            finally
            {
                if (SceneManager.GetActiveScene().path == "Assets/Scenes/Welcome.unity")
                {
                    if (SceneManager.GetActiveScene().isDirty)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    }

                    EditableUiSceneBuilder.ResetWelcomeDefaults();
                }

                if (!string.IsNullOrEmpty(previous))
                {
                    RestorePreviousScene(previous);
                }
            }
        }

        [Test]
        public void MainScene_HasEditableRuntimeUiHierarchy()
        {
            var previous = SceneManager.GetActiveScene().path;
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

                Assert.That(GameObject.Find("RuntimeUiRoot"), Is.Not.Null);
                Assert.That(GameObject.Find("RuntimeCanvas"), Is.Not.Null);
                Assert.That(GameObject.Find("CommandCenterHeader"), Is.Not.Null);
                Assert.That(GameObject.Find("DashboardPanel"), Is.Not.Null);
                Assert.That(GameObject.Find("StatusPanel"), Is.Not.Null);
                Assert.That(GameObject.Find("DataInputPanel"), Is.Not.Null);
                Assert.That(GameObject.Find("PlaybackControlsPanel"), Is.Not.Null);
                Assert.That(GameObject.Find("OceanCommandToolbar"), Is.Not.Null);
                Assert.That(GameObject.Find("ModalRoot"), Is.Not.Null);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                {
                    RestorePreviousScene(previous);
                }
            }
        }

        [Test]
        public void MainScene_LongLivedPanelsArePrefabInstances()
        {
            var previous = SceneManager.GetActiveScene().path;
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

                Assert.That(UnityEditor.PrefabUtility.GetPrefabInstanceStatus(GameObject.Find("DashboardPanel")), Is.EqualTo(UnityEditor.PrefabInstanceStatus.Connected));
                Assert.That(UnityEditor.PrefabUtility.GetPrefabInstanceStatus(GameObject.Find("StatusPanel")), Is.EqualTo(UnityEditor.PrefabInstanceStatus.Connected));
                Assert.That(UnityEditor.PrefabUtility.GetPrefabInstanceStatus(GameObject.Find("DataInputPanel")), Is.EqualTo(UnityEditor.PrefabInstanceStatus.Connected));
                Assert.That(UnityEditor.PrefabUtility.GetPrefabInstanceStatus(GameObject.Find("PlaybackControlsPanel")), Is.EqualTo(UnityEditor.PrefabInstanceStatus.Connected));
                Assert.That(UnityEditor.PrefabUtility.GetPrefabInstanceStatus(GameObject.Find("OceanCommandToolbar")), Is.EqualTo(UnityEditor.PrefabInstanceStatus.Connected));
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                {
                    RestorePreviousScene(previous);
                }
            }
        }

        [Test]
        public void MainScene_LongLivedPanelsAreUnderRuntimeCanvasWithExpectedPrefabSources()
        {
            var previous = SceneManager.GetActiveScene().path;
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                var runtimeCanvas = GameObject.Find("RuntimeCanvas").transform;
                AssertPanelUnderCanvasWithSource("DashboardPanel", runtimeCanvas, "Assets/UI/Prefabs/DashboardPanel.prefab");
                AssertPanelUnderCanvasWithSource("StatusPanel", runtimeCanvas, "Assets/UI/Prefabs/StatusPanel.prefab");
                AssertPanelUnderCanvasWithSource("DataInputPanel", runtimeCanvas, "Assets/UI/Prefabs/DataInputPanel.prefab");
                AssertPanelUnderCanvasWithSource("PlaybackControlsPanel", runtimeCanvas, "Assets/UI/Prefabs/PlaybackControlsPanel.prefab");
                AssertPanelUnderCanvasWithSource("OceanCommandToolbar", runtimeCanvas, "Assets/UI/Prefabs/OceanCommandToolbar.prefab");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                {
                    RestorePreviousScene(previous);
                }
            }
        }

        [Test]
        public void EditableUiSceneBuilder_BuildMainSceneRejectsSameNameNonPrefabPanel()
        {
            var previous = SceneManager.GetActiveScene().path;
            GameObject nonPrefab = null;
            try
            {
                EditableUiSceneBuilder.ResetMainUiDefaults();
                EditableUiSceneBuilder.BuildMainScene();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                var canvas = GameObject.Find("RuntimeCanvas");
                nonPrefab = new GameObject("DashboardPanel");
                nonPrefab.transform.SetParent(canvas.transform, false);
                LogAssert.Expect(LogType.Error, "Main.unity: RuntimeUiRoot -> DashboardPanel: Expected one long-lived UI object named DashboardPanel, found 2.");
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                var ex = Assert.Throws<System.InvalidOperationException>(() => EditableUiSceneBuilder.BuildMainScene());

                Assert.That(ex.Message, Does.Contain("same-name non-Prefab"));
            }
            finally
            {
                if (SceneManager.GetActiveScene().path == "Assets/Scenes/Main.unity")
                {
                    foreach (var transform in Object.FindObjectsOfType<Transform>(true))
                    {
                        if (transform.name == "DashboardPanel"
                            && UnityEditor.PrefabUtility.GetPrefabInstanceStatus(transform.gameObject) != UnityEditor.PrefabInstanceStatus.Connected)
                        {
                            Object.DestroyImmediate(transform.gameObject);
                        }
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                }

                if (!string.IsNullOrEmpty(previous))
                {
                    RestorePreviousScene(previous);
                }
            }
        }

        [Test]
        public void EditableUiSceneBuilder_BuildMainScenePreservesExistingPrefabInstanceOverrides()
        {
            var previous = SceneManager.GetActiveScene().path;
            var originalPosition = Vector2.zero;
            var originalSize = Vector2.zero;
            try
            {
                EditableUiSceneBuilder.BuildMainScene();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                var panel = GameObject.Find("DashboardPanel").GetComponent<RectTransform>();
                originalPosition = panel.anchoredPosition;
                originalSize = panel.sizeDelta;
                panel.anchoredPosition = new Vector2(123f, -456f);
                panel.sizeDelta = new Vector2(777f, 333f);
                UnityEditor.EditorUtility.SetDirty(panel);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                EditableUiSceneBuilder.BuildMainScene();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                panel = GameObject.Find("DashboardPanel").GetComponent<RectTransform>();

                Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(123f, -456f)));
                Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(777f, 333f)));
                panel.anchoredPosition = originalPosition;
                panel.sizeDelta = originalSize;
                UnityEditor.EditorUtility.SetDirty(panel);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                {
                    RestorePreviousScene(previous);
                }
            }
        }

        private static void AssertPanelUnderCanvasWithSource(string panelName, Transform runtimeCanvas, string expectedPrefabPath)
        {
            var panel = GameObject.Find(panelName);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.transform.parent, Is.EqualTo(runtimeCanvas));
            var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(panel);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(source), Is.EqualTo(expectedPrefabPath));
        }

        private static void RestorePreviousScene(string previous)
        {
            if (previous == "Assets/Scenes/Welcome.unity")
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
        }

        private static int FindObjectsNamed(string objectName)
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

        private static RuntimeUiRoot AddRuntimeUiRoot(GameObject rootObject)
        {
            var previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                return rootObject.AddComponent<RuntimeUiRoot>();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
            }
        }

        private static void ApplySerialized(UnityEditor.SerializedObject serialized)
        {
            var previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
            }
        }
    }
}
