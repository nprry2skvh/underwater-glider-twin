# 三维海流指挥舱改造 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将当前参数仿真与 CSV 回放界面升级为可操作的海洋任务指挥舱：中央区域展示真实三维海流矢量体，提供稳定的统一视角控制，并重组上下左右 UI，使任务状态、海流来源和回放控制清晰且不互相遮挡。

**Architecture:** 保留 `SimulationTelemetrySource`、`TrajectoryView`、`PlaybackController` 和 `GeoCoordinateMapper` 的既有职责。新增一个纯数据的海流查询/可视化采样层，将手工分层海流和联网 `OceanCurrentField` 汇聚为同一 ENU 向量查询接口；`OceanVolumeView` 仅消费已选取的三维向量实例。相机模式和 UI 命令分别由独立的控制器/视图负责，通过 `TwinBootstrap` 一次装配，切换视角时绝不重新生成海流数据、轨迹或播放状态。

**Tech Stack:** Unity C#、Unity UI (`UnityEngine.UI`)、EditMode NUnit tests、现有 Windows 构建与测试脚本。

## Global Constraints

- 本地坐标统一为 ENU：`X` 向东、`Z` 向北、`Y` 向上；原点为当前任务 `OriginLongitudeDeg` / `OriginLatitudeDeg`。
- `DepthM` 向下为正，显示换算固定为 `Y = -DepthM * depthScale`；`u` 东向为正、`v` 北向为正、`w` 向上为正，单位均为 `m/s`。没有 `w` 数据时使用 `0`，不得擅自反向。
- 默认 1920×1080 目标 60 FPS，最低可接受 45 FPS；ENU 候选格点采用固定分层采样预算，默认严格上限为 1,440 个；可见箭头严格上限为 360。达到任一上限时只能抽稀，不能增加绘制调用或创建更多对象。
- LOD 抽稀优先级：滑翔机邻域、轨迹安全通道边缘、近景/当前任务深度层、远景与低影响层。安全通道内不显示会遮挡滑翔机、轨迹或关键标记的箭头。
- 海流箭头启用深度测试、距离/层级透明衰减；滑翔机、当前位置标记、关键任务标记与轨迹在箭头之后绘制并始终清晰可见。
- 数据优先级：当前任务手工/已加载分层海流优先；联网 `OceanCurrentField` 仅在用户显式选择或没有可用分层时作为备选。联网失败必须保留当前已有数据与渲染，显示短而可操作的提示。
- 视角切换和复位只修改相机；不得重建海流源数据或 ENU 候选格点，不得重建轨迹、播放进度、暂停状态或相机以外的 UI/任务状态。

### 可测试的默认数值

这些值以 `SimulationProfile` 的可序列化默认配置实现，并由 EditMode 测试直接引用；不在 UI 中暴露为常规任务参数，避免误把可视化预算当作航行控制量。

| 配置项 | 默认值 | 单位 | 用途 |
| --- | ---: | --- | --- |
| `GliderClearanceRadiusM` | 500 | m | 滑翔机当前位置周围的箭头/流线留白半径 |
| `TrajectorySafetyCorridorRadiusM` | 750 | m | 主轨迹周围的箭头/流线安全通道半径 |
| `MinimumArrowSpeedMps` | 0.02 | m/s | 低于此值的海流箭头视为无效，不参与可见补选 |
| `MinimumStreamlineSpeedMps` | 0.05 | m/s | 低于此值时流线停止或不生成 |
| `IrregularFieldIdwRadiusKm` | 50 | km | 非规则联网格点 IDW 的最大查询半径；超过即不可用，不外推 |
| `CandidateDepthLayerCount` | 12 | 层 | 候选缓存固定深度层数量 |
| `CandidateHorizontalColumnCount` | 12 | 列 | 每深度层的 ENU 水平格点列数 |
| `CandidateHorizontalRowCount` | 10 | 行 | 每深度层的 ENU 水平格点行数 |

