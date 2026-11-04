# Museum Output Custody

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
now supports the history-backed position lifecycle described below; the runtime
dispatch guard still rejects custody transfer until its original control path is
wired and verified. The [native slot-lineage prerequisite](material-transfer-slot-lineage.md)
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
prerequisite, not permission to dispatch custody. The same original service now
conditionally admits selected and passive custody only with an authenticated
initial position and complete linked position history. Next wire the original
control candidate/compiler/JIT path, then verify real acquired-custody
storage/withdrawal/donation. Legacy/malformed provenance and unprepared generic
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

### Remaining Location Boundary

The current custody object retains acquisition portfolio/route, terminal-receipt
SHA, consumer, creation revision, initial position and linked position history
above. This completes the service-side compact location contract, not runtime
dispatch authority. Missing original facts must not be inferred from a later
location.

Both selected custody and passive custody affected by compaction need this
verification. Preserve their consumer, goal, owner, quantity and acquisition
identity; a position rebind does not consume or reacquire an item. Keep generic
upsert, release, consumption and cross-goal bypasses closed. Wire the original
control candidate/plan/dispatch/receipt path without mislabelling museum-output
handoff as a new machine acquisition. The native exit proof remains real acquired
output -> storage -> withdrawal -> authorized donation/consumption, including
other-goal protection and missing/stale/forged-lineage refusal. The original JIT
custody-transfer guard remains closed until that control integration; service
tests alone do not open it or reopen accepted geode/key or owner visual gates.
Frozen coverage counts and `formal_product_training=false` remain unchanged.
