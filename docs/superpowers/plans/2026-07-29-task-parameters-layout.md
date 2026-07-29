# Task Parameters Drawer Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the overlapping fixed-coordinate mission parameter editor with a readable three-section responsive drawer while preserving existing leaf control names and behavior.

**Architecture:** Keep `DataInputView` as the behavior owner. Add layout-only section and field wrappers beneath the existing `MissionConfigurationPanel`, and use a dedicated modal canvas for the ocean-current editor. The existing leaf controls and callbacks remain the source of truth; layout code only changes parenting, anchors, sizing, and responsive grouping.

**Tech Stack:** Unity UGUI (`RectTransform`, `ScrollRect`, `GridLayoutGroup`, `Canvas`), NUnit EditMode tests, existing Windows build scripts.

## Global Constraints

- Existing leaf control names and listeners must remain unchanged; only new wrapper parents may be introduced.
- The main drawer is below the command-center header; its viewport must not cover the header/toggle or playback bar.
- The ocean-current editor uses a modal Canvas with sorting order higher than the main command Canvas and a full-screen raycast blocker.
- Field labels include units and may wrap; CSV and long text inputs span the full available width.
- Responsive columns are three at wide layouts, two at 1280 px, and one when the minimum card width cannot be met.
- Do not modify telemetry, simulation, or prediction behavior in this UI-only change.

---

### Task 1: Define failing layout and modal tests

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Consumes: `DataInputView.Initialize`, stable leaf object names, `MissionConfigurationPanel`, and `OceanCurrentDrawerPanel`.
- Produces: executable assertions for field grouping, responsive columns, toggle hit area, and modal sorting/raycast behavior.

- [ ] **Step 1: Add a field-card grouping test**

  Initialize a view, open the task drawer, find `SimulationCyclesInput` and `SimulationCyclesInputLabel`, and assert their common parent name is `SimulationCyclesInputField`.

- [ ] **Step 2: Add responsive layout tests**

  Set a test Canvas width to 1920 and 1280, rebuild the view, and assert the content grid reports 3 and 2 columns respectively. Set a narrow width and assert one column when the minimum card width is exceeded.

- [ ] **Step 3: Add toggle and modal interaction tests**

  Invoke `MissionConfigurationDrawerToggleButton` twice and assert expanded height/text then collapsed height/text. Open `OceanCurrentDrawerButton` and assert the modal Canvas sorting order is greater than the main Canvas and its raycast blocker is enabled.

- [ ] **Step 4: Run the focused tests and verify they fail**

  Run:

  ```powershell
  & 'E:\upan\digital twin\scripts\test-editmode.cmd'
  ```

  Expected: the new grouping, responsive-column, or modal assertions fail against the current fixed-coordinate layout.

- [ ] **Step 5: Commit the tests**

  ```powershell
  git add UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
  git commit -m "test: define task parameter drawer layout contract"
  ```

### Task 2: Build section and field-card layout

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`

**Interfaces:**
- Consumes: Existing leaf creation calls and callbacks in `DataInputView.Initialize`.
- Produces: `TaskParametersSection`, `TaskParameterField`, and responsive layout helpers that preserve leaf names.

- [ ] **Step 1: Add layout constants and wrapper helpers**

  Add constants for header height, section spacing, minimum field width, and field height. Add helpers that create a section card and a field card, then reparent the already-created leaf controls without renaming them.

- [ ] **Step 2: Replace fixed x-coordinate placement with three section containers**

  Parent the existing task/prediction leaves into the first section, simulation leaves into the second, and ocean/flight leaves into the third. Keep `CsvPathInput` and its load button in a full-width row. Keep the existing object names and listener registrations.

- [ ] **Step 3: Implement responsive column calculation**

  Use available viewport width and a minimum card width to select 3, 2, or 1 columns. Set `GridLayoutGroup.constraintCount` accordingly and allow label `Text` components to wrap within the card.

- [ ] **Step 4: Run EditMode tests and fix layout-only failures**

  Run the test command from Task 1. Expected: all grouping and responsive layout assertions pass without changing simulation/prediction tests.

- [ ] **Step 5: Commit the section layout**

  ```powershell
  git add UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs
  git commit -m "fix: redesign task parameter drawer layout"
  ```

### Task 3: Isolate the ocean-current editor modal

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Consumes: `ToggleOceanCurrentDrawer`, `oceanCurrentDrawer`, and the main Canvas created by `UiFactory.EnsureCanvas`.
- Produces: a modal Canvas with a blocker, deterministic sorting order, and restore-on-close behavior.

- [ ] **Step 1: Create the modal Canvas and blocker**

  Create `OceanCurrentModalCanvas` with `overrideSorting = true`, sorting order above the main command Canvas, and a full-screen transparent `Image` with `raycastTarget = true`. Parent the existing ocean drawer under it without changing leaf names.

- [ ] **Step 2: Wire open/close state**

  Opening the ocean drawer enables the modal Canvas, places the drawer last, and focuses its first input. Closing disables the modal Canvas and restores the task drawer’s previous expanded state.

- [ ] **Step 3: Run modal tests and the full EditMode suite**

  ```powershell
  & 'E:\upan\digital twin\scripts\test-editmode.cmd'
  ```

  Expected: all tests pass, including existing ocean-layer behavior tests.

- [ ] **Step 4: Commit the modal isolation**

  ```powershell
  git add UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
  git commit -m "fix: isolate ocean current editor modal"
  ```

### Task 4: Runtime resolution verification

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs` only if assertions need platform-safe helpers.
- Output: `TestResults/WindowsBuild.log` and Player screenshots under `%USERPROFILE%\AppData\LocalLow\DefaultCompany\UnderwaterGliderTwin\Exports`.

**Interfaces:**
- Consumes: completed drawer and modal implementation from Tasks 2–3.
- Produces: verified packaged Player behavior at 1920×1080 and 1280×720.

- [ ] **Step 1: Run the complete EditMode suite**

  ```powershell
  & 'E:\upan\digital twin\scripts\test-editmode.cmd'
  ```

- [ ] **Step 2: Build the Windows Player**

  ```powershell
  & 'E:\upan\digital twin\scripts\build-windows.cmd'
  ```

- [ ] **Step 3: Capture both runtime resolutions**

  Start the packaged Player with `--simulation -screen-width 1920 -screen-height 1080`, capture after opening and closing the drawer, then repeat at 1280×720. Verify the drawer does not cover the header or playback controls, field labels sit directly above their inputs, and the ocean modal blocks background clicks.

- [ ] **Step 4: Inspect logs and diff**

  Clear or archive the existing Player.log before each run, inspect `%USERPROFILE%\AppData\LocalLow\DefaultCompany\UnderwaterGliderTwin\Player.log`, then run `git diff --check`.

- [ ] **Step 5: Commit verification notes**

  ```powershell
  git add docs/superpowers/specs/2026-07-29-task-parameters-layout-design.md docs/superpowers/plans/2026-07-29-task-parameters-layout.md
  git commit -m "docs: specify task parameter drawer redesign"
  ```
