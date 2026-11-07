# Owned Crop Seed Purchase Settlement

## Accepted Baseline

PR507 passed all42 CI steps in42372731074, merged asf85bbfcc4ddcb67aa63db0e880f2032843cead05,
and matched the tested tree586ba2ea57402436fec8294b4e25e6d539ce5f41.
Its owned planner input and PR506's explicit installer are retained. Neither
enabled automatic managed gameplay or formal training. No accepted L1 or E
trajectory needs to be recaptured for this accounting work.

## Existing Native Verifier And Ledger Service

`CropTeacherSeedPurchaseSupport.Open` binds the current source, complete ledger,
commitment and existing run-request layout/acquisition intent. It checks the
original tile denominator, seed, shop, price cap, planting deadline and full
owned shortfall budget. Owned chest staging precedes shopping. This is a
mechanical settlement scope, not independent proof of Teacher/run-plan eligibility;
the existing upstream plan loader/compiler remains responsible for dispatch.

Source/run-request/base-ledger files are hash-bound. A new support-step marker
uses the original portfolio service with portfolio/source-decision IDs both equal
to the step ID. Initial resource IDs and Teacher-owned claim identities are not
changed. Its entire returned result must independently replay through the original
commit evidence verifier, including all other roots and history.

`MoneyRevenueCropSeedAcquisitionProofBuilder.BuildSupportSlice` reuses the original
queue continuation, purchase, neutral-stage and per-step persistent-boundary
validators. It checks exact queue/run/executor/candidate, source actor/day,
shop/stock/output/quality and price scope, observed money/quantity changes and
native step snapshots. Whole-chain `Build` keeps its original shortfall,
continuity and manifest requirements. Only identical transition-record
construction is shared; no old verifier or executor is relaxed.

After verification, `VerifyTransition` writes a new compact proof with create-new
semantics and flushes it to disk before exposing the settlement request. It never
overwrites an existing proof. Queue/source/receipt/after/proof artifact hashes
are checked at later mutation boundaries. A standalone slice is not a whole
acquisition proof, policy label, terminal receipt or training authorization.

After interruption, explicit `ResumeTransition` revalidates the original native
inputs and requires both the journal's expected proof hash and exact rebuilt
proof equality. It only reads retained evidence; it does not overwrite a proof,
resend a mutation or create a duplicate recovery artifact.

## Actual Debit Before New Ownership

`CropTeacherSeedPurchaseSettlement` builds an original supporting-transition
request with the unique step marker, but the Teacher claim owner as
`route_source_decision_id`. Neutral steps rebind only this commitment's seeds and
purchase cash. Purchases consume one actual observed unit-price amount, not the
maximum quote or the player's whole wallet. Fully consumed cash is not rebound;
surviving partial cash is. Other owners' claims are neither consumed nor relabeled.

The original `SettleSupportingTransition` independently replays the complete
actual returned result at its real ledger timestamp. Only then is all active
resource supply checked against the after-state. The intentionally stale
before-ledger is not fed into the ordinary supply reader between native debit
and settlement.

One `CropTeacherSeedPurchaseCustody` retains this verified settlement, so building
the next request and verifying its reply do not repeatedly deep-validate a mutable
HTTP result. Exposed ledgers, proofs and requests are defensive copies.

Acquisition compares native before/after player slots. Existing seed slots may
not disappear, move or shrink during a purchase; positive deltas must match the
verified output amount/quality. Only the actual delta up to the outstanding crop
tile requirement is claimed. An existing own claim in that slot is increased by
the delta using the original upsert service, not reserved a second time. A new
slot gets a new claim. Preexisting unclaimed seeds and another root's share do
not become this crop's property. A stock bundle's unused excess stays unclaimed.

When the tile requirement is filled, the same original acquisition portfolio
commit explicitly releases remaining purchase cash. Its full result is replayed
through the original commit evidence verifier, then all current resource claims
are checked. No Museum-only output-custody contract is widened for seed purchases.

## Remaining Runtime Integration

This is an offline-tested transaction kernel, not an enabled managed runner.
The existing automatic handoff still rejects active resource claims. Do not
remove that barrier just because this helper accepts a synthetic/native slice.

1. Wire this kernel to the original run planner, daily-plan compiler, native
   queue/receipt path and original journaled portfolio/support settlement APIs.
   Use original fixed-purchase goal isolation, not nested global Teacher selection.
   Persist scoped inputs and actual replies; verify ledger readback. Lost replies
   use original exact-request durable lookup, never a resend or synthesized result.
2. Reuse original prepared-transfer/projection/settlement for owned chest seeds,
   including other claims shifted by chest compaction. Connect owned planting
   slot consumption and lifecycle/cancellation release boundaries; inspect existing
   consumers first rather than create a second executor.
3. Explicitly version/opt in the managed production entry only after those
   consumers are connected. Sample actual purchase/transfer native boundaries;
   do not repeat L1 or the13-day E acceptance merely for new bookkeeping.
4. Continue existing joint calendar/aggregate budgets/cross-root source continuity,
   then Provider/model IO and formal manifest/preflight. Student fresh-save21/21
   remains a post-training evaluation. L1 stays19/19 and11/11 roots, formal=false.

## Validation

28 new protocol checks reuse the original native queue/receipt fixture. They
cover actual versus cap price, exact/partial cash, consecutive purchases, shared
slots, fresh slots, stock-bundle surplus, neutral wait and native route graph
location, unsupported scope, immutable inputs/copies, create-new proof output,
expected-hash-only interruption recovery, and complete-replay rejection of altered
other-root state at all three mutations.
The old fixture additionally accepts current typed currency rows while preserving
its legacy array behavior. Fixture evidence is synthetic, not native gameplay.
Local Core2442/2442 and the original complete hermetic suite pass. Backend728/728
passed before the final three guards; the final731-case run passed730 and rejected
one reused create-new fixture export directory. A fresh export directory rerun
passed all28 new checks plus both original affected modes (30/30), without changing
production code or deleting retained diagnostics. Exact-head clean CI is recorded
at slice closeout.

The retained E native acquisition request was rebuilt through original whole-chain
`Build`: its original proof remains unchanged and the rebuilt bytes are identical,
SHA256 `5419dec84aa5bb24b7994c3e29ed9c6f03a6b0271ecf8193c29c195403892dfa`.
This is compatibility replay, not newly sampled native evidence.

No model API, worker, game, server or formal training was run for this slice.
Raw files and test artifacts stay outside Git; `local-data` is not staged.
