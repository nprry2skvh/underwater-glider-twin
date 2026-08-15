# Responsive Command Center UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 UnderwaterGliderTwin 的可编辑场景 UI 和运行时生成 UI 统一为支持 1920×1080、1366×768、1280×720 与抽屉模式的深海指挥台布局，消除大面积留白、遮挡和重复控制入口。

**Architecture:** 保留现有 `RuntimeUiRoot` 和 `ModalRoot` 作为唯一的长期 UI 根与抽屉/弹窗容器。新增纯布局策略 `ResponsiveUiLayoutPolicy`、统一布局控制器 `ResponsiveUiLayoutController` 和字体配置 `ResponsiveUiTypography`；可编辑场景由 `EditableUiSceneBuilder` 写入相同层级，运行时生成路径由 `UiFactory` 创建相同层级。业务视图只负责绑定和刷新数据，不再自行计算绝对坐标。

**Tech Stack:** Unity 2022.3.62f3c1、uGUI、CanvasScaler、RectTransform、HorizontalLayoutGroup、VerticalLayoutGroup、LayoutElement、ScrollRect、NUnit EditMode/PlayMode tests、现有 Windows build 与 screenshot capture 工具。

## Global Constraints

- 参考分辨率为 `1920×1080`；Canvas 使用 `Scale With Screen Size`，Match 为 `0.5`。
- 大于等于 1600px 宽使用完整三栏；1280–1599px 使用压缩三栏；小于 1280px 或低于安全高度使用抽屉模式。
- `1024×640` 不承诺完整三栏，只承诺抽屉模式与中央 3D 视图可用。
- 响应式断点只读取 Canvas/屏幕可用宽高，不以布局后的 `ViewportColumn.rect.width` 作为唯一判断条件。
- 断点使用进入/退出滞后阈值，避免布局切换震荡。
- `ModalRoot` 是唯一抽屉/弹窗容器；不创建第二个名为 `DrawerLayer` 的根对象。
- 现有 `OceanCurrentDrawer`、`FlightLegDrawer` 保持在 `ModalRoot` 下并保持对象名称。
- 不修改仿真、海流采样、预测模型、回放模型和相机业务逻辑。
- 所有滚动限定在配置高级参数、遥测内容、状态内容或单个抽屉内部，根 UI 不使用 `ScrollRect`。
- 标签和辅助文字必须通过字体配置与截图验证保持实际可读性；抽屉模式下只显示高频内容。
- 面板使用深海蓝，青色只用于强调，黄色只用于警告/预测历史，状态不能只依靠颜色区分。
- 每个任务完成后运行该任务列出的测试并单独提交，避免与工作区已有改动混合。

---

## 文件与职责地图

### 新建文件

- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutPolicy.cs`：只根据窗口宽高和上一次模式计算 `FullThreeColumn`、`CompressedThreeColumn` 或 `Drawer`，包含断点滞后阈值。
- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs`：根据布局模式、窗口尺寸和内容优先级生成可读的字体配置，并将配置应用到 UI 文本。
- `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs`：管理区域布局、模式切换、侧栏抽屉、遮罩、Escape 关闭与焦点回退；不处理业务数据。
- `UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiLayoutPolicyTests.cs`：覆盖断点、滞后和高度边界。
- `UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiTypographyTests.cs`：覆盖字体最小逻辑字号和抽屉模式高频内容规则。
- `UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs`：加载 Main 场景，验证模式切换、唯一 ModalRoot、抽屉父级、焦点回退和无重复控制入口。

### 修改文件

