# Continuous Parameter Trajectory and Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将参数仿真改为支持多次热更新和自动补片的连续时间线，并从同一不可变导出快照生成可重放 JSON、分析 CSV、右手 GLB 和一致 PNG。

**Architecture:** `SimulationTrajectoryTimeline` 是 committed frames、参数段和 revision 的唯一数据源；`PlaybackModel` 只保存游标和播放状态并引用 Timeline 发布的不可变快照。`SimulationRuntimeSession` 负责 latest-wins 请求、提交线性点、连续补片和完成状态；所有轨迹推进继续调用 `SimulationMissionStepper`。`TrajectoryExportService` 从时间线快照生成四种输出和第五个 manifest，PNG 使用独立 snapshot render view。

**Tech Stack:** Unity 2022.3.62f1、C#、Unity Test Framework/NUnit、`LineRenderer`/`RenderTexture`、System.IO、Unity `ImageConversion` 和现有 `PlaybackModel`/`PredictionController`。

**Spec:** `docs/superpowers/specs/2026-08-31-continuous-trajectory-export-design.md`

## Global Constraints

- 仅修改 `RuntimeDataSourceMode.Simulation`；CSV 回放路径保持现有行为。
- 初始 profile 使用 `ProfileSequence = 0`；成功提交的变更从 `1` 起递增。
- 新有效请求在当前请求原子提交线性点前到达时，使旧 in-flight 结果 stale；旧 coroutine 可完成，但回调不得提交、不得标记成功或取消成功。
- 无效请求不覆盖有效 pending；被合并、取消、失败、超时或 stale 的请求不生成参数段、不进入导出。
- `requestId` 只写入成功提交的参数段；内部版本号可以有间隙，但导出不能暴露未成功请求。
- 参数段起点使用成功提交后生成的第一帧 future 的 `RowIndex` 和 `ElapsedSeconds`。
- Timeline 是 committed frames 的唯一数据源；任何 future 替换或追加必须走同一原子提交入口。
- `CycleCount` 是任务总航段上限；运行时只允许保持或增加，达到上限进入 `Completed`。
- 自动补片条件是 `futureDurationSeconds < 120` 或 `futureFrameCount < 128`，且尚未 `Completed`。
- 全局安全上限为 `MaximumTimelineFrameCount = 200000`；超过上限拒绝启动或更新。
- ENU 是物理米制坐标；GLB 使用右手 Y-up：`X=East`、`Y=Up=-Depth`、`Z=-North`。
- JSON 浮点必须有限；缺失的计划坐标和可选诊断值写为 `null`。
- 导出包包含 JSON、CSV、GLB、PNG 四种输出及第五个控制文件 `manifest.json`；manifest 不写自身 hash。
- 原始工作区已有未提交改动；执行者不得 reset、checkout 或覆盖无关改动，所有实现继续在 `E:/upan/digital twin/.worktrees/unified-hybrid-trajectory`。

## File Map

- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineStatus.cs`: 时间线状态枚举，包括 `Idle`、`Queued`、`Generating`、`WaitingForFuture`、`Committed`、`Completed`、`Cancelled`、`Failed`。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineSegment.cs`: 成功提交的 profile 段及其起始 frame、时间和 requestId。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineSnapshot.cs`: 不可变 frame、segment、revision、mission 和 playback 导出视图。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTrajectoryTimeline.cs`: 唯一 committed frame 数据源和原子 replace/append 入口。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TelemetryFrame.cs`: 增加显式 `ProfileSequence` 和可选物理坐标辅助字段的兼容构造。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackModel.cs`: 引用不可变 Timeline snapshot，增加尾部等待、替换和追加操作。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackController.cs`: 按固定事件顺序发布 timeline 变更和 rebuild frame 事件。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRuntimeSession.cs`: 请求 latest-wins、stale 回调、自动补片和完成状态。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTrajectoryGenerator.cs`: 支持有界未来窗口和 continuation state。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationMissionStepper.cs`: 输出/恢复 ProfileSequence 所需状态，支持窗口化推进。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationProfile.cs` and `GliderDynamicsProfileValidator.cs`: 运行时 CycleCount、最大帧数和原点修改规则。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Prediction/PredictionController.cs`: rebuild 事件中先刷新 frame 引用，再计算预测。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryView.cs`: 按 ProfileSequence 保留历史/未来/参数段，消费 Timeline snapshot。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`: 用 Timeline 构造 simulation playback，并注册连续补片和导出服务。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportSnapshot.cs`: 导出所需的不可变 timeline、profile、prediction、camera 和 playback 快照。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportService.cs`: staging、四输出、manifest、hash 和异步状态编排。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryJsonCodec.cs` and `TrajectoryJsonImporter.cs`: 完整 JSON 序列化/校验/恢复。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryCsvWriter.cs`: 固定 UTF-8 BOM、列顺序和格式的 CSV writer。
- Create `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportManifest.cs`: 五项包清单及文件 hash。
- Define immutable `TrajectoryPlaybackState` and `CameraSnapshot` DTOs in `TrajectoryExportSnapshot.cs`; reuse the existing `PredictionSnapshot` type for the optional prediction view.
- Define `TrajectoryExportRequest`, `TrajectoryExportStatus`, `TrajectoryExportResult` and `TrajectoryExportPhase` in `TrajectoryExportService.cs`, including the published/staging directories and four output entries.
- Create `UnderwaterGliderTwin/Assets/Scripts/Visualization/GlbCoordinateMapper.cs`, `GlbTrajectoryWriter.cs`, `TrajectoryMeshDecimator.cs`: GLB 坐标、抽稀和可见轨迹网格。
- Create `UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryExportRenderView.cs`: 独立 snapshot 几何、离屏相机和 PNG 生成。
- Modify `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/RuntimeScreenshotCapture.cs`: 保持 F12 单图功能，新增完成/失败回调接口供完整导出使用。
- Modify `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`: 异步“导出全部”状态和结果显示。
- Create/modify tests under `UnderwaterGliderTwin/Assets/Tests/EditMode` and `Assets/Tests/PlayMode` for every task below.

