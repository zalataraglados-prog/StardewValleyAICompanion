# 原生对象交互架构收口交接（2026-08-29）

## 本轮范围

针对 `world.rotate_house_plant`、`world.play_singing_stone`、
`farming.collect_slime_ball`、`animals.withdraw_feed_hopper_hay`、
`animals.collect_auto_grabber_contents` 完成 issue #88 的增量架构迁移。

## 已完成

- 候选层统一安全物品上下文和稳定相邻站位选择；各对象保留独立可用性语义。
- 编译层统一精确目标投影、站位和安全槽位外壳；各对象保留独立参数与校验。
- 运行时统一移动跟随与破坏性陷阱判断；Slime Ball 不再维护第二套移动循环。
- 五类活动状态归入 `NativeObjectInteractionDomainState`，根 `ModEntry` 只负责一次 tick/reset/is-active 委派。
- `TrainingExecutionRequest` 新增 `native_object_execution_payload.v2` 强类型联合载荷；入口继续接受无 v2 载荷的 v1 请求。
- v2 入口严格校验 schema、动作 kind、恰好一个投影以及投影和 kind 一一对应。
- 五个能力声明迁入单一 seed，每个 option id 在能力源中只声明一次。
- 机器候选实现按常规、输入、预测职责拆为三个 partial 文件，拆分前后主体逐行一致。
- NuGet 依赖固定并生成 lock 文件；新增纯逻辑 CI；知识编译器支持固定生成时间。

## 架构边界

共享层只承载真正相同的机械能力：读取安全槽、稳定选站位、BFS 移动、陷阱检查、
请求外壳和状态域生命周期。House Plant 的空手旋转、Singing Stone 的声音/RNG、
Slime Ball 的掉落守恒、Feed Hopper 的筒仓守恒、Auto Grabber 的容器转移仍是五套独立契约与回执。

后续动作应按同一方式逐域迁移，不得一次性把未触及动作塞进通用对象执行器，也不得保留并行旧循环。

## 验证

- Core：1858/1858 通过。
- Backend：132/132 通过。
- Release solution build：0 warning，0 error。
- House Plant：隐藏 E 盘 8/8 通过，`runtime-house-plant-20260829-001431`。
- Singing Stone：隐藏 E 盘通过，`runtime-singing-stone-20260829-001624`。
- Slime Ball：隐藏 E 盘通过，`runtime-slime-ball-20260829-001854`，预测/实得 Slime 11、Petrified Slime 0。
- Feed Hopper：隐藏 E 盘通过，`runtime-feed-hopper-20260829-001943`，筒仓 10→2、背包 0→8。
- Auto Grabber：隐藏 E 盘通过，`runtime-auto-grabber-20260829-002033`，转移 2 栈/5 件且容器清空。

首次 Slime Ball smoke 暴露 fixture 异步传送竞态：fixture 已排队传送时脚本固定等待 500ms，可能仍读取 Farm。
脚本现改为在 30 秒内等待透明桥真正发布 Slime Hutch 投影；动作门禁没有放宽。
