# Crop Resource Reservation

## Scope

R2 L3 reuses the original crop Teacher, crop commitment receipt and reservation
portfolio service. PR503 already makes selection consume unreserved supply.
This artifact additionally allocates the selected plan's current seeds and exact
purchase cash in one planning-ledger revision. It is not another planner, ledger
type, execution path or training authority.

`money_revenue_crop_resource_reservation_receipt.v1` independently rebuilds a v2
Teacher decision through `MoneyRevenueCropProductionDecisionReceiptBuilder`.
It binds the decision/source file SHA, logical source state hash, decision and
commitment IDs, exact seed item, quantities, original atomic request and result.
Verification rebuilds that entire result; leaf flags cannot self-authorize it.
Manual v1 decisions and identity collisions cannot acquire this authority.

The current-stock allocation is `min(selected existing seeds, selected tiles)`.
Only the remainder gets an exact money claim at the verified Teacher price.
Future seeds are not material claims. Existing active reservations stay occupied;
no other root is released or overwritten. A mixed seed/cash request commits via
`ReservationPortfolioLedgerService.Commit`, or yields no accepted result.

Seed slots are allocated by the same extracted routine used by the original
`MoneyRevenueCropSeedReservationCommitBuilder`. The original entry still demands
its entire tile denominator, fresh IDs and exact original context. Its purpose,
claim IDs/order, missing-supply rejection and later native-proof checks remain
unchanged. This is reuse, not a relaxed partial-supply mode for the old entry.

## Commands

All three commands require a new `.json` output; an existing file or directory
is rejected before computation/writing.

```text
build-money-revenue-crop-resource-reservation --decision <decision.json> --output <receipt.json>
verify-money-revenue-crop-resource-reservation --receipt <receipt.json> --output <verified.json>
export-money-revenue-crop-resource-ledger --receipt <receipt.json> --output <reserved-ledger.json>
```

Export independently verifies the receipt before writing its resulting ledger.
The existing production obligation builder can read that ledger: current reserved
seed quantity is credited to the exact crop commitment and the purchase shortfall
remains explicit. An unpurchased item is never reported as current supply.

`resource_reservation_replay_verified=true` means a verified planning-ledger
atomic replay. `runtime_ledger_installed=false` and
`formal_product_training_authorized=false` remain mandatory. The unmodified old
native initializer requires the original decision receipt's exact ledger; do not
replace that ledger with this export and forge the old input manifest.

## Evidence

51 focused checks (14 new) cover mixed cash/stock ownership, unchanged other roots,
competitive allocation rejection, all-owned/all-purchased supply, original-plan
consumption, independent replay, seven receipt mutations, missing balances,
manual decisions, forged Teacher cost and existing-ID overwrite rejection.

Retained real current-v6 source offline Build/Verify are byte-identical:
`830a118a07c060adadf47cd57e60f05ddf617cd437ad5c5432b7b846046be0aa`.
It reserves400g, still requires5 seeds and claims no future material or runtime
installation. The original E native seed proof rebuilds byte-identically:
`e128dc0a7db8f3d8b8cbbd134f5b81542895a906cad327a6844462a81a7b0671`.
Three public CLI overwrite probes reject; all bound original files, E checkpoint
and original failure remain unchanged. No game was started. Raw reports/callers
are outside Git under `I:\StardewAITraining\money-resumption-20261107\atomic-resource-checks-a`.
All2442 Core tests and original complete hermetic pass with zero skipped tests;
final CI is recorded at PR closeout.

## Successor

### Current Ledger At Crop Handoff

The live crop handoff now reads the complete backend ledger once and passes its
immutable path into the original Teacher request. Dispatch records its path/SHA;
the original optional-argument/default path remains unchanged. Donation still
uses its original candidate gate, formatting and immutable ledger evidence store.

If the backend ledger already matches the captured source, its identity and all
active material/currency reservations are checked without a mutation. If stale,
the same original validators check current reserved slots, quantities, balances
and actor identity before requesting a marker-only portfolio commit. No resource
claim is added, cancelled, consumed or relabeled. An active crop must continue
its complete original commitment/layout rather than starting an empty ledger.

The original portfolio service advances one revision and anchors only the ledger
root. Its result is independently replayed through the original commit-evidence
verifier, including full ledger/history equality at the actual recorded time.
No direct SourceStateHash assignment or repository reconciliation change is used.
The original durable journal/lookup transport is shared with settlement: a lost
commit reply triggers one bounded lookup of the exact request, never another
commit. Legacy settlement keys and behavior remain unchanged. A rejected result,
altered claim/history/revision or untrusted lookup fails before Teacher dispatch.

20 new Backend checks include a real file repository commit/reload/lookup, fresh
and stale handoff, complete-resource forwarding to the original dispatch builder,
actor/supply/active-crop refusals, source mismatch, no-replay recovery and legacy
settlement compatibility. HTTP loss is scripted; this is not native-game evidence.
No new game snapshot is captured by the ledger bridge and it runs only at the
existing planning handoff, not per frame or action. Complete regressions/final CI
are recorded at PR closeout: all2442 Core and649 Backend tests, original complete
hermetic and19 original PowerShell transport checks pass, zero skipped unit tests.

This closes existing-ledger source binding, **not installation of PR504's newly
selected crop/resource claims**. Its runtime_ledger_installed flag remains false.

### Remaining Runtime Installation

Before managed native execution, connect this source-bound ledger through the
original installation/commit boundary and original supporting-transition
settlement. Neutral route steps must preserve/rebind active claims; each one-unit
purchase must consume only its exact money cost and establish custody of its
actual acquired slot. Existing reserved seeds must not be double-reserved after
purchase. Chest-held supply, if selected, must use the original inventory-transfer
and claim-relocation chain before planting. Version/opt in the new input path;
do not weaken the old initializer, source proof or complete-seed guard.

Then compose the original daily aggregate budget/calendar with selected-source
continuity for R2. Provider/model selection and formal manifest/preflight follow;
Student fresh-save21/21 is a post-training evaluation. L1 stays19/19,11/11 and
formal training stays false.
