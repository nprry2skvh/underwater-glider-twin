# 连续参数仿真与四文件轨迹导出设计

**状态：** 待用户审核

**日期：** 2026-08-31

## 1. 目标

将当前“有限帧列表 + 单次未来重建”的参数仿真改为可连续运行的参数时间线：用户在播放期间修改参数时，修改从当前播放帧之后生效，已成功提交的参数段永久保留，未来轨迹持续补片直到任务完成或达到安全上限。

同一条已提交时间线可以导出为 JSON、CSV、GLB 和 PNG。JSON 是可重放的权威数据，CSV 用于分析，GLB 用于外部三维查看，PNG 保存基于同一导出快照的独立三维渲染视图。

CSV 回放路径不改变；本设计只作用于 `RuntimeDataSourceMode.Simulation`。

## 2. 已冻结语义

### 2.1 参数变更

- 初始 profile 使用 `ProfileSequence = 0`。
- 每次成功提交的参数变更分配一个递增的 `ProfileSequence`。
- 已成功提交的参数变更逐次生成并保留一个参数段。
- 一次重建正在进行时，新请求只保留最后一个有效 profile。若新请求在当前请求的原子提交线性点之前到达，当前 in-flight 结果即视为 stale：其物理 coroutine 可以完成，但回调不得提交、不得标记为成功或取消成功；系统从最后一个有效 profile 重新生成并提交一次。
- 若旧请求已经完成原子提交，之后到达的新请求才生成下一个 `ProfileSequence`；因此连续快速修改只会保留每个提交线性点之后的最终有效 profile。
- 无效新请求不会覆盖已有的有效 pending profile。
- 被合并、取消、失败或无效的请求不生成参数段，也不进入导出。
- `requestId` 只写入最终成功提交的参数段；内部可以用版本号识别过期回调，但不能把未成功请求写入导出数据。
- 取消整个 pending 批次只取消未提交请求，不新增 `ProfileSequence`。
- 参数段的生效时间和起始帧，以该参数成功提交后生成的第一帧 future 的 `ElapsedSeconds` 与 `RowIndex` 为准，不使用按钮点击时间。
- 参数变更从当前播放帧之后的下一个积分步生效；当前帧及之前的历史帧不可修改。

### 2.2 任务生命周期

- `CycleCount` 表示整个任务的总航段上限，不是一次生成窗口。
- 自动补片只能在 `CompletedCycles < CycleCount` 时继续运行，不允许无限循环。
- 当 `futureDurationSeconds < 120` 或 `futureFrameCount < 128`，且 `CompletedCycles < CycleCount` 时，触发一次自动补片；两个条件任一满足即可触发。
- 全局安全上限为 `MaximumTimelineFrameCount = 200000`；profile 计算出的任务总帧数超过该上限时拒绝启动或更新。
- 完成全部航段进入 `Completed`；安全状态异常进入已有的 `LegTimeout`，两者不能混淆。
- 修改 `CycleCount` 时不能小于已经完成的航段数；增大上限允许继续生成。
- 运行中的 `CycleCount` 只允许增大或保持不变；减小会被拒绝，不截断已提交或已生成的 future。
- 修改原点经纬度在运行中拒绝，避免历史坐标系改变；速度、姿态目标、深度、浮力、海流和采样间隔从下一积分步影响未来。

### 2.3 播放与重建

- 播放、暂停、倒放、拖动和相机操作不改变轨迹时间线的物理状态。
- 重建期间播放继续使用已提交帧；成功提交时保留当前索引、连续索引、速度、方向、播放状态和相机对象。
- 如果提交时播放位置已离开原 seed，重建从最新有效播放帧重新建立未来分支；旧异步回调不能提交。
- 每个提交都通过完整的状态快照继续 `SimulationMissionStepper`，不按 elapsed time 重新推导任务相位。
- 播放速度过快而补片尚未完成时，游标停在已提交尾部并进入 `WaitingForFuture`，不报告 `Completed`；若此前正在播放，补片提交后自动恢复播放，若此前暂停则保持暂停。
- 进入 `Completed` 后禁止再次自动补片；只有显式重置任务才创建新的时间线。

## 3. 架构

### 3.1 `SimulationTrajectoryTimeline`

