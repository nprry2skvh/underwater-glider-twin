# 无 DVL轨迹推算 Phase B：XGBoost 与双模型 IMM 实施计划

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans task-by-task. Every production change starts with a failing test.

**Goal:** 在已通过的 Phase A 模型 A 上，加入经严格数据来源审计的 XGBoost 单步三维速度残差模型 B，并以标准双模型 IMM-UKF 输出可解释的模型概率和融合协方差。

**Architecture:** 模型 A 保持 Physics + GP；模型 B 为 Physics + GP + XGBoost 单步速度残差。两个 UKF 都维护六维 [pN,pE,pD,cN,cE,cD] 状态；IMM 完整执行交互、预测、分方向似然、概率保护和带模型分歧项的状态/协方差融合。普通真实 GPS 起终点仅用于航段级验证、校准和模型选择，绝不生成即时水平速度标签。

## Global Constraints

- 不修改 Phase A 的坐标、GPS/CTD、GP OOD、控制计划或预测包络契约。
- XGBoost 只输出单步三维速度残差，单位 m/s；dt_s 必须等于 0.1。
- 残差标签冻结为：`r_xgb(k) = v_ref_ground(k) - v_physics_ground(k)`。两项均为同一 NED 坐标、同一时间点、同一 0.1 s 步长的地速；`v_physics_ground` 已包含海流项。训练数据不得把海流估计、物理动力学和坐标变换误差重复标入同一个标签。
- artifact 保存的是 `unexplained_velocity_covariance_m2ps2`，即在 sigma-point 残差传播后仍未解释的速度残差协方差。运行时仅将其离散化到位置块：`Q_xgb_position = dt_s^2 * unexplained_velocity_covariance_m2ps2`；不可直接加到位置协方差，亦不得重复加入 sigma-point 已体现的扩散。
- 即时水平标签仅来自仿真真值或 USBL/LBL 等高精度参考轨迹。
- 模型 artifact 必须包含 feature order、单位、scaler、dt、训练标签来源、hash 和版本；任何不匹配均拒绝加载。
- 无 GPS/ADCP 时，CTD 垂向创新不得显著重排水平模型概率。
- 模型概率下限为 0.05；普通更新最大变化 0.10，GPS 水平更新后最大变化 0.25。
- IMM 转移矩阵统一采用 `Pi[i, j] = P(M_k=j | M_(k-1)=i)`：行是来源模型、列是目标模型、每行和为 1。模型交互使用 `c_j = sum_i Pi[i,j] * mu_i` 与 `mu_i_given_j = Pi[i,j] * mu_i / c_j`。
- 概率保护固定为有界单纯形投影：先计算似然后的原始后验，再投影到 `sum(mu)=1`、`mu_j>=0.05` 与 `|mu_j-mu_j_previous|<=delta` 的交集；`delta=0.10`，仅在当周期有有效 GPS 水平更新时为 `0.25`。投影后复查约束并记录触发项。双模型下该投影是确定性的；若约束集合因配置错误不可行，拒绝配置而非静默修改概率。

### Task 1: 定义残差训练数据、标签来源和 artifact 契约

**Files:**
- Create: PredictionTraining/navigation_hybrid/{contracts,feature_audit,labels}.py
- Create: PredictionTraining/tests/test_phase_b_contracts.py
- Create: NavigationCore/src/aug_twin_nav/xgb_artifact.py
- Create: NavigationCore/tests/test_xgb_artifact.py

- [ ] 先写测试：拒绝 dt 非 0.1、未来特征、普通 GPS 端点伪造的即时标签、hash/feature-order 不匹配 artifact；验证零海流、恒定海流、仅海流变化、仅物理误差四类标签均遵守冻结的地速残差定义。
- [ ] 定义 LabelSource 为 SIMULATION_TRUTH、REFERENCE_TRACK、GPS_ENDPOINT、CTD_ONLY；即时 XGBoost 标签只接受前两类。
- [ ] 定义 3 维 residual artifact manifest、校准协方差和 OOD 元数据。
- [ ] 跑测试并提交：feat: add audited phase-b residual contract。

### Task 2: 构建训练数据和三维 XGBoost 残差模型

**Files:**
- Create: PredictionTraining/navigation_hybrid/xgb_dataset.py
- Create: PredictionTraining/navigation_hybrid/train_xgb_residual.py
- Create: PredictionTraining/tests/test_phase_b_xgb_dataset.py
- Create: PredictionTraining/tests/test_phase_b_xgb_training.py

