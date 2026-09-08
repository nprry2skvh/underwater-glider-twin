# Unity 可编辑 UI 改造设计

## 背景

当前项目的欢迎页和主界面 UI 主要由 C# 脚本在运行时创建。`WelcomeBootstrap` 创建欢迎页的背景、面板、文字、输入框和按钮；`TwinBootstrap` 在主场景中挂载多个运行时 UI 视图；`UiFactory` 统一生成面板、按钮、输入框、滑条、文字等控件。

这导致用户很难在 Unity 编辑器中直接修改 UI 的位置、大小、颜色、图片和文字样式。目标是把 UI 外观从代码中移到 Unity 场景和 Prefab 中，让用户可以亲自拖拽和调整；代码只负责业务行为和数据刷新。

## 目标

改造后，用户可以在 Unity 编辑器中直接编辑几乎所有 UI 视觉元素：

- 背景图片和颜色
- 大面板、小面板、弹窗和抽屉的位置与大小
- 按钮、输入框、滑条、进度条的外观
- 文字内容的摆放、字号、颜色和对齐
- UI 图片资源的替换

功能逻辑仍由脚本负责，包括加载 CSV、进入仿真模式、播放控制、数据刷新、告警状态、预测展示、截图、海流配置和弹窗开关。

## 非目标

本次改造不重新设计水下环境、滑翔机模型、轨迹渲染、海流体渲染、地图瓦片或预测算法。它只改变 UI 的创建和绑定方式。

本次改造不要求最终视觉风格一次定稿。重点是先把 UI 变成可编辑结构，让用户之后能持续调整。

## 硬约束

### UI 根节点唯一所有权

改造后禁止运行时代码通过 `FindObjectOfType<Canvas>()` 自动寻找“第一个 Canvas”作为 UI 根节点。`WelcomeBootstrap` 必须通过 `[SerializeField]` 显式引用 `WelcomeCanvas`；`TwinBootstrap` 必须通过 `[SerializeField]` 显式引用 `RuntimeCanvas` 或 `RuntimeUiRoot`。

`UiFactory.EnsureCanvas()` 不能再作为长期 UI 的入口。迁移期可保留一个 `allowRuntimeFallback` 开关，但默认关闭；生产路径缺少 Canvas 时必须报错，而不是静默创建另一套 UI。

`Main` 场景采用唯一层级：

- `RuntimeUiRoot`
- `RuntimeUiRoot/RuntimeCanvas`
- `RuntimeUiRoot/RuntimeCanvas/CommandCenterHeader`
- `RuntimeUiRoot/RuntimeCanvas/DashboardPanel`
- `RuntimeUiRoot/RuntimeCanvas/StatusPanel`
- `RuntimeUiRoot/RuntimeCanvas/DataInputPanel`
- `RuntimeUiRoot/RuntimeCanvas/PlaybackControlsPanel`
- `RuntimeUiRoot/RuntimeCanvas/OceanCommandToolbar`
- `RuntimeUiRoot/RuntimeCanvas/ModalRoot`

`TwinBootstrap` 只引用 `RuntimeUiRoot`。它不能同时再实例化同一批长期 UI Prefab，避免重复 Canvas、重复按钮和重复面板。

### 不允许清空 Prefab 根节点

绑定脚本不能销毁包含可编辑 UI 的 Prefab 子物体。现有 `DataInputView.ClearRuntimeUi()` 会遍历并删除自身所有子节点，迁移时必须移除，或改成只清理明确标记的动态内容容器，例如列表行、临时提示和运行时生成的数据项。

长期存在的面板、按钮、输入框、文字、背景和装饰图片都归 Prefab 或场景所有，不能由重建流程删除。

### 缺失引用必须显式失败

每个绑定脚本必须提供 `ValidateReferences()`。编辑器中通过 `OnValidate()` 做快速检查；运行时启动时再次检查。关键引用缺失时禁用对应功能并输出清楚错误，错误信息必须包含 Prefab 名、对象路径和字段名。