新增 `UnderwaterGliderTwin.Telemetry.SimulationTrajectoryTimeline`，作为运行时唯一时间线，维护：

- 唯一的不可变已提交 frame 序列；
- 当前播放索引和当前播放帧的游标状态；
- future 尾部及末端 `SimulationStateSnapshot`；
- `SimulationTimelineSegment` 参数段序列；
- timeline revision、最后成功 requestId 和完成状态；
- pending 请求的内部状态，但不把 pending 状态伪装成已提交参数段。

Timeline 是 committed frames 的唯一数据源。`PlaybackModel` 不拥有第二份可变帧列表，只保存游标、播放控制状态，并读取 Timeline 发布的不可变快照。所有 `ReplaceFutureFrom` 和 `AppendFuture` 都必须通过 Timeline 的同一个提交入口；不能由 `PlaybackModel` 或视图自行替换帧。

Timeline 提供 `ReplaceFutureFrom(int preservedIndex, IReadOnlyList<TelemetryFrame> future)` 与 `AppendFuture(IReadOnlyList<TelemetryFrame> future)` 两类原子操作。操作前检查历史前缀、RowIndex、ElapsedSeconds 和 ProfileSequence；失败时不改变原时间线。

提交事件顺序固定为：

1. Timeline 原子发布新的 committed snapshot 和递增 revision，并触发 `TimelineChanged`。
2. PlaybackModel 更新其 snapshot 引用和游标，并触发 `FramesReplaced`；PredictionController 在此阶段只更新引用，TrajectoryView 更新轨迹几何。
3. PlaybackController 触发 `FrameChangedWithReason(Rebuild)`，PredictionController 基于新 snapshot 重算预测。
4. PlaybackController 再触发兼容的 `FrameChanged`，最后由 RuntimeSession 发布 `StatusChanged(Committed)`。

自动补片沿用当前 `ProfileSequence`，不创建新的参数段；只有 profile 更新成功提交才递增 ProfileSequence。

### 3.2 `SimulationRuntimeSession`

`SimulationRuntimeSession` 负责 profile 更新和自动补片：

1. 校验新 profile。
2. 为有效请求分配内部 request version，并记录候选 profile。
3. 若无 in-flight rebuild，则从当前播放帧建立 snapshot 并启动 coroutine；若已有 in-flight rebuild，则把当前操作标记为 stale，并只替换 queued candidate。旧操作可以物理完成，但不能再提交。
4. 只有仍是最新 request version 的结果才验证 future、分配下一个 `ProfileSequence`、写入参数段并原子提交 future。
5. 若 queued candidate 存在，从最新提交的 timeline 尾部或当前播放 seed 继续下一次生成；若新请求在上一操作提交前到达，旧操作不产生 segment。
6. future 低于 horizon 时，以当前有效 profile 自动补片。
7. 取消、失败、超时和过期回调只清理对应 pending 状态，不改变已提交历史、参数段或导出结果。

`StatusChanged` 扩展为可区分 `Idle`、`Queued`、`Generating`、`WaitingForFuture`、`Committed`、`Completed`、`Cancelled` 和 `Failed` 的状态，UI 不通过 `LastError` 推断成功状态。

### 3.3 `PlaybackModel`

增加只操作尾部的 API：

- `ReplaceFutureFrom(int preservedIndex, IReadOnlyList<TelemetryFrame> replacement)`；
- `AppendFrames(IReadOnlyList<TelemetryFrame> frames)`；
- `HasFutureHorizon(float seconds, int minimumFrames)`；
- 在尾部追加后保持 `continuousIndex`、当前索引和播放方向；到达尾部等待补片时保留 `resumeAfterFuture`。

现有 `ReplaceFrames` 保留兼容，但参数仿真运行时统一经由时间线 API，避免每次更新复制整条历史。

### 3.4 共享积分核心

继续使用 `SimulationMissionStepper` 作为完整轨迹和未来增量轨迹的唯一状态推进器。它必须暴露可恢复的末端状态，并接受新的 profile continuation；不得新增按时间相位生成未来的旁路逻辑。

## 4. 数据契约

### 4.1 Frame

`TelemetryFrame` 增加明确的 `ProfileSequence` 字段，旧构造调用默认值为 `0`。不通过时间范围反推参数段。所有由初始生成、未来生成、时间线追加和 JSON importer 创建的 frame 都必须写入该字段。

