# Student / Teacher Provider 接口要求（Canonical Requirements）

> 本文件只规定可替换性、权限边界和验收要求，不规定 API 形状、进程模型、序列化格式、模型架构、训练框架、损失函数或超参数。具体设计由实现者 / Codex 通过实验和工程约束决定。

## 1. 目标

StardewAI 的策略学习层必须允许第三方在不修改确定性游戏执行底座的前提下，独立替换：

- Student provider；
- Teacher provider；
- 二者的数据来源或模型实现。

运行时不得假设 provider 一定来自当前仓库内置实现，也不得要求 Student 与 Teacher 必须成对使用同一种实现。

## 2. 固定权限边界

无论 provider 如何替换，下列权威继续属于 StardewAI 确定性核心：

- authoritative state / candidate construction；
- hard constraints、权限、reservation、不可逆操作门；
- Planner / Compiler；
- Product Executor；
- fresh native verification / receipt；
- 正式 evidence / coverage admission。

Student provider 只能在核心提供的合法候选范围内返回策略偏好结果；不能新增合法性、绕过约束或直接执行游戏动作。

Teacher provider 只能提供监督、重标、比较或不可满足结论；不能以 Teacher 身份直接改写游戏状态或绕过正式 evidence gate。

Teacher 必须允许被显式禁用；非 Teacher 路线不得因缺少 Teacher provider 而被迫调用内置 Teacher。

## 3. Provider 可替换性要求

接口设计必须满足：

- Student 与 Teacher 可以独立替换、独立版本化、独立启停；
- provider 实现与来源对核心透明，核心只依赖稳定合同；
- provider 不得通过隐藏共享状态要求绑定到当前内置 Student / Teacher；
- provider 输入只能包含其角色被授权看到的信息；
- provider 输出必须能被核心验证、归因和拒绝；
- provider 版本、实现身份和关键输入/输出必须进入可重放 provenance；
- 接口或能力不兼容时必须 fail closed，不能静默降级成另一种语义；
- 正式训练、评测和运行时必须能够明确记录实际使用的 provider；
- 替换 provider 后，既有 Planner / Compiler / Executor / native verifier 不应需要随 provider 重写。

本文件不规定上述要求应通过库接口、进程边界、RPC、文件协议或其他方式实现；由 Codex 设计并给出理由。

## 4. 开训前模型定型实验要求

达到正式训练 coverage / reconciliation 门后、formal training manifest 生成前，Codex 必须基于冻结数据完成模型定型实验。

实验必须：

- 自行决定需要比较的模型方案、结构和超参数搜索方法；
- 把当前实现作为可比较基线，但不得被审计文档预设最终模型族；
- 使用 validation 选择方案，保持 test 独立；
- 给出唯一推荐的正式 Student 配置，或明确证明现有基线仍应保留；
- 报告足以复现实验的代码、数据、随机性、配置和结果身份；
- 证明推荐方案在策略质量、稳定性、资源成本和可部署性之间满足项目要求；
- 验证所选 Student 可通过本文件定义的可替换 provider 合同运行。

审计文档不预设网络类型、层数、宽度、embedding、loss、optimizer、learning rate、batch、量化或其他模型细节；这些结论必须由 Codex 实验产生。

## 5. 部署约束

训练允许使用 RTX 5070 Laptop GPU。

正式产品推理必须：

- 不把 CUDA、独立 GPU 或训练机硬件作为必要条件；
- 能在《Stardew Valley》最低配置级 CPU / 图形环境上运行；
- 在游戏、SMAPI、StardewAI runtime 与实际 Student 同时运行时，整机内存预算不超过 **8 GiB**；
- 不因替换 Student / Teacher provider 而提高确定性核心的最低运行要求，除非未来产品规范显式变更。

Codex 自行设计基准、采样方式和具体性能阈值，并在模型定型报告中证明推荐实现满足该约束。

## 6. 验收

开训前至少必须证明：

1. provider 合同已经实现并版本化；
2. 内置 Student 可通过该合同运行；
3. 一个与内置实现不同的替代 / mock Student 能在不修改确定性核心的情况下接入并完成合同级测试；
4. Teacher 可独立替换或禁用，且不会改变硬权威边界；
5. provider 身份与输出能够被正式 provenance / manifest 绑定；
6. Codex 已完成模型定型实验并提交可复现结论；
7. 所选正式 Student 通过低配 / 8 GiB 整机部署约束。

这些要求全部满足后，才允许进入 formal training manifest 与 training-ready preflight。
