# Grandpa Full Shipment Contribution Handoff

Current as of 2026-07-17. This supersedes the original static-only worker handoff.

## Implemented Chain

1. `WorldProgressReadAdapter` exports typed, deterministic Full Shipment eligibility and `Game1.MasterPlayer.basicShipped` progress using the verified vanilla eligibility rule.
2. `CandidateOptionAvailabilityEvaluator` produces fail-closed shipping candidates with `CanShip`, eligibility, prior count, already-shipped, and contribution fields.
3. `EventCandidateRanker` preserves those fields.
4. `GrandpaDirectionDailyCandidateBinding` directly binds `complete_full_shipment` only when every exact contribution condition agrees.
5. `DailyPlanCompiler` and `ActionQueueCompiler` compile `ship_inventory_item_to_bin`.
6. The native-input executor performs the shipping-bin interaction and writes an immediate pending receipt.
7. The delayed settlement recorder observes the day-end `basicShipped` update.

## Binding Invariant

A candidate may advance `complete_full_shipment` only when:

- option is `economy.ship_items`;
- kind is `ship_inventory_item_to_bin`;
- candidate is current, available, and timeline-legal;
- `CanShip == true`;
- `FullShipmentKnown == true`;
- `FullShipmentEligible == true`;
- `FullShipmentCurrentShippedCount == 0`;
- `FullShipmentAlreadyShipped == false`;
- `FullShipmentContributes == true`.

The `earn_money` direction remains independent and may still value profitable shipping that does not advance Full Shipment.

## Verification

- Focused Core: 103/103 passed.
- Full Core: 946/946 passed.
- Backend: 49/49 passed.
- E-drive isolated native shipping immediate smoke passed on 2026-07-17.
- Prior isolated evidence covers delayed day-end settlement.

## Remaining Boundary

No Full Shipment compiler/executor capability remains missing. Broader training readiness depends on completing the other Grandpa direction candidate chains and long-run executor stability, not this direction.
