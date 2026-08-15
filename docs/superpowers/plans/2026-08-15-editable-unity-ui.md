# Editable Unity UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the Unity UI from runtime-created long-lived controls to editable scene and Prefab UI while preserving CSV launch, simulation, playback, telemetry, status, ocean controls, drawers, modals, and dynamic lists.

**Architecture:** `Welcome` owns a serialized `WelcomeCanvas`; `Main` owns one serialized `RuntimeUiRoot`, which owns `RuntimeCanvas`, long-lived panels, `ModalRoot`, row templates, and drawer containers. Runtime scripts bind existing UI references and refresh values; only data rows and transient content may be instantiated at runtime.

**Tech Stack:** Unity 2022.3.62f3c1, UnityEngine.UI, Unity Test Framework, EditMode tests under `UnderwaterGliderTwin/Assets/Tests/EditMode`, PlayMode tests under `UnderwaterGliderTwin/Assets/Tests/PlayMode`, editor asset generation under `UnderwaterGliderTwin/Assets/Editor`.

## Global Constraints

- `TwinBootstrap` only references `RuntimeUiRoot`; it does not instantiate the same long-lived UI Prefabs.
- `WelcomeBootstrap` uses serialized `WelcomeCanvas` references and does not create the welcome UI in production.
- Long-lived UI cannot call `FindObjectOfType<Canvas>()` to select a root Canvas.
- `ClearRuntimeUi()` cannot destroy Prefab-owned child objects; it may only clear named dynamic containers.
- `allowRuntimeFallback` is for migration and development verification only, defaults to disabled for production, and logs `[UI Fallback] Runtime-generated UI is active: <PanelName>` when used.
- Static labels, buttons, input visuals, panel backgrounds, and modal containers are Prefab or scene-owned.
- Live values, alarm state, progress, interactability, input text, and dynamic row contents are script-owned.
- Dynamic repeated rows use a `RowTemplate` under a Prefab-owned content container.
- Prefab edits are made in Prefab Mode; scenes keep only necessary instance overrides.
- Key Prefab object paths are stable and covered by reference tests.
- Scene UI panels must be actual Prefab instances. Builders must create temporary panel roots, save Prefab assets, delete temporary roots, instantiate Prefabs with `PrefabUtility.InstantiatePrefab`, and parent those instances under `RuntimeCanvas`.
- `UiFactory.EnsureCanvas(...)` cannot call `Object.FindObjectOfType<Canvas>()`. Fallback may use only an explicitly supplied fallback Canvas or a Canvas found on the provided parent/ancestor chain.
- Tests must not destroy every `GameObject` in the loaded scene. Each test creates a private root container and only destroys that root.
- `RuntimeUiRoot.ValidateReferences()` must report duplicate Canvas, duplicate EventSystem, and duplicate long-lived UI objects.
- Reference validation is staged. Tasks 2-9 run with `RuntimeUiValidationProfile.BootstrapOnly` or `EnabledPanels`; Task 10 switches production to `Strict` after all Prefab references exist.
- `EnabledPanels` validation uses an explicit serialized `RuntimeUiPanelFlags enabledPanelValidationMask`; it must not infer validation scope from `GameObject.activeInHierarchy`.
- When `allowRuntimeFallback` is enabled, missing UI references are logged and the bootstrap continues through the fallback path; when fallback is disabled, blocking reference errors stop binding.
- Required and optional UI references must be explicit. Missing required controls block strict binding; missing optional/decorative controls are marked with `[OptionalUiReference]`, skipped by blocking validation, and cannot disable the whole UI.
- `RuntimeUiFallback.AllowRuntimeFallback` must be explicitly set during each bootstrap and restored to false on scene destroy or bootstrap cleanup.
- `EditableUiSceneBuilder` must be idempotent: repeated execution cannot create duplicate Canvas, EventSystem, Prefab instances, or root panels.
- Acceptance includes runtime screenshots at 1920x1080 and 1366x768, plus proof that a manual Prefab visual edit appears at runtime without code changes.
- `EditableUiSceneBuilder` must check for unsaved scene changes before overwriting `Welcome.unity` or `Main.unity`; in batchmode it fails with a clear error instead of silently discarding user edits.
- Dynamic row templates use binding components such as `OceanCurrentLayerRowView`; production code must not depend on long chains of string-based child `Find(...)` calls for row internals.
- `Welcome` builder methods must be idempotent and must backfill `WelcomeBootstrap` serialized references before saving the scene.
- RowTemplate components such as `OceanCurrentLayerRowView` must be included in root validation, not only validated when a row is instantiated at runtime.

---

## File Structure

- Create `UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceIssue.cs`: value type describing a missing or duplicate UI reference.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/IUiReferenceProvider.cs`: interface for reference validation providers.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceValidator.cs`: shared validation and object-path formatting.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiValidationProfile.cs`: staged validation profile and explicit panel validation flags for migration-safe binding.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/OptionalUiReferenceAttribute.cs`: marker for non-blocking optional UI fields.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`: serialized owner for `RuntimeCanvas`, long-lived panels, `ModalRoot`, drawer roots, and row templates.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`: grouped references used by view `Bind(...)` methods.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiFallback.cs`: single explicit switch for migration-only runtime generation.
- Create `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTestObjectScope.cs`: EditMode-only helper that tracks and destroys only objects created by a test.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`: restrict Canvas discovery and add fallback logging.
- Modify `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`: bind `RuntimeUiRoot` and call view `Bind(...)` methods.
- Modify `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/WelcomeBootstrap.cs`: bind serialized welcome references and remove production UI creation.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`: add `Bind(DashboardPanelRefs, PlaybackController, PredictionController)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs`: add `Bind(StatusPanelRefs, PlaybackController, AlarmEvaluator, TwinLogger, PredictionController)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`: add `Bind(OceanToolbarRefs, TwinCameraController, TrajectoryView)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`: add `Bind(PlaybackControlsRefs refs, PlaybackController playbackController, TwinCameraController cameraController, UnderwaterEnvironmentBuilder environmentBuilder, TrajectoryView trajectoryView, Action onExitRequested = null, Func<string> onScreenshotRequested = null, Action onMissionViewRequested = null)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView*.cs`: add grouped refs, restrict `ClearRuntimeUi()`, and migrate drawers/modals to Prefab-owned containers.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCurrentLayerRowView.cs`: row binding component for ocean current dynamic rows.
- Create `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`: editor utility that creates `Assets/UI`, UI Prefabs, and scene hierarchy using Unity APIs.
- Create or modify `UnderwaterGliderTwin/Assets/Scenes/Welcome.unity`: add visible editable welcome UI.
- Create or modify `UnderwaterGliderTwin/Assets/Scenes/Main.unity`: add visible editable `RuntimeUiRoot` hierarchy.
- Create `UnderwaterGliderTwin/Assets/UI/Images/.gitkeep`: image asset folder marker.
- Create `UnderwaterGliderTwin/Assets/UI/Prefabs/*.prefab`: editable long-lived UI Prefabs.
- Create `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`: reference, fallback, and structure tests.
- Create `UnderwaterGliderTwin/Assets/Tests/PlayMode/UnderwaterGliderTwin.PlayModeTests.asmdef`: PlayMode test assembly.
- Create `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`: button and runtime refresh tests.

### Test Commands

Use this command for EditMode verification:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' -batchmode -projectPath 'E:\upan\digital twin\UnderwaterGliderTwin' -runTests -testPlatform EditMode -testResults 'E:\upan\digital twin\TestResults-EditMode.xml' -quit
```

Use this command for PlayMode verification:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' -batchmode -projectPath 'E:\upan\digital twin\UnderwaterGliderTwin' -runTests -testPlatform PlayMode -testResults 'E:\upan\digital twin\TestResults-PlayMode.xml' -quit
```

---

### Task 1: Reference Validation Infrastructure

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceIssue.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/IUiReferenceProvider.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceValidator.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiValidationProfile.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/OptionalUiReferenceAttribute.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiFallback.cs`
- Create: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTestObjectScope.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`

**Interfaces:**
- Produces: `UiReferenceIssue(string prefabName, string objectPath, string fieldName, string message)`
- Produces: `IUiReferenceProvider.CollectReferenceIssues(List<UiReferenceIssue> issues)`
- Produces: `IUiReferenceGroup.CollectReferenceIssues(Component owner, string prefabName, string groupPath, List<UiReferenceIssue> issues)`
- Produces: `UiReferenceValidator.Require(Object value, Component owner, string prefabName, string fieldName, List<UiReferenceIssue> issues)`
- Produces: `UiReferenceValidator.RequireFields(object references, Component owner, string prefabName, string groupPath, List<UiReferenceIssue> issues)`
- Produces: `UiReferenceValidator.GetPath(Transform transform)`
- Produces: `RuntimeUiValidationProfile.BootstrapOnly`, `.EnabledPanels`, `.Strict`
- Produces: `RuntimeUiPanelFlags.None`, `.Dashboard`, `.Status`, `.DataInput`, `.Playback`, `.OceanToolbar`, `.All`
- Produces: `[OptionalUiReference]` for decorative or migration-only fields that should not block binding
- Produces: `RuntimeUiFallback.AllowRuntimeFallback`
- Produces: `RuntimeUiFallback.LogFallback(string panelName)`
- Produces: `RuntimeUiFallback.Reset()`
- Produces: `UiTestObjectScope.CreateRoot(string name)`

