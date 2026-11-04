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

No native game was started for this slice. It does not promote coverage, prove
full acquired-output custody in gameplay or authorize training. Custody currently
supports one exact museum item in player inventory. Relocation/rebind is rejected
until its multi-step custody lifecycle is integrated; it is not silently treated
as supported. The [native slot-lineage prerequisite](material-transfer-slot-lineage.md)
now handles chest compaction in the existing projector/executor/receipt chain;
it does not remove those custody guards or certify claim relocation. Backend reply retrieval now has its separate
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
prerequisite, not permission to move custody. Next extend the same retained
lifecycle with immutable acquisition provenance and authorized mutable custody
locations, then verify real acquired-custody storage/withdrawal/donation. Both
selected and passive custody remain rejected at preparation until that contract
is implemented and independently verified. No new planner or executor is needed.

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
the exact creation anchor and current initial position/revision. Invented slot,
node, state, revision, metadata or one-sided missing origin/anchor fails closed.
An unrelated global ledger revision or fresh clock state does not rewrite the
origin or invalidate an unchanged custody row. Ordinary ledger clones deep-copy
the position and preserve its history anchor; file-repository restart does too.

This SHA is an integrity binding, not a signature or independent proof of native
acquisition: the original canonical fresh-terminal verification remains required.
Legacy rows with neither field retain their existing consumer-only behavior and
serialization; no origin is fabricated on read, clone or restart. They gain no
relocation authority. Both selected and passively affected custody are still
rejected by the existing transfer preparation/settlement guards.

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

### Remaining Location Boundary

The current custody object retains acquisition portfolio/route, terminal-receipt
SHA, consumer, creation revision and the initial position binding above. Creation-history authority alone is not a
complete history of storage positions. Extend this same contract so immutable
acquisition provenance remains verifiable while each authorized position change
is tied to the prior row revision/location, prepared transfer, fresh receipt and
atomic result. Retain compact location evidence, not whole snapshots or a second
ledger. Missing original facts must not be inferred from a later location.

Both selected custody and passive custody affected by compaction need this
verification. Preserve their consumer, goal, owner, quantity and acquisition
identity; a position rebind does not consume or reacquire an item. Keep generic
upsert, release, consumption and cross-goal bypasses closed. Wire the original
control candidate/plan/dispatch/receipt path without mislabelling museum-output
handoff as a new machine acquisition. The native exit proof remains real acquired
output -> storage -> withdrawal -> authorized donation/consumption, including
other-goal protection and missing/stale/forged-lineage refusal. This is not yet
implemented beyond the initial binding above and does not reopen the accepted
geode/key or owner visual gates.
