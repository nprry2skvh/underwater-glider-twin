# Task 6 实施报告：面板空状态、数值列和长文本

## 范围

仅实现 Task 6：遥测空状态、状态徽标文字、固定数值/单位列、CSV 长路径省略与 Tooltip、状态/错误文本换行，以及对应 PlayMode 内容验收测试。保留 Task 2 的 `ConfigurationSummaryBar` / `ConfigurationExpandedContent` 层级，使用 Task 1/4 的 `UiTextRole` 辅助配置文本；未修改断点、颜色令牌、Tooltip/Focus 行为、PlaybackController 或 Task 7 内容。

## TDD 证据

- 复审 RED：`EditModeResults.xml`，总计 `434`，通过 `431`，失败 `3`，跳过 `0`。失败断言分别锁定 StatusPanel PredictionTime 单位分离、Dashboard fallback 数值/单位分离和长错误状态布局高度。
- GREEN：`EditModeResults.xml`，EditMode 总计 `434`，通过 `434`，失败 `0`，跳过 `0`。
- GREEN：`Task6-ReviewFinalPlayMode.xml`，PlayMode 总计 `19`，通过 `19`，失败 `0`，跳过 `0`。覆盖 Main prefab 的独立单位列、空列表显示后有效帧自动隐藏、固定列宽、长 CSV 路径和预测耗时单位。
- `git diff --check`：通过，无 whitespace error。

## 实现结果

- `DashboardView`：增加 `TelemetryEmptyState`、标题“尚未加载有效轨迹”和下一步提示；空帧列表显示，有效帧列表自动隐藏，不再使用会阻止后续隐藏的 forced 标志；Initialize fallback 与 Bind 均使用固定标签/数值/单位列，值文本不再拼接单位。
- `StatusPanelView`：在现有健康区域接入 `UiStateBadge`，保留正常、预测、警告等文字状态；PredictionTime、距离、电量、误差、置信度等指标的值文本与单位列分离，PredictionTime 使用 `ms`。
- `DataInputView`：新增 `FormatDisplayPath`；配置摘要中显示省略后的 `CsvPathDisplay`，输入控件保留完整路径并通过 `UiTooltip` 提供完整内容；配置错误状态使用独立可换行文本和 44 高度行，避免覆盖相邻控件。
- `PlaybackControlsView`：播放/导出状态文本启用换行并保持独立状态行。
- `UiFactory`：增加固定标签列、固定数值列、固定单位列和可换行状态文本的共享配置，统一应用 `UiTextRole`。
- `EditableUiPlayModeTests`：新增空状态、固定列稳定性、状态文字共存和长路径边界断言。

## 验收确认

- 空状态在真实的空帧条件下包含可见标题和下一步提示，不留下无文字深色条；随后注入有效帧列表会自动隐藏。
- 连续数值刷新前后行宽保持稳定，数值列不使用 flexible width，单位有独立列。
- 长 CSV 路径显示省略文本，父级宽度不因文本改变，Tooltip 保留完整路径。
- 错误/状态文本允许换行并有独立的 44 高度行，不改变配置 summary/expanded 层级。
- 既有配置折叠/展开、CSV、预测和仿真 PlayMode 回归仍通过；既有 EditMode 也全部通过。

## 风险

- 空状态和路径摘要节点在运行时补齐，Prefab 文件本身未新增序列化引用；若后续任务要求编辑器静态 Prefab 引用检查，需要由后续任务单独补充对应资产验证。
- 空状态测试使用 `RefreshTelemetryStateForTests(IReadOnlyList<TelemetryFrame>)` seam，因为 `PlaybackModel` 不接受零帧构造；该 seam 只按传入帧列表计算状态，不持久化强制显示标志。
- 本任务未运行 Windows Player 截图和构建；这些属于 Task 7 范围。
