# Original Crop Lifecycle Recovery Boundary

Current successor: [lifecycle admission and acceptance in the same owner](money-crop-owner-lifecycle.md).
This consumer stays the original reconstruction path; no parallel owner or
execution rules replace it.

## Parent And Reuse

PR520's final headde6272f401f342c0b38c58a248e20d978537ce63 passed all42 steps
of CI42423375907: Core2443/Backend1102, zero failures/skips. The real initial
review finding (backend identity) was reproduced and fixed; final fix inspection
and26 owner tests are controller-attributed, not a second full automatic review.
It merged as7602281a3bc2c84881756ff500f8614b07b189cc with identical tree
66a837308024fb01d5e8518c871baca12e05f242; its own refs were removed.

This successor is a recovery consumer for that SAME phase owner. It reuses the
original lifecycle scope, cancellation/calendar kernel and release/readback
verifiers. Original option/intent/cancel-journal composition is extracted intact;
normal runtime order, wire schema, mutation rules, catch/finally behavior and
release semantics are unchanged. The existing seed recovery artifact guard and
portfolio journal/reply/lookup reader are shared, not copied into a second engine.

## Exact Retained Boundary

The caller supplies the pinned original intent SHA and exact run/mode/reason,
scope and backend. Recovery rebuilds the original intent from the original
composer, including full input hashes, source, base revision and release identity.
Wrong run/reason/backend/hash or changed inputs fail before backend observations.
Output must be a new separate directory, never the original runtime or its inputs.
No success flag in the old summary is used as proof.

Recovery requires the retained verified-closure ledger written BEFORE release.
Calendar mode replays the original complete read reconciliation. Cancellation
mode first checks the exact original request/journal, then replays a retained full
cancel reply. If that reply file is absent, only the retained closure ledger is
fed to the original unique-cancel-history readback rebuilder. Known bad local
replies do not fall back. The resulting complete closed ledger must match exactly.

The post-release backend ledger is deliberately NOT fed back into the original
cancellation recovery kernel. It contains a later portfolio mutation; accepting
it as cancellation evidence would conflate phases. The old kernel's refusal is
correct and is preserved/tested, including another root expiring during cancel.

If resources need release, validate the original portfolio request/journal and
full local reply, or use only the original lookup if the reply file is absent.
Lookup must precede final Get; a missing/not-found/corrupt journal or bad reply is
not an invitation to resend or use current state as a substitute. No-resource
closure does not issue an empty mutation. Only then perform the original final
full readback and release verifier. That original Get may reconcile the ledger;
this consumer does not claim all observations are globally side-effect free.

The returned boundary retains original run/mode/runtime/intent/release identity,
source and final ledger paths, detached ledger reads and transitive artifact
guards. Artifact drift invalidates later use. It certifies a resource lifecycle
boundary, NOT a native harvest/outcome, a restarted game's live world, or training.

## Verification And Successor

Initial old-plus-new lifecycle suite49/49 passed. It includes original HTTP/file
repository behavior, calendar/cancel/no-resource recovery, retained or missing
release reply, missing cancel reply, exact other-root reconciliation, lookup-before-
Get, unchanged mutation counters, detached DTO and post-return artifact drift.
Further missing closure/journal, not-found lookup and final-readback failure cases
are included in final related regression403/403, zero failures/skips, including
all19 newly added recovery cases. Exact-head CI is reported in PR closeout.
All fixtures are synthetic native-format evidence, not new gameplay. Local raw
logs, failed iterations and generated reports remain outside version control.

Next: bind lifecycle admission/acceptance and this recovered boundary to the
existing single durable owner; retain pending/unpinned/partial preparation and
missing-proof/zero-versus-unknown dispatch handling without forgetting/resending
unknown work. No new owner, planner or executor. Then original travel/maintenance/
native-save continuity, explicit managed activation and bounded native sample;
original joint calendar/aggregate budget/cross-root continuity, Provider/model
and manifest/preflight still follow. Do not redo accepted L1 or long crop runs.

L1 remains19/19 and11/11, formal=false; Student fresh-save21/21 is post-training.
No game/server/worker/API/training starts here; local-data is not committed.
