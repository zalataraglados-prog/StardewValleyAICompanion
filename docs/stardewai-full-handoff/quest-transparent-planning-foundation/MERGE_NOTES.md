# Quest Semantic Merge — MERGE_NOTES.md

Merge of accepted quest transparent-planning implementation (`51dae9d`)
into current-main snapshot (`3f1ea34`).

## Shared-file preservation checks

Every shared file was edited surgically — only quest-specific additions
were applied; all existing current-main behavior is preserved:

| File | Preserved behavior |
|---|---|
| `ActionQueueContracts.cs` | All envelopes, refs, step types, audit, social plan — unchanged. Added `QuestPlanEnvelope` and `quest_plan` property. Added `using StardewAI.Contracts.State`. |
| `ProgressState.cs` | `QuestProgressRef`, `CompletedQuestProgressRef`, `CommunityCenterProgressRef`, `MuseumProgressRef`, `CollectionsProgressRef`, `PerfectionProgressRef`, `GoldenWalnutProgressRef` — unchanged (verified before/after). Added new DTOs: `PerTypeQuestFields`, `PerTypeObjectiveFields`, `SpecialOrderRewardProgressRef`, `SpecialOrderDonatedItemRef`, `QuestCandidateRef`, `QuestCompilerEnvelope`, `QuestCompilerEvidence`, `QuestProgressSnapshot`. Extended `SpecialOrderProgressRef` (added participant/lifecycle fields, `SpecialRule`, `IsIslandOrder`, donated items, rewards) and `SpecialOrderObjectiveProgressRef` (added `RuntimeType`, `FailOnCompletion`, `Complete`, `PerTypeFields`). |
| `ActionQueueCompiler.cs` | All route repair, movement, farming, fishing, mining, recovery, social, menu/interact/buy validators, step compilers, and plan-to-action pipeline — unchanged. Added `ValidateQuestAdvancePlan`, `CompileQuestPlan` methods. Added `quest.advance` validation call and `QuestPlan` to `NormalizedCommand` output. |
| `TimeBudgetValidator.cs` | All executor models, estimation rules, assumption lookups — unchanged. Changed `quest.advance` estimate from `Fixed(120, "quest_rule.v1")` → `Unknown("quest_duration_unknown_until_route_and_native_executor_timing")` per accepted final semantics (`-1` unknown cost). |
| `CandidateOptionAvailabilityEvaluator.cs` | All farm, shop, machine, interact, route, recovery, fishing, mining, social, economic candidate builders, gate-blocking logic, compile probes, `IsExecutorEnabled` — unchanged. Added `QuestCandidates()` method, `QuestCandidateGateBlockingReasons()`, `quest.advance` dispatch in `EventCandidates()` and `EventCandidateGateBlockingReasons()`, `quest.advance` in `IsPreviewOnly()` and `ExecutorDisabledReason()`. |
| `OptionRegistry.cs` | All 27 option registrations — unchanged except `quest.advance` (added state factors: `quests.special_orders`, `quests.completed_special_orders`, `quests.accepted_special_order_types`, `quests.mail_received`, `player.location_id`, `world_progress.community_center`, `world_progress.achievements`; added expected effects: candidate+envelope; added safety constraint: `quest_native_executor_not_implemented`). |
| `ProgressReadAdapter.cs` | `WorldProgressReadAdapter` (community center, museum, perfection, golden walnuts, collections) — fully preserved. `ProgressQuestReadAdapter` refactored to delegate to `IQuestProgressMapper`/`ReadQuestProgressMapper`; old inline mappers removed. `ReadActiveQuests`, `ReadCompletedQuests`, `ReadSpecialOrders` now use mapper. |
| `ProgressContractsTests.cs` | All 5 original serialization tests preserved. Added ~40 new quest/special-order contract tests covering: `QuestCandidateRef`, `QuestCompilerEnvelope`, type-9 disambiguation, per-type fields, identity binding, malformed/mismatched rejection, donated items, lifecycle fields, `selected_objective_index`. **Post-merge fix (1)**: restored omitted direct `ReadQuestProgressMapper` tests (18 tests), `QuestPlanEnvelopeSerializesSelectedObjectiveIndex`, `ClassifyIslandByTags`, and `UnknownTestObjective`/`UnknownTestReward` inner classes from accepted source commit `51dae9d`. **Post-merge fix (2)**: restored five named accepted tests: `QuestProgressMapperInterfaceExistsAndAdapterUsesIt`, `ReadQuestProgressMapperIsConcreteImplementation`, `QuestProgressRefSerializesRuntimeTypeAndPerTypeFields`, `SpecialOrderObjectiveRewardProgressRefsSerializeRuntimeType`, `QuestCandidateRefSerializesBlockedDiagnosticsAndUnknownCost`, plus added `using StardewAI.TransparentBridge.Adapters`. |
| `TrainingExecutionContractTests.cs` | All 8 original training execution contract tests preserved. Added `QuestPlanEnvelope` and `NormalizedCommand.quest_plan` serialization tests. |

## New quest-only files added

- `src/StardewAI.Core/OptionRegistry/QuestCandidateBuilder.cs` — builds ordinary and special-order candidates; categorizes next actions; builds compiler envelopes with live evidence.
- `src/StardewAI.TransparentBridge/Adapters/IQuestProgressMapper.cs` — interface for mapping game-object quest state to contract DTOs.
- `src/StardewAI.TransparentBridge/Adapters/ReadQuestProgressMapper.cs` — sealed concrete mapper (no mutating/lazy getters, no `SpecialOrder.IsIslandOrder()` call).

