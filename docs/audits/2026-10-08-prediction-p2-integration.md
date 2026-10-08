# 预测P2修复独立集成与推送

用户10-08对“独立集成工作区合并、重测、推送feature/underwater-glider-csv-twin，保留旧版本，不动主目录未提交内容”回复“对”。不授权覆盖脏文件、强推、替换Models或删除备份。

基线：远端目标91ba2da；本地主目录6142262；预测源27f8086（实现8dbf130，原最终验收3a21d39）。远端与源基于6142262各有11/22提交，不能直接将源指针推送覆盖远端运动改动。
独立树 `.worktrees/prediction-p2-integration`，分支codex/prediction-p2-integration；旧远端目标本地引用codex/backup-prediction-target-20261008=91ba2da，原pre-p2和pre-prediction运行包仍在digital-twin-stage01中保留。

## 工作步骤

- [x] 独立树远端基线Python/EditMode通过：Python11/11、Edit528/528。
- [ ] 合并预测源，保留运动与预测的两套已验收改动；冲突记录具体取舍。
- [ ] 合并结果Python/EditMode/PlayMode全量、Windows重建、默认Player烟测通过。
- [ ] 保存验收证据、确认规则，正常推送HEAD到明确目标；不强推。
- [ ] 核对远端提交、备份引用和主目录HEAD/未提交内容未被本次操作改变。

当前未作合并后验收、未推送。不把功能测试通过等同于实测精度提升。

主目录保护基线：HEAD6142262e101a2c8f3618a389b334a3aa18794a7c、index SHA256 BA4F318457CA09655A75536479C71DE996FF7888F506D8594ABA8F0A1A7A9958、tracked diff SHA256 A6740260F57BADC7D56623EFB1D9D086DB6FA546D1521BCE6012C198EE8952DB，176个status项；仅保存摘要，不保存diff中的私密内容。并行任务可能新增未跟踪文档，本轮不得改写。
merge-tree预检查有8个源码/测试冲突：Bootstrap、Geo映射、Dashboard、transform/visual driver、PlaybackModelTests、VisualizationTests、MotionDataConsistencyPlayModeTests；逐处保留双方行为后再验收。
