# Transparency Coverage — Full Shipment Contribution

| field or output id | consumer | required for | transparent source path | evidence | status |
|---|---|---|---|---|---|
| `world_progress.full_shipment_progress` | evaluator, sample adapter, candidate builder | Full Shipment direction binding candidate contribution | `ItemRegistry.GetObjectTypeDefinition().GetAllData()` (ParsedItemData) + `Game1.MasterPlayer.basicShipped` + `Object.isPotentialBasicShipped(itemId, category, objectType)` (static); category != -7 && category != -2 | EVD-FS-001 (this slice) | covered_for_read |
| `full_shipment_progress.eligible_item_count` | evaluator, binding | deterministic eligibility set size | Computed from all `ParsedItemData` entries passing the eligibility rule | EVD-FS-001 | implemented |
| `full_shipment_progress.shipped_eligible_item_count` | evaluator, binding | current shipped count of eligible items | Derived from `basicShipped` for eligible items only | EVD-FS-001 | implemented |
| `full_shipment_progress.missing_item_count` | evaluator, binding | remaining items to ship | `eligible_item_count - shipped_eligible_item_count` | EVD-FS-001 | implemented |
| `full_shipment_progress.completion_ratio` | evaluator, binding | numeric progress | `shipped_eligible_item_count / eligible_item_count` | EVD-FS-001 | implemented |
| `full_shipment_progress.complete` | evaluator, binding | achievement-34 equivalent | `shipped_eligible_item_count == eligible_item_count && eligible_item_count > 0` | EVD-FS-001 | implemented |
| `full_shipment_progress.items[]` (sorted by item_id then qualified_item_id) | evaluator, candidate builder | per-item eligibility/shipped info | item_id, qualified_item_id, display_name, category, object_type, current_shipped_count, shipped, FullShipmentItemProgressRef | EVD-FS-001 | implemented |
| `full_shipment_progress.missing_item_ids[]` (sorted ordinal) | evaluator, planner | explicit lossless missing set | Items where `shipped == false`, sorted by item_id | EVD-FS-001 | implemented |
| `economic candidate .full_shipment_known` | evaluator, binding | whether eligibility index is valid | true when `ReadFullShipmentIndex` succeeds; false when snapshot is missing/stale/malformed | EVD-FS-002 | implemented |
| `economic candidate .full_shipment_eligible` | binding | per-item eligibility | true when item_id present in validated index | EVD-FS-002 | implemented |
| `economic candidate .full_shipment_contributes` | binding | shipping-bin contribution signal | true only when known && eligible && current_shipped_count == 0 && CanShip | EVD-FS-002 | implemented |
| `economic candidate .can_shop_sell` | evaluator, binding | shop-sell-only exclusion from contribution | Set by existing shop/bin availability logic; copied through evaluator/ranker/binding | EVD-FS-002 | implemented |
| `policy event candidate .can_ship` | binding | shipping eligibility | Copied from economic candidate through evaluator and ranker | EVD-FS-003 | implemented |
| `policy event candidate .can_shop_sell` | binding | shop-sell eligibility | Copied from economic candidate through evaluator and ranker | EVD-FS-003 | implemented |
| `policy event candidate .full_shipment_known` | binding | downstream binding decisions | Copied from economic candidate through evaluator, ranker, and CloneCandidate | EVD-FS-003 | implemented |
| `policy event candidate .full_shipment_contributes` | binding, executor | honest contribution flag | Copied from economic candidate through evaluator, ranker, and CloneCandidate | EVD-FS-003 | implemented |
| `complete_full_shipment` catalog entry `RequiredTransparentFields` | binding | honest direction blocker | Empty; no fields claimed as missing for read | EVD-FS-004 | empty |
| `complete_full_shipment` catalog entry `CoveredTransparentFields` | binding, planner | transparent coverage record | `world_progress.shipping_collection`, `world_progress.full_shipment_progress` | EVD-FS-004 | catalogued |
| `complete_full_shipment` catalog entry `RequiredCapabilities` | binding, planner | honest capability gap | `NativeShippingCompilerCapability`, `NativeInputShippingExecutorCapability`, `EndOfDayBasicShippedPostconditionRecorderCapability` | EVD-FS-004 | missing |
| `complete_full_shipment` binding result | binding, planner | honest block | blocked due to missing capabilities; `MissingTransparentFields` empty; `CoveredTransparentFields` populated | EVD-FS-004 | verified_in_tests |

## Still Missing (Transparent/Candidate Coverage vs Execution)

| capability | what it provides | status |
|---|---|---|
| NativeShippingCompilerCapability | Compiles shipping-bin candidate plans into native game actions | missing |
| NativeInputShippingExecutorCapability | Performs actual shipping-bin interaction at runtime | missing |
| EndOfDayBasicShippedPostconditionRecorderCapability | Records `basicShipped` after nightly save for training feedback | missing |
| Strategy value scoring using Full Shipment contribution | Ranks candidates higher when they contribute to Full Shipment | not in this slice |
| Training episode feedback for Full Shipment | Closes the loop with observed basicShipped delta | missing |
