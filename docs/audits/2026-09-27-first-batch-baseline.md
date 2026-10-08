# First-Batch Baseline: UI and Motion Consistency

Date: 2026-09-27
Scope: Stage 0 and Stage 1 of the digital-twin roadmap. The baseline inventory below is followed by fresh isolated-worktree verification.

## Protected Behavior

- Keep every existing control, parameter input and event binding. The approved QGroundControl Fly View layout remains the UI reference; no visual redesign is in this batch.
- Keep the accepted Native water surface and wake visual-only. Motion-source changes must be checked against wake direction, pause, reverse and seek behavior.
- Keep committed `ProfileSequence` history, latest-wins pending updates and the distinction between simulation and CSV diagnostics.
- Do not merge the abandoned `twin-state-foundation` branch or overwrite user-owned changes in the main checkout or existing worktrees.

## Current Runtime UI Path

`Main.unity` serializes `useGeneratedRuntimeUi: 1`, `allowRuntimeFallback: 0` and `strictUiValidation: 1`. `TwinBootstrap.ShouldUseGeneratedRuntimeUi()` therefore selects generated UI, and bootstrap deactivates `RuntimeUiRoot` when that path is selected. Scene prefab references still exist, but source inspection alone cannot prove which controls appear in a packaged Player. The seven UI-related PlayMode failures reported for the separate motion-consistency worktree must be classified against the *current* UI contract before either production UI or assertions change.

## Control and Input Contract

The serialized reference groups in `RuntimeUiReferences.cs` define the preservation checklist. Both generated and scene-prefab paths must be checked wherever both remain supported.

| Area | Controls and inputs to preserve |
| --- | --- |
| Playback | Play/pause, reverse, replay, reset, export, exit, 0.5/1/2/5/10 speed, progress slider. |
| Camera and visibility | Follow, global, orbit, top, side, reset, mission volume, fog, particles, trajectory visibility. |
| Mission | CSV path/load, mission longitude and latitude. |
| Prediction | XGBoost model selection, horizon, apply, enable/disable, status and metrics. |
| Simulation | Cycles, duration, target depth, water-column depth, reference cycle, heading, heading delta, pitch, roll, run/apply, flight-leg settings. |
| Flight legs | Descent/ascent net buoyancy, pitch and roll, restore defaults, close. |
| Ocean current | Depth range, east/north velocity, previous/next/add/save/delete/lookup, online/cache/local-file modes, local path, prefetch width, forecast window, drawer controls. |
| Dynamics | Sea-trial and calm-water presets, CSV calibration, mass, reference area/length, wing span, mean chord, three inertia inputs, lift slope, base drag, turnaround duration, buoyancy/roll exponents and deadbands, piston hysteresis, roll restoring gain and max roll moment. |
| Readouts | Pose, location, velocity, battery, mission time/distance, ocean/current/ground/water speed, dynamics diagnostics, alarm, prediction and mission health. |

This table is a grouped checklist, not evidence that every item is currently visible or bound. Runtime evidence must include both visibility and action checks.

## Existing Evidence and Gaps

- Main checkout Windows build log: 2026-09-27 02:39 local time. This predates this batch's changes.
- A packaged-Player screenshot was captured on 2026-09-27 at `artifacts/stage0-ui-baseline-2026-09-27.png` from that build. It visibly shows the 3D scene, water, glider, top system bar, left tool rail and bottom playback controls. It does not verify hidden drawer inputs or interaction bindings.
- Main checkout EditMode XML: 436/436 passed on 2026-09-25; PlayMode XML: 7/9 passed on 2026-09-08. Both are stale for the current dirty checkout.
- The two old main-checkout PlayMode failures are `MainScene_HasSingleEditableUiRootAtRuntime` and `MainScene_DrawersRemainUnderModalRootAndCanOpen`. Source inspection shows the first asserts that `RuntimeUI` must be absent even though the current scene enables generated UI. The second asserts legacy `ModalRoot` parentage. Both need replacement with current-runtime visibility and action checks; this is a static classification, not proof that the current drawers work.
- `codex/motion-data-consistency-integration` contains a candidate implementation of the motion plan, but is not integrated. Earlier audit recorded 528/528 EditMode and 30/37 PlayMode, with seven UI-related failures. Those results are not a passing regression gate for this checkout.
- `GliderTransformDriver` currently uses `FittedTrajectory.PositionAt()` for live position and playback velocity. `WaterSurfaceView` consumes that velocity. Replacing the pose source requires a water-interaction regression check.
- The user explicitly authorized an isolated worktree and this batch's Unity EditMode/PlayMode tests after the baseline inventory was written.

## Integrated-Checkout Evidence (2026-09-27)

