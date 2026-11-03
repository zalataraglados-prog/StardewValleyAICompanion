# Independent Teacher Pairwise Admission

## Reason And Scope

Issue #394 confirmed that the structured trainer treated the executed selection
as preferred over every other legal candidate, even without independent Teacher
evidence. Native success establishes an outcome, not a counterfactual ordering.
This repair changes that shared training boundary, not the model family, hard
constraints, action compiler/executor, criterion denominator or formal launch gate.

## One Admission Path

`PolicyPairwiseSupervisionAdmission` is consumed by both
`PolicyTrajectoryDatasetBuilder` and `StructuredPolicyTrainer`.

It requires `policy_teacher_supervision.v2`, an explicitly configured replay
verifier, Teacher identity/version, source state, the complete candidate
denominator digest, deterministic partition and retained source references.
The production collection adapter reuses the existing receipt builder, which
rebuilds the independent preference and verifies its native before/receipt/after
evidence. Teacher rules are not duplicated in Core.

The replayed row must match the claimed context, versions, features, complete
candidate set, selection, native outcome and supervision. Backfilled returns are
separate observations and cannot create preference labels. Pairwise preferences
and their authoritative evaluations are compared with the independently rebuilt
artifact, including when an edited artifact has been rehashed.

Only explicit strict comparisons whose two endpoints are available and policy
admitted become training pairs. Unknown, tied, incomparable, unavailable and
unadmitted alternatives are not automatic negatives. A one-trainable-candidate
native receipt remains `verified_evidence_only_no_trainable_teacher_comparison`;
its native facts are preserved, but it cannot create a supervised dataset.

## Entrypoints And Artifacts

- Backend `POST /api/v1/training/structured/train` uses the configured collection
  replay verifier; the request cannot select a verifier by claiming its identity.
- PolicyDataset, PolicyModel and the live loop's actual structured training method
  use that same implementation and shared admission.
- CLI input rejection produces structured `status=rejected` with exit code `2`.
- A supervised dataset declares `independent_teacher_pairwise.v1`. An observation
  alone cannot create or overwrite its manifest. The trainer revalidates partition
  rows rather than trusting a caller-rehashed dataset manifest.
- Checkpoints record the supervision objective. Legacy initialization without it
  is rejected, not silently reclassified. Existing files are not migrated or removed.
- Historical v1 trajectory metadata remains readable as evidence, but strings or
  a selected flag cannot upgrade it to supervision. Reuse requires real retained
  sources to pass the existing independent builder and produce a new v2 record.

The Core replay port is host-configured, not a new execution authority. Backend
references the existing Bootstrap implementation for this offline verification;
Bootstrap still depends on Core, and Core has no reverse dependency on it.
The original formal launcher remains locked pending all separate readiness gates.
Valid offline supervision does not authorize a formal product training run.

Managed museum contexts additionally retain the strategy ledger path/hash in the
preference and trajectory sources. Teacher/tie-control compilation receives that
ledger; replay rereads it and rejects changed or omitted evidence. Managed museum
alternatives are rechecked through the existing candidate generator. This is
resource-legality evidence, not a new preference objective or native outcome;
see [museum material admission](museum-donation-material-admission.md).

## Verification

The regression matrix covers absent supervision despite native success;
rehashed selected-only partitions; wrong/missing Teacher identity, version,
state, denominator, partition, receipt, sources and labels; inaccessible/invalid
evidence; unlabeled alternatives; legacy initialization; and valid explicit pairs.
Sentinel checkpoint/manifests must remain byte-unchanged on rejection.

Final local verification passed Core game-free governance `1041/1041`, Backend
`265/265`, and the focused admission/dataset/trainer set `35/35`. The focused set
is included in the Core total, not an additional coverage numerator. Remote CI
must pass on the submitted PR before merge.

Backend tests exercise the real endpoint, both CLI binaries and the live loop's
actual training method. The positive endpoint test uses an explicit frozen replay
test double; production uses the collection adapter and retained source artifacts.
The independent collection self-test also exercises the real preference and
receipt rebuilders with a hermetic, explicitly supplied denominator fixture.
These are not native game runs or production training evidence.

`self-test-current-stage-one-policy-supervision --output-root <isolated-root>`
reuses the existing static inputs, current-frontier and rod checks without
rerunning unrelated machine/crop/portfolio matrices. Its local focused pass took
about four seconds. The broader collection run at
`I:\StardewAITraining\issue394-unit\collection-replay-v4.log` stopped earlier
with a file-access error in the separate portfolio-continuation fixture; it is
not reported as passing and is retained for later diagnosis, not silently deleted.

All generated fixtures/checkpoints stay outside Git. No game, worker or formal
training run is launched for this repair. After #394 closes, continue the same
Rusty Key primary's safe acquisition/reservation threshold portfolio; coverage
counts remain frozen until its own criterion-local gates pass.
