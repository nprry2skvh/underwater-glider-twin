# 预测验证实施与证据（2026-10-08）

## 已实现的边界

- 输入仅由发布时已经收到的历史构造，过去保持不使用 bfill/未来插值。CSV 明确的 `received_seconds` 会保留；接收时间缺失时只声明采样时间回放。
- 请求自行生成 10 秒间隔的目标轴，支持 30/60/300/900 秒。模型不读取未来真值；不支持的界面输入保留原设置并提示。
- Unity 冻结预测身份、物理帧、输入/模型哈希、参数段、海流配置及评分设置。重复 Seek 不覆盖，热更新保留旧分支；关闭预测显示仍对旧记录延迟评分。
- 评分在物理米制坐标完成，不使用显示比例；真值按 0.5 秒容差、20 秒最大插值间隔、60 秒等待期限对齐，缺测/失败不计为零误差。回看历史不显示该时间以后的评分。
- 推理异常/非有限位置输出保留为失败记录。没有概率区域时置信度/90%覆盖率不适用。
- 物理基线 `EventDrivenPhysicsForecaster.Forecast(snapshot, profile, targets)` 复用生产事件驱动 stepper；缺显式 mission/dynamics state/profile 时拒绝。没有把旧历史类比预测改名为纯物理，也没有修改运行模型选择。
- `residual_candidate.py` 接收冻结物理输出和实际观测的配对行，六输出为 ENU 东/北、深度、环绕航向、俯仰、横滚残差。只用 train 拟合，train 冻结限幅，development 选择零残差回退；final_test 标签不进入模型、限幅、回退或拟合摘要。候选输出禁止写入部署 `Models`，已存在候选禁止覆盖。

## 两份数据交付

| 数据 | 加载有效行 | 连续段 | 本次评估 | 预测/评分点 | 解释 |
|---|---:|---:|---|---:|---|
| `2.csv` | 236163 | 1 | exploratory、2 起点 | 12 / 387 | 与未知来源导航记录的一致性 |
| 合成 CSV | 50400 | 20 | 固定12/4/4，final_test 4段×2起点 | 64 / 2064 | 合成功能验证 |

质量报告、固定清单、冻结预测 JSONL、逐点评分 JSONL、尝试 JSONL 与分时长对照均位于：

- [导航数据报告](2026-10-08-prediction-validation/navigation/comparison_report.md)
- [合成数据报告](2026-10-08-prediction-validation/synthetic/comparison_report.md)
- [残差候选状态](2026-10-08-prediction-validation/residual-candidate/candidate.json)

两份 CSV 都没有接收时间、独立位置参考、海流产品发布时间/版本/有效区间。已部署模型训练数据重叠未知，因此事后建立 final_test 清单不是独立验收。连续段没有被冒充航次；小样本结果不用于选定优胜者。旧 artifact 的 `validation_status=accepted` 属于开发验证标识，本次没有改写该 artifact。

物理/残差代码可验证，但这两份 CSV 没有配对的完整 seed/profile 和冻结物理预测，报告分别为 unavailable/not_trained；不声称真实残差模型已训练或水下精度已验收。

## 可复现命令和配对契约

```powershell
py -3.14 -m unittest discover -s PredictionTraining/tests -q
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-playmode.ps1 -All
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-windows.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-player-default-simulation.ps1
py -3.14 PredictionTraining/validate_forecasts.py --csv 'E:\upan\digital twin\2.csv' --artifact Models/XGBoost --output docs/audits/2026-10-08-prediction-validation/navigation --source-kind navigation --max-origins 2 --run-id navigation-20261008
py -3.14 PredictionTraining/validate_forecasts.py --csv 'E:\upan\digital twin\glider_underwater_synthetic.csv' --artifact Models/XGBoost --output docs/audits/2026-10-08-prediction-validation/synthetic --source-kind synthetic --max-origins 2 --run-id synthetic-20261008
py -3.14 PredictionTraining/residual_candidate.py --pairs paired.csv --manifest paired-manifest.json --output CandidateRuns/unique-run --features speed_mps horizon_seconds
```

