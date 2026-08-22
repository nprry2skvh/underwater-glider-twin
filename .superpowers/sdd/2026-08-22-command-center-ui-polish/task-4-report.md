# Task 4 实施报告：统一颜色、字体、顶栏、中央工具条和播放栏

## 范围

- 统一深海页面/面板/卡片/控件/输入框/分隔线/文字/状态色 token；主强调色为 `#5DD7E8`。
- 增加 `Title`、`SectionTitle`、`Label`、`Value`、`Button`、`Auxiliary`、`Error` 文字角色映射，并让 Prefab/fallback 共用角色解析。
- 运行时调色只对默认白色/默认状态应用，保留已有 Prefab 非默认颜色、位置、字体和实例覆盖。
- 顶栏补齐产品名、运行模式、系统状态和退出信息；不新增 Canvas、EventSystem、ModalRoot。
- 中央海流工具条为相机入口的唯一活动承载，当前视角使用青色边线选中态。
- 播放栏改为 `PlaybackOperationsRow`、`PlaybackTimelineRow`、`PlaybackOptionsRow` 三段结构；保留已有绑定和回放事件回调，未修改 `PlaybackController`。
- 播放栏 Prefab 中重复相机按钮默认隐藏，Prefab 与 fallback 使用相同状态色 token。

## TDD 证据

### CHANGES_REQUESTED 修复 RED

先只加入审查回归断言，未改生产代码。

EditMode 命令：

```text
cmd /c scripts\test-editmode.cmd
```

精确结果：命令以退出码 `1` 结束；`TestResults/EditModeResults.xml` 为 `total=428 passed=425 failed=3 skipped=0`。失败断言为：

- `OceanCommandToolbarView_BindMarksExistingGlobalCameraMode`
- `OceanCommandToolbarView_ResetMarksGlobalCameraMode`
- `UiFactory_ApplyRuntimePalettePreservesCustomButtonStateColors`

随后首次全量 PlayMode 命令也保留了真实 RED：

```text
C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe -batchmode -nographics -projectPath E:\upan\digital twin\.worktrees\responsive-command-center-ui\UnderwaterGliderTwin -runTests -testPlatform PlayMode -testResults E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\Task4ReviewPlayModeResults.xml -logFile E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\Task4ReviewPlayMode.log
```

精确结果：`total=12 passed=11 failed=1 skipped=0`；失败为 `MainScene_CommandToolbarSelectionFollowsGlobalInitialization`，证明外部 bootstrap 在 Bind 后切换到 Global 时 UI 未跟随。

### GREEN

EditMode 命令：

```text
cmd /c scripts\test-editmode.cmd
```

精确结果：`EditMode tests passed: 428/428`，结果文件为：

```text
E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\EditModeResults.xml
```

全量 PlayMode 命令：

```text
C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe -batchmode -nographics -projectPath E:\upan\digital twin\.worktrees\responsive-command-center-ui\UnderwaterGliderTwin -runTests -testPlatform PlayMode -testResults E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\Task4ReviewPlayModeGreenResults.xml -logFile E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\Task4ReviewPlayModeGreen.log
```

精确结果：`result=Passed total=12 passed=12 failed=0 skipped=0`，不是旧的 7 项结果；结果文件为：

```text
E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\Task4ReviewPlayModeGreenResults.xml
```

最终检查：

```text
git diff --check
```

结果：无 whitespace 错误。

## 视觉检查说明

已对两个 Task 4 Prefab 做静态 YAML 检查：面板/控件状态色已切换到批准 token，工具条覆盖层 alpha 为 `0.2` 且 `raycastTarget = false`，播放栏重复相机按钮为 inactive；运行时 EditMode/PlayMode 断言覆盖了顶栏文本、三段播放层级、活动相机入口、初始化/外部模式切换/reset 后的选中边线、禁用态、完整按钮状态色保护和覆盖层射线行为。未新增 Canvas、EventSystem 或 ModalRoot。

本 Task 未生成四分辨率 Windows Player 截图；实际 Player 截图与构建验收留给计划中的 Task 7，因此剩余风险是不同 CanvasScaler/字体环境下的最终像素级可读性仍需 Player 证据确认。

## 未解决风险

- 未执行 Windows Player 四分辨率截图/构建验收；不影响本 Task 的 EditMode/PlayMode 结果，但需要 Task 7 完成最终视觉确认。
- 顶栏“退出”在本 Task 作为系统栏视觉信息保留；现有业务退出回调仍由播放栏原入口承载，未改变业务行为。
