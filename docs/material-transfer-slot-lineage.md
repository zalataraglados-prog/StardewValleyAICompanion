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

### Isolated Native Regression Entry

The legacy transfer smoke now uses the shared runtime-evidence helpers rather
than selecting the latest baseline save in place or running implicit builds.
It requires an explicit source slot, verifies the copied save, starts a dedicated
revision-zero actor/save ledger, permits exactly Bridge/Harness mods, suppresses
render/audio and owns/releases only its own hidden processes. Every build uses
explicit E game references, one build node and no shared compiler/deploy/zip;
execution uses the existing one-candidate daily-plan adapter with `--skip-training`.
Before launch, `run-inputs.json` binds clean script source, one consistent runtime
source revision, exact own-DLL/game hashes and the mod allowlist. A retained build
is accepted only if the compiled-source/configuration diff is empty; a mixed or
unattributable bundle is rejected rather than forcing a redundant docs-only build.
The old training-dataset assertions/claims are removed. `KeepGameRunning` is
rejected before launch because this verification entry must release ownership.

Its optional training-only `fixture_material_transfer_profile` is omitted from
legacy request JSON when null. `ordinary` preserves the old fixture;
`sparse_source` starts with eleven quality-4 wood in slot 5 and three ordinary
stone in slot 9, with all other slots empty. Fixture mutation ends before the
measured boundary. Two native right-click transfers round-trip a partial
multi-unit quantity, and the original C# projector is independently rebuilt
before its original `VerifyNativeSlots` checks both complete affected inventories.
The selected source must move 5 -> 0 and unrelated stone 9 -> 1 without changing
identity/quantity; a stale projection must be blocked with zero clicks and no
inventory mutation. This is physical lineage, not permission to move custody.

Direct locked decompile also confirms `InventoryMenu.rightClick` reads
`Game1.oldKBState`: LeftShift would transfer half a stack. The original executor
now neutralizes that in-game old-keyboard snapshot during the synchronous native
click and restores it in `finally`. It does not inject/release OS keys or change
the rest of the input frame. The native receipt still verifies actual quantities.

The shared logical JSON reader adds optional `-Raw` output from the same
verified reader, retaining the default object mode. The transfer adapter uses
this original text instead of reserializing snapshot numbers/timestamps.
Twenty game-free checks cover actual preflight refusals (including traversal
and recursively nested copy targets), unchanged plain and
content-addressed read modes, corrupted-blob rejection and the original
projector/verifier's stock/slot/owner/projection/intent refusals. Backend 384/384
passes, including optional-wire compatibility and source boundaries. The focused
Core material-transfer/content-addressed regressions also pass 24/24. These
checks and the E-path Harness build alone do not certify the real sparse scenario;
the accepted clean-source native run is recorded below. A Year-5 source is
allowed for this shared mechanical capability, never for a pre-Year-3 criterion.

The first clean `7a1d6f35` native attempt verified sparse setup but stopped before
any measured transfer: the test requested its actor ledger before ingesting the
fixture snapshot, so the existing Backend correctly returned unknown-state 404.
The entry now ingests that exact original snapshot before ledger lookup; lookup
errors are immediate rather than retried as if they were startup health. The
ordering is guarded by regression. This is an orchestration correction, not a
native transfer/compiler failure; the retained runtime may be reused only after
the same source-equivalence/hash checks. The sparse round trip was still pending
at this attempt.

The `ee2f76fa` script with the verified `7a1d6f35` runtime bundle reached native
execution but its existing X override never opened the chest menu: zero clicks,
source wood still eleven, lock released. Direct decompile confirms a player
chest's `checkForAction` requires `didPlayerJustRightClick(true)`, then owns its
normal mutex/lid animation/menu path. The original state machine now reuses the
already existing scoped `NativeRightClickEdgePatch`, calls that native object
method once and clears the scope in `finally`. It rechecks live adjacency,
identity, control, owner and lock, excludes giftboxes, and still waits for the
real menu and native lock. It never calls ShowMenu directly, acquires the lock
itself or changes inventory. The obsolete key-release state is removed rather
than retained as a second opening path. Both obsolete terminal X-release calls
were also removed. The new clean runtime run is accepted below.

### Accepted Native Boundary

`material-native-open-1104` used clean script and runtime source
`0cdc3d8daaf815af999e6cff5c6bf1745095c252`, with four explicit E-path builds,
zero build warnings/errors and one dedicated actor/save ledger at revision zero.
Both original daily-plan transfer directions were applied and independently
verified: two native menu/lock lifecycles, two unit clicks per direction and four
clicks total. Wood moved from source slot 5 to slot 0 after the first native
withdrawal, unrelated stone moved 9 to 1, and the round trip restored eleven
quality-4 wood and three ordinary stone. The bad projection was rejected with
`material_transfer_source_projection_drifted`, zero clicks and both complete
affected inventories unchanged.