配对 CSV 必须有 `segment_index,partition,origin_elapsed_seconds,target_elapsed_seconds,horizon_seconds,physics_forecast_id,profile_hash,current_hash,physics_available,current_kind`，以及 `physics_`/`actual_` 前缀的 `east_m,north_m,depth_m,heading_deg,pitch_deg,roll_deg`。`physics_available` 必须为 true，两种 hash 必须是64位十六进制内容哈希，不接受 unavailable/not_provided；`current_kind` 明确配置海流或 declared_zero 等来源。坐标是同一预测起点的物理米制 ENU，不是 Unity 显示坐标。manifest 保存配对 CSV SHA-256、整段边界和三份固定分区；CLI 验证源 hash，历史 290 秒及目标不可跨边界。特征只允许显式起点特征/物理模型输出，不允许实际未来标签。

若 `current_kind=observed_product`，还必须提供 `current_issued_seconds,current_valid_start_seconds,current_valid_end_seconds,current_version`：发布时间不得晚于预测起点，有效区间必须覆盖起点至目标时间，版本不得为空或占位值。缺失海流产品信息不能用配置零流冒充实测产品。

JSONL 使用 UTF-8、严格有限数值；缺失误差为 null。工程默认时间参数随每条预测保存，不视为实测传感器规格。Python 回放的几何/评分使用双精度，部署树推理按 Unity 的 float32 运算；Unity 显示/时间使用 float。这里不声称跨运行时逐位相等。

## 验证记录

- Python：45/45；基线 `datetime.utcnow()` 弃用警告仍存在，未增加新警告。
- 全量 EditMode：520/520。
- 全量 PlayMode：18/18，进程正常退出；本次没有出现既有原生退出挂起。
- Windows 构建成功；无参数默认仿真 Player smoke 通过，Exit=0、source=simulation；最终日志 `TestResults/DefaultSimulation-7348c487286c481599258d64e8784693.log`。这是启动/运行验收，不代替真实位置精度验收。
- 一次独立全分支审核 `f5f294b..145ea7c`（Gauss）提出11项重要问题、无Critical；以下修复经 RED→GREEN 和全量测试验证。没有派第二次审核，不把作者修复验证冒称为再次独立通过。
- 任务6收尾验收在修复提交 `98cb2b7` 上顺序重跑上述五个检查，全部通过；完整任务范围 `293caa3..98cb2b7`，六个计划任务完成。导航与合成报告已按修复代码重生成。

四项防泄漏/一致性门槛有回归覆盖：修改未来或晚到历史不改冻结预测/摘要（Python replay tests）；目标轴由请求确定（因果预测 EditMode tests）；显示缩放不改物理评分、Seek不重复累计（`PhysicalScoresIgnoreDisplayScaleAndRepeatedSeekDoesNotAccumulate`）；热更新保留旧预测并按历史分支回看（`HotUpdatePreservesOldForecastAndSeekUsesHistoricalBranch`）。这不是实际观测真值质量的替代证明。

### 独立审核修复闭环

