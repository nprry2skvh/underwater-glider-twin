# 预测P2修复独立集成与推送

用户10-08对“独立集成工作区合并、重测、推送feature/underwater-glider-csv-twin，保留旧版本，不动主目录未提交内容”回复“对”。不授权覆盖脏文件、强推、替换Models或删除备份。

基线：远端目标91ba2da；本地主目录6142262；预测源27f8086（实现8dbf130，原最终验收3a21d39）。远端与源基于6142262各有11/22提交，不能直接将源指针推送覆盖远端运动改动。
独立树 `.worktrees/prediction-p2-integration`，分支codex/prediction-p2-integration；旧远端目标本地引用codex/backup-prediction-target-20261008=91ba2da，原pre-p2和pre-prediction运行包仍在digital-twin-stage01中保留。

## 工作步骤

- [x] 独立树远端基线Python/EditMode通过：Python11/11、Edit528/528。
- [x] 合并预测源，保留运动与预测的两套已验收改动；冲突记录具体取舍。
- [x] 合并结果Python/EditMode/PlayMode全量、Windows重建、默认Player烟测通过。
- [x] 保存验收证据、确认规则，正常推送HEAD到明确目标；不强推。
- [x] 核对远端提交、备份引用和主目录HEAD/未提交内容未被本次操作改变。

合并、审核发现修复、最终验收及正常推送均已完成；最终回执见文末。不把功能测试通过等同于实测精度提升。

主目录保护基线：HEAD6142262e101a2c8f3618a389b334a3aa18794a7c、index SHA256 BA4F318457CA09655A75536479C71DE996FF7888F506D8594ABA8F0A1A7A9958、tracked diff SHA256 A6740260F57BADC7D56623EFB1D9D086DB6FA546D1521BCE6012C198EE8952DB，176个status项；仅保存摘要，不保存diff中的私密内容。并行任务可能新增未跟踪文档，本轮不得改写。
merge-tree预检查有8个源码/测试冲突：Bootstrap、Geo映射、Dashboard、transform/visual driver、PlaybackModelTests、VisualizationTests、MotionDataConsistencyPlayModeTests；逐处保留双方行为后再验收。

## 冲突处理记录

- 首次合并因基线Unity导入改了CRLF/stat而拒绝；确认三个场景/ProjectSettings文件git diff为空后仅在集成树git add刷新，cached diff为空，未纳入产品修改。新树自动生成的3个测试.meta移除，合并使用源已跟踪的稳定GUID；不是删除用户文件。
- 实际merge包含8个源码/测试及HANDOFF add/add共9冲突。HANDOFF保留当前集成目标；源码分区并行协调，禁止代理并发启动Unity。
- GeoCoordinateMapper保留双方共享END/ENU米制映射及源新增DepthScale，供评分/导出反缩放使用。
- GliderTransformDriver保留共享连续采样与坐标/速度映射，采用源历史索引回溯而非远端访问缓存，避免回看缺坐标点时复用未来位置；两边有效坐标线性插值不变。
- GliderVisualController采用源缺坐标隐藏速度向量规则，保留原已有诊断舵面/无诊断复位逻辑；未扩大视觉算法。
- PlaybackModelTests保留所有已自动合并测试，统一源Frame(float elapsedSeconds)测试辅助入口，消除仅命名不同的TimedFrame重复。
- VisualizationTests保留双方回归合集，新增源缺坐标/诊断向量隐藏测试；MotionDataConsistencyPlayModeTests沿用源精确数值且允许独立/内嵌单位的readout断言，以兼容两种UI布局，并非放宽运动数值。
- 启动/UI两个文件由McClintock独立协调，主控制器只处理其他文件；实际全量测试仍待统一执行。
- 代理返回后控制器检查diff：Bootstrap保留源一次性LaunchRequestParser/无参数默认仿真/非法参数失败，保留远端严格UI根策略。Dashboard保留源布局和最小绑定、远端连续采样/null-safe/empty state/距离缓存刷新；独立单位节点与内嵌单位均明确显示。所有源码/测试冲突标记消除，索引全部标记解决。代理只做语法静态检查，行为结果由控制器运行，不宣称代理独立验收。
- 首次合并五门槛：Python58/58；Edit尚未执行测试，编译报CS0103，GliderTransformDriver引用TelemetryPositionUtility却缺Telemetry using。根因是远端移除旧无用using与源新增回溯依赖被自动组合；补回必要using后重新完整运行，不把编译失败说成行为回归已通过。
- 对git diff --cached --check指出的25个.meta空值尾随空格及Main两行做apply_patch机械清理，GUID和YAML含义不变；避免将远端已清理问题重新带入。后续验证结束再确认cached check。

