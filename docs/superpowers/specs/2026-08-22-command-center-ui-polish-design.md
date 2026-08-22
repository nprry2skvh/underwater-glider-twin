# UnderwaterGliderTwin 指挥舱 UI 精修设计

## 状态

方案 A 已通过审核。本设计用于指导后续逐项实现；实现前必须保留现有 UI Root、Prefab 绑定和仿真业务逻辑。

## 目标

在现有响应式三栏/抽屉架构上完成一次视觉和交互精修，解决当前截图中底部配置区像调试占位、1280 宽度栏宽契约不成立、标题层级弱、字体实际显示过小、焦点反馈不足、空状态和长文本缺少设计的问题。

最终 UI 应体现“深海任务指挥舱”的审美：深色克制、中央 3D 视图区为唯一视觉中心、青色只表达操作与当前状态、信息层级由字号/间距/边线建立，而不是依靠大面积高饱和色块。

## 范围

### 包含

- 底部任务配置摘要栏和展开内容重构；
- 1280px 压缩三栏的栏宽契约和断点策略修正；
- 顶部系统栏、中央视图工具条和播放栏的视觉层级；
- 统一颜色令牌、字号角色、实际 CanvasScaler 缩放验证；
- 抽屉焦点进入、焦点回退、键盘导航、遮罩和 Escape 行为；
- Tooltip、状态徽标、空状态、告警、预测状态和长文本处理；
- 四种 Windows Player 分辨率截图、EditMode、PlayMode 和 Windows 构建验收。

### 不包含

- 不修改仿真、海流采样、预测模型、回放模型或相机业务逻辑；
- 不引入新的 UI 框架；
- 不创建第二个 Canvas、第二个 ModalRoot 或名为 DrawerLayer 的场景根对象；
- 不把挂载 `DataInputView` 的 `DataInputPanel` 根对象设置为 inactive；
- 不自动覆盖用户已经修改的 Prefab、场景实例颜色、位置、字体或事件引用；
- 不删除非 Prefab 的同名对象；
- 不用装饰性发光、渐变或高饱和色代替信息层级。

## 现有架构约束

长期 UI 层级保持：

```text
RuntimeCanvas
├── UiRoot
│   ├── SystemBar
│   ├── ConfigurationArea
│   │   ├── ConfigurationSummaryBar
│   │   └── ConfigurationExpandedContent
│   ├── MainBody
│   │   ├── TelemetryColumn
│   │   ├── ViewportColumn
│   │   └── StatusColumn
│   ├── PlaybackBar
│   └── DrawerEntryLayer
│       ├── TelemetryDrawerToggle
│       └── StatusDrawerToggle
└── ModalRoot
    ├── DrawerScrim
    ├── OceanCurrentDrawer
    ├── FlightLegDrawer
    └── TooltipPopup
```

`TelemetryDrawerToggle` 和 `StatusDrawerToggle` 必须位于 `UiRoot/DrawerEntryLayer`，不能位于会在抽屉模式中隐藏的左右栏内部。

`DataInputView` 继续挂载在 `DataInputPanel` 根对象上。折叠配置区时只切换 `ConfigurationExpandedContent` 的可见性、尺寸或 `CanvasGroup` 状态，不能停用 `DataInputPanel` 根对象，不能触发 `DataInputView.OnDisable`。

## 响应式布局规则

### 模式

| 条件 | 模式 | 规则 |
|---|---|---|
| 宽度 >= 1616 且高度 >= 656 | 完整三栏 | 左 280px、中央 flexible、右 320px、间距 16px |
| 宽度 1280–1615 且高度 >= 624 | 压缩三栏 | 左最小 236px、中央最小 640px、右最小 260px、间距 12px |
| 宽度 < 1280 或高度 < 624 | 抽屉 | 隐藏左右栏，只保留中央视图和常驻抽屉入口 |

### 滞后

- 抽屉退出：宽度 >= 1296 且高度 >= 656；
- 完整模式退出：宽度 < 1584；
- 模式判断只读取屏幕或 Canvas 可用宽高，不读取布局完成后的 `ViewportColumn.rect.width` 作为唯一依据；
- 重复调用相同尺寸的刷新方法不得创建新对象或重复绑定事件。

### 区域高度

- 完整模式：SystemBar 52px，ConfigurationArea 144px，PlaybackBar 92px；
- 压缩模式：SystemBar 52px，ConfigurationArea 128px，PlaybackBar 88px；
- 抽屉模式：ConfigurationArea 72px 摘要态，PlaybackBar 84px；
- 配置高级内容展开时使用内部滚动，不改变中央视图和播放栏的所有权；
- 根 UI 不使用 `ScrollRect`；配置、遥测、状态或单个弹窗内部最多各有一个纵向滚动区域。