- [ ] **Step 1: Write failing tests for missing-reference reports**

Add this test class:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

            UiReferenceValidator.Require(null, child.AddComponent<Text>(), "DashboardPanel.prefab", "DepthValue", issues);

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
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command above. Expected: compile errors for missing `UiReferenceIssue`, `UiReferenceValidator`, and `RuntimeUiFallback`.

- [ ] **Step 3: Implement validation types**

Create `UiReferenceIssue.cs`:

```csharp
namespace UnderwaterGliderTwin.UI
{
    public readonly struct UiReferenceIssue
    {
        public UiReferenceIssue(string prefabName, string objectPath, string fieldName, string message)
        {
            PrefabName = prefabName;
            ObjectPath = objectPath;
            FieldName = fieldName;
            Message = message;
        }

        public string PrefabName { get; }
        public string ObjectPath { get; }
        public string FieldName { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"{PrefabName}: {ObjectPath} -> {FieldName}: {Message}";
        }
    }
}
```

Create `IUiReferenceProvider.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public interface IUiReferenceProvider
    {
        void CollectReferenceIssues(List<UiReferenceIssue> issues);
    }

    public interface IUiReferenceGroup
    {
        void CollectReferenceIssues(Component owner, string prefabName, string groupPath, List<UiReferenceIssue> issues);
    }
}
```

Create `RuntimeUiValidationProfile.cs`:

```csharp
namespace UnderwaterGliderTwin.UI
{
    public enum RuntimeUiValidationProfile
    {
        BootstrapOnly,
        EnabledPanels,
        Strict
    }

    [System.Flags]
    public enum RuntimeUiPanelFlags
    {
        None = 0,
        Dashboard = 1 << 0,
        Status = 1 << 1,
        DataInput = 1 << 2,
        Playback = 1 << 3,
        OceanToolbar = 1 << 4,
        All = Dashboard | Status | DataInput | Playback | OceanToolbar
    }
}
```

Create `OptionalUiReferenceAttribute.cs`:

```csharp
using System;

namespace UnderwaterGliderTwin.UI
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalUiReferenceAttribute : Attribute
    {
    }
}
```

Create `UiReferenceValidator.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public static class UiReferenceValidator
    {
        public static void Require(Object value, Component owner, string prefabName, string fieldName, List<UiReferenceIssue> issues)
        {
            if (value != null)
            {
                return;
            }

            var safePrefabName = string.IsNullOrWhiteSpace(prefabName)
                ? (owner != null ? owner.gameObject.name : "<unknown ui>")
                : prefabName;
            var safeFieldName = string.IsNullOrWhiteSpace(fieldName) ? "<unnamed field>" : fieldName;

            issues.Add(new UiReferenceIssue(
                safePrefabName,
                owner != null ? GetPath(owner.transform) : "<missing owner>",
                safeFieldName,
                "Required UI reference is missing."));
        }

        public static void RequireFields(object references, Component owner, string prefabName, string groupPath, List<UiReferenceIssue> issues)
        {
            if (references == null)
            {
                issues.Add(new UiReferenceIssue(string.IsNullOrWhiteSpace(prefabName) ? "<unknown ui>" : prefabName, owner != null ? GetPath(owner.transform) : "<missing owner>", string.IsNullOrWhiteSpace(groupPath) ? "<unnamed group>" : groupPath, "Required UI reference group is missing."));
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public;
            foreach (var field in references.GetType().GetFields(flags))
            {
                if (field.GetCustomAttribute<OptionalUiReferenceAttribute>() != null)
                {
                    continue;
                }

                var fieldPath = string.IsNullOrEmpty(groupPath) ? field.Name : groupPath + "." + field.Name;
                var value = field.GetValue(references);
                if (typeof(Object).IsAssignableFrom(field.FieldType))
                {
                    Require(value as Object, owner, prefabName, fieldPath, issues);
                    continue;
                }

                if (typeof(IUiReferenceGroup).IsAssignableFrom(field.FieldType))
                {
                    var group = value as IUiReferenceGroup;
                    if (group == null)
                    {
                        issues.Add(new UiReferenceIssue(string.IsNullOrWhiteSpace(prefabName) ? "<unknown ui>" : prefabName, owner != null ? GetPath(owner.transform) : "<missing owner>", fieldPath, "Required UI reference group is missing."));
                    }
                    else
                    {
                        group.CollectReferenceIssues(owner, prefabName, fieldPath, issues);
                    }
                }
                else if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                {
                    RequireFields(value, owner, prefabName, fieldPath, issues);
                }
            }
        }

        public static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing transform>";
            }

            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }
    }
}
```

Create `RuntimeUiFallback.cs`:

```csharp
using UnityEngine;

namespace UnderwaterGliderTwin.UI
{
    public static class RuntimeUiFallback
    {
        public static bool AllowRuntimeFallback { get; set; }

        public static void LogFallback(string panelName)
        {
            Debug.LogWarning($"[UI Fallback] Runtime-generated UI is active: {panelName}");
        }

        public static void Reset()
        {
            AllowRuntimeFallback = false;
        }
    }
}
```

Create `UiTestObjectScope.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class UiTestObjectScope : IDisposable
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        public GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            return root;
        }

        public void Dispose()
        {
            for (var index = roots.Count - 1; index >= 0; index--)
            {
                if (roots[index] != null)
                {
                    Object.DestroyImmediate(roots[index]);
                }
            }

            roots.Clear();
        }
    }
}
```

- [ ] **Step 4: Run EditMode tests**

Run the EditMode command above. Expected: new tests pass; existing UI tests still use runtime generation and may continue passing.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceIssue.cs UnderwaterGliderTwin/Assets/Scripts/UI/IUiReferenceProvider.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceValidator.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiValidationProfile.cs UnderwaterGliderTwin/Assets/Scripts/UI/OptionalUiReferenceAttribute.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiFallback.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTestObjectScope.cs UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs
git commit -m "feat: add UI reference validation infrastructure"
```

---

### Task 2: RuntimeUiRoot and Scene Ownership

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`

**Interfaces:**
- Consumes: `IUiReferenceProvider`, `UiReferenceValidator`, `UiReferenceIssue`
- Produces: `RuntimeUiRoot.RuntimeCanvas`, `RuntimeUiRoot.ModalRoot`, `RuntimeUiRoot.References`
- Produces: `RuntimeUiRoot.ValidateReferences(RuntimeUiValidationProfile profile = RuntimeUiValidationProfile.EnabledPanels)`
- Produces: `RuntimeUiReferences` grouped UI references for subsequent panel binding tasks

- [ ] **Step 1: Write failing test for root ownership**

Add to `EditableUiReferenceTests.cs`:

