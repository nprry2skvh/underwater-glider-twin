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

---

## File Structure

- Create `UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceIssue.cs`: value type describing a missing or duplicate UI reference.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/IUiReferenceProvider.cs`: interface for reference validation providers.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceValidator.cs`: shared validation and object-path formatting.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`: serialized owner for `RuntimeCanvas`, long-lived panels, `ModalRoot`, drawer roots, and row templates.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`: grouped references used by view `Bind(...)` methods.
- Create `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiFallback.cs`: single explicit switch for migration-only runtime generation.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`: restrict Canvas discovery and add fallback logging.
- Modify `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`: bind `RuntimeUiRoot` and call view `Bind(...)` methods.
- Modify `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/WelcomeBootstrap.cs`: bind serialized welcome references and remove production UI creation.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`: add `Bind(DashboardPanelRefs, PlaybackController, PredictionController)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs`: add `Bind(StatusPanelRefs, PlaybackController, AlarmEvaluator, TwinLogger, PredictionController)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`: add `Bind(OceanToolbarRefs, TwinCameraController, TrajectoryView)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`: add `Bind(PlaybackControlsRefs refs, PlaybackController playbackController, TwinCameraController cameraController, UnderwaterEnvironmentBuilder environmentBuilder, TrajectoryView trajectoryView, Action onExitRequested = null, Func<string> onScreenshotRequested = null, Action onMissionViewRequested = null)`.
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView*.cs`: add grouped refs, restrict `ClearRuntimeUi()`, and migrate drawers/modals to Prefab-owned containers.
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
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiFallback.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`

**Interfaces:**
- Produces: `UiReferenceIssue(string prefabName, string objectPath, string fieldName, string message)`
- Produces: `IUiReferenceProvider.CollectReferenceIssues(List<UiReferenceIssue> issues)`
- Produces: `UiReferenceValidator.Require(Object value, Component owner, string prefabName, string fieldName, List<UiReferenceIssue> issues)`
- Produces: `UiReferenceValidator.GetPath(Transform transform)`
- Produces: `RuntimeUiFallback.AllowRuntimeFallback`
- Produces: `RuntimeUiFallback.LogFallback(string panelName)`

- [ ] **Step 1: Write failing tests for missing-reference reports**