| 问题 | 修复与回归证据 |
|---|---|
| 晚到数据参与速度清洗 | 先按发布时可见性过滤，再清洗输入；真值准备独立。`test_late_rows_are_removed_before_navigation_cleaning` |
| 全历史重复扫描/超时分配 | 待评分目标按时间索引，观测按采样时间索引；已完成档案退出活动索引，汇总按forecastId索引。90,000个目标档案的重复deadline检查从RED的162ms降为测试预算内（<20ms）。完整档案保留仍占内存，不宣称无界常量空间。 |
| 首次回看无法评分 | 新发布回放预测先与原接收事件缓存对齐，再处理deadline。`FirstBackwardPublicationReconcilesAlreadyReceivedTruth` |
| 回看提前显示迟到评分 | 保存AvailableAtSeconds，回放按评分实际可用时间筛选。`ReplayMetricsWaitForActualScoreAvailabilityNotOnlyTargetTime` |
| 离线后到精确点覆盖先前插值 | 按接收事件顺序冻结第一个可用分数；相同键不替换。`test_first_interpolated_score_is_not_replaced_by_later_exact_observation` |
| 历史输入窗口不一致 | 两端统一30个过去保持的10秒格点；共享24维特征fixture通过，误差容差2e-4。5秒原始采样不再只取145秒。 |
| 树阈值选择不同叶子 | 特征、阈值、叶值、base score和累加采用float32；等值边界两叶100/200反例通过。 |
| 离线绕过artifact验收 | 验证manifest版本/accepted/文件SHA-256及大小/完整输出来源；有效JSON的模型篡改同样拒绝。 |
| 非整除输出间隔漏终点 | 本批仅接受10秒输出间隔；0、NaN、7、40等显式拒绝，不返回截断成功。 |
| 物理接口用默认依赖伪装可用 | Snapshot保留显式profile/dynamics/current来源标志；缺依赖拒绝。明确传入的空配置表示仿真零海流，null表示缺数据。 |
| 残差接受占位来源 | 要求有效hash、physics_available与current_kind，不接受占位值/无物理配对；实测海流产品需发布时间、版本及有效区间，晚于起点发布的产品拒绝。`test_observed_current_product_cannot_be_issued_after_forecast_origin` |
| 海流hash漏resolver设置（原Minor升Important） | preference及IDW radius进入规范hash；两个字段分别变化的回归通过。 |

导航重新回放共16次尝试，其中恒速4次因最后两格点来自同一保持观测而失败，保留在分母中，不生成虚假零误差。因此只有12条冻结成功预测；该变化不被掩盖。此前报告已被本次重生成结果替代。

## 实施裁决与限制

1. 已有审核确认是实施授权，计划是执行分解，不再次暂停索要同一批准；代价是具体分解在交付时审核。
2. 接收时间/独立真值缺失使用显式降级，不制造时间或精度；代价是不能得出运营精度结论。
3. 旧测试要求把全部未来帧交给预测器，与因果规格冲突，改为历史窗口断言；依赖未来输入的第三方预测器不兼容新生产入口。
4. 初查误判合成数据为一段，后按实际清洗确认20段并更正为固定三份清单；仍保留部署训练重叠未知的限制，不将其升级为独立验收。
5. 物理/残差只交付验证接口，不启用新运行模型、不在未配对 CSV 上伪造训练；真实残差性能仍需配对数据。
6. 为维持既有调用，保留两参数 `GetMetrics`，增加按回放时刻过滤的 `GetMetricsThrough`；代价是两个明确聚合入口。
7. resolver hash遗漏影响输入身份，原Minor升级Important并修复；代价是额外hash契约测试，而非忽略该问题。
8. 实测准确率/优胜者、部署模型替换/新模型启用、CFD/视觉验收不在本次评判内；代价是这些事项仍未验证，不声称完成。
9. 独立审核不并发运行Unity；修复后由实施者重跑完整验证，不派重复审核；代价是测试证据来自实施者。
10. 模型历史统一10秒格点，本批输出间隔也只支持10秒；代价是5秒数据同样需要290秒历史，旧1秒Fake测试不再要求原始行数量。
11. 明确给出的空海流配置是仿真零流，缺少来源是unavailable；代价是调用者必须保留显式依赖来源，不能把配置流解释为海流实测产品。

延期Minor：无（唯一Minor已按实际影响升级并修复）。

仅在隔离分支提交，不合并/推送；部署 Models 与动力学默认值保持不变。上一运行包已保存在 `Builds/UnderwaterGliderTwin.pre-prediction-20261008`，新包发布在 `Builds/UnderwaterGliderTwin`。

保存版本的 `UnderwaterGliderTwin.Runtime.dll` SHA-256 为 `AD725B0771C167AD529618BDFFF7F459BDC383239875C277E0458FEA971088E5`，最终新包为 `8B3742A89099B72CF0071235ECFE7D6B415103484F13E9F053F00A00C5E72C5B`。可用该 DLL 确认运行时版本，不能用相同的 Unity launcher exe hash 判定功能未更新。