```csharp
[Test]
public void RuntimeUiRoot_RequiresCanvasAndModalRoot()
{
    var root = scope.CreateRoot("RuntimeUiRoot").AddComponent<RuntimeUiRoot>();
    var issues = root.ValidateReferences(RuntimeUiValidationProfile.BootstrapOnly);

    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("runtimeCanvas"));
    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("modalRoot"));
}

[Test]
public void RuntimeUiRoot_ReportsDuplicateEventSystemsAndLongLivedUi()
{
    scope.CreateRoot("EventSystemA").AddComponent<EventSystem>();
    scope.CreateRoot("EventSystemB").AddComponent<EventSystem>();
    scope.CreateRoot("DashboardPanel");
    scope.CreateRoot("DashboardPanel");
    var root = scope.CreateRoot("RuntimeUiRoot").AddComponent<RuntimeUiRoot>();

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
    var root = rootObject.AddComponent<RuntimeUiRoot>();
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
    var root = rootObject.AddComponent<RuntimeUiRoot>();
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
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command above. Expected: compile error for missing `RuntimeUiRoot`.

- [ ] **Step 3: Implement RuntimeUiRoot and grouped references**

Create `RuntimeUiReferences.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    [Serializable]
    public abstract class UiReferenceGroupBase : IUiReferenceGroup
    {
        public void CollectReferenceIssues(Component owner, string prefabName, string groupPath, List<UiReferenceIssue> issues)
        {
            UiReferenceValidator.RequireFields(this, owner, prefabName, groupPath, issues);
        }
    }

    [Serializable]
    public sealed class RuntimeUiReferences : UiReferenceGroupBase
    {
        public DashboardPanelRefs dashboard = new DashboardPanelRefs();
        public StatusPanelRefs status = new StatusPanelRefs();
        public DataInputPanelRefs dataInput = new DataInputPanelRefs();
        public PlaybackControlsRefs playback = new PlaybackControlsRefs();
        public OceanToolbarRefs oceanToolbar = new OceanToolbarRefs();
    }

    [Serializable]
    public sealed class DashboardPanelRefs : UiReferenceGroupBase
    {
        public RectTransform panel;
        public Button detailsButton;
        public GameObject navigationReferenceCard;
        public Text depthValue;
        public Text headingValue;
        public Text pitchValue;
        public Text rollValue;
        public Text yawValue;
        public Text batteryValue;
        public Text latitudeValue;
        public Text longitudeValue;
        public Text velocityXValue;
        public Text velocityYValue;
        public Text velocityZValue;
        public Text speedValue;
        public Text verticalSpeedValue;
        public Text horizontalSpeedValue;
        public Text missionTimeValue;
        public Text distanceValue;
        [OptionalUiReference] public Text predictionErrorValue;
        public Text oceanCurrentValue;
        public Text waterSpeedValue;
        public Text groundSpeedValue;
        public Text sideSlipValue;
        public Text netBuoyancyValue;
        public Text energyValue;
        public Text angleOfAttackValue;
        public Text liftForceValue;
        public Text dragForceValue;
        public Text angularRateValue;
        public Text hydrodynamicMomentValue;
        public Text inertiaValue;
        public Text pistonPositionValue;
        public Text controlSurfaceValue;
        public Text actuatorPowerValue;
        public Text dynamicsSummaryValue;
        public RectTransform advancedRowsRoot;
    }

    [Serializable]
    public sealed class StatusPanelRefs : UiReferenceGroupBase
    {
        public RectTransform panel;
        public Image alarmBackground;
        public Image progressFill;
        public Text missionValue;
        public Text modeValue;
        public Text stateValue;
        public Text segmentValue;
        public Text remainingDistanceValue;
        public Text etaValue;
        public Text predictionStatusValue;
        public Text batteryValue;
        public Text driftValue;
        public Text rmseValue;
        public Text maeValue;
        [OptionalUiReference] public Text confidenceValue;
        public Text predictionTimeValue;
        public Text engineeringValidationValue;
        public Text alarmValue;
        public Text missionHealthValue;
        public RectTransform predictionMetricRowsRoot;
        public RectTransform predictionMetricRowTemplate;
    }

    [Serializable]
    public sealed class PlaybackControlsRefs : UiReferenceGroupBase
    {
        public RectTransform panel;
        public Button playPauseButton;
        public Button reverseButton;
        public Button replayButton;
        public Button resetButton;
        public Button exportButton;
        public Button exitButton;
        public Button cameraFollowButton;
        public Button cameraGlobalButton;
        public Button cameraOrbitButton;
        public Button missionVolumeButton;
        public Toggle fogToggle;
        public Toggle particlesToggle;
        public Toggle trajectoryToggle;
        public Button speed05Button;
        public Button speed1Button;
        public Button speed2Button;
        public Button speed5Button;
        public Button speed10Button;
        public Slider progressSlider;
        public Text statusText;
    }

    [Serializable]
    public sealed class OceanToolbarRefs : UiReferenceGroupBase
    {
        public RectTransform viewportFrame;
        public RectTransform panel;
        public Text visibleArrowCount;
        public Button cameraFollowCommand;
        public Button cameraGlobalCommand;
        public Button cameraTopCommand;
        public Button cameraSideCommand;
        public Button cameraOrbitCommand;
        public Button cameraResetCommand;
    }
    [Serializable] public sealed class DataInputPanelRefs : UiReferenceGroupBase { public RectTransform panel; public MissionSectionRefs mission = new MissionSectionRefs(); public SimulationSectionRefs simulation = new SimulationSectionRefs(); public OceanSectionRefs ocean = new OceanSectionRefs(); public DynamicsSectionRefs dynamics = new DynamicsSectionRefs(); public PredictionSectionRefs prediction = new PredictionSectionRefs(); }
    [Serializable] public sealed class MissionSectionRefs : UiReferenceGroupBase { public InputField csvPathInput; public Button reloadCsvButton; }
    [Serializable] public sealed class SimulationSectionRefs : UiReferenceGroupBase { public Button applyButton; public RectTransform flightLegDrawer; }
    [Serializable] public sealed class OceanSectionRefs : UiReferenceGroupBase { public Button drawerButton; public RectTransform oceanCurrentDrawer; public RectTransform oceanLayerContent; public RectTransform oceanLayerRowTemplate; }
    [Serializable] public sealed class DynamicsSectionRefs : UiReferenceGroupBase { public InputField massInput; public InputField referenceAreaInput; }
    [Serializable] public sealed class PredictionSectionRefs : UiReferenceGroupBase { public Button physicsModelButton; public Button xgBoostModelButton; }
}
```

Create `RuntimeUiRoot.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
                issues.Add(new UiReferenceIssue("OceanCurrentLayerRow.prefab", UiReferenceValidator.GetPath(template), fieldName, "RowTemplate must include an IUiReferenceProvider such as OceanCurrentLayerRowView."));
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
            var all = FindObjectsOfType<T>(true);
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

            issues.Add(new UiReferenceIssue("Main.unity", UiReferenceValidator.GetPath(transform), fieldName, $"Expected exactly one {fieldName}, found {matches.Length}."));
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
                issues.Add(new UiReferenceIssue("Main.unity", UiReferenceValidator.GetPath(transform), name, $"Expected one long-lived UI object named {name}, found {count}."));
            }
        }
    }
}
```

- [ ] **Step 4: Modify TwinBootstrap to serialize RuntimeUiRoot**

Add fields:

```csharp
[SerializeField] private RuntimeUiRoot runtimeUiRoot;
[SerializeField] private bool allowRuntimeFallback;
[SerializeField] private bool strictUiValidation;
```

Before UI initialization, add:

```csharp
RuntimeUiFallback.AllowRuntimeFallback = allowRuntimeFallback;
var usePrefabUi = runtimeUiRoot != null;
if (runtimeUiRoot == null && !allowRuntimeFallback)
{
    throw new InvalidOperationException("RuntimeUiRoot is required. Assign Main/RuntimeUiRoot on TwinBootstrap.");
}
if (runtimeUiRoot != null)
{
    var validationProfile = strictUiValidation
        ? RuntimeUiValidationProfile.Strict
        : RuntimeUiValidationProfile.EnabledPanels;
    var uiIssues = runtimeUiRoot.ValidateReferences(validationProfile);
    if (uiIssues.Count > 0)
    {
        foreach (var issue in uiIssues)
        {
            Debug.LogError(issue.ToString(), runtimeUiRoot);
        }
        if (allowRuntimeFallback)
        {
            RuntimeUiFallback.LogFallback("RuntimeUiRoot validation failed; using runtime-created UI");
            usePrefabUi = false;
        }
        else
        {
            enabled = false;
            return;
        }
    }
}
if (usePrefabUi && !runtimeUiRoot.TryEnsureSingleEventSystem())
{
    enabled = false;
    return;
}
```

Do not set `strictUiValidation` to true until Task 10. During Tasks 2-9, the project remains runnable because missing references for panels that have not been migrated yet do not disable `TwinBootstrap`. If `allowRuntimeFallback` is true, reference errors must always be visible in the Console but must not block the fallback path.

During migration, maintain `RuntimeUiRoot.enabledPanelValidationMask` explicitly:

```text
Task 2-5: None
Task 6: Dashboard | Status | Playback | OceanToolbar
Task 7-9: Dashboard | Status | Playback | OceanToolbar | DataInput
Task 10: Strict validation ignores the mask and validates all required references
```

Do not use `panel.gameObject.activeInHierarchy` to decide whether a panel requires validation; hidden drawers, panels, or first-run disabled controls may still be opened by runtime behavior.

Remove the older stop-on-any-issue shape:

```csharp
if (runtimeUiRoot != null)
{
    var uiIssues = runtimeUiRoot.ValidateReferences();
    if (uiIssues.Count > 0)
    {
        enabled = false;
        return;
    }
}
```

In `TwinBootstrap.OnDestroy()`, add:

```csharp
RuntimeUiFallback.Reset();
```

Keep existing runtime view creation in place until each view has a `Bind(...)` path. Gate the old path behind `allowRuntimeFallback`.

- [ ] **Step 5: Run EditMode tests**

Run the EditMode command above. Expected: root validation test passes; existing tests still pass when they explicitly enable fallback in the next task.

- [ ] **Step 6: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs
git commit -m "feat: add RuntimeUiRoot ownership model"
```

---

