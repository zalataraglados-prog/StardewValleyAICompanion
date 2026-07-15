# Risk

## Risk Level

- Medium-high until isolated runtime smoke validates quest candidate deserialization, type-9 disambiguation, and compiler envelope wiring in-game.
- Medium for compile correctness because build/test execution was intentionally skipped under the active user-play constraint.

## Residual Risks

- `QuestCandidateBuilder` reads from deserialized `QuestProgressRef`/`SpecialOrderProgressRef` JSON snapshots rather than directly from Net fields; field mismatches or missing JsonPropertyName annotations could produce silent nulls. All JsonPropertyName values have been verified against the schema and the field names emitted by the bridge.
- Type-9 disambiguation is now resolved at the bridge producer via CLR pattern matching (`ResolveRuntimeClass`); the heuristic `QuestCandidateBuilder.ResolveType9RuntimeClass()` persists as a consumer-side fallback but should never execute with the wired bridge.
- Quest and special-order candidates are always blocked by `quest_native_executor_not_implemented`; no runtime execution path exists for any candidate. `EstimatedTicks` and `EnergyCost` use -1 sentinel values.
- Unknown time/energy on candidates means they cannot participate in ranking or budget validation; the `preview_only` gate prevents downstream selection.
- Base `Quest` handling (`quest.GetType() == typeof(Quest)`) now explicitly supports the concrete base class without marking it as unsupported subtype.
- Title/description availability (`TitleAvailable`/`DescriptionAvailable`) is inferred from field state since `_loadedTitle` (protected) and `_loadedDescription` (private) are not externally accessible.
- `ReadQuestProgressMapper` depends on StardewValley game types not available at test-time; only interface contract and concrete instantiation are directly testable. Full mapping correctness relies on bridge integration within SMAPI runtime.
- `DonatedItems` null entries (from `OnFail` writing null) produce zeroed refs with empty ItemId; callers must distinguish active null-placeholder from actual non-null item with empty `ItemId`.
- `SelectedObjectiveIndex` is local to snapshot; if objective list order changes between snapshots (mod intervention), index becomes stale. No identity key per objective bounds the index.
- Malformed numeric rejection is compile-time parameter validation only; model-side production of those parameters is outside compiler scope.

## Mitigations (Items 30-38)

- **Item 30:** Base `Quest` uses explicit `typeof(Quest)` case; only unknown derived classes emit `unsupported_subclass`.
- **Item 31:** Incomplete objective index returned from `CategorizeSpecialOrderNextAction` bind the same row for all evidence.
- **Item 32:** `CategorizeType9NextAction` uses decompile-backed field patterns for `ItemHarvestQuest`, `LostItemQuest`, `SecretLostItemQuest`.
- **Item 33:** `specialRule` null check changed from `string.IsNullOrWhiteSpace` to `is null` to treat empty string as valid.
- **Item 34:** Participant/reward-claim fields emitted as deterministic `Dictionary<long,bool>`.
- **Item 35:** All six special-order lifecycle/randomization fields emitted from direct Net field reads.
- **Item 36:** Title/description/objective read from public backing fields; no observer path calls loading getters.
- **Item 37:** All ordinary-quest lifecycle and subclass reward fields emitted.
- **Item 38:** Production `CompileQuestPlan` reconstructs from live snapshot, requires and cross-checks identity, rejects missing/stale/ambiguous/contradictory parameters, populates from matched live evidence.
- No observer path calls quest event/probe/mutator/reload methods; only direct field reads and proven public getters are used.
- The `PerTypeObjectiveFields` and `PerTypeQuestFields` DTOs preserve explicit `Available`/`UnavailableReason` per field group; missing adapter fields fail closed with diagnostics.
- Ordinary quests and special orders remain separate candidate families with independent schemas.
- CLR pattern matching replaces heuristic field-pattern disambiguation for type-9 quests at the producer.
- All 11 ordinary subclasses, all 9 objective subtypes, and all 6 reward subtypes have explicit pattern-match branches and per-type field extractions.
- Static tests assert runtime-type disambiguation, type-9 subclasses, base Quest, ordinary/special-order separation, missing-field fail-closed behavior, blocked diagnostics, unknown cost, compiler envelope serialization, anti-confusion identity binding, negative-one sentinel, and known-availability initialization.
- **Item 39:** `QuestProgressRef.ModData` and `ObsoleteCompletionString` added with deterministic ordering.
- **Item 40:** `TitleAvailable`/`DescriptionAvailable` fail closed on empty values; `CurrentObjectiveAvailable` added.
- **Item 41:** Pure `ReadQuestProgressMapper` extracted; `ProgressQuestReadAdapter` injects via constructor; direct mapper contract tests added.
- **Item 42:** `evidence.md` and `transparency-coverage.md` updated with Items 39-45 evidence and 10 new coverage rows.
- **Item 43:** Null-safe `donatedItems` with `QualifiedItemId` + `ModData`; null entries zeroed; donor-ID claim removed.
- **Item 44:** `SelectedObjectiveIndex` populated end-to-end in `QuestCandidateRef`, `QuestCompilerEnvelope`, `QuestPlanEnvelope`; cross-checked and validated.
- **Item 45:** Malformed numeric parameters (`target_count`, `current_count`, `objective_index`) rejected with explicit `_malformed:` block reasons in both `BuildCompilerEnvelope` and `CompileQuestPlan`.
- Runtime validation commands are recorded as pending for the controller to run after the user-play constraint is lifted.
