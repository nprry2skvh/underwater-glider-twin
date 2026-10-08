# 预测算法回放对照

- 真值等级：`navigation_record_unverified`
- 解释边界：`consistency_with_navigation_record_only`
- 独立最终测试：`False`
- 连续段数：1；已验证航次数：未知

| 方法 | 状态/尝试 | 已评分 | 失败 | 未评分 | 水平 MAE(m) | 三维 RMSE(m) |
|---|---:|---:|---:|---:|---:|---:|
| constant_velocity | 8 | 4 | 4 | 0 | 138.158 | 286.110 |
| current_deployed_xgboost_with_hold_fallback | 8 | 7 | 0 | 1 | 3998.708 | 6013.844 |
| event_driven_physics_with_current | unavailable | — | — | — | — | — |
| physics_plus_residual_candidate | not_trained | — | — | — | — | — |

90% 概率区域覆盖率：不适用；当前方法没有输出并经独立数据校准的概率区域。

限制：
- receive timestamps absent; replay is ordered by sample time only
- ocean-current product issue/version/valid-time metadata absent
- fewer than three independent continuous segments; no final-test claim
- position provenance is unverified; errors mean consistency with navigation record only
- 已部署模型的训练数据重叠未知；后建清单不能证明独立最终测试。
- 本次分区：exploratory；实际评估连续段：1。
- 上表为各次预测均权汇总；这是小样本功能验证，不用于选定算法优胜者。

| 方法 | 时长(s) | 已评分点 | 水平 MAE(m) | 终点水平 MAE(m) |
|---|---:|---:|---:|---:|
| constant_velocity | 30 | 3 | 19.931 | 18.880 |
| constant_velocity | 60 | 6 | 29.503 | 47.683 |
| constant_velocity | 300 | 22 | 116.472 | — |
| constant_velocity | 900 | 78 | 386.724 | 711.240 |
| current_deployed_xgboost_with_hold_fallback | 30 | 3 | 37.002 | 46.772 |
| current_deployed_xgboost_with_hold_fallback | 60 | 7 | 1350.575 | 56.437 |
| current_deployed_xgboost_with_hold_fallback | 300 | 23 | 492.778 | — |
| current_deployed_xgboost_with_hold_fallback | 900 | 79 | 375.403 | 443.712 |
