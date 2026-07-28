# 滑翔机海流、非线性动力学与运行时参数设计

日期：2026-07-28  
状态：已确认，待拆分实施计划

## 目标

本次改造解决四个用户问题：

1. 海流无法稳定获取，且缺少本地离线来源。
2. 排油量导致的浮力变化、滚转响应过于线性。
3. 配置 UI 在不同窗口尺寸下遮挡 3D 仿真。
4. 仿真运行中无法调整滑翔机自身参数。

保持 CSV replay、现有预测入口和旧线性动力学默认行为兼容。

## 海流来源与状态机

### 获取策略

使用明确的 `OceanCurrentAcquisitionMode`：

- `Online`：在线请求 → 最近有效缓存 → 当前选中的本地文件。
- `CacheOnly`：只读缓存；不联网、不启动 Python。
- `LocalFile`：只读当前选中的 JSON/NetCDF；不联网、不读取其他文件。

UI 同时显示“策略”和“实际来源”，例如“Online / local cache”或“LocalFile / NetCDF direct”。失败时保留上一份有效海流，不清空 profile/field，不覆盖有效缓存。

### JSON 与 NetCDF

统一入口 `OceanCurrentFileLoader`：

- JSON 直接调用现有 `CopernicusCurrentResponseParser`。
- NetCDF classic / 64-bit offset 由 C# 读取器处理。
- NetCDF4/HDF5 或 C# 读取器明确报告“不支持”的文件，交给 Python 转换器生成临时 JSON，再走同一 parser。

首版 NetCDF 变量契约：

- 坐标同义名：`time/valid_time`、`depth/lev/deptht`、`lat/latitude`、`lon/longitude`。
- 速度同义名：`u/uo/eastward_sea_water_velocity`、`v/vo/northward_sea_water_velocity`。
- `u/v` 必须能唯一映射到 `[time, depth, lat, lon]` 四个命名维度；没有 `time` 维的静态文件可映射到 `[depth, lat, lon]` 并视为单帧。其余无法映射、缺失维度或出现歧义时拒绝，不按数组长度猜测。
- 单位接受 `m/s`、`m s-1`、`cm/s`、`knot`，统一转换为 `m/s`；未知单位拒绝。
- 经度归一化到 `[-180, 180)`；纬度、经度、深度坐标排序时，必须同步重排 `u/v`（以及可选 `w`）对应数据轴。
- 深度统一为正向向下。
- 多时间帧使用最近时间帧，不做插值。参考时间优先级：用户请求时间 → 仿真开始时间 → 当前系统 UTC。存在 `time` 维时，时间单位缺失或无法解析时拒绝，不默认使用第一帧；静态三维文件不需要时间单位。
- `w` 缺失时填 0。

### Python 转换器失败契约

NetCDF4/HDF5 fallback 使用独立临时目录（位于 `Application.temporaryCachePath` 下，以请求/文件 hash 命名），输入文件只读，输出 JSON 使用临时文件名写入，完成后再原子改名。以下情况均返回可读错误并保留旧海流：Python 不存在、依赖缺失、进程启动失败、超时、非零退出码、输出文件缺失、JSON 不合法或 schema validation 失败。转换输出必须重新经过现有 JSON parser 和同一结构校验；成功或失败回调结束后清理 request、临时 JSON 和 staging 文件，清理失败只记录日志，不覆盖有效缓存。

### 缓存 key

缓存 key 为规范化字段的稳定哈希，至少包含：schema version、dataset/provider version、u/v 变量映射、请求时间 UTC 小时桶、完整 bbox、最小/最大深度、prefetch 宽度、forecast window、时间选择策略。在线来源的 provider version 来自数据集/转换器版本；本地来源的 identity 使用 loader schema version + 规范化绝对路径 + 文件内容 SHA-256，文件大小/mtime 只能作为诊断字段，不能单独作为可信 key。任何字段变化都不能命中旧场。