### Task 3: Fallback Guard, UiFactory Restriction, and Dynamic Cleanup Guard

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`

**Interfaces:**
- Consumes: `RuntimeUiFallback.AllowRuntimeFallback`
- Produces: `UiFactory.EnsureCanvas(Transform parent, string fallbackPanelName, Canvas explicitFallbackCanvas = null)`
- Produces: `DataInputView.ClearDynamicRuntimeUi()`

- [ ] **Step 1: Write failing fallback tests**

Add:

```csharp
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

    var canvas = UiFactory.EnsureCanvas(owner, "DashboardPanel", null);

    Assert.That(canvas, Is.Not.SameAs(unrelated));
    Assert.That(canvas.transform.parent, Is.EqualTo(owner));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: compile error or assertion failure because `EnsureCanvas(Transform,string)` does not exist.

- [ ] **Step 3: Restrict UiFactory canvas creation**

Add an overload and route the old overload through it:

```csharp
public static Canvas EnsureCanvas(Transform parent)
{
    return EnsureCanvas(parent, parent != null ? parent.name : "UnknownPanel", null);
}

public static Canvas EnsureCanvas(Transform parent, string fallbackPanelName, Canvas explicitFallbackCanvas = null)
{
    EnsureEventSystem();
    if (explicitFallbackCanvas != null)
    {
        return explicitFallbackCanvas;
    }

    var parentCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
    if (parentCanvas != null)
    {
        return parentCanvas;
    }

    if (!RuntimeUiFallback.AllowRuntimeFallback)
    {
        throw new System.InvalidOperationException($"Runtime UI fallback is disabled. Missing RuntimeCanvas for {fallbackPanelName}.");
    }

    RuntimeUiFallback.LogFallback(fallbackPanelName);
    var canvasObject = new GameObject("RuntimeCanvas");
    canvasObject.transform.SetParent(parent, false);
    var canvas = canvasObject.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 10;
    var scaler = canvasObject.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920f, 1080f);
    scaler.matchWidthOrHeight = 0.5f;
    canvasObject.AddComponent<GraphicRaycaster>();
    return canvas;
}
```

- [ ] **Step 4: Update old EditMode tests to opt into fallback and scoped cleanup**

In `UiTests.TearDown()`, add:

```csharp
RuntimeUiFallback.Reset();
```

Replace broad cleanup of every scene `GameObject` with a tracked root container for new tests. For existing tests that still create scene-wide objects, keep cleanup only inside tests that do not open saved scenes; any test that opens `Welcome.unity` or `Main.unity` must restore the previous scene and cannot call `Object.FindObjectsOfType<GameObject>()` teardown.

At the start of tests that call old `Initialize(...)` runtime-creation paths, add:

```csharp
RuntimeUiFallback.AllowRuntimeFallback = true;
```

Apply this to tests that instantiate `DashboardView`, `DataInputView`, `StatusPanelView`, `OceanCommandToolbarView`, and `PlaybackControlsView` without a scene-owned root.

- [ ] **Step 5: Restrict DataInputView cleanup**

Replace `ClearRuntimeUi()` calls with `ClearDynamicRuntimeUi()`. Implement:

```csharp
private void ClearDynamicRuntimeUi()
{
    modelButtons.Clear();
    bottomDrawerContent = null;
    bottomDrawerViewport = null;
    bottomDrawerScrollRect = null;
    bottomDrawerToggleButton = null;
    oceanCurrentModalCanvas = null;
    ClearChildren(dynamicContentRoot);
}

private static void ClearChildren(Transform root)
{
    if (root == null)
    {
        return;
    }

    for (var index = root.childCount - 1; index >= 0; index--)
    {
        var child = root.GetChild(index).gameObject;
        if (Application.isPlaying)
        {
            Destroy(child);
        }
        else
        {
            DestroyImmediate(child);
        }
    }
}
```

Add `private Transform dynamicContentRoot;` and assign it to the old runtime panel only in fallback mode. Prefab-bound mode assigns it to row/content containers supplied by refs.

- [ ] **Step 6: Run EditMode tests**

Run the EditMode command. Expected: fallback guard test passes; old tests pass after opting into fallback.

- [ ] **Step 7: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs
git commit -m "feat: guard runtime UI fallback"
```

---

### Task 4: Editable Welcome Scene

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/WelcomeBootstrap.cs`
- Create: `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scenes/Welcome.unity`
- Create: `UnderwaterGliderTwin/Assets/UI/Images/.gitkeep`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`

**Interfaces:**
- Produces: `WelcomeBootstrap.ValidateReferences()`
- Produces: serialized fields `welcomeCanvas`, `csvInput`, `status`, `confirmCsvButton`, `startCsvButton`, `simulationButton`
- Produces: editor method `EditableUiSceneBuilder.BuildWelcomeScene()`

- [ ] **Step 1: Write failing welcome binding test**

Add:

```csharp
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
    var previous = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
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
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
        }
    }
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
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: compile error because `ValidateReferences()` does not exist.

- [ ] **Step 3: Add serialized welcome references**

In `WelcomeBootstrap`, add:

```csharp
[SerializeField] private Canvas welcomeCanvas;
[SerializeField] private InputField csvInput;
[SerializeField] private Text status;
[SerializeField] private Button confirmCsvButton;
[SerializeField] private Button startCsvButton;
[SerializeField] private Button simulationButton;
[SerializeField] private bool allowRuntimeFallback;
```

Add:

```csharp
public List<UiReferenceIssue> ValidateReferences()
{
    var issues = new List<UiReferenceIssue>();
    UiReferenceValidator.Require(welcomeCanvas, this, "Welcome.unity", "welcomeCanvas", issues);
    UiReferenceValidator.Require(csvInput, this, "Welcome.unity", "csvInput", issues);
    UiReferenceValidator.Require(status, this, "Welcome.unity", "status", issues);
    UiReferenceValidator.Require(confirmCsvButton, this, "Welcome.unity", "confirmCsvButton", issues);
    UiReferenceValidator.Require(startCsvButton, this, "Welcome.unity", "startCsvButton", issues);
    UiReferenceValidator.Require(simulationButton, this, "Welcome.unity", "simulationButton", issues);
    return issues;
}
```

In `Awake()`, replace direct `BuildUi(lastCsv)` with:

```csharp
var issues = ValidateReferences();
if (issues.Count > 0)
{
    if (!allowRuntimeFallback)
    {
        foreach (var issue in issues)
        {
            Debug.LogError(issue.ToString(), this);
        }
        enabled = false;
        return;
    }

    RuntimeUiFallback.AllowRuntimeFallback = true;
    RuntimeUiFallback.LogFallback("WelcomeCanvas");
    BuildUi(lastCsv);
}
else
{
    BindUi(lastCsv);
}
```

Add `BindUi(string initialCsv)` to assign text and button listeners.

In `WelcomeBootstrap.OnDestroy()`, add:

```csharp
RuntimeUiFallback.Reset();
```

- [ ] **Step 4: Add editor builder for welcome hierarchy**

Create `EditableUiSceneBuilder.cs` with a menu item and callable method:

