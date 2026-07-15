# Grandpa Full Shipment Contribution - Worker Notes

**BUILD NOT RUN** -- no `dotnet build`, `dotnet test`, or runtime harness was launched. **TESTS NOT RUN.** All test definitions are unrun.

## Summary

Implemented the static transparent-read and candidate-output slice for exact Full Shipment progress in Stardew Valley. The `world_progress.full_shipment_progress` field reports a typed, deterministic, sorted eligibility snapshot computed from `ParsedItemData` (returned by `ItemRegistry.GetObjectTypeDefinition().GetAllData()`) and `Game1.MasterPlayer.basicShipped` using the exact decompiled eligibility rule: `data.Category != -7`, `data.Category != -2`, `StardewValley.Object.isPotentialBasicShipped(data.ItemId, data.Category, data.ObjectType)` (static method, no item instantiation). The `complete_full_shipment` direction is honestly blocked on three still-missing capabilities with empty `RequiredTransparentFields`; the covered fields are catalogued separately in `CoveredTransparentFields`.

## Changed Files

### Contracts
- **`src/StardewAI.Contracts/State/ProgressState.cs`**: Added `FullShipmentProgressRef` and `FullShipmentItemProgressRef` typed records with ItemId-then-QualifiedItemId sorted entries and ordinal-sorted missing-item enumeration.
- **`src/StardewAI.Contracts/Options/OptionContracts.cs`**: Extended `EconomicCandidate` with `CanShip`, `CanShopSell`, `FullShipmentKnown`, `FullShipmentEligible`, `FullShipmentCurrentShippedCount`, `FullShipmentAlreadyShipped`, `FullShipmentContributes` fields.
- **`src/StardewAI.Contracts/Training/TrainingFeatureContracts.cs`**: Extended `PolicyEventCandidatePrediction` with `CanShip`, `CanShopSell`, `FullShipmentKnown`, `FullShipmentEligible`, `FullShipmentCurrentShippedCount`, `FullShipmentAlreadyShipped`, `FullShipmentContributes` fields.
- **`src/StardewAI.Contracts/Training/GrandpaDirectionDailyCandidateBindingContracts.cs`**: Added `CoveredTransparentFields` to `GrandpaDirectionBindingResult`.

### Adapter
- **`src/StardewAI.TransparentBridge/Adapters/ProgressReadAdapter.cs`**: Added `full_shipment_progress` field to `WorldProgressReadAdapter.Collect()`. Rewrote `ReadFullShipmentProgress` to enumerate `ParsedItemData` from `GetAllData()`, use `data.ObjectType`, call static `Object.isPotentialBasicShipped(itemId, category, objectType)`. Renamed eligibility helper to `IsEligibleForFullShipment` (private, inline expression, no try/catch). Removed `using StardewValley.GameData.Objects`. Sorted entries by ItemId then QualifiedItemId, missing ItemIds ordinal.

### Core Evaluator
- **`src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.cs`**: Added `ReadFullShipmentIndex()` fail-closed helper that validates envelope status (`available`/`derived`), value shape, unique non-empty item_ids, shipped/consistency, count match. `SellCandidates` now populates `FullShipmentKnown`, `FullShipmentEligible`, `FullShipmentCurrentShippedCount`, `FullShipmentAlreadyShipped`, and `FullShipmentContributes` (= known && eligible && count==0 && CanShip).

### Core Training
- **`src/StardewAI.Core/Training/EventCandidateRanker.cs`**: Updated economic candidate -> PolicyEventCandidatePrediction mapping to copy all new Full Shipment contribution fields plus `CanShip`/`CanShopSell`.
- **`src/StardewAI.Core/Training/GrandpaDirectionDailyCandidateBinding.cs`**: Updated `CloneCandidate` to preserve all new fields. Updated `Bind` and `BuildBlocked` to populate `CoveredTransparentFields`.
- **`src/StardewAI.Core/Training/GrandpaDirectionCatalog.cs`**: Added `CoveredTransparentFields` property to `GrandpaDirectionCatalogEntry`. For `complete_full_shipment`: empty `RequiredTransparentFields`, `CoveredTransparentFields` = `["world_progress.shipping_collection", "world_progress.full_shipment_progress"]`, three capability blockers unchanged.