修复现有缓存读取时丢失 `Field` 的问题，并保证缓存 JSON 与内存结果使用同一 manifest/schema 语义。

## 非线性动力学

在 `GliderDynamicsProfile` 增加可验证参数：

- 浮力曲线指数：`0.5–3.0`。
- 浮力死区：最大活塞行程比例 `0–0.20`。
- 活塞滞回：最大行程比例 `0–0.10`，且不得大于死区比例。
- 浮力/活塞响应时间：`0.1–120 s`。
- 滚转曲线指数：`0.5–3.0`。
- 滚转死区：控制面最大偏角的 `0–25%`。
- 非线性恢复项和滚转控制力矩设置独立上限，最终力矩不得超出安全上限。

使用有界 signed-power 映射、死区、滞回和一阶响应。指数为 1、死区/滞回为 0 时，必须与旧线性模型等价。所有入口通过同一个 profile validator；非法 profile 不得写入运行时状态。

仿真诊断沿用现有可空的 `TelemetryFrame.Diagnostics`/`SimulationDiagnostics`。CSV frame 的 diagnostics 永远为空，不参与预测、告警或轨迹计算，只允许 UI 在判空后显示。

## 运行时仿真热更新

增加 `SimulationStateSnapshot`，保存当前 elapsed、frame index、位置/地理坐标、深度、姿态/角速度、地速/水速、电池、活塞、控制面和当前海流采样。

应用参数的事务规则：

1. 从当前播放帧复制 snapshot 作为 future trajectory seed。
2. 历史 frame 不修改；可创建新容器，但 `[0..currentIndex]` 的 frame 引用必须与旧容器一致。
3. 只从 current time 生成 future frames。
4. 轨迹显示分为历史段和预测段。
5. 当前帧、播放速度、暂停状态、相机状态不变。
6. 重建失败时完全保留旧 profile、旧 future frames、当前帧和相机状态。

运行时可热更新动力学、海流和目标控制参数；改变航段数量、采样间隔等结构参数也只能重建 future，不得加载场景或回到起点。非法输入不改变任何旧状态。

future 重建必须异步或分帧执行。重建期间显示 pending 状态，旧轨迹继续播放；只有完整 future 生成、结构校验和数值边界校验均通过后，才在主线程一次性替换 future 容器。取消、超时或失败都丢弃 staging 结果，不改变旧 profile、旧轨迹、播放状态和相机。

## UI 布局

将当前固定宽度顶部配置面板改为底部参数抽屉：

- 默认收起；展开高度最多窗口高度 35%。
- 3D viewport 有效交互区域至少占窗口高度 65%。
- 抽屉只覆盖 UI overlay，不改变相机投影和世界布局。
- 内容使用 `ScrollRect` 与分组布局，不使用超出窗口的固定 x 坐标。
- 分组：任务、海流、浮力与滚转、机体参数、预测。
- 保持既有关键 GameObject 名称。
- 在 1280×720、1920×1080 下验证中文标签不截断、控件不越界、按钮可点击。

## 任务切分

1. 海流文件导入、NetCDF 契约与缓存状态机。
2. 非线性动力学参数、validator 与默认兼容测试。
3. 运行时仿真会话、snapshot 与 future-only 重建。
4. 底部参数抽屉、来源状态展示与分辨率回归。

每项单独测试和提交；最终运行完整 EditMode、Python、Windows build 与 packaged-player smoke test。

## 主要验收

- 在线失败可从缓存或本地 JSON/NetCDF 恢复；LocalFile 不联网。
- NetCDF classic 直读，NetCDF4/HDF5 converter fallback；变量、单位、维度歧义和时间轴错误均有可读错误。
- 坐标排序后数据轴仍正确对应。
- 非线性参数改变实际浮力/滚转曲线，默认值保持旧测试。
- 运行中修改参数后当前位置、历史轨迹、播放和相机状态不跳变；future 改变。
- 非法输入、无效海流、future 重建失败均不破坏旧状态。
- UI 在目标分辨率下不遮挡 3D 视图。
