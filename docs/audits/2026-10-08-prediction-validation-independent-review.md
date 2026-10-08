# 预测验证闭环独立复审（2026-10-08）

## 结论与范围

审核范围 `f5f294b..0b9df2a`，重点复核修复提交 `98cb2b7`。结论：需要修复后再验收，共确认 5 项 P2；当前不能将“无待修复问题”作为合并依据。既有测试全部通过，但新增边界反例未被覆盖。此次没有修改实现、模型或参数，没有提交、合并或推送。

复审由当前审核者检查 Python 回放/残差代码与交付证据，独立子审核者检查 Unity 运行时代码；当前审核者随后核对全部 Unity 发现的源码条件。Python 反例在内存/系统临时目录执行；Unity 子审核者报告已进行内存反例验证，没有启动 Unity 或编辑源文件。

## 已确认问题

### R1 / P2：在轨迹尾部热更新时没有创建新分支

位置：`UnderwaterGliderTwin/Assets/Scripts/Prediction/PredictionController.cs:128`。

`OnFramesReplaced` 只依据旧帧是否为新帧的完整相等前缀来判断是否新建分支。当在缓存最后一帧提交参数更新，保留全部旧帧、追加 ProfileSequence=1 的新未来时，`appendOnly=true`，没有更新 branchId、profileBranches 和海流配置哈希。新序列观测随后回退到旧 branchId，可参与旧预测评分，违反仿真分支隔离。

修复方向：区分同参数的未来补充与新增参数段；结合段身份/ProfileSequence/更新事件检测参数变更，而不能只检查帧前缀。增加在缓存末帧热更新后 branchId 和 currentVersion 更新、旧分支不接受新观测的回归。

### R2 / P2：旧分支接收时间会使新分支预测提前过期

位置：`UnderwaterGliderTwin/Assets/Scripts/Prediction/ForecastLedger.cs:62`（共享时钟在第20、99、104行）。

ledger 使用全局单调 `latestReceipt`。先回放 branch-0 到1000秒，再回看390秒并创建 branch-1、发布目标400秒的预测时，Publish 用旧的1000秒调用 TryFinalize。新分支还没有400秒观测，预测立即冻结为 missing；之后精确的 branch-1/400秒观测也不能恢复评分。

修复方向：仿真分支拥有各自的可用时间/过期进度，或把过期判断绑定到可评分的观测时间域。保留同一分支首次回看时重放已接收事件的行为。增加“旧分支已走远→回看热更新→新分支到达真值”的回归。

### R3 / P2：非整数起点导致有效评分被 UI 隐藏

位置：`UnderwaterGliderTwin/Assets/Scripts/Prediction/PredictionController.cs:280`。

ToSnapshot 通过最后目标时间减起点时间与 HorizonSeconds 的 float 精确相等判断筛选档案。起点510.1f、时长30f、终点540.1f相减可得到29.99997，记录被跳过。子审核反例中 ledger 已有5米 RMSE，而 snapshot 返回 NaN。真实CSV和非整数采样时刻可能触发。

修复方向：冻结记录保存原始请求时长，以该身份匹配；或采用有明确误差界限的比较。增加非整数起点且已有评分的展示回归。

### R4 / P2：Python 真值质量标志被丢弃，插值也未检查两端质量

位置：`PredictionTraining/train_models.py:237`、`PredictionTraining/validate_forecasts.py:319`、`PredictionTraining/validate_forecasts.py:352`。

CSV加载器只额外保留 received_seconds，没有保留 has_position_reference，评分随后默认 True。独立临时CSV反例：50行每行均显式 has_position_reference=False，加载后该列不存在，200秒起点的三个目标全部返回 scored。

即使调用者直接传入质量列，_align_truth 复制 lower 后插值，保留 lower 的标志而忽略 upper。独立反例：目标210秒的精确观测迟到250秒；200秒端点有效，220秒端点 has_position_reference=False。在220秒评分仍返回 scored，水平误差约8.2e-8米，而不是不可评分。Unity侧已检查两端位置参考，因此两端验证行为也不一致。

修复方向：导入保留并严格解析质量/分支元数据；位置插值要求两个端点具备有效位置参考。增加上述CLI导入和插值反例。对于仅位置不可用但深度/姿态有效的观测，目前两端评分器都会跳过全部误差；后续应按指标有效性分开处理，避免位置缺测使压力计/姿态验证也消失。

### R5 / P2：航向残差回退使用线性差值而非环绕角误差

位置：`PredictionTraining/residual_candidate.py:114`。

虽然 train_y/dev_y 被归一到[-180,180)，候选选择仍用 abs(predicted-dev_y)。独立反例：物理航向0°，训练残差+179°、development残差−179°，模型输出+179°。当前报告 development_mae=358°、零残差误差179°，选择 zero_residual；正确的候选环绕角误差只有2°。

修复方向：heading输出使用 abs(wrap(predicted-dev_y)) 计算 development 误差，并采用一致的环绕指标验收；其他输出保留线性 MAE。增加跨±180°回归。残差模型尚未部署，不影响当前部署XGBoost，但交付的训练接口会错误选择候选。

## 独立验证证据

- Python全量独立重跑：45/45，退出码0；仍有既有 datetime.utcnow 弃用警告。
- EditMode独立重跑：520/520，退出码0，22.40秒；`TestResults/Review-EditMode-4ef1c4fe99944459a9f3956986f85ebd.xml`。
- PlayMode独立重跑：18/18，退出码0，7.43秒；`TestResults/Review-PlayMode-04e88da150184dc7a60571251638a23f.xml`。
- 现有新包独立默认仿真烟测：Exit=0、source=simulation；`TestResults/DefaultSimulation-bdd71eb622bd40fb95acf09bee2ff575.log`。没有再次构建覆盖运行包。
- 已核对原构建日志 Build Finished, Result: Success，正常退出；新旧 Runtime.dll SHA-256与原报告一致，分别为 `8B3742A89099B72CF0071235ECFE7D6B415103484F13E9F053F00A00C5E72C5B` 和 `AD725B0771C167AD529618BDFFF7F459BDC383239875C277E0458FEA971088E5`。
- `git diff f5f294b..0b9df2a -- Models` 为空；复审开始时工作树干净。复审只增加本报告、更新本工作区HANDOFF及生成被忽略的测试日志。

测试覆盖补充：`CausalForecastIntegrationTests.cs:69` 的历史回看使用200秒起点，仅21个历史格点，因此 captures的是失败尝试；它验证了失败去重，但不能证明成功预测的历史分支复用。建议用≥290秒的起点并显式断言成功、起点和branchId。

## 继续保留的结论边界

因果时间轴、冻结档案、迟到评分基础及来源记录已经交付；已有测试与运行包验收证据可信。缺独立位置参考/接收时间、部署训练重叠未知、残差未实训、固定10秒格点，以及CFD/视觉未验收是已明确的范围限制，没有把这些限制当作新代码缺陷。

本复审不宣称实测预测精度提升。下一步先修复R1–R5并补反例回归，再重新验收；之后再开展独立数据与实际残差训练。
