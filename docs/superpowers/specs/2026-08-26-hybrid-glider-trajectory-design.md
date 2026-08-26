# 混合动力学滑翔机仿真轨迹设计

**日期：** 2026-08-26  
**状态：** 待审阅  
**范围：** UnderwaterGliderTwin 参数仿真、运行时未来轨迹重建与轨迹诊断

## 目标

将当前参数仿真统一为一套可控、可重建、可测试的滑翔机轨迹算法：

- 俯仰、横滚、航向、目标深度和速度是控制目标，不被轨迹插值直接覆盖。
- 海流影响地面运动，同时不错误地替代相对水动力速度。
- 下潜、掉头、上浮由实际状态事件驱动，而不是只按时间正弦相位切换。
- 初始完整仿真与运行时未来增量仿真使用同一套状态转移逻辑。
- 相同输入快照产生相同轨迹，参数热更新只替换未来，不改变历史。

## 现状与问题

当前工程已经有 `GliderDynamicsState`、`GliderDynamicsProfile`、`GliderDynamicsIntegrator` 和 `SimulationTrajectoryGenerator`。积分器具备位置、地速、水速、姿态、角速度、浮力、活塞、控制面、功耗和电池状态，但生成器的完整轨迹与未来切片分别使用事件驱动路径和按时间种子的路径。

主要问题是：

1. 目标俯仰会与深度误差叠加，显示和输入之间缺少明确的“目标值/实际值”语义。
2. 轨迹航向、垂向运动、姿态控制和海流耦合分散在生成器与积分器中。
3. 未来切片使用另一套相位逻辑，参数更新后可能与已渲染历史的末端不连续。
4. 大步长虽然会被拆分，但接触约束、状态切换和控制更新没有形成统一的固定步长契约。

## 选定方案

采用“混合航段状态机 + 降阶三维水动力模型 + 固定步长半隐式 Euler”。

这不是纯运动学曲线，也不是当前参数不足以支撑的完整 6-DOF 模型。它保留可解释的水动力和控制状态，同时用固定步长与事件边界保证实时性、稳定性和可重复性。

### 航段状态机

```text
Surface -> Descent -> BottomTurn -> Ascent -> Surface
```

状态转移规则：

- `Surface -> Descent`：完成水面保持且开始新的循环。
- `Descent -> BottomTurn`：实际深度达到目标深度减去制动缓冲，或垂向速度与剩余距离表明即将越界。
- `BottomTurn -> Ascent`：掉头时间达到配置值，且目标深度方向已切换。
- `Ascent -> Surface`：实际深度小于水面容差且垂向速度已被约束。
- 任意状态 -> `SafetyWarning`：超过单航段安全时限。
- 任意状态 -> `LegTimeout`：超过安全时限的紧急倍数。

航段状态、已用时间、循环序号和当前目标应成为显式的可快照状态，不能只从总时间反推。

### 动力学状态

保留现有状态字段，并明确其含义：

```text
position                 世界/任务局部位置，单位 m
earthVelocity            相对地速度，单位 m/s
waterVelocity            相对水速度，单位 m/s
heading, pitch, roll     机体姿态，单位 deg
headingRate, pitchRate,
rollRate                 姿态角速度，单位 deg/s
netBuoyancyForce         净浮力，单位 N
pistonPosition           活塞位置，单位 mm
controlSurfaceDeflection 控制面偏角，单位 deg
batteryPercent           剩余电量
missionState             航段状态与状态计时器
```

海流约定为：

```text
waterRelativeVelocity = earthVelocity - currentVelocity
earthVelocity         = waterRelativeVelocity + currentVelocity
```

阻力、升力、攻角、侧滑角和水动力矩只能使用 `waterRelativeVelocity`。位置积分只能使用 `earthVelocity`。

### 控制层

仿真 profile 提供目标量：

- 下潜/上浮目标俯仰
- 下潜/上浮目标横滚
- 目标深度
- 目标航向
- 下潜/上浮速度
- 可选净浮力

控制层将目标量转换为执行器目标：

```text
depth error      -> buoyancy/piston target
pitch error      -> pitch-control-surface target
roll error       -> roll-control-surface target
heading error    -> yaw-control-surface target
```

执行器使用响应时间、最大偏角、最大角速度和阻尼逐步收敛。`PitchAmplitudeDeg = 11.2` 表示目标俯仰幅值；实际姿态允许因为响应延迟、海流、攻角和深度制动产生短暂偏差，但必须记录诊断值。

### 水动力模型

每个内部步长计算：

1. 根据当前姿态得到机体前、右、下方向。
2. 计算相对水速度、前向速度、侧向速度和下向速度。
3. 计算攻角与侧滑角。
4. 计算升力、阻力和侧向力，并进行系数限幅。
5. 计算推进、浮力和控制面力矩。
6. 计算姿态恢复、角阻尼和功耗。