```csharp
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnderwaterGliderTwin.Bootstrap;

namespace UnderwaterGliderTwin.Editor
{
    public static class EditableUiSceneBuilder
    {
        [MenuItem("UnderwaterGliderTwin/UI/Rebuild Welcome UI")]
        public static void BuildWelcomeScene()
        {
            EnsureNoUnsavedSceneChanges("Welcome UI rebuild");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Welcome.unity");
            var bootstrapObject = GameObject.Find("WelcomeBootstrap") ?? new GameObject("WelcomeBootstrap");
            var bootstrap = bootstrapObject.GetComponent<WelcomeBootstrap>() ?? bootstrapObject.AddComponent<WelcomeBootstrap>();
            var canvasObject = GameObject.Find("WelcomeCanvas") ?? new GameObject("WelcomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            EnsureEventSystem(scene);
            var refs = CreateWelcomeChildren(canvasObject.transform);
            AssignWelcomeReferences(bootstrap, canvas, refs);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureNoUnsavedSceneChanges(string operationName)
        {
            if (Application.isBatchMode && EditorSceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException(operationName + " cancelled because the active scene has unsaved changes. Save or revert the scene before running the batch builder.");
            }
            if (Application.isBatchMode)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new InvalidOperationException(operationName + " cancelled because there are unsaved scene changes.");
            }
        }

        private static void EnsureEventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var eventSystem in Object.FindObjectsOfType<EventSystem>(true))
            {
                if (eventSystem.gameObject.scene == scene)
                {
                    return;
                }
            }

            var created = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, scene);
        }

        private readonly struct WelcomeUiBuildRefs
        {
            public WelcomeUiBuildRefs(InputField csvInput, Text status, Button confirmCsvButton, Button startCsvButton, Button simulationButton)
            {
                CsvInput = csvInput;
                Status = status;
                ConfirmCsvButton = confirmCsvButton;
                StartCsvButton = startCsvButton;
                SimulationButton = simulationButton;
            }

            public InputField CsvInput { get; }
            public Text Status { get; }
            public Button ConfirmCsvButton { get; }
            public Button StartCsvButton { get; }
            public Button SimulationButton { get; }
        }

        private static WelcomeUiBuildRefs CreateWelcomeChildren(Transform canvas)
        {
            GetOrCreateImage(canvas, "BackgroundImage", Vector2.zero, Vector2.one, new Color(0.025f, 0.12f, 0.18f));
            var panel = GetOrCreateImage(canvas, "LaunchPanel", new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), new Color(0.03f, 0.12f, 0.22f, 0.98f));
            GetOrCreateText(panel.transform, "TitleText", "水下滑翔机数字孪生", 34, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f));
            GetOrCreateText(panel.transform, "DescriptionText", "CSV 遥测回放、参数化任务仿真与实验性短时预测", 18, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.83f));
            var csvInput = GetOrCreateInput(panel.transform, "CsvPathInput", new Vector2(0.08f, 0.58f), new Vector2(0.72f, 0.67f));
            var confirm = GetOrCreateButton(panel.transform, "ConfirmCsvButton", "确认 CSV 路径", new Vector2(0.74f, 0.58f), new Vector2(0.92f, 0.67f));
            var start = GetOrCreateButton(panel.transform, "StartCsvButton", "开始上次 / 默认 CSV", new Vector2(0.08f, 0.43f), new Vector2(0.48f, 0.53f));
            var simulation = GetOrCreateButton(panel.transform, "SimulationButton", "进入仿真模式", new Vector2(0.52f, 0.43f), new Vector2(0.92f, 0.53f));
            var status = GetOrCreateText(panel.transform, "LaunchStatusText", string.Empty, 15, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.30f));
            return new WelcomeUiBuildRefs(csvInput, status, confirm, start, simulation);
        }

        private static void AssignWelcomeReferences(WelcomeBootstrap bootstrap, Canvas canvas, WelcomeUiBuildRefs refs)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("welcomeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("csvInput").objectReferenceValue = refs.CsvInput;
            serialized.FindProperty("status").objectReferenceValue = refs.Status;
            serialized.FindProperty("confirmCsvButton").objectReferenceValue = refs.ConfirmCsvButton;
            serialized.FindProperty("startCsvButton").objectReferenceValue = refs.StartCsvButton;
            serialized.FindProperty("simulationButton").objectReferenceValue = refs.SimulationButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }
    }
}
```

Use helper methods equivalent to the current welcome `CreateImage`, `CreateText`, `CreateButton`, and `CreateInput`, but name them `GetOrCreateImage`, `GetOrCreateText`, `GetOrCreateButton`, and `GetOrCreateInput`. Each helper first searches only direct children of the supplied parent for the stable object name, reuses the existing object if present, ensures required components exist, then reapplies anchors, color, text, and size.

- [ ] **Step 5: Run builder in Unity batchmode**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' -batchmode -projectPath 'E:\upan\digital twin\UnderwaterGliderTwin' -executeMethod UnderwaterGliderTwin.Editor.EditableUiSceneBuilder.BuildWelcomeScene -quit
```

Expected: `Welcome.unity` contains `WelcomeCanvas`, `LaunchPanel`, buttons, input field, and text objects in Hierarchy.

- [ ] **Step 6: Run EditMode tests**

Run the EditMode command. Expected: welcome reference tests pass.

- [ ] **Step 7: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Bootstrap/WelcomeBootstrap.cs UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs UnderwaterGliderTwin/Assets/Scenes/Welcome.unity UnderwaterGliderTwin/Assets/UI/Images/.gitkeep UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs
git commit -m "feat: make welcome UI editable"
```

---

### Task 5: Main Scene Root and Base Panel Prefabs

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scenes/Main.unity`
- Create: `UnderwaterGliderTwin/Assets/UI/Prefabs/DashboardPanel.prefab`
- Create: `UnderwaterGliderTwin/Assets/UI/Prefabs/StatusPanel.prefab`
- Create: `UnderwaterGliderTwin/Assets/UI/Prefabs/DataInputPanel.prefab`
- Create: `UnderwaterGliderTwin/Assets/UI/Prefabs/PlaybackControlsPanel.prefab`
- Create: `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCommandToolbar.prefab`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`

**Interfaces:**
- Consumes: `RuntimeUiRoot`, `RuntimeUiReferences`
- Produces: stable scene hierarchy under `RuntimeUiRoot/RuntimeCanvas`

- [ ] **Step 1: Write failing structure test**

Add:

```csharp
[Test]
public void MainScene_HasEditableRuntimeUiHierarchy()
{
    var previous = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
    try
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

        Assert.That(GameObject.Find("RuntimeUiRoot"), Is.Not.Null);
        Assert.That(GameObject.Find("RuntimeCanvas"), Is.Not.Null);
        Assert.That(GameObject.Find("CommandCenterHeader"), Is.Not.Null);
        Assert.That(GameObject.Find("DashboardPanel"), Is.Not.Null);
        Assert.That(GameObject.Find("StatusPanel"), Is.Not.Null);
        Assert.That(GameObject.Find("PlaybackControlsPanel"), Is.Not.Null);
        Assert.That(GameObject.Find("OceanCommandToolbar"), Is.Not.Null);
        Assert.That(GameObject.Find("ModalRoot"), Is.Not.Null);
    }
    finally
    {
        if (!string.IsNullOrEmpty(previous))
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
        }
    }
}

[Test]
public void MainScene_LongLivedPanelsArePrefabInstances()
{
    var previous = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
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
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: assertion failure because `Main.unity` has no `RuntimeUiRoot`.

- [ ] **Step 3: Extend editor builder**

Add `BuildMainScene()` menu item and method. It calls `EnsureNoUnsavedSceneChanges("Main UI rebuild")`, opens `Assets/Scenes/Main.unity`, creates or reuses `RuntimeUiRoot`, creates or reuses `RuntimeCanvas` with `CanvasScaler` reference resolution `1920x1080`, ensures the required child objects exist once, adds `RuntimeUiRoot` component, assigns serialized fields through `SerializedObject`, and saves the scene.

When `BuildMainScene()` first creates the root in Task 5, assign `enabledPanelValidationMask = RuntimeUiPanelFlags.None`. Task 6 updates the scene value to `Dashboard | Status | Playback | OceanToolbar`; Task 7 updates it to include `DataInput`.

The hierarchy must be exactly:

```text
RuntimeUiRoot
└── RuntimeCanvas
    ├── CommandCenterHeader
    ├── DashboardPanel
    ├── StatusPanel
    ├── DataInputPanel
    ├── PlaybackControlsPanel
    ├── OceanCommandToolbar
    └── ModalRoot
```

- [ ] **Step 4: Create base Prefabs**

Use the builder to create Prefab assets with this exact flow for each long-lived panel:

```text
Create temporary panel root outside RuntimeCanvas
Save it with PrefabUtility.SaveAsPrefabAsset
Destroy the temporary panel root
Delete any existing non-Prefab scene object with the same panel name
Instantiate the saved Prefab with PrefabUtility.InstantiatePrefab
Parent the Prefab instance under RuntimeCanvas
Reset RectTransform anchors, position, size, and sibling order
```

Keep these stable names:

```text
DashboardPanel.prefab
StatusPanel.prefab
DataInputPanel.prefab
PlaybackControlsPanel.prefab
OceanCommandToolbar.prefab
```

The builder must be idempotent. Running `BuildMainScene()` twice must still leave exactly one `RuntimeUiRoot`, one `RuntimeCanvas`, one `EventSystem`, one `ModalRoot`, and one connected Prefab instance for each panel.

- [ ] **Step 5: Run builder**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' -batchmode -projectPath 'E:\upan\digital twin\UnderwaterGliderTwin' -executeMethod UnderwaterGliderTwin.Editor.EditableUiSceneBuilder.BuildMainScene -quit
```

- [ ] **Step 6: Run EditMode tests**

Run the EditMode command. Expected: structure test passes; no duplicate Canvas or EventSystem appears in `Main.unity`.

