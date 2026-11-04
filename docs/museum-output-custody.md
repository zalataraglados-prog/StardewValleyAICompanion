# Museum Output Custody

## Shared Native Interaction Timing

The original candidate, day plan, primitive and native menu/unlock wait checks
now share `Contracts.Execution.MaterialTransferBudgetPolicy`. It preserves the
runtime's 180/120 tick waits and counts their successful `limit + 1` boundaries,
one click per item and four state transitions: `306 + quantity` interaction
ticks (one item: 307 ticks / eight conservative game minutes). Movement and
interaction are charged separately, not by a one-minute terminal shortcut.
No new executor or Core dependency is added to the runtime harness.

The selected rolling custody now binds a current-day forecast of the full trip
to the chest, original exact stand for local transfer, native interaction and
subsequent museum route. Remote forecasts require the actual selected first edge;
`BuildApproach`'s current single segment is never treated as complete arrival.
This remains a route/timing forecast: fresh physical authorization/capacity,
actual withdrawal and original settlement are mandatory, not granted by a ready
flag. Missing post-clearance geometry/date/timing is explicit; later donation
dates and the real morning key outcome remain pending.
See the [time contract](rusty-key-time-budget.md#complete-custody-interaction-budget).

## One Ledger And One Consumption Path

The existing acquisition settlement builder now derives one museum output claim
from its independently rebuilt execution binding and fresh terminal item gain.
The claim selects an actor-owned, unreserved inventory unit from the fresh
material graph. Input claims completed by this same transaction are excluded
from the projection; unrelated claims remain protected. Unknown/unverified gain,
missing graph or unavailable output fails closed.

`SettleCompletedRoute` completes input claims and reuses the existing portfolio
commit service to retain the output in the same ledger revision. Failure leaves
the original ledger untouched. The output is an ordinary active material
reservation with typed acquisition/consumer provenance and creation history,
not a second inventory store. Its dedicated ownership marker is in that same
ledger. Ordinary material upsert/cancel and generic route completion cannot
overwrite or release the custody row.

The museum candidate generator recognizes only an exact history-backed consumer
authorization. The action compiler re-derives it and checks the actual plan goal;
same goal or a purpose string alone is insufficient. Existing other-goal claims
stay subtracted. Dispatch checks current ledger/actor/state, exact row revision,
slot/item, unique authorization parameters and spendable quantity. Removing the
custody parameters cannot turn reserved stock into free inventory. Legitimate
multi-value preconditions/safety constraints are not rejected as duplicate keys.

Only leased donation commands carry the new optional normalized `goal_id`.
Unleased/legacy command JSON remains unchanged. New optional custody fields are
omitted when absent; old non-museum settlement requests retain their semantics.

## Native Completion Handoff

After a verified fresh donation, the live loop invokes the shared supporting
transition endpoint, as rain-totem material consumption already does. The new
request builder requires matching receipt/item/state/actor identity, the exact
inventory decrement, museum per-item false-to-true transition and exact donation
counter increase. Only the authorized one-unit custody row is completed; the
other goal's unit is not released. The shared service also requires the museum
completion projection before accepting custody consumption.

The live hook runs only for a leased donation. It projects identity, inventory
and museum fields from snapshots already ingested by the loop; it does not
serialize/hash the full snapshot again or add per-frame polling. Failure uses
the existing shared material-settlement failure marker and sets a retry-required
flag rather than manufacturing a verified training outcome.

## Verification And Boundaries

- Backend full regression: 303 passed; museum/direction Core focus: 87 passed.
- Rolling producer regression: 25 passed, now including its own dedicated lease;
  the separate complete-threshold suite remains 30 passed.
- Focused independent Teacher replay and generated checkpoint check passed.
- Synthetic cases cover atomic rollback, protecting another unit, foreign goal,
  forged/missing/stale authority, wrong consumer, ordinary-write bypasses and
  failed/stale/unchanged native transitions. The actual live method is invoked
  with an explicit HTTP test double which calls the real shared ledger service;
  it tests successful completion and rejected settlement. This is not a native
  game run or a production Backend HTTP trace.

No native game was started for the original slice. It did not promote coverage,
prove full acquired-output custody in gameplay or authorize training. The service
now supports the history-backed position lifecycle and purpose-bound control
below. The dispatch guard accepts only that exact verified control; generic
unbound custody moves remain rejected. The outer rolling selection is connected
below; the [native stored-output round trip](#accepted-native-stored-round-trip) is now accepted within its stated scope. The [native slot-lineage prerequisite](material-transfer-slot-lineage.md)
handles chest compaction in the existing projector/executor/receipt chain;
physical lineage alone does not certify claim relocation. Backend reply retrieval has its separate
[recovery contract](portfolio-mutation-reply-recovery.md#live-material-settlement-hooks):
the actual Museum/Rain Totem live hooks journal before POST and may retrieve one
exact stored reply without replay. Unknown/pending results and missing proof
still stop; the failure flag alone is not recovery.

The subsequent [control admission](control-execution-admission.md) and
[rolling runtime](rusty-key-runtime-rolling.md) slices already integrated the
producer/commit/execution chain and separated safe control from Teacher preference;
the original mandatory-Teacher binding statement is historical, not a current
blocker. The [accepted current-geode native boundary](geode-dialogue-lifecycle.md#accepted-native-boundary)
now verifies actual acquisition/custody/donation/settlement, normal night and
next-morning key event, including an independent rebuild by the original custody
request builder. This supersedes the earlier no-game slice's native-proof gap,
not its one-unit player-inventory limitation. Remaining Rusty Key work includes
relocation, joint windows/budgets,
criterion-local independent Teacher/native partitions and
the pre-Year-3 deadline. The prepared Year-5 boundary is shared capability, not
that deadline proof. The isolated sparse transfer boundary is now accepted.
The original projector's complete reservation-location map and offline replay
are implemented as the next [custody prerequisite](material-transfer-slot-lineage.md#reservation-map-before-custody-settlement),
not custody relocation authority. The original Backend now supports
[compact preparation and atomic ordinary transfer settlement](material-transfer-slot-lineage.md#prepared-atomic-settlement),
including passive other-goal slot rebinds, without retaining more full snapshots.
The original canonical ordinary producer/receipt/compiler/dispatch/settlement
path now consumes that protocol, including complete passive-compaction maps and
Live readiness checks after movement. This is a tested ordinary-transfer
prerequisite, not permission to relabel custody as machine input. The same original service now
conditionally admits selected and passive custody only with an authenticated
initial position and complete linked position history. The original control is
now wired into outer rolling selection and the native acquired-custody
storage/withdrawal/donation boundary below has passed. Legacy/malformed provenance and unprepared generic
relocation/rebind remain rejected. No new planner or executor is needed.

### Initial Position Binding

New custody created by the original acquisition settlement now retains an
optional typed `initial_position`: original player node/slot, source state hash,
reservation revision, version and binding SHA. The same existing creation-history
entry carries `output_custody_initial_position_sha256`; the acquisition settlement
still publishes only one new global ledger revision and one normal custody row.
No snapshot, second ledger or extra creation-history entry is retained.

The shared original custody contract binds that position to save/player/ledger,
reservation identity, goal/owner/item/quantity/purpose and immutable acquisition
portfolio/route/terminal receipt/consumer/creation revision. New rows must match
the exact creation anchor and the position reached by the complete history below.
Before any move that is the initial position/revision. Invented slot,
node, state, revision, metadata or one-sided missing origin/anchor fails closed.
An unrelated global ledger revision or fresh clock state does not rewrite the
origin or invalidate an unchanged custody row. Ordinary ledger clones deep-copy
the position and preserve its history anchor; file-repository restart does too.

This SHA is an integrity binding, not a signature or independent proof of native
acquisition: the original canonical fresh-terminal verification remains required.
Legacy rows with neither field retain consumer-only authority at their original
player node, creation row revision and source state, and their serialization is
unchanged; no origin is fabricated on read, clone or restart. They gain no
relocation authority. Removing the origin and all new history fields after a move
cannot downgrade a moved row to a valid legacy record.

Targeted tests use the actual original acquisition settlement and file repository,
cover changed/missing facts and anchors, a rehashed but inconsistent position,
deep copies, independent global-state refresh, restart, unchanged publication
and legacy omission. Original museum/material Core tests (80) and Rusty Key
rolling producer checks (34) pass without a game run or coverage/training promotion.
Full Backend passes 473/473 (23 added cases). Under the old two-logical-CPU
throttle the existing malformed-reply recovery case again hit its 15-second outer
deadline; with four logical CPUs at BelowNormal the unchanged complete suite
passed in 14 seconds, twice (472 before the added independent-clock clone case,
473 after it). This records a load-sensitive observation, not a proof of its
precise root cause or a widened timeout. Builds and small focused checks can
remain two-CPU; daytime full Backend checks use the measured four-CPU profile.

### Linked Position History

The original prepared-transfer service now conditionally admits both selected
custody moves and passive custody rebinds caused by compaction. It independently
reprojects all affected reservations and verifies both complete fresh inventories
before publishing the original single-revision transaction. Every affected
custody row attaches one optional typed `output_custody_position_transition` to
its existing relocation/rebind history entry; no second ledger, extra history
entry or full snapshot is retained.

Each link binds the unchanged initial-position SHA, preceding position SHA,
before/after node, slot, state and row revision, retained portfolio/route/source
revision, prepared-transfer SHA and fresh supporting-receipt SHA. The shared
custody contract walks every later row-history entry from the original creation,
requires strictly increasing global revisions and exact row-revision increments,
and verifies the matching original preparation/completion/receipt markers. Only
player-to-normal-chest moves and same-node passive rebinds are admitted. It does
not accept a caller-supplied final slot or a rehashed but discontinuous chain.
Marker lookup is indexed once per validation, rather than rescanning history for
each link; no unsafe mutable-ledger cache or per-frame snapshot is introduced.

Acquisition source, consumer, goal, owner, item, quantity and initial position stay
unchanged. Passive custody keeps its own source/goal even when another goal's
transfer caused compaction. Failed native-slot checks or proof reconstruction
return the exact original pending preparation and ledger, without publishing a
partial position history. Ordinary clones deep-copy links; file restart and
read-only settlement-reply retrieval preserve the published proof. The original
donation builder and settlement consume the returned player-held custody.

The 28 new Backend cases cover a service deposit/withdraw/donate round trip,
other-goal passive compaction, file restart/reply lookup, chain and marker
tampering (including rehashed links and legacy downgrade), failed complete-slot
checks and unprepared relocation/rebind refusal. Full Backend passes 501/501,
complete game-free Core 1155/1155 and the original rolling producer 34/34.
Four initial Core failures exposed an old signed-player fixture that changed
the actor ID but retained `player:1` inventory/intent/claim/move nodes; those
fixture identities now move together, without weakening production checks or
expected assertions. The focused museum/material group then passes 80/80.
These are synthetic inventory/ledger/API tests using the original acquisition
and donation services, not a new native game run or criterion/Teacher evidence.

PR #420's P2 review identified that valid stored provenance must not authorize
donation consumption. Two regressions first reproduced the old behavior: a real
unreserved player duplicate is donated while the retained chest unit is untouched,
yet a forged lease could pass the request builder and a direct request could
complete the stored reservation. Both original entry points now share explicit
`IsAuthorizedForConsumption`, which additionally requires the exact player node.
Ordinary custody provenance remains valid in storage for transfer/planning; the
original candidate and JIT donation checks already require player-held custody.
The returned-player round trip still passes, and rejected consumption leaves
the complete input ledger unchanged. The first direct-request fixture lacked
its required reason; after correcting that setup it reproduced actual acceptance
before the production fix. No failing fixture was counted as proof of the defect.

### Original Control And Compiler Binding

`MuseumOutputCustodyTransferRequestBuilder` uses the authenticated current row
and an explicit destination to build the original `inventory.transfer_item`
candidate and portfolio preparation. It does not mint custody, acquire another
unit, choose a new strategic goal, consume stock or write a ledger. Unknown
identity/origin, a pending preparation, invalid destination, unavailable menu or
route, unreadable/conflicting clock and a transfer that does not fit the day's
remaining minutes are rejected before commit. The time budget uses the existing
`GameClockBudgetPolicy` and `DailyPlanCompiler`, not a new scheduler.

After the original commit, compilation independently repeats that commit with
the original service and compares the complete result, including other-goal
claims. It then uses the original daily-plan and action-queue compilers.
`museum_output_custody_transfer` binds the prepared SHA, portfolio and global
revision, actual goal, selected custody ID/row revision and current position SHA.
It has no `acquisition_*` annotation and cannot impersonate a machine input.
The original JIT validates this binding and the actual execution JSON. Removing,
mixing, duplicating or rewriting scope parameters does not restore a legacy path.

The original ordinary prepared-transfer protocol may now passively compact an
authenticated custody row only when it does not select that row for relocation
and all affected position histories are valid. Actively selecting custody still
requires the dedicated control above. It retains its own goal/source/consumer
when another goal caused compaction, as the same original settlement proves.

`MuseumOutputCustodyTransferSettlementRequestBuilder` verifies the actual queue
item, exact receipt/actor/state identities and complete before/after images of
both retained inventories. Normal movement may change only the dispatched item's
state hash; all other queue authority remains pinned. The before-image check
reuses the original slot verifier, while after-image verification remains the
same shared native projection. It creates an original supporting-transition
request with relocations and no consumptions. Its independent `Verify` repeats
the original service transaction and compares the entire result/map/position
history; a reported success or caller-provided final slot alone is insufficient.

The intent parameter codec and deterministic queue-ID stabilizer are shared with
the original machine/held-donation code, not copied alternatives. Validation is
boundary-only and purpose-scoped to queue bindings and the material graph; full
snapshot hash/identity admission remains at the existing ingestion/logical-reader
boundary. There is no new full-snapshot serialization/cache, per-frame polling,
planner, inventory store or native transfer executor. The locked decompiled
`Chest.addItem` still compacts before stacking/appending; these changes do not
replace that previously accepted physical behavior.

The bootstrap CLI exposes three read-only adapters, using the existing artifact
readers and atomic output writer:

1. `build-museum-custody-transfer-request`: `--snapshot`, `--strategy-ledger`,
   `--reservation-id`, `--destination-node`, `--output`, optional
   `--commit-request-output`. Submit that request through the original journaled
   commit endpoint and retain its complete response.
2. `compile-museum-custody-transfer`: the same original snapshot and **pre-commit**
   ledger/reservation/destination, plus `--commit-result` and `--output`; optional
   `--selected-queue-output` and `--selected-plan-output`. Execute that queue with
   the original precompiled Live path, not direct inventory writes.
3. `build-museum-custody-transfer-settlement`: `--queue`, the actual
   `--dispatched-item`, `--receipt`, actual transfer `--before-snapshot` and
   `--after-snapshot`, **prepared** `--strategy-ledger`, and `--output`; optional
   `--settlement-request-output`. The before snapshot is the transfer's fresh
   state after movement, not necessarily the queue's initial state. After the
   original journaled supporting-transition POST, repeat the adapter with
   `--settlement-result` and a separate proof output to independently verify it.

Each adapter reports `ready`, blocking reasons, `execution_performed=false` and
`formal_training_authorized=false`; refusal exits 2 and must not be ignored.
Failed result verification publishes no request authority. These commands do
not retry mutations or execute the game. Unknown transport outcomes still use
the existing exact read-only reply retrieval, never blind native/POST replay.

The local synthetic/API group covers both directions, original donation,
file restart, actual Live/Backend HTTP after normal movement, before/after
inventory drift, scope downgrade, changed commit and changed settlement results.
Three actual CLI invocations reproduce the same request/queue/settlement and
verify the retained result; a missing destination and a changed settlement map
return 2 with no request file/authority. Full Backend passes 543/543 (42 added
control cases); full game-free Core passes 1156/1156. The existing passive-custody
service case additionally exercises its actual compiled/JIT path. Original
canonical machine-transfer/settlement/terminal proof and rolling producer 34/34
pass after sharing the intent encoder and queue-ID stabilizer. Release build has
zero warnings/errors and the generated checkpoint remains EVD-338.
The bounded local fixture is under
`I:\StardewAITraining\custody-control-20261104\public-cli-01`; its scope explicitly
says synthetic-only, not native gameplay or training evidence.

### Remaining Location Boundary

The current custody object retains acquisition portfolio/route, terminal-receipt
SHA, consumer, creation revision, initial position and linked position history
above. The service/control/compiler/JIT/receipt contracts and outer rolling
selection/runtime client are now connected through those same implementations.
Missing original facts must not be inferred from a later location.

Both selected custody and passive custody affected by compaction need this
verification. Preserve their consumer, goal, owner, quantity and acquisition
identity; a position rebind does not consume or reacquire an item. Keep generic
upsert, release, consumption and cross-goal bypasses closed. Rolling chooses
stored custody only for this goal/consumer; cross-map approach reuses the original
resolved route and gate/repair handling without a speculative commit. Same-map
transfer uses the original prepared request/compiler. Actual Live command JSON
must equal the dispatched typed item; absent/changed records cannot settle.
The runtime adapter retains per-transfer before/after paths instead of assuming
the queue's initial snapshot is still current. It calls the original journaled
mutation/reply recovery and independent result replay, without relabeling
museum-output handoff as machine acquisition. No new inventory store, executor,
full-snapshot cache or per-frame scan was introduced. The native exit proof is real acquired
output -> storage -> withdrawal -> authorized donation/consumption, including
other-goal protection and missing/stale/forged-lineage refusal. The JIT guard is
now conditional on the complete bound control, not removed. API/CLI tests alone
did not prove that native trajectory; the accepted native boundary below now does,
without reopening accepted geode/key or owner visual gates.
Frozen coverage counts and `formal_product_training=false` remain unchanged.

### Next-Day Source Reuse (Synthetic Only)

The original custody position state hash identifies the last authenticated
material-position event, not the latest clock tick or day. A date-only change
must not rewrite its acquisition receipt, initial position, linked history,
reservation revision or position binding. The original transfer request builder
re-observes the actual source slot/quantity/authorization in the current snapshot;
the full donation forecast separately needs current-date map/gate/calibration.

Four public build/preparation cases pass on 2026-11-05: unchanged stored custody
with a fresh next-day date source, yesterday's date proof, missing stock and a
broken original position anchor. A fresh source permits the original immediate
navigation and current-day forecast; an old date cannot prove that forecast but
does not invalidate a separately legal immediate connector. Missing stock or
broken lineage returns no transfer/navigation queue or speculative acquisition.
The persisted original ledger bytes, receipt and position binding stay unchanged.
No production custody logic changed. These are synthetic file-reload/CLI-source
contracts, not new native overnight evidence. Artifacts stay outside Git at
`I:\StardewAITraining\next-day-custody-20261105\step-01`.

### Accepted Native Stored Round Trip

On 2026-11-04 the hidden/muted isolated run `custody-b3512d5f-1104` completed six
queues and seven applied, fresh, verified native actions: geode acquisition,
movement/store, automatic rolling withdrawal, two original map connectors and
original museum donation/consumption. Storage was an explicit mechanical probe;
retrieval and donation were selected by the existing rolling controller. It did
not reacquire the stored output or substitute a free player copy.

The one `(O)549` has player counts `0 -> 1 -> 0 -> 1 -> 0`, chest counts
`0 -> 0 -> 1 -> 0 -> 0`; geodes decrease only `2 -> 1`, and museum progress
changes only `59 -> 60` on donation. Ledger revisions are `1,3,5,7,8`; the three
other-goal stone units remain active at the compacted slot `9 -> 1` with their
goal/provenance retained. The final local status is
`native_donation_threshold_observed`, with no night/key dispatch and no key claim.

Retained-file review invoked the original transfer settlement CLI twice, rebuilt
the donation request with the original C# builder and matched the original Live
RPC journal, retrieved its result through the original read-only file-repository
lookup (including stored reply/hash checks), and exactly replayed the original
donation settlement. All five phase state hashes, binary/mod hashes and actor/save
identity passed. Script `b3512d5f`, clean harness source `b84ced79` and other clean
runtime source `f2b0c4ef` have explicit Git-tree equivalence for the code they run.

Raw artifacts remain outside Git at
`I:\StardewAITraining\custody-round-trip-20261104\native\custody-b3512d5f-1104`.
`independent-review-03/review.json` SHA-256 is
`681f33b01cd724f8f164a62e8920c6a98a4e860c8190b75e8a35564a3e6d238a`.
The original ProofFarm source hash remains
`68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d`;
owned processes and ports 5132/8765/8767 were released.

This accepts same-map custody deposit/withdrawal plus native travel/donation,
not native cross-map chest approach, full fresh-save sixty-item supply,
joint future windows/budgets, criterion Teacher/native partitions, a pre-Year-3
completion proof or formal training. Existing negative authority tests keep their
own synthetic/API scope. Next close joint timing/windows and the remaining
criterion evidence; do not rerun this accepted boundary as a new prerequisite.