The independent review rebuilt both projections through the original C#
`MaterialTransferProjector.Project` and checked full slots using its original
`VerifyNativeSlots`; it rechecked all retained DLL hashes, the source save and
released game/ports. Local raw evidence remains outside Git under
`I:\StardewAITraining\material-transfer-isolation-20261104\native\material-native-open-1104`.
`independent-native-review.json` SHA-256:
`b470d245ed7d7bce10f090cc7ffdeb74d14b24574f99658cca30974d1e29c671`.
Original source directory SHA-256 stayed
`93153ed126876c240d963a484ac6bb6c880e16b4999c26c5502271721f2be79e`.
The final focused executor/inventory tests passed 7/7. The earlier game-free20,
Backend384 and focused Core24 are separate checks, not native action counts.

This accepts only shared native physical slot lineage on the isolated Year-5
baseline. It is not custody relocation authorization, a pre-Year-3 criterion,
Teacher/global/fresh-save completion or training. No frozen coverage count changes.

### Reservation Map Before Custody Settlement

The original `MaterialTransferProjector.ProjectReservations` now derives the
complete active-claim map for both affected inventories from its original native
physical projection and an explicit set of whole-claim moves. Each map entry
binds the original claim ID/revision, old/new node/slot, item and unchanged
quantity; the result also pins the ledger ID/revision. Unmoved claims follow
their original native slot lineage, including other goals, destination stacks
compacted before insertion and the source stack retained after partial withdrawal.
Unchanged positions are included; unrelated inventories and inactive claims are
not rewritten. No graph, claim, custody provenance or ledger is mutated.

The map rejects missing/duplicate claim identities, invalid claim revisions, owner or
history-backed custody, partial-claim moves, moving more reserved quantity than
the transfer, taking an unselected goal's retained units, and allocating claims
against pre-existing destination stock rather than the actual incoming native
contribution. Whole claims may target different native insertion slots; a claim
is never silently split or reassigned to an arbitrary compatible slot. Native
identity is a signed long, not a positive counter: locked `Farmer` initializes
the ID with `Utility.RandomLong`, which uses eight random bytes and `ToInt64`.
The retained real actor's negative ID exposed and corrected an invalid positivity
check; signed extrema/zero and exact ownership matching are covered by tests.

Focused original-projector/claim checks pass 56/56, with 32 new cases; the final
complete game-free Core run passes 1155/1155, and original Backend
portfolio/transfer checks pass 94/94. The retained native before/after
snapshots were separately replayed with fifteen synthetic full-stock claims in
each direction. Both independently rebuilt native slot projections and all
predicted claim locations/quantities match the real post-inventories. Local
`offline-reservation-lineage-review.json` under the accepted native evidence root
has SHA-256 `da2ddf66822823df2576a40e7186ff81bf880f9f0313e7f5f4973547fef07ed7`.
This is offline synthetic reservation evidence over an accepted native trace:
zero new game actions, live ledger mutations or training samples.

`projected` is not authorization or native settlement. This helper has not yet
been connected to the production custody path, and both existing custody
relocation/rebind rejection guards remain enabled. Continue the same lifecycle:

1. Bind the complete map to the original canonical support request, compiled
   queue, exact dispatched/native receipt, actor and retained acquisition source.
   Independently rebuild it from the retained before graph and current ledger;
   caller-supplied slot maps are not evidence on their own.
2. In the original supporting-transition transaction, validate every affected
   claim and full fresh native slots, then clone/update all affected locations
   atomically in one ledger revision. Preserve goals, quantities, original
   acquisition custody and creation history; any mismatch leaves the input
   ledger untouched. Other-goal passive compaction is not consumption authority.
3. Carry the exact custody/ledger revisions through the existing candidate,
   daily-plan compiler and dispatch checks, with fresh replanning after transfer.
   Do not replace the original transfer executor or add a second ledger.
4. Validate real acquired custody deposit -> withdrawal -> authorized donation
   -> original consumption, including unrelated-goal preservation and stale,
   forged or reply-loss refusals. Only then replace the corresponding rejection
   guards with that verified lifecycle. No criterion/Teacher/training promotion
   follows from the map or from the earlier sparse fixture alone.

### Prepared Atomic Settlement

The original portfolio commit/supporting-transition service now has an optional
prepared ordinary-transfer path. This is necessary because Backend intentionally
retains only two full snapshots: a move/transfer queue can evict its original
before snapshot before settlement. The snapshot limit is not increased and the
client does not supply a replacement before graph.

