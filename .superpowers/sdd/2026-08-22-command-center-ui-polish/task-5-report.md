# Task 5 报告：Tooltip、状态徽标和可见焦点

## RED

- EditMode（新增断言首次运行）：430 total，428 passed，2 failed。失败为 `Task5_FeedbackComponents_ExposeAccessibleContracts` 和 `Task5_FeedbackDecorations_DoNotInterceptRaycastsOrResizeHost`，原因是 Task 5 组件/TooltipPopup 尚不存在。
- ResponsiveUiLayoutPlayModeTests 焦点 RED：1 total，0 passed，1 failed。ModalRoot 外部抽屉焦点未进入目标抽屉首个 Selectable，失败断言为预期 `StatusField` 未得到。
- GeneratedRuntimeUiPlayModeTests RED：2 total，1 passed，1 failed。`GeneratedRuntimeUi_CreatesCanonicalHierarchy_AndSameSizeRefreshDoesNotDuplicateNodes` 失败，`TooltipPopup` 尚未生成。

## GREEN

- EditMode：430 total，430 passed，0 failed。结果文件：`TestResults/EditModeResults.xml`。
- ResponsiveUiLayoutPlayModeTests：4 total，4 passed，0 failed。覆盖当前焦点为空、中央视图、播放栏、ModalRoot 抽屉内部、打开后进入目标抽屉、关闭回退入口和遮罩 raycast 状态。结果文件：`TestResults/Task5GreenResponsiveResults.xml`。
- GeneratedRuntimeUiPlayModeTests：2 total，2 passed，0 failed。覆盖单 Canvas、唯一 ModalRoot、TooltipPopup 非拦截和重复刷新不复制节点。结果文件：`TestResults/Task5GreenGenerated3Results.xml`。
- `git diff --check`：通过；仅有 Git 对既有 LF/CRLF 工作区文件的换行提示。

## 实现摘要

- 新增 `UiTooltip`、`UiTooltipController`、`UiStateBadge`、`UiFocusVisual`。
- TooltipPopup 复用唯一 `ModalRoot`，Image 与 CanvasGroup 均不拦截射线；仅显示时刷新位置，支持延迟、边界限制、焦点/离开/Escape 隐藏。
- 状态徽标同时提供文字、标记图形和色条；焦点使用同层 Outline/非射线装饰，不改变宿主 RectTransform 尺寸。
- 抽屉焦点从空焦点、中央视图、播放栏和外部抽屉内容均可进入；关闭后回到对应入口；不同 RuntimeUiRoot 不互相抢焦点；动画从 alpha 0/不可交互起始。
- `RuntimeUiReferences`、`TwinBootstrap` 和 `ResponsiveUiLayoutController` 均复用现有 UI Root/Canvas/ModalRoot，没有创建第二个 Canvas、EventSystem 或 ModalRoot。

## 范围与风险

- 本次只改 Task 5 brief 列出的生产文件、测试文件、新增组件 `.meta` 和本报告；未修改 Welcome.unity、ProjectSettings.asset、paper-ppt 文件或 Task 6/7 内容。
- 风险：Tooltip 仍使用 uGUI Legacy `Text`，复杂字体/本地化长文本的最终视觉验收留给后续 Player 截图任务；本任务未执行 Task 7 Windows Player 构建。
