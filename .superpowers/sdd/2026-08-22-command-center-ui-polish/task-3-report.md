# Task 3 Report: Command Center Root Structure and Compressed Layout

## Scope

- Kept the work inside Task 3 boundaries: three-column compressed widths, permanent `DrawerEntryLayer` entry points, shared Prefab/fallback hierarchy, single `Canvas`/`ModalRoot`, Builder idempotence, and Prefab override protection.
- Did not change the color system, configuration-area business behavior, Tooltip behavior, or panel content density.

## RED

- `E:\upan\digital twin\scripts\test-editmode.cmd`
  - Result before implementation: `EditMode tests passed: 381/381`.
  - The added EditMode Builder/root protections were already satisfied by part of the current baseline.
- `ResponsiveUiLayoutPlayModeTests`
  - Result before implementation: `result=Failed(Child) passed=2 failed=1 total=3`.
  - Failing test: `RefreshForScreen_AppliesThreeColumnWidthProfilesAtResponsiveBreakpoints`.
  - Expected failure: `TelemetryColumn must expose LayoutElement sizing; Expected: not null; But was: null`.

## Implementation

- `ResponsiveUiLayoutController.RefreshForScreen(float width, float height)` still resolves mode from only the passed dimensions and reuses existing objects.
- Three-column profiles now apply idempotently in non-drawer modes:
  - Full: telemetry `280`, viewport `minWidth=640 flexible=1`, status `320`, spacing `12`.
  - Compressed: telemetry `236`, viewport `minWidth=640 flexible=1`, status `260`, spacing `12`.
- Fallback UI creation now seeds the same full three-column LayoutElement defaults as the editable scene.
- Builder now writes matching layout components for `MainBody` and the three columns in `Main.unity`.
- Builder modal Prefab reuse now scans scene-wide by name, rejects same-name non-Prefab modals, rejects duplicate connected modal Prefabs, and reparents a valid connected modal back under `ModalRoot` without overwriting Prefab instance visual overrides.

## Prefab Protection

- Added/extended Builder tests that set special values on an existing Prefab scene instance and assert the Builder does not overwrite them:
  - `DashboardPanel` position.
  - `DashboardPanel` size.
  - `DashboardPanel` image color.
  - `DashboardPanel/TitleText` font size.
- Added same-name non-Prefab modal protection for `OceanCurrentDrawer`.
- Existing same-name non-Prefab panel protection remains in place.

## Unique Root Structure

- Generated runtime UI and editable scene tests assert:
  - `UiRoot/DrawerEntryLayer/TelemetryDrawerToggle` exists.
  - `UiRoot/DrawerEntryLayer/StatusDrawerToggle` exists.
  - `RuntimeUiRoot.DrawerLayer` is the same object as `RuntimeUiRoot.ModalRoot`.
  - Exactly one `Canvas`.
  - Exactly one `ModalRoot`.
- Builder idempotence check:
  - Ran `BuildMainScene` twice.
  - `Main.unity` SHA-256 before and after the second run: `3CE1DE99334A09D91BDDEEA2385DCE4070DDDC4A8D708C60D0D5488030592946`.
  - Exact scene-name counts after the second run: `RuntimeCanvas=1`, `UiRoot=1`, `DrawerEntryLayer=1`, `TelemetryDrawerToggle=1`, `StatusDrawerToggle=1`, `ModalRoot=1`, `DrawerScrim=1`.

## GREEN

- `E:\upan\digital twin\scripts\test-editmode.cmd`
  - Result after implementation: `EditMode tests passed: 381/381`.
- `ResponsiveUiLayoutPlayModeTests`
  - Result after implementation: `result=Passed passed=3 failed=0 total=3`.
  - Covers `1700x640`, `1280x623`, and `1296x656` through the existing policy strategy without changing the policy algorithm.
- `GeneratedRuntimeUiPlayModeTests`
  - Result after implementation: `result=Passed passed=2 failed=0 total=2`.

## Commit

- Focused commit message: `fix: align command center compressed layout and root structure`.
