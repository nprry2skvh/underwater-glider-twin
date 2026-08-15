using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class EditableUiReferenceTests
    {
        private UiTestObjectScope scope;

        [SetUp]
        public void SetUp()
        {
            scope = new UiTestObjectScope();
        }

        [TearDown]
        public void TearDown()
        {
            RuntimeUiFallback.Reset();
            scope.Dispose();
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
            serialized.ApplyModifiedPropertiesWithoutUndo();

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
            serialized.ApplyModifiedPropertiesWithoutUndo();

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
            serialized.ApplyModifiedPropertiesWithoutUndo();

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
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var issues = root.ValidateReferences(RuntimeUiValidationProfile.Strict);

            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.dashboard.depthValue"));
            Assert.That(issues, Has.Some.Property("FieldName").EqualTo("references.dataInput.mission.csvPathInput"));
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
    }
}
