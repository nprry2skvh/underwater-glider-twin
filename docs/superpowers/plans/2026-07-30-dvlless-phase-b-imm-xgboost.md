# 无 DVL轨迹推算 Phase B：XGBoost 与双模型 IMM 实施计划

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans task-by-task. Every production change starts with a failing test.

**Goal:** 在已通过的 Phase A 模型 A 上，加入经严格数据来源审计的 XGBoost 单步三维速度残差模型 B，并以标准双模型 IMM-UKF 输出可解释的模型概率和融合协方差。

**Architecture:** 模型 A 保持 Physics + GP；模型 B 为 Physics + GP + XGBoost 单步速度残差。两个 UKF 都维护六维 [pN,pE,pD,cN,cE,cD] 状态；IMM 完整执行交互、预测、分方向似然、概率保护和带模型分歧项的状态/协方差融合。普通真实 GPS 起终点仅用于航段级验证、校准和模型选择，绝不生成即时水平速度标签。

## Global Constraints

- 不修改 Phase A 的坐标、GPS/CTD、GP OOD、控制计划或预测包络契约。
- XGBoost 只输出单步三维速度残差，单位 m/s；dt_s 必须等于 0.1。
- 即时水平标签仅来自仿真真值或 USBL/LBL 等高精度参考轨迹。
- 模型 artifact 必须包含 feature order、单位、scaler、dt、训练标签来源、hash 和版本；任何不匹配均拒绝加载。
- 无 GPS/ADCP 时，CTD 垂向创新不得显著重排水平模型概率。
- 模型概率下限为 0.05；普通更新最大变化 0.10，GPS 水平更新后最大变化 0.25。

### Task 1: 定义残差训练数据、标签来源和 artifact 契约

**Files:**
- Create: PredictionTraining/navigation_hybrid/{contracts,feature_audit,labels}.py
- Create: PredictionTraining/tests/test_phase_b_contracts.py
- Create: NavigationCore/src/aug_twin_nav/xgb_artifact.py
- Create: NavigationCore/tests/test_xgb_artifact.py

- [ ] 先写测试：拒绝 dt 非 0.1、未来特征、普通 GPS 端点伪造的即时标签、hash/feature-order 不匹配 artifact。
- [ ] 定义 LabelSource 为 SIMULATION_TRUTH、REFERENCE_TRACK、GPS_ENDPOINT、CTD_ONLY；即时 XGBoost 标签只接受前两类。
- [ ] 定义 3 维 residual artifact manifest、校准协方差和 OOD 元数据。
- [ ] 跑测试并提交：feat: add audited phase-b residual contract。

### Task 2: 构建训练数据和三维 XGBoost 残差模型

**Files:**
- Create: PredictionTraining/navigation_hybrid/xgb_dataset.py
- Create: PredictionTraining/navigation_hybrid/train_xgb_residual.py
- Create: PredictionTraining/tests/test_phase_b_xgb_dataset.py
- Create: PredictionTraining/tests/test_phase_b_xgb_training.py

- [ ] 先写测试：标签等于参考速度减物理速度；按完整航次切分；动态缺失特征禁用模型而不是均值填充。
- [ ] 实现仿真/参考轨迹的特征生成、按航次切分、三路单输出 XGBoost 和未解释残差协方差。
- [ ] 导出可复现 artifact 与独立航次验证报告。
- [ ] 跑训练 smoke test 并提交：feat: train audited three-axis residual model。

### Task 3: 实现运行时 XGBoost 残差加载、批量推理和健康门控

**Files:**
- Create: NavigationCore/src/aug_twin_nav/xgb_residual.py
- Create: NavigationCore/tests/test_xgb_residual.py

- [ ] 先写测试：三维输出单位正确、OOD/特征不足返回禁用、artifact 版本错误拒绝、相同输入确定性输出。
- [ ] 实现 artifact loader、scaler、三路 booster 推理、物理单位限幅和未解释 Q_xgb。
- [ ] 跑测试并提交：feat: add gated xgboost residual runtime。

### Task 4: 实现标准双模型 IMM 交互与协方差混合

**Files:**
- Create: NavigationCore/src/aug_twin_nav/imm.py
- Create: NavigationCore/tests/test_imm.py

- [ ] 先写测试：转移矩阵行随机、交互均值、交互协方差、融合协方差包含模型分歧项、概率总和为一。
- [ ] 实现冻结转移矩阵 [[0.985,0.015],[0.050,0.950]]，并提供版本化配置入口。
- [ ] 实现完整交互、模型预测、后验概率和 x/P 融合。
- [ ] 跑测试并提交：feat: add dual-model imm fusion。

### Task 5: 实现分方向似然、概率保护和 A/B 运行引擎

**Files:**
- Modify: NavigationCore/src/aug_twin_nav/engine.py
- Create: NavigationCore/tests/test_phase_b_engine.py

- [ ] 先写测试：无 GPS 时 CTD 不重排水平权重；GPS 更新后概率变化受 0.25 上限约束；OOD/禁用 XGBoost 回退模型 A。
- [ ] 将模型 A/B 作为独立 UKF 接入 IMM；GPS 贡献水平似然，CTD 贡献垂向似然。
- [ ] 快照增加 mode probabilities、residual health、artifact id 和各模型 NIS。
- [ ] 跑测试并提交：feat: integrate imm residual navigation engine。

### Task 6: 独立 A/B 验收与 Phase B 出口门

**Files:**
- Create: NavigationCore/tests/test_phase_b_acceptance.py
- Create: PredictionTraining/navigation_hybrid/phase_b_report.py
- Modify: docs/superpowers/specs/2026-07-30-dvlless-hybrid-trajectory-design.md

- [ ] 验证模型 B 相对 A 在独立仿真/参考轨迹上的 60/120 秒误差独立改善，且 P90 不恶化超过 5%。
- [ ] 验证无 artifact、OOD、特征不足和 GPS 拒止下可安全回退模型 A。
- [ ] 报告 A/B 误差、概率对数损失、NIS、覆盖率、延迟、标签来源与 artifact hash。
- [ ] 全量运行 NavigationCore/tests、PredictionTraining/tests 和阶段 B 场景；提交：test: verify phase-b imm residual acceptance。

## Exit Gate

Phase B 只有在模型 B 独立带来可重复收益、artifact 与时间审计通过、IMM 概率/协方差一致、所有故障路径回退模型 A 后才完成。TCN、WebSocket、Unity 和自动安全控制仍不属于本阶段。