候选缓存预算固定为 `12 × 12 × 10 = 1,440`。任务范围改变时按相同数量重新映射位置，不增加格点总数。

### 实现边界补充（本计划以此为准）

- **候选缓冲、时间刷新和镜头刷新分离：** 任务加载、成功“应用海流设置”、成功原子替换联网场、来源切换、任务 ENU 范围/任务深度变更时，允许显式 `RebuildSourceAndCandidateCache` 重建海流源数据、固定预算内的 ENU 候选格点和轨迹安全通道。播放时间变化调用 `RefreshForTime`：重新查询预算内候选格点在新 `ElapsedSeconds` 的向量，剔除因覆盖边界、无效样本或低速阈值而失效的已选箭头，再按相同 LOD 与安全通道规则从有效候选中补选，最后更新实例方向、长度和颜色；可见集合始终不超过 360，且不重建候选格点。相机变化调用 `RefreshForCamera`：对当前有效候选索引重新评分、排序并更新最多 360 条实例变换/颜色数据，不重建候选格点。两类刷新都不得创建/销毁 `GameObject`、共享网格或材质，也不得产生持续 GC 分配。
- **两类场数据必须如实标识：** 手工 `OceanCurrentProfile` 是“分层均匀场”，只随深度变化，水平方向均匀，`w=0`；它可以被采样到三维体积中的多个位置，但不能被描述为经纬度格点场。`OceanCurrentField` 才是“经纬深度格点场”，可以随位置和时间变化。`OceanCurrentResolver` 统一查询结果，但质量摘要必须显示 `分层均匀场` 或 `联网格点场`。
- **唯一来源选择：** 新增可序列化 `OceanCurrentSourcePreference` 枚举，值为 `LayeredPreferred` 与 `NetworkPreferred`。它仅由 `SimulationProfile`/运行态持有，UI 只编辑此值，`OceanCurrentResolver` 只读取此值；不再传递独立的“联网场是否启用”布尔值。
- **统一查询契约：** 新增不可变 `OceanCurrentQuery`，至少包括经度、纬度、ENU 位置、`DepthM` 和 `ElapsedSeconds`。Resolver 将经纬度用于物理场查询、ENU 用于可视化布局，轨迹仿真和体渲染传入同一时刻的查询对象。
  - 分层均匀场：按不重叠层的深度中心排序，在相邻中心之间线性插值；首尾之外钳制到最近一层。保存时拒绝重叠层并显示可操作提示。
  - 联网格点场：仅在可用的经纬/深度/时间覆盖域内插值。规则格点使用三线性加时间线性插值；非规则样本使用有最大半径限制的逆距离加权。超出水平、深度或时间覆盖域，或邻域无有效样本时返回“不可用”，由 Resolver 按来源优先级决定是否兜底，绝不外推。
- **透明与关键元素渲染策略：** 海流箭头使用透明材质，`ZTest LEqual`、`ZWrite Off`、队列 `3000`；箭头之间可以半透明混合。轨迹、滑翔机和关键标记在箭头之后以较高队列绘制，并仍对不透明环境执行 `ZTest LEqual`。因此它们不会被箭头覆盖，也不会透过海床等真实不透明遮挡物；不采用“任何情况下都穿透显示”的覆盖相机。
- **流线属于本期，但有固定上限：** 最多 12 条，每条最多 24 个 RK2 积分步；种子只取自体积边缘且避开滑翔机/轨迹安全通道。达到场边界、遇到不可用查询、低于最小速度或进入安全通道即停止。流线仅作辅助，不纳入 360 个箭头预算，也不得替代箭头。
- **联网采用暂存后原子替换：** 下载、解析和质量验证全部完成后才将候选 `OceanCurrentField` 写入运行配置；失败路径不得修改现有 `OceanCurrentField`、手工层或当前渲染选择。失败提示依据实际保留对象生成，例如“保留当前联网格点场，可重试”或“保留当前 3 层分层场，可重试”，而不是固定写层数。手工层不会被联网结果隐式覆盖。
- **验证分层：** EditMode 验证纯数据、预算、排序、来源优先级和状态不重置；PlayMode/运行时验收验证渲染顺序、透明遮挡、分辨率布局、GC 和帧率。Profiler 记录是 60/45 FPS 验收的唯一依据。

