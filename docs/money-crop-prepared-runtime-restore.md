# Original Prepared Runtime Restore And Execution Ownership

## Scope And Compatibility

New owned seed preparation writes `crop_teacher_managed_seed_start.v2` with
`dispatch_protocol=owner_loop.v1`. Historical seed-start v1, standalone
Prepare+Pin and lifecycle-start v1 retain their original behavior and readers.
They do not acquire an invented no-dispatch guarantee or replay eligibility.
The owner remains the same OS lease, gate, pending operation and accepted chain.

`ExecuteSeedAsync` durably claims the original execution-loop invocation before
calling it under the owner gate. Its dispatch record pins admission, preparation,
original intent, run, step and sequence. `native_request_performed=null` means
unknown, NOT false and NOT a native proof. A durable or uncertain claim cannot be
invoked again. Disposal waits for the callback. The callback must run the original
queue validation/loop; it must not re-enter owner APIs while holding that gate.
Completion and acceptance may follow outside the callback.

Only new owned runtimes bind a private execution-lease check to their existing
queue/prefix entry points. Original unowned behavior is unchanged. This is a
trusted in-process orchestration boundary, not a security sandbox for arbitrary
code. It does not change native input, animation, compiler, dispatch HTTP or
settlement rules, and it does not introduce another execution loop.

## Verified Prepared-Only Recovery

`RestorePreparedSeedAsync` requires a retained v2 start, no claimed execution loop,
no attached live runtime and no retained original execution/stop artifacts. It
uses the same original pinned intent, completed preparation, marker reply or
journal lookup, base/marker ledgers and compiled queue verification as full native
recovery. These checks are extracted without changing their order or old failure
rules. The final complete authority readback must still equal the original marker.
The original Get may reconcile under its existing contract; this is not advertised
as a globally side-effect-free read.

The original sealed purchase/transfer/planting runtime is reconstructed with the
same transaction and validated marker. Planting reopens the original bounded
prefix; no per-seed queues or new prefix verifier. A missing owner preparation pin
can be recreated only after these checks. The original complete preparation and
its queue must exist; arbitrary partial preparation is not solved by this API.

When a missing marker reply is recovered by the original journal lookup, it is
retained through the original create-new writer only AFTER complete preparation
and marker-authority verification. Later native settlement can retire that lookup;
retaining the verified original reply preserves subsequent full recovery without
resending a mutation or changing the old reader. An existing bad reply still fails,
never falls back. Transitive artifacts are rechecked at owner boundaries, not on
every frame; the execution-lease check itself is cheap. No new world capture.

The original preparation pin, dispatch claim and acceptance chain validate each
other on reopen. Live v2 acceptance requires the attached claimed original runtime.
Independent full original-native recovery remains available with actual retained
proof/consumption/custody/readback; it is never converted into prepared-only or
zero-execution recovery merely because some files are absent.

## Verification And Remaining Work

Initial original-owner regression passed36/36. First new focused run passed12/15:
two new fixtures used the wrong status predicate/journal filename (only fixtures
fixed), and the missing-reply case exposed loss of future marker lookup after
settlement. Verified original-reply retention fixed the latter; focused15/15 passed.
Final related regression passed448/448, no failures/skips, including20 new cases.
Exact-head CI/review are recorded in the PR closeout.
These are real HTTP/file repository tests with synthetic native-format fixtures,
not new game outcomes or Student training.

Next reuse the original strict `CompleteWithoutNativeDispatchAsync` context for
known controller stops and handle incomplete preparation without a completed
queue, preserving original journals/lookup and never retrying unknown mutations.
Then original travel/maintenance/native-save continuity, explicit managed entry
activation and bounded native samples, shared calendar/aggregate budgets/cross-root
continuity, Provider/model and formal manifest/preflight. The automatic managed
entry is still disabled. L1 remains19/19 and11/11, formal=false; Student fresh-save
21/21 stays post-training. No worker/API, new native game or training in this slice;
raw logs and local-data stay outside Git.