### Tests
- **`tests/StardewAI.Core.Tests/FullShipmentContributionTests.cs`**: 26 test definitions:
  - 12 evaluator snapshot tests (calling `CandidateOptionAvailabilityEvaluator.Evaluate`, including missing/nonnumeric current count and nonpositive sale price fail-closed checks)
  - 4 pipeline tests (ranker, binder clone)
  - 1 static source-guard test (adapter uses exact static call; no ItemRegistry.Create, instance call, or Utility.getFarmerItemsShippedPercent)
  - 4 catalog/binding tests (CoveredTransparentFields semantics including result/catalog non-aliasing)
  - 4 contract arithmetic/sorting tests (DTO wizard kept for arithmetic; no Minerals/category -2 eligibility claims)
  - 1 helper method test (CoveredTransparentFields non-aliasing)
- **`tests/StardewAI.Core.Tests/GrandpaDirectionDailyCandidateBindingTests.cs`**: Updated one assertion for the corrected catalog block reason.

### Documentation
- **`docs/stardewai-full-handoff/grandpa-full-shipment-contribution/`**: This directory with WORKER_NOTES, evidence, transparency-coverage, risk, and test-results.

## Tests Run or Recommended

**NOT RUN** -- per task constraint. Recommended test commands after the real repo is updated:
```powershell
dotnet test StardewValleyAICompanion.sln --filter "FullyQualifiedName~FullShipmentContributionTests"
dotnet test StardewValleyAICompanion.sln --filter "FullyQualifiedName~GrandpaDirectionDailyCandidateBindingTests"
```

The test class covers:
1. Evaluator: shipping-capable missing eligible item => known true, contributes true
2. Evaluator: already-shipped item => contributes false with exact count
3. Evaluator: shop-sell-only => contributes false
4. Evaluator: known ineligible item => eligible false, contributes false
5. Evaluator: missing field => known false, contributes false
6. Evaluator: stale/unavailable status => known false
7. Evaluator: malformed items array => known false
8. Evaluator: duplicate item_ids => known false
9. Evaluator: eligible_item_count mismatch => known false
10. Evaluator: shipped/count inconsistency => known false
11-14. Pipeline: ranker copies fields, preserves CanShip/CanShopSell, binder clone preserves fields, shop-only ranker
15-17. Catalog: empty MissingTransparentFields + covered catalogued, CoveredTransparentFields on entry, 12 entries
18-20. DTO: sort order, complete semantics, empty/zero semantics, missing-sort-ordinal

## Risks

- **Low** for this static-read/candidate-output slice: no executor, no side effects, no game mutation.
- The `full_shipment_progress` computation in `WorldProgressReadAdapter` calls `Object.isPotentialBasicShipped(itemId, category, objectType)` (static) and `ItemRegistry.GetObjectTypeDefinition().GetAllData()` (returns `ParsedItemData`) at runtime, which requires SMAPI to be loaded. If SMAPI is not running, the adapter returns null (unavailable status).
- The catalog entry for `complete_full_shipment` has empty `RequiredTransparentFields` and lists covered fields separately. The direction is blocked solely on the three still-missing capabilities.
- Shop-sell-only items correctly show `CanShip=false` when the transparent bridge reports shipping bin is not available, and `FullShipmentContributes` is computed from the index's `shipped` flag AND `CanShip`.
- `FullShipmentContributes` is only true when `known && eligible && currentCount==0 && CanShip`. All malformed, missing, duplicate, or inconsistent progress data cascades to `FullShipmentKnown=false` for the entire index.
- No item instances are created in the adapter; the static `Object.isPotentialBasicShipped` method is called directly with field values from `ParsedItemData`.

## Remaining Work

1. **Native shipping compiler**: A compiler that translates shipping-bin candidate plans into native game actions.
2. **Native-input shipping executor**: A runtime executor that performs the actual shipping-bin interaction in-game.
3. **End-of-day `basicShipped` update postcondition recorder**: An observer that captures `basicShipped` after nightly save.
4. **Full Shipment direction binding**: Once the three capabilities above exist, update the catalog to enable direct binding.
5. **Candidate scoring integration**: Add Full Shipment contribution as a reward signal in the event candidate ranker.
6. **Training episode feedback**: Close the loop with observed `basicShipped` delta after end-of-day processing.
7. **Runtime validation**: Run the bridge with SMAPI to verify live `ParsedItemData` enumeration and static `isPotentialBasicShipped` results match `Utility.getFarmerItemsShippedPercent` (without calling the side-effecting utility).