生产模式禁止静默回退到 `UiFactory` 生成替代 UI。迁移期如果确实需要保留旧路径，必须挂在显式的 `allowRuntimeFallback` 开关下，并在日志中说明当前使用的是迁移回退路径。

`allowRuntimeFallback` 只能用于迁移和开发验证，生产默认关闭。每次启用回退路径必须输出格式明确的日志，例如 `[UI Fallback] Runtime-generated UI is active: DataInputPanel`。

### 动态内容边界

静态标题、标签、按钮文字、背景、面板、输入框外观和按钮外观由 Prefab 管理。实时数值、告警、进度、按钮可用状态和输入框当前值由脚本刷新。

海流层、飞行腿、预测指标等重复内容使用 Prefab 提供的 `RowTemplate`。脚本只实例化数据行、写入数据和控制显示隐藏，不再临时拼整套 UI。弹窗和抽屉由 `ModalRoot` 或对应 Prefab 提供容器，脚本只控制 `SetActive`、内容刷新和事件绑定。

### 统一绑定入口

运行时视图脚本逐步从 `Initialize(...)` 迁移到 `Bind(...)`，或使用 `Initialize(RuntimeUiReferences references, ...)`。绑定入口接收场景或 Prefab 中已经存在的引用，脚本不得在 `Awake`、`Start` 或 `Initialize` 中自行创建长期 UI。

### Prefab 版本管理

Prefab 修改优先在 Prefab Mode 中完成。场景只保存必要的 Prefab 实例覆盖，例如位置、初始显隐和场景级引用。禁止同时大规模修改 Prefab 源和大量场景实例覆盖。

关键 UI Prefab 必须保持稳定对象路径。字段绑定依赖的对象命名和层级不能随意重命名，避免序列化引用频繁断裂。确实需要重命名时，同一次变更必须更新绑定引用和引用完整性测试。

## 推荐架构

采用“场景/Prefab 负责外观，脚本负责绑定”的结构。

1. `Assets/UI/Images`
   存放背景、面板、按钮、装饰纹理等图片资源。用户把 `.png` 或 `.jpg` 拖进来后，设置为 `Sprite (2D and UI)` 即可用于 UI。

2. `Assets/UI/Prefabs`
   存放可编辑 UI Prefab。每个主要 UI 区块独立成一个 Prefab，用户可以打开 Prefab 修改外观。

3. `Welcome` 场景
   保留一个真实的 `WelcomeCanvas`，其中包含欢迎页背景、标题、说明、CSV 输入框、确认按钮、默认 CSV 按钮、仿真模式按钮和状态文字。`WelcomeBootstrap` 不再创建这些对象，而是绑定场景里已有的对象。

4. `Main` 场景
   保留一个真实的 `RuntimeUiRoot` 和 `RuntimeCanvas`，其中挂载主界面 Prefab。`TwinBootstrap` 不再让各个视图从零创建 UI，而是通过序列化字段绑定这些 Prefab。`RuntimeUiRoot` 是主界面 UI 的唯一所有者。

5. 绑定脚本
   每个 UI 区块拥有一个对应的绑定脚本，例如 `DashboardView`、`StatusPanelView`、`DataInputView`、`PlaybackControlsView`。这些脚本通过序列化字段引用已有的 `Text`、`Button`、`InputField`、`Slider`、`Image` 等组件。

6. `UiReferenceValidator`
   在 UI 根节点或主要 Prefab 根节点上增加统一引用验证器。验证器收集各绑定脚本的引用检查结果，并用“Prefab 名 + 对象路径 + 字段名”的格式输出问题，方便用户在 Unity Hierarchy 中定位。

## 主要组件

### WelcomeCanvas

包含：

