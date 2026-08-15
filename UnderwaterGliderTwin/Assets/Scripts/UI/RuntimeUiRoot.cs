using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace UnderwaterGliderTwin.UI
{
    public sealed class RuntimeUiRoot : MonoBehaviour, IUiReferenceProvider
    {
        [SerializeField] private Canvas runtimeCanvas;
        [SerializeField] private RectTransform modalRoot;
        [SerializeField] private RuntimeUiPanelFlags enabledPanelValidationMask;
        [SerializeField] private RuntimeUiReferences references = new RuntimeUiReferences();

        public Canvas RuntimeCanvas => runtimeCanvas;
        public RectTransform ModalRoot => modalRoot;
        public RuntimeUiPanelFlags EnabledPanelValidationMask => enabledPanelValidationMask;
        public RuntimeUiReferences References => references;

        public List<UiReferenceIssue> ValidateReferences(RuntimeUiValidationProfile profile = RuntimeUiValidationProfile.EnabledPanels)
        {
            var issues = new List<UiReferenceIssue>();
            CollectReferenceIssues(profile, issues);
            return issues;
        }

        public void CollectReferenceIssues(List<UiReferenceIssue> issues)
        {
            CollectReferenceIssues(RuntimeUiValidationProfile.Strict, issues);
        }

        private void CollectReferenceIssues(RuntimeUiValidationProfile profile, List<UiReferenceIssue> issues)
        {
            UiReferenceValidator.Require(runtimeCanvas, this, "Main.unity", "runtimeCanvas", issues);
            UiReferenceValidator.Require(modalRoot, this, "Main.unity", "modalRoot", issues);
            CollectGroupedReferenceIssues(profile, issues);
            ReportDuplicateComponentsInOwnerScene<Canvas>("Canvas", issues);
            ReportDuplicateComponentsInOwnerScene<EventSystem>("EventSystem", issues);
            ReportDuplicateChildren("DashboardPanel", issues);
            ReportDuplicateChildren("StatusPanel", issues);
            ReportDuplicateChildren("DataInputPanel", issues);
            ReportDuplicateChildren("PlaybackControlsPanel", issues);
            ReportDuplicateChildren("OceanCommandToolbar", issues);
        }

        private void OnValidate()
        {
            if (!gameObject.scene.IsValid() || string.IsNullOrEmpty(gameObject.scene.path))
            {
                return;
            }

            var issues = ValidateReferences(RuntimeUiValidationProfile.EnabledPanels);
            foreach (var issue in issues)
            {
                Debug.LogError(issue.ToString(), this);
            }
        }

        private void CollectGroupedReferenceIssues(RuntimeUiValidationProfile profile, List<UiReferenceIssue> issues)
        {
            if (references == null)
            {
                UiReferenceValidator.Require(null, this, "Main.unity", "references", issues);
                return;
            }

            if (profile == RuntimeUiValidationProfile.BootstrapOnly)
            {
                return;
            }

            if (profile == RuntimeUiValidationProfile.Strict)
            {
                references.CollectReferenceIssues(this, "Main.unity", "references", issues);
                CollectRowTemplateIssues(GetOceanLayerRowTemplate(), "references.dataInput.ocean.oceanLayerRowTemplate", issues);
                return;
            }

            CollectGroupIfSelected(RuntimeUiPanelFlags.Dashboard, references.dashboard, "references.dashboard", issues);
            CollectGroupIfSelected(RuntimeUiPanelFlags.Status, references.status, "references.status", issues);
            CollectGroupIfSelected(RuntimeUiPanelFlags.DataInput, references.dataInput, "references.dataInput", issues);
            CollectGroupIfSelected(RuntimeUiPanelFlags.Playback, references.playback, "references.playback", issues);
            CollectGroupIfSelected(RuntimeUiPanelFlags.OceanToolbar, references.oceanToolbar, "references.oceanToolbar", issues);
            var rowTemplate = GetOceanLayerRowTemplate();
            if (rowTemplate != null)
            {
                CollectRowTemplateIssues(rowTemplate, "references.dataInput.ocean.oceanLayerRowTemplate", issues);
            }
        }

        private void CollectGroupIfSelected(RuntimeUiPanelFlags flag, IUiReferenceGroup group, string groupPath, List<UiReferenceIssue> issues)
        {
            if ((enabledPanelValidationMask & flag) == 0)
            {
                return;
            }

            if (group == null)
            {
                UiReferenceValidator.Require(null, this, "Main.unity", groupPath, issues);
                return;
            }

            group.CollectReferenceIssues(this, "Main.unity", groupPath, issues);
        }

        private RectTransform GetOceanLayerRowTemplate()
        {
            return references != null && references.dataInput != null && references.dataInput.ocean != null
                ? references.dataInput.ocean.oceanLayerRowTemplate
                : null;
        }

        private void CollectRowTemplateIssues(RectTransform template, string fieldName, List<UiReferenceIssue> issues)
        {
            UiReferenceValidator.Require(template, this, "OceanCurrentLayerRow.prefab", fieldName, issues);
            if (template == null)
            {
                return;
            }

            var providers = template.GetComponents<IUiReferenceProvider>();
            if (providers.Length == 0)
            {
                issues.Add(new UiReferenceIssue(
                    "OceanCurrentLayerRow.prefab",
                    UiReferenceValidator.GetPath(template),
                    fieldName,
                    "RowTemplate must include an IUiReferenceProvider such as OceanCurrentLayerRowView."));
                return;
            }

            foreach (var provider in providers)
            {
                provider.CollectReferenceIssues(issues);
            }
        }

        public bool TryEnsureSingleEventSystem()
        {
            var eventSystems = FindSceneComponents<EventSystem>();
            if (eventSystems.Length > 1)
            {
                Debug.LogError("RuntimeUiRoot found duplicate EventSystem objects.", this);
                return false;
            }

            if (eventSystems.Length == 1)
            {
                return true;
            }

            var eventSystem = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystem, gameObject.scene);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            return true;
        }

        private T[] FindSceneComponents<T>() where T : Component
        {
            var all = UnityEngine.Object.FindObjectsOfType<T>(true);
            var matches = new List<T>();
            foreach (var item in all)
            {
                if (item.gameObject.scene == gameObject.scene)
                {
                    matches.Add(item);
                }
            }

            return matches.ToArray();
        }

        private void ReportDuplicateComponentsInOwnerScene<T>(string fieldName, List<UiReferenceIssue> issues) where T : Component
        {
            var matches = FindSceneComponents<T>();
            if (matches.Length <= 1)
            {
                return;
            }

            issues.Add(new UiReferenceIssue(
                "Main.unity",
                UiReferenceValidator.GetPath(transform),
                fieldName,
                $"Expected exactly one {fieldName}, found {matches.Length}."));
        }

        private void ReportDuplicateChildren(string name, List<UiReferenceIssue> issues)
        {
            var count = 0;
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    count++;
                }
            }

            if (count > 1)
            {
                issues.Add(new UiReferenceIssue(
                    "Main.unity",
                    UiReferenceValidator.GetPath(transform),
                    name,
                    $"Expected one long-lived UI object named {name}, found {count}."));
            }
        }
    }
}
