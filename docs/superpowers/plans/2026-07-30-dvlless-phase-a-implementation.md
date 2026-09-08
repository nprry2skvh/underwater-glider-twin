# 无 DVL 三维轨迹推算 Phase A 实施计划

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** 交付不依赖 DVL、XGBoost、TCN 或 IMM 的可运行模型 A：融合 GPS/CTD/AHRS，以物理模型和保守 GP 先验推算六维状态，并输出绑定未来控制假设的 30/60/120/300 秒轨迹及不确定度。

**Architecture:** 新建 Python 包 NavigationCore 作为唯一的 Phase A 估计器。状态为 pN,pE,pD,cN,cE,cD；AHRS 和控制量作为输入，GPS 仅更新水平位置，CTD 仅更新深度。预测器在当前 UKF 均值/协方差上前向传播，不调用 ML；Unity、WebSocket 和安全自动控制属于后续阶段。

**Tech Stack:** Python 3.12, NumPy, SciPy, pytest。仅在 GP artifact 加载器落地后再增加 GPyTorch runtime；Phase A 的无 artifact 路径不得依赖 GPyTorch。

## 全局约束

- NED 坐标，D 向下为正；Body 轴为 x 前、y 右、z 下，内部角度为 radians。R_n_b = Rz(yaw) @ Ry(pitch) @ Rx(roll)，表示 Body 到 NED；正 pitch 是抬头，因此在 yaw=roll=0 时 Body 前向速度的 D 分量为负。
- 地速必须是 R_n_b @ v_water_body + c。
- 不创建 DVL、vw 或 ba 在线状态；不加载 XGBoost、TCN 或 IMM。
- Phase A 接受 0 < dt_s <= 1.0，场景默认 dt_s 为 0.1；预测时域为 30/60/120/300 秒。
- GPS 的直接测量函数仅观测 pN,pE；CTD 的直接测量函数仅观测 pD；AHRS 只作时间对齐姿态输入。位置与海流已有交叉协方差时，GPS/CTD 允许通过标准 UKF 交叉项间接校正相关海流状态，且该间接更新必须可审计。
- GP artifact 缺失、无效或 OOD 时，alpha_gp 为 0，海流均值不吸引，过程噪声增加，健康状态降级。
- 三种控制假设必须互斥并在每个预测快照中显式输出。
- Phase A 不定义 AdcpObservation 或 update_adcp；ADCP 只在后续阶段作为可选海流观测加入。
- 初始噪声配置固定为 tau_c_ne=300 s、tau_c_d=60 s、Qp=dt_s*diag(0.04,0.04,0.01) m2、Qc_base=dt_s*diag(2.5e-6,2.5e-6,1.0e-6) (m/s)2，OOD 增量为 dt_s*diag(2.5e-5,2.5e-5,1.0e-5) (m/s)2；全部配置版本化并在后续阶段校准。
- Freshness 配置固定为 max_ahrs_age_s=0.20、max_ctd_age_s=2.50、max_control_age_s=1.00、max_physics_age_s=0.20、max_gps_age_s=30.0；测试必须覆盖每个阈值的边界。PSD 修正先使用状态尺度 diag(100,100,100,0.5,0.5,0.2) 归一化协方差，再以 psd_eigenvalue_floor_normalized=1e-10 投影；快照诊断记录修正是否发生、最小归一化特征值和修正次数。

---

## 文件结构

~~~text
NavigationCore/
  pyproject.toml
  src/aug_twin_nav/
    contracts.py
    math3d.py
    physics.py
    gp_prior.py
    ukf6.py
    observability.py
    predictor.py
    engine.py
    cli.py
  tests/
    conftest.py
    test_contracts.py
    test_rotation.py
    test_physics.py
    test_gp_prior.py
    test_ukf6_measurements.py
    test_prediction_contract.py
    test_engine_nominal.py
~~~

### Task 1: 建立 6 维契约、时间语义和控制预测契约

**Files:**
- Create: NavigationCore/pyproject.toml
- Create: NavigationCore/src/aug_twin_nav/contracts.py
- Create: NavigationCore/tests/test_contracts.py

**Interfaces:**

~~~python
State6(position_ned_m, current_ned_mps)
AhrsInput(measurement_time_s, yaw_rad, pitch_rad, roll_rad, healthy)
GpsObservation(measurement_time_s, north_m, east_m, covariance_ne_m2)
CtdObservation(measurement_time_s, depth_m, variance_m2)
ControlCommand(effective_time_s, buoyancy_command, rudder_command_rad, target_depth_m, target_heading_rad)
ControlPlan(assumption, version, valid_until_s, commands)
PredictionSnapshot(...)
~~~

