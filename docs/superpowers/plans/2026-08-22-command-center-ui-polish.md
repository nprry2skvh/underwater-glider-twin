# Command Center UI Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不破坏现有 RuntimeUiRoot、Prefab 实例和业务绑定的前提下，完成深海任务指挥舱 UI 的视觉精修、响应式修正、配置区重构、焦点反馈和四分辨率验收。

**Architecture:** 保留现有单 Canvas、单 `ModalRoot` 和现有业务 View 绑定。将配置区拆为常驻 `ConfigurationSummaryBar` 与可折叠 `ConfigurationExpandedContent`，折叠时保持 `DataInputView` 根对象激活；使用屏幕宽高和带滞后的布局策略控制完整三栏、压缩三栏和抽屉模式；用轻量 `UiTooltip`、`UiTooltipController`、`UiStateBadge` 和 `UiFocusVisual` 补充交互反馈，所有装饰层不拦截射线。

**Tech Stack:** Unity 2022.3.62f3c1、uGUI、CanvasScaler、RectTransform、LayoutGroup、LayoutElement、ScrollRect、EventSystem、NUnit EditMode/PlayMode tests、Windows Player screenshot capture。

**Spec:** `docs/superpowers/specs/2026-08-22-command-center-ui-polish-design.md`

## Global Constraints

- 完整三栏：`width >= 1616 && height >= 656`；压缩三栏：`width >= 1280 && height >= 624 && !Full`；抽屉：`width < 1280 || height < 624`。
- 上一次为 Drawer 时，只有 `width >= 1296 && height >= 656` 才退出；上一次为 Compressed 时，只有 `width < 1264 || height < 620` 才进入抽屉；上一次为 Full 时，`width < 1584 || height < 640` 退出完整模式。
- `1700×640` 必须进入压缩三栏，`1280×623` 必须进入抽屉，`1296×656` 必须允许从抽屉退出到压缩三栏。
- `DataInputPanel` 根对象和 `DataInputView` 组件始终保持激活；折叠只切换 `ConfigurationExpandedContent`。
- 抽屉入口只能位于 `UiRoot/DrawerEntryLayer`，不得放入将被隐藏的左右栏。
- `ModalRoot` 是唯一抽屉/浮层容器；不得创建第二个 Canvas、第二个 ModalRoot 或名为 DrawerLayer 的场景对象。
- Tooltip Popup、焦点描边、状态徽标装饰 Image 的 `raycastTarget = false`；Tooltip CanvasGroup 的 `blocksRaycasts = false`。
- Tooltip 和焦点视觉不能改变 RectTransform 尺寸、布局尺寸、可点击区域或兄弟节点排序。
- 实际字号按 `logicalSize * CanvasScaler.scaleFactor` 验收；1024×640 高频标签和按钮实际目标至少 11–12px。
- Prefab 已存在时只补齐缺失结构；不得整体覆盖用户颜色、位置、字体、事件和实例覆盖。
- 不得静默删除同名非 Prefab 对象；发现冲突必须报告或跳过。
- 根 UI 不使用 ScrollRect；每个配置、遥测、状态或单个弹窗区域最多一个纵向 ScrollRect。
- 修改不涉及仿真、海流采样、预测、回放、相机业务逻辑。
- 每个任务单独测试并提交，提交不得包含工作区已有的非 UI 改动：`.superpowers/sdd/paper-ppt-task-3-report.md`、`UnderwaterGliderTwin/Assets/Scenes/Welcome.unity`、`UnderwaterGliderTwin/ProjectSettings/ProjectSettings.asset`。

---

## 文件与职责地图

### 新建文件

