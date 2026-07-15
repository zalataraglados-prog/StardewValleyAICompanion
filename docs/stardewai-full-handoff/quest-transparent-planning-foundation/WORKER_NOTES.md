# Worker Notes

## 2026-07-14 Quest Transparent Planning Foundation (Resolve Controller Review Items 30-38)

### Item 30 — Base Quest handling
- `ReadPerTypeQuestFields` now has an explicit `Quest` base case: `case Quest _ when quest.GetType() == typeof(Quest)`.
- Only truly unknown derived classes hit the `default` branch with `unsupported_subclass` reason.
- `IsBaseQuest` is set only when `quest.GetType() == typeof(Quest)`.

### Item 31 — Incomplete objective index binding
- `CategorizeSpecialOrderNextAction` now returns `selectedObjectiveIndex` via `out` parameter.
- `BuildSpecialOrderCandidate` uses the selected index to read target/count/fields from the same objective row that determined the action category.
- Cross-wiring of evidence between objectives is eliminated.

### Item 32 — Type-9 decompile-backed next-action categories
- `CategorizeOrdinaryNextAction` case 9 now delegates to `CategorizeType9NextAction` with decompile-backed results:
  - `ItemHarvestQuest`: `"harvest_items"`
  - `LostItemQuest` (item not found): `"find_lost_item"`; (found): `"return_lost_item_to_npc"`
  - `SecretLostItemQuest` (not found): `"find_secret_lost_item"`; (found): `"return_secret_lost_item_to_npc"`
  - Unresolved type-9: `"type9_ambiguous"` with `type9_ambiguous_fields` diagnostic.

### Item 33 — Empty specialRule
- Changed from `string.IsNullOrWhiteSpace(order.SpecialRule)` to `order.SpecialRule is null`.
- Empty string `""` is treated as a valid direct value; only `null` means unavailable.

### Item 34 — Participant/reward-claim mappings
- `SpecialOrderProgressRef` now has `Participants`, `SeenParticipants`, `UnclaimedRewards` as `Dictionary<long, bool>` with deterministic ordering by key.
- `DonatedItems` is `SpecialOrderDonatedItemRef[]` with `ItemId`, `Stack`, `Quality` (no donor-ID field; donation provenance via IsNullEntry).
- Bridge adapter populates all four from `NetLongDictionary`/`donatedItems` collections.

### Item 35 — Special-order lifecycle/randomization fields
- `SpecialOrderProgressRef` now includes: `PreSelectedItems` (Dictionary), `SelectedRandomElements` (Dictionary), `GenerationSeed`, `ReadyForRemoval`, `ItemToRemoveOnEnd`, `MailToRemoveOnEnd`.
- All populated from direct `NetStringDictionary`/`NetInt`/`NetBool`/`NetString` fields in the bridge.

### Item 36 — Title/description/objective without loading
- Removed all calls to `quest.questTitle`, `quest.questDescription`, `quest.currentObjective` getters.
- Reads `_questTitle`, `_questDescription`, `_currentObjective` public backing fields directly.
- `TitleAvailable`/`DescriptionAvailable` indicate cache population without triggering loading.

### Item 37 — Lifecycle/reward fields for ordinary quests
- `QuestProgressRef` now includes: `RewardDescription`, `ShowNew`, `Destroy`, `NextQuests`.
- `PerTypeQuestFields` now includes: `Reward`, `TargetMessage` for subclass quests with direct fields.
- Bridge populates all from their `NetString`/`NetBool`/`NetStringList` fields.

### Item 38 — Production compiler path with live candidate reconstruction
- `ActionQueueCompiler.CompileQuestPlan` now:
  - Reconstructs candidates from live `quests.active_quests` and `quests.special_orders` snapshot JSON.
  - Requires at least one supplied identity (`candidate_id`, `quest_id`, or `quest_key`).
  - Filters candidates by every supplied identity, rejecting missing (not found), ambiguous (multiple matches).
  - Cross-checks every supplied parameter (`runtime_type`, `next_action`, `target_npc`, `target_location`, `item_id`, `target_count`, `current_count`) against the matched live candidate.
  - Emits specific mismatch block reasons.
  - Populates envelope only from the matched live candidate (never from untrusted action parameters).
  - Attaches `LiveEvidence` with the matched `QuestCandidateRef` and raw snapshot arrays.
- `QuestCandidateBuilder.BuildCompilerEnvelope` updated to filter by all supplied identities, reject ambiguous/absent instead of defaulting.

### Covered Fields Justifications
- `questDescription` getter is excluded because the backing field `_questDescription` is a public string read directly.
- `targetMessage` on `FishingQuest`/`ItemDeliveryQuest`/`SlayMonsterQuest` are plain `string` fields; `ResourceCollectionQuest` uses `NetString`.

## 2026-07-14 Quest Transparent Planning Foundation Items 39-45

### Item 39 — Quest.modData and obsolete_completionString
- `QuestProgressRef` gains `ModData` (Dictionary\<string,string\>) and `ObsoleteCompletionString` (string).
- Bridge adapter reads `quest.modData.Pairs` ordered by key; reads `quest.obsolete_completionString` directly.
- Determistic ordering proven by ordered `OrderBy(pair => pair.Key, StringComparer.Ordinal)`.

