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
| `2.csv` | 236163 | 1 | exploratory、2 起点 | 16 / 516 | 与未知来源导航记录的一致性 |
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
py -3.14 PredictionTraining/validate_forecasts.py --csv 'E:\upan\digital twin\2.csv' --artifact Models/XGBoost --output docs/audits/2026-10-08-prediction-validation/navigation --source-kind navigation --max-origins 2 --run-id navigation-20261008
py -3.14 PredictionTraining/validate_forecasts.py --csv 'E:\upan\digital twin\glider_underwater_synthetic.csv' --artifact Models/XGBoost --output docs/audits/2026-10-08-prediction-validation/synthetic --source-kind synthetic --max-origins 2 --run-id synthetic-20261008
py -3.14 PredictionTraining/residual_candidate.py --pairs paired.csv --manifest paired-manifest.json --output CandidateRuns/unique-run --features speed_mps horizon_seconds
```

配对 CSV 必须有 `segment_index,partition,origin_elapsed_seconds,target_elapsed_seconds,horizon_seconds,physics_forecast_id,profile_hash,current_hash`，以及 `physics_`/`actual_` 前缀的 `east_m,north_m,depth_m,heading_deg,pitch_deg,roll_deg`。坐标是同一预测起点的物理米制 ENU，不是 Unity 显示坐标。manifest 保存配对 CSV SHA-256、整段边界和三份固定分区；CLI 验证源 hash，历史 290 秒及目标不可跨边界。特征只允许显式起点特征/物理模型输出，不允许实际未来标签。

JSONL 使用 UTF-8、严格有限数值；缺失误差为 null。工程默认时间参数随每条预测保存，不视为实测传感器规格。Python 回放使用双精度数值，Unity 显示/时间使用 float；这里不声称跨运行时逐位相等。

## 验证记录

- Python：35/35；基线 `datetime.utcnow()` 弃用警告仍存在，未增加新警告。
- 全量 EditMode：512/512。
- 全量 PlayMode：待最终运行结果写入。
- Windows 构建、无参数默认仿真 smoke：待最终运行结果写入。
- 全分支新上下文审核：待完成。

## 实施裁决与限制

1. 已有审核确认是实施授权，计划是执行分解，不再次暂停索要同一批准；代价是具体分解在交付时审核。
2. 接收时间/独立真值缺失使用显式降级，不制造时间或精度；代价是不能得出运营精度结论。
3. 旧测试要求把全部未来帧交给预测器，与因果规格冲突，改为历史窗口断言；依赖未来输入的第三方预测器不兼容新生产入口。
4. 初查误判合成数据为一段，后按实际清洗确认20段并更正为固定三份清单；仍保留部署训练重叠未知的限制，不将其升级为独立验收。
5. 物理/残差只交付验证接口，不启用新运行模型、不在未配对 CSV 上伪造训练；真实残差性能仍需配对数据。
6. 为维持既有调用，保留两参数 `GetMetrics`，增加按回放时刻过滤的 `GetMetricsThrough`；代价是两个明确聚合入口。

仅在隔离分支提交，不合并/推送；部署 Models 与动力学默认值保持不变。上一运行包应保存在 `Builds/UnderwaterGliderTwin.pre-prediction-20261008`，新包发布在 `Builds/UnderwaterGliderTwin`。
