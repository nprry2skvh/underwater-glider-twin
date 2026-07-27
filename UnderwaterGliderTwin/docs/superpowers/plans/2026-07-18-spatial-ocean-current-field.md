# Spatial Ocean Current Field Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make parameter simulation sample Copernicus current data by the glider's evolving longitude, latitude, depth, and elapsed mission time.

**Architecture:** Keep the existing depth-layer profile as the deterministic fallback. Add a serializable sampled current field returned by the Copernicus fetcher, interpolate it at each dynamics step, and fall back to the nearest usable field sample or depth profile outside the downloaded coverage. Fetch a bounded mission-area grid once before simulation and cache it as the existing request cache does.

**Tech Stack:** Unity 2022.3 C#, NUnit EditMode tests, Python Copernicus Marine/xarray fetcher, JSON cache.

## Global Constraints

- The Windows build remains usable without network access through cached data and manual depth layers.
- No network request may occur inside the simulation step loop.
- A missing field sample must fall back deterministically and surface a coverage status in the existing UI.
- Existing single-location requests and cache tests remain compatible.

---

### Task 1: Spatial current field model and interpolation

**Files:**
- Create: `Assets/Scripts/Telemetry/OceanCurrentField.cs`
- Modify: `Assets/Tests/EditMode/OceanCurrentProfileTests.cs`

- [ ] Write failing NUnit tests for a field that returns different east/north velocity at two positions and for an out-of-field fallback result.
- [ ] Add `OceanCurrentFieldSample` and `OceanCurrentField.TryGetVelocity(longitude, latitude, depth, elapsedSeconds, out Vector2)` using nearest valid samples weighted by horizontal, depth, and elapsed-time distance.
- [ ] Run the EditMode suite and verify the new tests pass.

### Task 2: Request, response, cache, and fetcher grid support

**Files:**
- Modify: `Assets/Scripts/Telemetry/CopernicusCurrentRequest.cs`
- Modify: `Assets/Scripts/Telemetry/CopernicusCurrentCache.cs`
- Modify: `Assets/Scripts/Telemetry/CopernicusCurrentResponseParser.cs`
- Modify: `Assets/StreamingAssets/CopernicusCurrentFetcher.py`
- Modify: `Assets/Tests/EditMode/CopernicusCurrentCacheTests.cs`
- Modify: `Assets/Tests/EditMode/CopernicusCurrentResponseParserTests.cs`

- [ ] Write failing tests proving region/time request values change cache keys and JSON field samples are parsed.
- [ ] Extend the request with region bounds and forecast duration while preserving the point-request constructor.
- [ ] Fetch the rectangular longitude/latitude/depth/time subset, serialize finite samples plus the legacy depth layers, and parse them into `CopernicusCurrentResult`.
- [ ] Run the EditMode suite and verify request/parser/cache coverage passes.

### Task 3: Simulation sampling and fallback diagnostics

**Files:**
- Modify: `Assets/Scripts/Telemetry/SimulationProfile.cs`
- Modify: `Assets/Scripts/Telemetry/SimulationTrajectoryGenerator.cs`
- Modify: `Assets/Tests/EditMode/SimulationTelemetrySourceTests.cs`

- [ ] Write a failing simulation test with two spatial samples that changes the glider's ground velocity after it crosses into the second sample region.
- [ ] Store the optional field on `SimulationProfile`, sample it using current longitude/latitude/depth/elapsed time before every dynamics integration, and fall back to `OceanCurrentProfile` when unavailable.
- [ ] Run the EditMode suite and verify trajectory behavior and legacy fallback tests pass.

### Task 4: Configuration UI and coverage feedback

**Files:**
- Modify: `Assets/Scripts/UI/DataInputView.cs`
- Modify: `Assets/Tests/EditMode/UiTests.cs`

- [ ] Write a failing UI test for regional fetch defaults and a post-fetch coverage summary.
- [ ] Add ocean-current drawer inputs for prefetch half-width and forecast window, use them to construct the request, persist them to the simulation profile, and display the cached grid coverage/status.
- [ ] Run the EditMode suite and verify UI tests pass.

### Task 5: Full verification

**Files:**
- No production file changes expected.

- [ ] Run `powershell -ExecutionPolicy Bypass -File ..\scripts\test-editmode.ps1` from `UnderwaterGliderTwin`.
- [ ] Run `powershell -ExecutionPolicy Bypass -File ..\scripts\build-windows.ps1` from `UnderwaterGliderTwin`.
- [ ] Inspect both outputs before reporting completion.
