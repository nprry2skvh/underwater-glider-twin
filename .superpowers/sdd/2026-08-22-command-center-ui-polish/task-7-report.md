# Task 7 验收报告

日期：2026-08-23
工作区：`E:\upan\digital twin\.worktrees\responsive-command-center-ui`

## 原始 RED

Task 7 的四张真实 Windows Player 截图均复现以下问题：

- `SystemBar` 顶栏的产品名、系统状态、运行模式/任务名和退出文本互相重叠；
- Dashboard、Status、DataInput 和 Ocean toolbar 中仍有 prefab legacy `Text`/`Image`/`InputField` 直接子节点未移除或未重排，形成无文字深色横条；
- PlaybackControls 的旧 `StatusText` 未进入时间线行，按钮文本没有被恢复到可读的布局高度。

## 根因证据

运行时 Hierarchy 追踪确认 Player 使用 `RuntimeUiRoot -> RuntimeCanvas -> UiRoot -> SystemBar`，`SystemBar` 本身是顶部全宽 RectTransform（`anchorMin=(0,1)`、`anchorMax=(1,1)`、高度 48）。`CommandCenterHeader` 是其直接子节点，四个 Header 文本也是 Header 的直接子节点；Main 场景序列化的 `SystemBar` 没有预先序列化这些子节点，Header 由 `UiFactory.EnsureCommandCenterHeader` 从 existing/fallback 路径复用或创建。

SystemBar 诊断断言 `MainScene_SystemBarHeaderUsesNonOverlappingAbsoluteSlots` 先在原始右侧槽位上失败：旧的右锚定 `RectTransform` 区间出现 `CommandCenterSystemHealth/Runtime/Exit` 重叠，失败证据为 `Expected: less than 591.384399f, But was: 807.384338f`。同一断言确认 Header 没有启用 `HorizontalLayoutGroup`，因此本问题不是 LayoutGroup 正常排版造成的，而是 legacy Header 的右锚定槽位偏移/尺寸组合错误；若用户覆盖带有启用的 legacy `HorizontalLayoutGroup`，断言也会明确失败，避免其重写固定槽位。

其余 Hierarchy assertions 还证明：

- Dashboard/Status 的绑定行曾由 `EnsureRow` 全部落在同一 `anchoredPosition`；
- prefab 的旧输入控件名称与 `DataInputView.Layout` 查找名称不一致，导致控件留在 collapsed panel 之外；
- Playback prefab 使用 `StatusText`，而布局查找 `PlaybackStatus`；
- Ocean toolbar 的旧命令按钮没有进入水平命令行；
- Dashboard advanced unit、Status `AlarmValue` 和 DataInputPanel legacy `Image` 在错误状态下仍 active/visible。

## 修复

修复保持在 runtime binding/layout 层，没有修改 prefab、`PlaybackController` 业务逻辑或断点算法：

- `UiFactory.cs`：统一 existing/fallback Header；产品名固定为左锚定，右侧三个文本使用不重叠的右锚定槽位（健康 `-234/120`、运行模式 `-80/142`、退出 `-16/52`），并保留 Header `HorizontalLayoutGroup` 防回归断言契约。
- `DashboardView.cs`、`StatusPanelView.cs`：绑定行使用实际 prefab 文本或创建缺失 label，按行索引放置；advanced unit 随详情状态隐藏；旧 Alarm 文本/背景隐藏。
- `DataInputView.cs`、`DataInputView.Layout.cs`：增加 prefab legacy child alias 映射，并关闭 collapsed 状态下 legacy panel background。
- `OceanCommandToolbarView.cs`：将标题、计数和六个命令重排到 `OceanToolbarCommandsRow`，恢复按钮文本布局。
- `PlaybackControlsView.cs`：将 prefab `StatusText` 绑定为 `PlaybackStatus` 并移动到时间线行，恢复控制文本和行高。
- `EditableUiPlayModeTests.cs`：新增 Hierarchy/RectTransform assertions，覆盖行重叠、legacy graphics、Playback status 和 SystemBar 槽位。

Prefab user override protection 保持成立：没有写回 prefab；existing prefab/scene 节点只在 runtime 被绑定、重排或隐藏。

## CHANGES_REQUESTED 修复：字体覆盖保护

审查发现 `OceanCommandToolbarView` 和 `PlaybackControlsView` 曾用
`Mathf.Max(existingText.fontSize, 14)` 抬高已有 prefab/scene `Text` 的字号，破坏用户实例覆盖。
先加入回归断言并在旧实现上运行：已有字号 `7` 被改成 `14`，EditMode 为 `434/435`，测试按预期失败。

最小修复为删除已有控件的运行时 `fontSize` 写入；`UiFactory` 的 wrapped status 路径也改为只应用颜色角色。fallback 新建控件仍在创建时使用默认字号，因此缺失控件仍有可读默认值。回归测试为
`BoundPrefabTypography_PreservesExistingTextFontSizes`，覆盖已有按钮文本、Playback status 文本的特殊小字号，绑定和布局后保持不变。

## 验证结果

- EditMode：`435/435` passed，`TestResults/EditModeResults.xml`；字体保护回归覆盖通过。
- 完整 PlayMode：`25/25` passed，`TestResults/Task7PlayModeFull.xml`。
- System/UI 诊断 PlayMode：`16/16` passed，`TestResults/Task7Diagnostics.xml`；其中 SystemBar 失败断言在临时恢复 RED 槽位时确实失败，恢复修复后通过。
- Windows Player build：删除临时截图 harness 后重新构建成功，`TestResults/WindowsBuild.log` 含 `Build Finished, Result: Success.`。
- 真实 Windows Player 截图均重新生成并用 `view_image` 检查：
  - `TestResults/ui-polish-1920x1080.png`（1920×1080）
  - `TestResults/ui-polish-1366x768.png`（1366×768）
  - `TestResults/ui-polish-1280x720.png`（1280×720）
  - `TestResults/ui-polish-1024x640.png`（1024×640）

四个尺寸中 SystemBar 均按“产品名 | 系统正常 | 海流任务指挥舱 | 退出”分离显示，无文字重叠、无越界；主体旧空条已消失，播放栏文本可读。

配置摘要态之外，使用真实 Windows Player 的 Unity EventSystem pointer click 触发展开，运行时断言为 `CanvasGroup.alpha=1`、展开面板高度 `320`，并生成、用 `view_image` 检查了：

- `TestResults/ui-polish-expanded-1280x720.png`（1280×720，展开态；真实 Player）

该环境在 1920 窗口下受桌面裁剪影响产生了无效黑帧，因此没有把它当作验收截图；没有使用 batchmode 截图冒充 Player，也没有提交临时 harness、日志或无效帧。

本次提交不包含 `Welcome.unity`、`ProjectSettings.asset`、`paper-ppt` 或无关 `.meta` 的用户既有改动。
