# Glider Runtime Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add reliable online/local ocean-current acquisition, bounded nonlinear buoyancy/roll dynamics, transactional hot updates during simulation, and a responsive bottom parameter drawer without regressing CSV replay.

**Architecture:** Keep the existing JSON response schema and `TelemetryFrame` model. Add a bounded C# NetCDF classic reader with Python converter fallback, a shared profile validator and nonlinear actuator mappings, a simulation runtime session that replaces only validated future frames, and a responsive UI overlay that subscribes to session/source status.

**Tech Stack:** Unity C# EditMode tests, existing `IEnumerator` process seams, Python `copernicusmarine`/`xarray` converter, Unity UI `Canvas`/`ScrollRect`, NUnit.

## Global Constraints

- NetCDF direct support is limited to classic / 64-bit offset; NetCDF4/HDF5 uses the converter fallback.
- Existing JSON parser/schema remains the single normalized output boundary.
- CSV replay must keep `TelemetryFrame.Diagnostics == null`; diagnostics are display-only and never feed prediction, alarm, or trajectory logic.
- Invalid input or failed acquisition/rebuild must preserve the last valid state and must not silently produce an empty current field.
- Runtime future rebuild is asynchronous or frame-sliced; old playback continues until a complete validated replacement is ready.
- Use unique temporary directories/files; never write tests to fixed drive-root paths or mutate known-good model/current artifacts.
- Every task ends with its focused test command and a focused commit; do not stage unrelated worktree files.

---

### Task 1: Ocean-current file acquisition and cache state machine

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/OceanCurrentAcquisitionMode.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/OceanCurrentSourceIdentity.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/OceanCurrentFileLoader.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/NetCdfClassicCurrentReader.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/ICopernicusCurrentFetchBackend.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/CopernicusCurrentRequest.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/CopernicusCurrentCache.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/CopernicusCurrentClient.cs`
- Modify: `UnderwaterGliderTwin/Assets/StreamingAssets/CopernicusCurrentFetcher.py`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/OceanCurrentFileLoaderTests.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/NetCdfClassicCurrentReaderTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/CopernicusCurrentCacheTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/CopernicusCurrentClientTests.cs`

**Interfaces:**
- `OceanCurrentAcquisitionMode { Online, CacheOnly, LocalFile }`.
- `OceanCurrentFileLoader.TryLoad(string path, DateTime? referenceTimeUtc, out CopernicusCurrentResult result, out string error)`.
- `NetCdfClassicCurrentReader.TryRead(string path, DateTime referenceTimeUtc, out CopernicusCurrentResult result, out string error)`.
- `OceanCurrentSourceIdentity.ForLocalFile(string normalizedPath, string contentSha256, int schemaVersion)`.
- `IOceanCurrentFileConverter.Convert(string inputPath, string outputPath, Action onCompleted, Action<string> onFailure, Action<string> onProgress)` is the injectable process seam used only after the direct reader reports unsupported NetCDF.
- `ICopernicusCurrentFetchBackend.Run(CopernicusCurrentRequest request, string requestPath, string responsePath, Action<string> onCompleted, Action<string> onFailure, Action<string> onProgress)` is the injectable online/Python process seam used by `CopernicusCurrentClient`.
- `CopernicusCurrentClient.Fetch(request, mode, localPath, referenceTimeUtc, onSuccess, onFailure, onProgress)`; keep the old overload delegating to `Online` for callers not yet migrated.

- [ ] **Step 1: Add failing acquisition-mode and cache tests.**