- [ ] **Step 7: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs UnderwaterGliderTwin/Assets/Scenes/Main.unity UnderwaterGliderTwin/Assets/UI/Prefabs UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs
git commit -m "feat: add editable main UI root"
```

---

### Task 6: Bind Dashboard, Status, Toolbar, and Playback Panels

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Consumes: `RuntimeUiReferences.dashboard`, `.status`, `.oceanToolbar`, `.playback`
- Produces: `Bind(...)` methods for four panels

- [ ] **Step 1: Add failing Bind tests**

For each view, add one test that creates the minimal refs object, calls `Bind(...)`, and verifies existing behavior. Example for dashboard:

```csharp
[Test]
public void DashboardView_BindsExistingTelemetryText()
{
    var playback = CreatePlayback(Frames(2));
    var prediction = CreatePrediction(playback, Frames(2));
    var root = new GameObject("DashboardPanel").AddComponent<RectTransform>();
    var depth = CreateText(root.transform, "DepthValue");
    var battery = CreateText(root.transform, "BatteryValue");
    var refs = new DashboardPanelRefs { panel = root, depthValue = depth, batteryValue = battery };
    var dashboard = new GameObject("Dashboard").AddComponent<DashboardView>();

    dashboard.Bind(refs, playback, prediction);

    Assert.That(depth.text, Is.EqualTo("10.0 m"));
    Assert.That(battery.text, Is.EqualTo("15 %"));
}
```

Use existing `CreatePlayback`, `CreatePrediction`, and a local `CreateText(Transform,string)` helper in `UiTests`.

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: compile errors for missing `Bind(...)` methods.

- [ ] **Step 3: Add Bind methods**

Each `Bind(...)` method assigns serialized UI fields from refs, subscribes to existing model events, and calls the same refresh methods that `Initialize(...)` uses. Keep old `Initialize(...)` as fallback-only path during migration.

For `DashboardView`, first extract a real refresh entry point from the existing private `OnFrameChanged(...)` path:

```csharp
internal void RefreshFromCurrentFrame()
{
    if (playback == null || playback.Model == null)
    {
        return;
    }

    OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
    RefreshDetails();
}
```

Example shape:

```csharp
public void Bind(DashboardPanelRefs refs, PlaybackController playbackController, PredictionController predictionController)
{
    this.playbackController = playbackController;
    this.predictionController = predictionController;
    panel = refs.panel;
    depthValue = refs.depthValue;
    batteryValue = refs.batteryValue;
    playback.FrameChangedWithReason += OnFrameChanged;
    RefreshFromCurrentFrame();
}
```

- [ ] **Step 4: Update TwinBootstrap to call Bind for base panels**

Replace fallback creation for these four panels with:

```csharp
if (runtimeUiRoot != null)
{
    var refs = runtimeUiRoot.References;
    gameObject.AddComponent<DashboardView>().Bind(refs.dashboard, PlaybackController, prediction);
    gameObject.AddComponent<StatusPanelView>().Bind(refs.status, PlaybackController, AlarmEvaluator, Logger, prediction);
    gameObject.AddComponent<OceanCommandToolbarView>().Bind(refs.oceanToolbar, cameraController, trajectoryView);
    Action missionViewRequested = () =>
    {
        if (RuntimeDataSourceState.CurrentMode != RuntimeDataSourceMode.Simulation)
        {
            cameraController.SetMode(CameraMode.Global);
            trajectoryView.SetCameraMode(CameraMode.Global);
            return;
        }

        cameraController.SetMissionVolumeView(RuntimeDataSourceState.SimulationProfile.TargetDepthM, missionHorizontalExtents, missionDepthScale);
        trajectoryView.SetCameraMode(CameraMode.Global);
    };
    gameObject.AddComponent<PlaybackControlsView>().Bind(refs.playback, PlaybackController, cameraController, environment, trajectoryView, onScreenshotRequested: screenshotCapture.CaptureManual, onMissionViewRequested: missionViewRequested);
}
else if (allowRuntimeFallback)
{
    RuntimeUiFallback.LogFallback("Main base panels");
    // existing Initialize path
}
```

Keep the existing mission-view lambda body unchanged.

After the four base panels are bound and their Prefab references are assigned, update `RuntimeUiRoot.enabledPanelValidationMask` in `Assets/Scenes/Main.unity` to:

```csharp
RuntimeUiPanelFlags.Dashboard | RuntimeUiPanelFlags.Status | RuntimeUiPanelFlags.Playback | RuntimeUiPanelFlags.OceanToolbar
```

- [ ] **Step 5: Run EditMode tests**

Run the EditMode command. Expected: new Bind tests pass; existing fallback tests pass.

- [ ] **Step 6: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
git commit -m "feat: bind base UI panels from editable refs"
```

---

### Task 7: Bind DataInput Sections and Restrict Dynamic Lists

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.MissionSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.SimulationSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.DynamicsSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCurrentLayerRowView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Consumes: `DataInputPanelRefs`
- Produces: `DataInputView.Bind(DataInputPanelRefs refs, string csvPath, SimulationProfile profile, PredictionController prediction, Action<string> reloadCsv, Action<SimulationProfile> reloadSimulation, Action<OceanCurrentProfile> applyOceanCurrent)`
- Produces: dynamic row creation under `refs.ocean.oceanLayerContent` using `refs.ocean.oceanLayerRowTemplate`
- Produces: `OceanCurrentLayerRowView.Bind(int index, OceanCurrentLayer layer, Action<int> onEdit, Action<int> onRemove)`

- [ ] **Step 1: Generate the required DataInput reference inventory**

Before writing `Bind(...)`, inventory every current long-lived UI field or stable object created in these files:

```powershell
Select-String -Path 'UnderwaterGliderTwin\Assets\Scripts\UI\DataInputView*.cs' -Pattern 'private (Text|Button|InputField|Slider|Toggle|RectTransform|ScrollRect|Image)|UiFactory\.(Text|Button|PrimaryButton|InputField|Slider|Toggle|Panel|CommandPanel)\("' 
```

Update `RuntimeUiReferences.cs` so `MissionSectionRefs`, `SimulationSectionRefs`, `OceanSectionRefs`, `DynamicsSectionRefs`, and `PredictionSectionRefs` contain a field for every long-lived UI control from that inventory. The migration is not complete until all existing buttons, inputs, sliders, status texts, drawer roots, content roots, and row templates have refs or are explicitly classified as dynamic row internals.

- [ ] **Step 2: Add failing DataInput binding test**

Create a minimal refs object with CSV input, reload button, apply button, ocean drawer button, and row template. Call `Bind(...)` and assert that input text and button listeners work.

```csharp
[Test]
public void DataInputView_BindsExistingCsvInputAndReloadButton()
{
    var panel = new GameObject("DataInputPanel").AddComponent<RectTransform>();
    var input = CreateInput(panel.transform, "CsvPathInput");
    var reload = CreateButton(panel.transform, "ReloadCsvButton");
    var refs = new DataInputPanelRefs();
    refs.panel = panel;
    refs.mission.csvPathInput = input;
    refs.mission.reloadCsvButton = reload;
    var requestedPath = string.Empty;
    var view = new GameObject("DataInput").AddComponent<DataInputView>();

    view.Bind(refs, "E:\\data\\sample.csv", SimulationProfile.Default, null, path => requestedPath = path, _ => { }, null);
    reload.onClick.Invoke();

    Assert.That(input.text, Is.EqualTo("E:\\data\\sample.csv"));
    Assert.That(requestedPath, Is.EqualTo("E:\\data\\sample.csv"));
}
```

- [ ] **Step 3: Run test to verify it fails**

Run the EditMode command. Expected: compile error for missing `DataInputView.Bind(...)`.

- [ ] **Step 4: Add DataInputView.Bind**

Implement `Bind(...)` to assign callbacks and refs, initialize text fields, build model buttons into the Prefab-owned prediction section, and connect existing callbacks. It must not call `ClearRuntimeUi()` and must not create `DataInputPanel`.

- [ ] **Step 5: Replace full-root cleanup**

Remove calls that delete `transform` children in Prefab-bound mode. Limit cleanup to dynamic containers:

```csharp
ClearChildren(refs.ocean.oceanLayerContent);
HideRowTemplate(refs.ocean.oceanLayerRowTemplate);
```

- [ ] **Step 6: Add row binding component**

Create `OceanCurrentLayerRowView.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed class OceanCurrentLayerRowView : MonoBehaviour, IUiReferenceProvider
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text depthRangeText;
        [SerializeField] private Text velocityText;
        [SerializeField] private Button editButton;
        [SerializeField] private Button removeButton;

        public void Bind(int index, OceanCurrentLayer layer, Action<int> onEdit, Action<int> onRemove)
        {
            titleText.text = $"Layer {index + 1}";
            depthRangeText.text = $"{layer.MinDepthM:0}-{layer.MaxDepthM:0} m";
            velocityText.text = $"E {layer.EastwardMps:0.00} / N {layer.NorthwardMps:0.00} m/s";
            editButton.onClick.RemoveAllListeners();
            removeButton.onClick.RemoveAllListeners();
            editButton.onClick.AddListener(() => onEdit(index));
            removeButton.onClick.AddListener(() => onRemove(index));
        }

        public void CollectReferenceIssues(List<UiReferenceIssue> issues)
        {
            UiReferenceValidator.Require(titleText, this, "OceanCurrentLayerRow.prefab", "titleText", issues);
            UiReferenceValidator.Require(depthRangeText, this, "OceanCurrentLayerRow.prefab", "depthRangeText", issues);
            UiReferenceValidator.Require(velocityText, this, "OceanCurrentLayerRow.prefab", "velocityText", issues);
            UiReferenceValidator.Require(editButton, this, "OceanCurrentLayerRow.prefab", "editButton", issues);
            UiReferenceValidator.Require(removeButton, this, "OceanCurrentLayerRow.prefab", "removeButton", issues);
        }
    }
}
```

