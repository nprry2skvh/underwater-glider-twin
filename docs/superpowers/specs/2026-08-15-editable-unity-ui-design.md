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

## 推荐架构

采用“场景/Prefab 负责外观，脚本负责绑定”的结构。

1. `Assets/UI/Images`
   存放背景、面板、按钮、装饰纹理等图片资源。用户把 `.png` 或 `.jpg` 拖进来后，设置为 `Sprite (2D and UI)` 即可用于 UI。

2. `Assets/UI/Prefabs`
   存放可编辑 UI Prefab。每个主要 UI 区块独立成一个 Prefab，用户可以打开 Prefab 修改外观。

3. `Welcome` 场景
   保留一个真实的 `WelcomeCanvas`，其中包含欢迎页背景、标题、说明、CSV 输入框、确认按钮、默认 CSV 按钮、仿真模式按钮和状态文字。`WelcomeBootstrap` 不再创建这些对象，而是绑定场景里已有的对象。

4. `Main` 场景
   保留一个真实的 `RuntimeCanvas`，其中挂载主界面 Prefab。`TwinBootstrap` 不再让各个视图从零创建 UI，而是实例化或绑定这些 Prefab。

5. 绑定脚本
   每个 UI 区块拥有一个对应的绑定脚本，例如 `DashboardView`、`StatusPanelView`、`DataInputView`、`PlaybackControlsView`。这些脚本通过序列化字段引用已有的 `Text`、`Button`、`InputField`、`Slider`、`Image` 等组件。

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

## 数据流

启动流程保持不变：

1. `Welcome` 场景启动。
2. `WelcomeBootstrap` 读取命令行和上次 CSV 路径。
3. 用户点击按钮后，`LaunchCoordinator` 设置数据源并进入 `Main` 场景。
4. `TwinBootstrap` 加载 CSV 或仿真数据，创建可视化对象。
5. `TwinBootstrap` 找到或实例化 `RuntimeCanvas` 和各 UI Prefab。
6. 各 UI 绑定脚本把运行时数据写入已有文字、按钮、输入框和滑条。

这样 UI 外观和业务逻辑分离。用户改图片、位置和颜色时，不需要改数据加载和仿真逻辑。

## 错误处理

每个绑定脚本启动时检查关键引用：

- 必要按钮缺失时，输出包含对象名和 Prefab 名的错误。
- 必要文字缺失时，跳过对应刷新并输出警告。
- 可选装饰图片缺失时不报错。
- 如果 `EventSystem` 缺失，运行时自动补一个，保证按钮可点击。
- 如果 `Canvas` 缺失，显示明确错误，不再静默创建一套不可编辑 UI。

## 测试策略

保留现有 UI EditMode 测试中验证功能的部分，同时调整测试方式：

- 测试欢迎页按钮是否正确调用启动逻辑。
- 测试主界面关键控件引用完整。
- 测试播放、仿真、海流抽屉、飞行腿设置和预测面板仍可通过按钮触发。
- 测试常见分辨率下关键面板不互相遮挡。
- 测试缺失引用时错误信息清楚。

旧测试中依赖 `GameObject.Find` 查找运行时生成对象的部分，需要改为加载 Prefab 或场景后验证绑定对象。

## 实施顺序

为了降低风险，实施分四步：

1. 创建 UI 资源目录、基础 Canvas、欢迎页可编辑结构。
2. 把欢迎页从代码生成改为场景绑定。
3. 为主界面建立 `RuntimeCanvas` 和主要 UI Prefab，并让脚本优先绑定 Prefab。
4. 逐步替换 `UiFactory` 生成控件的路径，只保留少量用于动态列表或临时弹窗的辅助方法。

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

## 风险与取舍

完整可编辑 UI 的自由度最高，但改造范围也最大。项目现有 UI 测试较多，部分测试需要随结构变化更新。主界面里有不少动态内容，完全静态化并不合适；推荐保留动态列表和临时内容的少量代码创建能力，同时把长期存在的面板和控件迁到 Prefab。

这个方案的核心取舍是：牺牲一些一次性开发时间，换取后续 UI 调整的直观性和可维护性。
