# Owned Seed Supply In The Original Crop Planner

## Accepted Predecessor

PR506 passed all42 CI steps in42369036098 and review, merged as565f3a0c with
identical tested/main trees, and removed its owned refs. The explicit installer
can persist original crop/resource requests. Its planning receipt is unchanged;
ledger installation is not gameplay or training authority.

## Current Consumer

The original `CropTeacherRunPlanner.Evaluate` now accepts an optional trailing
`resourceLedger`. Its null/default path keeps the original inventory-count,
cross-commitment legacy seed grouping and acquisition fallback. No snapshot is
rewritten to fake a different inventory. New optional step fields are omitted
when null so old step JSON does not acquire extra fields. The original13 planner
tests are linked into the game-free Backend suite, not copied or weakened; Core's
game-free dependency boundary stays intact.

An explicit managed call uses `CropTeacherOwnedSeedSupply.Read` once when pending
planting actually needs a supply decision. It consumes only the material graph
and player/currency domains of the existing ingested snapshot, not a serialized
copy or fresh capture of the whole world, and does not run per frame. Completed
planting does not force another seed-supply projection for watering/harvesting.

The typed ledger overload shares the original path-reader's complete validation.
Current root/source/save/player identities, all active material/currency claims,
authorized player inventory, raw player-slot identity/stack/quality and native
location must agree. External player inventories and non-chest nodes are rejected
before routing, even with a read-authorization flag. No missing raw quality is
guessed as zero. Only money/shop-currency player fields enter the serialized
currency projection; raw slots/location are read directly. Each requested
active crop binds its seed, original tile denominator and planting day to the
existing run plan/layout. Ownership requires the original Teacher decision and
exact crop-specific claim purpose; equal seed ID or decision ID alone is not
sufficient. Other roots and another crop of the same seed are not pooled.

Only this commitment's active claimed player slots count as ready seeds. For
example, a three-seed native stack with two reserved for another root supplies
only one seed for a two-tile plan; the planner still asks for one purchase.
An unclaimed inventory increase does not acquire ownership. Only the exact
crop-specific seed-purchase money reservation funds the remaining purchase,
not the native wallet or another root's construction funds.

Owned chest seeds take precedence over additional purchases, even if the old
run plan has no purchase intent because those seeds already exist. The planner
emits `route_to_seed_supply` with the graph's actual location, then
`transfer_seed_supply` with the exact reservation ID and original
`MaterialTransferIntent`. The original transfer projector checks the native
same-location/normal-chest/lock/capacity/stack branch. This is a typed proposed
step, not a new executor or a completed transfer/relocation proof.

The default automatic handoff still rejects active resource claims. This slice
does not wire managed steps into execution, install a new claim, change the old
initializer, or authorize native outcomes/formal training. The source is an
ingested planning input, not a standalone native-proof or snapshot-import bypass.

## Exact Next Boundary

The per-step purchase accounting kernel is now implemented; see
[settlement contract and remaining runtime wiring](money-crop-seed-purchase-settlement.md).
It reuses original native validators and portfolio services; the automatic runner
is not enabled. The list below is the full integration scope, not a claim that
every item still requires a new implementation.

1. Before each route/interaction/purchase/transfer, use the original portfolio
   service/API to create a unique marker-only support commit. Its portfolio and
   source-decision IDs are the support-step ID. Do not change PR504/506's original
   resource portfolio ID or Teacher-owned claim source just to satisfy settlement.
2. Reuse original candidates, daily-plan/action-queue compiler and strong native
   queue/transition receipt validators. Bind the exact shop/stock/output/currency,
   one-unit price and commitment; no arbitrary action can spend the owned budget.
3. Settle via the original supporting-transition API using the support-step
   portfolio but the claim owner's Teacher decision as `route_source_decision_id`.
   Neutral steps rebind only this owner's surviving claims. Other roots are
   preserved and independently supply-validated, not relabeled to the current
   owner; the old service deliberately rejects cross-owner rebind requests.
4. A verified purchase consumes one observed unit-price amount. Reserve only its
   real acquired seed-slot delta with the original material/portfolio services;
   do not reserve existing owned seeds again. The Museum-only output-custody
   consumer contract must not be repurposed or widened for ordinary crop seeds.
   Release any verified surplus/unused purchase budget through the original
   portfolio boundary on completion or cancellation; do not hide it in a projection.
5. Chest staging uses original prepared-transfer/native-slot reservation
   projection and settlement, including shared-slot and chest-compaction effects
   on other claims. A projected intent alone cannot authorize movement of claims.
6. After those consumers are connected, explicitly version/opt in the managed
   production input and remove only the temporary unmanaged-consumer barrier for
   that verified path. Then sample the native acquisition/transfer boundary,
   retaining old failures; do not repeat L1 roots or the13-day E trajectory.

Between a verified native purchase/transfer and its settlement, the old ledger
intentionally still describes the before-state claims. Do not call the ordinary
owned-supply reader or source-refresh validator on that intermediate pair: a
purchase has spent the reserved money and a chest transfer may have compacted
slots. First validate the exact native delta, settle the retained original
ledger through the original service, establish the acquired-slot claim, and only
then evaluate the next step. A lost settlement reply uses the original durable
lookup, not an unsolicited replan or relaxed supply check.

The original global calendar/aggregate budgets/source continuity remain after
this boundary, followed by Provider/model selection and formal manifest/preflight.
L1 stays19/19 and11/11 roots, formal=false; Student fresh-save21/21 remains a
post-training evaluation. No new action-registration count or training gate is
introduced by this plumbing.

## Validation

21 new checks cover owned versus native totals, exact purchase shortfall and
budget, real additional claims rather than unowned inventory growth, another
crop of the same seed/decision, source/actor/slot/quality/type/location/calendar
drift, chest route/transfer, original locked-chest rejection and early refusal of
foreign-player/non-chest external custody. The foreign-player regression was red
before the new consumer guard was corrected. Synthetic test
data is not native evidence. The original13 planner checks are compiled from
their existing source, including the whole legacy first-day progression.
Complete Backend703/703 and Core2442/2442 pass, zero skipped tests. Original full
hermetic and final Release build (zero warnings/errors) also pass. Exact-head CI
is recorded at PR closeout. The final narrow currency projection passes74 focused
checks covering owned supply, all original planner cases and original live-ledger/
installation boundaries; it does not serialize unrelated player domains.
