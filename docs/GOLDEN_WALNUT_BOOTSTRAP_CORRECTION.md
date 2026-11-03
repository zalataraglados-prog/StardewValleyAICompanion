# Golden Walnut source-schedule bootstrap correction

> **Acceptance-scope correction, 2026-11-03T16:30:45+08:00:** #224 and #390 supersede the earlier scheduling-only amendment. Native access semantics remain strict; a complete fresh-save schedule and an unconditional Gourmand 3/3 receipt are not Master Angler criterion-local admission requirements. See [the R2 amendment](PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md) and [the Teacher contract](TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md).

## Verdict

The peer review reproduced a real circular dependency: a locked Ginger Island
map prevented terminal route proof, the missing route prevented a Master Angler
intent, and the missing intent prevented the already-implemented island unlock
subflow from running. The correction keeps terminal fishing strict and adds one
separate prerequisite-control channel.

## Single execution path

`MasterAnglerTargetDateIntentBuilder` still emits ordinary candidates only when
the complete terminal route is produced and guaranteed on time. If an exact
authoritative island fish window is otherwise valid, transparent
`ginger_island_access.v1` says that target is locked, and terminal route
production fails, the builder may instead emit one deterministic control-only
intent. That intent carries no route edges, arrival time or route evidence and
sets `runtime_terminal_validation_required=false`.

Core validates the control intent's exact artifact identity, missing-species
membership, date/time window, source, equipment/dynamic conditions and weather,
but deliberately does not claim terminal route reachability. It then enters the
existing `TryBuildGingerIslandPrerequisiteCandidates` branch. All resulting work
still uses the existing availability evaluator, `DailyPlanCompiler`,
`ActionQueueCompiler` and Product Executor. There is no second island planner,
route planner, compiler or executor.

The live loop selects an available marked prerequisite deterministically before
Teacher preference construction. Learner score/model score/reward are neutralized
and the selection artifact records
`teacher_preference_label_eligible=false` and
`formal_training_authorized=false`. The policy-trajectory appender independently
rejects every marked control candidate. Once transparent access changes, the
builder stops emitting the control intent and ordinary full-route validation is
required again before fishing.

## Runtime status

<!-- acceptance-scope-20261103-r2; authored_at=2026-11-03T16:30:45+08:00; baseline=0b23350d60a67993172db4a5d099b01fd9b54693; sources=#224,#390; docs-only -->

### Current acceptance-scope correction · 2026-11-03T16:30:45+08:00

The previous `pretraining-order-20261103-r1` amendment retained the very dependency reported by [#390](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/390): a global fresh-save source schedule and a chosen Gourmand route were required before the local fishing root could close. Moving that root to position 10 did not fix the dependency. That unconditional local gate and its derived fixed rank are superseded, not preserved as quality requirements.

The immediate corrective work is to separate criterion-local coverage, reusable prerequisite certificates and Teacher-wide integration, under the existing WIP=1 rule. Rusty Key remains a supported upstream priority candidate after this correction, but the remaining root order must be reassessed using local scope and actual reusable evidence. Do not claim this documentation change has modified the builders, script gates or machine reports; #390 remains unresolved in implementation.

<!-- audit_invalidation: scope=r2_pending_status; invalidated_at=2026-11-03T16:30:45+08:00; annotated_at=2026-11-03T18:52:51+08:00; superseded_by=PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md; historical_only=true -->
> **Post-merge note:** Earlier wording that #390 was not implemented applies only to the 16:30 R2 drafting snapshot. PR #391 merged at 2026-11-03T17:49:49+08:00; no full game or formal training promotion is implied.

PR #389 records v111 stopping at Spring 27 / iteration 647 with 479 verified actions and zero stage errors. It also records an isolated already-unlocked island baseline. Use that copy for the relevant local fishing/access/receipt smoke and short handoffs; label its existing unlock state explicitly. It cannot prove fresh-save unlock provenance, and no v111 in-memory continuation may be spliced into a later process.

Master Angler local admission retains the native fish denominator, typed fish-area prerequisites, legitimate catch chains and criterion-local comparison/native-outcome train/validation/test evidence. Shared prerequisite certificates must be valid and fresh-bound. Gourmand is one OR route for walnut funding: validate its promised transitions when selected, but accept a different legally sufficient funding route without forcing Gourmand 3/3.

The full source schedule, native island-acquisition chronology and independent frozen-artifact replay remain useful and required at their applicable Teacher-wide integration boundary, after local capability aggregation. They must not be fed back as unconditional local-root prerequisites. Existing executors, identity checks, receipt verifiers, negative cases and `--skip-training` remain; no second execution engine, automatic coverage promotion or model-training authorization is introduced.

### Superseded 2026-11-03 R1 decision

The original scheduling-only amendment was authored at `2026-11-03T15:46:34+08:00` and committed in `0b23350d`. Its fixed position-10 decision and blanket retention of source-schedule/Gourmand/72/14/3 as the Master Angler exit gate are withdrawn by R2. The original text remains available in Git history; v111 and earlier evidence is not deleted.

### Historical v90/v91 status (not the current next task or local admission rule)

The active v90 run predates this correction and remains calibration-only under
`--skip-training`. It proves the existing crop, sleep/save, mail, quest and
collection loops, but cannot be resumed as post-fix evidence. After merge it is
frozen as pre-fix evidence and v91 must start from a fresh day-zero identity.

Full fresh-save island arrival, source increment, First Parrot, Western Turtle,
Island House, durable schedule generation and independent replay were the
historical runtime exit condition. Under R2 they belong to the applicable global
integration proof, not an unconditional Master Angler local gate. No model
parameter training is authorized here.
