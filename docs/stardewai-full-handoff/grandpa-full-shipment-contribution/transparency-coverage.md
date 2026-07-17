# Full Shipment Transparency Coverage

| Layer | Data or capability | Status |
|---|---|---|
| Transparent read | Eligible item set from parsed object data and vanilla static eligibility rule | Implemented |
| Transparent read | Per-item `basicShipped` count and already-shipped state | Implemented |
| Transparent read | Eligible, shipped, missing counts, ratio, completion, sorted missing IDs | Implemented |
| Candidate | `CanShip`, eligibility, count, already-shipped, contribution | Implemented fail-closed |
| Ranker | Full Shipment fields preserved without fabrication | Implemented |
| Grandpa binder | Exact contribution evidence gate | Implemented |
| Daily plan | `ship_inventory_item_to_bin` candidate handoff | Implemented |
| Action compiler | Explicit shipping-bin native action queue | Implemented |
| Runtime | Native-input shipping-bin interaction | Implemented and smoke-tested |
| Feedback | Immediate inventory/bin receipt | Implemented and smoke-tested |
| Feedback | Delayed day-end `basicShipped` settlement | Implemented with prior isolated runtime proof |

Covered transparent fields are `world_progress.shipping_collection` and `world_progress.full_shipment_progress`. No capability remains listed as missing for `complete_full_shipment`.

The binder requires `CanShip == true`, `FullShipmentKnown == true`, `FullShipmentEligible == true`, `FullShipmentCurrentShippedCount == 0`, `FullShipmentAlreadyShipped == false`, and `FullShipmentContributes == true`.