导出 frame 同时包含物理米制 ENU 坐标：`enu.eastM`、`enu.northM`、`enu.upM`，并保留 `depthM` 作为向下为正的业务字段。ENU 坐标不是 Unity 的水平缩放或深度缩放坐标。

### 4.2 参数段

`SimulationTimelineSegment` 至少包含：

```text
ProfileSequence       int
RequestId             long
StartRowIndex         int
StartElapsedSeconds   float
Profile               SimulationProfileSnapshot
CommittedAtUtc        string
```

只有成功提交的 segment 才出现在 timeline 和导出文件中。

### 4.3 JSON

JSON 使用 `TrajectoryJsonCodec` 明确序列化和反序列化，不依赖 Unity `JsonUtility` 对 NaN、Infinity、nullable 数值和完整嵌套 profile 的隐式行为。所有浮点值必须是有限数字；缺失的计划坐标和可选诊断字段写为 `null`。

顶层契约固定包含：

```json
{
  "schemaVersion": 1,
  "exportId": "20260831-153000-<short-uuid>",
  "createdAtUtc": "2026-08-31T07:30:00Z",
  "algorithmVersion": "hybrid-glider-trajectory-v2",
  "integrationStepSeconds": 0.5,
  "sampleIntervalSeconds": 5.0,
  "timelineRevision": 12,
  "committedFrameCount": 1000,
  "committedUntilElapsedSeconds": 4995.0,
  "pendingUpdate": false,
  "playback": {
    "currentFrameIndex": 340,
    "currentElapsedSeconds": 1700.0,
    "isPlaying": true,
    "speed": 1.0,
    "direction": 1
  },
  "coordinate": {
    "datum": "WGS84",
    "axes": "enu:east,north,up",
    "units": "m",
    "depthPositive": "down"
  },
  "initialProfile": {
    "cycleCount": 6,
    "cycleDurationSeconds": 900.0,
    "sampleIntervalSeconds": 5.0,
    "targetDepthM": 160.0,
    "horizontalSpeedMps": 0.65,
    "startHeadingDeg": 42.0,
    "headingDeltaPerCycleDeg": 14.0,
    "pitchAmplitudeDeg": 12.0,
    "rollAmplitudeDeg": 3.0,
    "descentSpeedMps": null,
    "ascentSpeedMps": null,
    "descentPitchDeg": null,
    "ascentPitchDeg": null,
    "descentRollDeg": null,
    "ascentRollDeg": null,
    "descentNetBuoyancyForceN": null,
    "ascentNetBuoyancyForceN": null,
    "originLongitudeDeg": 120.0,
    "originLatitudeDeg": 25.0,
    "waterColumnDepthM": 520.0,
    "oceanCurrentProfile": { "layers": [], "defaultVelocityMps": [0.0, 0.0] },
    "oceanCurrentField": { "source": "none", "dataSha256": null },
    "dynamics": { "integrationStepSeconds": 0.5 }
  },
  "parameterSegments": [],
  "frames": []
}
```

`initialProfile`、每个 segment 的 `profile` 和海流配置必须完整保存，包括 Dynamics、海流层/场、来源偏好、来源稳定引用和 SHA-256。若海流来自外部文件，导出 manifest 必须记录文件相对路径和 hash；若无法稳定引用，则把实际采样数据写入 JSON 的 `oceanCurrentSamples`，保证脱离原文件仍可重放。

每个 frame 包含实际/计划经纬度、`enu: { eastM, northM, upM }`、`depthM`、mission state、诊断字段和 `ProfileSequence`。其中 ENU 是物理米制坐标，`upM = -depthM` 只表示当前帧的垂向位置关系；计划坐标缺失时对应字段为 `null`。Importer 必须先校验算法版本、坐标契约、帧单调性、profile 段起点和 hash，再恢复 timeline。

JSON 可选保存导出时已发布的 prediction snapshot，包括起始帧、预测点、置信度和误差指标；该对象标记为非权威预测数据，importer 不将其写回物理 timeline。

### 4.4 CSV

CSV 采用 UTF-8 with BOM，以兼容 Windows Excel 的中文表头；逗号分隔，双引号转义，InvariantCulture 浮点格式（最多 6 位小数），缺失值为空字段，首行固定为 schema header。JSON 仍是无损重放来源；CSV 的 6 位小数用于分析显示，导入器不得使用 CSV 作为确定性重放输入。列顺序为：

