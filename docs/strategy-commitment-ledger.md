# Strategy Commitment Ledger

## Boundary

`strategy_commitment_ledger.v1` is controller-owned strategy state. It is not emitted by TransparentBridge and is not included in the game `state_hash`. The bridge remains authoritative for current game facts; the ledger records explicit future decisions that do not yet exist in game state.

The ledger currently carries crop planting commitments, exact material and native-currency reservations, and typed machine intents. For `outdoor_seasonal` crop planting, a strategy producer chooses only:

- stable commitment and source-decision IDs;
- seed ID;
- tile count;
- planting year, season, and day.

The controller binds harvest identity and context tags, base growth days, regrow days, minimum units per wave, absolute planting/harvest days, and last in-season harvest from the current native `farm.crop_catalog`. Unsupported locations, invalid seasons, past dates, crops that cannot mature, missing calendar anchors, unknown seeds, and stale revisions fail closed.

The projection is conservative and conditional: no fertilizer, Agriculturist, paddy acceleration, missed watering, crop loss, or later skill change is invented. Those modifiers require explicit future commitment fields before they may shorten the deadline.

## Persistence and revisions

The Backend persists one ledger per save/player identity under `STARDEWAI_STRATEGY_LEDGER_DIR`. The default is `E:\StardewAITraining\strategy-commitments` when E: is available, otherwise an application-local directory. File names are SHA-256 hashes of save/player identity; updates use a same-directory temporary file followed by replacement.

Every mutation requires `expected_ledger_revision`. A stale caller receives `ledger_revision_conflict` and cannot overwrite a newer plan. Upsert, cancel, and automatic completion append immutable history rows with ledger revision, commitment revision, operation, source decision, source state hash, time, and reason. Cancelled/completed commitments remain in the ledger for audit but no longer create machine demand.

Endpoints:

- `GET /api/v1/strategy/commitments/latest`
- `POST /api/v1/strategy/commitments/crops/upsert`
- `POST /api/v1/strategy/commitments/crops/{commitmentId}/cancel`
- `POST /api/v1/strategy/commitments/materials/upsert`
- `POST /api/v1/strategy/commitments/materials/{reservationId}/cancel`
- `POST /api/v1/strategy/commitments/currencies/upsert`
- `POST /api/v1/strategy/commitments/currencies/{reservationId}/cancel`

## Resource reservations

Material reservations bind one actor-authorized `material_inventory_graph.v1` node, slot, qualified item and quantity. Native-currency reservations bind one exact locked shop-currency domain member: money (`0`), star tokens (`1`), club coins (`2`), or Qi gems (`4`). Both carry source decision, source snapshot, goal and purpose. Only active rows reduce available supply; cancelled and completed rows remain auditable.

Every upsert recomputes unreserved supply from the referenced current snapshot and all other active reservations. Currency balance input must be a complete `player.shop_currency_balances.v1` projection and its money row must equal `player.money`. Unknown currencies, wrong owners, malformed rows, stale ledger revisions, overbooking and arithmetic overflow fail closed. The shared currency definition is used by TransparentBridge, shop-quote evaluation, the supply projection and the ledger service, so there is no parallel ID/key table.

These endpoints provide controller storage and double-spend prevention for individual rows. They do not choose among alternative acquisition routes and do not authorize a Teacher label. The target-date `inventory_reservation` axis now emits one exact multi-row claim set per feasible route, sharing a source decision, state hash and expected ledger revision. The controller must select a route and commit the complete set atomically; sequential partial success is not sufficient for execution authorization.

## Machine binding

Machine demand first uses current detached native input probes when the crop exists in inventory. For future crops that do not yet exist in inventory, it reproduces the decompiled static `MachineDataUtility.CanApplyOutput` boundary over complete native machine trigger rows: `ItemPlacedInMachine`, optional item identity, all required/negated context tags, and required count. Dynamic `GameStateQuery`/output conditions, custom output methods, machine-level extra inputs, time modifiers/blockers, overnight-only completion, and missing durations fail closed instead of being guessed.

It selects the next committed first/regrow wave, combines commitments arriving on the same day, and emits:

- `next_arrival_source=committed_strategy_ledger`;
- ledger ID and revision;
- exact commitment IDs;
- conservative minimum incoming units;
- service interval to the next regrow wave;
- existing capacity and deficit between arrival waves.

Candidate, daily plan, and action compiler preserve these values. The compiler reloads the authoritative ledger and recalculates demand. Any revision, cancellation, completion, crop identity, date, or quantity drift blocks the old action as `craft_machine_item_demand_projection_drifted`.

## Remaining scope

This closes controller persistence for the listed commitment and reservation types, not the entire long-horizon planner. Greenhouse/Island/Indoor Pot rules, fertilizer and skill modifiers, crop layout feasibility, route-portfolio selection, future-income commitments, animal/building commitments, mining/smelting queues, storage supply, and broader machine placement/service still require separate typed commitments or transparent state.
