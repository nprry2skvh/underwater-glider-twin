# Causal Forecast Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 落实因果预测、冻结记录、延迟评分和可复现独立回测。
**Architecture:** Python 管理因果预处理、固定清单与离线报告；Unity 推理使用独立请求时间轴，冻结服务拥有不可变预测及幂等评分，Controller 只负责播放联动和展示。保留已部署模型，物理预测复用事件驱动 stepper。
**Tech Stack:** Python/NumPy/pandas/unittest；Unity 2022.3/C#/NUnit。
**Spec:** `docs/superpowers/specs/2026-10-08-causal-forecast-validation.md`

**完成记录（2026-10-08）：** 任务1–6及一次独立审核修复均已完成；功能修复提交 `98cb2b7`。最终五项验收通过：Python45/45、EditMode520/520、PlayMode18/18、Windows构建、默认Player smoke。证据及裁决见 `docs/audits/2026-10-08-prediction-validation.md`。真实残差性能与独立水下精度不属于已通过项。

## Global Constraints
- 支持时长 30、60、300、900 秒；默认输出步长 10 秒；其他请求 unsupported。
- 缺接收时间标记 sample_time_replay；缺独立位置真值不声称实测精度。
- 时间容差 0.5 秒、插值最大间隔 20 秒、等待期限 60 秒，随结果保存。
- 不替换 Models、不修改动力学默认值；在现有隔离分支工作，不合并/推送。
- 每项先 RED 再 GREEN；提交有用代码和验证证据，保留前版本。

## Review Focus
- 延迟到达、乱序和重复观测不改变已冻结输入或重复评分（任务 1/3）。
- 末帧请求、非法时长与间隔不得偷取真值或被静默截短（任务 2）。
- 不足独立连续段、跨边界目标或合成来源不得形成最终实测通过结论（任务 1/4）。
- 参数热更新后旧预测保留，仿真真值不能跨 branch（任务 3）。
- 缺状态/profile/海流的物理或残差方法不得以默认伪数据冒充可用（任务 5）。

### Task 1: 因果预处理与固定切分
**Files:** `PredictionTraining/forecast_data.py`，`PredictionTraining/train_models.py`，`PredictionTraining/tests/test_forecast_data.py`。
**Interfaces:** Produces `causal_history(frame, issued_seconds, window_size=30, sample_interval_seconds=10)`；`build_split_manifest(segments, source_sha256)`；`eligible_origins(start,end,history_seconds,horizon_seconds)`。
- [x] 写测试：首行缺值不得被未来填补；10 秒格点仅保持过去值；received_seconds=100 的观测在 issued=20 不可见；三份清单互斥且目标不得跨 end。
- [x] Run `py -3.14 -m unittest discover -s PredictionTraining/tests -q`。Expected: 新 API 缺失/旧 bfill 与插值断言 FAIL。
- [x] 实施因果 API，移除旧加载器 bfill、旧特征 dt bfill，重采样改过去保持；固定清单不足三段拒绝最终测试。
- [x] 同命令 Expected: PASS（基线已知 datetime.utcnow 弃用警告单独保留）。
- [x] Commit `fix: make forecast preprocessing causal and freeze dataset splits`。

### Task 2: 独立推理时间轴
**Files:** PredictionWindow/Builder、XGBoostPredictor、PredictionResult、PredictionController、RuntimePredictionState，EditMode/PredictionTests。
**Interfaces:** Produces `PredictionWindow.TargetElapsedSeconds`、`HorizonSeconds`；`PredictionResult.ForecastFrames` 和 `TargetElapsedSeconds`；真实输出无 ActualPoints。
- [x] 写测试：39 起点仅保留 40 历史帧，60 秒预测输出 400..450 六点；未来时间改变/删除预测不变；7200 请求 unsupported。
- [x] Run `powershell -NoProfile -File scripts/test-editmode.ps1`。Expected: 新时间轴/独立推理断言 FAIL。
- [x] Builder 新增历史专用 BuildForecast；XGBoost 只按该目标轴求锚点，输出目标 TelemetryFrame；Controller 不传未来帧。
- [x] 同命令 Expected: 全量 EditMode PASS。
- [x] Commit `fix: decouple forecast inference from future observations`。

