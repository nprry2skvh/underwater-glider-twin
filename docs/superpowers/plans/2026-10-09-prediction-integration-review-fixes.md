# 预测集成审核修复计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复集成审核发现的详情显隐及诊断舵角回归，验证后完成已授权的合并推送。

**Architecture:** 保留两条UI路径和原布局；绑定模式仅登记已有详情文本/单位及详情根，不重建prefab。坐标有效性只控制向量，舵角仅依赖诊断有效性。

**Tech Stack:** Unity C#、NUnit、PowerShell、Python 3.14。

**Spec:** `docs/audits/2026-10-08-prediction-p2-integration.md`（用户授权及Pasteur集成发现）。

## Global Constraints

- 不切主目录HEAD、不stash/reset、不强推、不替换Models；保留旧版本与主目录脏改动。
- 不重审原P2，不重复派审核；当前一次审核的修复由RED→GREEN及全量验收裁决。
- 不把功能测试通过等同于实测精度提升。
- 用户已授权直接修复并正常合并推送，无新增外部动作，不重复请求批准。

## Review Focus

- 完整绑定prefab和无详情根的布局，已有详情值都能初始隐藏、展开、再隐藏。
- 最小绑定不含航向/姿态，详情根仍按按钮切换，不因提前返回失去初始化。
- 已有独立单位及标签，与数值同时切换，不隐藏核心深度/电量。
- 缺坐标但诊断有效：舵角(4,6,8)仍为左右翼10/2、尾舵8度，三向量隐藏。
- 缺诊断时舵角回中；内嵌/独立单位断言均不得接受缺单位的裸数值。

### Task 1: 集成审核一次修复与发布门槛

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Visualization/GliderVisualController.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/VisualizationTests.cs`
- Verify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/MotionDataConsistencyPlayModeTests.cs`

**Interfaces:**
- Consumes: `DashboardView.Bind(DashboardPanelRefs, PlaybackController, PredictionController)`、`Button.onClick`、`GliderVisualController.ApplySample(ContinuousMotionSample)`。
- Produces: 接口不变；绑定详情与生成详情一致，舵角/坐标质量解耦。

- [x] Step 1: 写真实Dashboard prefab绑定回归（有/无advancedRowsRoot）及最小绑定回归，断言初始隐藏→按钮展开→再隐藏；核心深度保持启用，独立单位/标签随详情切换。
- [x] Step 2: 运行 `scripts/test-editmode.ps1`，预期新增绑定显隐断言失败（不是编译错误）；保存XML和日志。
- [x] Step 3: 在Bind登记详情值、标签、单位与可选根；初始化调用RefreshDetails，保留布局及最小绑定；重跑EditMode，预期全绿。
- [x] Step 4: 写缺坐标但非零诊断舵角的真实播放回归，断言10/2/8度且三向量隐藏；运行EditMode，预期舵角0而失败。
- [x] Step 5: 仅移除缺坐标分支重复清零；保留无诊断复位，重跑EditMode全绿。
- [x] Step 6: 收紧已有AssertReadout：独立节点存在则数值和单位均精确检查；否则必须有内嵌单位。不改变运动阈值或生产接口。
- [x] Step 7: 运行 `TestResults/validate-integration.ps1`，预期Python58、EditMode新增回归全通过、Play21、Windows重建和默认Player smoke通过。保存新DLL hash。
- [ ] Step 8: 登记两P2修复、单位补强、审核未裁决范围及保护摘要；正常提交merge和推送 `HEAD:refs/heads/feature/underwater-glider-csv-twin`，核对远端，保留主目录和旧包。