```csharp
[UnityTest]
public IEnumerator LocalFileModeDoesNotInvokeRemoteBackendAndLoadsSelectedFile()
{
    var path = WriteUniqueJsonFixture();
    var backend = new SpyCurrentFetchBackend();
    var client = new CopernicusCurrentClient(backend);
    CopernicusCurrentResult loaded = null;
    yield return client.Fetch(RequestForTest(), OceanCurrentAcquisitionMode.LocalFile, path, DateTime.UtcNow,
        result => loaded = result, error => Assert.Fail(error));
    Assert.That(loaded, Is.Not.Null);
    Assert.That(backend.RunCount, Is.EqualTo(0));
}

[UnityTest]
public IEnumerator CacheOnlyModeDoesNotInvokeRemoteBackendAndUsesOnlyCache()
{
    var request = RequestForTest();
    CopernicusCurrentCache.Store(request, WriteUniqueJsonFixtureContent());
    var backend = new SpyCurrentFetchBackend();
    var client = new CopernicusCurrentClient(backend);
    CopernicusCurrentResult loaded = null;
    yield return client.Fetch(request, OceanCurrentAcquisitionMode.CacheOnly, null, DateTime.UtcNow,
        result => loaded = result, error => Assert.Fail(error));
    Assert.That(loaded, Is.Not.Null);
    Assert.That(backend.RunCount, Is.EqualTo(0));
}

[UnityTest]
public IEnumerator CacheOnlyMissFailsWithoutReplacingExistingResult()
{
    var active = new CurrentResultHolder(ParseKnownGoodResult());
    var before = active.Current;
    var backend = new SpyCurrentFetchBackend();
    var client = new CopernicusCurrentClient(backend);
    var failure = string.Empty;
    yield return client.Fetch(RequestWithUniqueCacheKey(), OceanCurrentAcquisitionMode.CacheOnly, null, DateTime.UtcNow,
        result => active.Current = result, error => failure = error);
    Assert.That(failure, Does.Contain("cache"));
    Assert.That(active.Current, Is.SameAs(before));
    Assert.That(backend.RunCount, Is.EqualTo(0));
}

[Test]
public void FileLoaderOnlyTestsParsingAndNormalization()
{
    var path = WriteUniqueJsonFixture();
    Assert.That(OceanCurrentFileLoader.TryLoad(path, DateTime.UtcNow, out var loaded, out var error), Is.True, error);
    Assert.That(loaded.Field.Samples.Count, Is.GreaterThan(0));
}

[Test]
public void CacheKeyChangesWhenLocalFileContentChanges()
{
    var first = OceanCurrentSourceIdentity.ForLocalFile("C:\\temp\\field.nc", "hash-a", 1);
    var second = OceanCurrentSourceIdentity.ForLocalFile("C:\\temp\\field.nc", "hash-b", 1);
    Assert.That(first.CacheToken, Is.Not.EqualTo(second.CacheToken));
}
```

The client tests inject `SpyCurrentFetchBackend` and separately assert that `Online` invokes it exactly once. `CurrentResultHolder` is a two-line test holder with a mutable `Current` property, so the cache-miss test proves the caller’s previous result reference is untouched. Loader tests never make mode assertions; they only test JSON/NetCDF parsing, normalization, and schema errors. Every fixture is beneath a GUID-named temporary directory and injects a fake `IOceanCurrentFileConverter`; no test starts Python or depends on credentials.

