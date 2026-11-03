# Golden Walnut source-schedule bootstrap correction

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

<!-- pretraining-order-20261103-r1; edited_at=2026-11-03T15:46:34+08:00; research_base=049f40edfcef910b00451ad5f7b948916df67ba1; owner-authorized; docs-only -->

### Current scheduling correction · 2026-11-03T15:46:34+08:00

The owner authorized a dependency-first pretraining reorder. Rusty Key is now the sole primary root; Master Angler is **parked, incomplete, at position 10**, with its code and evidence retained. See `CURRENT_STATUS_CN.md` section 6. The prerequisite-control channel described above remains valid; graph wiring is not proof that an upstream root or a fresh save has completed its prerequisites.

PR #389 records v111 stopping at Spring 27 / iteration 647 with 479 verified actions and zero stage errors. It also records an isolated, already-unlocked island baseline. When Master Angler resumes, first run the bounded native island/Gourmand/walnut handoff smoke on that copy, then compose short prerequisite chains, and only then attempt the required fresh-day-zero source schedule. The boundary copy cannot prove fresh-save unlock provenance, and no v111 in-memory continuation may be spliced into a later process.

This changes scheduling only: source-schedule/native receipts, independent replay, Gourmand continuity, 72/14/3 completion, WIP=1 and `--skip-training` remain required. It is not a new execution engine, a new bug finding, or training authorization.

### Historical v90/v91 status (not the current next task)

The active v90 run predates this correction and remains calibration-only under
`--skip-training`. It proves the existing crop, sleep/save, mail, quest and
collection loops, but cannot be resumed as post-fix evidence. After merge it is
frozen as pre-fix evidence and v91 must start from a fresh day-zero identity.

Full fresh-save island arrival, source increment, First Parrot, Western Turtle,
Island House, durable schedule generation and independent replay remain the
runtime exit condition. No model parameter training is authorized here.