Add this root-validation test after the component exists:

```csharp
[Test]
public void RuntimeUiRoot_ValidatesOceanRowTemplateProvider()
{
    var rootObject = scope.CreateRoot("RuntimeUiRoot");
    var canvas = new GameObject("RuntimeCanvas", typeof(Canvas)).GetComponent<Canvas>();
    canvas.transform.SetParent(rootObject.transform, false);
    var modalRoot = new GameObject("ModalRoot").AddComponent<RectTransform>();
    modalRoot.transform.SetParent(canvas.transform, false);
    var template = new GameObject("OceanCurrentLayerRowTemplate").AddComponent<RectTransform>();
    template.transform.SetParent(modalRoot.transform, false);
    template.gameObject.AddComponent<OceanCurrentLayerRowView>();
    var root = rootObject.AddComponent<RuntimeUiRoot>();
    var serialized = new UnityEditor.SerializedObject(root);
    serialized.FindProperty("runtimeCanvas").objectReferenceValue = canvas;
    serialized.FindProperty("modalRoot").objectReferenceValue = modalRoot;
    serialized.FindProperty("references.dataInput.ocean.oceanLayerRowTemplate").objectReferenceValue = template;
    serialized.ApplyModifiedPropertiesWithoutUndo();

    var issues = root.ValidateReferences(RuntimeUiValidationProfile.EnabledPanels);

    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("titleText"));
    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("editButton"));
}
```

This test proves `RuntimeUiRoot` calls `IUiReferenceProvider.CollectReferenceIssues(...)` on the row template itself; template internals cannot be left unchecked until row instantiation.

- [ ] **Step 7: Convert repeated rows to RowTemplate**

For ocean layers, instantiate:

```csharp
var row = Instantiate(refs.ocean.oceanLayerRowTemplate, refs.ocean.oceanLayerContent);
row.gameObject.SetActive(true);
row.name = $"OceanCurrentLayerRow{index + 1}";
row.GetComponent<OceanCurrentLayerRowView>().Bind(index, layer, EditOceanLayer, RemoveOceanLayer);
```

Do not use string child `Find(...)` for row internals in production code. The row template owns its internal references through `OceanCurrentLayerRowView`.

- [ ] **Step 8: Update TwinBootstrap to bind data input**

Use:

```csharp
dataInput.Bind(runtimeUiRoot.References.dataInput, CurrentCsvPath, RuntimeDataSourceState.SimulationProfile, prediction, ReloadFromCsvPath, ReloadFromSimulationProfile, oceanVolume != null ? oceanVolume.UpdateCurrentProfile : null);
```

After DataInput refs and RowTemplate refs are assigned, update `RuntimeUiRoot.enabledPanelValidationMask` in `Assets/Scenes/Main.unity` to include `RuntimeUiPanelFlags.DataInput`.

- [ ] **Step 9: Run EditMode tests**

Run the EditMode command. Expected: DataInput bind test passes; existing DataInput behavior tests pass through fallback or bound refs.

- [ ] **Step 10: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView*.cs UnderwaterGliderTwin/Assets/Scripts/UI/OceanCurrentLayerRowView.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
git commit -m "feat: bind data input UI from editable refs"
```

---

### Task 8: ModalRoot, Ocean Drawer, and Flight Leg Drawer Prefabs

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.SimulationSection.cs`
- Create: `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCurrentDrawer.prefab`
- Create: `UnderwaterGliderTwin/Assets/UI/Prefabs/FlightLegDrawer.prefab`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Consumes: `RuntimeUiRoot.ModalRoot`
- Produces: drawers as inactive Prefab-owned children under `ModalRoot`

- [ ] **Step 1: Add failing test for no modal Canvas creation**

Add:

```csharp
[Test]
public void OceanDrawer_UsesModalRootWithoutCreatingExtraCanvas()
{
    RuntimeUiFallback.AllowRuntimeFallback = false;
    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    var root = scope.CreateRoot("ModalTestRoot");
    var modalRoot = new GameObject("ModalRoot").AddComponent<RectTransform>();
    modalRoot.SetParent(root.transform, false);
    var drawer = new GameObject("OceanCurrentDrawerPanel").AddComponent<RectTransform>();
    drawer.SetParent(modalRoot, false);
    var refs = new DataInputPanelRefs();
    refs.ocean.oceanCurrentDrawer = drawer;
    var view = new GameObject("DataInput").AddComponent<DataInputView>();
    view.transform.SetParent(root.transform, false);

    view.Bind(refs, string.Empty, SimulationProfile.Default, null, _ => { }, _ => { }, null);

    Assert.That(CountObjectsNamedInScene(scene, "OceanCurrentModalCanvas"), Is.EqualTo(0));
}

private static int CountObjectsNamedInScene(UnityEngine.SceneManagement.Scene scene, string objectName)
{
    var count = 0;
    foreach (var transform in Object.FindObjectsOfType<Transform>(true))
    {
        if (transform.gameObject.scene == scene && transform.name == objectName)
        {
            count++;
        }
    }
    return count;
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: assertion failure or missing refs behavior until drawer path is migrated.

- [ ] **Step 3: Remove production `EnsureOceanCurrentModalCanvas` usage**

Change ocean drawer opening code to use `refs.ocean.oceanCurrentDrawer`. Keep `EnsureOceanCurrentModalCanvas` callable only when `RuntimeUiFallback.AllowRuntimeFallback` is true, and make it log fallback.

- [ ] **Step 4: Move flight leg drawer to ModalRoot**

Change `BuildFlightLegDrawer` to bind `refs.simulation.flightLegDrawer` in Prefab-bound mode. The drawer is inactive by default and opened with `SetActive(true)`.

- [ ] **Step 5: Extend builder for drawer Prefabs**

Create inactive drawer roots under `RuntimeCanvas/ModalRoot`:

```text
ModalRoot/OceanCurrentDrawerPanel
ModalRoot/FlightLegDrawerPanel
```

Save both as Prefabs under `Assets/UI/Prefabs`.

- [ ] **Step 6: Run EditMode tests**

Run the EditMode command. Expected: no production path creates `OceanCurrentModalCanvas`; fallback tests still allow it.

- [ ] **Step 7: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.SimulationSection.cs UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCurrentDrawer.prefab UnderwaterGliderTwin/Assets/UI/Prefabs/FlightLegDrawer.prefab UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
git commit -m "feat: move drawers into editable modal root"
```

---

### Task 9: PlayMode and Acceptance Tests

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Tests/PlayMode/UnderwaterGliderTwin.PlayModeTests.asmdef`
- Create: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`

**Interfaces:**
- Consumes: editable `Welcome` and `Main` scenes
- Produces: PlayMode checks for launch UI, main UI uniqueness, drawer activation, and dynamic row refresh

- [ ] **Step 1: Create PlayMode asmdef**

Create:

```json
{
  "name": "UnderwaterGliderTwin.PlayModeTests",
  "rootNamespace": "UnderwaterGliderTwin.Tests",
  "references": [
    "UnderwaterGliderTwin.Runtime",
    "UnityEngine.UI"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": [
    "TestAssemblies"
  ]
}
```

- [ ] **Step 2: Add PlayMode uniqueness test**

