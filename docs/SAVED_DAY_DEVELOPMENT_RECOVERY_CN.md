# 夏3保存点的隔离开发恢复

## 托管续跑的指定保存日上限（274）

显式的 `continue-crop-managed-owner` 或 `continue-live-crop-managed-owner`
可以使用 `--stop-after-managed-crop-saved-day <totalDay>`。它是控制器的可选
停止点，不改变计划、旧首日开关、候选编译、执行器或保存证明。日期必须在原
计划的剩余日之内。指定 41 表示允许完成 day 41（夏14），并在由同一个 owner
接受 day 41→42 的原生保存后退出，不派发 day 42 的动作。

未接受的睡眠、没有实际写盘的日期变化、已过去的日期或计划外日期不能记成
通过。规划数据不被重写。预期有界停止由
`crop_teacher_managed_target_day_saved_owner_retained` 报告，仍是完整循环尚未
收尾的退出；首收和正式训练不由这个停止原因推导。发现日期越界且没有接受
存档，记录 `crop_teacher_managed_target_day_crossed_without_accepted_save`。

实际夏4保留快照（day 31）与原计划只读重放：41 被允许为目标，30、54
均被拒绝。仓库外报告 `block274-saved-day-limit-real-inputs.json`，SHA-256
`f71051fa63da2dd743c31bb13da8a633919e02e054d221e117d8c175e0eec3f0`。
接口与原过夜相邻回归 68/68，零失败或跳过；包含真实写盘协议夹具的正例及
未写盘负例，TRX SHA-256 `492343e4fa3be97a68fd0d289215cd28edf6bd1830445ad093fc171edf727b93`。
此次没有启动游戏。继续夏4及以后仍需对应授权；不能把原夏3单日批准扩成多日。

## 272分段生产来源与已接受历史重开

`ReadRetainedJointProductionTraceAsync`复用现有接受记录读取器。父段只读到原
保存点41，用父owner原编号、Backend/journal及原生回执。子段保留自身编号，
开发加载不是其中一条原生动作；`DeveloperSavedBoundary`记录原接受/保存证书、
副本来源及复核/重绑定证据，父42–47不会进入新追踪。两段都保留原计划及哈希。
不把开发切换写成原`saved_content_readback`，不签发严格生产/终局证明，不训练。

实际夏4保留owner第一次重开揭示一个非空分支编号问题：旧legacy/plan检查早于
开发来源后缀；现先按已固定的原计划及来源构成完全相同的子分支ID，再由原索引
核对子操作。保留原有文件和ID，普通owner的格式和行为不动。

最终定向/相邻142项通过。真实271只读重开再次通过：父41项含原种植、子9项始于
实际加载帧并终于已接受夏4，743份输入哈希；不改运行头、不调用Backend、不
启动游戏。仓库外`block272-production-segments-verification.json` SHA
`61b9710a97c75161c128d775aaa35c70e97adbff488e884f0a1c5727b6e4421a`。
完整第5步、后续正式准入及训练未完成；按原顺序核对实际路线及其缺失证据，
而非再跑已证夏3。

## 271夏3单日隔离实机闭环通过

在仓库外独立目录暂存MSYS2 Mesa/WGL，从发布SHA核对归档、校验实际PE依赖。
游戏原SDL2.30.9支持用`SDL_OPENGL_LIBRARY`指定进程局部OpenGL；隐藏探针
实测llvmpipe OpenGL4.6，两种framebuffer扩展均可用，实际隔离游戏亦加载了
相同两个Mesa DLL。保持原Windows设备/驱动/注册表、原游戏安装和全局环境；
静音、隐藏、两核、低优先级。过程和包留仓库外`block271-process-local-gl`。

对新隔离的原41保存点副本实机加载夏3，保留首读与稳定读，完成原API副本账本
重绑定；原`continue-live-crop-managed-owner`成功接回原作物计划和已接受历史
进度。10轮、7次主阶段verified、9项owner接受：三批实际浇水4+2+2格，补水
一次，末项为从夏3到夏4的原生睡眠/保存。原owner逐批回执与真实水壶资源结果
核对通过；8株同位置蓝莓全部推进一天，下一收获尚需的浇水天数11→10。