- Source base: `6142262`; the main checkout had 165 dirty paths when checked. Integration lives in `.worktrees/digital-twin-stage01` on `codex/digital-twin-stage01`; no main-checkout product files were overwritten.
- The generated UI is the active `Main` scene path. A PlayMode inventory checks 43 named parameter `InputField`s, 34 named command `Button`s, the progress slider and three visibility toggles. Two drawer actions and close behavior are exercised separately. This verifies representative controls and bindings, not every possible input value or button side effect.
- Full PlayMode: 14/14 passed, including continuous motion/profile-update/reverse tests and generated UI tests. Result: `TestResults/PlayModeResults.xml` in the isolated worktree.
- Full EditMode: 474 total, 470 passed, 1 failed, 3 skipped. A red-green regression test now proves that seeking backward to a coordinate-dropout frame cannot reuse a future position. The remaining failure is `MeshBuilder_ResolvesTheInteractionAreaMoreDenselyThanTheFarField`; measured central spacing 8.74286652 versus required <1.21395111. The mesh behavior predates the motion integration and is left unchanged because the Native water appearance was previously accepted. GPU-dependent readback tests skip under Unity `-nographics` with a null graphics device; they need a graphics-capable verification pass.
- Windows build: succeeded in the isolated worktree after the final source change. `artifacts/stage1-final-player-2026-09-27.png` shows the same top bar, left tool rail, bottom controls, water and glider as the stage 0 baseline. A hidden-window screenshot was black and is not used as visual evidence.
- A real 1280x720 Player capture, `artifacts/stage1-ui-1280x720-2026-09-27.png`, shows the top bar, tool rail and playback bar without visible overlap or clipping. Text is small at this size; drawer contents were not visually inspected in this screenshot.
- Playback close-top screenshot: `artifacts/stage1-water-interaction-2026-09-27.png`. Player log recorded `playing=True`, interaction speed 0.511 m/s and depth 0.015 Unity m. The image shows a near-glider surface disturbance. This is a visual smoke check, not a physical-validity proof.
- Exhaustive interaction tests and visual inspection of every hidden drawer remain open evidence gaps. They should be completed before claiming that every UI workflow is visually validated.

## Stage 0 Exit Criteria

1. Record the exact source revision and dirty-file inventory for any build/test evidence; leave all user-owned changes intact.
2. Classify each UI PlayMode failure as current defect, obsolete assertion or unsupported UI path, with a reason and evidence.
3. Verify the preservation table against the packaged Player at the chosen viewport and at least one smaller viewport; capture any missing control or parameter as a defect, not as an intentional simplification.
4. Identify one canonical runtime UI path before changing the scene toggle. No toggle change is implied by this audit.

## Stage 1 Exit Criteria

1. Glider pose, attitude, dashboard motion readouts and motion export refer to the same authoritative committed-frame sample at the same elapsed time.
2. Fitted/smoothed trajectory geometry is presentation-only. Exact historical timestamps do not depend on later frames.
3. Coordinate units and east/down/north to Unity east/up/north mapping are applied once; missing coordinates/diagnostics stay finite and explicitly unavailable.
4. Irregular timestamps, reverse playback, seek, surface holds and profile updates preserve continuity and committed-history semantics.
5. Windows Player still exposes all protected controls and reproduces the accepted water behavior. Tests/build evidence must be fresh and attributable to the integrated checkout.

## 2026-09-29 Reverification

- The previously failing water-mesh density test reproduced at 470 passed / 1 failed / 3 skipped in `-nographics` EditMode. `WaterSurfaceMeshBuilder.MapGridCoordinate` had the dense segment fraction and dense physical extent reversed; the mapping now allocates 68% of segments to the central 28% of physical width while keeping the same topology, bounds and vertex count.
- With the corrected mapping, `-nographics` EditMode recorded 471 passed / 0 failed / 3 skipped. The three skipped wake-field tests explicitly require graphics resources. A direct graphical EditMode run passed 474/474; removing `-nographics` from `scripts/test-editmode.ps1` then produced a normal script exit 0 with 474/474.
- Full PlayMode after the mesh change passed 14/14. The Windows Player rebuild reported `Build Finished, Result: Success` without shader errors. The pre-mesh build remains at `Builds/UnderwaterGliderTwin.pre-mesh-20260929`, and the new build is at `Builds/UnderwaterGliderTwin`.
- The 2026-09-27 screenshots predate this mesh fix. They continue to document UI layout and prior water appearance, but are **not** post-fix visual evidence. A current Player screenshot and direct visual comparison remain open before claiming that the accepted water appearance is unchanged.

## 2026-09-29 Commit Safety Review