### Item 40 — Cached-value availability
- `TitleAvailable` changed from `quest._questTitle is not null` to `quest._questTitle is not null && quest._questTitle.Length > 0`. Empty cached titles now fail closed.
- `DescriptionAvailable` similarly checks both non-null and non-empty.
- New `CurrentObjectiveAvailable` field emitted alongside `current_objective`.

### Item 41 — Pure raw-facts-to-contract mapper
- `ReadQuestProgressMapper` extracted implementing `IQuestProgressMapper`. All mapping logic moved there.
- `ProgressQuestReadAdapter` accepts `IQuestProgressMapper` via constructor (defaults to `ReadQuestProgressMapper`).
- Direct mapper tests: interface contract test, concrete instantiation test.
- `ProgressContractsTests` continue to test the candidate builder pipeline.
- **Correction:** The mapper accepts live game classes (`Quest`, `SpecialOrder`, etc.) and is not a "pure" DTO-to-DTO mapper. It is a side-effect-free direct-field reader for game types. Tests construct in-memory game instances and exercise the exact mapping methods used by the adapter, but require game DLLs at compile/runtime. The mapper makes no probe/mutator/reload calls on game classes.

### Item 42 — Documentation update
- `evidence.md` updated with Items 39-45 evidence section.
- `transparency-coverage.md` updated with 10 new rows for mod_data, qualified_item_id, selected_objective_index, malformed-numeric rejection.
- Resolution table extended with Items 39-45.

### Item 43 — Null-safe donated items
- `order.donatedItems` may contain null entries after `OnFail` (decompiled evidence at SpecialOrder.cs:192-195).
- Bridge adapter now uses `Select((item, _) => item is not null ? ... : zeroed ref)`. Null entries produce `SpecialOrderDonatedItemRef` with empty ItemId, zero stack/quality.
- `QualifiedItemId` added to `SpecialOrderDonatedItemRef` alongside raw `ItemId`.
- `ModData` (Dictionary\<string,string\>) added to `SpecialOrderDonatedItemRef` from `item.modData.Pairs`.
- WORKER_NOTES.md corrected: donated items have no direct donor-ID; claim removed.

### Item 44 — Selected objective index end to end
- `QuestCandidateRef.SelectedObjectiveIndex` emitted from `BuildSpecialOrderCandidate`.
- `QuestCompilerEnvelope.SelectedObjectiveIndex` and `QuestPlanEnvelope.SelectedObjectiveIndex` populated from matched candidate.
- `CompileQuestPlan` cross-checks model-supplied `selected_objective_index` against live candidate.
- `BuildCompilerEnvelope` validates via `requestedObjectiveIndex` with malformed/mismatch rejection.
- Tests added: candidate serialization, builder population, envelope serialization, stale index rejection.

### Item 45 — Malformed numeric parameter rejection
- `BuildCompilerEnvelope`: `requestedTargetCount`, `requestedCurrentCount`, `requestedObjectiveIndex` now validated with `int.TryParse`. Malformed values produce `quest_target_count_malformed`, `quest_current_count_malformed`, `quest_selected_objective_index_malformed`.
- `CompileQuestPlan`: same three numeric fields guarded by `string.IsNullOrWhiteSpace` before `int.TryParse`. Malformed values block with explicit reason instead of silently skipping comparison.
- Tests added: malformed target count, malformed current count, malformed objective index, mismatched counts, mismatched objective index, ambiguous identity.

## 2026-07-14 Items 46-48 — Direct Mapper Tests, IsNullEntry, Static Closeout

### Item 46 — Direct ReadQuestProgressMapper method tests
- Added direct mapper method tests that construct safe in-memory game class instances (Quest, ItemDeliveryQuest, SlayMonsterQuest, ItemHarvestQuest, LostItemQuest, SecretLostItemQuest, SpecialOrder, all nine objective subclasses, all six reward subclasses, two unknown subclass types).
- Tests populate direct Netcode fields (`.Value` properties) and plain fields directly; no Game1/content access in test methods.
- `MapIsIslandOrder` split: `ClassifyIslandByTags` is `internal static` and directly testable without Game1.content. `MapIsIslandOrder` delegates after reading DataLoader. Tests added for island/non-island/null/empty tag classification.
- `MapDonatedItem(Item?)` extracted as a public method on `IQuestProgressMapper` and `ReadQuestProgressMapper` for per-item donated-item testing without full order mapping.

### Item 47 — IsNullEntry flag
- `SpecialOrderDonatedItemRef.IsNullEntry` (bool, `is_null_entry` JSON) added.
- `ReadQuestProgressMapper.MapDonatedItem` sets `IsNullEntry = true` for null input, `false` for real items.
- `MapSpecialOrder` delegates to `MapDonatedItem` for the donated items array (ordering semantics preserved).
- Tests verify one null entry and one real entry through `MapDonatedItem`, confirming IsNullEntry distinguishes them.

### Item 48 — Static-only closeout
- No build, test, or runtime commands executed.
- All changes are static file edits and git operations only.
- WORKER_NOTES.md corrected: the mapper accepts live game classes and is not a pure DTO mapper; tests exercise the exact adapter-used methods but require game DLLs. Donor-ID claim corrected.
