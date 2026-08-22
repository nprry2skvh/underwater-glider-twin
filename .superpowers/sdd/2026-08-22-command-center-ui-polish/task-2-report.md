# Task 2 Report - Command Center UI Polish

## RED

- Added PlayMode coverage in `EditableUiPlayModeTests.MainScene_ConfigurationSummaryAndExpandedContent_KeepDataInputViewAliveAcrossRepeatedToggles`.
- RED run: `TestResults/Task2RedPlayModeResults3.xml`.
- Result: PlayMode `8 total / 7 passed / 1 failed`.
- Failure was the intended structural failure on the existing runtime UI: the test found `DataInputPanel` and `DataInputView`, then failed because `ConfigurationExpandedContent` was missing (`Expected: not null But was: null`, `EditableUiPlayModeTests.cs:116`).
- This RED did not use a root-object active assertion as a fake failure.

## GREEN

- Implemented stable runtime structure:
  - `DataInputPanel` / `DataInputView` remain active.
  - Direct child `ConfigurationSummaryBar` is stable.
  - Direct child `ConfigurationExpandedContent` is stable.
  - Collapse/expand changes only expanded content visibility/height/interactivity.
  - Existing CSV load, prediction toggle/configuration, and simulation apply listeners remain callable.
  - Repeated binding/toggling reuses existing nodes and removes/re-adds the drawer toggle listener idempotently.
- Verification already completed before this report:
  - `scripts/test-editmode.cmd`: `418/418` EditMode tests passed.
  - Unity PlayMode `TestResults/Task2GreenPlayModeResults9.xml`: `8/8` passed.

## Repeated toggle verification

- The new PlayMode test toggles the runtime configuration drawer four times.
- After repeated toggles it asserts:
  - `DataInputPanel.activeSelf == true`
  - `DataInputView.enabled == true`
  - `ConfigurationExpandedForTests == false`
  - exactly one direct `ConfigurationSummaryBar`
  - exactly one direct `ConfigurationExpandedContent`
  - exactly one active `MissionConfigurationDrawerToggleButton`
- It also invokes CSV, prediction, and simulation actions before and after repeated toggles to verify listener callability is preserved without duplicate runtime nodes.

## Commit

- Commit message: `refactor: separate configuration summary from expanded content`

## Risk

- Kept compatibility aliases for older UI tests (`MissionConfigurationDrawerHeader`, `MissionConfigurationViewport`) while introducing the requested stable node names.
- Runtime editable object names are normalized during `DataInputView.Bind`, so prefab YAML was not changed.
- Root and expanded drawer scroll rects are both configured for compatibility with existing tests and runtime scrolling.
- Non-Task-2 dirty files in the worktree were intentionally left untouched and will not be included in the focused commit.

## Reviewer follow-up - ScrollRect ownership and listener idempotency

### RED

- Added/adjusted EditMode coverage before production changes:
  - `MissionConfigurationUsesSingleInteractiveScrollRectInExpandedContent`
  - `ParameterDrawersUseFastMouseWheelScrolling`
  - `BoundParameterDrawersUseFastMouseWheelScrolling`
  - `DataInputView_RebindDoesNotDuplicateDrawerToggleOrLoadListeners`
- RED command: `scripts/test-editmode.cmd`.
- RED result: failed as expected, `420 total / 417 passed / 3 failed`.
- Expected failures:
  - `ParameterDrawersUseFastMouseWheelScrolling`: root `MissionConfigurationPanel` still had an enabled `ScrollRect`.
  - `BoundParameterDrawersUseFastMouseWheelScrolling`: bound root panel `ScrollRect` still remained enabled.
  - `MissionConfigurationUsesSingleInteractiveScrollRectInExpandedContent`: more than one enabled mission-configuration `ScrollRect` existed under the drawer.
- The repeated Bind/listener test passed during RED, confirming the listener behavior was already implemented but previously lacked direct coverage.

### Fix

- Removed the root interactive mission-configuration `ScrollRect` compatibility path.
- The only enabled, wheel-processing mission-configuration `ScrollRect` now lives on `ConfigurationExpandedContent` and targets `ConfigurationScrollViewport` / `MissionConfigurationContent`.
- If a bound root `MissionConfigurationPanel` already carries a `ScrollRect`, `DataInputView` stops it, clears its viewport/content, disables horizontal/vertical scrolling, and disables the component so root/summary-wheel input cannot drive expanded content.
- The previous risk note about root and expanded drawer scroll rects both being configured is superseded by this follow-up.

### GREEN

- GREEN commands:
  - `scripts/test-editmode.cmd`
  - `Unity.exe -batchmode -nographics -projectPath UnderwaterGliderTwin -runTests -testPlatform PlayMode -testResults TestResults/Task2ReviewerGreenPlayModeResults.xml`
- GREEN results:
  - EditMode: `420/420` passed.
  - PlayMode: `8/8` passed (`TestResults/Task2ReviewerGreenPlayModeResults.xml`).

### Commit

- Follow-up commit message: `fix: keep configuration scrolling on expanded content`

### Risk

- Existing root `ScrollRect` components are disabled rather than destroyed, preserving serialized/component stability while preventing duplicate input handling.
- The listener-idempotency assertion is now direct: repeated `Bind` calls followed by one drawer-toggle click produce exactly one expanded-state transition, and one CSV-load click produces exactly one callback.