- **Do not publish the current stage 0/1 worktree as one commit.** It combines motion, UI, scene, water and runner changes; the UI/water/scene ownership and acceptance boundary are not established by the passing suites. The existing dirty worktree and pre-mesh build backup remain intact.
- Packaged startup regressed: `BuildWindows.BuildScenePaths` contains only `Main`, while `Welcome` and its bootstrap were removed. `RuntimeDataSourceState` defaults to CSV, and no `2.csv` is bundled with this worktree's build. A no-argument launch without an external CSV therefore fails initialization. On 2026-09-29 the user selected **direct simulation for no-argument launch**; implementation is still pending. Preserve explicit CSV/simulation arguments and in-app mode changes, then test the no-argument packaged path before publishing.
- The motion branch `codex/motion-data-consistency-integration` separately fixed the independently verified coordinate-dropout vector defect in commit `97a2580`: a new EditMode regression failed before the fix, then the full EditMode suite passed 529/529, full PlayMode 38/38, and Windows build succeeded. The stage 0/1 worktree does **not** yet include that fix.
- A hidden-window packaged Player on the motion branch started and exited without an error, but its screenshot is entirely black. This is startup-only evidence, not a visual acceptance check. A visible post-mesh capture is still needed for this stage 0/1 worktree.

## 2026-09-29 Default-Simulation Startup Check

- User decision: a Windows Player launch without data-source arguments enters the default parameter simulation directly; Welcome is not restored. The root workspace `DECISIONS.md` records the frozen choice.
- Root cause: `RuntimeDataSourceState.CurrentMode` initialized to CSV, while command-line application only selected simulation. With a remembered CSV path, the pre-fix Player loaded that CSV on a no-source-flag launch. A valid explicit `--csv` could not override a previously selected simulation state.
- Test-first evidence: the new EditMode case for explicit CSV failed with expected `True`, actual `False` (475/476 passed); `scripts/test-player-default-simulation.ps1` failed because the pre-fix build loaded the remembered CSV; its invalid `--csv=` probe also failed because the pre-fix build silently loaded that CSV.
- Current change: default runtime mode is simulation; valid explicit CSV selects CSV; `TwinBootstrap` validates and applies the same parsed launch request only on the first Main initialization, allowing later in-app mode changes to survive scene reload. README now matches the Main-first launch contract.
- Green evidence: graphical EditMode 476/476, full PlayMode 14/14, Windows build success. The new packaged Player passed the no-source-flag simulation smoke and the invalid CSV error smoke. A separate hidden-window explicit CSV launch exited 0, produced a screenshot file, and recorded loading 5000 CSV frames. Hidden-window screenshots are black and are **not** visual acceptance evidence.
- Previous builds are preserved at `Builds/UnderwaterGliderTwin.pre-mesh-20260929` and `Builds/UnderwaterGliderTwin.pre-startup-20260929`; the current build is `Builds/UnderwaterGliderTwin`. This dirty stage 0/1 worktree still contains broad, unreviewed UI/water/scene changes and must not be committed or published as a single batch solely on these startup test results.
- After read-only code review, the double-parse race was removed and the invalid-CSV smoke assertion was narrowed to the parser's actual error. The post-review EditMode 476/476, PlayMode 14/14, rebuilt Player, default-simulation smoke, invalid-CSV smoke and explicit-CSV smoke all passed again. The new tests restore the prior process-global runtime state after each case.

## 2026-09-29 Commit Triage

- The user authorized triage of the dirty stage 0/1 worktree and committing useful changes. This is an isolated development-branch checkpoint, not a release or merge approval. Tracked UI/scene removals are part of the Main-first generated-UI integration; repository reference search found no remaining production references to the removed legacy components. New Unity source/shader/test assets include their `.meta` files.
- Fresh verification on this exact worktree after review fixes: graphical EditMode 480/480, full PlayMode 14/14, Windows build exit 0, packaged default-simulation smoke exit 0, and invalid explicit `--csv=` smoke detecting the startup error. The tests do not prove every hidden UI control or post-mesh water appearance.
- The pure-black `stage1-ui-motion-2026-09-27.png` and near-duplicate `stage1-ui-motion-visible-2026-09-27.png` were removed as unusable/redundant generated captures. Three informative screenshots remain as dated, pre-mesh evidence only; no post-mesh visual approval is claimed.
- The independently verified coordinate-dropout vector fix was ported from `codex/motion-data-consistency-integration` with a new test. The test failed before the fix (476 passed, 1 failed) and the full 477/477 suite passed afterward; PlayMode, build and startup smokes were rerun after the port.
- Independent review identified two active regressions: the only mission-volume action was hidden, and the retained prefab binding path left mission inputs empty and skipped duration auto-correction. Both were fixed test-first: the mission action is visible and invokes its callback; the bound Main-scene prefab displays the supplied profile and runs without manual input; bound depth edits reapply duration correction. The final full-suite/build/smoke results above followed these fixes.
- Remaining release gates: inspect a visible post-mesh Player and exercise representative hidden-drawer controls in the packaged Player before merging this branch.
