# 预测验证复审P2修复与验收证据

依据：用户确认独立复审R1–R5，随后“开始”授权执行。隔离分支 `codex/digital-twin-stage01`，基线 `0b9df2a`；不合并/推送，不替换Models或动力学默认值。

## 修复与回归

| 项目 | 修复 | RED→GREEN证据 |
|---|---|---|
| R1 尾部热更新 | ProfileSequence身份区分新段和同参数补片，更新branch/current版本；旧预测保留 | `TailProfileUpdateCreatesNewBranchAndCurrentVersion`失败后通过，旧800秒目标不接受新seq1观测；`SameProfileAppendKeepsExistingBranch`保持通过 |
| R2 新分支提前过期 | 仿真receipt/deadline按branch隔离，缓存真实观测仍可对齐；命名ExpireBranch入口 | `OldSimulationClockCannotExpireNewBranchForecast`前/后事件两例、`BranchDeadlineDoesNotExpireAnotherBranch`、`LegacyExpiryDoesNotSeedTheClockOfALaterBranch`失败后通过；同域回看和真实缓存对齐仍通过 |
| R3 UI隐藏评分 | 冻结原始RequestedHorizonSeconds，生产PublishRequest不用终点时间差匹配；旧Publish签名兼容 | 起点510.1、30秒反例由ledger RMSE11.7076/UI NaN变为相等；历史Seek改300秒且明确预测成功及ID/branch复用 |
| R4 真值质量丢失 | 导入保留并严格解析质量/branch/grade，不数值转换或ffill元数据；双端位置参考/有限位置/同branch同grade；按每行仿真来源过滤 | 全false CSV由scored变no_position_reference；False字符串/0、非法/缺失值、双端任一无参考等待有效精确观测、跨grade/foreign simulation branch均有反例回归 |
| R5 角残差回退 | 限幅后仅heading用wrap差值选择候选，记录指标；其他输出保持线性 | 真实XGBoost训练+179/dev−179：heading MAE358→2且candidate；east仍358且zero_residual。final_test扰动隔离回归继续通过 |

实现提交：R1/R2 `2fcd640`，R3 `cf4d13c`，R4 `bdf57f9`，R5 `fd52c1a`。
在修复提交fd52c1a上，五项顺序验收已通过：Python54/54、EditMode525/525、PlayMode21/21、Windows构建和默认Player smoke，Exit=0/source=simulation；日志 `TestResults/DefaultSimulation-265020dabb40402a95baba102ba764f4.log`。既有utcnow弃用警告未在本轮顺带修改。

## 新上下文审核与一次修复

Averroes只读审核发现3项Important、无Critical/Minor；原始裁决见 `2026-10-08-prediction-p2-final-review.md`。控制器核对调用链后维持等级，追加一次修复，不再次派审核。提交 `8dbf130`：

| 发现 | RED→GREEN及全量Python |
|---|---|
| F1 CLI用segment编号而非真实branch | `test_cli_freezes_visible_simulation_branch_and_rejects_foreign_truth`两例失败后通过，55/55；真实CLI冻结可见branch-a、保留segment_index=0，129同分支目标scored、换未来branch-b则129目标missing |
| F2 显式仿真无branch仍评分 | `test_csv_explicit_simulation_grade_requires_branch_column`、`test_direct_simulation_truth_without_branch_identity_cannot_score`失败后通过，57/57；导入和直接评分入口均拒绝缺身份的显式仿真数据 |
| F3 默认仿真等级覆盖明确导航 | `test_explicit_navigation_truth_can_score_foreign_branch_under_simulation_default`失败后通过，58/58；navigation_reference在两种默认等级下均可跨branch评分，明确仿真仍隔离 |

8dbf130之后在eed3fef上重新执行五项验收：Python58/58、EditMode525/525、PlayMode21/21，Windows构建成功，默认Player烟测Exit=0/source=simulation、PNG存在。日志 `TestResults/DefaultSimulation-8a58ee4471cd43e08647cb563888c6f4.log`；Edit时间12:48:57Z–12:49:21Z，Play12:49:31Z–12:49:39Z；构建日志 `TestResults/WindowsBuild.log`明确Result: Success。