```text
RowIndex,ElapsedSeconds,RawTime,LongitudeDeg,LatitudeDeg,EastM,NorthM,UpM,DepthM,AltitudeM,HeadingDeg,PitchDeg,RollDeg,Voltage24V,Current24A,BatteryPercent,RunState,MissionPhase,TargetSegment,TargetHeadingDeg,TargetDepthM,ProfileSequence,PlannedLongitudeDeg,PlannedLatitudeDeg,WaterVelocityEastMps,WaterVelocityNorthMps,CurrentEastMps,CurrentNorthMps,NetBuoyancyForceN,EnergyWatts
```

CSV 只承载逐帧数据；完整 profile、海流来源 hash 和 playback 状态以同一 `exportId` 的 JSON/manifest 为权威来源。

### 4.5 GLB

GLB 使用标准右手、Y-up 契约：

```text
GLB X = East
GLB Y = Up = -Depth
GLB Z = -North
```

因此 `ENU(E=1, N=2, Depth=3)` 必须转换为 `GLB(1, -3, -2)`。该映射通过单元测试和人工外部查看器检查固定下来。

GLB 至少包含：

- 实际轨迹：按 `ProfileSequence` 分段的细管或带状网格；
- 计划轨迹：单独节点，可关闭；
- 预测轨迹：如果导出快照包含当前已发布的 `PredictionSnapshot`，则作为标记为 `nonAuthoritative` 的独立节点导出；没有有效预测时省略该节点。不导出未提交 prediction cache；
- 起点、航段切换点和终点标记；
- `extras` 中的原点、坐标手性、单位、timeline revision 和 exportId。

轨迹网格使用确定性的最大顶点预算和折线抽稀；抽稀只影响 GLB 几何，不影响 JSON/CSV 的完整帧。默认 GLB 顶点预算为 100000，超过预算按时间均匀采样并保留所有参数段边界、mission phase 边界和首尾点。

## 5. 四文件发布和 PNG 一致性

`manifest.json` 是第五个控制文件，不计入用户要求的四种导出格式；四个输出文件和 manifest 共同构成一个导出包。manifest 包含四个输出文件的相对路径、大小、SHA-256、schema 版本、exportId、坐标契约和 `selfContained` 标志，但不包含自身 hash，避免循环依赖。导出服务首先创建 `Exports/.staging/<exportId>/`，生成 JSON、CSV、GLB 和 PNG，校验四个文件后写入 manifest，再将整个 staging 目录一次性发布到 `Exports/<exportId>/`；不使用四个互不关联的独立 rename。

所有外部海流文件必须复制到导出包的 `attachments/` 子目录，manifest 记录其相对路径和 SHA-256；网络海流若没有可复制的稳定文件，则必须把实际采样数据写入 JSON。无法复制或嵌入时 `selfContained = false`，导出失败而不发布半完整包。目标 exportId 目录已存在时禁止覆盖，生成新的短 UUID。程序启动时删除超过 24 小时的 `.staging` 目录；当前进程的 staging 目录由 finally 清理。

PNG 不能再使用同步 `Func<string>` 语义。为允许实时场景继续播放，PNG 使用独立的导出渲染视图和离屏相机，而不是截取实时 Camera：

1. 捕获与 JSON/CSV/GLB 相同的 timeline snapshot、当前帧、相机姿态和可见图层状态。
2. 创建只读的 `TrajectoryExportRenderView` 和独立 RenderTexture；它只使用 snapshot 几何，不订阅实时 Playback、参数或相机事件。
3. 在导出相机中渲染固定帧，等待 `WaitForEndOfFrame` 后读回 PNG，并确认文件存在。
4. 四个文件全部完成并通过 manifest 校验后，才向 UI 报告成功。
5. 销毁独立导出视图；实时播放、参数修改和相机控制不被暂停或替换；失败时删除 staging 目录并报告原因。

UI 采用异步 `IProgress<TrajectoryExportStatus>` 或回调结果对象，显示“准备快照、生成数据、生成 GLB、等待截图、发布完成/失败”，只有四个文件和 manifest 发布完成后才显示目录。

## 6. UI 行为