---

### Task 1: Introduce Timeline Contracts and Single-Source Playback

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineStatus.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineSegment.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineSnapshot.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTrajectoryTimeline.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TelemetryFrame.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackModel.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackController.cs`
- Create: `UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationTrajectoryTimelineTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/PlaybackModelTests.cs`

**Interfaces:**
- `SimulationTimelineSegment(int profileSequence, long requestId, int startRowIndex, float startElapsedSeconds, SimulationProfile profile, DateTime committedAtUtc)`。
- `SimulationTimelineSnapshot(IReadOnlyList<TelemetryFrame> frames, IReadOnlyList<SimulationTimelineSegment> segments, int revision, SimulationTimelineStatus status, SimulationMissionState missionState = null, TrajectoryPlaybackState playback = null)`，构造后所有列表只读，并以只读属性暴露 mission/playback 导出视图。
- `SimulationTrajectoryTimeline.CommittedSnapshot`、`Revision`、`Changed`。
- `SimulationTrajectoryTimeline.ReplaceFutureFrom(int preservedIndex, IReadOnlyList<TelemetryFrame> future, SimulationTimelineSegment segment)`。
- `SimulationTrajectoryTimeline.AppendFuture(IReadOnlyList<TelemetryFrame> future)`，沿用最后一个 ProfileSequence，不新增 segment。
- `PlaybackModel.BindTimeline(SimulationTrajectoryTimeline timeline)`、`HasFutureHorizon(float seconds, int minimumFrames)`、`IsWaitingForFuture`。
- `TelemetryFrame.ProfileSequence` 的旧构造调用默认值为 `0`。

- [ ] **Step 1: Write failing tests for explicit ProfileSequence and atomic timeline operations.**

```csharp
[Test]
public void ReplaceFutureCommitsOneSegmentAndPreservesHistory()
{
    var history = new[] { Frame(0, 0f, 0), Frame(1, 5f, 0) };
    var timeline = new SimulationTrajectoryTimeline(history, new SimulationTimelineSegment(0, 0, 0, 0f, Profile(), DateTime.UtcNow));
    var future = new[] { Frame(2, 10f, 1), Frame(3, 15f, 1) };

    timeline.ReplaceFutureFrom(1, future, new SimulationTimelineSegment(1, 42, 2, 10f, Profile(), DateTime.UtcNow));

    Assert.That(timeline.CommittedSnapshot.Frames.Count, Is.EqualTo(4));
    Assert.That(timeline.CommittedSnapshot.Frames[0].ProfileSequence, Is.EqualTo(0));
    Assert.That(timeline.CommittedSnapshot.Frames[2].ProfileSequence, Is.EqualTo(1));
    Assert.That(timeline.CommittedSnapshot.Segments.Count, Is.EqualTo(2));
}

[Test]
public void AppendFutureReusesCurrentProfileSequenceAndIncrementsRevision()
{
    var timeline = TimelineWithFrames(4, profileSequence: 2);
    var revision = timeline.Revision;

    timeline.AppendFuture(new[] { Frame(4, 20f, 2) });

    Assert.That(timeline.CommittedSnapshot.Frames[4].ProfileSequence, Is.EqualTo(2));
    Assert.That(timeline.Revision, Is.EqualTo(revision + 1));
    Assert.That(timeline.CommittedSnapshot.Segments.Count, Is.EqualTo(1));
}
```

测试夹具在 `SimulationTrajectoryTimelineTests.cs` 内定义：`Frame(int rowIndex, float elapsedSeconds, int profileSequence)` 返回带确定性坐标的 `TelemetryFrame`；`Profile()` 返回通过验证的最小 `SimulationProfile`；`TimelineWithFrames(int frameCount, int profileSequence)` 创建初始段并生成连续行号的时间线。夹具只服务于测试，不进入运行时代码。

- [ ] **Step 2: Run the focused EditMode tests and verify they fail for missing contracts.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: FAIL in `SimulationTrajectoryTimelineTests` with missing `ProfileSequence`/Timeline API or incorrect atomic result.

- [ ] **Step 3: Implement the minimal immutable snapshot and atomic commit path.**

Store the committed frame list in `SimulationTrajectoryTimeline`; return read-only views. Validate preserved history byte-for-byte, strictly increasing `RowIndex`/`ElapsedSeconds`, nondecreasing `ProfileSequence`, and segment start equal to the first future frame. Replace/append by publishing one new snapshot and incrementing `Revision`; never mutate a previously published list.

- [ ] **Step 4: Route PlaybackModel through the Timeline snapshot and fix event order.**

`PlaybackModel` may retain only the current immutable list reference and cursor state. On timeline commit, update the reference before firing `FramesReplaced`. `PlaybackController` must publish in this order: `TimelineChanged`, `FramesReplaced`, `FrameChangedWithReason(Rebuild)`, compatibility `FrameChanged`. Add tests that a subscriber sees the new frame list during every event.

- [ ] **Step 5: Run the full EditMode suite and commit the contract layer.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: all existing tests plus the new timeline tests pass.

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineStatus.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineSegment.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTimelineSnapshot.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTrajectoryTimeline.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/TelemetryFrame.cs UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackModel.cs UnderwaterGliderTwin/Assets/Scripts/Playback/PlaybackController.cs UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationTrajectoryTimelineTests.cs UnderwaterGliderTwin/Assets/Tests/EditMode/PlaybackModelTests.cs && git commit -m "feat: add single-source simulation timeline"`