- `BackgroundImage`
- `LaunchPanel`
- `TitleText`
- `DescriptionText`
- `CsvPathInput`
- `ConfirmCsvButton`
- `StartCsvButton`
- `SimulationButton`
- `LaunchStatusText`

`WelcomeBootstrap` 在启动时检查这些引用是否存在，存在则绑定点击事件和初始 CSV 路径；缺失则在控制台输出清楚错误。

### RuntimeCanvas

包含主界面的长期 UI 容器：

- `CommandCenterHeader`
- `DashboardPanel`
- `StatusPanel`
- `DataInputPanel`
- `PlaybackControlsPanel`
- `OceanCommandToolbar`
- `ModalRoot`

这些对象的位置、大小、图片和颜色由场景或 Prefab 决定。

### RuntimeUiRoot

`RuntimeUiRoot` 是 `Main` 场景中所有长期 UI 的唯一父节点。它保存 `RuntimeCanvas`、`ModalRoot` 和各主面板 Prefab 的显式引用。`TwinBootstrap` 只绑定这个根节点，不再通过全局搜索猜测当前应该使用哪个 Canvas。

`RuntimeUiRoot` 负责：

- 保存主要 UI Prefab 引用
- 暴露 `ValidateReferences()`
- 检查重复 Canvas、重复 EventSystem 和重复长期 UI
- 为弹窗、抽屉和动态列表提供固定容器

### 分组引用

`DataInputView` 的引用数量较多，不能把所有输入框、按钮和文字平铺在一个脚本字段列表里。迁移时按功能分组：

- `MissionSectionRefs`
- `SimulationSectionRefs`
- `OceanSectionRefs`
- `DynamicsSectionRefs`
- `PredictionSectionRefs`

每个分组只保存本区域的控件引用，并提供自己的验证方法。根绑定脚本聚合这些结果。

### UI Prefab

拆分为：

- `DashboardPanel.prefab`
- `StatusPanel.prefab`
- `DataInputPanel.prefab`
- `PlaybackControlsPanel.prefab`
- `OceanCommandToolbar.prefab`
- `OceanCurrentDrawer.prefab`
- `FlightLegDrawer.prefab`
- `MissionConfigurationPanel.prefab`
- `PredictionPanel.prefab`

复杂面板可以先只提取外层容器和常用控件，避免一次性重写所有细节导致风险过大。后续可以逐步把剩余动态控件迁入 Prefab。

弹窗和抽屉也必须进入 Prefab 边界。现有海流弹窗动态创建 Canvas、GraphicRaycaster 和遮罩的逻辑，需要迁入 `ModalRoot` 或 `OceanCurrentDrawer.prefab`。脚本只负责打开、关闭、填充数据和绑定按钮事件。

## 数据流

启动流程保持不变：

1. `Welcome` 场景启动。
2. `WelcomeBootstrap` 读取命令行和上次 CSV 路径。
3. 用户点击按钮后，`LaunchCoordinator` 设置数据源并进入 `Main` 场景。
4. `TwinBootstrap` 加载 CSV 或仿真数据，创建可视化对象。
5. `TwinBootstrap` 使用序列化字段绑定唯一的 `RuntimeUiRoot`，再由 `RuntimeUiRoot` 暴露 `RuntimeCanvas`、`ModalRoot` 和各 UI Prefab 引用。
6. 各 UI 绑定脚本把运行时数据写入已有文字、按钮、输入框和滑条。

这样 UI 外观和业务逻辑分离。用户改图片、位置和颜色时，不需要改数据加载和仿真逻辑。

## 错误处理

每个绑定脚本启动时检查关键引用：

- 必要按钮缺失时，输出包含对象名和 Prefab 名的错误。
- 必要文字缺失时，跳过对应刷新并输出警告。
- 可选装饰图片缺失时不报错。
- 如果 `EventSystem` 缺失，运行时自动补一个，保证按钮可点击。
- 如果 `Canvas` 缺失，显示明确错误，不再静默创建一套不可编辑 UI。
- 如果关键引用缺失，禁用对应功能入口，避免按钮点击后产生空引用异常。
- 如果发现重复 Canvas、重复 EventSystem 或重复长期 UI，输出错误并阻止继续绑定。