An optional `material_transfer_preparation` on the original portfolio commit
identifies the native intent, explicit whole-claim relocations and route source.
After normal portfolio validation/staging, the server derives the complete map
from the ingested source snapshot and committed claims. It retains exactly the
two physical inventories and their access points in one optional
`prepared_material_transfer` inside the same ledger. The record binds source
state/tick, actor via graph/ledger identity, goal/portfolio/route, ledger and row
revisions, the full material-reservation metadata hash, intent and complete map.
It is deep-cloned, hash-bound and anchored by `material_transfer_prepared` history
in that same commit revision. Normal portfolio claims/budgets/intents retain
their existing validation and are not replaced by a second reservation store.

Settlement references `prepared_material_transfer_sha256`. Under the original
repository lock it validates the retained origin/history and pinned metadata,
independently rebuilds the map, and verifies both full current native inventories
using the original projector/verifier. Other inventories' active reservations
must still fit the final full material graph. Only then does the original
transaction clone/update all affected material locations/revisions/source hashes,
preserving goals, quantities, decisions and other metadata. Selected relocation
and passive compaction are recorded at one new ledger revision; no goal is
consumed or marked terminal. The result carries the complete optional
`material_transfer_reservations` map. Successful publication removes the compact
preparation; history retains its hash, not repeated graphs.

A failed check, including final supply validation after staging, returns the
original ledger and keeps its preparation. A pending preparation cannot be
overwritten, silently omitted, or bypassed using another portfolio's marker for
a legacy relocation. Ledger/metadata drift fails closed; this is not automatic
cancellation, replay or permission to discard unresolved preparation. The
existing atomic file publish and one-read mutation-reply recovery cover the new
optional fields without re-executing native work. Legacy requests/ledgers/results
omit the new fields when unused.

Service and actual Backend HTTP/file-repository tests cover ordinary transfer,
other-goal compaction, same-stack retained units, exact rollback, missing/forged
bindings/history, independently rejected rehashed maps, revision/metadata drift,
repeat refusal, restart and before-snapshot eviction. HTTP tests inject minimal
synthetic snapshots through StateStore; they do not certify Bridge ingestion or
new native gameplay. Lost preparation replies are retrieved read-only rather
than POSTed again, and settlement reply lookup preserves the published file.

**Production boundary:** Backend/service support is implemented, but the original
canonical acquisition producer, commit/settlement receipt reconstruction and
candidate/compiler/dispatch path do not yet emit/consume this preparation.
Connect those original paths next; do not build a parallel orchestrator. Both
selected custody and passively affected custody are still rejected, including
an ordinary resource transfer that would rebind another custody row. The real
acquired-output storage -> withdrawal -> authorized donation/consumption proof
and custody guard replacement remain later in this same lifecycle. All frozen
coverage, Teacher and formal-training counts remain unchanged.

Verification: final full Backend 414/414 (30 new cases), complete game-free Core
1155/1155. The retained native `material-native-open-1104` snapshots were also
read through the original validated logical reader and replayed against the
actual file repository with fifteen synthetic full-stock reservations. Both
directions prepared exactly two inventories, settled fifteen affected claims in
one revision and removed preparation, publishing revisions 4 and 6 with a
repository restart between directions. Local compact preparations/results and
`review.json` are under
`I:\StardewAITraining\material-transfer-isolation-20261104\atomic-retained-replay-4d51d446ac144446b1aea72081a40219`;
review SHA-256 `cc0efbc37b27ad6ff504cf0350119dbfea8006b93c2767cf28c054fb2aee03dc`.
This is offline synthetic claim/transaction evidence over an accepted native
profile, not a new native execution, canonical acquisition proof or custody
authorization. No raw snapshots were copied by this replay or committed to Git.

One local full run exceeded the existing 15-second outer deadline in the old
museum malformed-reply recovery test under the two-logical-CPU throttle. The
unchanged recovery group subsequently passed 14/14 and the complete final suite
passed 414/414. No deadline or production retry policy was widened; retain this
as a resource-sensitivity observation, not a waived failure or a new game bug.

Earlier focused Core tests cover sparse partial/full withdrawal, deposit compaction and
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
as the next task. The isolated smoke entry described above has replaced the
unsafe baseline-in-place flow, and the sparse native boundary above is accepted.
The next work is complete atomic custody relocation/rebind through the existing
ledger/supporting-transition path, retaining acquisition provenance and all
affected other-goal reservations; the protection guards stay closed until that
contract is implemented and verified. The retained source under `RuntimeRoot/saves` is read and copied, not
the execution destination. The prepared Year-5 baseline remains shared mechanical
capability, not pre-Year-3 criterion evidence. Frozen coverage and
`formal_product_training=false` stay unchanged.