### Task 2: Implement Continuous Generation, Stale Requests, and Auto-Refill

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRuntimeSession.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTrajectoryGenerator.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationMissionStepper.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRebuildResult.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationProfile.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/GliderDynamicsProfileValidator.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationRuntimeSessionTests.cs`

**Interfaces:**
- Extend `ISimulationFutureGenerator.GenerateFuture` with `int maximumFrameCount` while retaining the existing completion callback shape.
- `SimulationRuntimeSession.RequestProfileUpdate(SimulationProfile candidate)` remains the public entry point.
- Add `SimulationRuntimeSession.TryEnsureFutureHorizon()` and `SimulationRuntimeSession.Status`.
- Add `SimulationRuntimeSession.StatusChanged`, `CancelPendingUpdate()`, and an injectable UTC clock for deterministic deadline tests.
- `SimulationRebuildResult` carries only frames and success/error/cancel flags; requestId and ProfileSequence remain session-owned.
- `SimulationTrajectoryGenerator.GenerateFutureSlices(SimulationStateSnapshot snapshot, SimulationProfile profile, int frameSliceBudget, int maximumFrameCount)` returns bounded slices and stops at mission completion or the supplied frame budget.

- [ ] **Step 1: Add failing tests for the P0 linearization rule and request coalescing.**

```csharp
[Test]
public void ValidRequestBeforeFirstCommitMakesInFlightResultStale()
{
    var generator = new ManualFakeFutureGenerator();
    var model = new PlaybackModel(BuildFrames(8), 1f);
    var session = CreateSessionForTest(model, generator);

    Assert.That(session.RequestProfileUpdate(ProfileWithDepth(100f)), Is.True);
    Assert.That(session.RequestProfileUpdate(ProfileWithDepth(140f)), Is.True);
    generator.CompleteAt(0, FutureFor(model.CurrentFrame, 1));

    Assert.That(session.QueuedProfile.TargetDepthM, Is.EqualTo(140f));
    Assert.That(session.Timeline.Segments.Count, Is.EqualTo(0));
    generator.CompleteAt(1, FutureFor(model.CurrentFrame, 2));
    Assert.That(session.Timeline.Segments.Count, Is.EqualTo(1));
}

[Test]
public void InvalidRequestDoesNotReplaceValidQueuedCandidate()
{
    var generator = new ManualFakeFutureGenerator();
    var session = CreateSessionForTest(new PlaybackModel(BuildFrames(8), 1f), generator);
    Assert.That(session.RequestProfileUpdate(ProfileWithDepth(100f)), Is.True);
    Assert.That(session.RequestProfileUpdate(ProfileWithDepth(-1f)), Is.False);
    Assert.That(session.QueuedProfile.TargetDepthM, Is.EqualTo(100f));
}
```

测试夹具在 `SimulationRuntimeSessionTests.cs` 内定义：`BuildFrames(int count)`、`CreateSessionForTest(PlaybackModel model, ManualFakeFutureGenerator generator)`、`ProfileWithDepth(float depthM)` 和 `FutureFor(TelemetryFrame frame, int profileSequence)`；`ManualFakeFutureGenerator.CompleteAt` 必须按请求启动顺序触发回调，并保留回调后的新请求，便于验证 stale 丢弃。运行时接口提供只读 `Timeline`、`QueuedProfile` 和 `Status` 属性。

同一测试文件还提供 `FakeUtcClock` 和 `CaptureStatuses(session)`：分别用于把时间推进到“超过默认 30 秒但未超过按工作量计算的 deadline”以及“到达 deadline”；长任务 deadline 必须不超过 5 分钟。状态断言覆盖 `Queued/Generating/Committed`、`Cancelled`、`Failed` 和 `WaitingForFuture` 的顺序，迟到的旧取消/完成回调不得追加状态或 segment。

- [ ] **Step 2: Run the focused tests and verify the existing reject-on-pending behavior fails the new contract.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: FAIL because the current session rejects a second valid request and has no Timeline segment lifecycle.

- [ ] **Step 3: Add request lifecycle state with a commit linearization point.**

Assign an internal monotonically increasing request version when a valid candidate is accepted. When another valid candidate arrives before commit, bump the version, mark the old operation stale, cancel it if possible, and retain only the newest queued candidate. In `CompleteRequest`, first reject any callback whose version is not current; only after future validation and Timeline atomic publication assign `ProfileSequence` and persist `requestId` in the segment.

- [ ] **Step 4: Add bounded generation and continuation state.**

Keep `GenerateFrames(profile)` deterministic for offline full generation. Add a bounded future generation path that returns at most `maximumFrameCount` frames and a continuation snapshot. Use the current `SimulationMissionStepper` mission state, not elapsed-time phase inference, and stamp every generated frame with the active ProfileSequence.

- [ ] **Step 5: Implement horizon refill and tail waiting.**

In `Tick`, call `TryEnsureFutureHorizon()` only when `futureDurationSeconds < 120 || futureFrameCount < 128`, `CompletedCycles < CycleCount`, no in-flight operation exists, and the frame cap is not reached. If playback reaches the committed tail while generation is pending, set `WaitingForFuture`, stop advancing the cursor, preserve `resumeAfterFuture`, and resume only after append success. After `Completed`, never refill.

- [ ] **Step 6: Freeze profile boundary rules, deadline and completion tests.**

Reject a running profile whose `CycleCount` is lower than the current value or whose origin differs. Permit equal/increased CycleCount only. Validate projected total frames against `200000`. Preserve the existing workload-based deadline: it is at least the configured timeout, scales with the estimated workload, and is capped at 5 minutes. Add tests for `Completed`, `WaitingForFuture`, speed/direction preservation, no segment for automatic append, cancellation/failure/timeout without sequence increment, and late callback rejection.

- [ ] **Step 7: Run EditMode and commit the continuous runtime.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: all tests pass, including three-request latest-wins and stale callback tests.

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRuntimeSession.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationTrajectoryGenerator.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationMissionStepper.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationRebuildResult.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/SimulationProfile.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/GliderDynamicsProfileValidator.cs UnderwaterGliderTwin/Assets/Tests/EditMode/SimulationRuntimeSessionTests.cs && git commit -m "feat: support continuous parameter simulation"`