- `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`：保留 `ModalRoot` 序列化字段，增加 `DrawerLayer` 语义别名和布局引用验证。
- `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`：增加 `ResponsiveLayoutRefs`，描述系统栏、配置区、MainBody 三列、播放栏、抽屉遮罩和抽屉入口引用。
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`：把运行时生成 UI 改为统一层级、布局组件、颜色和字体配置。
- `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`：把高频参数与高级参数分离，限制滚动边界，复用现有配置抽屉，不创建第二套配置弹窗。
- `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`：将遥测行和高级行改为布局组驱动，减少无效留白并支持内部滚动。
- `UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs`：将状态摘要、预测指标和工程校核改为布局组驱动，处理长文本和状态样式。
- `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`：作为唯一的中央视图相机工具条，保留跟随、全局、俯视、侧视、环绕和复位。
- `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`：隐藏旧的重复相机按钮，保留播放、时间轴、速度、图层和退出功能。
- `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`：在可编辑 Prefab 路径和 `UiFactory` 生成路径中初始化同一布局控制器，并在 UI 引用校验后再绑定视图。
- `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`：生成/修复统一层级、布局组件、ModalRoot 内容和序列化引用，确保重复运行幂等。
- `UnderwaterGliderTwin/Assets/UI/Prefabs/DashboardPanel.prefab`：遥测卡片的深色视觉、紧凑间距、滚动内容根和可读字体。
- `UnderwaterGliderTwin/Assets/UI/Prefabs/StatusPanel.prefab`：状态卡片、预测指标和工程校核的布局与状态色。
- `UnderwaterGliderTwin/Assets/UI/Prefabs/DataInputPanel.prefab`：两行高频配置区、内部高级参数滚动区和统一输入框样式。
- `UnderwaterGliderTwin/Assets/UI/Prefabs/PlaybackControlsPanel.prefab`：唯一播放栏、时间轴、速度和图层控件。
- `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCommandToolbar.prefab`：中央视图内的唯一相机工具条。
- `UnderwaterGliderTwin/Assets/UI/Prefabs/FlightLegDrawer.prefab`：保持 ModalRoot 归属，应用抽屉宽度、遮罩和焦点规则。
- `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCurrentDrawer.prefab`：保持 ModalRoot 归属，应用抽屉宽度、内部滚动和状态色。
- `UnderwaterGliderTwin/Assets/Scenes/Main.unity`：写入最终的可编辑场景层级和布局组件序列化配置。
- `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`：增加统一 ModalRoot、布局引用和无重复根对象回归测试。
- `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`：增加抽屉焦点、遮罩和唯一相机控制入口回归测试。

---

## Task 1: 建立响应式断点策略与字体可读性基线

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutPolicy.cs`
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs`
- Create: `UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiLayoutPolicyTests.cs`
- Create: `UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiTypographyTests.cs`

**Interfaces:**
- `ResponsiveUiLayoutPolicy.Resolve(float width, float height, RuntimeUiLayoutMode previousMode) -> RuntimeUiLayoutMode`
- `ResponsiveUiLayoutPolicy.GetEffectiveCanvasScale(float width, float height, Vector2 referenceResolution, float match) -> float`
- `ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode mode, float width, float height) -> ResponsiveUiTypographyProfile`
- `ResponsiveUiTypographyProfile` 至少包含 `sectionTitleSize`、`labelSize`、`valueSize`、`buttonSize`、`auxiliarySize`、`showLowPriorityText`。

- [ ] **Step 1: 写断点策略失败测试。**

```csharp
[TestCase(1920f, 1080f, RuntimeUiLayoutMode.FullThreeColumn)]
[TestCase(1600f, 900f, RuntimeUiLayoutMode.FullThreeColumn)]
[TestCase(1366f, 768f, RuntimeUiLayoutMode.CompressedThreeColumn)]
[TestCase(1280f, 720f, RuntimeUiLayoutMode.CompressedThreeColumn)]
[TestCase(1279f, 720f, RuntimeUiLayoutMode.Drawer)]
[TestCase(1024f, 640f, RuntimeUiLayoutMode.Drawer)]
[TestCase(1000f, 620f, RuntimeUiLayoutMode.Drawer)]
public void Resolve_UsesWindowDimensions(float width, float height, RuntimeUiLayoutMode expected)
{
    Assert.That(ResponsiveUiLayoutPolicy.Resolve(width, height, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(expected));
}

[Test]
public void Resolve_UsesHysteresisAroundDrawerBoundary()
{
    Assert.That(ResponsiveUiLayoutPolicy.Resolve(1280f, 720f, RuntimeUiLayoutMode.Drawer), Is.EqualTo(RuntimeUiLayoutMode.Drawer));
    Assert.That(ResponsiveUiLayoutPolicy.Resolve(1296f, 720f, RuntimeUiLayoutMode.Drawer), Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
    Assert.That(ResponsiveUiLayoutPolicy.Resolve(1264f, 720f, RuntimeUiLayoutMode.CompressedThreeColumn), Is.EqualTo(RuntimeUiLayoutMode.Drawer));
}
```

- [ ] **Step 2: 运行新增 EditMode 测试确认它们失败。**

Run: `.scripts	est-editmode.cmd`

Expected: 新增策略类型和方法尚未存在，测试失败；不修改已有测试结果。

- [ ] **Step 3: 实现无布局测量的策略。**

使用以下固定滞后值：进入抽屉 `width < 1264` 或 `height < 624`，退出抽屉 `width >= 1296` 且 `height >= 656`；进入完整三栏 `width >= 1616`，离开完整三栏 `width < 1584`。普通无前态计算使用 1600/1280 分界。策略只读取传入的窗口宽高，不读取任何 `RectTransform.rect`。

- [ ] **Step 4: 写字体基线失败测试。**

```csharp
[Test]
public void Typography_DrawerModeKeepsReadableMinimums()
{
    var profile = ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode.Drawer, 1024f, 640f);

    Assert.That(profile.labelSize, Is.GreaterThanOrEqualTo(18));
    Assert.That(profile.auxiliarySize, Is.GreaterThanOrEqualTo(16));
    Assert.That(profile.showLowPriorityText, Is.False);
}

[Test]
public void Typography_DesktopShowsAllPriorityLevels()
{
    var profile = ResponsiveUiTypography.ForMode(RuntimeUiLayoutMode.FullThreeColumn, 1920f, 1080f);

    Assert.That(profile.labelSize, Is.GreaterThanOrEqualTo(16));
    Assert.That(profile.valueSize, Is.GreaterThanOrEqualTo(16));
    Assert.That(profile.showLowPriorityText, Is.True);
}
```

- [ ] **Step 5: 实现字体配置。**

保持 CanvasScaler 参考分辨率不变，但不让标签继续使用 11–12 的逻辑字号。完整三栏使用标签 16、数值 16、标题 20；压缩三栏使用标签 16、数值 16、标题 18；抽屉模式使用标签 18、辅助文字 16、标题 20，并隐藏低优先级指标。`GetEffectiveCanvasScale` 只用于选择 profile，不改变布局断点。

- [ ] **Step 6: 运行测试并提交。**

Run: `.scripts\test-editmode.cmd`

Expected: 新增布局策略和字体测试通过，已有测试保持通过。

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutPolicy.cs UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiTypography.cs UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiLayoutPolicyTests.cs UnderwaterGliderTwin/Assets/Tests/EditMode/ResponsiveUiTypographyTests.cs && git commit -m "feat: add responsive UI policy and typography profiles"`

---

## Task 2: 统一 RuntimeUiRoot、ModalRoot 与引用所有权

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`
- Modify: `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scenes/Main.unity`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`

**Interfaces:**
- `RuntimeUiRoot.DrawerLayer` 返回现有 `modalRoot`，不创建新对象。
- `RuntimeUiReferences.layout` 类型为 `ResponsiveLayoutRefs`，包含 `systemBar`、`configurationArea`、`mainBody`、`telemetryColumn`、`viewportColumn`、`statusColumn`、`playbackBar`、`drawerScrim`、`telemetryDrawerToggle`、`statusDrawerToggle`。
- `EditableUiSceneBuilder` 必须保持 `BuildMainScene` 幂等，重复执行不能生成第二个 `RuntimeUiRoot`、`RuntimeCanvas` 或 `ModalRoot`。

- [ ] **Step 1: 为 ModalRoot 归属写回归测试。**

在 `EditableUiReferenceTests` 和 `EditableUiPlayModeTests` 中断言：

```csharp
Assert.That(runtimeRoot.ModalRoot.name, Is.EqualTo("ModalRoot"));
Assert.That(runtimeRoot.DrawerLayer, Is.SameAs(runtimeRoot.ModalRoot));
Assert.That(FindSceneObject(scene, "OceanCurrentDrawer").transform.parent.name, Is.EqualTo("ModalRoot"));
Assert.That(FindSceneObject(scene, "FlightLegDrawer").transform.parent.name, Is.EqualTo("ModalRoot"));
Assert.That(FindSceneObjects(scene, "DrawerLayer").Count, Is.EqualTo(0));
```

- [ ] **Step 2: 运行回归测试确认新引用尚未实现。**

Run: `.scripts\test-editmode.cmd`

Expected: 新增属性或布局引用缺失导致新增断言失败。

- [ ] **Step 3: 保留 ModalRoot 作为唯一容器。**

在 `RuntimeUiRoot` 中增加 `DrawerLayer` 只读别名和布局引用校验；不要重命名场景中的 `ModalRoot`。在 `RuntimeUiReferences` 中增加布局引用组，但不把 `OceanCurrentDrawer`、`FlightLegDrawer` 移到新根。

明确配置抽屉所有权：不新建顶层 `ConfigurationDrawer`。现有 `DataInputView.Layout.cs` 的 `bottomDrawerContent`/`bottomDrawerViewport` 继续承载高级参数；只有海流和航段弹窗作为 `ModalRoot` 的直接子对象。

- [ ] **Step 4: 更新 EditableUiSceneBuilder。**

调整 `BuildMainScene`、`AssignMainReferences`、`AssignUiGroup`、`EnsurePanelPrefabInstance` 和 `EnsureModalPrefabInstance`：在 `RuntimeCanvas` 下创建 `UiRoot/SystemBar/ConfigurationArea/MainBody/TelemetryColumn/ViewportColumn/StatusColumn/PlaybackBar`，在 `ModalRoot` 下创建 `DrawerScrim` 和现有两个 Drawer Prefab 实例；把新的 `ResponsiveUiLayoutController` 挂到 `UiRoot`。

- [ ] **Step 5: 写入 Main.unity 并验证幂等。**

运行编辑器构建入口两次，第二次不得产生新增对象或重复引用；确认所有长期面板仍为 Prefab 实例。

- [ ] **Step 6: 运行 EditMode/PlayMode 回归并提交。**

Run: `.scripts\test-editmode.cmd`；随后在 Unity Test Runner 运行 `EditableUiPlayModeTests` 与 `ResponsiveUiLayoutPlayModeTests`。

Expected: 场景只有一个 `RuntimeUiRoot`、一个 Canvas、一个 EventSystem 和一个 `ModalRoot`。

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs UnderwaterGliderTwin/Assets/Scenes/Main.unity UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs && git commit -m "refactor: unify runtime UI root and drawer ownership"`

---

## Task 3: 建立统一的布局组件与深海视觉系统

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/DashboardPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/StatusPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/DataInputPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/PlaybackControlsPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCommandToolbar.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/FlightLegDrawer.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCurrentDrawer.prefab`
- Modify: `UnderwaterGliderTwin/Assets/Scenes/Main.unity`

**Interfaces:**
- `UiFactory` 保留现有 `Panel`, `Button`, `InputField`, `Slider`, `Toggle` 等公开工厂方法的调用契约，只统一其颜色、字号、最小高度和状态色。
- 新增布局辅助方法：`ConfigureVerticalContent(RectTransform content, float spacing)`、`ConfigureHorizontalZone(RectTransform zone, float minWidth, float preferredWidth)`、`ApplyCommandPalette(Graphic graphic, UiVisualRole role)`。
- `ResponsiveUiLayoutController` 消费这些布局组件，不通过 `UiFactory` 反向读取业务状态。

- [ ] **Step 1: 将颜色和控件状态写成失败测试/固定断言。**

在 `ResponsiveUiTypographyTests` 或新的 `UiFactoryVisualTests` 中断言：普通面板不是高饱和青/黄，主强调色为 `#5DD7E8` 对应的 Unity Color，禁用状态明显低于默认状态，按钮文字在主按钮上使用深色。

- [ ] **Step 2: 运行测试确认当前颜色断言不满足。**

Run: `.scripts\test-editmode.cmd`

Expected: 当前 `CommandPanelEdge`、`CommandAccent` 和工作区中高饱和 Prefab 覆盖值导致新增视觉断言失败。

- [ ] **Step 3: 更新 UiFactory 视觉令牌。**

将 `CommandPanelFill`、`CommandPanelEdge`、`CommandAccent`、`CommandText` 统一到设计文档颜色。补充默认、悬停、按下、禁用和焦点描边的 `ColorBlock`；不要使用大面积青色/黄色背景。按钮最小高度 32px，抽屉入口最小命中高度 36px。

- [ ] **Step 4: 把生成式 UI 的绝对坐标改为布局组件。**

调整 `EnsureCommandCenterHeader`、`ConfigureRect` 相关调用和运行时生成区域：`SystemBar`、`ConfigurationArea`、`MainBody`、三列、`PlaybackBar` 使用锚点 + `LayoutGroup` + `LayoutElement`；中央列 `flexibleWidth = 1`、`minWidth = 640`；根节点不添加 `ScrollRect`。

- [ ] **Step 5: 更新 Prefab 的视觉与布局序列化配置。**

为 `DashboardPanel`、`StatusPanel`、`DataInputPanel`、`PlaybackControlsPanel`、`OceanCommandToolbar` 和两个 Drawer 设置深色面板、统一边线、内部间距、最小高度、滚动视口和文本溢出规则。删除四个当前工作区 Prefab 中的大面积青色/黄色覆盖色，但保留功能组件和事件引用。

- [ ] **Step 6: 在四组尺寸下做一次人工布局检查并提交。**

运行生成式 UI 和可编辑场景 UI，检查 1920×1080、1366×768、1280×720、1024×640 下没有新遮挡；截图保存到 `TestResults/ui-plan-task3-*.png`。

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/UI/Prefabs UnderwaterGliderTwin/Assets/Scenes/Main.unity && git commit -m "feat: apply responsive command center visual system"`

---

## Task 4: 实现响应式布局控制器和抽屉状态机

**Files:**
- Create: `UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs`
- Modify: `UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs`

**Interfaces:**
- `ResponsiveUiLayoutController.CurrentMode { get; }`
- `ResponsiveUiLayoutController.RefreshForScreen(float width, float height)`
- `ResponsiveUiLayoutController.OpenSideDrawer(RuntimeUiSideDrawer drawer)`
- `ResponsiveUiLayoutController.CloseSideDrawer()`
- `ResponsiveUiLayoutController.ToggleAdvancedConfiguration()` 只控制 `DataInputView` 已有的 bottom drawer，不创建新的顶层配置 Modal。
- `RuntimeUiSideDrawer` 取值为 `Telemetry`、`Status`。

- [ ] **Step 1: 写布局控制器测试。**

验证 `RefreshForScreen(1366,768)` 保持三栏，`RefreshForScreen(1279,720)` 进入抽屉模式；从 1280 到 1279 只切换一次；从抽屉模式恢复到 1296 后进入压缩三栏；同侧抽屉打开时另一个抽屉关闭。

- [ ] **Step 2: 实现区域模式切换。**

控制器从 `RuntimeUiReferences.layout` 取得区域引用，设置三栏的 `GameObject.activeSelf`、入口按钮和 `LayoutElement`；不读取布局后宽度作为断点。窗口变化由 `OnRectTransformDimensionsChange` 或显式 `RefreshForScreen` 触发，但每次只根据 Canvas 尺寸计算一次目标模式。

- [ ] **Step 3: 实现抽屉遮罩与焦点回退。**

打开抽屉时设置遮罩可见、`blocksRaycasts = true`，把 `EventSystem.current.SetSelectedGameObject` 指向抽屉首个可交互控件；关闭时还原 `blocksRaycasts = false` 并把焦点返回对应入口按钮。处理 `Input.GetKeyDown(KeyCode.Escape)`，默认动画 160ms，增加 `animationsEnabled` 序列化开关以支持关闭动画。

- [ ] **Step 4: 接入配置高级参数抽屉。**

在 `DataInputView.Layout.cs` 中保留 `bottomDrawerContent`、`bottomDrawerViewport` 和 `bottomDrawerScrollRect`，将高频字段移动到固定可见区域，把动态参数放进单一 ScrollRect；不要把该区域再注册成 `ModalRoot` 子对象。

- [ ] **Step 5: 在 TwinBootstrap 两条 UI 路径初始化控制器。**

当 `useGeneratedRuntimeUi` 为真时，`UiFactory` 生成完 `RuntimeCanvas` 后创建/绑定控制器；当使用序列化 `RuntimeUiRoot` 时，在 `ValidateConfiguredRuntimeUi` 通过后绑定同一控制器。两条路径均不能创建第二个 Canvas 或第二个 ModalRoot。

- [ ] **Step 6: 运行测试并提交。**

Run: `.scripts\test-editmode.cmd`；Unity Test Runner 运行 `ResponsiveUiLayoutPlayModeTests`。

Expected: 断点不会震荡，抽屉只在主动打开时覆盖中央区域，关闭后焦点回到入口。

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/UI/ResponsiveUiLayoutController.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiRoot.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs UnderwaterGliderTwin/Assets/Scripts/UI/DataInputView.Layout.cs UnderwaterGliderTwin/Assets/Editor/EditableUiSceneBuilder.cs UnderwaterGliderTwin/Assets/Scripts/Bootstrap/TwinBootstrap.cs && git commit -m "feat: add stable responsive UI layout controller"`

---

## Task 5: 整理业务视图绑定、文本密度和唯一控制入口

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs`
- Modify: `UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/DashboardPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/StatusPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/OceanCommandToolbar.prefab`
- Modify: `UnderwaterGliderTwin/Assets/UI/Prefabs/PlaybackControlsPanel.prefab`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`

**Interfaces:**
- `DashboardView` 和 `StatusPanelView` 继续消费已有 `DashboardPanelRefs`、`StatusPanelRefs`，只改变行创建和布局方式。
- `OceanCommandToolbarView` 是唯一相机模式入口，消费 `OceanToolbarRefs`。
- `PlaybackControlsView` 继续消费 `PlaybackControlsRefs`，但对旧的 `cameraFollowButton`、`cameraGlobalButton`、`cameraOrbitButton` 只做兼容隐藏，不再创建第二组入口。

- [ ] **Step 1: 为重复控制入口写 PlayMode 失败测试。**

加载 Main 场景后统计相机按钮，断言中央工具条包含六个相机操作，PlaybackBar 不显示旧相机按钮；断言播放按钮、速度按钮和图层开关各只有一组。

- [ ] **Step 2: 将 DashboardView 行创建改为布局组。**

调整 `AddRow`、`AddAdvancedRow` 和 `BuildNavigationReferenceCard`：使用 `VerticalLayoutGroup`、`LayoutElement`、固定数值列宽和 `Text.horizontalOverflow = OverflowMode.Ellipsis`；移除依赖 `topOffset` 的逐行绝对定位。高级动态行放进现有 `advancedRowsRoot` 的 ScrollRect 内容区。

- [ ] **Step 3: 将 StatusPanelView 指标行改为布局组。**

调整 `AddRow`、`RegisterPredictionMetric` 和 `SetPredictionMetricsVisible`：保持预测/工程指标数据绑定不变，只改为两列键值行；状态信息同时输出文字，错误和告警信息支持换行或省略，不撑破父级。

- [ ] **Step 4: 固定相机控制归属。**

在 `OceanCommandToolbarView` 保留跟随、全局、俯视、侧视、环绕、复位；在 `PlaybackControlsView.HideLegacyCameraControls` 中停用旧相机按钮和旧布局节点，但保留序列化引用以兼容旧 Prefab。确认 `missionVolumeButton` 仍有唯一可见入口。

- [ ] **Step 5: 处理输入、状态和焦点样式。**

为输入框设置可见标签、统一 placeholder、省略号和固定数值列宽；为按钮设置悬停、按下、禁用、焦点描边；为图标/无文字按钮补 Tooltip；验证键盘 Tab 顺序为系统栏 → 配置区 → MainBody → 播放区。

- [ ] **Step 6: 运行业务回归并提交。**

Run: `.scripts\test-editmode.cmd`；Unity Test Runner 运行 `EditableUiPlayModeTests` 和 `ResponsiveUiLayoutPlayModeTests`。

Expected: CSV、仿真、预测、海流配置、播放、图层、相机和退出动作保持原行为，且不存在重复相机控制入口。

Commit: `git add UnderwaterGliderTwin/Assets/Scripts/UI/DashboardView.cs UnderwaterGliderTwin/Assets/Scripts/UI/StatusPanelView.cs UnderwaterGliderTwin/Assets/Scripts/UI/OceanCommandToolbarView.cs UnderwaterGliderTwin/Assets/Scripts/UI/PlaybackControlsView.cs UnderwaterGliderTwin/Assets/Scripts/UI/RuntimeUiReferences.cs UnderwaterGliderTwin/Assets/Scripts/UI/UiFactory.cs UnderwaterGliderTwin/Assets/UI/Prefabs UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs && git commit -m "refactor: consolidate UI bindings and command ownership"`

---

## Task 6: 四分辨率验证、字体可读性检查和构建回归

**Files:**
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/ResponsiveUiLayoutPlayModeTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/EditMode/EditableUiReferenceTests.cs`
- Modify: `UnderwaterGliderTwin/Assets/Tests/PlayMode/EditableUiPlayModeTests.cs`
- Create/Modify: `TestResults/ui-responsive-1920x1080.png`
- Create/Modify: `TestResults/ui-responsive-1366x768.png`
- Create/Modify: `TestResults/ui-responsive-1280x720.png`
- Create/Modify: `TestResults/ui-responsive-1024x640.png`

- [ ] **Step 1: 增加尺寸矩阵 PlayMode 测试。**

对四组尺寸分别调用 `Screen.SetResolution` 或现有 screenshot capture 的分辨率参数，等待一帧布局稳定后断言当前模式、中央视图最小宽度约束、抽屉可打开、根 Canvas/ModalRoot 数量和滚动节点层级。

- [ ] **Step 2: 检查实际文字可读性。**

在四组截图中检查：标签、输入值、按钮文字和状态文字均能在截图中辨认；1024×640 只出现高频内容和抽屉入口；没有文本被裁切、叠加或被颜色背景吞没。特别检查 CSV 长路径、预测不可用、工程校核失败和错误提示。

- [ ] **Step 3: 检查键盘与动画行为。**

验证 Tab 顺序、Escape 关闭、抽屉关闭后的焦点回退、禁用按钮反馈、告警文字和动画开关。关闭动画时不应改变最终布局或交互状态。

- [ ] **Step 4: 运行完整 EditMode 和 PlayMode 回归。**

Run: `.scripts\test-editmode.cmd`

Run: Unity Test Runner PlayMode，至少执行 `EditableUiPlayModeTests`、`ResponsiveUiLayoutPlayModeTests` 和现有 PlayMode 测试。

Expected: EditMode 全部通过；PlayMode 无 UI 根重复、抽屉父级错误、布局震荡或业务动作回归。

- [ ] **Step 5: 生成 Windows 构建并检查启动日志。**

Run: `.scripts\build-windows.cmd`

Expected: `TestResults/WindowsBuild.log` 包含 `Build Finished, Result: Success`，启动后使用与四组尺寸对应的截图参数完成视觉检查。

- [ ] **Step 6: 汇总验收证据并提交。**

记录四组截图路径、EditMode XML、PlayMode 结果、构建日志和任何已知限制；确认没有将 `Library`、临时日志或其他工作区改动加入提交。

Commit: `git add UnderwaterGliderTwin/Assets/Tests TestResults/ui-responsive-*.png && git commit -m "test: verify responsive command center UI"`

---

## 实现完成标准

- 运行时生成 UI 与可编辑场景 UI 采用同一层级、同一断点策略和同一颜色/字体令牌。
- `ModalRoot` 是唯一抽屉/弹窗容器，`DrawerLayer` 只作为代码语义别名，不产生第二个场景对象。
- 断点只使用窗口尺寸并带滞后，窗口轻微变化不会在三栏和抽屉间来回切换。
- 1366×768 下中央 3D 视图区仍然可用，1024×640 下抽屉模式可用且文字可读。
- 没有大面积无效留白、重复相机按钮或面板互相遮挡。
- CSV、参数、海流、预测、回放、相机和退出功能保持原有行为。
- EditMode、PlayMode、四组截图和 Windows 构建均有可复核证据。

## 计划自检

- 设计文档中的断点、字体风险、ModalRoot 所有权、完整文件清单和验收项均映射到 Task 1–6。
- 计划没有未决占位语句，也没有把范围不明的工作留给实现阶段。
- 后续任务引用的 `RuntimeUiLayoutMode`、`ResponsiveUiLayoutPolicy.Resolve`、`ResponsiveUiTypographyProfile`、`ResponsiveUiLayoutController` 和 `RuntimeUiSideDrawer` 均在前序任务中定义。
- 计划同时覆盖 `TwinBootstrap` 的生成式 UI 路径和 `EditableUiSceneBuilder` 的 Prefab 路径。
