# Strategy Commitment Ledger

## Boundary

`strategy_commitment_ledger.v1` is controller-owned strategy state. It is not emitted by TransparentBridge and is not included in the game `state_hash`. The bridge remains authoritative for current game facts; the ledger records explicit future decisions that do not yet exist in game state.

The first supported commitment is `outdoor_seasonal` crop planting. A strategy producer chooses only:

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

This closes the first cross-season crop commitment slice, not the entire long-horizon planner. Greenhouse/Island/Indoor Pot rules, fertilizer and skill modifiers, crop layout feasibility, seed purchasing/reservation, animal/building commitments, mining/smelting queues, storage supply, and machine placement/service still require separate typed commitments or transparent state.