### Task 3: Integrate Prediction, Visualization, and Bootstrap with Timeline Events

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Prediction/PredictionController.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/RuntimeDataSourceState.cs` only if simulation timeline ownership requires state cleanup.
- Create: `UnderwaterGliderTwin/Assets/Tests/PlayMode/ContinuousSimulationBootstrapTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/PredictionTests.cs` and relevant visualization tests.

**Interfaces:**
- `PredictionController.BindTimeline(SimulationTrajectoryTimeline timeline)` updates its frame source before rebuild recomputation.
- `TrajectoryView.ReplaceFutureTrajectory(SimulationTimelineSnapshot snapshot, int preservedIndex)` renders committed history and future without replacing its GameObject or camera.
- `TwinBootstrap` creates the Timeline before `PlaybackModel`, registers one session, and forwards `ReloadFromSimulationProfile` to the session in simulation mode.

- [ ] **Step 1: Add failing integration tests for repeated updates and object identity.**

```csharp
[UnityTest]
public IEnumerator RepeatedSimulationUpdatesKeepSceneAndCameraIdentity()
{
    yield return LoadMainSimulationScene();
    var bootstrap = Object.FindObjectOfType<TwinBootstrap>();
    var scenePath = SceneManager.GetActiveScene().path;
    var cameraId = Camera.main.GetInstanceID();
    var cameraPosition = Camera.main.transform.position;
    var cameraRotation = Camera.main.transform.rotation;
    var cameraFov = Camera.main.fieldOfView;
    var playbackId = bootstrap.PlaybackController.GetInstanceID();

    yield return ApplyAndWait(bootstrap, ProfileWithDepth(110f));
    yield return ApplyAndWait(bootstrap, ProfileWithDepth(140f));
    yield return ApplyAndWait(bootstrap, ProfileWithDepth(180f));

    Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
    Assert.That(Camera.main.GetInstanceID(), Is.EqualTo(cameraId));
    Assert.That(Camera.main.transform.position, Is.EqualTo(cameraPosition));
    Assert.That(Camera.main.transform.rotation, Is.EqualTo(cameraRotation));
    Assert.That(Camera.main.fieldOfView, Is.EqualTo(cameraFov));
    Assert.That(bootstrap.PlaybackController.GetInstanceID(), Is.EqualTo(playbackId));
    Assert.That(bootstrap.SimulationSession.Timeline.Segments.Count, Is.EqualTo(3));
}
```

同一 PlayMode 文件增加生产异步链路测试：通过 Main 场景中注册的真实 `SimulationFutureTrajectoryGenerator` 发起一个足够大的 future，确认多个 frame slice 之间主线程继续推进；随后调用取消并等待旧 coroutine 回调，确认没有 segment、没有成功/取消成功伪造状态，且 active scene、Camera 与 `PlaybackController` 身份不变。该测试不得替换为 `ManualFakeFutureGenerator`。

测试夹具在 `ContinuousSimulationBootstrapTests.cs` 内定义：`LoadMainSimulationScene()` 只加载一次 Main 场景；`ApplyAndWait(TwinBootstrap bootstrap, SimulationProfile profile)` 只调用公开的 `ReloadFromSimulationProfile` 并逐帧等待 `StatusChanged(Committed)`；`ProfileWithDepth` 和 `WaitUntilTimelineHasFuture` 使用同一测试的超时保护。测试还记录当前帧、连续索引和历史前缀，在每次更新后验证它们未跳变。

- [ ] **Step 2: Run the focused PlayMode test and verify the current single-rebuild integration fails.**

Run: `Unity.exe -batchmode -nographics -projectPath UnderwaterGliderTwin -runTests -testPlatform PlayMode -testFilter UnderwaterGliderTwin.Tests.ContinuousSimulationBootstrapTests -testResults TestResults/ContinuousSimulationBootstrap.xml -logFile TestResults/ContinuousSimulationBootstrap.log`
Expected: FAIL until Timeline registration, repeated update handling, and event order exist.

- [ ] **Step 3: Change Bootstrap construction order and event subscriptions.**

Construct `SimulationTrajectoryTimeline` from the initial simulation frames, bind `PlaybackModel` to it, then initialize PredictionController and TrajectoryView against the same snapshot. Keep CSV initialization unchanged. Ensure `OnDestroy` unsubscribes session, timeline, prediction, and view handlers.

- [ ] **Step 4: Update PredictionController and TrajectoryView for the fixed event order.**

PredictionController refreshes its frame reference on `FramesReplaced` and computes only after `FrameChangedWithReason(Rebuild)`. TrajectoryView maintains immutable rendered history buffers and rebuilds only the future/parameter segment geometry. No update may create a second camera, PlaybackController, or scene.

- [ ] **Step 5: Run focused PlayMode, then EditMode and commit integration.**

Run the focused PlayMode command from Step 2, then `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`.
Expected: repeated updates pass; all EditMode tests remain green.

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Prediction/PredictionController.cs UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryView.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/ContinuousSimulationBootstrapTests.cs && git commit -m "feat: integrate continuous timeline with playback"`

