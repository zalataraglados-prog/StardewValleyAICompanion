# Original Seed Preparation Ownership

## Accepted Parent And Scope

PR522 exact head6d6482f87f1de29605eaddfe72ac42c88180411b passed all42 steps of
CI42426869336 (Core2443/Backend1136, no failures/skips) and same-head review with
no findings. Main4f057050877c7e9e86e761babf68e2ff8127ccf6 has identical tree
18eeabf669512122caf11cb06848f9e9f12e2fe3; own refs were removed. Its single
mixed seed/lifecycle owner and original recovery consumers remain in use.

New seed admission freezes the SHA of the original runtime intent BEFORE a
business mutation. It uses the same purchase/transfer/planting binder, original
planting preflight/requested/bounded batch/claim, original CreateIntent and the
same preparation-details append helper. Those helpers retain original data,
serialization and call positions; no second intent/compiler/executor or weakened
source rule. Original runtime preparation and original recovery share the helper.

## One Invocation And One Pin

PrepareSeedAsync invokes a trusted ORIGINAL preparation factory once under the
same owner gate. The factory must call original preparation/compilation, not
dispatch native requests. The existing lifecycle call-claim writer/index is
shared, with a typed seed-start schema; no independent owner or retry loop.
A start record is a claim of preparation invocation, NOT proof of native dispatch
or its absence. Durable/uncertain claims cannot be invoked again after reopen.

The returned original sealed runtime is not returned to the caller until its
private preparation certificate, exact identity/inputs/request/batch/claim and
pre-admitted intent SHA pass, the real marker journal in the configured root is
verified, and the owner preparation record is durable. The existing public
PinPrepared path remains for original standalone callers. Live/native execution
and acceptance still use the original transaction and proof/settlement context.
No new Get, model call or world capture is introduced by the owner preparation
wrapper; the original base/marker reads still happen in original preparation.

Journal identity composition is extracted from the existing recovery reader:
same canonical bytes, hash, filenames, schema/backend/operation/request identity
and original reader budget/regular-file checks. No mutation/lookup contract changes.
The new owned invocation checks actual journal files before pinning. This does
not retroactively require new fields on old standalone records.

## Recovering An Unpinned, Verified Original Result

A pinned admission without an owner preparation record may still have a COMPLETE
original preparation and native result retained by the original standalone path.
RecoverPending can now use that PRE-mutation intent SHA as the recovery anchor.
It must first pass the SAME original preparation/marker/base/queue/native proof,
consumption/custody, original journal/reply/lookup and complete final readback
consumer. The consumer exposes only its already-verified original prepared queue
path/hash. Only AFTER full verification does the owner reconstruct the preparation
index, mark reconstructed_after_original_native_verification, then accept normally.
It never fabricates a queue, proof, reply, zero execution or success label.

The reconstructed record's native_dispatch_performed=false describes this index
reconstruction issuing NO NEW native request; the explicit reconstruction flag
records that the ORIGINAL native effect was independently verified. It does not
deny that original execution happened. The original native proof remains required
by the accepted record. A failed pin/acceptance write faults the instance and
retained original state is reopened; nothing is resent.

Old seed admissions without a pre-mutation intent remain readable and retain the
original standalone Prepare+Pin contract. They cannot use the new first-invocation
API or gain an invented historical pre-pin. Missing legacy preparation/intent,
incomplete original preparation or missing native proof keeps pending ownership.
Missing files are NOT evidence of zero/native safety or permission to retry.

## Verification And Exact Successor

Initial45/45 and expanded49/49 owner cases passed; a NEW unreachable test branch
warning was removed without a production change. Added cases cover original
preparation once, durable pin acknowledgement loss, full unpinned native recovery
with retained/lost final reply, invocation claim uncertainty, actual journal-root
mismatch, legacy compatibility and original transfer/planting factory paths.
Final related428/428 passed, zero failures/skips, including all10 new cases.
Exact-head full CI is in PR closeout. Fixtures are original HTTP/file-store
and synthetic native-format evidence, not new game/Teacher/Student outcomes.

Next is preparation-only/partial-preparation recovery and native dispatch ownership
using original journals/lookup and original strict controller-stop context. The
existing CompleteWithoutNativeDispatchAsync must be reused: full original queue,
explicit zero-request controller stop, unchanged before/after/ticks and original
marker authority readback. Do not write another validator or classify missing
proof as zero. Unknown native requests retain pending and are never resent.

Then original travel/maintenance/native-save continuity, explicit managed
activation and bounded native sampling, original global calendar/aggregate
budget/cross-root, Provider/model and manifest/preflight. This slice does NOT
claim arbitrary partial preparation is resumable or automatic managed training
is enabled. L1 stays19/19 and11/11, formal=false; Student fresh-save21/21 remains
post-training. No game/server/API worker/training; local-data/raw logs stay external.
