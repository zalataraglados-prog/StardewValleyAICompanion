# Original Zero-Dispatch Controller Stop Ownership

## Original Contract And Shared Implementation

The original no-dispatch kernel was introduced by c53115f0 (PR516). Its public
JSON history confirms the intent: preserve mechanical batches and fresh native
prefix checks; record a controller stop separately when no native request occurred.
Missing native proof is not zero execution, and a blocked observation is not work.

The exact original identity/source/kind, queue, receipt, complete unchanged
before/after/ticks, pending count, explicit index-zero stop and marker-authority
checks are reused by live completion and recovery. The predicate is extracted,
not copied or weakened. The existing prepared-queue and identity helpers are
shared without changing their old default behavior or failure rules. The existing
artifact pin/read guard is moved to a common component for runtime/lifecycle/
recovery; the runtime does not depend on a recovery implementation.

Only a runtime bound to the new owned execution protocol retains
`controller-stop-transition.json`, after strict receipt validation and before the
original final authority readback. Old standalone completion keeps its original
files and diagnostics. The retained context binds original queue, before, receipt,
after, run, executor, day, kind and candidate; it is NOT a native outcome proof.

## One Owner, Distinct Acceptance

`AcceptStoppedSeedAsync` requires the same claimed original runtime and its
private verified zero-dispatch boundary. It cannot use native acceptance or a
caller-supplied success flag. The original live result is detached from mutable
returned DTOs, and transitive files are pinned. Normal acceptance reuses its
verified result without another backend read or full-world capture.

The accepted boundary is `controller_stop_no_native_dispatch`, with native
request count zero, native outcome false and no `support-proof.json`. The owner
records the actual marker ledger and unchanged source; seed/cash ownership stays
unchanged. It closes that invocation, not the crop task, and does not reward a
candidate, generate Teacher labels or increment native-work progress. The old
dispatch claim remains immutable and cannot be retried. A distinct admission is
possible after fresh planning/cause correction, not an automatic unchanged retry.

## Recovery And Refusal

The same owner recovers this branch only with its v2 dispatch claim and retained
original controller context (or its already accepted stop boundary). It replays
the original complete preparation/marker/journal/queue and strict stop kernel,
then requires a complete current readback equal to the original marker. The
original Get reconciliation contract remains; no mutation/native request is
resent. Retained native proof/transition/settlement artifacts conflict and reject.

A lost context-write acknowledgement can recover if the full original context
actually reached disk; a context never written cannot. Missing receipt/context,
bad local marker reply, nonzero execution, different source path (even identical
contents), changed ticks/state or backend authority drift remain pending. Missing
files/proof never authorize zero classification. Diagnostic summary status is
not proof; neither a missing nor a lying summary replaces the strict original
context. Index reopen pins dispatch/preparation/intent and stop/receipt identity;
an accepted stop cannot be promoted to native success.

## Verification And Successor

Initial new/old zero-dispatch group11/11 and new focused19/19 passed, no failures
or skips. Related469/469 includes21 new cases, including durable-context
acknowledgement/never-written boundaries. After the common-guard move, final
focused75/75 passed; exact-head full CI/review are reported in PR closeout.
Tests use the original execution loop/HTTP/file repository with synthetic
native-format fixtures, not new gameplay evidence. No second executor, snapshot
capture per frame, worker/API, game or training in this slice; local-data stays out
of Git. L1 remains19/19 and11/11, formal=false; Student fresh-save21/21 is post-training.

Next handle genuinely incomplete preparation without a completed original queue,
using original intent/journal/lookup/compiler and append-only recovery ownership;
unknown mutations must not be resent or old failed evidence overwritten. Then
original travel/maintenance/native-save continuity, explicit managed native
activation and bounded samples, shared calendars/budgets/cross-root continuity,
Provider/model and formal manifest/preflight. This stop path does not claim those
remaining integration requirements complete.
