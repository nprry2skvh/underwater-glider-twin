# Ocean Import and Runtime Rebuild UI Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make local ocean-current import usable for long-running simulations and remove misleading/overlapping controls from the ocean-current modal.

**Architecture:** Keep file acquisition in `OceanCurrentFileLoader`/`CopernicusCurrentClient`, keep transactional future-only rebuilds in `SimulationRuntimeSession`, and keep labels/layout in `DataInputView` partial UI files. A successful local load stages the profile and field first; only a successful future rebuild commits the new runtime state.

**Tech Stack:** Unity 2022.3.62f3c1, C#, NUnit EditMode tests, Unity uGUI.

## Global Constraints

- LocalFile never accesses the network or other cache files.
- A failed future rebuild preserves the previous profile, field, frames, playback state, and camera.
- Existing GameObject names and callbacks remain compatible with current tests.
- CSV replay and existing prediction entry points remain unchanged.
- No third-party runtime dependency or native file dialog is introduced.

---

### Task 1: Scale runtime rebuild timeout to future workload

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRuntimeSession.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationRuntimeSessionTests.cs`

**Interfaces:**
- `RequestProfileUpdate` keeps its existing signature and transactional semantics.
- The default timeout remains a hard safety floor; long profiles receive a larger calculated deadline with a finite upper bound.

- [ ] **Step 1: Write the failing test**

Add a test using an injected clock and a long candidate profile. After advancing time beyond 30 seconds but below the calculated long-profile deadline, `Tick()` must leave `IsRebuildPending` true and must not set `LastError` to timeout.

- [ ] **Step 2: Run the focused test and verify it fails**

Run `scripts\test-editmode.cmd` and inspect the `SimulationRuntimeSessionTests` result. The new test must fail because the current implementation always uses the fixed 30-second deadline.

- [ ] **Step 3: Implement the minimal deadline calculation**

Estimate future frame count from `CycleCount`, `CycleDurationSeconds`, and `SampleIntervalSeconds`; convert the estimated slice count into a bounded extra budget. Set `pendingDeadline` to the greater of the configured timeout and the workload budget, with a finite maximum so a stuck generator still fails fast.

- [ ] **Step 4: Run focused and full EditMode tests**

Run `scripts\test-editmode.cmd`. Expected: all tests pass, including the existing explicit timeout rollback test.

- [ ] **Step 5: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRuntimeSession.cs UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationRuntimeSessionTests.cs
git commit -m "fix: allow long simulation rebuilds to complete"
```

### Task 2: Make local ocean import explicit and responsive

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Keep `OceanCurrentDrawerLookupButton` as the single callback entry point.
- Preserve `OceanCurrentLocalFileInput`, `OceanCurrentActualSource`, and existing modal object names.
- Button text reflects the acquisition mode: Online=`联网获取海流`, CacheOnly=`读取缓存`, LocalFile=`加载本地文件`.

- [ ] **Step 1: Write failing UI tests**

Add tests that select LocalFile and assert the lookup button label is `加载本地文件`, select CacheOnly and assert `读取缓存`, and verify the modal field cards keep each label and input under one field container with no horizontal overlap at 1280 and 1920 layout widths.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run `scripts\test-editmode.cmd`; the label test must fail because the current button is always `联网获取海流`, and the layout test must fail for the fixed-coordinate modal.

- [ ] **Step 3: Implement mode-aware button state**

Update the existing button text and interactability whenever `SetOceanCurrentAcquisitionMode` runs. Keep LocalFile strictly on `CopernicusCurrentClient.Fetch(..., LocalFile, selectedPath, ...)`; do not add a network fallback.

- [ ] **Step 4: Replace modal fixed rows with responsive field cards**

Use a vertical label/input field container and a responsive grid for the modal’s numeric controls. Keep title, close button, status, and acquisition controls outside the scrollable content. Ensure the local path row spans the modal width and the lookup button remains visible and clickable.

- [ ] **Step 5: Verify UI tests and build**

Run `scripts\test-editmode.cmd`, then `scripts\build-windows.cmd`.

- [ ] **Step 6: Commit**

```powershell
git add UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
git commit -m "fix: clarify local current import and responsive modal layout"
```

### Task 3: Packaged acceptance verification

- [ ] Run `py -m unittest discover PredictionTraining/tests`.
- [ ] Run `scripts\test-editmode.cmd` and `scripts\build-windows.cmd`.
- [ ] Start the packaged Player with a long simulation profile, import `C:\temp\ocean-current.json` in LocalFile mode, and verify `load.log` contains `Simulation profile update applied without reloading the scene.`
- [ ] Capture a screenshot showing the mode-aware button, actual source, and non-overlapping labels at the target resolution.