- [ ] **Step 2: Run the focused tests and verify the new APIs fail to compile or assert.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`  
Expected: FAIL in the new tests because the acquisition mode, loader, and source identity do not yet exist.

- [ ] **Step 3: Implement the normalized file loader.**

Use the existing parser as the only JSON boundary. For local files, normalize the absolute path, compute SHA-256, select the C# NetCDF reader for classic files, and invoke a converter seam for unsupported NetCDF4/HDF5. Return errors with the path, mode, and failing stage; never replace the caller’s existing result on failure.

- [ ] **Step 4: Implement the bounded NetCDF reader and fixture writer.**

Parse classic/64-bit headers, named dimensions, coordinate variables, attributes, float32/float64 arrays, and dimension order. Accept `[depth,lat,lon]` static arrays or uniquely named `[time,depth,lat,lon]` arrays. Reject missing/ambiguous mappings and unknown units. Normalize coordinates and apply the same permutation to `u`, `v`, and optional `w`; choose the nearest time using request time, simulation start, or current UTC.

- [ ] **Step 5: Add converter failure handling and cleanup.**

Run the Python converter in a unique directory under `Application.temporaryCachePath`. Treat missing Python, missing dependencies, start failure, timeout, non-zero exit, missing output, invalid JSON, and schema failure as readable errors. Write output to staging, atomically rename to the response path, parse it with `CopernicusCurrentResponseParser`, then delete request/response/staging files in a `finally` path.

- [ ] **Step 6: Fix cache identity and field preservation.**

Build the cache key from schema/provider version, dataset, variable mapping, UTC hour bucket, full bbox, depths, prefetch, forecast window, time policy, and local file content identity. Restore both `Profile` and `Field` from cache. Use a unique temporary cache file and replace only after complete JSON validation.

- [ ] **Step 7: Add tests for the complete contract.**

Cover alias variables, unit conversion, descending coordinate permutations, nearest-time selection, malformed/ambiguous dimensions, invalid time units, static 3D files, converter timeout/invalid output, cache field round-trip, and the three acquisition modes. Use unique temp directories and a fake converter/process runner; do not invoke real credentials in EditMode tests.

- [ ] **Step 8: Run and commit Task 1.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`  
Expected: all existing tests plus the new ocean-current tests pass.  
Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Telemetry UnderwaterGliderTwin/Assets/StreamingAssets/CopernicusCurrentFetcher.py UnderwaterGliderTwin/Assets/Tests/EditMode/*Current*; git commit -m "feat: add layered ocean current file acquisition"`

### Task 2: Bounded nonlinear dynamics and profile validation

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/GliderDynamicsProfileValidator.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/GliderDynamicsProfile.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/GliderDynamicsIntegrator.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationProfile.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/GliderDynamicsProfileValidatorTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/GliderDynamicsTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationTelemetrySourceTests.cs`

**Interfaces:**
- `GliderDynamicsProfileValidator.TryValidate(GliderDynamicsProfile profile, out string error)`.
- Profile fields validated by this task: existing `BuoyancyResponseSeconds` and `PistonResponseSeconds`, plus new `BuoyancyCurveExponent`, `BuoyancyDeadbandFraction`, `PistonHysteresisFraction`, `RollCurveExponent`, `RollDeadbandFraction`, `NonlinearRollRestoringGain`, and `MaxRollMomentNm`.
- `GliderDynamicsProfile.Clone()` copies every new field.

- [ ] **Step 1: Write validator and compatibility tests first.**

```csharp
[Test]
public void InvalidNonlinearParametersAreRejected()
{
    var profile = GliderDynamicsProfile.Default;
    profile.BuoyancyCurveExponent = 4f;
    Assert.That(GliderDynamicsProfileValidator.TryValidate(profile, out var error), Is.False);
    Assert.That(error, Does.Contain("BuoyancyCurveExponent"));
}

[Test]
public void LinearDefaultsRemainEquivalent()
{
    var settings = GliderDynamicsProfile.Default;
    settings.BuoyancyCurveExponent = 1f;
    settings.BuoyancyDeadbandFraction = 0f;
    settings.PistonHysteresisFraction = 0f;
    settings.RollCurveExponent = 1f;
    settings.RollDeadbandFraction = 0f;
    Assert.That(Integrate(settings), Is.EqualTo(IntegrateLegacy(settings)).Using(Within(0.0001f)));
}
```

The fixture’s `Integrate` helper calls `GliderDynamicsIntegrator.Step` with a fixed state/current/target and its `IntegrateLegacy` helper contains the pre-change linear mapping captured before editing the production file.

- [ ] **Step 2: Run the focused tests to establish failure.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`  
Expected: new validator/curve tests fail before implementation.

- [ ] **Step 3: Add bounded fields and validation.**

Enforce these exact ranges: `BuoyancyCurveExponent` is `0.5-3.0`; `BuoyancyDeadbandFraction` is `0-0.20`; `PistonHysteresisFraction` is `0-0.10` and `<= BuoyancyDeadbandFraction`; `BuoyancyResponseSeconds` and `PistonResponseSeconds` are `0.1-120`; `RollCurveExponent` is `0.5-3.0`; `RollDeadbandFraction` is `0-0.25`; moment limits are finite and non-negative. Return field-specific errors and never mutate the input profile during validation.

- [ ] **Step 4: Replace only the actuator mappings.**

Implement a signed-power helper with deadband and a direction-aware hysteresis helper. Apply them to desired piston position, net buoyancy, control-surface roll command, and nonlinear roll restoring moment. Clamp the final moment and preserve the old path exactly for linear defaults.

- [ ] **Step 5: Add regression tests.**

Assert monotonic bounded buoyancy, hysteresis on reversal, finite response under extreme valid values, nonlinear roll moment at large deflection, no NaN/Infinity, clone preservation, diagnostics populated for simulation, and diagnostics absent for CSV frames.

- [ ] **Step 6: Run and commit Task 2.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`  
Expected: existing dynamics and simulation-source tests plus new validator tests pass.  
Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Telemetry/GliderDynamics* UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationProfile.cs UnderwaterGliderTwin/Assets/Tests/EditMode/GliderDynamics* UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationTelemetrySourceTests.cs; git commit -m "feat: add bounded nonlinear glider dynamics"`

### Task 3: Transactional future-only simulation hot updates

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationStateSnapshot.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRebuildResult.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRuntimeSession.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRuntimeRegistry.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTrajectoryGenerator.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackModel.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackController.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationRuntimeSessionTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/PlaybackModelTests.cs`

**Interfaces:**
- `SimulationStateSnapshot.FromFrame(TelemetryFrame frame, SimulationProfile profile)`.
- `SimulationRuntimeSession.RequestProfileUpdate(SimulationProfile candidate)` returns `bool` and exposes `IsRebuildPending`, `LastError`, `StatusChanged`, and `ActiveProfile`.
- `SimulationRuntimeRegistry.Active` exposes the current simulation session to UI without changing the existing `DataInputView.Initialize` callback signature. `SimulationRuntimeRegistry.ActiveChanged` fires whenever `SetActive` replaces the session; `SetActive(null)` clears it and fires once.
- `PlaybackModel.ReplaceFrames(IReadOnlyList<TelemetryFrame> frames, int preservedIndex)` preserves the exact frame values and order through `preservedIndex` (the current `TelemetryFrame` is a readonly struct, so reference identity is not meaningful) and keeps playing/speed/direction state.
- `TrajectoryView.ReplaceFutureTrajectory(...)` updates only the prediction segment.

- [ ] **Step 1: Add failing playback and session transaction tests.**

```csharp
[Test]
public void ApplyProfileKeepsHistoryReferencesAndCurrentIndex()
{
    var oldFrames = BuildFrames(8);
    var model = new PlaybackModel(oldFrames, 1f);
    model.SeekNormalized(0.5f);
    var currentIndex = model.CurrentIndex;
    var historySnapshot = CaptureFrameSnapshot(model.Frames[0]);
    var fakeGenerator = new ManualFakeFutureGenerator();
    var session = CreateSessionForTest(oldFrames, fakeGenerator);
    Assert.That(session.RequestProfileUpdate(ChangedProfile), Is.True);
    fakeGenerator.CompleteWithDeterministicFuture();
    Assert.That(model.CurrentIndex, Is.EqualTo(currentIndex));
    Assert.That(CaptureFrameSnapshot(model.Frames[0]), Is.EqualTo(historySnapshot));
}
```

`CaptureFrameSnapshot` returns an immutable value containing every `TelemetryFrame` field, including nullable diagnostics. `CreateSessionForTest` returns a session wired to the supplied `PlaybackModel`; `ManualFakeFutureGenerator` captures the production completion callback and `CompleteWithDeterministicFuture` releases it after the pending-state assertions.

- [ ] **Step 2: Run focused tests and confirm transaction APIs fail.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`  
Expected: new session tests fail before the session and replacement APIs exist.

- [ ] **Step 3: Add snapshot and future-generation seam.**

Extend the trajectory generator with a seed overload that starts from `SimulationStateSnapshot` and accepts a frame-slice budget. The production scheduler yields between chunks; tests use a synchronous fake generator implementing the same callback contract.

- [ ] **Step 4: Implement atomic future replacement.**

Build a staging list from the unchanged history plus generated future. Validate frame monotonicity, finite values, and profile bounds. Commit the new list only after validation; on cancellation, timeout, invalid output, or exception discard staging and keep the old list/profile.

- [ ] **Step 5: Integrate without scene reload.**

Keep the existing simulation callback signature. `TwinBootstrap` owns the only bootstrap integration: it creates/registers the session once, and its existing `ReloadFromSimulationProfile(SimulationProfile)` method forwards to `RequestProfileUpdate` in simulation mode. It must not reload the scene; CSV reload behavior remains unchanged. Task 4 must not modify `TwinBootstrap`.

- [ ] **Step 6: Update playback and trajectory views.**

Preserve current index, speed, direction, paused/playing state, and camera. Render history and future as separate trajectory segments. Keep `TelemetryFrame.Diagnostics` nullable and ensure prediction/alarm code consumes only the normal frame fields.

- [ ] **Step 7: Add failure and performance tests.**

Cover pending status while old frames continue, successful future replacement, history-reference preservation, current position unchanged, generator failure rollback, invalid candidate rollback, cancellation, and large-frame slicing without a single-frame blocking call.

- [ ] **Step 8: Run and commit Task 3.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`  
Expected: playback, trajectory, simulation-source, and runtime-session tests pass.  
Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Telemetry/Simulation* UnderwaterGliderTwin/Assets/Scripts/Playback/Playback* UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryView.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationRuntimeSessionTests.cs UnderwaterGliderTwin/Assets/Tests/EditMode/PlaybackModelTests.cs; git commit -m "feat: support transactional simulation hot updates"`

### Task 4: Responsive bottom parameter drawer and source status UI

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.MissionSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.OceanSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.SimulationSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.DynamicsSection.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- Reuse `SimulationRuntimeRegistry.Active.StatusChanged` for pending/success/failure text; the existing simulation callback signature remains unchanged and all `TwinBootstrap` integration stays in Task 3.
- Subscribe to `SimulationRuntimeRegistry.ActiveChanged` in `OnEnable`; detach from the old session before attaching to the new one, then detach from both registry and session in `OnDisable`.
- Add local-file path and `OceanCurrentAcquisitionMode` controls that call Task 1’s acquisition API.
- Keep `MissionConfigurationPanel`, `FlightLegDrawerPanel`, and existing button names stable.

- [ ] **Step 1: Add failing layout tests.**

```csharp
[Test]
public void BottomDrawerStaysWithinViewportAt1280x720()
{
    var view = BuildRuntimeView(1280, 720);
    var drawer = view.transform.Find("Canvas/MissionConfigurationPanel") as RectTransform;
    Assert.That(drawer.rect.height, Is.LessThanOrEqualTo(720f * 0.35f));
    Assert.That(CountOutOfBoundsControls(drawer, 1280f, 720f), Is.EqualTo(0));
}

[Test]
public void InvalidRuntimeInputLeavesActiveProfileUnchanged()
{
    var harness = CreateDrawerHarness(1280, 720);
    var before = harness.Session.ActiveProfile.Dynamics.MassKg;
    harness.SetInput("DynamicsMassInput", "not-a-number");
    harness.Click("SimulationApplyButton");
    Assert.That(harness.Session.ActiveProfile.Dynamics.MassKg, Is.EqualTo(before));
}
```

`CreateDrawerHarness`, `SetInput`, and `Click` are test-fixture helpers that return the instantiated `DataInputView`, its registered runtime session, and named child controls; they are implemented in the test file before the production layout changes.

- [ ] **Step 2: Run focused UI tests and verify fixed-coordinate assumptions fail.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`  
Expected: new viewport and invalid-input tests fail against the current 1600px-wide top panel.

- [ ] **Step 3: Build the anchored bottom drawer.**

Use stretch anchors, `VerticalLayoutGroup`, `HorizontalLayoutGroup`, `ContentSizeFitter`, and a `ScrollRect`. Keep the drawer collapsed by default, cap its height at 35%, and ensure the viewport remains at least 65% of the window. Remove hard-coded coordinates that exceed the safe area.

- [ ] **Step 4: Move existing groups into the drawer.**

Place mission, current, buoyancy/roll, dynamics, and prediction controls in named sections. Keep existing event handlers and GameObject names where possible; change only layout parents and input wiring.

- [ ] **Step 5: Add source strategy and runtime status.**

Add Online/CacheOnly/LocalFile selection, local JSON/NetCDF path input, actual-source readout, pending text, and readable failure text. Subscribe/unsubscribe to `SimulationRuntimeRegistry.Active.StatusChanged` exactly once and guard all diagnostics reads with `HasValue`.

- [ ] **Step 6: Add UI cleanup and resolution regression tests.**

Test 1280×720 and 1920×1080 bounds, Chinese label overflow, drawer open/close, one-click listener behavior after repeated initialization, invalid parameter rollback, invalid current rollback, pending status, and successful status clearing.

- [ ] **Step 7: Run full verification and commit Task 4.**

Run: `& 'E:\upan\digital twin\scripts\test-editmode.cmd'`; `python -m unittest discover PredictionTraining/tests`; `& 'E:\upan\digital twin\scripts\build-windows.cmd'`.  
Then run packaged-player smoke checks in a copied build: Welcome first screen, simulation hot update, CSV replay, local JSON/NetCDF current import, missing/rejected current fallback, and `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Player.log` after moving the old log aside.  
Commit: `git add UnderwaterGliderTwin/Assets/Scripts/UI UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs; git commit -m "feat: add responsive runtime parameter drawer"`

## Final cross-task verification

- [ ] Confirm `git diff` is empty except intentional user files before release integration.
- [ ] Run the full EditMode suite and record the result XML path.
- [ ] Run Python unit tests with the repository-supported command.
- [ ] Build the Windows player from a clean output directory.
- [ ] Run packaged-player smoke tests without modifying the known-good source current/model artifacts.
- [ ] Review each task commit and update the existing pull request only after all checks pass.