### Task 3: 冻结与延迟评分
**Files:** `FrozenForecast.cs`、`ForecastLedger.cs`、PredictionController、PredictionSnapshot、StatusPanelView/Data；新增 EditMode 测试。
**Interfaces:** Consumes Task 2 physical ForecastFrames；Produces ledger `Publish`/`Observe`/`Expire`，不可变记录及 ScoreSummary，配置和值随记录保存。
- [x] 写测试：输入数组变更不改记录；同一 seek 键不重发；重复 Observe 只评分一次；不同 simulation branch 不评分；ENU 已知 3/4/5 米误差与缩放无关；角度359/1误差2；缺测到 deadline 不为零。
- [x] Run EditMode。Expected: ledger 未定义/断言 FAIL。
- [x] 实施 SHA-256 血缘、深复制、按时对齐和受限真值插值、幂等键；Controller 在观测到达时更新现有记录，热更新保留旧 branch；无评分 UI 用“—”。
- [x] Run EditMode。Expected: PASS。
- [x] Commit `feat: freeze forecasts and score observations after arrival`。

### Task 4: 可复现回放与审计报告
**Files:** `PredictionTraining/validate_forecasts.py`、`tests/test_forecast_replay.py`、审计报告。
**Interfaces:** Consumes Task 1 manifests/causal histories；Produces frozen JSONL、scores JSONL、quality_report.json、comparison_report.json/Markdown；部署树按已有 JSON 格式求值。
- [x] 写测试：改变未来及晚到历史不改预测；manifest 源 hash 不匹配拒绝；测试分区不拟合；失败/缺测入分母、未知导航标明一致性、无区域覆盖率 null。
- [x] Run Python unittest。Expected: 新回放 API 未定义 FAIL。
- [x] CLI 先审计 CSV/保存固定清单，再按指定清单只用历史跑恒速与部署模型；物理/残差未提供配对预测时明确 unavailable/not_trained。
- [x] Run Python suite 并在根目录两份 CSV 上执行审计/回测，保存配置、来源 hash 和固定清单。Expected: PASS；报告不出现独立精度通过声明。
- [x] Commit `feat: add reproducible forecast replay and honest validation reports`。

### Task 5: 真实物理对照和残差候选接口
**Files:** `EventDrivenPhysicsForecaster.cs`、EditMode 物理测试、Python residual training/测试。
**Interfaces:** Consumes Task 2 target axis 与 SimulationStateSnapshot；Produces 只用起点 seed/profile 的物理帧，以及配对物理预测输入的 residual candidate（不覆盖部署模型）。
- [x] 写测试：物理续推与相同 seed 的 stepper 一致且不读取未来；缺 profile/state 明确失败；残差只 fit train、dev 固定限幅，final_test 不能参与拟合。
- [x] Run Python/EditMode。Expected: API 未定义 FAIL。
- [x] 复用 FromSnapshot/TryAdvance 生成目标帧；实现六输出残差候选并保存 fit/development 清单 hash，实际输入不满足时说明未训练。
- [x] Run Python/EditMode。Expected: PASS。
- [x] Commit `feat: add event-driven physics and residual validation candidates`。

### Task 6: 集成验收与交付
**Files:** PlayMode 集成测试、`docs/audits/2026-10-08-prediction-validation.md`、HANDOFF。
**Interfaces:** Consumes Task 1–5；验收四项防泄漏/缩放/回放门槛。
- [x] 写并验证 Controller 播放/seek/替换未来集成测试 RED→GREEN。
- [x] Run Python 全量、EditMode 全量、PlayMode 全量、Windows build 与默认烟测。Expected: 全通过；若原生退出挂起与已有环境问题如实分开记录。
- [x] 一次新上下文全分支 review，重要发现用 RED→GREEN 修复并重跑全量。
- [x] 保存最终报告/检查点并提交；不合并/推送、不删旧运行包、不宣称缺真值/未训练项已通过。
