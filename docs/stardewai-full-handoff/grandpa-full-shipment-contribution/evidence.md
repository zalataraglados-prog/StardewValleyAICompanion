# Evidence

## Decompiled Source Evidence

### Full Shipment Eligibility Rule
From `StardewValley.Utility.getFarmerItemsShippedPercent(Farmer)`:
- Enumerates `ItemRegistry.GetObjectTypeDefinition().GetAllData()` to obtain every registered `ParsedItemData` entry.
- Filters: category must not be `-7` (non-shippable gems) and not `-2` (non-shippable minerals).
- Each remaining candidate must pass `Object.isPotentialBasicShipped(string itemId, int category, string objectType)` — the three-argument static method on `StardewValley.Object`.
- Checks `farmer.basicShipped` by unqualified `ItemId` (not QualifiedItemId).

### Eligibility Method Signature (decompiled)
`StardewValley.Object.isPotentialBasicShipped(string item_id, int category, string object_type)` — static, no instance required. Called with the `ParsedItemData` fields: `ItemId`, `Category`, `ObjectType`.

### Side Effect Warning
Calling `Utility.getFarmerItemsShippedPercent()` mutates `recentlyDiscoveredMissingBasicShippedItem` on the farmer instance. The bridge adapter calls only the static `Object.isPotentialBasicShipped(item_id, category, object_type)` with fields from `ParsedItemData`; no item instances are created (no `ItemRegistry.Create`), and the side-effecting utility is not invoked.

### Achievement 34 Grant
`Stats.checkForShippingAchievements()` grants achievement 34 (`achievement_full_shipment`) only when `Utility.hasFarmerShippedAllItems()` is true. The bridge does not call this method; it instead reports the transparent `full_shipment_progress.complete` flag derived from the exact same item set.

## Existing Bridge Evidence

### shipping_collection (unchanged)
The existing `world_progress.shipping_collection` field reads `Game1.MasterPlayer.basicShipped` as a sorted `Dictionary<string, int>`. It is listed in `CoveredTransparentFields` for the `complete_full_shipment` catalog entry, not as a missing requirement.

### New full_shipment_progress
Added to `WorldProgressReadAdapter`. Computed at each snapshot from:
1. `ItemRegistry.GetObjectTypeDefinition().GetAllData()` — live `ParsedItemData` definitions
2. `Game1.MasterPlayer.basicShipped` — per-item shipped counts
3. Pure eligibility check: `category != -7 && category != -2 && Object.isPotentialBasicShipped(itemId, category, objectType)` (static)

Output is a typed `FullShipmentProgressRef` with ItemId-then-QualifiedItemId sorted deterministic item entries and ordinal-sorted missing-item enumeration.

## New Canonical Evidence Sources

| Evidence | Decompiled Source Path | Sandbox File |
|---|---|---|
| Full Shipment item eligibility (category filter) | `StardewValley.Utility.getFarmerItemsShippedPercent` | `src/StardewAI.TransparentBridge/Adapters/ProgressReadAdapter.cs:ReadFullShipmentProgress` |
| `Object.isPotentialBasicShipped(string, int, string)` static | `StardewValley.Object.isPotentialBasicShipped(item_id, category, object_type)` | `src/StardewAI.TransparentBridge/Adapters/ProgressReadAdapter.cs:IsEligibleForFullShipment` |
| `ParsedItemData` from `GetAllData()` | `ItemRegistry.GetObjectTypeDefinition().GetAllData()` | Enumerated in `ReadFullShipmentProgress` |
| Achievement 34 grant | `StardewValley.Stats.checkForShippingAchievements()` | Not called by bridge; completion ratio derived instead |
| `basicShipped` data source | `Game1.MasterPlayer.basicShipped` | Already in `shipping_collection`; reused in `full_shipment_progress` |
| Candidate Full Shipment fields | `CandidateOptionAvailabilityEvaluator.SellCandidates` | `src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.cs:ReadFullShipmentIndex` + `SellCandidates` |
| Covered/Required transparency separation | `GrandpaDirectionCatalog` | `src/StardewAI.Core/Training/GrandpaDirectionCatalog.cs:CoveredTransparentFields` |