现有“导出”按钮改为“导出全部”，一次导出四文件包；截图快捷键仍可单独工作，但不冒充完整轨迹导出。导出期间按钮禁用或显示取消状态，参数修改和播放可以继续；PNG 来自独立 snapshot render view，不读取实时场景变化。

导出成功后显示目录和 `exportId`；失败只显示可操作的短错误，不清空当前轨迹或当前海流场。

## 7. 错误与边界

- profile 无效：拒绝该请求，保持当前 pending 和 active profile。
- pending latest-wins：新有效请求在当前请求原子提交前到达时，使 in-flight candidate stale；旧 coroutine 的回调只能被丢弃，不能提交、不能标记为成功或取消成功。只有已越过提交线性点的请求才能创建参数段。
- 取消/超时/异常：不新增 segment，不改变已提交 timeline。
- 过期回调：按 request version 和 seed row/time 双重校验，静默丢弃。
- future 为空、帧不单调、ProfileSequence 倒退、坐标越界、NaN/Infinity：拒绝整次提交。
- `CycleCount` 达到总上限：提交最后合法帧后进入 `Completed`，自动补片停止。
- `WaitingForFuture` 只表示播放游标暂时到达已提交尾部，不能写入导出为完成状态；补片成功后按 `resumeAfterFuture` 恢复。
- 导出失败：只清理 staging；已存在的历史导出包和当前运行时状态不变。
- JSON importer 发现算法版本、海流来源 hash 或坐标契约不匹配：拒绝加载并给出明确错误，不做近似重放。

## 8. 测试与验收

### EditMode

- 连续生成超过初始 future horizon，验证自动补片直到 `Completed`。
- 连续提交三次有效 profile，验证 `ProfileSequence = 1,2,3`、起始 RowIndex/ElapsedSeconds 等于各自第一帧 future。
- pending 期间提交多个 profile，验证只保留最后有效 profile；无效请求不覆盖它。
- 验证取消、失败、超时和迟到回调不新增 segment、不改变历史。
- 验证历史 frame、mission state、连续索引和 current world position 不跳变。
- 验证 CycleCount 总上限、Completed 状态、最大 200000 帧保护和 origin 修改拒绝。
- 验证 `ProfileSequence` 不依赖时间反推，所有 frame 显式携带字段。
- 验证 JSON 不输出 NaN/Infinity，缺失值输出 `null`，完整 profile 和海流 hash 可恢复。
- 验证 CSV UTF-8 BOM、列顺序、空值、浮点格式和行数。
- 验证 GLB header、chunk 长度、顶点预算、参数段边界和坐标转换：`(1,2,3) -> (1,-3,-2)`。
- 导出 JSON 后 importer 重新加载，验证 frames、segments、mission state、timeline revision 和 playback 状态一致。
- 验证导出后再次导入并重新导出，JSON/CSV/GLB 的规范化内容和关键 hash 确定一致。
- 验证四个输出 hash 写入 manifest，缺一个文件、外部海流依赖无法复制或嵌入时不发布目录；验证 manifest 不自引用 hash。

### PlayMode / packaged smoke

- 启动 Main 场景，连续播放超过初始轨迹末端，确认不会因有限列表自动停止，直到 `Completed`。
- 播放中依次修改速度、深度、姿态和海流参数，确认每次成功提交从当前播放帧连续生效，场景、相机和 PlaybackController 不替换。
- pending 期间快速修改参数，确认 UI 显示 queued/latest-wins，最终只提交最后有效 profile。
- 导出期间移动相机、播放和修改参数，确认独立 PNG、JSON/CSV/GLB 使用同一 export snapshot，实时运行状态不被暂停或替换。
- 使用 Blender 或兼容 glTF 查看器检查 GLB 的东、北、深度方向、实际/计划/预测图层、退化线段和参数段边界。
- Windows Player smoke test 验证导出目录、manifest 和 importer 可用；记录全量 PlayMode 的 Unity 原生渲染环境阻塞，不以 focused 测试替代全量结论。

## 9. 非目标

- 不把 CSV 回放改造成连续参数仿真。
- 不引入数据库、云端任务同步或多人协同。
- 不承诺由现有 profile 产生海试认证级高保真水动力结果。
- 不把每一次未提交的 UI 滑块中间值记录为参数段。
