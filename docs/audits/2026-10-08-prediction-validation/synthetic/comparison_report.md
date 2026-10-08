# 预测算法回放对照

- 真值等级：`synthetic_simulation`
- 解释边界：`synthetic_functional_validation_only`
- 独立最终测试：`False`
- 连续段数：20；已验证航次数：未知

| 方法 | 状态/尝试 | 已评分 | 失败 | 未评分 | 水平 MAE(m) | 三维 RMSE(m) |
|---|---:|---:|---:|---:|---:|---:|
| constant_velocity | 32 | 32 | 0 | 0 | 4.699 | 13.737 |
| current_deployed_xgboost_with_hold_fallback | 32 | 32 | 0 | 0 | 12.864 | 22.655 |
| event_driven_physics_with_current | unavailable | — | — | — | — | — |
| physics_plus_residual_candidate | not_trained | — | — | — | — | — |

90% 概率区域覆盖率：不适用；当前方法没有输出并经独立数据校准的概率区域。

限制：
- receive timestamps absent; replay is ordered by sample time only
- ocean-current product issue/version/valid-time metadata absent
- 已部署模型的训练数据重叠未知；后建清单不能证明独立最终测试。
- 本次分区：final_test；实际评估连续段：4。
- 上表为各次预测均权汇总；这是小样本功能验证，不用于选定算法优胜者。

| 方法 | 时长(s) | 已评分点 | 水平 MAE(m) | 终点水平 MAE(m) |
|---|---:|---:|---:|---:|
| constant_velocity | 30 | 24 | 0.368 | 0.554 |
| constant_velocity | 60 | 48 | 0.572 | 0.981 |
| constant_velocity | 300 | 240 | 2.820 | 6.347 |
| constant_velocity | 900 | 720 | 15.035 | 39.423 |
| current_deployed_xgboost_with_hold_fallback | 30 | 24 | 7.429 | 11.091 |
| current_deployed_xgboost_with_hold_fallback | 60 | 48 | 7.616 | 6.316 |
| current_deployed_xgboost_with_hold_fallback | 300 | 240 | 8.804 | 14.491 |
| current_deployed_xgboost_with_hold_fallback | 900 | 720 | 27.605 | 77.868 |