### Task 4: Build Immutable Export Snapshot, JSON Contract, CSV, and Importer

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportSnapshot.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryJsonCodec.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryJsonImporter.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryCsvWriter.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportManifest.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportService.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/RuntimePathResolver.cs` if an export package directory helper is needed.
- Create: `UnderwaterGliderTwin/Assets/Tests/EditMode/TrajectoryExportCodecTests.cs`

**Interfaces:**
- `TrajectoryExportSnapshot.Capture(SimulationTimelineSnapshot timeline, PlaybackModel playback, PredictionSnapshot prediction, CameraSnapshot camera)`.
- `TrajectoryJsonCodec.Serialize(TrajectoryExportSnapshot snapshot)` and `Deserialize(string json)`.
- `TrajectoryJsonImporter.TryRestoreJson(string json, out SimulationTimelineSnapshot timeline, out string error)` for in-memory round-trip tests, in addition to the package-directory importer.
- `TrajectoryJsonImporter.TryRestore(string packageDirectory, out SimulationTrajectoryTimeline timeline, out TrajectoryPlaybackState playback, out string error)`.
- `TrajectoryCsvWriter.Write(TextWriter writer, TrajectoryExportSnapshot snapshot)`.
- `TrajectoryExportService.BeginExport(TrajectoryExportSnapshot snapshot, string exportRoot, Action<TrajectoryExportStatus> progress, Action<TrajectoryExportResult> completed)`.

`CameraSnapshot` 包含位置、旋转、FOV、渲染尺寸和可见层；`TrajectoryPlaybackState` 包含当前帧、连续索引、连续 elapsed、播放/速度/方向。`TrajectoryExportResult.PublishedDirectory` 只在发布成功后非空；失败时同时返回清理后的 `StagingDirectory` 状态，便于断言无残留。

- [ ] **Step 1: Add failing codec and round-trip tests.**

```csharp
[Test]
public void JsonRoundTripPreservesSegmentsFramesMissionAndPlayback()
{
    var snapshot = BuildExportSnapshotWithTwoSuccessfulSegments();
    var json = TrajectoryJsonCodec.Serialize(snapshot);

    Assert.That(json, Does.Contain("\"profileSequence\":1"));
    Assert.That(json, Does.Not.Contain("NaN"));
    Assert.That(json, Does.Not.Contain("Infinity"));
    Assert.That(TrajectoryJsonImporter.TryRestoreJson(json, out var restored, out var error), Is.True, error);
    Assert.That(restored.Frames.Count, Is.EqualTo(snapshot.Timeline.Frames.Count));
    Assert.That(restored.Segments.Count, Is.EqualTo(snapshot.Timeline.Segments.Count));
    Assert.That(restored.Playback.CurrentFrameIndex, Is.EqualTo(snapshot.Playback.CurrentFrameIndex));
}

[Test]
public void CsvUsesFixedHeaderAndUtf8BomContract()
{
    var bytes = WriteCsvBytes(BuildExportSnapshotWithMissingPlannedCoordinates());
    Assert.That(bytes[0], Is.EqualTo(0xEF));
    Assert.That(bytes[1], Is.EqualTo(0xBB));
    Assert.That(bytes[2], Is.EqualTo(0xBF));
    Assert.That(DecodeUtf8(bytes), Does.Contain("EastM,NorthM,UpM,DepthM"));
}
```

测试夹具在 `TrajectoryExportCodecTests.cs` 内定义：`BuildExportSnapshotWithTwoSuccessfulSegments()`、`BuildExportSnapshotWithMissingPlannedCoordinates()`、`WriteCsvBytes(TrajectoryExportSnapshot snapshot)` 和 `DecodeUtf8(byte[] bytes)`；它们返回包含固定 revision、mission、playback、profile segments 和一条缺失可选坐标的确定性快照。

- [ ] **Step 2: Run the focused EditMode tests and verify they fail without codecs.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: FAIL with missing codec/importer/writer types.

- [ ] **Step 3: Implement deterministic export DTOs and finite-value normalization.**

Serialize the complete `SimulationProfile`, nested `Dynamics`, ocean profile/field/source preference, algorithm version, integration step, sample interval, mission state, timeline revision, playback state, each frame’s `ProfileSequence`, and optional prediction snapshot. Emit `enu.eastM`, `enu.northM`, `enu.upM`, `depthM`, and `null` for absent optional values. Use a custom writer/parser rather than Unity `JsonUtility` so nullable values and exact field order are controlled.

- [ ] **Step 4: Implement CSV with the fixed schema.**

Write UTF-8 with BOM, comma separators, RFC-style quoted fields, empty missing values, InvariantCulture numbers with at most six fractional digits, and the exact header from the spec. Treat JSON as the only lossless replay source.

- [ ] **Step 5: Implement importer validation and deterministic re-export.**

Reject unsupported schema/algorithm versions, nonfinite numbers, invalid coordinate contract, nonmonotonic rows, missing segment starts, ProfileSequence regressions, and mismatched ocean source hashes. Restore Timeline, segments, mission state, revision, and playback controls. Add export-import-export normalization tests.

- [ ] **Step 6: Run EditMode and commit the data export layer.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: codec, CSV, importer and all existing tests pass.

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportSnapshot.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryJsonCodec.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryJsonImporter.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryCsvWriter.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportManifest.cs UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportService.cs UnderwaterGliderTwin/Assets/Tests/EditMode/TrajectoryExportCodecTests.cs && git commit -m "feat: add replayable trajectory export data"`