---

## 1. 建立统一的三维海流数据与坐标契约

**Files:**
- Create: `Assets/Scripts/Telemetry/OceanCurrentVector.cs`
- Create: `Assets/Scripts/Telemetry/OceanCurrentResolver.cs`
- Modify: `Assets/Scripts/Telemetry/OceanCurrentField.cs`
- Modify: `Assets/Scripts/Telemetry/OceanCurrentProfile.cs`
- Modify: `Assets/Scripts/Telemetry/SimulationProfile.cs`
- Modify: `Assets/Scripts/Telemetry/SimulationTrajectoryGenerator.cs`
- Modify: `Assets/Scripts/Mapping/GeoCoordinateMapper.cs`
- Test: `Assets/Tests/EditMode/OceanCurrentResolverTests.cs`
- Test: `Assets/Tests/EditMode/OceanCurrentProfileTests.cs`

- [ ] **Step 1: 先写失败测试，锁定 ENU、深度和来源优先级。**
  - 首先断言 `SimulationProfile.Default` 的安全半径、流速阈值、IDW 半径与 `12 × 12 × 10` 采样常量均为本计划的固定值；所有采样、流线和 Resolver 测试只能通过这些配置取值，禁止散落字面量。
  - 覆盖 `OceanCurrentVector` 从 `(u, v, w)` 转为 Unity 向量 `(u, w, v)`；断言 `DepthM=100` 使用 `GeoCoordinateMapper.MapDepth` 后落在负 `Y`。
  - 构造同时有分层海流与联网场的 `SimulationProfile`：`LayeredPreferred` 必须优先分层，`NetworkPreferred` 必须优先联网场；首选来源不可用时才允许另一来源兜底。
  - 覆盖无垂向数据时 `w == 0`、分层中心之间的线性插值、层重叠的拒绝、以及联网场在经纬/深度/时间任一维越界时保留分层兜底。
  - 用同一组 `OceanCurrentQuery` 断言轨迹生成和体采样获得相同的向量与来源；时间超出联网场范围不得回退到“最近时间样本”。

- [ ] **Step 2: 扩展数据模型，但保持旧数据兼容。**
  - 在 `OceanCurrentFieldSample` 增加可选 `VerticalMps`，保留现有六参构造函数并让其默认 `0f`；新增包含 `w` 的构造函数、克隆与有效性校验。
  - 新建不可变 `OceanCurrentQuery` 与 `OceanCurrentVector`，前者包含经纬度、ENU、深度和时间，后者显式承载 `u/v/w` 与来源；不要让渲染层直接猜测坐标正方向或查询时间。
  - 新建可序列化 `OceanCurrentSourcePreference`（`LayeredPreferred`、`NetworkPreferred`），由 `SimulationProfile` 克隆与运行态唯一持有；新建 `OceanCurrentResolver`，只读取该枚举，返回单一的 `TryGetVector(OceanCurrentQuery query, out OceanCurrentVector vector)` 查询入口及来源/数据类型摘要。
  - 在 `SimulationTrajectoryGenerator` 改为使用该解析器，确保仿真受流与可视化读到同一海流定义。
  - 仅在 `GeoCoordinateMapper` 增加明确的 ENU/深度辅助方法，不改变既有 CSV 映射结果。

- [ ] **Step 3: 运行相关 EditMode 测试。**
  - Run: `powershell -ExecutionPolicy Bypass -File ..\scripts\test-editmode.ps1`
  - Expected: 所有 EditMode 测试通过；新增测试验证 ENU、`w` 默认值、来源优先级和覆盖范围兜底。

## 2. 将海流体替换为受预算约束的真实三维矢量采样