## 合并后验收

补回using后完整重跑：Python58/58、EditMode525/525、PlayMode21/21；Windows重建成功，默认仿真Player烟测Exit=0/source=simulation，PNG存在。日志 `TestResults/integration-five-gates.log`、`TestResults/WindowsBuild.log`、`TestResults/DefaultSimulation-a852f08afce440c8b0f7fd7d4e91bd93.log`。此为控制器实跑，不等同于审核者独立运行或实測精度通过。
EditMode XML时间为2026-10-08 13:39:45Z–13:40:09Z，PlayMode为13:40:19Z–13:40:28Z。集成Runtime.dll SHA256为E6EC2A37FA5706E1755A9278843598E36C84EB2A17C1594A5EC1EB316C33D900；烟测PNG存在。git diff --cached --check通过；未解决冲突为空，Models与91ba2da完全相同。

一次只读集成审核Pasteur（01a11bbf-8a73-74a1-bd49-d490120f53a2）上轮因额度中断，无结论。10-09用户要求继续，已向同一代理恢复；快照TREE9cd4c04c7d2418c296c0c739f27f17f25d3a786c；范围为8冲突及相关调用链，不重复原P2审查，不并发Unity。此后只有HANDOFF/audit及Main两空值尾空格变化，源码不变。结束后裁决发现再推送。

10-09重新fetch远端仍为91ba2da；主目录HEAD/index/tracked diff三摘要与保护基线相同，主目录HANDOFF属并行任务，不改写。待确认审核和推送后补最终回执。

## 集成审核及修复裁决

Pasteur已完成双向静态审核：无Critical，两个Important/P2，结论With fixes；不是独立运行测试。I1为Bind遗漏详情登记与初始隐藏；I2为缺坐标分支重复清零有效诊断舵角。核对双方原版及现代码后确认，采用真实行为回归修复。Minor/P3为AssertReadout允许无单位裸数值，作为本次显示完整性验收补强收紧，不修改生产模型或运动阈值。执行步骤见 `docs/superpowers/plans/2026-10-09-prediction-integration-review-fixes.md`；沿已授权的集成收尾执行，不新增审核席位。

审核未裁决的逐项处理：

1. ML/训练/原P2正确性：保留既有独立审核与RED→GREEN记录，不重新全面审核；代价是本次集成审核不提供第二次算法结论。
2. 实艇真值/精度/部署：继续不验收、不替换Models；代价是不能承诺实测精度或提升。
3. 非冲突范围全量响应式布局/删改：沿用来源已验收版本，跑合并全量及默认Player；代价是不能代表所有分辨率交互验证。
4. Player交互/重载及新反例：控制器补真实EditMode回归并跑PlayMode与Player烟测；代价是仍非全面手工交互验收。
5. 推送/备份/主目录保护：由控制器用远端hash、旧DLL和主目录三摘要核对；代价是并行任务的独立改动不受本轮验收覆盖。

I1 RED：Edit528个中525通过、3失败，完整prefab有/无详情根均报YawValue初始仍enabled，最小绑定根仍active；`TestResults/integration-details-red.xml`及`.log`。不属于编译或夹具错误。绑定仅登记详情值/标签/单位和可选根，初始化不依赖完整数据回调；GREEN Edit528/528，`integration-details-green.log`。不重建或重排prefab，不修改连续数据采样。