### Task 5: Implement Right-Hand GLB Geometry and Isolated PNG Rendering

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/Visualization/GlbCoordinateMapper.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Visualization/GlbTrajectoryWriter.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryMeshDecimator.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryExportRenderView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/RuntimeScreenshotCapture.cs`
- Create: `UnderwaterGliderTwin/Assets/Tests/EditMode/GlbTrajectoryWriterTests.cs`
- Create: `UnderwaterGliderTwin/Assets/Tests/PlayMode/TrajectoryExportRenderTests.cs`

**Interfaces:**
- `GlbCoordinateMapper.ToGlbPosition(double eastM, double northM, double depthM)` returns `Vector3(eastM, -depthM, -northM)`.
- `GlbTrajectoryWriter.Write(Stream output, TrajectoryExportSnapshot snapshot, int vertexBudget)`.
- `TrajectoryMeshDecimator.Decimate(IReadOnlyList<Vector3> source, int budget, IReadOnlyCollection<int> requiredBoundaryIndices)`.
- `TrajectoryExportRenderView.CaptureAsync(TrajectoryExportSnapshot snapshot, string pngPath, Action<bool, string> completed)`.

- [ ] **Step 1: Add failing known-point and GLB structure tests.**

```csharp
[Test]
public void GlbCoordinateMappingUsesRightHandedYUpContract()
{
    var result = GlbCoordinateMapper.ToGlbPosition(1d, 2d, 3d);
    Assert.That(result.x, Is.EqualTo(1f));
    Assert.That(result.y, Is.EqualTo(-3f));
    Assert.That(result.z, Is.EqualTo(-2f));
}

[Test]
public void GlbWriterPreservesSegmentBoundariesAndVertexBudget()
{
    using (var output = new MemoryStream())
    {
        GlbTrajectoryWriter.Write(output, BuildExportSnapshotWithTwoSuccessfulSegments(), 32);
        var bytes = output.ToArray();
        Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("glTF"));
        Assert.That(ReadGlbChunkLengths(bytes).All(length => length >= 0), Is.True);
        Assert.That(ReadGlbNodeNames(bytes), Does.Contain("ProfileSequence-1"));
    }
}
```

`GlbTrajectoryWriterTests.cs` 内定义 `BuildExportSnapshotWithTwoSuccessfulSegments()`、`ReadGlbChunkLengths(byte[] bytes)`（返回 `IReadOnlyList<int>`）和 `ReadGlbNodeNames(byte[] bytes)`；解析器只检查 GLB 容器结构与节点元数据，不以字符串偶然匹配替代二进制边界断言。

- [ ] **Step 2: Run the focused EditMode tests and verify missing writer/mapping failures.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: FAIL with missing GLB writer or incorrect coordinate result.

- [ ] **Step 3: Implement deterministic GLB mapping and bounded geometry.**

Generate actual and planned trajectory nodes, optional non-authoritative prediction node, start/segment/end markers, and glTF `extras`. Use physical ENU coordinates, a deterministic tube/ribbon mesh, and a default vertex budget of `100000`. Decimation must retain first/last points plus every ProfileSequence and mission-phase boundary. Handle duplicate/degenerate points without zero-area or invalid index buffers.

- [ ] **Step 4: Implement isolated snapshot PNG rendering.**

Create a temporary export root with read-only trajectory geometry, cloned materials, an offscreen camera and RenderTexture. Do not subscribe it to Playback, Timeline, parameter, or live camera events. Render from the captured camera pose and visible layers, wait for end-of-frame, read the image, and invoke completion only after the PNG exists. Destroy all temporary objects in success and failure paths.

- [ ] **Step 5: Add PlayMode consistency test and run focused suites.**

Run: `Unity.exe -batchmode -nographics -projectPath UnderwaterGliderTwin -runTests -testPlatform PlayMode -testFilter UnderwaterGliderTwin.Tests.TrajectoryExportRenderTests -testResults TestResults/TrajectoryExportRender.xml -logFile TestResults/TrajectoryExportRender.log`
Expected: PNG succeeds while live playback/camera changes continue; live Camera and PlaybackController instance IDs remain unchanged.

- [ ] **Step 6: Run EditMode and commit the geometry/render layer.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: all coordinate, GLB and existing tests pass.

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Visualization/GlbCoordinateMapper.cs UnderwaterGliderTwin/Assets/Scripts/Visualization/GlbTrajectoryWriter.cs UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryMeshDecimator.cs UnderwaterGliderTwin/Assets/Scripts/Visualization/TrajectoryExportRenderView.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/RuntimeScreenshotCapture.cs UnderwaterGliderTwin/Assets/Tests/EditMode/GlbTrajectoryWriterTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/TrajectoryExportRenderTests.cs && git commit -m "feat: export trajectory glb and snapshot png"`