**Files:**
- Create: `Assets/Scripts/Visualization/OceanVolumeSampling.cs`
- Modify: `Assets/Scripts/Visualization/OceanVolumeView.cs`
- Modify: `Assets/Scripts/Visualization/RuntimeMaterialFactory.cs`
- Modify: `Assets/Scripts/Visualization/TrajectoryView.cs`
- Modify: `Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `Assets/Tests/EditMode/OceanVolumeViewTests.cs`
- Test: `Assets/Tests/EditMode/OceanVolumeSamplingTests.cs`

- [ ] **Step 1: 先写失败测试，定义可见箭头选择规则。**
  - 给定三维候选格点、相机位置、滑翔机位置和轨迹折线，断言选择结果不超过 `360` 个。
  - 构造大范围/深水任务，断言 ENU 候选格点严格等于 `SimulationProfile` 的 `12 × 12 × 10 = 1,440` 分配，且按深度层和水平区域具有稳定、可复现的采样覆盖。
  - 断言滑翔机邻域和轨迹安全通道内的候选被剔除或强制降为不可见；当前深度层、近景层在预算紧张时优先保留。
  - 断言 `0.02 m/s` 以下的箭头候选被剔除、`0.05 m/s` 以下不生成或终止流线；近景与远景箭头的几何尺寸、亮度和透明度连续衰减，且三维方向包含 `w` 分量。
  - 使用随 `ElapsedSeconds` 变化且局部时间覆盖失效的联网场，推进播放时间后断言原已选失效箭头被剔除、有效候选按相同 LOD/安全通道规则补选，集合仍不超过 `360`。
  - 断言时间刷新只查询 `<= 1,440` 个候选，且候选缓存版本、`GameObject` 数、共享网格/材质实例数保持不变；在连续时间刷新与镜头刷新中断言无持续 GC 分配。
  - 对既有 `BuildCurrentArrowGeometry(Vector2)` 保持兼容测试，同时新增 `Vector3` 版本的方向与长度测试。

- [ ] **Step 2: 新建纯采样/LOD 模块。**
  - `OceanVolumeSampling` 生成 ENU 的 `X × Z × Depth` 候选格点，并以固定的 1,440 总预算在水平/深度层间稳定分配；通过 `OceanCurrentResolver` 查询真实向量。手工层在所有水平格点复用同一深度查询结果，明确作为分层均匀场显示；联网场才展示真实时空格点变化。
  - 手工分层场只会在每个水平格点查询相同的、随深度插值的 `u/v`，并在摘要明确标识“分层均匀场”；联网场才按经纬、深度和时间生成空间变化的格点向量。
  - 让选择器输入相机、滑翔机、当前轨迹、任务深度和播放时间，输出排序后的 `CurrentArrowInstance`。排序必须是确定性的，方便测试与复现。
  - 实现最多 12 条流线、每条最多 24 个 RK2 积分步：种子来自体积边缘，避开安全通道，遇到边界、无效查询、低流速或安全通道立即截断。流线不得替代箭头。

- [ ] **Step 3: 重写 `OceanVolumeView` 的绘制职责。**
  - 从“每箭头创建 Cylinder + Cone GameObject”切换为共享网格/材质的批量实例或按层合并网格，避免超过预算后继续创建对象。
  - 箭头使用 `(u, w, v)` 方向、速度色标、相机距离衰减、深度测试与透明度。近处较粗亮，远处较细淡。
  - 海流箭头材质固定为 `ZTest LEqual`、`ZWrite Off`、队列 `3000`；`TrajectoryView`、滑翔机和任务标记以更高队列绘制，但继续对海床等不透明场景做深度测试。测试“箭头不遮挡关键元素”，不测试关键元素穿透真实环境。
  - `TwinBootstrap` 传入新的解析器、当前滑翔机 Transform、轨迹和任务 ENU 范围；任务加载、海流设置成功应用、联网场成功原子替换、来源切换或任务范围变更时显式调用 `RebuildSourceAndCandidateCache`。镜头变化只调用无分配的 `RefreshForCamera` 来重排并上传可见实例数据；播放时间变化只调用无分配的 `RefreshForTime`，重新查询预算内候选、剔除失效实例并补选有效实例后更新方向、长度和颜色。

- [ ] **Step 4: 运行体渲染与可视化测试。**
  - Run: `powershell -ExecutionPolicy Bypass -File ..\scripts\test-editmode.ps1`
  - Expected: `OceanVolumeViewTests` 和新采样测试通过，确认 `<= 360`、安全通道、三维方向、衰减、时间推进更新箭头、候选缓存不重建和共享渲染资源。

## 3. 统一相机预设、自由环绕与交互状态

**Files:**
- Modify: `Assets/Scripts/Visualization/TwinCameraController.cs`
- Modify: `Assets/Scripts/Visualization/TrajectoryView.cs`
- Modify: `Assets/Scripts/UI/PlaybackControlsView.cs`
- Modify: `Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `Assets/Tests/EditMode/VisualizationTests.cs`
- Test: `Assets/Tests/EditMode/UiTests.cs`

