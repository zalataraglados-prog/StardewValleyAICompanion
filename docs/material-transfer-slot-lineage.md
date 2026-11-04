# Material Transfer Slot Lineage

## Native Rule And Existing Defect

The locked local 1.6.15 decompile was read directly: `Chest.grabItemFromChest`
removes the selected item and calls `clearNulls`; `Chest.addItem` compacts before
stacking/insertion; `grabItemFromInventory` also compacts. `clearNulls` delegates
to `RemoveEmptySlots`. Conversely, `Farmer.removeItemFromInventory` sets the player
slot to null without compacting the other player slots. `ItemGrabMenu` invokes
those native handlers during its existing right-click transfer path.

The original executor kept clicking `intent.SourceSlotIndex` and reading that
same index after every click. A sparse source chest could compact its selected
stack after the first unit, so a later click/postcondition addressed the wrong
slot. The independent support receipt had the same original-index assumption;
it also read a deposit destination's pre-compaction quantity at its new index.
Node quantity totals alone cannot detect unrelated reserved items moving slots.

## Shared Projection And Native Input

The existing `MaterialTransferProjector` still owns native insertion and capacity
rules. It now exports optional `native_slot_transitions` for the two affected
inventories: original/final index, exact item/quality/runtime and before/after
stack. Removed/inserted ends use explicit null indices. Unrelated occupied slots
are included because native chest compaction can move their reservations too;
the rest of the world/material graph is not copied. Duplicate indices or invalid
zero-stack slot entries block instead of producing a guessed map. Legacy projection JSON
without this optional field remains readable and is not silently certified.

The original native executor retains the exact selected `Item` reference and
locates that same reference on each click. It does not search for an arbitrary
matching item, rewrite inventory, add another transfer engine or capture a
snapshot per tick. A missing/duplicated reference or changed stack fails closed.
Original menu, mutex, owner, quantity, release and timeout checks remain in force.

The result and plan-execution episode preserve the actual remaining source slot
in optional `material_transfer_source_slot_after`. The existing source fact now
reports the node's real quantity delta, not a misleading stack delta at a stale
index. Original intent and full typed slot lineage remain available for exact
source binding, including a completely removed stack.

## Independent Receipt

The existing supporting-transfer receipt rebuilds the projection from its
before graph once, then checks both affected post-inventories against it. It
resolves the selected source's final index and destination's original index
through that map. A removed source is not confused with a different item shifted
into its old slot. Missing nodes, owner/quality/runtime/quantity drift and changes
to unrelated stock block even when transferred node totals happen to match.

This is physical slot-lineage validation, not permission to release or move a
reserved claim. The caller must independently rebuild the expected projection;
an externally supplied map alone is not a native receipt or authority.

## Verification And Remaining Work

Focused Core tests cover sparse partial/full withdrawal, deposit compaction and
split insertion, preserving player holes, unrelated stock/slot drift, malformed
graphs, explicit null/legacy serialization, and the actual independent receipt
method. Full game-free Core passes 1083 tests and the original Backend suite
passes 381 tests. Harness builds against the
explicit E runtime without deployment. All local work uses single-core,
BelowNormal execution during quiet hours; no game was started.

The existing `self-test-machine-material-transfer-terminal-coverage` now accepts
an isolated `--output-root` fixture mode as well as its prior explicit
`--execution-inputs` mode; supplying both is rejected. It reuses the original full
static/axis fixture and canonical transfer -> settlement -> terminal continuation
proof, including its tamper refusals. It does not rerun the entire collection
matrix, and CI now runs it directly. Local canonical proof and original control
support rebind (20 checks) pass. Earlier missing-input/wrong fixture setup attempts
were not counted as proof; their local diagnostics were not deleted.

This does not complete museum custody relocation/rebind or promote coverage or
training. That work must consume retained acquisition provenance, bind the
authorized candidate/compiler/dispatch, move all affected reservations atomically
without changing unrelated goals/quantities, and verify fresh native transfer
before donation. The existing custody rejection guards remain enabled.

The current-geode acquisition/custody/donation/consumption/night/key trace is now
[accepted](geode-dialogue-lifecycle.md#accepted-native-boundary); do not repeat it
as the next task. First put the legacy transfer smoke under the shared isolated
save, dedicated ledger, exact mod allowlist and owned hidden-process contract,
and use `--skip-training` instead of its historical training path. Then complete
the sparse-chest native regression before opening custody relocation/rebind.
Sparse-chest native regression is
still required separately; build/game-free tests do not certify it. The legacy
`Invoke-RuntimeMaterialTransferDailyPlanSmoke.ps1` points directly at
`RuntimeRoot/saves`; do not run it against a retained baseline. Any reuse must
first use the shared isolated-save/owned-process contract and preserve source
hashes. The prepared Year-5 baseline remains boundary capability, not pre-Year-3
criterion evidence. Frozen coverage and `formal_product_training=false` stay
unchanged.
