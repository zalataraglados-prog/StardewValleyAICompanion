# Controller Audit

## Decision

Accepted for static integration on 2026-07-14.

The accepted revision was not built or tested. No game, SMAPI, runtime harness, training process, deployment, or E: runtime/training operation was started.

## Corrected Understanding

`world_progress.shipping_collection` already existed and reads `Game1.MasterPlayer.basicShipped`. The missing transparent input was the dynamic Full Shipment target universe and its exact per-item completion state, not the shipped dictionary itself.

## Accepted Behavior

- `world_progress.full_shipment_progress` enumerates the live object registry instead of using a fixed vanilla list or count.
- Eligibility mirrors `Utility.getFarmerItemsShippedPercent`: category is not `-7`/`-2`, then `Object.isPotentialBasicShipped(itemId, category, objectType)` is called.
- Progress lookup uses unqualified ItemId, matching `basicShipped` keys.
- Output includes every eligible target, shipped count/state, deterministic missing IDs, aggregate counts, ratio, and completion flag.
- The adapter does not call the side-effecting Utility percentage method and does not create item instances.
- Economic candidates record typed shop-sell, shipping, eligibility, prior shipment, and new-contribution state.
- Missing, stale, unavailable, malformed, duplicate, or internally inconsistent target data makes the entire contribution index unknown.
- Shop-sell-only, nonpositive shipping value, ineligible, and already-shipped candidates cannot claim Full Shipment contribution.
- Typed fields survive ranking and binding-candidate cloning without adding a guessed score, reward, duration, achievement delta, or completion prediction.

## Remaining Blocker

`complete_full_shipment` remains disabled for direction binding. Transparent inputs are now catalogued as covered; missing blockers are:

- native shipping compiler;
- native-input shipping executor;
- end-of-day `basicShipped` postcondition recorder.

Until all three exist, this direction must not enter training as an executable closed-loop action.

## Static Verification

- Controller audited production adapter, evaluator, ranker, binder clone, catalog, contracts, and focused test definitions.
- Main-repository files matched the sandbox baseline before merge.
- `git apply --check` passed before merge.
- Tests are defined but not run.
- Build is not run.

## Next Slice

Separate native shipping from shop selling at the candidate/compiler boundary, compile an explicit shipping-bin interaction queue, implement player-input execution without direct inventory mutation, and record both immediate inventory/bin state plus the next end-of-day `basicShipped` delta before enabling Full Shipment direction binding.