- [ ] **Step 1: 先写失败测试，固定相机语义。**
  - 扩展 `CameraMode` 并测试五个模式：`Follow`、`Global`、`Top`、`Side`、`Orbit`。
  - 验证每种预设复位到自己的推荐姿态；`Orbit` 复位回默认全局透视。
  - 验证跟随模式收到旋转/平移/缩放输入后转为 `Orbit` 且保留当前相机姿态；暂停不会禁用相机操作。
  - 记录一个播放索引与一个海流视图实例计数，切换/复位模式后断言两者不变。

- [ ] **Step 2: 将 `TwinCameraController` 变为明确的状态机。**
  - 为每个预设提取 `ApplyRecommendedPose(CameraMode)`；全局视角框选完整海流体和轨迹，俯视聚焦水平漂移，侧视聚焦深度剖面。
  - 添加 `ResetCurrentView()`、`NotifyManualCameraInput()` 和相机变更事件，供海流 LOD 刷新使用。
  - 统一输入：左键拖动旋转、右键拖动平移、滚轮/触控板捏合缩放、双击复位、双指拖动平移；不把中键作为唯一平移入口。
  - 不读取或写入 `PlaybackController` 的播放状态，确保暂停时可自由查看。

- [ ] **Step 3: 让 UI 只作为一个相机入口。**
  - 从 `PlaybackControlsView` 移除分散的旧“跟随/全局/环绕/海域”组；改由中央工具栏统一发出相机命令。
  - `TrajectoryView.SetCameraMode` 只调整轨迹可见性，不触发数据重采样或播放状态变化。

- [ ] **Step 4: 运行相机与 UI 回归测试。**
  - Run: `powershell -ExecutionPolicy Bypass -File ..\scripts\test-editmode.ps1`
  - Expected: 预设、手动转自由环绕、双击复位和状态不重置的测试均通过。

## 4. 重排为工程任务指挥舱 UI，并消除重复控制