Create:

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
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

            var scene = SceneManager.GetActiveScene();
            Assert.That(Object.FindObjectsOfType<Canvas>().Count(canvas => canvas.gameObject.scene == scene), Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<EventSystem>().Count(eventSystem => eventSystem.gameObject.scene == scene), Is.EqualTo(1));
            Assert.That(GameObject.Find("RuntimeUiRoot"), Is.Not.Null);
            Assert.That(GameObject.Find("RuntimeCanvas"), Is.Not.Null);
            Assert.That(GameObject.Find("RuntimeUI"), Is.Null);
            Assert.That(GameObject.FindObjectsOfType<Button>().Length, Is.GreaterThan(0));
        }
    }
}
```

- [ ] **Step 3: Add EditMode acceptance tests**

Add tests for:

```csharp
Assert.That(GameObject.Find("WelcomeCanvas"), Is.Not.Null);
Assert.That(GameObject.Find("RuntimeUiRoot"), Is.Not.Null);
Assert.That(GameObject.Find("ModalRoot"), Is.Not.Null);
```

Open scenes through `EditorSceneManager.OpenScene`. Store `EditorSceneManager.GetActiveScene().path` before opening a test scene and reopen the previous scene in a `finally` block so scene tests do not leak state into later tests.

Add an idempotency test:

```csharp
[Test]
public void EditableUiSceneBuilder_BuildMainSceneIsIdempotent()
{
    var previous = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
    try
    {
        UnderwaterGliderTwin.Editor.EditableUiSceneBuilder.BuildMainScene();
        UnderwaterGliderTwin.Editor.EditableUiSceneBuilder.BuildMainScene();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Assert.That(Object.FindObjectsOfType<Canvas>().Count(canvas => canvas.gameObject.scene == scene), Is.EqualTo(1));
        Assert.That(Object.FindObjectsOfType<EventSystem>().Count(eventSystem => eventSystem.gameObject.scene == scene), Is.EqualTo(1));
        Assert.That(GameObject.FindObjectsOfType<GameObject>().Count(go => go.scene == scene && go.name == "DashboardPanel"), Is.EqualTo(1));
        Assert.That(UnityEditor.PrefabUtility.GetPrefabInstanceStatus(GameObject.Find("DashboardPanel")), Is.EqualTo(UnityEditor.PrefabInstanceStatus.Connected));
    }
    finally
    {
        if (!string.IsNullOrEmpty(previous))
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
        }
    }
}
```

- [ ] **Step 4: Run EditMode tests**

Run the EditMode command. Expected: reference and scene structure tests pass.

- [ ] **Step 5: Run PlayMode tests**

Run the PlayMode command. Expected: PlayMode uniqueness test passes. If the scene needs a valid data source, set runtime simulation mode in the test before loading `Main`.

- [ ] **Step 6: Manual prefab edit acceptance**

In Unity Editor, open `Assets/UI/Prefabs/DashboardPanel.prefab`, change `DashboardPanel` Image color, save, enter Play Mode, and verify the color appears without editing code.

- [ ] **Step 7: Dual-resolution screenshot acceptance**

Run the player or PlayMode screenshot harness at 1920x1080 and 1366x768. Save screenshots to:

```text
E:\upan\digital twin\Artifacts\EditableUi\main-1920x1080.png
E:\upan\digital twin\Artifacts\EditableUi\main-1366x768.png
```

Expected visual checks:

- `RuntimeCanvas` fills the screen.
- `DashboardPanel`, `StatusPanel`, `DataInputPanel`, `PlaybackControlsPanel`, and `OceanCommandToolbar` are visible.
- No long-lived panels overlap incoherently.
- The manual `DashboardPanel` Prefab color edit is visible in both screenshots.

- [ ] **Step 8: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Tests/PlayMode UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs
git commit -m "test: verify editable UI runtime acceptance"
```

---

### Task 10: Remove Long-Lived Runtime UI Creation

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView*.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`

**Interfaces:**
- Consumes: all previous `Bind(...)` paths
- Produces: production runtime with no long-lived UI creation through `UiFactory`

- [ ] **Step 1: Add failing test for production no-fallback path**

Add:

```csharp
[Test]
public void RuntimeFallback_ProductionDefaultRemainsDisabled()
{
    Assert.That(RuntimeUiFallback.AllowRuntimeFallback, Is.False);
}
```

Add PlayMode assertion that no object named `RuntimeUI` exists after loading `Main`.

- [ ] **Step 2: Enable strict production validation**

Set `TwinBootstrap.strictUiValidation` to true in `Assets/Scenes/Main.unity` after all long-lived panel, drawer, modal, and RowTemplate references have been assigned.

Also set `RuntimeUiRoot.enabledPanelValidationMask` to `RuntimeUiPanelFlags.All`. `Strict` validation validates all required groups regardless of the mask, but setting the mask to `All` keeps editor `EnabledPanels` validation and scene intent aligned.

Add an EditMode scene test:

```csharp
[Test]
public void MainScene_PassesStrictRuntimeUiValidation()
{
    var previous = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
    try
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var root = Object.FindObjectOfType<RuntimeUiRoot>();

        Assert.That(root, Is.Not.Null);
        Assert.That(root.ValidateReferences(RuntimeUiValidationProfile.Strict), Is.Empty);
    }
    finally
    {
        if (!string.IsNullOrEmpty(previous))
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
        }
    }
}
```

Expected: the test fails until every required serialized reference, including RowTemplate internals, is assigned. Optional fields marked with `[OptionalUiReference]` do not fail strict validation.

- [ ] **Step 3: Remove fallback from TwinBootstrap production flow**

Leave `allowRuntimeFallback` serialized for development, default false. Production branch requires `runtimeUiRoot != null`. Runtime-generated `canvasRoot = new GameObject("RuntimeUI")` is only executed inside:

```csharp
if (allowRuntimeFallback)
{
    RuntimeUiFallback.LogFallback("RuntimeUI");
    // old migration path
}
```

- [ ] **Step 4: Mark old Initialize paths as migration-only**

Add `[System.Obsolete("Use Bind(...) with editable UI references.")]` to old `Initialize(...)` methods that create long-lived UI. Keep them compiled because tests and development fallback still use them.

- [ ] **Step 5: Run all tests**

Run EditMode command. Expected: pass.

Run PlayMode command. Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/*.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs
git commit -m "feat: disable long-lived runtime UI creation"
```

---

## Self-Review

Spec coverage:

- UI root ownership is covered by Tasks 2, 5, 9, and 10.
- `ClearRuntimeUi()` safety is covered by Tasks 3 and 7.
- `EnsureCanvas()` fallback restriction is covered by Task 3.
- Welcome editable scene is covered by Task 4.
- Main editable root and Prefabs are covered by Task 5.
- Base panel binding is covered by Task 6.
- Data input grouping and dynamic rows are covered by Task 7.
- Drawers and modals under `ModalRoot` are covered by Task 8.
- EditMode, PlayMode, dual runtime uniqueness, and Prefab edit acceptance are covered by Task 9.
- Production removal of long-lived runtime creation is covered by Task 10.
- Prefab instance replacement is covered by Task 5.
- Complete base-panel refs are covered by Task 2, and complete DataInput refs are enforced by Task 7's inventory step.
- Canvas fallback no longer uses global `FindObjectOfType<Canvas>()`; Task 3 uses explicit fallback Canvas or parent-chain Canvas only.
- Scoped test cleanup is covered by Task 1 and Task 3.
- Duplicate Canvas, duplicate EventSystem, and duplicate long-lived UI detection are covered by Task 2 and Task 9.
- Static fallback state isolation is covered by Task 1, Task 2, Task 3, and Task 4.
- `DashboardView` refresh uses the real existing `OnFrameChanged(...)` path through `RefreshFromCurrentFrame()`, covered by Task 6.
- Builder idempotency and dual-resolution screenshots are covered by Task 9.
- Staged reference validation is covered by Tasks 1, 2, and 10 through `RuntimeUiValidationProfile.BootstrapOnly`, `EnabledPanels`, and `Strict`.
- `EnabledPanels` validation uses explicit `RuntimeUiPanelFlags` instead of `activeInHierarchy`, covered by Tasks 1, 2, 5, 6, 7, and 10.
- Full grouped reference validation is covered by Task 1 and Task 2 through `UiReferenceGroupBase`, `UiReferenceValidator.RequireFields(...)`, and `references.CollectReferenceIssues(...)`, but is only enforced as a production blocker in Task 10.
- Duplicate detection scope is limited to the owner scene for Canvas/EventSystem and to the `RuntimeUiRoot` subtree for long-lived panels, covered by Task 2.
- `DataInputPanel.prefab` is included in Task 5 file creation, Prefab instance tests, and stable Prefab name list.
- `UiTestObjectScope` is consistently located under `UnderwaterGliderTwin/Assets/Tests/EditMode`, covered by Task 1.
- Welcome builder idempotency, `GetOrCreate` helpers, serialized reference backfill, and `bootstrap.ValidateReferences()` verification are covered by Task 4.
- `EditableUiSceneBuilder` protects unsaved scene changes before rebuilding scenes, including explicit batchmode dirty-scene failure, covered by Tasks 4 and 5.
- PlayMode tests prepare `RuntimeDataSourceState.UseSimulation(SimulationProfile.Default)` before loading `Main`, covered by Task 9.
- Dynamic ocean current rows use `OceanCurrentLayerRowView`, and RowTemplate internals are validated from `RuntimeUiRoot`, covered by Tasks 2 and 7.
- Modal/drawer tests use `UiTestObjectScope` and scene-scoped object counting instead of global `GameObject.Find(...)`, covered by Task 8.
- `UiReferenceValidator.Require(...)` handles null owner, null prefab name, and null field name, covered by Task 1.
- Optional UI references use `[OptionalUiReference]` and do not block strict validation, covered by Tasks 1 and 2.

Placeholder scan:

- The plan contains concrete file paths, class names, method names, test names, commands, and expected outcomes.
- No task relies on an unnamed future implementation.

Type consistency:

- `RuntimeUiRoot.References` returns `RuntimeUiReferences`.
- View binding tasks consume the grouped refs defined in `RuntimeUiReferences.cs`.
- `RuntimeUiFallback.AllowRuntimeFallback` and `RuntimeUiFallback.LogFallback(string)` are used consistently.
