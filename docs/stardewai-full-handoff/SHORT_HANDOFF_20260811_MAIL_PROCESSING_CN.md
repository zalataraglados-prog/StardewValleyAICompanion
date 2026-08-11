# StardewAI 短交接：原生邮箱信件处理闭环

日期：2026-08-11

## 已完成

- `mail.process_letter` 已从显式 blocked 迁移为正式高层选项。
- 共享 `MailDirectiveParser` 同时供透明桥与 KnowledgeCompiler 使用，锁定 1.6.15 的
  179 封信、107 条 `%action`/`%item` 指令全部可分类，阻塞为 0。
- 透明桥读取原生顺序邮箱队列、玩家拥有的实际邮箱位置、保守附件容量和完整
  `LetterViewerMenu` 页面、附件、任务、特别订单及稳定身份。
- 候选按新快照逐段执行：跨图连接、邮箱接近、原生 `Mailbox` 交互、原生信件菜单完成。
  DailyPlan 复用既有 `move_to_tile`、`interact`、`close_menu`；没有第二套执行器。
- 运行时代码不直接写钱、配方、任务、特别订单、附件或最大体力，只发送原生输入并核对收据。
- 隐藏静默隔离矩阵 5/5：普通附件、即时金钱、制作配方、任务接受、星之果实溢出菜单。
  权威制品：`artifacts/runtime-mail-processing/runtime-mail-processing-20260811-221959/summary.json`。
- full 快照已重新捕获并原子安装：required 107、readable 90、contextual 17、blocking 0。
  KnowledgeCompiler 为 585/585、blocking 0。
- 当前对账：`115 registered / 177 semantic / 114 compiler-bound / 62 catalogued-blocked /
  41 five-gate / 28 training allowlist / 0 Product Executor`。

## 边界

- 打开信件前随机附件的最终身份由原生构造器决定，因此容量检查使用保守上界；背包不足时必须先走
  已有存储转移链，不允许赌溢出菜单或直接写背包。
- 该证据覆盖锁定原版 1.6.15。模组新增指令失败关闭。
- Harness 原生闭环不等于 Product Executor 已接入，也不代表长程策略训练完成。

## 下一步

闭合 `mining.use_elevator`。先证明普通矿井现有移动、入口交互、楼层转换、新快照重规划与战斗/清障
执行器可复用，再仅新增电梯菜单的透明楼层集合、合法目标选择、原生按钮输入和准确楼层收据。
普通矿井、火山矿井和金镰刀洞窟必须保持三种身份；不得复制任何一套矿井执行循环。
