# 原全局日计划续行与集合控制模式

## 两个不同用途

| 显式参数 | 作物结束后的选择来源 | 要求/边界 |
| --- | --- | --- |
| `--continue-after-crop-teacher-run` | 原Stage One集合Teacher优先，必要时普通daily fallback | 原集合authority及原恢复/managed合同；保留旧用途 |
| `--continue-global-daily-plan-after-crop-teacher-run` | 原普通全局日计划/GoalResolver，不自动启用集合Teacher | 原managed owner、单人原生反馈/回执/保存、stop留存；全候选入口 |
| 均不启用 | 原校准/显式恢复的停止行为 | 不取得任何新续行权限 |

旧模式的依据是 `cfcac4674419ff95a06a9b3d8f3ab8000de4f3b9`
（2026-10-26，Continue collection control after crop teacher）及其原CURRENT_WORK/STATUS。
它是有意的集合控制模式，不是一个需要改掉的旧执行器bug。244真实记录131、145、
原生收杆/出货/日界等工作，但不能因此声称普通全局跨根选择已验证。

## 新入口

标准 `scripts/Invoke-ProductBootstrapCalibration.ps1` 增加尾部可选参数
`-ContinueGlobalDailyPlanAfterCropTeacherRun`，保持所有旧位置参数不变。
已有Main运行调用可以直接传同名CLI开关。参数与原
`-UseManagedCropTeacher -JointDailyCalendar -ArtifactRetentionMode stop`
一起用于R2当前共享日历路线；其余原保存、端口、反馈、留存与资源预算参数仍须提供。
这不是要求重启当前隔离现场或新建存档的命令。

新模式与旧集合续跑、standalone collection、require-source-schedule及require-Gourmand
互斥，防止在声称ordinary global时实际激活集合优先模式。原
`-CaptureGoldenWalnutFreshSaveEvidence` 单独采集来源仍可用，不把采集行为等同于
require完成或正式权限。初始化候选option/参数/kind/ID覆盖不允许；后续原编译器生成的
合法队列及其具体参数不因此被禁用。

新旧续行共同复用原业务边界：原episode完成、实际终局/睡眠已接受、原handoff落盘后
才释放owner及作物租约，之后再选择。普通维护夜/未接受日界不释放；原件改动或写入
冲突保留原owner，不能开始另一份。新模式避免原首日校准快捷退出，但不豁免原单次
尝试上限、完整owner业务、共享账本、保存或正式准入。机械维护仍走原owner/执行器，
不是每步新调用上游模型。新owner的控制次数由原dispatch重置。

报告/启动器summary只在新模式启用时增加
`continue_global_daily_plan_after_crop_teacher_run=true`，默认不增加false字段。
原run身份、独立artifact-run身份、保存槽与来源请求绑定规则保持。

## 验证及尚未完成的事

- 最终定向81项（26秒）及标准启动器64项AST/真实parser检查通过，零失败/跳过。
  原owner夹具覆盖新旧模式下接受终局释放、维护夜拒绝、未接受日界拒绝、证据变化、
  写入冲突与默认不续行；夹具不是新的真实游戏验收。
- 没有改变旧执行器、GoalResolver、DailyPlanCompiler、原native verifier、两项正式
  native blocker或报告九类gate；没有复制新Planner/Executor。
- 本块未启动新的多日游戏运行。当前仍使用244春15保存及236+244来源引用；不要恢复
  旧07de、重跑235或十三天前缀。选线先从新鲜观测交给原入口，再据真实结果绑定预算。
- **完整第5步未完成：** 仍须固定实际所选路线的来源、数量、期限、共享资源/日历及
  适用Day-0证据。旧crop dispatch默认8格是有界初始生产，不是100万收入能力证明；
  不得为过验盲改64格、强制某个候选、另造规划器或把本模式标记当成功证据。
- 完成实际路线及其原生缺口之后，才接#570同一Core正式准入核到报告/prepare/launch，
  再#569实验室。formal=false，原存档/回执/日志/local-data不进Git。