## 测试策略

测试分为 EditMode 和 PlayMode 两类。

EditMode 测试：

- 验证 UI Prefab 引用完整。
- 验证 `OnValidate()` 和 `ValidateReferences()` 能发现缺失关键引用。
- 验证 `RuntimeUiRoot`、`WelcomeCanvas`、`RuntimeCanvas` 和 `ModalRoot` 结构存在。
- 验证常见分辨率 1920×1080、1366×768 下关键面板不互相遮挡。

PlayMode 测试：

- 验证欢迎页按钮点击、CSV 加载、仿真启动、播放控制、抽屉、弹窗和数据刷新。
- 验证海流层、飞行腿、预测指标等动态列表仍能刷新。
- 使用 `LogAssert.Expect` 验证缺失引用时的错误日志。
- 验证正常运行时没有重复 Canvas、重复 EventSystem 或重复长期 UI。

旧测试中依赖 `GameObject.Find` 查找运行时生成对象的部分，需要改为加载 Prefab 或场景后验证绑定对象。

## 实施顺序

为了降低风险，实施分六步：

1. 创建 `RuntimeUiRoot`、`UiReferenceValidator` 和分组引用结构，先定义唯一 UI 根节点和引用验证机制。
2. 处理结构性风险：移除或限制 `ClearRuntimeUi()`，替换 `EnsureCanvas()` 的长期 UI 入口，迁移动态弹窗 Canvas 创建逻辑到 `ModalRoot`。
3. 创建 UI 资源目录、基础 Canvas、欢迎页可编辑结构。
4. 把欢迎页从代码生成改为场景绑定。
5. 为主界面建立 `RuntimeCanvas` 和主要 UI Prefab，并让脚本优先绑定 Prefab。
6. 逐步替换 `UiFactory` 生成控件的路径，只保留少量用于动态列表数据行或临时内容的辅助方法。

每一步都保留可运行状态，避免一次性大改后难以定位问题。

## 用户编辑流程

改造完成后，用户修改 UI 的日常流程是：

1. 把图片拖入 `Assets/UI/Images`。
2. 选中图片，在 Inspector 中设置 `Texture Type` 为 `Sprite (2D and UI)` 并应用。
3. 打开 `Welcome` 场景或对应 UI Prefab。
4. 选中要修改的 `Image`、`Button`、`Text` 或 `InputField`。
5. 调整 `Rect Transform`、`Image`、`Text`、`Button` 等组件。
6. 保存场景或 Prefab。
7. 点击 Play 检查效果。

## 验收标准

改造完成后必须满足：

- 不运行游戏时，`Welcome` 和 `Main` 场景的完整长期 UI 都能在 Hierarchy 中看到。
- 修改 Prefab 的颜色、位置、文字或图片后，无需改代码即可生效。
- 正常运行时 `UiFactory` 不再创建长期存在的 UI。
- 正常运行时不存在重复 Canvas、重复 EventSystem 或重复按钮。
- Prefab 缺少关键引用时能明确报错，且不会静默生成替代 UI。
- 所有动态列表仍可正常刷新。
- 海流弹窗、飞行腿抽屉、预测面板等复杂 UI 仍可打开、关闭和更新内容。

## 风险与取舍

完整可编辑 UI 的自由度最高，但改造范围也最大。项目现有 UI 测试较多，部分测试需要随结构变化更新。主界面里有不少动态内容，完全静态化并不合适；推荐保留动态列表和临时内容的少量代码创建能力，同时把长期存在的面板和控件迁到 Prefab。

这个方案的核心取舍是：牺牲一些一次性开发时间，换取后续 UI 调整的直观性和可维护性。