## Accepted final semantics applied

| Rule | Applied |
|---|---|
| No mutating/lazy quest getters | Mapper reads `quest._questTitle`, `quest.id.Value` etc; no lazy/trigger evaluation. |
| No `SpecialOrder.IsIslandOrder()` call | `MapIsIslandOrder` uses `DataLoader.SpecialOrders` + tag check instead. |
| `-1` unknown costs | `QuestCandidateRef.TimeCostUnknown`/`EnergyCostUnknown` = `true`; `TimeBudgetValidator` uses `Unknown()` not `Fixed()`. |
| Executor-blocked event availability | All quest candidates set `Available = false`, block reasons include `quest_native_executor_not_implemented`. |
| Stable candidate/objective identity | Candidate IDs use `quest:{questId}:{RuntimeType}` and `special_order:{questKey}`. `SelectedObjectiveIndex` propagated. |
| Null donated-item observability | `MapDonatedItem(null)` returns `IsNullEntry = true`. |
| Direct mapper tests | `ReadQuestProgressMapper` tests exercise real `Quest`, `ItemDeliveryQuest`, `SlayMonsterQuest`, `CollectObjective`, `DonateObjective`, `MoneyReward` etc. |
| Fully-qualified `QuestDuration.Week` | Test fixture uses `StardewValley.GameData.SpecialOrders.QuestDuration.Week`. |

## Static scan results

- **Conflict markers**: NONE found in any `.cs` file.
- **Forbidden `IsIslandOrder()` calls**: NONE found in any `src/` file.
- **`quest.advance` option**: Properly registered with expanded state factors and `quest_native_executor_not_implemented` constraint.

## Unresolved static risk

None identified. All quest code is gated behind the `quest_native_executor_not_implemented` block reason, so the quest path is never executed at runtime. The bridge mapper (`ReadQuestProgressMapper`) has full coverage of ordinary quest subtypes and special order objectives/rewards; unknown subclasses fail closed (`Available = false` with descriptive `UnavailableReason`).

## Post-merge fix (3): missing `StardewValley.SpecialOrders` namespace imports

`ProgressContractsTests.cs` uses `OrderObjective` (line 1521) and `OrderReward` (line 1525) but was missing the corresponding `using` directives. Added:

```
using StardewValley.SpecialOrders.Objectives;
using StardewValley.SpecialOrders.Rewards;
```

Fixes CS0246 for both types. No other changes.

## Post-merge fix (4): add ModBuildConfig unit-test project configuration

`StardewAI.Core.Tests.csproj` now references `Pathoschild.Stardew.ModBuildConfig` v4.* (`PrivateAssets="All"`) and sets:
- `EnableGameDebugging=false`
- `EnableModDeploy=false`
- `EnableModZip=false`
- `BundleExtraAssemblies=All`

This provides game assembly compile references (XNA/MonoGame/StardewValley) that are normally resolved transitively by mod projects but were missing because the `TransparentBridge` reference carries them via `PrivateAssets=All`. Preserves all existing test framework references and project references.

## Post-merge fix (5): test-source corrections after real-build reachability

The real build reached the quest test source and found three remaining test-only errors in `ProgressContractsTests.cs`. Applied surgical corrections to the test file only:

1. **Missing imports**: Added `StardewAI.Core.OptionRegistry`, `StardewValley`, `StardewValley.Quests`, `StardewValley.SpecialOrders` (preserved existing `Objectives`/`Rewards` imports).
2. **`Assert.Contains` target correction** in `ObjectivePerTypeFieldsJsonPropertyNamesMatchBridgeKeys`: final two assertions now inspect `slayJson` (serialized string) instead of the DTO `slayObj`.
3. **Reflection for internal method**: `ClassifyIslandByTagsIdentifiesIsland` uses `BindingFlags.NonPublic | BindingFlags.Static` via `GetMethod` + `Invoke` because `ReadQuestProgressMapper.ClassifyByIslandTags` is intentionally internal and the test assembly is not a friend assembly. All five original assertions preserved.

No production code, project files, or other test methods were changed.

## Post-merge fix (6): three bounded semantic fixes for focused test failures

Applied the three fixes specified in `QUEST_RUNTIME_TEST_FIX.md`:

1. **`ReadQuestProgressMapper.ResolveRuntimeClass`** (`src/StardewAI.TransparentBridge/Adapters/ReadQuestProgressMapper.cs:377`): Added early `if (quest.GetType() == typeof(Quest)) return "Quest"` guard before the switch, so the base `Quest` runtime type returns `"Quest"` instead of falling through to `FullName` (`StardewValley.Quests.Quest`). The default branch still preserves full CLR type names for unknown/modded subclasses.

2. **`QuestCompilerEnvelope.LiveEvidence`** (`src/StardewAI.Contracts/State/ProgressState.cs:507`): Added `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` so the `live_evidence` property is omitted from JSON when null. Property name and non-null output are preserved.

3. **Donated item test fixture** (`tests/StardewAI.Core.Tests/ProgressContractsTests.cs:1382`): Replaced `new StardewValley.Object("70", 5)` with parameterless constructor, then set `ItemId = "70"` and `Stack = 5` to avoid `ResetParentSheetIndex` failure outside initialized game content. All assertions for `"70"`, `"(O)70"`, stack 5, and null-entry distinction are preserved.

No other files or tests modified.
