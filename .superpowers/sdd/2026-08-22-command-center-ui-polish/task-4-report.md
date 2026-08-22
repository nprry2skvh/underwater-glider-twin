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

### RED

命令：

```text
E:\upan\digital twin\.worktrees\responsive-command-center-ui\scripts\test-editmode.cmd
```

真实结果：`425` 个 EditMode 用例中 `422` 通过、`3` 失败。失败断言为：

- `UiFactory_UsesApprovedCommandCenterPaletteAndButtonStates`：旧面板色值不是 `#0B2430`。
- `PlaybackControlsView_UsesThreeVisualSegmentsWithoutCameraDuplicates`：三段播放行尚未存在。
- `OceanCommandToolbarView_MarksCurrentCameraWithAccentEdge`：点击视角后仍为普通边线色。

### GREEN

命令：

```text
E:\upan\digital twin\.worktrees\responsive-command-center-ui\scripts\test-editmode.cmd
```

精确结果：`EditMode tests passed: 425/425`，结果文件为：

```text
E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\EditModeResults.xml
```

命令：

```text
C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe -batchmode -nographics -projectPath E:\upan\digital twin\.worktrees\responsive-command-center-ui\UnderwaterGliderTwin -runTests -testPlatform PlayMode -testResults E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\PlayModeResults.xml -logFile E:\upan\digital twin\.worktrees\responsive-command-center-ui\TestResults\PlayMode.log -quit
```

精确结果：`RESULT=Passed TOTAL=7 PASSED=7 FAILED=0`。

最终检查：

```text
git diff --check
```

结果：无 whitespace 错误。

## 视觉检查说明

已对两个 Task 4 Prefab 做静态 YAML 检查：面板/控件状态色已切换到批准 token，工具条覆盖层 alpha 为 `0.2` 且 `raycastTarget = false`，播放栏重复相机按钮为 inactive；运行时 EditMode/PlayMode 断言覆盖了顶栏文本、三段播放层级、活动相机入口、选中边线、禁用态和覆盖层射线行为。

本 Task 未生成四分辨率 Windows Player 截图；实际 Player 截图与构建验收留给计划中的 Task 7，因此剩余风险是不同 CanvasScaler/字体环境下的最终像素级可读性仍需 Player 证据确认。

## 未解决风险

- 未执行 Windows Player 四分辨率截图/构建验收；不影响本 Task 的 EditMode/PlayMode 结果，但需要 Task 7 完成最终视觉确认。
- 顶栏“退出”在本 Task 作为系统栏视觉信息保留；现有业务退出回调仍由播放栏原入口承载，未改变业务行为。
