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
as supported. Rejected/network-ambiguous settlement still requires recovery of
the persisted receipt and lease; the failure flag alone is not that recovery.

The next Rusty Key work remains autonomous producer/commit/execution orchestration
and a single retained native acquisition-to-donation-to-settlement trace, joint
time/opening-window/next-morning checks and criterion-local independent Teacher
partitions before Year 3. In particular, existing acquisition execution binding
currently requires verified portfolio Teacher preference; a deterministic
control proposal must not bypass that boundary by pretending to be a Teacher.
Keep execution evidence and preference-label admission separate when integrating
the producer. No new planner or executor is needed.