- `UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltip.cs`：单个控件的 Tooltip 文案、触发目标和宿主引用。
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltipController.cs`：`ModalRoot/TooltipPopup` 下唯一 Tooltip 浮层，处理延迟、定位、隐藏和边界限制。
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiStateBadge.cs`：状态文字、图标/几何标记、色条和状态类型。
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiFocusVisual.cs`：Selectable 的选中/取消选中视觉反馈，不改变布局尺寸。
- 必要的 EditMode/PlayMode 测试文件，优先扩展现有 UI 测试文件，只有测试职责明显独立时才新建。

### 修改文件

- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutPolicy.cs`：完整的高度/宽度分支和滞后算法。
- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs`：文字角色、实际字号和高频/低频显示规则。
- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs`：模式刷新、抽屉遮罩、焦点进入/回退、动画和入口层。
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`：视觉令牌、控件状态、摘要栏和装饰射线规则。
- `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs`、`DataInputView.Layout.cs`：摘要栏/展开内容和不停用业务根对象的折叠流程。
- `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`、`StatusPanelView.cs`：空状态、固定数值列、徽标和长文本。
- `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`、`PlaybackControlsView.cs`：唯一相机/播放入口和布局层级。
- `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`、`RuntimeUiReferences.cs`、`TwinBootstrap.cs`：Prefab/fallback 统一引用。
- `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`、`UnderwaterGliderTwin/Assets/Scenes/Main.unity`：幂等场景结构和不覆盖保护。
- 相关 UI Prefab：只补齐缺失节点和状态样式，不整体重置。

---

### Task 1: 固化断点、字号和可读性测试基线

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutPolicy.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiLayoutPolicyTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiTypographyTests.cs`
- Test: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`

**Interfaces:**
- `ResponsiveUiLayoutPolicy.Resolve(float width, float height, RuntimeUiLayoutMode previousMode) -> RuntimeUiLayoutMode`
- `ResponsiveUiLayoutPolicy.GetEffectiveCanvasScale(float width, float height, Vector2 referenceResolution, float match) -> float`
- `ResponsiveUiTypography.GetActualPixelSize(int logicalSize, float effectiveScale) -> float`
- `ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode mode, float width, float height) -> ResponsiveUiTypographyProfile`
- `ResponsiveUiTypographyProfile` 至少提供 `sectionTitleSize`、`labelSize`、`valueSize`、`buttonSize`、`auxiliarySize`、`minimumReadablePixelSize`、`showLowPriorityText`。

- [ ] **Step 1: 写失败的边界测试。**

在 `ResponsiveUiLayoutPolicyTests.cs` 增加以下断言：

```csharp
[TestCase(1920f, 1080f, RuntimeUiLayoutMode.FullThreeColumn)]
[TestCase(1700f, 640f, RuntimeUiLayoutMode.CompressedThreeColumn)]
[TestCase(1616f, 656f, RuntimeUiLayoutMode.FullThreeColumn)]
[TestCase(1615f, 655f, RuntimeUiLayoutMode.CompressedThreeColumn)]
[TestCase(1280f, 720f, RuntimeUiLayoutMode.CompressedThreeColumn)]
[TestCase(1280f, 623f, RuntimeUiLayoutMode.Drawer)]
[TestCase(1279f, 720f, RuntimeUiLayoutMode.Drawer)]
[TestCase(1024f, 640f, RuntimeUiLayoutMode.Drawer)]
public void Resolve_HandlesEveryDimensionBand(float width, float height, RuntimeUiLayoutMode expected)
{
    Assert.That(ResponsiveUiLayoutPolicy.Resolve(width, height, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(expected));
}
```

增加滞后断言：`Drawer + 1296×656 -> Compressed`、`Compressed + 1263×620 -> Drawer`、`Full + 1700×640 -> Compressed`。

- [ ] **Step 2: 运行失败测试。**

Run: `E:\upan\digital twin\scripts\test-editmode.cmd`

Expected: 新增高度边界或滞后断言至少有一项失败，证明测试不是只覆盖旧规则。

- [ ] **Step 3: 实现完整分支。**

按 spec 的默认规则和 previous-mode 滞后规则实现，不读取 `ViewportColumn.rect.width`，不在策略类中创建 Unity 对象。

- [ ] **Step 4: 增加实际字号测试。**

使用参考分辨率 `1920×1080`、`matchWidthOrHeight = 0.5f` 验证：`1024×640` 抽屉模式的高频标签逻辑字号经过有效缩放后 >= 11px；Prefab/fallback 的文字角色使用同一 profile。

- [ ] **Step 5: 运行测试并提交。**

Run: `E:\upan\digital twin\scripts\test-editmode.cmd`

Expected: 所有 EditMode 测试通过。

Commit:

```bash
git add UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutPolicy.cs UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiLayoutPolicyTests.cs UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiTypographyTests.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs
git commit -m "fix: define command center responsive breakpoints"
```

### Task 2: 重构配置摘要态并保持业务根对象激活

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/DataInputPanel.prefab`

**Interfaces:**
- `DataInputView.SetConfigurationExpanded(bool expanded)`：只切换 `ConfigurationExpandedContent` 的可见性/尺寸，不停用 `DataInputPanel`。
- `DataInputView.ToggleAdvancedConfiguration()`：重复调用幂等，不创建新内容，不增加按钮监听。
- `DataInputView.ConfigurationExpandedForTests { get; }`：仅用于 PlayMode 验证摘要/展开状态。

- [ ] **Step 1: 写配置折叠失败测试。**

加载 Main 场景，取得 `DataInputView`、`DataInputPanel` 和展开按钮，重复执行折叠/展开四次，断言：

```csharp
Assert.That(dataInputPanel.activeSelf, Is.True);
Assert.That(dataInputView.enabled, Is.True);
Assert.That(dataInputView.ConfigurationExpandedForTests, Is.False);
```

在折叠前后调用 CSV、预测配置和仿真入口的测试回调，确认绑定对象仍存在。

- [ ] **Step 2: 运行失败测试。**

Run: Unity Test Runner PlayMode，执行 `ResponsiveUiLayoutPlayModeTests` 和 `EditableUiPlayModeTests`。

Expected: 当前不存在摘要/展开状态契约，新增断言失败。

- [ ] **Step 3: 创建稳定层级。**

将 `DataInputPanel` 子级整理为：

```text
DataInputPanel
├── ConfigurationSummaryBar
└── ConfigurationExpandedContent
    └── ConfigurationScrollViewport
```

`DataInputView` 仍挂在 `DataInputPanel`，折叠时只操作 `ConfigurationExpandedContent` 的 `CanvasGroup`、尺寸和 `ScrollRect` 内容。

- [ ] **Step 4: 移除旧控件残留。**

把旧输入控件移动到唯一的展开内容根；折叠状态不得保留旧标题、背景条或透明但仍占位的 LayoutElement。不要调用 `DataInputPanel.SetActive(false)`，不要在折叠时解绑业务事件。

- [ ] **Step 5: 保护重复绑定。**

在 `ConfigureResponsiveBottomDrawer` 和 `ToggleBottomDrawer` 中使用现有节点复用逻辑；按钮 listener 绑定前移除同一个 handler，重复执行不会增加 listener 数量。

- [ ] **Step 6: 运行 PlayMode 并提交。**

Run: Unity Test Runner，执行 `ResponsiveUiLayoutPlayModeTests`、`EditableUiPlayModeTests`。

Expected: 配置折叠/展开多次后根对象仍激活、业务入口仍可调用、没有重复监听。

Commit:

```bash
git add UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs UnderwaterGliderTwin/Assets/UI/Prefabs/DataInputPanel.prefab
git commit -m "refactor: separate configuration summary from expanded content"
```

### Task 3: 修正三栏布局、常驻入口和 Prefab/fallback 结构

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Modify: `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scenes/Main.unity`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/GeneratedRuntimeUiPlayModeTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs`

**Interfaces:**
- `RuntimeUiRoot.DrawerLayer` 只返回现有 `ModalRoot`，不创建新对象。
- `RuntimeUiReferences.layout` 提供 `systemBar`、`configurationArea`、`mainBody`、三栏、`playbackBar`、`drawerEntryLayer`、`drawerScrim` 和两个入口按钮。
- `ResponsiveUiLayoutController.RefreshForScreen(float width, float height)` 只读取传入尺寸并复用已有对象。

- [ ] **Step 1: 写结构和 Prefab 保护测试。**

测试必须断言：

```csharp
Assert.That(Find("UiRoot/DrawerEntryLayer/TelemetryDrawerToggle"), Is.Not.Null);
Assert.That(Find("UiRoot/DrawerEntryLayer/StatusDrawerToggle"), Is.Not.Null);
Assert.That(runtimeRoot.DrawerLayer, Is.SameAs(runtimeRoot.ModalRoot));
Assert.That(FindObjectsOfType<Canvas>(true).Length, Is.EqualTo(1));
Assert.That(FindObjectsNamed("ModalRoot").Count, Is.EqualTo(1));
```

Prefab 保护测试先对一个现有 Prefab/场景实例设置特殊颜色、位置和字体值，运行 Builder 后逐项断言值未被覆盖；同名非 Prefab 对象必须被报告或跳过。

- [ ] **Step 2: 运行失败测试。**

Run: `E:\upan\digital twin\scripts\test-editmode.cmd`；随后运行相关 PlayMode 测试。

Expected: 新增布局引用或保护断言至少失败一项。

- [ ] **Step 3: 调整压缩三栏。**

完整模式使用左 280px/中央 flexible/右 320px；压缩模式将左右栏最小宽度分别设置为 236px 和 260px，中央列 `minWidth = 640`，间距 12px；1280×720 不得依赖溢出维持布局。

- [ ] **Step 4: 固化入口和 ModalRoot。**

确保 fallback 和 Prefab 路径都生成/绑定 `DrawerEntryLayer`；入口不放入 `TelemetryColumn` 或 `StatusColumn`；`ModalRoot` 保持唯一，Tooltip 后续也只作为其子级。

- [ ] **Step 5: 让刷新幂等。**

重复调用相同尺寸 `RefreshForScreen` 不创建入口、遮罩、CanvasGroup 或抽屉对象，不重复绑定点击事件。窗口尺寸变化由现有 `Update` 或等价尺寸通知触发，但断点决策只执行一次。

- [ ] **Step 6: 写场景并运行回归。**

运行 Builder 两次，比较第二次前后的层级和对象计数；运行 EditMode 和 PlayMode。

Commit:

```bash
git add UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs UnderwaterGliderTwin/Assets/Scenes/Main.unity UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/GeneratedRuntimeUiPlayModeTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs
git commit -m "fix: align command center compressed layout and root structure"
```

### Task 4: 统一颜色、字体、顶栏、中央工具条和播放栏

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCommandToolbar.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/PlaybackControlsPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`

**Interfaces:**
- `UiFactory.ApplyRuntimePalette(Transform root)` 保留现有调用契约，并统一面板、卡片、按钮、输入框、选中态和禁用态颜色。
- 文字角色使用 `Title`、`SectionTitle`、`Label`、`Value`、`Button`、`Auxiliary`、`Error`，Prefab/fallback 使用同一映射。

- [ ] **Step 1: 写视觉断言。**

断言普通面板使用深海蓝，主强调色接近 `#5DD7E8`，主按钮文字使用深色，禁用态对比度低于普通态但仍可辨认，视图区覆盖层 alpha 低于普通面板且 `raycastTarget = false`。

- [ ] **Step 2: 运行失败测试。**

Run: `E:\upan\digital twin\scripts\test-editmode.cmd`

Expected: 至少有一项新增颜色/角色断言失败。

- [ ] **Step 3: 实现颜色和状态。**

使用 spec 颜色令牌，按钮提供 normal/highlighted/pressed/selected/disabled 状态；选中态增加青色边线或轻量填充，不使用大面积发光。

- [ ] **Step 4: 重排顶栏和中央工具条。**

顶栏显示产品名、任务/运行模式、系统状态和退出；中央工具条只保留唯一相机入口，当前视角有明确选中态；播放栏不再显示重复相机按钮。

- [ ] **Step 5: 重排播放栏。**

使用三段结构：播放操作、时间/状态、速度/图例；保留现有事件绑定和业务回调，不修改 PlaybackController 行为。

- [ ] **Step 6: 运行 EditMode/PlayMode 并提交。**

检查四种尺寸的临时截图，确认无文字为空的深色按钮条和无意义大面积空白。

Commit:

```bash
git add UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCommandToolbar.prefab UnderwaterGliderTwin/Assets/UI/Prefabs/PlaybackControlsPanel.prefab UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs
git commit -m "feat: polish command center visual hierarchy"
```

### Task 5: 增加 Tooltip、状态徽标和可见焦点

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltip.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltipController.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/UiStateBadge.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFocusVisual.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/GeneratedRuntimeUiPlayModeTests.cs`

**Interfaces:**
- `UiTooltip` 提供序列化 `message` 和宿主 `Selectable`；
- `UiTooltipController.Show(UiTooltip source)`、`Hide()`、`RefreshPosition()`；
- `UiStateBadge.SetState(UiStateKind state, string label)`；
- `UiFocusVisual` 实现 `ISelectHandler`/`IDeselectHandler`，只切换 Outline/装饰视觉；
- `ResponsiveUiLayoutController` 保持 `OpenSideDrawer`、`CloseSideDrawer`、`RefreshForScreen` 公开契约。

- [ ] **Step 1: 写射线和焦点失败测试。**

断言 Tooltip Popup 的 Image `raycastTarget == false`、CanvasGroup `blocksRaycasts == false`；焦点描边和状态徽标装饰 Image 不拦截射线且不改变 RectTransform 尺寸。

- [ ] **Step 2: 写焦点场景测试。**

分别设置当前焦点为空、中央视图、播放栏、抽屉内部，打开 Telemetry/Status 抽屉，断言焦点进入抽屉首个 Selectable；关闭后焦点返回对应入口；遮罩打开时背景不接收点击，关闭后恢复。

- [ ] **Step 3: 运行失败测试。**

Run: Unity Test Runner PlayMode，执行 `ResponsiveUiLayoutPlayModeTests` 和 `GeneratedRuntimeUiPlayModeTests`。

Expected: 新增组件和焦点行为尚未实现，测试失败。

- [ ] **Step 4: 实现 Tooltip。**

`UiTooltipController` 复用 `ModalRoot/TooltipPopup`，不创建 Canvas；Popup Image 和 CanvasGroup 不拦截射线；显示延迟约 350ms，离开/焦点变化/关闭抽屉/Escape 时隐藏，位置限制在 Canvas 内。

- [ ] **Step 5: 实现状态徽标和焦点。**

状态徽标显示文字 + 图标/几何标记 + 色条；焦点使用 Outline 或同层绘制，不改变布局；Button/InputField/Toggle 设置明确 Navigation 顺序。

- [ ] **Step 6: 修复抽屉焦点 guard 和动画起始态。**

移除会阻止外部焦点进入抽屉的错误早退；打开动画从 alpha 0、不可交互状态开始，完成后再启用交互；无动画和有动画最终状态一致。

- [ ] **Step 7: 运行测试并提交。**

Commit:

```bash
git add UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltip.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltipController.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiStateBadge.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiFocusVisual.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs UnderwaterGliderTwin/Assets/Tests/EditMode/UiTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/GeneratedRuntimeUiPlayModeTests.cs
git commit -m "feat: add command center focus tooltip and state feedback"
```

### Task 6: 完善面板空状态、数值列和长文本

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/DashboardPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/StatusPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/DataInputPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`

**Interfaces:**
- 现有数据绑定和刷新方法签名不变；只新增显示辅助方法，例如 `SetTelemetryEmptyState(bool visible)`、`SetStatusBadge(UiStateKind state, string label)` 和 `FormatDisplayPath(string path, int maxCharacters)`。

- [ ] **Step 1: 写内容验收测试。**

断言无有效轨迹时显示“尚未加载有效轨迹”和下一步提示；长 CSV 路径不会改变父级宽度；告警和预测状态同时有文字；数值行宽度在连续刷新前后不变。

- [ ] **Step 2: 实现遥测和状态空态。**

在 `DashboardView` 增加空状态卡片；在 `StatusPanelView` 使用 `UiStateBadge` 显示正常、待机、预测、警告和错误；保留现有数据源和业务判断。

- [ ] **Step 3: 固定数值列和单位。**

行使用固定标签列、固定数值列和独立单位列；实时刷新只替换文本；数值使用统一小数位、时间格式和单位。

- [ ] **Step 4: 处理长路径和错误文本。**

路径文本显示省略版本，Tooltip 显示完整内容；错误信息允许换行并使用独立状态行，不挤压按钮或输入框。

- [ ] **Step 5: 运行测试并提交。**

Commit:

```bash
git add UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/UI/Prefabs/DashboardPanel.prefab UnderwaterGliderTwin/Assets/UI/Prefabs/StatusPanel.prefab UnderwaterGliderTwin/Assets/UI/Prefabs/DataInputPanel.prefab UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs
git commit -m "feat: improve command center empty states and data density"
```

### Task 7: 四分辨率 Windows Player 验收和完整回归

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs`
- Create: `TestResults/ui-polish-1920x1080.png`
- Create: `TestResults/ui-polish-1366x768.png`
- Create: `TestResults/ui-polish-1280x720.png`
- Create: `TestResults/ui-polish-1024x640.png`

- [ ] **Step 1: 增加边界和回归测试。**

覆盖 `1700×640`、`1280×623`、`1296×656`、`1615×655`；重复折叠/展开配置区；重复刷新相同尺寸；检查 Tooltip、焦点、遮罩和射线。

- [ ] **Step 2: 运行 EditMode。**

Run: `E:\upan\digital twin\scripts\test-editmode.cmd`

Expected: 全部 EditMode 测试通过，生成 XML 结果供记录。

- [ ] **Step 3: 运行 PlayMode。**

在 Unity Test Runner 执行现有 PlayMode 全集，至少包括 `EditableUiPlayModeTests`、`ResponsiveUiLayoutPlayModeTests`、`GeneratedRuntimeUiPlayModeTests`。

- [ ] **Step 4: 生成 Windows Player 截图。**

使用实际 Player 分别生成：

```text
TestResults/ui-polish-1920x1080.png
TestResults/ui-polish-1366x768.png
TestResults/ui-polish-1280x720.png
TestResults/ui-polish-1024x640.png
```

截图必须检查配置摘要态、配置展开态、抽屉入口、中央视图、顶栏和播放栏；不得只依赖 BatchMode 的 `Screen.SetResolution`。

- [ ] **Step 5: 运行 Windows 构建。**

Run: `E:\upan\digital twin\scripts\build-windows.cmd`

Expected: `TestResults/WindowsBuild.log` 包含 `Build Finished, Result: Success`。

- [ ] **Step 6: 进行最终视觉审查。**

逐张确认：无旧配置残留、无无文字占位条、顶栏可见、1280 不挤压、1024 抽屉入口常驻、文字实际可读、焦点可见、Tooltip 不拦截、中央视图区仍为最大视觉区域。

- [ ] **Step 7: 汇总证据并提交。**

只提交测试文件和四张截图，不提交 `Library`、临时日志或工作区已有非 UI 改动。

```bash
git add UnderwaterGliderTwin/Assets/Tests TestResults/ui-polish-*.png
git commit -m "test: verify command center UI polish"
```

## 完成标准

- 所有七个任务均有独立提交和测试证据；
- 1920×1080 使用完整三栏，1366×768 使用稳定压缩三栏，1280×720 不违背栏宽契约，1024×640 使用抽屉模式；
- `DataInputPanel` 和 `DataInputView` 在配置折叠时仍激活，CSV/预测/仿真回归通过；
- 只有一个 Canvas、一个 EventSystem、一个 ModalRoot；抽屉入口在 DrawerEntryLayer；
- Tooltip、焦点描边、状态徽标装饰不拦截射线；
- 抽屉焦点进入/回退、Escape、遮罩和无动画最终态测试通过；
- Prefab 特殊颜色、位置、字体和实例覆盖在 Builder 后保持不变；
- EditMode、PlayMode、四分辨率 Windows Player 截图和 Windows 构建全部有可复核证据。