**Files:**
- Modify: `Assets/Scripts/UI/UiFactory.cs`
- Modify: `Assets/Scripts/UI/DataInputView.cs`
- Modify: `Assets/Scripts/UI/DashboardView.cs`
- Modify: `Assets/Scripts/UI/StatusPanelView.cs`
- Modify: `Assets/Scripts/UI/PlaybackControlsView.cs`
- Create: `Assets/Scripts/UI/OceanCommandToolbarView.cs`
- Modify: `Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Test: `Assets/Tests/EditMode/UiTests.cs`

- [ ] **Step 1: 先写失败测试，锁定结构和文本。**
  - 初始化 UI 后，断言存在单一顶部状态栏、三个顶部配置子组、中央 `OceanCommandToolbarView`、左遥测、右状态和单一底部主控区。
  - 断言页面不存在第二个播放/暂停按钮，也不存在旧相机按钮组。
  - 断言中央只主显速度色标；“有效箭头数/LOD”和暂停状态为辅助文本；存在五个视角按钮与“复位视角”。
  - 断言主要标签包含单位，例如 `深度 (m)`、`东向流速 (m/s)`、`经度 (°)`，且输入值区不重复拼接单位。

- [ ] **Step 2: 扩展 UI 基础样式，避免卡片堆叠。**
  - 在 `UiFactory` 增加细描边面板、分组标题、图标型紧凑按钮、选中态和工具提示的复用方法；统一青蓝高亮、低对比深色背景、8px 以下视觉圆角。
  - 所有布局以固定参考分辨率和响应式锚点/约束实现；不要继续依赖一长串无分组的绝对坐标。

- [ ] **Step 3: 重构四个现有视图与新增中央工具栏。**
  - `DataInputView` 将顶部表单划分为“数据源与模型”“任务与航段”“海流与位置”；各组固定的唯一主操作分别是 `加载数据`、`应用航段`、`应用海流设置`。全局 `运行仿真` 作为三组右侧的任务执行命令，不属于任何一个分组。海流分层编辑和网络加载保留在抽屉内。
  - `DashboardView` 改为左侧实时遥测和航向罗盘，保留现有数据与详情展开。
  - `OceanCommandToolbarView` 放在海流体上方：五个相机按钮、复位视角、速度色标、轻量 LOD/暂停提示和显示开关；它不提供第二个暂停命令。
  - `StatusPanelView` 改为右侧可扫描状态，显示任务进度、剩余水平航程、ETA、预测、工程校核和小型航迹剖面。
  - `PlaybackControlsView` 只保留唯一的底部开始/暂停、重置、速度、时间轴、导出、退出等主控。

- [ ] **Step 4: 运行 UI 回归测试和人工分辨率检查。**
  - Run: `powershell -ExecutionPolicy Bypass -File ..\scripts\test-editmode.ps1`
  - Expected: UI 测试通过；在 1920×1080 与 1366×768 下，顶部/侧栏/底部不相互覆盖，所有控件文字可读。

## 5. 统一海流来源、短状态文案与失败保留策略

**Files:**
- Modify: `Assets/Scripts/UI/DataInputView.cs`
- Modify: `Assets/Scripts/UI/StatusPanelView.cs`
- Modify: `Assets/Scripts/Telemetry/OceanCurrentQualityEvaluator.cs`
- Modify: `Assets/Scripts/Telemetry/CopernicusCurrentResponseParser.cs`
- Modify: `Assets/Scripts/Bootstrap/RuntimeDataSourceState.cs`
- Test: `Assets/Tests/EditMode/UiTests.cs`
- Test: `Assets/Tests/EditMode/OceanCurrentProfileTests.cs`

- [ ] **Step 1: 先写失败测试，固定用户可见结果。**
  - 成功下载后测试 `LayeredPreferred` 与 `NetworkPreferred` 分别选择期望来源；首选来源为空或不可用时才退到另一来源。
  - 失败下载时断言 `OceanCurrentProfile`、`OceanCurrentField` 和渲染选择均保留旧实例/旧内容；提示应按实际保留对象显示，例如 `海流下载失败：保留当前联网格点场，可重试` 或 `海流下载失败：保留当前 3 层分层场，可重试`。
  - 用一个解析失败或质量不足的联网响应验证候选对象不会写入当前配置；仅当下载、解析、覆盖质量检查全部成功时才一次性替换联网场候选数据，且不改写手工层。
  - 覆盖 `StatusPanelView`，确保不会显示 `manifest.json`、模型文件路径、`artifacts`、异常堆栈或其他调试串。

- [ ] **Step 2: 接入显式来源选择和短格式状态。**
  - 在 `SimulationProfile`/运行态加入唯一可序列化的 `OceanCurrentSourcePreference`；默认 `LayeredPreferred`。Resolver 不接收第二个来源布尔值，UI 也不维护镜像状态。
  - `DataInputView` 的联网加载先生成暂存候选，完成下载、解析和质量检查后才原子替换联网场；它不隐式覆盖用户手工分层或自动切源。提供紧凑的来源切换控件和状态摘要，并明确显示“分层均匀场”或“联网格点场”。
  - `StatusPanelView.FormatPredictionStatus`、工程校核和任务进度改为稳定短格式，如 `预测：可用`、`校核：通过`、`航段：2 / 6`；异常给出下一步操作而非内部错误。

- [ ] **Step 3: 运行数据来源和状态文案测试。**
  - Run: `powershell -ExecutionPolicy Bypass -File ..\scripts\test-editmode.ps1`
  - Expected: 下载失败不丢数据、来源优先级可预测、状态区无调试残留。

## 6. 端到端验证、性能护栏与 Windows 构建

**Files:**
- Modify: `Assets/Tests/EditMode/OceanVolumeViewTests.cs`
- Modify: `Assets/Tests/EditMode/VisualizationTests.cs`
- Modify: `Assets/Tests/EditMode/UiTests.cs`
- Create: `Assets/Tests/PlayMode/CommandCenterVisualTests.cs`
- Modify: `docs/superpowers/specs/2026-07-21-volumetric-current-command-center-design.md`（仅补充最终验证记录，如有必要）

- [ ] **Step 1: 增加预算和不重建行为的回归测试。**
  - 以深水任务、密集当前场、长轨迹和相机切换构造测试数据；断言可见箭头 `<= 360`、候选排序稳定、安全通道清晰。
  - 验证相机模式切换不会调用源数据/ENU 候选缓冲重建，也不会创建 `GameObject`、网格或材质；允许更新可见实例集合。播放索引、暂停状态、状态面板值不变化。

- [ ] **Step 2: 添加 PlayMode 渲染和布局冒烟测试。**
  - 在已加载的命令中心场景中检查海流箭头为透明且不写深度，轨迹/滑翔机/关键标记队列高于箭头并仍开启对不透明环境的深度测试。
  - 在 1920×1080 和 1366×768 下检查顶部、左右侧栏、中央工具栏和底部主控 RectTransform 不相交；此测试不将帧率或视觉质量误判为 EditMode 可以证明的内容。

- [ ] **Step 3: 在运行时人工验收。**
  - 启动参数仿真：旋转全局、俯视、侧视与自由环绕，确认能一眼看见前后深度层、近远衰减和轨迹通道。
  - 暂停后继续旋转/平移/缩放；双击验证各预设复位语义；模拟网络失败并确认海流图仍保持可见。
  - 用 Unity Profiler 在默认 1920×1080 场景验证目标 60 FPS，最低不低于 45 FPS；记录超过预算时的 LOD 级别和实际箭头数。

- [ ] **Step 4: 执行完整自动化验证。**
  - Run: `powershell -ExecutionPolicy Bypass -File ..\scripts\test-editmode.ps1`
  - Expected: 全部 EditMode 测试通过。
  - Run: 使用 Unity Test Runner 执行 PlayMode tests。
  - Expected: `CommandCenterVisualTests` 通过。
  - Run: `powershell -ExecutionPolicy Bypass -File ..\scripts\build-windows.ps1`
  - Expected: Windows 构建成功，生成可运行包且无编译错误。

## Verification Checklist

- [ ] ENU、深度正方向和 `w` 的定义在轨迹、箭头和侧视中一致。
- [ ] ENU 候选格点不超过 1,440，可见箭头不超过 360，近景/远景衰减、安全通道与关键标记遮挡规则正确。
- [ ] 播放时间推进会刷新联网格点场箭头、剔除失效箭头并补选有效候选，且不重建 ENU 候选缓存或创建渲染对象；相机切换仅重排可见实例；两类刷新无持续 GC 分配。
- [ ] 五种视角和复位规则完整；相机交互不影响暂停、任务、轨迹或海流数据状态。
- [ ] 顶部配置分组明确，中央无重复暂停，右侧无调试残留，底部是唯一主控区。
- [ ] 联网失败不会清空已有海流；来源优先级可解释、可切换、可测试。
- [ ] EditMode 测试与 Windows 构建均通过。