I2 RED：Edit529中528通过、唯一新回归失败，缺坐标时左翼应10度而实际0度；`TestResults/integration-surfaces-red.xml`及`.log`。最小修复移除向量隐藏分支重复清零，舵面仍统一按诊断设置；新回归还验证后续无诊断时全部回中。单位断言只接受精确的独立值+单位，或带单位的内嵌值；独立数值可见时其单位必须可见。随后一次五门槛重跑同时作为I2 GREEN及最终门槛，不重复审核。

## 10-09 最终合并门槛

修复后五门槛实跑Exit=0：Python58/58、EditMode529/529、PlayMode21/21、Windows重建、默认Player Exit0/source=simulation。三个详情回归与非零舵角回归逐项Passed。Edit XML UTC时间为2026-10-08 18:31:25Z–18:31:47Z，Play为18:31:56Z–18:32:04Z（客户端日期10-09）。日志 `TestResults/integration-final-five-gates.log`；Player `DefaultSimulation-216a41637c514ec2b324677f8f2dc307.log`，同名PNG27260字节。最终Runtime.dll SHA256：65DB7AF791338A2B59DD2F69DE4085875D3F04496DF43F7C0A61781E5242C345。

控制器依据两项RED→GREEN及全量套件裁决Important已解决，Minor单位保护已补强，无延期项；并非宣称审核者重新批准或独立运行。最终源码较审核TREE仅增加详情登记、移除重复清零、对应测试及单位断言，没有扩大算法范围。Unity结束后清理Main唯一新增空值尾空格，YAML语义不变。

提交前再次fetch远端仍91ba2da；Models diff为空。主目录三摘要完全匹配保护基线。两旧运行包DLL再次验证保持8B3742A8…与AD725B07…，备份引用仍91ba2da。待提交merge、正常推送和远端回执；不调整主目录的本地目标分支。

## 合并与推送回执

10-09 02:35（Asia/Shanghai）正常推送成功：`91ba2da..0016ef2 HEAD -> feature/underwater-glider-csv-twin`，`ls-remote`核对目标为 `0016ef2312c155118cafb9664e0b25dab7ae9d20`。合并父提交为df6cce9与27f8086，验证原远端91ba2da和预测来源27f8086均为祖先，双方历史保留；未强推。

推送后主目录HEAD/index/tracked diff三摘要再次完全匹配基线，集成树干净、Models无修改、备份引用91ba2da仍在。主目录本地目标分支故意保持6142262，不能在其脏索引背后移动分支；未经另行授权不自动同步、stash或覆盖。根HANDOFF属于并行物理基线任务，本轮没有改写。来源与集成工作树及两个旧运行包均保留。

本回执仅修改文档，会正常提交并推送，不改变已验收的产品源码。最终回执提交哈希可由 `codex/prediction-p2-integration` HEAD和目标远端核对，不需重新运行五门槛。用户可运行集成树 `Builds/UnderwaterGliderTwin/UnderwaterGliderTwin.exe` 查看新版；主目录运行包未被本次替换。

回执88d3a19首次推送遇到连接重置，普通远端读取随后连接443失败；DNS正常，TCP443检查False。Windows系统现有代理已启用且监听，Git/进程未配置代理。仅对这次Git传递 `-c http.proxy=<已核验的系统代理>` 后，读取确认远端仍0016ef2，正常推送0016ef2..5947a5f成功，最终ls-remote匹配5947a5f8fae033320c9e0fd17a79ed9f01bb5aa0。未改全局网络配置、未降低TLS校验或强推。此经验记入隔离树LESSONS，不冻结端口为长期配置。

集成HANDOFF结构校验ready（991字符、单一目标/下一步、3里程碑）；最后保护检查主目录HEAD/index/diff仍匹配。文档回执及本次网络恢复记录与0016ef2产品源码完全相同，最终记录提交仍以分支HEAD及目标远端核对。
