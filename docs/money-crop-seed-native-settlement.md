# Owned Crop Seed Native Settlement

## Scope And Accepted Parent

PR510 merged as088b932b182a3a16fd0cd02f826c1c946d76d982 after42/42 CI steps
in42386221700 (Backend805/Core2442), same-head review without findings and
tested/main tree equality. Its conserved preparation/dispatch binding remains.
L1 stays19/19 criteria and11/11 roots; formal=false, with no L1/E resampling.

This slice connects a verified preparation to native-format queue evidence and
the **original settlement kernel**. It does not start a game, enable automatic
managed dispatch, authorize training or claim a new actual gameplay sample.
The fixtures are synthetic protocol evidence, not a substitute for opt-in native
acceptance. Original executors, the shared persistent invariant and old machine/
Museum contracts are not rewritten to fit this consumer.

## Bound Queue And Native Outcome

`CropTeacherSeedTransferMarker.ValidateQueue` requires the original one-transfer
queue, clean compiler/item diagnostics, original supported actor profile, source,
crop ownership and prepared binding. The original dispatch evaluator still
checks the execution intent/projection actually consumed by the Live executor.
One parsed queue is retained for verification, not loaded separately per check.

`VerifyTransition` consumes an explicit `owned_seed_transfer` request with the
expected run/executor/candidate and the physical queue SHA locked by its caller.
It uses the original complete `QueueExecutionReceiptValidator`, full source/after
snapshot hash validation and exact preparation source. Same actor/save/day,
planting deadline, monotonic clock and envelope clock agreement remain required.
The original per-step after file must be inside its receipt directory and match
the actual terminal after-state. A distinct step file is independently retained
and hash-bound; a receipt cannot alias the queue or before file as that step.

The original native transfer implementation moves only within the same map,
forbids removable-obstacle clearing, closes the actual chest menu and waits for
native lock release. The new proof does not synthesize any of those operations.

## Exact Resource Boundary

The original `MaterialTransferProjector` and its `VerifyNativeBeforeSlots` /
`VerifyNativeSlots` establish every involved source/destination slot, type,
quality and quantity. Multiple destination allocations are all retained. The
new comparison adapter materializes only those original projected transitions
onto raw material-graph templates; it is not a second insertion algorithm.
Other inventories, metadata, access authorization and unknown graph fields
remain unchanged.

Publisher-derived changes are checked, not blanket ignored:

- Selected seed aggregate quantities agree with all original authorized/
  restricted slots. All four quantity buckets are conserved; `source_slot_count`
  follows native merging/splitting/depletion.
- Chest occupied/free counts and the empty/nonempty hit/relocation affordances
  follow the original publisher's rules. Existing source echoes must already be
  correct; an after-state cannot repair a bad source silently.
- Raw player inventory agrees with the complete native graph. Existing items'
  metadata and untouched empty slots are preserved. New item fields shared with
  the chest slot must match; additional new-item fields remain observed in the
  original after artifact, not invented from unavailable source properties.
- Available capacity echoes use the actual `max_items`, `occupied_item_stacks`,
  `empty_slots`, `has_empty_slot` names. The maximum is unchanged; occupancy can
  change. Available seed echoes are checked on **both** sides against native
  slots/raw inventory and the original crop catalog's resolved commitment ID.
- The actual storage field is `farm.chests`, whose value schema is
  `storage_infrastructure.v1`, not a new `farm.storage_infrastructure` field.
  Its source rows/counts must match the original canonical graph/scope. Only
  corresponding projected rows and derived empty/shove counts may change.
- Money, all published shop currencies and any wallet field are unchanged.
  Optional unreadable echoes may only remain byte-semantically unchanged after
  the original nonsemantic read-metadata normalization; they are never guessed.

The original expected-resource persistent invariant is then reused. Only an
in-memory comparison view substitutes the already independently verified
storage echo value; real snapshots/hashes are never rewritten. All other
persistent fields keep the original rules and unknown fields are not dropped.

Source authority: `FarmReadAdapter.MaterialInventoryGraph.cs` (native insertion
graph, quantity grouping, access/storage echo), `PlayerReadAdapter.cs` (raw and
seed inventories/capacity), `CropCommitmentLedgerService.cs` (catalog seed ID)
and the existing `ModEntry.MaterialTransfer.cs` native executor. No wiki/example
constants or new bridge fields are needed for this mechanical transition.

## Original Settlement And Recovery

`CropTeacherSeedTransferSettlement` holds one verified outcome. Its request uses
the original prepared SHA and every exact relocation. The original service
also rebinds all passively affected claims through chest compaction, preserving
their original owners/purposes/quantities. Surviving own claims outside that
projection and owned cash are rebound, not consumed. Other-root cash and
outside-node claims remain untouched. No new seeds are minted or reserved.
Unused cash release belongs to the explicit lifecycle owner, not an implicit
transfer-side discard.

A clone-only original-service preflight and full after-supply validation precede
proof publication. Only a real durable settlement reply, independently replayed
in full at its actual returned timestamp, establishes the new ledger. Then the
original owned-supply reader validates the settled positions. The actual runtime
owner must also perform a complete final ledger readback; it is not implied by
this kernel. Caller-visible proof/request/ledger values are detached copies.

Proofs use create-new plus disk flush. Physical native/source hashes are merged
by exact path to avoid repeatedly hashing the same large snapshot within one
guard phase; no integrity check is dropped. Path aliases follow the actual
Windows/Linux case rules, so a distinct Linux step file cannot be skipped as an
after-file alias. Each later request/reply boundary
rechecks the immutable artifacts. Resume requires the expected physical proof
SHA and exact original native rebuild, and reads rather than overwrites it.
Original durable mutation lookup can recover the published settlement after
restart without resending it. That lookup alone is not native-outcome authority.
Incomplete/partial native execution is not relabeled completed or rolled back.

## Exact Next Work

Runtime integration continues in [Owned Seed Shared Runtime](money-crop-seed-shared-runtime.md).
The following list is the accepted kernel's original handoff, not a request to
reimplement completed preparation, proof or settlement.

1. Connect these accepted transfer scopes to the original control/compiler/native
   queue path and original journal/lookup callbacks, including full base/prepared/
   final readback and candidate identity locked **before** execution. Reuse the
   purchase transaction's common orchestration instead of copying a second
   retry/readback/transaction owner. Automatic activation remains off.
2. Connect existing planting to exact owned-slot consumption and explicit
   completion/cancellation release, preserving legacy unowned behavior. Check
   existing pending-transfer recovery/cancellation APIs before adding anything;
   never clear an unverified or partially applied preparation blindly.
3. Finish the unique managed activation/resume owner. Then explicitly version/opt
   in and run a small actual transfer/planting boundary sample, not another13-day
   or frozen-L1 resample.
4. Continue joint calendar/aggregate budgets/cross-root continuity, Provider/model
   IO and formal manifest/preflight. Student fresh-save21/21 is post-training
   evaluation; none of these protocol checks authorizes formal training.

Final local/CI counts and the one retained old timeout/rerun are recorded in the
PR closeout. Raw artifacts and `local-data` remain outside Git. No worker/model
API is required; API peer cooperation stays disabled until credits are verified.