水面、目标深度和掉头过程的约束在状态机边界处理，不通过事后把轨迹点硬裁剪来解决。

### 数值积分

内部统一使用固定步长，建议默认 `0.5 s`，允许在性能/精度测试后调整到 `0.25 s`：

```text
for each internal step:
    forces = EvaluateForces(state, controls, current)
    rates  = EvaluateStateRates(state, forces)
    state.velocity += rates.velocity * h
    state.position += state.velocity * h
    state.angularRate += rates.angularRate * h
    state.attitude += state.angularRate * h
    state = ApplyMissionConstraints(state, missionState)
```

这属于半隐式 Euler：先更新速度/角速度，再使用更新后的速度/角速度更新位置/姿态。输出采样间隔只负责抽取帧，不改变动力学步长。

所有 `deltaSeconds > 0.5 s` 的调用都必须拆成整数个内部步长加一个尾步长，并确保相同总时长在不同输出采样间隔下结果近似一致。

## 代码边界

实现阶段优先在现有边界内演进：

- `GliderDynamicsState.cs`：增加显式航段状态、状态计时器和必要的目标/诊断字段。
- `GliderDynamicsProfile.cs`：集中存放积分步长、约束容差、控制器和水动力参数；保持旧 profile 字段兼容。
- `GliderDynamicsIntegrator.cs`：只负责单步状态转移、力/矩评估和执行器响应，不负责循环航段切换。
- `SimulationTrajectoryGenerator.cs`：负责状态机、目标生成、完整轨迹和未来切片；完整轨迹与未来切片调用同一状态转移核心。
- `SimulationStateSnapshot.cs`：保存恢复未来仿真所需的完整状态，不能只保存当前遥测帧。
- `SimulationDiagnostics.cs`：暴露目标俯仰、实际俯仰、攻角、侧滑、深度误差和约束状态。
- `SimulationTelemetrySource.cs`：继续将生成状态映射为 `TelemetryFrame`，不在这里重新计算动力学。

不在本阶段引入机器学习模型、外部物理引擎或完整刚体 6-DOF 姿态四元数系统。

## 数据流

```text
SimulationProfile
        |
        v
MissionStateMachine -> target commands -> DynamicsIntegrator
        ^                                      |
        |                                      v
  StateSnapshot <----- next state <----- current field sampler
        |
        v
TelemetryFrame / SimulationDiagnostics
```

未来切片必须从 `SimulationStateSnapshot` 复制出的状态开始；不能重新以 `elapsedSeconds` 的正弦相位生成一条与当前状态无关的轨迹。

## 兼容策略

- 旧的对称 `PitchAmplitudeDeg`、`RollAmplitudeDeg` 继续有效。
- 有效的方向性下潜/上浮参数优先于旧对称参数。
- 旧 profile 缺少新增状态字段时使用确定性默认值。
- UI 仍编辑 profile，不直接编辑积分器内部状态。
- CSV 回放路径不受影响；本设计只作用于参数仿真。

## 验证标准

### 单元测试

- 固定步长拆分前后总时长结果在明确误差范围内一致。
- 目标俯仰 11.2° 时，稳态实际俯仰收敛到目标范围，且短暂误差有诊断记录。
- 下潜时深度单调增加，上浮时深度单调减少，掉头阶段不越过水面/目标深度约束。
- 海流只改变地速和地面位移，不把海流速度直接当成水动力速度。
- 相同 profile 与 snapshot 生成的完整/增量轨迹确定一致。
- 未来重建不会修改 snapshot 之前的历史帧。
- 控制面、活塞、浮力和电池值始终位于 profile 限制范围内。

### 集成测试

- 运行时修改俯仰、深度、海流和速度后，历史轨迹保持不变，未来轨迹连续。
- 连续播放、暂停、恢复和重建不会改变航段状态顺序。
- 1280×720 与 1366×768 只验证 UI 布局，不改变仿真数值。
- Windows Player 启动后能加载参数仿真并持续生成未来切片。

### 验收指标

默认 profile 下至少满足：

- 目标深度误差在到达阶段不超过配置容差加一个内部步长造成的最大位移。
- 目标俯仰的稳态误差不超过 `max(1°，目标值的 10%)`。
- 相同输入重复运行的关键帧位置、姿态和电量误差小于 `1e-4`。
- 未来轨迹替换时历史末帧位置和姿态不跳变。

## 非目标

- 不承诺从现有少量参数直接得到经过海试认证的高保真水动力结果。
- 不把显示层的姿态修正混入仿真层。
- 不借助轨迹后处理强行把模型姿态改成路径切线。
- 不改变 CSV 回放数据的原始姿态语义。