夏4磁盘4文件指纹`54fb4a23522effaf85db0baa34829ec6a5955bed3dcd9619caeed9c9379b7218`
与已接受的原生保存证书一致。仓库外`block271-managed-global-loop/business-outcome.json`
SHA`b75decfb1b2a850f9460b594077a21c30bad04afd0d6f0347c9d59f4c9bd53d9`，含原
运行、接受链、回执、帧及真实存档核验。Main以`crop_teacher_managed_first_day_saved_owner_retained`
停止并退出2，是既有单日停机语义；业务完成依据是上述回执、生长与磁盘验证，
不是退出码。关停仅限本次三项隔离进程，原41存档、42–47原件和原账本486的
哈希经重核不变。没有发出夏4动作或重新归类旧47。

此证据仍是开发分支：不可用于正式训练或代替旧严格重载证明。下一缺口是既有
生产汇总的父前缀、显式开发加载和子操作记录的来源衔接，不重跑已证夏3，也
不以仅含子操作的trace冒称完整原生产生命周期。

## 270实机接续准备

同一Windows会话、游戏原`SDL2.dll`的隐藏窗口探针只见一个SDL显示输出，实际
renderer为`GDI Generic`，OpenGL `1.1.0`，无`GL_ARB_framebuffer_object`和
`GL_EXT_framebuffer_object`。这是当前会话不可用的直接证据，不等同认定
GTX1050禁用是唯一原因。探针没有启动SMAPI或改变设备设置。新实机未启动。

静态核对修正了恢复脚本遗漏的`continue-live-crop-managed-owner`子命令，调用
真实已编译Main入口离线验证：到达原同进程检查，并按预期拒绝无进程的测试
session，没有服务/动作/数据行。冻结Bridge/Harness和开发读回二进制沿用原SHA
逐个校验，不再因新owner代码变化误拒确实未改的游戏运行组件。

旧实际运行中，首份全量读和首次动作前全量读均在tick124；唯一变化字段为
`quests.daily_quest_offer`。已有映射先读底层字段，后读取任务描述，后续帧
才显示填充后的文本及对应指纹。原同进程比较拒绝这两份不同哈希；后帧哈希
与当次真实首个动作的before一致，按原持久字段比较内核也没有超出此前四项
已明确核对的分支差异。新接续保留首份原始帧，在世界就绪后取下一份真实帧
作为读回/重绑定来源，提交前另做同tick/哈希复核，不忽略字段或改写帧。

本机图形会话可用后再开新隔离包；未变更之前的269失败包、原存档或旧记录。
预检通过原文件/二进制/磁盘/端口检查，但报告`graphics_ready=false`。
仓库外脚本依次为`Invoke-Block267SavedDayRecovery.ps1 -OwnerSourceFix -AfterGraphicsRecovery -Launch`
与`Invoke-Block268RecoveredGlobalLoop.ps1 -OwnerSourceFix -AfterGraphicsRecovery -Run`。
原`--stop-after-crop-teacher-first-day`在显式开发来源下停于恢复日后的保存点；
旧Main可将该有界停机记为`incomplete`，不能按退出码判断生长结果。须核实新
owner的浇水与夏3→4真实保存回执，以及同一批作物的实际生长，才算本日闭合。

## 269接续实现与尚未完成的实机验证

已在现有owner补`DeveloperSavedSourcePath`，默认空，不建立第二套执行器。
来源文件显式标记developer-only/排除正式训练，固定原owner头及已接受41的
完整小证书前缀、复制后的加载快照/重绑定账本、人工开发读回复核、原API
marker请求/回复、独立save-root/slot及原生进程ID/路径/启动时间/可执行哈希。
加载时仍验证原计划与原始输入；新分支从保存点开始，操作记录从1独立编号，
mutation命名加来源哈希，不能复用原42–47或旧账本历史中的ID。

日界/收获进度复用原证书读取逻辑，默认路径及旧记录格式不变。重开仍走原
`CropTeacherManagedOwnerResume`；同进程接续绑定开发来源的真实进程，不冒充
已接受native reload。marker验证通过新增窄门面复用原内部校验器，完整GET
复用原reconciled readback。原计划首日不改；仅显式开发来源将既有首日停止
开关的停止起点设为恢复日，使本次验证只覆盖夏3。

Backend定向/相邻86项通过；真实留存源另行离线接入成功：继承2次历史日界，
选出`route_to_farm`，重新打开仍保留相同来源。它不验证活进程或作物生长。
新269夏3副本在存档加载前遭遇OpenGL图形设备初始化失败，无动作/新账本提交/
新保存。GTX1050另显示设备禁用代码22，未改设备/驱动，已询问用户；失败
日志和副本保留，原件未变。新失败游戏已精确关闭，无在跑的验证服务。