- [ ] 先写测试：标签等于参考地速减包含海流项的物理地速；按完整航次切分；动态缺失特征禁用模型而不是均值填充；验证标签、特征、参考速度均使用同一时刻和 NED 坐标。
- [ ] 实现仿真/参考轨迹的特征生成、按航次切分、三路单输出 XGBoost 和 `unexplained_velocity_covariance_m2ps2`；运行时以 `dt^2` 注入位置过程噪声。
- [ ] 导出可复现 artifact 与独立航次验证报告。
- [ ] 跑训练 smoke test 并提交：feat: train audited three-axis residual model。

### Task 3: 实现运行时 XGBoost 残差加载、批量推理和健康门控

**Files:**
- Create: NavigationCore/src/aug_twin_nav/xgb_residual.py
- Create: NavigationCore/tests/test_xgb_residual.py

- [ ] 先写测试：三维输出单位正确、OOD/特征不足返回禁用、artifact 版本错误拒绝、hash/feature-order/scaler 不匹配拒绝、dt 不为 0.1、推理超时、非有限或越限输出均记录原因并回退；相同输入确定性输出。
- [ ] 实现 artifact loader、scaler、三路 booster 推理、物理单位限幅、`unexplained_velocity_covariance_m2ps2` 到离散位置噪声的转换，以及完整 fallback reason。
- [ ] 跑测试并提交：feat: add gated xgboost residual runtime。

### Task 4: 实现标准双模型 IMM 交互与协方差混合

**Files:**
- Create: NavigationCore/src/aug_twin_nav/imm.py
- Create: NavigationCore/tests/test_imm.py

- [ ] 先写测试：转移矩阵行随机且行/列语义符合冻结定义、交互均值、交互协方差、融合协方差包含模型分歧项、概率总和为一。
- [ ] 实现冻结转移矩阵 [[0.985,0.015],[0.050,0.950]]，并提供版本化配置入口。
- [ ] 实现完整交互、模型预测、后验概率和 x/P 融合。
- [ ] 跑测试并提交：feat: add dual-model imm fusion。

### Task 5: 实现分方向似然、概率保护和 A/B 运行引擎

**Files:**
- Modify: NavigationCore/src/aug_twin_nav/engine.py
- Create: NavigationCore/tests/test_phase_b_engine.py

- [ ] 先写测试：无 GPS 时 CTD 不重排水平权重；GPS 更新后概率变化受 0.25 上限约束；下限、变化上限与归一化按冻结顺序执行且保持和为一；OOD/禁用 XGBoost 回退模型 A；Phase B 的模型 A 对同一输入与 Phase A 基线的状态、协方差、预测轨迹和健康状态完全一致。
- [ ] 将模型 A/B 作为独立 UKF 接入 IMM；GPS 贡献水平似然，CTD 贡献垂向似然。
- [ ] 快照增加 mode probabilities、概率保护诊断、residual health、fallback_reason、xgb_enabled、artifact id，以及每个模型的 `nis_gps`、`nis_ctd`、`log_likelihood_gps` 和 `log_likelihood_ctd`。
- [ ] 跑测试并提交：feat: integrate imm residual navigation engine。

### Task 6: 独立 A/B 验收与 Phase B 出口门

**Files:**
- Create: NavigationCore/tests/test_phase_b_acceptance.py
- Create: PredictionTraining/navigation_hybrid/phase_b_report.py
- Modify: docs/superpowers/specs/2026-07-30-dvlless-hybrid-trajectory-design.md

- [ ] 验证模型 B 相对 A 在独立仿真/参考轨迹上的 60/120 秒误差独立改善，且 P90 不恶化超过 5%；IMM 融合结果仅作为单独报告，不能替代 A/B 分别的收益结论。
- [ ] 验证无 artifact、OOD、特征不足和 GPS 拒止下可安全回退模型 A。
- [ ] 报告 A/B 误差、概率对数损失、NIS、覆盖率、延迟、标签来源与 artifact hash。
- [ ] 全量运行 NavigationCore/tests、PredictionTraining/tests 和阶段 B 场景；提交：test: verify phase-b imm residual acceptance。

## Exit Gate

Phase B 只有在模型 B 独立带来可重复收益、artifact 与时间审计通过、IMM 概率/协方差一致、所有故障路径回退模型 A 后才完成。TCN、WebSocket、Unity 和自动安全控制仍不属于本阶段。
