# Owned Planting Inventory Binding

## Accepted Parent

PR512 merged as077e237ba565716cd209f7ae182b0d1ef1b5fdd8 after42/42 CI steps
in42395289703 (Backend872/Core2442), same-head automatic review without
findings and tested/main tree equality. Original purchase/transfer orchestration
is accepted. This continues its existing owned planting/lifecycle successor;
it does not introduce a new goal, executor or parallel planting implementation.

## Legacy Meaning And Native Authority

`ExecutePlantSeed` originally accepts a seed identity and uses
`FindSeedInventoryIndex` to choose the first matching item. Blame traces that
line through the domain split ac84194d1 to consolidation44c98f4bb in July,
before the current owned crop claims. The bounded historical JSON search found
later source reads/logs, not an original rationale statement. Therefore this
work does not label the old unowned behavior a bug or rewrite its default.

Fresh type decompilation of the actual E runtime's `Stardew Valley.dll` on
2026-11-08 confirms `HoeDirt.plant` resolves the seed and creates the crop,
updates SeedsSown/speed/paddy behavior, but does not decrement inventory.
The existing harness decrements one item afterward and nulls the exhausted
player slot; it does not compact player inventory. The DLL SHA256 is
7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee.
Generated game source stays outside Git. No wiki/example constant replaces
these original functions or the live crop catalog/layout.

## Additive Exact Slot Contract

`CropTeacherSeedPlantingSupport` reuses the original immutable seed source,
complete supply validation and original independent marker-reply replay. It
requires settled player custody on the original planting day and an unchanged,
unique original layout. No pending transfer is cleared to make planting fit.

The marker binds a subset of the original plant batch to one existing owned
player claim. It preserves the original maximum8 tiles, stops at that claim's
quantity and never takes another root's allocation or invents a placement.
Each action retains its original tile, seed and exact claim/ledger revisions,
and carries source stack, batch quantity and ordered per-slot offset. Expected
stacks progress mechanically across the batch. One source/ledger marker and one
native queue serve the batch, not one new executor or independent allocation
algorithm per seed. Returned actions/ledger/batch are detached copies.

The original compiler/dispatcher add a branch only for the new binding. It
requires `earn_money`, the active crop owner/purpose, current source and ledger,
the selected native player slot, exact quality/quantity and aggregate claimed
capacity. Whole-queue validation binds original action IDs, order, count,
parameters, actor and primitive. Stripping all binding fields cannot downgrade
a managed marker to ordinary planting; extra native-command overrides also
fail its exact queue comparison. Unowned compilation remains unchanged.

The existing execution-request mapper forwards inventory_slot_index,
qualified_item_id and expected_stack_before/after. Only two optional fields
are added: crop_seed_planting_version and seed_inventory_quality; null fields
are omitted from the old wire shape. The shared inventory contract lives in
Contracts so the native harness needs no new Core/planner dependency.

The original native planting function uses the explicit slot only when this
contract is present. It checks the live item ID/resolved seed, quality and stack
before calling the same `dirt.plant` and `ConsumeOneInventoryItem`. Invalid or
incomplete bindings block without falling back to an earlier seed stack. Owned
post-state diagnostics report the actual selected slot, including exhaustion.
Without the opt-in, first-match lookup and old diagnostics remain unchanged.
Adjacent movement, planting rules, animations and resource effects are not
reimplemented or bypassed.

## Cost And Verification

The complete supply source is verified at its original scope boundary. Per
action checks inspect selected graph/inventory JSON elements, not repeated
deserialization or serialization of the full world/material graph. Source file
hash checks are outside the per-action builder loop. No global/path-based cache,
new snapshot generation or reduction of transparent fields is introduced.

Protocol tests use the accepted transfer fixture to obtain actual-shaped owned
player slots, the original compiler/dispatcher and original execution-request
mapper. An earlier seed stack is deliberately present. Cases cover sequential
eight-use bindings, claim-limited batches, owner/ledger/quantity/source drift,
order/duplicate/stripped/extra-command rejection, exact typed quality/slot data,
source immutability and upstream invalid layout/seed/claim exclusion. These are
synthetic protocol tests, not new actual gameplay evidence. The real E-referenced
harness build has zero warnings/errors and EnableModDeploy=false; no game or
installed mod is changed. Final local/CI results belong to the PR closeout.

The actual ASP.NET compiler endpoint also preserves the eight mechanical plant
actions under the owned goal binding without entering a nested goal Teacher.
Fixtures use the original file repository for preparation/settlement/marker
replies; no test-only repository method or simulated commit result is invented.
Local complete Backend907 and Core2442 passed before the final HTTP/casefold
addition; final focused37/37 passed on the updated code, all without skips.
The final native build has zero warnings/errors. The complete hermetic suite
runs once in exact-head CI rather than being duplicated locally; final full CI
counts/review/tree equality are recorded at closeout. Early fixture date/name
mistakes remain in external logs and were not fixed by weakening date rules.

## Exact Remaining Work

1. Complete the same batch's original native receipt and per-step after-state
   verification, including exact owned-slot deltas, partial successful prefixes,
   complete persistent-resource boundary and original supporting-transition
   material consumption. Reuse existing crop/lifecycle proof readers rather
   than a second crop simulator. Then explicit completion/cancellation releases
   through original APIs, never clearing unverified prepared transfers.
2. Connect that verified consumption to the unique managed runtime owner and
   its original journal/readback/recovery pipeline. Current generic per-item
   runtime readiness is not yet enabled for owned planting: after the first
   item its fresh native state differs from the compiled batch source while
   the ledger remains bound to the original batch. Handle the verified prefix
   in the new consumer; do not relax old source binding or silently convert the
   batch to single-seed queues to make an isolated test pass.
3. Only after that, explicitly version/opt in and collect a small real native
   transfer/planting boundary sample. Do not repeat accepted13-day/L1 evidence.
   Continue existing joint calendar/budgets/continuity, Provider/model IO and
   formal manifest/preflight afterward.

Automatic managed activation stays off. L1 stays19/19 and11/11, formal=false;
Student fresh-save21/21 is post-training evaluation. No worker, model API,
training, server deployment or game run is authorized by these protocol checks.
No generated source, raw snapshots/journals/receipts or local-data is committed.
