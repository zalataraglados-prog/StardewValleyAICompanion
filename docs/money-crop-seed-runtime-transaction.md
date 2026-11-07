# Owned Seed Runtime Transaction

## Accepted Baseline

PR508 passed42/42 CI steps in42377567925, including Backend731/Core2442, with
same-head automatic review reporting no findings. It merged asc96f59e2928a5c0569c0ebd79fb61a4d2dd18000;
tested/main tree e69374f4f122f769ae3e1e4e3755ca1b7d7bfca1 matched and owned refs were removed.
Its accounting kernel, original full-chain proof and the automatic active-claim
barrier remain in place. L1 stays19/19,11/11 roots; formal=false.

## Original Control Path, Optional Scope

The original seed control entry accepts an optional `CropTeacherSeedPurchaseSupport`.
Without it, the same original compile body receives `earn_money`; its queue and
default behavior are unchanged. With it, the original compiler receives
`daily.closed_loop`, isolating a fixed purchase from nested global Teacher selection.
No second compiler, executor, global planner or model call is added.

The control result carries the prepared transaction into the existing native
execution block. The main loop validates its actual dispatch queue before the
original executor and completes accounting immediately after the native receipt,
before harvest bookkeeping, feedback or another planning attempt. Managed input
requires the original runtime executor, queue receipts, source and candidate
identity. The original priority story/dialogue/late-day checks remain above the
seed branch; unrelated control actions are not relabeled as seed purchases.

The automatic handoff still does not construct managed scopes. There is no new
user-facing activation flag and the existing active-claim barrier is not removed.
This is optional-path wiring, not a declaration that the whole crop runner is ready.

## Marker, Compilation And Mutation Order

`CropTeacherSeedPurchaseRuntime.PrepareAsync` creates a new evidence directory,
persists scope input hashes and intent, then compares the complete actual base
ledger. It calls the original journaled portfolio commit, persists its real reply,
and independently replays it into one immutable `CropTeacherSeedPurchaseMarker`.
A complete ledger readback must agree before compilation can begin.

Compilation binds the exact seed/shop/price scope through the original native
purchase binding reader. Original compiled-file bytes are hash-bound. The actual
dispatch copy must have identical complete JSON content, allowing only its text
encoding/formatting to differ; its own physical hash is separately verified in the
native proof. This preserves the old main loop's UTF-8 writer without changing
queue IDs, parameters, actions or sources.

`CompleteAsync` requires the same run/executor/day, bound compilation and full
native transition proof. The original executor must already have ingested the
actual after-snapshot. The adapter does not import a supplied snapshot as proof
or perform native dispatch. It then uses the original supporting-transition API
and, for a purchase, the original acquired-seed portfolio commit. Both full
results independently replay before a final complete ledger readback.

Marker, settlement and acquisition have separate diagnostic objects, so the two
commit calls cannot overwrite each other's journal keys. All original requests,
real replies, proof and input hashes are retained. The original RPC journal/lookup
handles lost replies; there is no second retry implementation or mutation resend.
Duplicate completion attempts and existing preparation directories are refused.
Failures preserve the partial marker/settlement and diagnostics, not rollback or
invented success. Process restart remains recover-only through original retained
journals and kernel proof rebuild, never by blindly calling preparation again.

## Runtime Recapture Without Relabeling Work

`Open` optionally accepts the prior verified snapshot when the ledger still
anchors that source. It must hash to that actual ledger source and both snapshots
must pass the original full hash/identity checks. Original runtime-identity
normalization is reused without modifying its meaning: it excludes known run and
read metadata, not clocks or game progress.

The new adapter additionally permits only a readable, envelope-consistent,
same-day monotonic `time.time` change. Each source gets one original comparison
view; date and every other state field remain compared. No real snapshot/hash is
rewritten. Unknown progress, resources, location, calendar changes and clock rewind
cannot be hidden by this path. Purpose/profile changes are not guessed equivalent;
the caller must retain a matching relevant source or provide an original verified
transition for an actual change.

All prior/current claims are supply-checked. A clone-only original marker preview
projects the fresh root for that validation; only the real HTTP commit/replay can
advance the authoritative ledger. Other owners' rows and source identities remain
unchanged. In particular, a native purchase after-state cannot be treated as an
idle recapture before its debit settlement.

## Exact Next Work

1. Connect original prepared-transfer/native-slot projection and settlement for
   owned chest seeds, including compaction of other claims. Connect the existing
   planting consumer to exact owned-slot consumption and lifecycle/cancellation
   release. Inspect those existing implementations first; do not build duplicates.
2. Finish the managed activation/resume owner: derive scopes from the original
   owned planner step, retain the matching prior verified source, and reconstruct
   interrupted phases by original journal lookup/proof replay only. Non-idle
   control transitions retain their own original evidence, not this clock adapter.
3. Only after those consumers are connected, explicitly version/opt in the
   managed production input and sample actual purchase/transfer boundaries. Do
   not repeat accepted L1 or the13-day E trajectory for accounting changes.
4. Continue original joint calendar/aggregate budgets/cross-root continuity,
   Provider/model IO and formal manifest/preflight. Student fresh-save21/21 stays
   post-training evaluation; this slice does not authorize training.

## Validation

New protocol checks exercise real ASP.NET endpoints, the real file repository
and original C# journal/lookup: normal purchase, all three lost replies with no
resend, reload/readback, neutral support, corrupted replies/readbacks, compile
failure, duplicate completion/directory, native identity drift, changed queue,
encoding-equivalent queue copies and conservative runtime recapture. Synthetic
native receipt fixtures are reused, not presented as new gameplay evidence.

Original control source guards are linked into the game-free Backend suite from
their existing source, together with two wiring guards; they are not copied and
Core does not gain a LiveLoop dependency. Local complete Backend767/767 passes;
the final optimized source projection has focused regression and clean exact-head
CI recorded at closeout. No game, server, worker, model API or training run was
started by the controller. Raw files and `local-data` are not committed.