### Task 6: Publish Manifest Atomically and Connect the UI

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportService.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/RuntimePathResolver.cs`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/PlaybackControlsPanel.prefab` only if the existing ExportButton label cannot be changed at runtime.
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`
- Create: `UnderwaterGliderTwin/Assets/Tests/EditMode/TrajectoryExportServiceTests.cs`

**Interfaces:**
- `TrajectoryExportManifest.Files` contains four `ExportFileEntry` objects with relative path, byte length and SHA-256; it excludes its own hash.
- `TrajectoryExportService` writes to `Exports/.staging/<exportId>/`, copies referenced current files into `attachments/`, and publishes the whole directory as `Exports/<exportId>/` only after all checks pass.
- `TrajectoryExportService.ExportSynchronously(TrajectoryExportSnapshot snapshot)` is an EditMode-only deterministic test seam that returns `TrajectoryExportResult` with `Succeeded`, `PublishedDirectory`, `StagingDirectory` and `Files`.
- `PlaybackControlsView.BindExport(Func<TrajectoryExportRequest, Action<TrajectoryExportStatus>, Action<TrajectoryExportResult>, IDisposable> beginExport)` replaces the synchronous screenshot `Func<string>` for the full export button; F12 keeps the old screenshot path.

- [ ] **Step 1: Add failing tests for staging, hashes, and UI async state.**

```csharp
[Test]
public void ExportDoesNotPublishWhenOneOutputIsMissing()
{
    var service = CreateExportServiceWithFailingPngWriter();
    var result = service.ExportSynchronously(BuildExportSnapshot());

    Assert.That(result.Succeeded, Is.False);
    Assert.That(Directory.Exists(result.PublishedDirectory), Is.False);
    Assert.That(Directory.Exists(result.StagingDirectory), Is.False);
}

[Test]
public void ExportButtonReportsCompletionOnlyAfterManifestIsPublished()
{
    var states = new List<TrajectoryExportStatus>();
    var result = InvokeExportButton(states);
    Assert.That(states[0].Phase, Is.EqualTo(TrajectoryExportPhase.CaptureSnapshot));
    Assert.That(states[states.Count - 1].Phase, Is.EqualTo(TrajectoryExportPhase.Published));
    Assert.That(result.Files.Count, Is.EqualTo(4));
}
```

`TrajectoryExportServiceTests.cs` 内定义 `BuildExportSnapshot()`、`CreateExportServiceWithFailingPngWriter()` 和 `InvokeExportButton(List<TrajectoryExportStatus> states)`；后者通过公开 UI 绑定触发异步导出，并返回已发布的 `TrajectoryExportResult`，不直接调用私有实现。

- [ ] **Step 2: Run EditMode and verify old synchronous screenshot binding fails the new tests.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: FAIL because `ExportButton` currently invokes a synchronous `Func<string>` and no manifest staging exists.

- [ ] **Step 3: Implement exportId, package staging, attachments, and cleanup.**

Generate `yyyyMMdd-HHmmss-<short-uuid>` IDs; never overwrite an existing destination. Copy external ocean files under `attachments/` and record relative path/hash. If the file cannot be copied, embed sampled ocean data in JSON; otherwise fail with `selfContained=false` and remove staging. Remove staging directories older than 24 hours on startup and remove the active staging directory in `finally` on failure.

- [ ] **Step 4: Compute hashes and publish the complete package.**

Write JSON, CSV, GLB and PNG into staging, verify existence, finite data, size, schema and SHA-256, write manifest, then rename the directory on the same volume. The manifest is the fifth control file and has no self-hash. Only after directory publication invoke the completed callback.

- [ ] **Step 5: Replace the UI synchronous callback with async status.**

Change the existing ExportButton label to `导出全部`. Disable it while an export is active, show each export phase, display the published directory and exportId only after completion, and preserve the current scene when export fails. Keep F12 as a separate screenshot-only action.

- [ ] **Step 6: Run EditMode and commit UI/package publishing.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: export service, UI tests and all existing tests pass.

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/Telemetry/TrajectoryExportService.cs UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/RuntimePathResolver.cs UnderwaterGliderTwin/Assets/Tests/EditMode/TrajectoryExportServiceTests.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs && git commit -m "feat: publish complete trajectory export packages"`

