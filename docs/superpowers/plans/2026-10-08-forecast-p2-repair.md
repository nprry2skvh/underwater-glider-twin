# Forecast P2 Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复独立复审R1–R5，证明边界回归并重新交付运行包。
**Architecture:** Controller按参数段区分补片/热更新，Ledger保留档案并隔离仿真时间域；冻结请求时长避免float差值身份匹配。Python保留真值质量/分支元数据，位置插值双端验证，航向候选用环绕MAE。
**Tech Stack:** Unity/C#/NUnit、Python/pandas/unittest。
**Spec:** `docs/audits/2026-10-08-prediction-validation-independent-review.md`；基础契约 `docs/superpowers/specs/2026-10-08-causal-forecast-validation.md`，确认规则 `DECISIONS.md`。

## Global Constraints
- 用户“开始”授权按已确认顺序修复、验收和提交；保留现有分支，不合并/推送，不替换Models或动力学默认值。
- 每项先失败回归再修复；支持30/60/300/900秒、10秒格点；评分容差0.5秒、插值间隔20秒、等待60秒不变。
- 旧预测/观测缓存保留；新分支不受旧分支时钟过期；真实观测仍可用于旧预测评分。
- 显式质量false不得丢弃或当True；位置插值双端均有效，不扩大到分指标评分重构。
- 仅航向使用环绕候选误差，其他输出仍线性MAE；final_test不进入拟合/选择。
- 保留旧运行包及本轮修复前运行包；实测精度与真实残差训练仍未验收。

## Review Focus
- 缓存末帧热更新与同参数追加的区分，海流版本及观测分支身份（Task1）。
- 旧分支先走远、回看创建新分支，以及同分支回放缓存对齐；显式超时入口不污染后创建的分支（Task1）。
- 分数实际可用时间和非整数发布时刻；成功历史回看≥290秒且不重复冻结（Task2）。
- false字符串/0/缺失或非法质量值、分支元数据、插值任一端点无位置参考（Task3）。
- ±180度边界、限幅后候选误差、非航向线性误差与final_test隔离（Task4）。

### Task 1: R1＋R2 分支及时间域
**Files:** PredictionController.cs、ForecastLedger.cs；ForecastReviewRegressionTests.cs、CausalForecastIntegrationTests.cs。
**Interfaces:** 保留 `Publish` 既有签名；增加 `ExpireBranch(float receivedSeconds, string branchId, bool isSimulation=true)`；Ledger内部时间域为(是否仿真,branch)，真实观测可对已有仿真域推进，旧仿真域不能推进新域。
- [x] 测试 `OldSimulationClockCannotExpireNewBranchForecast`：旧域1000，新域390发布400目标，先pending后400真值评分5米；`BranchDeadlineDoesNotExpireAnotherBranch`：显式域超时互不影响。
- [x] PlayMode测试 `TailProfileUpdateCreatesNewBranchAndCurrentVersion`：790秒热更新、保留80旧帧追加seq1，新branch/hash；旧800目标不得接受seq1观测。`SameProfileAppendKeepsExistingBranch` 确认自动补片身份不变。
- [x] Run EditMode与PlayMode全量。Expected: 新边界断言FAIL，已有测试PASS。
- [x] 根据新future的ProfileSequence/Timeline参数段识别热更新；Ledger的缓存对齐与过期分别用可评分事件和所属域时钟，保留旧档案。兼容Expire仅推进已注册域，不给后创建的域继承全局时间。
- [x] Run两套全量。Expected: PASS。Commit `fix: isolate forecast branches and simulation deadline clocks`。

### Task 2: R3 冻结请求身份及成功Seek
**Files:** FrozenForecast.cs、ForecastLedger.cs、PredictionController.cs、CausalForecastIntegrationTests.cs。
**Interfaces:** 新 `PublishRequest` 参数与旧Publish相同，尾参数 `float requestedHorizonSeconds`；FrozenForecast增加 `RequestedHorizonSeconds`，Controller使用此值筛选。旧Publish作兼容入口，不改反射调用签名。
- [x] `FractionalOriginShowsAvailableDelayedScore`：起点510.1、30秒预测、末端540.1，到达真值后UI RMSE等于ledger且非NaN；历史Seek起点改300秒并断言Frames非空、无Failure、origin/branch保持不变。
- [x] Run PlayMode。Expected: fractional UI断言FAIL，成功Seek明确通过。
- [x] 冻结原始请求时长；生产入口及失败档案均使用PublishRequest，UI不再由终点减起点匹配请求时长。
- [x] Run EditMode/PlayMode全量。Expected: PASS。Commit `fix: retain requested forecast horizon for delayed score display`。

### Task 3: R4 真值质量和分支元数据
**Files:** train_models.py、validate_forecasts.py、tests/test_review_regressions.py。
**Interfaces:** `load_csv`保留 `has_position_reference`、`branch_id`、`truth_grade`；严格解析bool真/假、1/0，缺失/非法显式质量拒绝；无该列维持标记为未知来源的导航记录一致性回放。
- [x] CSV集成反例所有false不得scored；false/0/非法/缺失解析、分支列保留；插值上下端任一false不得scored；禁止跨branch/真值等级插值。
- [x] Run `py -3.14 -m unittest discover -s PredictionTraining/tests -q`。Expected: 新反例FAIL。
- [x] 质量/分支字段不参与数值转换或ffill；导入保留后传入评分；插值两端有效位置参考、同branch/grade且有限物理位置才可用。
- [x] Run Python全量。Expected: PASS。Commit `fix: preserve truth reference quality through replay loading and alignment`。

### Task 4: R5 环绕残差候选选择
**Files:** residual_candidate.py、tests/test_residual_candidate.py。
**Interfaces:** `train_candidate`签名不变；heading的development_mae为abs(wrap(predicted-dev_y))，其余为abs(linear difference)。
- [x] `test_heading_candidate_selection_uses_wrapped_development_error`：真实XGBoost训练+179、dev−179，报告约2而非358且source=candidate；非航向误差及final_test隔离维持现有回归。
- [x] Run Python全量。Expected: heading断言FAIL。
- [x] 限幅后预测与dev标签之差仅对heading wrap；记录候选评估指标名称。
- [x] Run Python全量。Expected: PASS。Commit `fix: select heading residual candidates with circular error`。

### Task 5: 重新验收及提交
**Files:** 审计补充报告、HANDOFF、DECISIONS；生成两份导航/合成报告。
**Interfaces:** 消费Task1–4；记录各问题RED/GREEN、全部测试、构建与smoke以及新旧DLL哈希。
- [x] 重生成导航/合成回放报告；不把未知位置来源/训练重叠当独立精度验收。
- [x] 保留本轮修复前运行包 `Builds/UnderwaterGliderTwin.pre-p2-20261008`，禁止覆盖原备份。
- [x] Run Python、EditMode、PlayMode全量，Windows构建、默认Player smoke。Expected: 全通过、正常退出。
- [x] 一次新上下文审查本轮diff，重要发现用失败回归修复；不并发启动Unity、不派重复复审。
- [x] 保存验收证据、回读检查点及确认规则，提交有用的复审报告和本轮改动，保留分支，不合并/推送。