最终裁决：本轮R1–R5及追加F1–F3的功能修复验收通过，计划五任务完成。所有新缺陷都有先失败后通过证据，一次集中修复后的全量套件全绿；旧报告保留历史范围。裁决由实现者依据回归作出，不宣称审核人再次批准或独立运行验收。提交后保留隔离分支/工作树，不自动合并或推送，不代表独立实测精度验收。

## 重新生成的交付

原导航/合成报告目录已按修复源码重生成，JSONL中代码hash随新实现更新；导航16次尝试/12条成功冻结预测/387评分点（四次恒速失败仍在分母），合成64条预测/2064点。两份CSV没有新增质量或接收元数据，结果数值不变；不以数值不变掩盖质量契约的修复，也不把未知导航来源或合成数据当独立精度验收。

复现五项验收：

```powershell
py -3.14 -m unittest discover -s PredictionTraining/tests -q
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-editmode.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-playmode.ps1 -All
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-windows.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-player-default-simulation.ps1
```

## 实施裁决与边界

1. 用户已批准具体修复顺序并要求开始，执行分解不再次索取批准；代价是分解在交付时审核。
2. 保留既有Publish/Expire反射调用签名，新增命名入口而非重载；代价是维护显式兼容入口。无分支Expire仅显式推进已经注册的域，不能给后来分支继承时钟。
3. 未知/非有限请求时长记null，生产保存原始有限请求；代价是旧调用方若无请求信息，不能恢复精确的非整数时长身份。
4. 有显式质量列时缺失/非法值拒绝；无质量列保持未知来源导航一致性；代价是非法元数据CSV不再静默导入。未扩展为按压力计/姿态分别评分的重构。
5. 显式仿真等级缺branch在导入和评分均拒绝，不提供隐式绑定；代价是原先接受的无身份显式仿真CSV须补元数据。

审核人逐项未裁决内容，由控制器承担以下裁决（无Minor延期）：

6. 旧完整功能不作整体验收，仅覆盖P2及受影响路径；代价是未触及功能仍可能有缺陷。
7. 实际精度/提升不验收，无独立位置/接收时间/训练重叠证据；代价是不能承诺精度或算法优胜。
8. 残差部署收益不验收、Models不替换；代价是现有部署回退继续保留。
9. 无位置时深度/姿态独立评分不扩展；代价是可用分指标暂不单独保留。
10. 无质量列旧输入仍仅导航一致性；代价是分数不能证明绝对精度。
11. 旧Publish无原始时长保持身份限制；代价是旧直接调用须补元数据。
12. 旧Expire仍推进已注册域，命名入口才分域；代价是通用调用会影响所有已有分支。
13. 多参数段导入初始化、重复采样修订、任意CSV布局不额外验收；代价是使用前须单独回归审核。
14. 不重构航向跨角拟合，仅修评价；代价是分布环绕建模限制仍可能存在。
15. 不增加步长/时长、不验收CFD/视觉；代价是支持及QA只限原契约。
16. 运行验收使用控制器全量证据，审核人只读避免并发；代价是没有第二执行者的独立运行结论。审核运行限制是控制器安排，不是用户禁止验收。

本计划忽略目录清理曾被环境拦截，保留scratch，不绕过删除限制；不影响代码/证据提交，不当作实现阻塞。

独立实测精度、真实残差训练和部署模型替换仍未完成；成功功能回归不能替代独立位置真值。原复审报告保留为历史发现证据，后续裁决以本轮修复与验收记录为准。
本轮修复前包保留 `Builds/UnderwaterGliderTwin.pre-p2-20261008`，Runtime.dll SHA-256 `8B3742A89099B72CF0071235ECFE7D6B415103484F13E9F053F00A00C5E72C5B`；原更早包 `Builds/UnderwaterGliderTwin.pre-prediction-20261008`亦保留。
eed3fef重新构建的新包 Runtime.dll SHA-256 `52AB98AE15F37216C9FDA177ACEC3144CCA19DDC1792E2A415ECFB63CDFF13F1`；与fd52c1a相同，因为追加修复仅修改Python而非Unity实现。运行包目录 `Builds/UnderwaterGliderTwin`。两份JSONL均核对当前validate_forecasts.py代码hash；导航12/387、合成64/2064，两次CLI退出码0。`git diff 0b9df2a -- Models`为空，场景没有本轮遗留改动。