## 底部配置区设计

### 摘要态

摘要栏只显示一条结构化信息和展开按钮，例如：

```text
任务配置   CSV 回放 · 物理模型 · 时域 120 s · 海流层 0                  展开参数
```

折叠时不得留下旧输入框、空背景条、重复标题或隐藏控件的残余布局。

### 展开态

内容分为三个卡片：

1. 任务与预测：CSV、模型、时域、应用预测、运行状态；
2. 仿真参数：循环次数、周期、深度、航向、俯仰、横滚和安全参考；
3. 海流与航段：海流层、经纬度、层切换、保存、删除和航段配置。

高频操作保留在摘要或第一行；低频参数只进入 `ConfigurationExpandedContent` 内的单一 `ScrollRect`。

## 视觉系统

```text
页面背景    #071821
普通面板    #0B2430
内容卡片    #102F3C
控件背景    #153B4A
输入框      #071821
分隔线      #164454
主文字      #E5F2F3
次要文字    #91B5BE
强调色      #5DD7E8
正常状态    #41C6A7
警告状态    #F2C66D
错误状态    #F27B7B
```

规则：

- 面板背景使用深蓝，不能整块使用青色或黄色；
- 当前选中视角、主按钮和焦点使用强调色；
- 黄色仅用于警告和预测历史；
- 告警、预测、待机等状态必须同时有文字或图标，不能只靠颜色；
- 区块标题使用 16–18px 和左侧短强调线；
- 普通标签使用 14–16px，数值使用 14–16px，关键 KPI 使用 18–20px；
- 数值列固定宽度，单位独立显示，避免实时刷新导致布局跳动。

## 字体和实际可读性

实际显示尺寸统一按以下公式验收：

```text
实际字号 = 逻辑字号 × CanvasScaler 有效缩放比例
```

要求：

- 1024×640 下高频标签和按钮实际显示高度目标为 11–12px；
- 低优先级辅助文字在抽屉模式中可以隐藏；
- Prefab 路径和 fallback 路径必须应用同一套 `UiTextRole`；
- 不允许旧的硬编码字号覆盖统一字体角色；
- 测试必须同时验证逻辑字号、有效缩放后的字号以及四种实际截图；
- 普通文本不得因 Wrap/Truncate 组合而覆盖相邻控件；
- 长 CSV 路径显示省略版本，完整路径通过 Tooltip 提供。

建议的文字角色：

```text
Title, SectionTitle, Label, Value, Button, Auxiliary, Error
```

## Tooltip 和状态徽标载体

### Tooltip

新增 `UiTooltip` 组件和 `UiTooltipController`：

- `UiTooltip` 挂在图标按钮或无文字按钮上，提供 `message`；
- `UiTooltipController` 挂在 `RuntimeCanvas/ModalRoot/TooltipPopup` 下；
- Tooltip 不创建 Canvas，不阻挡射线，不改变 ModalRoot 所有权；
- 鼠标悬停或键盘聚焦后延迟约 350ms 显示；
- 最大宽度 280px，支持换行，自动限制在 Canvas 可见区域内；
- 鼠标离开、焦点变化、抽屉关闭或按 Escape 时隐藏；
- Tooltip 不遮挡当前按钮和主要操作区域。

### 状态徽标

新增 `UiStateBadge` 组件，统一渲染：

- 状态文字；
- 状态图标或几何标记；
- 左侧色条或边框；
- 正常、待机、预测、警告、错误五种状态。

状态绑定必须保持现有业务 View 的数据来源不变，只改变显示载体。

## 抽屉和焦点行为

打开抽屉时：

1. 保存当前入口按钮；
2. 让对应抽屉可见并设置 `interactable = true`、`blocksRaycasts = true`；
3. 显示遮罩，阻止中央视图点击；
4. 焦点强制进入抽屉内第一个可交互控件；
5. 当前焦点为空、位于中央视图区、位于播放栏或已在抽屉内部时都必须正确处理。

关闭抽屉时：

1. 通过 Escape、遮罩点击或入口按钮关闭；
2. 设置抽屉 `interactable = false`、`blocksRaycasts = false`；
3. 隐藏遮罩；
4. 焦点返回打开抽屉前保存的入口按钮；
5. 关闭动画和无动画模式的最终状态必须一致。

所有 Button、InputField 和 Toggle 必须具有可见焦点反馈和明确 Navigation 顺序，顺序为：SystemBar → ConfigurationArea → MainBody → PlaybackBar；抽屉打开时焦点限制在抽屉内部。

