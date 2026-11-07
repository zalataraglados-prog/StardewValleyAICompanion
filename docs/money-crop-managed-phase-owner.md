# Durable Owner For Original Crop Seed Phases

## Accepted Parent And Scope

PR519 merged as94223ee15ec7deeb00cb8162b6d532c0560bc373. Its exact head
d516a2875776b09df28dfdd25cddf674600239b5 passed CI42417971193 (all42 steps,
Core2443/Backend1076, no failures/skips) and same-head review. The accepted
original-runtime recovery consumer is reused, not replaced or reimplemented.

This owner binds the ORIGINAL complete native run plan/request, initial source,
initial full resource ledger, actor, run/executor, backend origin and portfolio
journal directory. It reuses the exact original plan rebuild/deep-equality check
extracted from ResumeLoader; resume ledger forking and volatile-harvest rules
are unchanged. No second planner, executor, portfolio store or business rule.

## Exclusive Admission And Original Preparation

An open owner holds an OS file lease (FileShare.None); an instance semaphore also
serializes concurrent admissions. One immutable create-new owner intent and an
append-only sequence of admission/preparation/acceptance records are written by
the original durable writer. A failed write faults that instance: it must reopen
retained state, not retry an unknown write or forget pending work.

Exactly one operation may be pending. The frozen original step is bound through
the accepted purchase/transfer/planting scope selector to the current full ledger,
original input hashes and a new child runtime directory. The caller still supplies
the original planner step; this component does not select a replacement action.
Run-plan membership is necessary, not permission to execute a different step.

PinPrepared accepts only the private certificate of the actual original sealed
runtime. It checks run/executor/step/runtime identity, full input map, original
preparation request, commitment, runtime kind, and planting's original requested
batch plus optional exact claim. It pins the original intent and compiled queue
hashes. A public ICropTeacherSeedRuntime implementation cannot assert verification.
Missing/unprepared work blocks admission; no fake zero-action success.

## Acceptance And Restart

Live acceptance consumes the original transaction's verified context only after
native proof, actual consumption/custody and complete final readback succeed.
The certificate is opt-in and lazy: ordinary unmanaged completion does not add
certificate validation or a full-world capture. The final ledger is detached from
the public mutable returned DTO. Acceptance adds no backend read and does not
rebuild the original native proof; original retained artifact guards stay active.

Acceptance advances source/ledger pointers only after its durable record is
written. It retains exact original intent/proof, preparation/admission/prior record
hashes, after source and full ledger hashes and actor/source identity. Current
frontier guards do not rescan every historical full snapshot on every action.

Reopen validates the entire append-only index and retained physical inputs.
Output directory ancestors cannot be reparse points or files, and runtime roots
cannot occupy the owner's header, lease or any future operation-record namespace.
Changed identity, orphan/reordered records, malformed sequence or relative stored
paths fail closed. A pending operation must recover via PR519's original
intent/marker/proof/journal/lookup path before another admission. If only an
accepted head remains, it must restore that original boundary against the backend
before further admission. A pending later mutation takes precedence over restoring
an older head, which may no longer be the backend's current revision.

No commit/settlement/native action is resent by these recovery consumers. Known
bad local replies are not replaced by lookup. Restored files still do NOT prove a
restarted game's live world has not rolled back; fresh native continuity belongs
to the next original dispatch boundary.

## Verification And Explicit Successor

The first17-case owner run exposed one NEW test adapter mismatch: it admitted a
real planner step but prepared a hardcoded fixture batch/claim. Only the adapter
was corrected to forward the admitted step. Both original default claim selection
and explicit claim/partial-prefix cases now pass. No original planner, verifier,
batch bound, executor or success semantics were relaxed. Final owner25/25 passes,
including purchase live/restart/lost reply, transfer, planting, detached mutable
return, exclusive lease, same-instance concurrency, uncertain writes, unfinished
work, corrupt index and lost durable-acceptance acknowledgement. Related seed/
lifecycle regression380/380 passed before the final added acknowledgement case;
the final25-case run also includes future-record/header directory collision cases.
Original resume-mode/transition-ledger9/9 passed after an explicit native-profile
restore against E; its first no-restore attempt hit stale governance-only package
assets (missing game references), with no production edit to bypass that failure.
These are synthetic native-format/real HTTP-file-store
tests, not new gameplay or training evidence; related regression/CI is recorded
in the PR closeout, including earlier failed fixture iterations.

Next is the SAME owner's remaining phase handling: unpinned/partial preparations,
missing native proof, proven zero dispatch versus unknown dispatch, not-yet-issued
mutations and cross-process lifecycle close/release. Reuse the original journal,
lookup, cancellation/calendar kernel and explicit lifecycle runtime. In particular,
after release, a final Get is NOT the pre-release cancellation ledger; reconstruct
the original cancellation boundary from retained complete evidence before release
recovery. Do not fabricate an API reply or resend an unknown mutation.

Then connect original travel/maintenance/native-save continuity without weakening
the active-crop source binder or treating idle recapture as travel proof. Only
after those paths connect: explicit managed activation, bounded native sample,
original joint calendar/aggregate budget/cross-root continuity, Provider/model IO
and formal manifest/preflight. This seed owner alone does not close global R2.

L1 remains19/19 criteria and11/11 roots; formal training stays false. Student
fresh-save21/21 is post-training. No game/server/worker/API/training was started
for this slice. Raw artifacts and local-data remain outside version control.