### Task 7: End-to-End Regression, Import/Re-export, and Packaged Smoke

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Tests/PlayMode/ContinuousTrajectoryExportPlayModeTests.cs`
- Create: `scripts/test-playmode.ps1` if no reusable PlayMode runner exists.
- Create: `scripts/test-playmode-focused.ps1` if no reusable PlayMode runner exists.
- Modify: `docs/superpowers/specs/2026-08-31-continuous-trajectory-export-design.md` only to record final verification evidence.
- Modify: `HANDOFF.md` only at the end of the task checkpoint; do not alter project memory before evidence exists.

**Interfaces:**
- PlayMode tests use the public Bootstrap/session APIs and never invoke private methods through reflection except when testing existing scene wiring.
- Packaged smoke uses `scripts/build-windows.ps1` and verifies `Build Finished, Result: Success` in `TestResults/WindowsBuild.log`.
- The test file defines `ExportResultHolder { TrajectoryExportResult Value; }` and `IEnumerator ExportAndWait(TwinBootstrap bootstrap, ExportResultHolder holder)`; the helper assigns `Value` only from the published completion callback and yields until that callback or timeout.
- If the repository has no reusable PlayMode runner, `test-playmode.ps1` accepts `-All` and runs the complete suite, while `test-playmode-focused.ps1` accepts `-Filter <test-name>` and forwards that filter to Unity; both write XML and log paths under `TestResults/`.
- The test file also defines `LoadMainSimulationScene()`, `ApplyAndWait(TwinBootstrap bootstrap, SimulationProfile profile)` and `WaitUntilTimelineHasFuture(TwinBootstrap bootstrap, int minimumFrames, float timeoutSeconds)` as coroutine helpers with explicit timeout failure; each test creates a unique export root and deletes it in `finally`.

- [ ] **Step 1: Add the end-to-end failing test for continuous updates and export.**

```csharp
[UnityTest]
public IEnumerator ContinuousUpdatesProduceReplayableFourFilePackage()
{
    yield return LoadMainSimulationScene();
    var bootstrap = Object.FindObjectOfType<TwinBootstrap>();
    yield return ApplyAndWait(bootstrap, ProfileWithDepth(110f));
    yield return ApplyAndWait(bootstrap, ProfileWithDepth(140f));
    yield return ApplyAndWait(bootstrap, ProfileWithDepth(180f));
    yield return WaitUntilTimelineHasFuture(bootstrap, 128, 60f);
    var holder = new ExportResultHolder();
    yield return ExportAndWait(bootstrap, holder);
    var exportResult = holder.Value;

    Assert.That(exportResult.Succeeded, Is.True);
    Assert.That(File.Exists(Path.Combine(exportResult.PublishedDirectory, "trajectory.json")), Is.True);
    Assert.That(File.Exists(Path.Combine(exportResult.PublishedDirectory, "trajectory.csv")), Is.True);
    Assert.That(File.Exists(Path.Combine(exportResult.PublishedDirectory, "trajectory.glb")), Is.True);
    Assert.That(File.Exists(Path.Combine(exportResult.PublishedDirectory, "trajectory.png")), Is.True);
    Assert.That(TrajectoryJsonImporter.TryRestore(exportResult.PublishedDirectory, out var restored, out var error), Is.True, error);
    Assert.That(restored.Segments.Count, Is.EqualTo(3));
}
```

- [ ] **Step 2: Run focused PlayMode and fix integration failures one at a time.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-playmode-focused.ps1 -Filter UnderwaterGliderTwin.Tests.ContinuousTrajectoryExportPlayModeTests`
Expected: PASS for repeated parameter changes, auto-refill, independent PNG render, package publication, and importer restore.

- [ ] **Step 3: Run the complete EditMode suite.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1`
Expected: all tests pass; record the exact count in the final report.

- [ ] **Step 4: Run the complete PlayMode suite and preserve environment evidence.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-playmode.ps1 -All`
Expected: record XML result if available. If Unity reproduces the known native SIGSEGV in `DrawSharedGeometryJobs`/`DrawLineOrTrailFromNodeQueue`, report it as an environment blocker and do not replace the full-suite result with focused results.

- [ ] **Step 5: Run Windows packaged-player smoke.**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-windows.ps1`
Expected: `Windows build succeeded` and `Build Finished, Result: Success`; launch the player with simulation mode, modify parameters three times, export the package, and verify the output directory contains four outputs plus manifest.

- [ ] **Step 6: Review diff, clean Unity artifacts, and commit only intended changes.**

Run: `git diff --check`; `git status --short`; remove only generated `InitTestScene*.unity`, test-generated `.meta`, temporary TestResults files and Unity-mutated `runInBackground` changes. Re-run `git status --short` and confirm the original worktree remains untouched except for project memory files.

Commit: `git add UnderwaterGliderTwin/Assets/Tests/PlayMode/ContinuousTrajectoryExportPlayModeTests.cs scripts/test-playmode.ps1 scripts/test-playmode-focused.ps1 docs/superpowers/specs/2026-08-31-continuous-trajectory-export-design.md && git commit -m "test: verify continuous trajectory export end to end"`

## Verification Checklist

- [ ] Three rapid valid requests before the first commit result in one committed latest profile; a request arriving after commit creates the next ProfileSequence.
- [ ] Invalid requests do not overwrite a valid queued candidate.
- [ ] Stale, cancelled, failed and timed-out requests never create segments or appear in JSON/CSV/GLB.
- [ ] All committed frames carry explicit ProfileSequence and segment start matches the first future frame.
- [ ] Timeline is the sole committed-frame data source and event order is deterministic.
- [ ] Automatic append reuses the current ProfileSequence and stops at `Completed`.
- [ ] Playback waits at the tail without falsely reporting completion and resumes only when appropriate.
- [ ] History, current frame, mission state, playback state, camera and scene identity survive every update.
- [ ] JSON contains complete profiles, algorithm/integration metadata, ocean dependency hashes and finite/null values.
- [ ] JSON importer restores the timeline and a second export is deterministic.
- [ ] CSV has UTF-8 BOM, fixed columns, explicit ENU fields and documented precision.
- [ ] GLB maps `(E=1,N=2,Depth=3)` to `(1,-3,-2)`, preserves boundaries, handles degenerate points and stays within vertex budget.
- [ ] PNG uses an independent snapshot renderer and is reported successful only after the file exists.
- [ ] Manifest validates four output hashes, package self-containment and unique exportId without self-hash recursion.
- [ ] Focused PlayMode and packaged smoke pass; full PlayMode result is reported separately if Unity native rendering crashes.