## 空状态、告警和长文本

- 遥测没有有效数据时显示“尚未加载有效轨迹”和下一步操作提示；
- 预测不可用时显示文字原因，不只显示黄色；
- 告警同时显示文字、图标或色条；
- 错误消息允许换行，不撑破按钮和面板；
- CSV 路径使用省略文本，Tooltip 显示完整路径；
- 播放导出路径和仿真错误状态使用专用状态行；
- 运行中数值固定列宽，实时刷新不得改变卡片宽度。

## 文件职责和预计修改范围

### 新增

- `UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltip.cs`：单个控件的 Tooltip 数据与触发接口；
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiTooltipController.cs`：ModalRoot 下唯一 Tooltip 浮层；
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiStateBadge.cs`：状态文字、图标和色条的统一渲染；
- `UnderwaterGliderTwin/Assets/Scripts/UI/UiFocusVisual.cs`：Button/InputField/Toggle 的可见焦点反馈；
- 相关 EditMode/PlayMode 测试文件。

### 修改

- `UiFactory.cs`：颜色令牌、字体角色、控件状态、Tooltip/焦点/徽标辅助创建；
- `ResponsiveUiLayoutPolicy.cs`：1280 压缩布局与滞后阈值；
- `ResponsiveUiTypography.cs`：角色字号和实际缩放计算；
- `ResponsiveUiLayoutController.cs`：模式切换、抽屉焦点、动画和遮罩；
- `DataInputView.cs`、`DataInputView.Layout.cs`：摘要态与展开态，不停用业务根对象；
- `DashboardView.cs`、`StatusPanelView.cs`：空状态、固定数值列、状态徽标和长文本；
- `OceanCommandToolbarView.cs`、`PlaybackControlsView.cs`：唯一控制入口和视觉层级；
- `RuntimeUiRoot.cs`、`RuntimeUiReferences.cs`、`TwinBootstrap.cs`：Prefab/fallback 统一引用；
- `EditableUiSceneBuilder.cs`、`Main.unity` 和相关 Prefab：只补齐缺失结构，不整体重置覆盖。

## 测试和验收

### EditMode

- 断点边界：`1279/1280`、`1296`、`1584/1616`、`623/624`、`655/656`；
- 逻辑字号和 CanvasScaler 有效字号；
- Prefab/fallback 使用同一 `UiTextRole`；
- 配置区折叠不会停用 `DataInputView` 根对象；
- 不会生成第二个 Canvas、ModalRoot 或 TooltipPopup；
- Prefab 已存在时不会被整体覆盖。

### PlayMode

- 配置区折叠/展开后 CSV、预测和仿真仍能执行；
- 当前焦点为空时打开抽屉；
- 当前焦点在中央视图区时打开抽屉；
- 当前焦点在播放栏时打开抽屉；
- 当前焦点已在抽屉内时重复打开/关闭；
- 关闭后焦点返回对应入口；
- 遮罩阻止背景点击，关闭后不再拦截；
- Escape、遮罩点击和入口按钮关闭行为一致；
- 无动画与有动画的最终状态一致；
- 相同尺寸重复刷新不重复创建对象或绑定事件。

### Windows Player 视觉验收

实际 Player 生成以下截图：

```text
TestResults/ui-polish-1920x1080.png
TestResults/ui-polish-1366x768.png
TestResults/ui-polish-1280x720.png
TestResults/ui-polish-1024x640.png
```

检查：

- 没有旧配置控件残留；
- 没有无文字深色占位条；
- 顶栏标题和状态可辨认；
- 1280 下三栏不挤压文字；
- 1024 下抽屉入口一直可见；
- 所有高频按钮文字实际可读；
- 中央视图区始终是最大视觉区域；
- 没有滚动区域互相嵌套滚动；
- 没有透明控件拦截 3D 视图。

命令：

```powershell
E:\upan\digital twin\scripts\test-editmode.cmd
E:\upan\digital twin\scripts\build-windows.cmd
```

## 实施顺序

1. 先增加测试和字体/断点基线；
2. 重做配置摘要态与展开态；
3. 修正 1280 压缩布局；
4. 统一颜色、字体、顶栏、中央工具条和播放栏；
5. 实现 Tooltip、状态徽标和焦点视觉；
6. 完善空状态、告警和长文本；
7. 用实际 Windows Player 截图、PlayMode、EditMode 和构建完成验收。

每项任务独立测试、独立审查、独立提交。所有提交必须避开工作区已有的非 UI 用户改动。
