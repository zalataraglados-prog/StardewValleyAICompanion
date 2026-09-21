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
- `POST /api/v1/strategy/commitments/reservation-portfolios/commit`
- `POST /api/v1/strategy/commitments/reservation-portfolios/settle-completed-route`

## Resource reservations

Material reservations bind one actor-authorized `material_inventory_graph.v1` node, slot, qualified item and quantity. Native-currency reservations bind one exact locked shop-currency domain member: money (`0`), star tokens (`1`), club coins (`2`), or Qi gems (`4`). Both carry source decision, source snapshot, goal and purpose. Only active rows reduce available supply; cancelled and completed rows remain auditable.

Every upsert recomputes unreserved supply from the referenced current snapshot and all other active reservations. Currency balance input must be a complete `player.shop_currency_balances.v1` projection and its money row must equal `player.money`. Unknown currencies, wrong owners, malformed rows, stale ledger revisions, overbooking and arithmetic overflow fail closed. The shared currency definition is used by TransparentBridge, shop-quote evaluation, the supply projection and the ledger service, so there is no parallel ID/key table.

The individual-row endpoints provide controller storage and double-spend prevention, but do not choose among alternative acquisition routes or authorize a Teacher label. The target-date `inventory_reservation` axis emits one exact multi-row claim set per feasible route, sharing a source decision, state hash and expected ledger revision.

`reservation-portfolios/commit` is the sole portfolio mutation boundary. It validates all explicit releases and every material/currency claim against one state hash and one expected revision, applies them only to an in-memory staging ledger, and discards the whole staging result if any later claim fails. A successful request is persisted once under the repository lock, advances the ledger exactly once, and records every component plus `reservation_portfolio_commit` at that same ledger revision. Claimless selections use the same transaction to record only that ownership marker; the input ledger is cloned and never mutated in place. This is an atomic storage primitive; route portfolio admission is owned by `acquisition_route_portfolio_admission.v1`, and neither layer alone authorizes a Teacher label or execution.

`reservation-portfolios/settle-completed-route` is the matching post-execution storage primitive. It requires one fresh snapshot, optimistic ledger revision, portfolio/goal/route identity, a lowercase SHA-256 reference to the fresh terminal receipt, and the exact complete set of active material/currency reservation IDs owned by that route source decision. It stages every row as `completed`, records the completion reason and evidence hash, appends component history plus one `reservation_portfolio_route_complete` marker, and persists the result at one new revision. A missing or extra ID rejects the whole mutation; a route with no claims still receives one auditable marker. This endpoint does not itself authenticate the referenced receipt or decide portfolio membership. The deterministic rollout receipt remains responsible for rebuilding those facts before any training admission.

`acquisition_route_portfolio_commit_receipt.v1` is the post-storage proof boundary. It deterministically rebuilds the admission and checks the exact active material/currency rows, explicit cancellations, commit result, single revision advance and same-revision history. Claimless portfolios still require an accepted marker-only commit result. `acquisition_route_execution_binding.v1` rebuilds that receipt and requires every normalized route command to carry the exact portfolio ID and committed ledger revision before dispatch. These artifacts prove ownership, not Teacher preference or portfolio completion.

`acquisition_route_portfolio_teacher_preference.v1` now supplies preference for the strictly decidable subset without accepting a caller candidate list. It derives a complete bounded candidate denominator from authoritative requirement selection rules and target-date Pareto route occurrences, reuses the same admission/preflight for every proposal, and selects only one aggregate vector that strictly Pareto-dominates every other admitted vector. Equal, incomparable or over-limit candidate sets remain blocked. Execution binding rebuilds this preference and requires its selected proposal/admission before the committed ledger can authorize input.

`acquisition_route_portfolio_settlement_receipt.v1` closes the proof boundary around completed-route storage. Its request builder reconstructs the verified execution binding and fresh terminal receipt, then derives the route's exact active claim set from the committed ledger. After the Backend mutation, the receipt checks one revision, every completed row and history entry, the route marker, result/ledger equality, and an exact replay using the marker timestamp. A valid receipt requires a fresh replan and does not authorize the next route from stale portfolio state.

`acquisition_route_portfolio_rollout_checkpoint.v1` is the first controller boundary after settlement. It rebuilds the Teacher preference and settlement, maps the completed route to one authoritative alternative, and evaluates each scoped selection rule. It proves completion only when every scope is satisfied, every selected occurrence is completed and no selected decision has an active reservation. Otherwise it requires a fresh replan. It does not itself authorize another route or formal training.

`acquisition_route_portfolio_continuation_teacher_request.v1` carries any verified incomplete checkpoint into the next current-state Teacher denominator. The builder requires the exact terminal state and settled-ledger hash/revision. Completed alternatives are removed internally and remaining slots are recalculated before the same bounded enumeration, strict-Pareto selection and atomic admission preflight run. Caller-authored completion fields are rejected. The selected continuation reuses exact commit, execution-binding, fresh terminal and settlement proofs while preserving the prior checkpoint identity.

`acquisition_route_portfolio_rollout_proof_manifest.v1` is the repeatable proof surface. It contains one initial proof and an ordered continuation-transition list. Verification rebuilds every checkpoint, prior hash, transition count, state and ledger identity before exposing the latest checkpoint; `acquisition_route_portfolio_rollout_proof_receipt.v1` binds the manifest and latest checkpoint hashes. A two-transition positive fixture and tamper rejection pass. The implementation has no fixed continuation count, while a three-or-more-transition fixture and final controller admission remain explicit gates.

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

This closes controller persistence for the listed commitment and reservation types, atomic material/currency or marker-only portfolio mutation, post-commit ownership proof, strict-Pareto Teacher selection, per-route dispatch binding, exact completed-route settlement, verified continuation selection/execution/settlement, cumulative two-transition completion and the repeatable proof-chain verifier, not the entire long-horizon planner. Greenhouse/Island/Indoor Pot rules, fertilizer and skill modifiers, crop layout feasibility, evidence-backed resolution of incomparable route portfolios, a three-or-more-transition regression, final rollout admission, future-income commitments, animal/building commitments, mining/smelting queues, storage supply, and broader machine placement/service still require separate typed commitments or transparent state.
