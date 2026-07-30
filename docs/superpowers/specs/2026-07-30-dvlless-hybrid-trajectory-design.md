# 无 DVL 混合三维轨迹推算与概率预测：冻结设计基线

## 系统定位

本系统是无 DVL 的轨迹推算与概率预测系统，不是持续精确水下定位系统。它以水面 GPS、CTD、AHRS/IMU、控制量和可选海流资料为输入，输出未来 30、60、120、300 秒的三维预测均值轨迹、预测包络、健康状态和未来控制假设。

潜航期间无 GPS、无 ADCP 时，系统不宣称水平轨迹已被直接观测验证；水平不确定度必须随时间增长。Phase A 将 ADCP 延后，不定义其观测接口。

## 坐标、状态和观测

统一使用 NED 坐标：North、East、Down，深度 D 向下为正。海流 c 是相对地球的 NED 速度。唯一合法地速方程是：

~~~text
v_ground = R_n_b(attitude) * v_water_body + c
~~~

在线状态：

~~~text
x = [pN, pE, pD, cN, cE, cD]
~~~

GPS 默认观测 [pN,pE]，CTD 默认观测 pD；GPS 高程只能在另建基准、精度和水面条件契约后作为可选测量。AHRS 是带时间戳的外部姿态输入。ADCP 若存在可观测三维海流，否则不是运行必要条件。

## 健康状态

Phase A 中，FULL 表示 AHRS、CTD、控制量和物理模型正常，且有近期 GPS 水平约束；DEPTH_ONLY 表示 CTD 正常、但 AHRS 或控制输入不足以进行可信水平传播；DEAD_RECKONING 表示 AHRS、CTD、控制与物理模型正常但无近期 GPS；UNRELIABLE 表示 CTD 不可用，或多个核心输入/模型同时失效。DEPTH_ONLY 只输出可信深度和低可信水平占位信息，DEAD_RECKONING 继续输出水平推算但扩大水平不确定度。

Phase A 初始 freshness 配置为 max_ahrs_age_s=0.20、max_ctd_age_s=2.50、max_control_age_s=1.00、max_physics_age_s=0.20、max_gps_age_s=30.0。它们全部版本化；健康状态测试必须覆盖每个阈值的边界。物理 freshness 指最近一次物理传播成功、参数有效且输出有限的年龄，不表示车辆物理参数永久有效。

## 模型层

模型 A 为 Physics + GP。机体系固定为 x 前、y 右、z 下；R_n_b = Rz(yaw) @ Ry(pitch) @ Rx(roll)，表示 Body 到 NED。正 pitch 定义为抬头；在 yaw=roll=0 时，正 pitch 将 Body 前向速度映射到负 D（向上）分量。物理模型计算相对水速，GP 仅作为海流过程先验：

~~~text
c_next = c + alpha_gp * (1-rho_c) * (c_gp-c) + w_c
~~~

alpha_gp 等于 1 为域内、0.25 为边缘、0 为 OOD 或无 GP artifact。无 artifact 时模型 A 仍可运行：海流均值不被吸引、过程噪声提高、健康状态降级。

Phase A 使用可变积分步长 0 < dt_s <= 1.0，全部场景默认 dt_s=0.1。过程方程为 p_next = p + dt_s * (v_water_ned + c)，c_next = c + g * (c_gp-c)；alpha_gp=0 时 c 均值保持，不自动向零衰减。初始过程协方差为 Q=blockdiag(Qp,Qc)：Qp=dt_s*diag(0.04,0.04,0.01) m2，Qc=dt_s*diag(2.5e-6,2.5e-6,1.0e-6) (m/s)2 加 g2*Sigma_gp，加 Qood；Qood 在域内为零，在 OOD 为 dt_s*diag(2.5e-5,2.5e-5,1.0e-5) (m/s)2。水平海流时间常数为 300 秒，垂向为 60 秒；上述值均版本化为初始仿真参数并在后续航次校准。

模型 B 为 Physics + GP + XGBoost 单步三维速度残差。XGBoost 即时水平残差只允许由仿真真值或高精度参考轨迹训练；普通真实航次 GPS 端点只可用于航段校准，不得伪造为单步标签。

在线采用模型 A/B 的标准双模型 IMM-UKF。完整实现交互、预测、观测似然、模型概率更新、状态混合和带模型分歧项的协方差混合。潜航时 CTD 创新只能影响垂向似然；无 GPS/ADCP 时模型概率仅按转移矩阵缓慢演化。模型概率有下限和单次变化率限制。

TCN 不进入 UKF 单步传播。它只在预测层给出 30/60/120/300 秒累计位置残差分支。展示可显示混合均值；安全层必须保持 AB/TCN 分支风险，使用保守风险或并集包络。

## 预测契约

每条预测必须带：

~~~text
control_assumption: PLANNED_COMMANDS | ZERO_ORDER_HOLD | SAFE_FALLBACK
control_plan_version
control_plan_valid_until
~~~

控制序列不足时必须显式切换假设，不得静默以当前控制替代未来计划。

控制命令包含 effective_time_s、buoyancy_command、rudder_command_rad、target_depth_m、target_heading_rad。PLANNED_COMMANDS 以 effective_time_s 零阶保持，且仅可使用至 valid_until_s；从计划覆盖缺口开始，明确切换到 ZERO_ORDER_HOLD。SAFE_FALLBACK 在 Phase A 只能由外部提供控制序列，Phase A 不生成安全命令。快照记录实际使用的每个控制分段，而非仅记录初始请求假设。

预测协方差每一积分步传播完整 6x6 矩阵并加入过程噪声。P50/P90 水平包络使用 P_NE：半轴为 sqrt(chi2_2(q)*lambda_i)，方向取特征向量方位；chi2_2(0.50)=1.386294，chi2_2(0.90)=4.605170。深度包络使用 mu_D plus/minus z_q*sqrt(P_DD)，z_50=0.674490，z_90=1.644854。快照输出椭圆长短轴、方向及保守 P90 半径 sqrt(chi2_2(0.90)*lambda_max)，而不是仅输出单个未定义半径。

PSD 修正不直接对混合单位的原始协方差使用单一物理量 floor。先以状态尺度 S=diag(100 m,100 m,100 m,0.5 m/s,0.5 m/s,0.2 m/s) 归一化协方差，再采用 psd_eigenvalue_floor_normalized=1e-10 做特征值投影，最后映射回原始单位。快照诊断必须记录 psd_repair_applied、minimum_normalized_eigenvalue 和修正次数。

TCN 预测层混合权重 gamma_H 必须是确定性、可审计且版本化的函数。OOD 或历史不足时 gamma_H 等于 0。TCN 协方差与 AB 协方差相加仅作为初版独立性假设，必须用独立航次覆盖率校准；需要时加入交叉项或膨胀系数。快照必须保留 risk_ab、risk_tcn、risk_selected 和 risk_branch。

## 分阶段交付

阶段 A：无 ML 的模型 A、6 维 UKF、GP OOD 降级、30–300 秒预测和快照输出。

阶段 B：XGBoost 单步残差与双模型 IMM-UKF。

阶段 C：TCN 预测层、端点弱监督、可审计 gamma_H 和多分支预测。

阶段 D：WebSocket、Unity 可视化和多分支安全展示。

阶段 A 通过前不得进入阶段 B；阶段 B 与 C 分别独立比较各自相对基线的收益。