下一步仍为图形环境恢复后，以新隔离包只完成夏3真实浇水、生长、保存。
完整生命周期汇总尚须显式连接父前缀和开发加载来源；不能用子分支trace或
本次局部绿灯冒领完整Teacher/正式训练。以下267/268是原诊断过程，不覆盖
上述当前状态。

## 当前范围

2026-11-23恢复工作时，主机已经重启。11月18日保留的游戏、Backend和
Product进程均已消失，旧PID已可能被系统复用，不能继续按旧PID操作。
旧owner0148的42–47步是已经发生、但未进入最后保存文件的历史尾部；
不修改、删除或重标这些原件，也不把磁盘存档当成第47步之后的内存状态。

用户分别批准了两项限定操作：

- 旧47仅做一次开发补证，排除正式训练，不伪造缺失的拾取/工具调用观察。
- 从原夏3早晨保存点复制出新隔离分支，保留原42–47和原账本，不回退或覆盖。

最后完整保存点是原已接受41/native190，日期`1:summer:3`，4文件、
8,881,378字节，聚合SHA256：
`f1174f975b870d352739c0b6a4e71e9f6182f5f142f3a1d95f7693a5d45ea4e4`。
只从该日恢复，不重跑已接受的前13天。

## 开发分支与严格读回的区别

实际副本已经由原生游戏加载，复制文件哈希、实际save-root/slot加载日志、
原始快照和桥事件流均保留。新目录和WebSocket端点必然不同；本次还观测到
288处杂草ID变化及社区中心纸条可见性派生投影变化。

旧`OpenNativeLoad`的完整持久状态比较仍拒绝这些差异，未改旧规则来使其通过。
这次显式开发恢复仅核对指定差异：对象数量、位置和其余字段不变，变化的ID
限于已逐项列出的杂草种类；社区中心其余内容、捐赠/完成状态不变；经既有
native-load比较视图后，不得再有其他持久资源变化。原始快照未归一化、改写
或用作伪造的严格证书。`strict_saved_resource_readback_passed=false`及
`generic_native_reload_certificate_claimed=false`必须保留。

历史47补证与新分支加载复核是不同证据。新分支不能补造旧47的观察，也没有
为旧owner写普通accepted/native-result记录。旧47不进入新数据集或正式训练。

## 复用与实际进展

隔离Backend以保存点41的账本481为副本种子，调用原reservation-portfolio
commit API实际重绑定至真实加载快照，生成副本修订482。回复通过原
`ReservationPortfolioCommitEvidenceVerifier`，完整GET再由原
`StrategyLedgerEvidenceStore.RequireReconciledReadback`验证；不能新增原始JSON
逐字相等条件，也不能重发已经成功的mutation。原E盘账本486保持不变。

复用已验证263的Main/Backend/Product及264的Bridge/Harness，无新执行器或
替代规划器。新Main沿用原爷爷21分目标、全局日计划和联合日历；新数据输出
在E盘隔离目录，启用后续托管浇水的按需资源观察，`skip-training=true`。
仅本次启动把磁盘保留线从默认8192MiB改为4096MiB，保留原256次stop留存及
content-addressed gzip；这是当前主机空间设置，不是游戏业务准入标准。

主循环最终退出0：17轮、6次主阶段verified，完成原生地图连接、等待、菜单
关闭、返家清障和睡眠，真实保存夏3→夏4，保存SHA为
`beda0f477aba2111e984f73f271083e5e9cd4db1b596f4864572ac101d3e5bee`。
**这不是完整生产恢复通过。** 原始回执中没有浇水，8株作物的生长阶段/阶段
天数均未推进。恢复脚本只恢复账本，没有接回原作物owner；机械维护按设计
不在自主候选池，不能为修恢复把它改成模型候选或另造执行器。本诊断分支
整体排除正式训练，原CLI成功报告保留，另记`business_exit_verified=false`。

下一修复是显式保存点来源接到既有owner：保留原不可变作物计划与已接受
历史保存/收获进度，独立命名新分支的操作ID，绑定真实新进程和当前来源。
复用原联合日历、维护编译和执行链；不能仅复制资金/作物账本就进入普通
冷启动循环。先定向静态/相邻回归，再只跑复制的夏3，不重跑前13天。

授权、原始日志、失败记录和实际进程身份位于仓库外
`I:\StardewAI-Collab\resident-refill-20261108`的267/268工件和协作板。
新游戏和服务已在实际保存后关闭，旧PID不能复用。本次不是完整Teacher21、
正式训练或第5步整体收口；后续仍为第5步→#570→#569。
