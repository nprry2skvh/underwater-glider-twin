# P2修复新上下文审核（修复前裁决）

审核人Averroes，范围 `0b9df2a..fd52c1a`，一次只读审核；此记录不是后续修复的再次审核。
核对评审包与5个提交及git diff一致。R1–R3/R5定向证据成立；false/0、插值双端有效性、限幅后航向2°/线性358、final_test隔离探针通过。读取控制器Edit525/Play21及构建证据；未独立运行Unity/构建/Player，也没有独立全量Python验收。运行限制由控制器为避免并发Unity设置，不是用户撤销验收授权。

## Findings

无Critical，无独立Minor；结论 With fixes。

1. Important F1：`validate_forecasts.py` CLI将预测branch硬编码segment-index，导入branch-a/simulation_branch的130行反例129目标全部missing；应从发布时可见起点取branch，segment_index独立保存，增加CLI同/异分支回归。
2. Important F2：显式simulation_branch缺整列branch_id时，loader接受且scorer绕过身份验证，三目标误scored；必须拒绝或不可评分，并覆盖整列缺失。
3. Important F3：评分默认仿真等级与逐行导航等级做OR，将明确navigation_reference观测当仿真限制branch；默认值只能用于没有逐行等级的输入，明确导航跨branch仍可评分。

控制器核对调用链后维持三个Important等级，进入一次TDD修复，不派重复审核。修复结果见 `2026-10-08-prediction-p2-repair.md`。

## Declined to judge（逐项保留）

- 旧broad feature完整正确性，本轮只审P2及受影响调用链。
- 实际运行精度/提升，缺独立位置、接收时间及训练重叠证据。
- 残差部署/收益，Models未替换，训练探针不能证明收益。
- 无位置时分指标保留深度/姿态，计划排除该重构。
- 无质量列的旧输入行为，已保留未验证来源导航一致性回放。
- 旧Publish无原始时长的精确身份，兼容限制已接受，生产PublishRequest。
- 旧Expire推进已注册域，兼容保留；本轮仅查新域及命名入口隔离。
- 多参数段导入初始化、重复采样修订、任意CSV布局，未改动旧功能。
- 航向回归器跨角度拟合，R5只修评价指标。
- 新增步长/时长及CFD/视觉，不在范围。
- 独立Unity/构建/Player与完整包验收，控制器禁止审核人重复运行；仅读取现有证据。
