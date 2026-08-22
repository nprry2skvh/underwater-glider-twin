# Task 6 实施报告：面板空状态、数值列和长文本

## 范围

仅实现 Task 6：遥测空状态、状态徽标文字、固定数值/单位列、CSV 长路径省略与 Tooltip、状态/错误文本换行，以及对应 PlayMode 内容验收测试。保留 Task 2 的 `ConfigurationSummaryBar` / `ConfigurationExpandedContent` 层级，使用 Task 1/4 的 `UiTextRole` 辅助配置文本；未修改断点、颜色令牌、Tooltip/Focus 行为、PlaybackController 或 Task 7 内容。

## TDD 证据

- RED：`Task6-RED.xml`，PlayMode `15/19` passed，`4` failed，`0` skipped。4 个失败分别对应缺失遥测空状态、单位列、状态徽标和长路径显示，均为预期的功能缺口。
- GREEN：`EditModeResults.xml`，EditMode `431/431` passed，`0` failed，`0` skipped。
- GREEN：`Task6-FinalPlayMode.xml`，PlayMode `19/19` passed，`0` failed，`0` skipped。
- `git diff --check`：通过，无 whitespace error。

## 实现结果

- `DashboardView`：增加 `TelemetryEmptyState`、标题“尚未加载有效轨迹”和下一步提示；无有效帧时显示，有效数据时隐藏；遥测行改为固定标签/数值/单位列，运行时刷新只替换文本。
- `StatusPanelView`：在现有健康区域接入 `UiStateBadge`，保留正常、预测、警告等文字状态；预测指标和告警文本同时保留。
- `DataInputView`：新增 `FormatDisplayPath`；配置摘要中显示省略后的 `CsvPathDisplay`，输入控件保留完整路径并通过 `UiTooltip` 提供完整内容；配置错误状态使用独立可换行文本。
- `PlaybackControlsView`：播放/导出状态文本启用换行并保持独立状态行。
- `UiFactory`：增加固定标签列、固定数值列、固定单位列和可换行状态文本的共享配置，统一应用 `UiTextRole`。
- `EditableUiPlayModeTests`：新增空状态、固定列稳定性、状态文字共存和长路径边界断言。

## 验收确认

- 空状态包含可见标题和下一步提示，不留下无文字深色条。
- 连续数值刷新前后行宽保持稳定，数值列不使用 flexible width，单位有独立列。
- 长 CSV 路径显示省略文本，父级宽度不因文本改变，Tooltip 保留完整路径。
- 错误/状态文本允许换行，不改变配置 summary/expanded 层级。
- 既有配置折叠/展开、CSV、预测和仿真 PlayMode 回归仍通过；既有 EditMode 也全部通过。

## 风险

- 空状态和路径摘要节点在运行时补齐，Prefab 文件本身未新增序列化引用；若后续任务要求编辑器静态 Prefab 引用检查，需要由后续任务单独补充对应资产验证。
- 为兼容既有 `Initialize` 调用方，legacy 初始化路径继续保留带单位的旧文本；Runtime Prefab `Bind` 路径使用独立单位列。两条路径由现有绑定方式区分。
- 本任务未运行 Windows Player 截图和构建；这些属于 Task 7 范围。
