# Task 2 report: narrow command-center drawers

## Changed files

- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutPolicy.cs`
- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs`
- `UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiLayoutPolicyTests.cs`
- `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

## Policy

- Enter Drawer below `1400x624` from Compressed or Full.
- Keep Drawer until `1456x656`, then enter Compressed (or Full directly at `1616x656`).
- Enter Full from Compressed or Drawer only at `width >= 1616` and `height >= 656`.
- A Full layout exits only when `width < 1600` or `height < 640`; therefore `1616x655` remains Full only when the previous mode is Full.

## Coverage

- Boundary tests cover 1280x720, 1366x768, 1399x624, 1400x624, 1456x656, 1599x656, 1600x656, 1616x656, and the 623/624/639/640/655/656 height boundaries, including the state-dependent `1616x655` transition.
- Runtime tests verify repeated `RefreshForScreen(1280, 720)` remains Drawer and creates no GameObjects, and that entry buttons remain in `DrawerEntryLayer` while hidden side columns are inactive.

## Verification

- `git diff --check` completed without whitespace errors.
- Focused Unity EditMode test command was attempted before implementation and again for the correction. It could not run because Unity process `41324`, started before this task, already had `UnderwaterGliderTwin` open. Unity reports: `Multiple Unity instances cannot open the same project.`
- Focused and full EditMode execution remain blocked by that user-owned editor process; it was not terminated.

## Fix round 1: full-entry hysteresis correction

- Corrected the Compressed-to-Full gate to require both `width >= 1616` and `height >= 656`.
- Added the state-dependent `1616x655` regression coverage: it remains Compressed from Compressed, and remains Full from Full until the explicit Full exit floor is crossed.
- The refresh allocation test was not renamed: `UiTests.cs` already contains unrelated user changes, and the correction does not require altering that test's scope.
- A fresh focused and full EditMode invocation could not run because Unity process `41324` is still running tests with `-projectPath "E:\upan\digital twin\UnderwaterGliderTwin"`; the new full invocation wrote no test log. The process was not terminated.
