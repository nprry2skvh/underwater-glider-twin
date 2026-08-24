# Task 1 report: contain configuration content in responsive area

## Files changed

- `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
  - Added the legacy `ModelLabel` alias for `PredictionModelLabel`.
  - Moves the prefab's legacy `TitleText` into the collapsed/expanded content boundary with the other legacy configuration titles.
- `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`
  - Added focused EditMode coverage for legacy label migration, collapsed visibility, `DataInputPanel` parentage under `ConfigurationArea`, and idempotent setup.
- `.superpowers/sdd/2026-08-24-command-center-visual-fix/task-1-report.md`
  - This report.

The existing `DataInputPanel.prefab` remains unchanged. Its `ModelLabel` name was preserved because the runtime alias migrates the existing instance without overwriting unrelated prefab content or overrides.

## TDD evidence

### Red

Command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\upan\digital twin\UnderwaterGliderTwin' -runTests -testPlatform EditMode -testFilter 'UnderwaterGliderTwin.Tests.UiTests.DataInputView_MigratesLegacyModelLabelIntoExpandedContent|UnderwaterGliderTwin.Tests.UiTests.DataInputView_CollapsedConfigurationKeepsViewActiveAndHidesExpandedContent|UnderwaterGliderTwin.Tests.UiTests.DataInputPanel_RemainsUnderConfigurationAreaAfterConfigurationSetup|UnderwaterGliderTwin.Tests.UiTests.DataInputView_RepeatedConfigurationSetupDoesNotDuplicateContainers' -testResults $env:TEMP\task-1-ui-red-results.xml -logFile -
```

Result: failed as expected, 3 passed / 1 failed. `DataInputView_MigratesLegacyModelLabelIntoExpandedContent` failed at `UiTests.cs:1075` with `Expected: True; But was: False`. The shipped legacy `ModelLabel` remained outside `ConfigurationExpandedContent`, confirming the missing alias/migration behavior.

### Green and full EditMode attempts

Focused command retried with a fresh temporary XML path after implementation. Unity did not create a result XML. Its launch output reported:

```text
[Licensing::Module] Error: Access token is unavailable; failed to update
Write-Error: Unity did not produce focused test results
```

The full EditMode suite was not launched after the focused green command could not authenticate and emit a result file.

## Verification

- `git diff --check` completed with no whitespace errors.
- Manual diff review confirms the production change is confined to compatibility/containment logic; it does not deactivate the `DataInputView` root, create another `ModalRoot`, or alter the `DataInputPanel` prefab.

## Concerns

- Fresh focused and full EditMode green results remain unverified in this environment because the Unity batch editor cannot acquire an access token. Re-run the focused four tests and then the full EditMode suite in an authenticated Unity session.
- The workspace contains unrelated pre-existing modifications and untracked files; none are included in this task's commit.

## Fix round 1: reviewer coverage evidence

### Changes

- Replaced the synthetic legacy-label checks with EditMode tests that load and instantiate the shipped `Assets/UI/Prefabs/DataInputPanel.prefab` under a real screen-space canvas.
- The migrated-prefab test captures all shipped legacy configuration titles/labels (`MissionConfigurationTitle`, `TitleText`, `ModelLabel`, `SimulationLabel`, and `OceanCurrentLabel`) before binding and asserts they are contained by `ConfigurationExpandedContent` afterward.
- The collapsed-state test forces canvas updates and asserts every captured legacy title/label is outside `ConfigurationSummaryBar`, contained by `ConfigurationExpandedContent`, and has `CanvasRenderer.GetAlpha() == 0`; this verifies effective rendering alpha rather than only the ancestor `CanvasGroup` state.
- The repeated-`Bind` test now uses the shipped prefab, verifies the same migrated title objects stay in the one expanded container, asserts the summary/expanded containers are not duplicated, and invokes the real `LoadCsvButton` once to prove the listener was not duplicated.

### Verification attempts

`git diff --check` completed with no whitespace errors.

Focused EditMode command (the four Task 1 tests) was attempted on 2026-08-24. Unity exited with code `1073741845` before test discovery and did not create `C:\Users\win\AppData\Local\Temp\task-1-round-1-focused-results.xml`.

The fresh blocking output was:

```text
It looks like another Unity instance is running with this project open.

Multiple Unity instances cannot open the same project.

Project: E:/upan/digital twin/UnderwaterGliderTwin
```

The full EditMode command was also attempted and exited with code `1073741845` before test discovery, without creating `C:\Users\win\AppData\Local\Temp\task-1-round-1-full-results.xml`, with the same project-lock error. Unity additionally logged `[Licensing::Module] Error: Access token is unavailable; failed to update`, but it successfully updated/resolved the license; the terminal blocker for both test runs was the open-project lock, not a test result.

### Concerns

- Focused and full EditMode results are unverified because another Unity instance currently has `UnderwaterGliderTwin` open. Close that editor (or run these commands from its authenticated session) and rerun both commands; no green claim is made here.