- [ ] 写入 test_state6_rejects_any_shape_other_than_three_plus_three。
- [ ] 写入 test_gps_observation_has_no_depth_field。
- [ ] 写入 test_control_plan_requires_exactly_one_assumption。
- [ ] 运行 python -m pytest NavigationCore/tests/test_contracts.py -q；确认因生产接口不存在而失败。
- [ ] 实现有限值、形状、单条观测的合法时间范围和控制假设验证；跨观测的时间单调性只由后续 Engine/scheduler 验证。
- [ ] 重跑该测试文件；预期 PASS。
- [ ] 提交：feat: add dvlless phase-a contracts。

### Task 2: 实现 NED 旋转与无 DVL 地速回归

**Files:**
- Create: NavigationCore/src/aug_twin_nav/math3d.py
- Create: NavigationCore/tests/test_rotation.py

**Interfaces:**

~~~python
def r_n_b(yaw_rad, pitch_rad, roll_rad): ...
def ground_velocity_ned(v_water_body_mps, current_ned_mps, attitude): ...
~~~

- [ ] 写入 identity 姿态保持速度方向、海流加法、旋转矩阵正交性、正俯仰将 Body 前向速度映射为负 D 分量四个失败测试。
- [ ] 海流加法测试必须断言 [0.3,0,0] + [0,0.2,0] 等于 [0.3,0.2,0]。
- [ ] 运行测试并确认 RED。
- [ ] 仅实现 body-to-NED 旋转和加法地速公式。
- [ ] 重跑测试；预期 PASS。
- [ ] 提交：feat: add ned ground velocity convention。

### Task 3: 实现可校准物理相对水速模型

**Files:**
- Create: NavigationCore/src/aug_twin_nav/physics.py
- Create: NavigationCore/tests/test_physics.py

**Interfaces:**

~~~python
GliderParameters(nominal_speed_mps, pitch_speed_gain, buoyancy_speed_gain, max_speed_mps)
def relative_water_velocity_body(control, attitude, depth_m, parameters): ...
~~~

- [ ] 写入中性命令给出标定前向速度、正俯仰经 Task 2 旋转后使 NED D 分量为负、速度受最大值限制三个失败测试。
- [ ] 运行测试并确认 RED。
- [ ] 实现最小确定性模型：标定前向速度加执行器项、Body 前向速度由 Task 2 的明确姿态旋转产生 NED 垂向分量、总速度限幅。
- [ ] 重跑测试；预期 PASS。
- [ ] 提交：feat: add dvlless physical water velocity model。

### Task 4: 实现 GP 海流先验与无 artifact/OOD 保守回退

**Files:**
- Create: NavigationCore/src/aug_twin_nav/gp_prior.py
- Create: NavigationCore/tests/test_gp_prior.py

**Interfaces:**

~~~python
CurrentPrior(mean_ned_mps, covariance_m2ps2, domain, alpha_gp)
class GpCurrentPrior:
    def evaluate(self, position_ned_m, time_s): ...
~~~

- [ ] 写入缺 artifact 返回零均值、alpha 为零和降级健康状态的失败测试。
- [ ] 写入 OOD alpha 为零且协方差大于域内、域内 alpha 为一的失败测试。
- [ ] 运行测试并确认 RED。
- [ ] 实现 loader 接口与确定性测试 fixture；生产 GP 反序列化可留到后续，但缺失必须走保守路径。
- [ ] 重跑测试；预期 PASS。
- [ ] 提交：feat: add conservative gp prior fallback。

### Task 5: 实现 GPS/CTD 方向分离的 6 维 UKF

**Files:**
- Create: NavigationCore/src/aug_twin_nav/ukf6.py
- Create: NavigationCore/tests/test_ukf6_measurements.py

**Interfaces:**

~~~python
class Ukf6:
    def predict(self, control, attitude, dt_s, prior): ...
    def update_gps(self, observation): ...
    def update_ctd(self, observation): ...
    def state_and_covariance(self): ...
~~~