Add this test class:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class EditableUiReferenceTests
    {
        [TearDown]
        public void TearDown()
        {
            RuntimeUiFallback.AllowRuntimeFallback = false;
            foreach (var obj in Object.FindObjectsOfType<GameObject>())
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void Validator_ReportsPrefabObjectPathAndFieldName()
        {
            var root = new GameObject("DashboardPanel");
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

namespace UnderwaterGliderTwin.UI
{
    public interface IUiReferenceProvider
    {
        void CollectReferenceIssues(List<UiReferenceIssue> issues);
    }
}
```

Create `UiReferenceValidator.cs`:

```csharp
using System.Collections.Generic;
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

            issues.Add(new UiReferenceIssue(
                string.IsNullOrWhiteSpace(prefabName) ? owner.gameObject.name : prefabName,
                owner != null ? GetPath(owner.transform) : "<missing owner>",
                fieldName,
                "Required UI reference is missing."));
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
    }
}
```

- [ ] **Step 4: Run EditMode tests**

Run the EditMode command above. Expected: new tests pass; existing UI tests still use runtime generation and may continue passing.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceIssue.cs UnderwaterGliderTwin/Assets/Scripts/UI/IUiReferenceProvider.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiReferenceValidator.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiFallback.cs UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs
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
- Produces: `RuntimeUiRoot.ValidateReferences()`
- Produces: `RuntimeUiReferences` grouped UI references for subsequent panel binding tasks

- [ ] **Step 1: Write failing test for root ownership**

Add to `EditableUiReferenceTests.cs`:

```csharp
[Test]
public void RuntimeUiRoot_RequiresCanvasAndModalRoot()
{
    var root = new GameObject("RuntimeUiRoot").AddComponent<RuntimeUiRoot>();
    var issues = root.ValidateReferences();

    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("runtimeCanvas"));
    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("modalRoot"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command above. Expected: compile error for missing `RuntimeUiRoot`.

- [ ] **Step 3: Implement RuntimeUiRoot and grouped references**

Create `RuntimeUiReferences.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    [Serializable]
    public sealed class RuntimeUiReferences
    {
        public DashboardPanelRefs dashboard = new DashboardPanelRefs();
        public StatusPanelRefs status = new StatusPanelRefs();
        public DataInputPanelRefs dataInput = new DataInputPanelRefs();
        public PlaybackControlsRefs playback = new PlaybackControlsRefs();
        public OceanToolbarRefs oceanToolbar = new OceanToolbarRefs();
    }

    [Serializable] public sealed class DashboardPanelRefs { public RectTransform panel; public Text depthValue; public Text batteryValue; }
    [Serializable] public sealed class StatusPanelRefs { public RectTransform panel; public Text alarmValue; public Text missionValue; }
    [Serializable] public sealed class PlaybackControlsRefs { public RectTransform panel; public Button playPauseButton; public Slider progressSlider; public Button exitButton; }
    [Serializable] public sealed class OceanToolbarRefs { public RectTransform panel; public Button topButton; public Button followButton; public Text visibleArrowCount; }
    [Serializable] public sealed class DataInputPanelRefs { public RectTransform panel; public MissionSectionRefs mission = new MissionSectionRefs(); public SimulationSectionRefs simulation = new SimulationSectionRefs(); public OceanSectionRefs ocean = new OceanSectionRefs(); public DynamicsSectionRefs dynamics = new DynamicsSectionRefs(); public PredictionSectionRefs prediction = new PredictionSectionRefs(); }
    [Serializable] public sealed class MissionSectionRefs { public InputField csvPathInput; public Button reloadCsvButton; }
    [Serializable] public sealed class SimulationSectionRefs { public Button applyButton; public RectTransform flightLegDrawer; }
    [Serializable] public sealed class OceanSectionRefs { public Button drawerButton; public RectTransform oceanCurrentDrawer; public RectTransform oceanLayerContent; public RectTransform oceanLayerRowTemplate; }
    [Serializable] public sealed class DynamicsSectionRefs { public InputField massInput; public InputField referenceAreaInput; }
    [Serializable] public sealed class PredictionSectionRefs { public Button physicsModelButton; public Button xgBoostModelButton; }
}
```

Create `RuntimeUiRoot.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public sealed class RuntimeUiRoot : MonoBehaviour, IUiReferenceProvider
    {
        [SerializeField] private Canvas runtimeCanvas;
        [SerializeField] private RectTransform modalRoot;
        [SerializeField] private RuntimeUiReferences references = new RuntimeUiReferences();

        public Canvas RuntimeCanvas => runtimeCanvas;
        public RectTransform ModalRoot => modalRoot;
        public RuntimeUiReferences References => references;

        public List<UiReferenceIssue> ValidateReferences()
        {
            var issues = new List<UiReferenceIssue>();
            CollectReferenceIssues(issues);
            return issues;
        }

        public void CollectReferenceIssues(List<UiReferenceIssue> issues)
        {
            UiReferenceValidator.Require(runtimeCanvas, this, "Main.unity", "runtimeCanvas", issues);
            UiReferenceValidator.Require(modalRoot, this, "Main.unity", "modalRoot", issues);
        }

        private void OnValidate()
        {
            var issues = ValidateReferences();
            foreach (var issue in issues)
            {
                Debug.LogError(issue.ToString(), this);
            }
        }

        public void EnsureSingleEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
```

- [ ] **Step 4: Modify TwinBootstrap to serialize RuntimeUiRoot**

Add fields:

```csharp
[SerializeField] private RuntimeUiRoot runtimeUiRoot;
[SerializeField] private bool allowRuntimeFallback;
```

Before UI initialization, add:

```csharp
RuntimeUiFallback.AllowRuntimeFallback = allowRuntimeFallback;
if (runtimeUiRoot == null && !allowRuntimeFallback)
{
    throw new InvalidOperationException("RuntimeUiRoot is required. Assign Main/RuntimeUiRoot on TwinBootstrap.");
}
runtimeUiRoot?.EnsureSingleEventSystem();
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
- Produces: `UiFactory.EnsureCanvas(Transform parent, string fallbackPanelName)`
- Produces: `DataInputView.ClearDynamicRuntimeUi()`

- [ ] **Step 1: Write failing fallback tests**

Add:

```csharp
[Test]
public void UiFactory_RejectsRuntimeCanvasCreationWhenFallbackDisabled()
{
    RuntimeUiFallback.AllowRuntimeFallback = false;
    var owner = new GameObject("Dashboard").transform;

    var ex = Assert.Throws<System.InvalidOperationException>(() => UiFactory.EnsureCanvas(owner, "DashboardPanel"));

    Assert.That(ex.Message, Does.Contain("Runtime UI fallback is disabled"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: compile error or assertion failure because `EnsureCanvas(Transform,string)` does not exist.

- [ ] **Step 3: Restrict UiFactory canvas creation**

Add an overload and route the old overload through it:

```csharp
public static Canvas EnsureCanvas(Transform parent)
{
    return EnsureCanvas(parent, parent != null ? parent.name : "UnknownPanel");
}

public static Canvas EnsureCanvas(Transform parent, string fallbackPanelName)
{
    var canvas = Object.FindObjectOfType<Canvas>();
    EnsureEventSystem();
    if (canvas != null)
    {
        return canvas;
    }

    if (!RuntimeUiFallback.AllowRuntimeFallback)
    {
        throw new System.InvalidOperationException($"Runtime UI fallback is disabled. Missing RuntimeCanvas for {fallbackPanelName}.");
    }

    RuntimeUiFallback.LogFallback(fallbackPanelName);
    var canvasObject = new GameObject("RuntimeCanvas");
    canvasObject.transform.SetParent(parent, false);
    canvas = canvasObject.AddComponent<Canvas>();
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

- [ ] **Step 4: Update old EditMode tests to opt into fallback**

In `UiTests.TearDown()`, add:

```csharp
RuntimeUiFallback.AllowRuntimeFallback = false;
```

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
    var bootstrap = new GameObject("WelcomeBootstrap").AddComponent<WelcomeBootstrap>();

    var issues = bootstrap.ValidateReferences();

    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("welcomeCanvas"));
    Assert.That(issues, Has.Some.Property("FieldName").EqualTo("csvInput"));
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

- [ ] **Step 4: Add editor builder for welcome hierarchy**

Create `EditableUiSceneBuilder.cs` with a menu item and callable method:

```csharp
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
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Welcome.unity");
            var bootstrapObject = GameObject.Find("WelcomeBootstrap") ?? new GameObject("WelcomeBootstrap");
            var bootstrap = bootstrapObject.GetComponent<WelcomeBootstrap>() ?? bootstrapObject.AddComponent<WelcomeBootstrap>();
            var canvasObject = GameObject.Find("WelcomeCanvas") ?? new GameObject("WelcomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            EnsureEventSystem();
            CreateWelcomeChildren(canvasObject.transform);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void CreateWelcomeChildren(Transform canvas)
        {
            CreateImage(canvas, "BackgroundImage", Vector2.zero, Vector2.one, new Color(0.025f, 0.12f, 0.18f));
            var panel = CreateImage(canvas, "LaunchPanel", new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), new Color(0.03f, 0.12f, 0.22f, 0.98f));
            CreateText(panel.transform, "TitleText", "水下滑翔机数字孪生", 34, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f));
            CreateText(panel.transform, "DescriptionText", "CSV 遥测回放、参数化任务仿真与实验性短时预测", 18, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.83f));
            CreateInput(panel.transform, "CsvPathInput", new Vector2(0.08f, 0.58f), new Vector2(0.72f, 0.67f));
            CreateButton(panel.transform, "ConfirmCsvButton", "确认 CSV 路径", new Vector2(0.74f, 0.58f), new Vector2(0.92f, 0.67f));
            CreateButton(panel.transform, "StartCsvButton", "开始上次 / 默认 CSV", new Vector2(0.08f, 0.43f), new Vector2(0.48f, 0.53f));
            CreateButton(panel.transform, "SimulationButton", "进入仿真模式", new Vector2(0.52f, 0.43f), new Vector2(0.92f, 0.53f));
            CreateText(panel.transform, "LaunchStatusText", string.Empty, 15, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.30f));
        }
    }
}
```

Use helper methods equivalent to the current welcome `CreateImage`, `CreateText`, `CreateButton`, and `CreateInput`, with stable object names above.

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
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: assertion failure because `Main.unity` has no `RuntimeUiRoot`.

- [ ] **Step 3: Extend editor builder**

Add `BuildMainScene()` menu item and method. It opens `Assets/Scenes/Main.unity`, creates `RuntimeUiRoot`, creates `RuntimeCanvas` with `CanvasScaler` reference resolution `1920x1080`, creates the required child objects, adds `RuntimeUiRoot` component, assigns serialized fields through `SerializedObject`, and saves the scene.

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

Use the builder to create Prefab assets from the corresponding panel roots under `Assets/UI/Prefabs`. Use `PrefabUtility.SaveAsPrefabAsset` for each panel root. Keep these stable names:

```text
DashboardPanel.prefab
StatusPanel.prefab
PlaybackControlsPanel.prefab
OceanCommandToolbar.prefab
```

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

Example shape:

```csharp
public void Bind(DashboardPanelRefs refs, PlaybackController playbackController, PredictionController predictionController)
{
    this.playbackController = playbackController;
    this.predictionController = predictionController;
    panel = refs.panel;
    depthValue = refs.depthValue;
    batteryValue = refs.batteryValue;
    UpdateTelemetry(FrameUpdateReason.Initial);
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
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Consumes: `DataInputPanelRefs`
- Produces: `DataInputView.Bind(DataInputPanelRefs refs, string csvPath, SimulationProfile profile, PredictionController prediction, Action<string> reloadCsv, Action<SimulationProfile> reloadSimulation, Action<OceanCurrentProfile> applyOceanCurrent)`
- Produces: dynamic row creation under `refs.ocean.oceanLayerContent` using `refs.ocean.oceanLayerRowTemplate`

- [ ] **Step 1: Add failing DataInput binding test**

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

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode command. Expected: compile error for missing `DataInputView.Bind(...)`.

- [ ] **Step 3: Add DataInputView.Bind**

Implement `Bind(...)` to assign callbacks and refs, initialize text fields, build model buttons into the Prefab-owned prediction section, and connect existing callbacks. It must not call `ClearRuntimeUi()` and must not create `DataInputPanel`.

- [ ] **Step 4: Replace full-root cleanup**

Remove calls that delete `transform` children in Prefab-bound mode. Limit cleanup to dynamic containers:

```csharp
ClearChildren(refs.ocean.oceanLayerContent);
HideRowTemplate(refs.ocean.oceanLayerRowTemplate);
```

- [ ] **Step 5: Convert repeated rows to RowTemplate**

For ocean layers, instantiate:

```csharp
var row = Instantiate(refs.ocean.oceanLayerRowTemplate, refs.ocean.oceanLayerContent);
row.gameObject.SetActive(true);
row.name = $"OceanCurrentLayerRow{index + 1}";
```

Write values into child `Text` and `InputField` references found by stable names inside the row template.

- [ ] **Step 6: Update TwinBootstrap to bind data input**

Use:

```csharp
dataInput.Bind(runtimeUiRoot.References.dataInput, CurrentCsvPath, RuntimeDataSourceState.SimulationProfile, prediction, ReloadFromCsvPath, ReloadFromSimulationProfile, oceanVolume != null ? oceanVolume.UpdateCurrentProfile : null);
```

- [ ] **Step 7: Run EditMode tests**

Run the EditMode command. Expected: DataInput bind test passes; existing DataInput behavior tests pass through fallback or bound refs.

- [ ] **Step 8: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView*.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
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
    var modalRoot = new GameObject("ModalRoot").AddComponent<RectTransform>();
    var drawer = new GameObject("OceanCurrentDrawerPanel").AddComponent<RectTransform>();
    drawer.SetParent(modalRoot, false);
    var refs = new DataInputPanelRefs();
    refs.ocean.oceanCurrentDrawer = drawer;
    var view = new GameObject("DataInput").AddComponent<DataInputView>();

    view.Bind(refs, string.Empty, SimulationProfile.Default, null, _ => { }, _ => { }, null);

    Assert.That(GameObject.Find("OceanCurrentModalCanvas"), Is.Null);
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
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class EditableUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_HasSingleEditableUiRootAtRuntime()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            Assert.That(Object.FindObjectsOfType<Canvas>().Length, Is.EqualTo(1));
            Assert.That(GameObject.Find("RuntimeUiRoot"), Is.Not.Null);
            Assert.That(GameObject.Find("RuntimeCanvas"), Is.Not.Null);
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

Open scenes through `EditorSceneManager.OpenScene`.

- [ ] **Step 4: Run EditMode tests**

Run the EditMode command. Expected: reference and scene structure tests pass.

- [ ] **Step 5: Run PlayMode tests**

Run the PlayMode command. Expected: PlayMode uniqueness test passes. If the scene needs a valid data source, set runtime simulation mode in the test before loading `Main`.

- [ ] **Step 6: Manual prefab edit acceptance**

In Unity Editor, open `Assets/UI/Prefabs/DashboardPanel.prefab`, change `DashboardPanel` Image color, save, enter Play Mode, and verify the color appears without editing code.

- [ ] **Step 7: Commit**

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

- [ ] **Step 2: Remove fallback from TwinBootstrap production flow**

Leave `allowRuntimeFallback` serialized for development, default false. Production branch requires `runtimeUiRoot != null`. Runtime-generated `canvasRoot = new GameObject("RuntimeUI")` is only executed inside:

```csharp
if (allowRuntimeFallback)
{
    RuntimeUiFallback.LogFallback("RuntimeUI");
    // old migration path
}
```

- [ ] **Step 3: Mark old Initialize paths as migration-only**

Add `[System.Obsolete("Use Bind(...) with editable UI references.")]` to old `Initialize(...)` methods that create long-lived UI. Keep them compiled because tests and development fallback still use them.

- [ ] **Step 4: Run all tests**

Run EditMode command. Expected: pass.

Run PlayMode command. Expected: pass.

- [ ] **Step 5: Commit**

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

Placeholder scan:

- The plan contains concrete file paths, class names, method names, test names, commands, and expected outcomes.
- No task relies on an unnamed future implementation.

Type consistency:

- `RuntimeUiRoot.References` returns `RuntimeUiReferences`.
- View binding tasks consume the grouped refs defined in `RuntimeUiReferences.cs`.
- `RuntimeUiFallback.AllowRuntimeFallback` and `RuntimeUiFallback.LogFallback(string)` are used consistently.
