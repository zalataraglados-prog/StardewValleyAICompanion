# StardewAI Code Structure Audit

Date: 2026-07-17

## Verdict

The solution-level decomposition is sound and the dependency graph is acyclic. The file-level decomposition is not yet sound: three orchestration files remain large enough to hide duplicate behavior and make handoffs risky.

## Project Boundaries

- `StardewAI.Contracts` is the shared schema boundary and has no project dependencies.
- `StardewAI.Core` contains deterministic planning, validation, ranking, and training logic and depends only on Contracts.
- `StardewAI.TransparentBridge` contains SMAPI/game-state readers and depends only on Contracts.
- `StardewAI.Backend` hosts the HTTP API and composes Core plus Contracts.
- `StardewAI.RuntimePrimitives` is a game-independent runtime state library. Its separate netstandard target prevents the net6 SMAPI harness from inheriting Core's JSON package graph.
- `StardewAI.RuntimeTestHarness` owns native game input/execution and depends only on Contracts and RuntimePrimitives.
- Training and smoke tools are outer-layer executables. Tests depend inward on the projects they verify.

No production project currently depends on a test project, tool project, or higher-level host.

## High-Priority Findings

### Runtime executor monolith

`tools/StardewAI.RuntimeTestHarness/ModEntry.cs` is 15,615 lines with approximately 408 methods and 41 nested types. It mixes HTTP transport, SMAPI lifecycle, input override, movement, farming, mining, volcano, combat, fishing, social actions, shipping, sleep, fixture setup, and result serialization.

Target decomposition:

- `ModEntry.cs`: entry point, lifecycle wiring, top-level request dispatch only.
- `RuntimeServer.cs`: listener, request parsing, pending request lifecycle.
- `PlayerInputController.cs`: button override, movement, facing, slot restore.
- `NavigationController.cs`: BFS/path following and dynamic obstacle checks.
- `Execution/FarmExecutionHandler.cs`
- `Execution/MiningExecutionHandler.cs`
- `Execution/VolcanoExecutionHandler.cs`
- `Execution/CombatExecutionHandler.cs`
- `Execution/FishingExecutionHandler.cs`
- `Execution/SocialExecutionHandler.cs`
- `Execution/ShippingExecutionHandler.cs`
- `Execution/RecoveryExecutionHandler.cs`
- `Fixtures/RuntimeFixtureBuilder.cs`
- Domain state records beside their owning handler instead of nested in `ModEntry`.

First move methods into partial files without behavior changes. Then introduce handlers around stable state clusters. Do not combine those two transformations in one commit.

### Action compiler monolith

`src/StardewAI.Core/Execution/ActionQueueCompiler.cs` is 4,804 lines with approximately 170 methods. Dispatch is now centralized, but parameter normalization, validation, routing, and step compilation remain in one file.

Split the existing partial class into domain files before introducing new abstractions:

- `ActionQueueCompiler.Validation.cs`
- `ActionQueueCompiler.Routing.cs`
- `ActionQueueCompiler.Farm.cs`
- `ActionQueueCompiler.Recovery.cs`
- `ActionQueueCompiler.Shop.cs`
- `ActionQueueCompiler.Fishing.cs`
- `ActionQueueCompiler.Mining.cs`
- `ActionQueueCompiler.Volcano.cs`
- `ActionQueueCompiler.Social.cs`
- `ActionQueueCompiler.Quest.cs`

Keep `ActionQueueCompiler.Dispatch.cs` as the only option-to-compiler registration point.

### Candidate evaluator monolith

`src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.cs` is 4,133 lines with approximately 125 methods. It still contains recovery, interaction, route repair, farming, machine, obstacle, quest, economy, and shipping candidate generation.

The existing standalone fishing, mining, social, and volcano builders are the correct pattern. Continue with dedicated builders for recovery, routing, farm maintenance, machines, economy, quest, and shipping. Keep the evaluator responsible only for safety gating, provider dispatch, compiler probing, and final availability aggregation.

## Medium-Priority Findings

- `DailyPlanCompiler.cs` is 1,863 lines. Split daily scheduling, continuation handling, time budgeting, and option binding after the candidate/compiler split stabilizes.
- `StardewAI.LiveTrainingLoop/Program.cs` is 1,834 lines. Move argument parsing, runtime client, episode persistence, and monitor output into classes; leave top-level startup in `Program.cs`.
- TransparentBridge adapter ownership is clear, but `MiningReadAdapter`, `MiningMonsterDropResolver`, `ShopAccessReadAdapter`, `FishingReadAdapter`, and `FarmReadAdapter` are each over 1,000 lines. Use domain subfolders and split rule resolvers from snapshot projection.
- The 30 PowerShell scripts are flat. Group by `deploy`, `training`, and runtime domain only after test paths and documentation are updated in the same commit.
- Several test files exceed 1,000 lines. Split by behavior surface and retain source guards only for negative safety constraints. Positive deterministic behavior should move to direct unit tests as runtime primitives are extracted.

## What Should Not Be Merged

- Bridge `ModEntry` and RuntimeTestHarness `ModEntry` are different SMAPI mods and should remain separate.
- Live execution and deterministic simulation should remain separate implementations with shared contracts.
- Ordinary mine, Skull Cavern, Quarry Mine, and Volcano planners should keep domain-specific rules. Only shared mechanics such as native heavy-hitter progress, movement, and combat input should be reused.
- `RuntimePrimitives` should remain game-independent; do not reference Core or SMAPI from it.

## Refactor Order And Exit Conditions

1. Split RuntimeTestHarness into partial domain files with no logic changes. Exit when the solution tests pass and runtime source guards retain the same assertions.
2. Extract input/navigation controllers. Exit when visible walking and hidden runtime smoke behavior are unchanged.
3. Split ActionQueueCompiler partial files. Exit when all compiler snapshots and 959 Core tests pass.
4. Extract remaining candidate builders. Exit when every registered candidate option has identical availability and parameter output for existing fixtures.
5. Split large bridge adapters and tests. Exit when transparent snapshot schemas and coverage reports are byte-for-byte or semantically equivalent.

Each slice should be one focused commit. Do not mix file movement with behavior changes.