- [ ] 写入 GPS 测量向量只有水平两维且在交叉协方差为零时不改变深度、CTD 测量向量只有深度一维且在交叉协方差为零时不改变水平位置、GPS 在人为设置的位置—海流交叉协方差时可间接降低相关海流不确定度、无 GPS 时水平位置方差增长、协方差保持对称半正定的失败测试。
- [ ] 运行测试并确认 RED。
- [ ] 实现 Merwe sigma points。模型 A 使用 g=alpha_gp*(1-rho_c)、p_next=p+dt_s*(v_water_ned+c)、c_next=c+g*(c_gp-c)，rho_c=exp(-dt_s/tau_c)。使用 Qp、Qc_base、g2*Sigma_gp 和 Qood 组成过程噪声；实现 GPS 2D 更新、CTD 1D 更新、NIS 记录、协方差对称化和 PSD 下限。
- [ ] 重跑测试；预期 PASS。
- [ ] 提交：feat: add six-state gps-ctd ukf。

### Task 6: 实现健康状态和控制绑定的轨迹预测器

**Files:**
- Create: NavigationCore/src/aug_twin_nav/observability.py
- Create: NavigationCore/src/aug_twin_nav/predictor.py
- Create: NavigationCore/tests/test_prediction_contract.py

**Interfaces:**

~~~python
def classify_health(has_fresh_ahrs, has_fresh_ctd, has_valid_control, has_valid_physics, has_recent_gps): ...
class TrajectoryPredictor:
    def predict(self, state, covariance, controls, horizons_s): ...
~~~

- [ ] 写入 CTD 新鲜但 AHRS 或控制/物理输入不足时为 DEPTH_ONLY、AHRS/CTD/控制/物理正常且无 GPS 时为 DEAD_RECKONING、CTD 失效或多个核心输入失效时为 UNRELIABLE 的失败测试；每个 freshness 阈值必须验证恰好等于阈值仍新鲜、超过阈值即失效。
- [ ] 写入快照包含四个预测时域和控制元数据、PLANNED_COMMANDS 与 ZERO_ORDER_HOLD 明确不同的失败测试。
- [ ] 运行测试并确认 RED。
- [ ] 实现模型 A 前向传播和完整 6x6 协方差传播。水平 P50/P90 使用 chi-square 二维椭圆，深度 P50/P90 使用正态区间；快照输出椭圆长短轴、方向、保守半径。PSD 修正必须在归一化协方差上使用 1e-10 floor 并写入诊断。PLANNED_COMMANDS 按 effective_time_s 零阶保持，计划缺口时从缺口开始显式切换 ZERO_ORDER_HOLD，并记录实际控制分段。
- [ ] 重跑测试；预期 PASS。
- [ ] 提交：feat: add phase-a trajectory prediction contract。

### Task 7: 集成运行、固定场景和 Phase A 验收

**Files:**
- Create: NavigationCore/src/aug_twin_nav/engine.py
- Create: NavigationCore/src/aug_twin_nav/cli.py
- Create: NavigationCore/tests/test_engine_nominal.py
- Create: NavigationCore/tests/scenarios/nominal_no_dvl.json
- Create: NavigationCore/tests/scenarios/gps_denied_ood.json

**Interface:**

~~~python
class NavigationEngine:
    def step(self, simulation_time_s, control, ahrs, observations): ...
~~~

- [ ] 写入正常场景无任何 DVL 输入即可运行、GPS 拒止加 OOD 场景报告 DEAD_RECKONING 且水平不确定度增长、GPS 恢复更新被接受且记录 NIS、水平不确定度下降、协方差仍 PSD、状态跳变处于配置上限内的失败测试。
- [ ] 运行测试并确认 RED。
- [ ] 实现确定性场景 runner 与命令 python -m aug_twin_nav.cli --scenario nominal_no_dvl。
- [ ] 运行 python -m pytest NavigationCore/tests -q；预期全绿。
- [ ] 运行 nominal 和 OOD CLI 场景；输出仅写入临时测试目录。
- [ ] 提交：feat: complete dvlless phase-a baseline。

## Phase A 出口门

Phase A 完成条件：

1. NavigationCore/tests 全部通过。
2. 正常场景不需要 DVL、XGBoost、TCN 或 IMM。
3. 地速加号、GPS 仅水平、CTD 仅深度、无 GPS 协方差增长、GP OOD 和三种控制假设测试通过。
4. 每个预测快照有健康状态、四个时域、协方差推导的 P50/P90 包络和控制假设/版本/有效期。
5. 水平约束消失时快照诚实输出 DEAD_RECKONING 或 UNRELIABLE。

## 刻意延后

Phase A 不实现 XGBoost、IMM、TCN、WebSocket、Unity 传输、自动安全命令、DVL 适配器或 GPS 端点弱监督训练。这些属于 Phase B–D，需单独计划和审核。
