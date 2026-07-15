# Risk — Full Shipment Contribution

## Risk Level
**Low** for this static-read/candidate-output slice. No executor, no game state mutation, no runtime harness launch.

## Residual Risks

1. **ItemRegistry API surface change**: `ItemRegistry.GetObjectTypeDefinition().GetAllData()` (returning `ParsedItemData`) is called at every snapshot in the `WorldProgressReadAdapter`. If the SMAPI/Stardew API changes in a future version, the adapter returns null for `full_shipment_progress` (not an exception). This cascades to `FullShipmentKnown=false` throughout the candidate chain.

2. **`Object.isPotentialBasicShipped(string, int, string)` static variance**: The eligibility check calls the static three-argument method directly with fields from `ParsedItemData`. No item instances are created (no `ItemRegistry.Create`). If the static method behaves differently from the instance method for certain items (e.g., modded items), the results may differ from `getFarmerItemsShippedPercent()`. This is mitigated by calling the same static method that the decompiled utility uses internally.

3. **Shop-sell-only items with CanShip=true**: If a candidate's `CanShip` is incorrectly set to `true` by the existing availability evaluator for an item that can only be shop-sold, the Full Shipment contribution flag could be incorrectly set to `true`. This is a pre-existing risk in the candidate builder, not introduced by this slice.

4. **Candidate ranker does not score Full Shipment contribution**: The `EventCandidateRanker` copies the Full Shipment fields but does not modify its scoring formula. Intentional for this slice.

5. **No runtime validation**: The task explicitly forbids running the game or SMAPI. Runtime validation should be performed after the three missing capabilities are implemented, comparing live `ParsedItemData` results from the bridge against `Utility.getFarmerItemsShippedPercent` (without calling the side-effecting utility).

## Mitigations

- All nullable Full Shipment fields default to `null` (unknown), and `FullShipmentContributes` is always safely `false` when any upstream field is missing, null, or malformed.
- `FullShipmentContributes` = `Known && Eligible && CurrentShippedCount == 0 && CanShip`. Shop-sell-only items and already-shipped items never contribute.
- Malformed, missing, stale, unavailable, or duplicate progress data causes the entire index to be rejected (`ReadFullShipmentIndex` returns null), cascading to `FullShipmentKnown=false` and `FullShipmentContributes=false` for all candidates.
- The catalog entry honestly lists the three missing capabilities in `RequiredCapabilities`, has empty `RequiredTransparentFields`, and catalogues the covered fields in `CoveredTransparentFields`.
- 26 test definitions validate edge cases without requiring game runtime.
