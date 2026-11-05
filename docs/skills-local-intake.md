# Skills Criterion-Local Intake

## Accepted Starting Point

Baseline: `main@56a133dcb0455d7281fe11ed0544abdf82b88d48`, 2026-11-05.
PR #446 / final CI `42054598280` accepted the Master Angler local gate.
The frozen union is executable 7/19, split-complete 7/19, ready 7/19 and
6/11 roots. Formal training is false; do not resample accepted roots.

The next sole primary is `raise_skill_levels`, covering `player_level_15`
and `player_level_25`. This selection is a remaining-local-cost assessment,
not a claim of Skills completion or a revival of the superseded R1 order.
The R2 L1/L2/L3/L4 boundaries remain authoritative.

## Reuse And Remaining Cost

Read-only inspection of the current code, dependency graph and original
coverage source registry found:

| Remaining Root | Reuse | Actual Next Increment |
| --- | --- | --- |
| Skills | `PlayerReadAdapter.Skills` projects base/effective level, exact XP and native next-level thresholds; existing XP candidates, `GrandpaDirectionDailyCandidateBinding.HasSkillExperienceEvidence`, compiler rebind and native receipts already exist. The first current-deficit consumer below is now implemented. | Bind the existing positive-XP candidates to an independent comparison and actual threshold result, then connect the original coverage source. No typed root source is registered yet. |
| Friendship | `CurrentSocialDayTeacherLabelBuilder`, native talk/gift, ordered itinerary and bounded rollout/day-progress policies already exist. | Independent comparison/native root-source wiring is still absent; future contact production and decay/calendar composition remain separate responsibilities, not reasons to rewrite current social execution. |
| Marriage/House | Original social, purchase, proposal and construction actions can be reused. | Partner, inventory/weather/access, wedding and two upgrade branches need precise composition and same-run native results; no root source is registered. |
| Full Museum | Rusty Key acquisition/custody/reservation/donation and generic acquisition corpus are available. | Expand the 95-item heterogeneous denominator and artifact-spot probability/retry contract, then join independent comparisons and actual completion; do not reopen Rusty Key. |
| Money | Original crop Teacher, production/settlement chain and threshold corpus adapter exist. | Preserve the six thresholds by three partitions (18/18), unresolved continuity/production commitments and final economic integration; not a prerequisite for every other root. |

Skills is selected because its first local consumer has bounded five-skill
state and existing XP projections; it does not need NPC future contact,
proposal/construction calendar composition or 95 acquisition sources for that
increment. Node counts alone are not work estimates. The other four roots stay
queued; their next relative order will be reassessed after this local root,
without pretending that an unmeasured whole-game duration is precise.

## Native Intake Completed This Slice

Live decompilation used the actual E runtime DLL, SHA-256
`7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee`.
Outputs remain outside Git in
`I:\StardewAITraining\skills-local-20261105\decompile`:

- `StardewValley.Farmer.decompiled.cs`, SHA
  `6556990203ed8b6596626000d06f180e66c1256702dc1a6c7f1f1557854d0dc6`.
  `Level` (line 1597) sums the six base fields and divides by two;
  `gainExperience` (3027) ignores skill 5/nonpositive awards, updates XP/base
  levels immediately and records pending `newLevels`. Its Mastery branch is
  separate. `checkForLevelGain`/`getBaseExperienceForLevel` (3109/3121) give
  cumulative thresholds 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000,
  15000. `GetUnmodifiedSkillLevel` (7139) reads the original base fields.
- `StardewValley.Utility.decompiled.cs`, SHA
  `f6c4599a8fe6591f1c519299dfb2e363164d80f72ad71640465370f699c61181`.
  `getGrandpaScore` (6113) adds one point at each native player Level 15/25;
  it does not wait for profession selection to count these base levels.

The [Skills wiki](https://stardewvalleywiki.com/Skills) corroborates the ladder
and immediate level gain versus next-morning recipe/profession benefits;
the [Grandpa wiki](https://stardewvalleywiki.com/Grandpa#Scoring) corroborates
30/50 total levels. Native code is the deciding versioned source.

The bridge's introducing commit is `99cdba2be3096f24351f0ed80951e69a21d45129`
(2026-07-17); profession fields were later extended in `db541cf7`.
Its native next-threshold reads match the decompilation. Retained session JSON
search found only this investigation's references, not the original July
discussion; do not claim the missing design conversation was recovered.
No old bridge, binding, compiler or executor rule was changed. In particular,
the generic `has_native_experience_sources`/pending mapping fields do not
authorize training Luck or replace source-specific positive-XP evidence.

Step 1 below is satisfied for these native rules, not for future source routes.
No Skills comparison source, native outcome or coverage promotion was produced
by the intake or the current-state projection below.

## Current Deficit Consumer

`CurrentSkillExperienceDeficitBuilder` reuses the original snapshot field reader
and `SnapshotHash.MatchesStateHash`. It validates the six unique native rows,
version, base-level/XP/next-threshold coherence, effective buff delta and native
scoring sum before emitting five trainable-skill deficits. It rejects unreadable,
inconsistent, duplicate or nonvanilla Luck state rather than substituting zero.
At-cap post-15000 XP does not become a negative deficit. Effective buffs and
Mastery do not count toward the base-level criteria.

The public Bootstrap CLI is `build-current-skill-experience-deficit --snapshot
ABS --output NEW.json`. Output must not already exist, so this reader cannot
overwrite its source. Blocked fields remain nullable/empty, never valid-looking
zero progress. `ready_current_state_only` explicitly retains
`trainingLabelEligible=false` and `nativeOutcomeVerified=false`.

The existing terminal-train catch's exact `after-snapshot-0001-item-0001.json`
content-addressed source was replayed through the original reader, without
opening the game. Result: all base levels zero, Fishing XP 5, other XP zero,
total remaining level-ten XP 74995. Independent rebuild with both invocation
caches disabled is byte-identical, SHA
`cf082f34ba7651bbef9a596c0079ebda20cc0695c7c4ad910626760b155f07a5`.
Reports remain in `I:\StardewAITraining\skills-local-20261105`.
The original PowerShell-reserialized `fishing-snapshot.json` is a diagnostic
copy, not a bound native source: it was correctly rejected for state-hash
mismatch. No old collector, hash rule or original evidence was changed to admit
it. Public CLI source-overwrite misuse was also rejected without mutation.

All 27 focused checks, complete game-free Core 1564/1564 and Backend 594/594 pass,
zero skipped. The bound-source CLI took about 1.05 seconds/159 MiB peak working
set including process startup, compressed source materialization and full hash
verification. It is an offline decision-boundary consumer, not a per-frame read
or extra full scan. This slice passed PR #447 final CI `42059282002` and
merged as `bb336431f0dda7e4c1954a447455beea8d3f364b`; its branch is removed.
Step 2 is accepted, not a reason to reopen the reader or repeat its native input.

## Independent Current Comparison

Step 3 is implemented in `CurrentSkillTeacherPreferenceBuilder` and its small
candidate/time-admission/comparison modules. Public Bootstrap commands are
`build-current-skill-teacher-preference --snapshot ABS --output NEW.json`
(optional `--strategy-ledger ABS`) and
`verify-current-skill-teacher-preference --preference ABS --output NEW.json`.
Both refuse an existing output; Verify rebuilds original inputs and compares
the complete stored artifact, including negative training/outcome declarations.

The producer rebuilds the original registry/evaluator/ranker from one source,
neutralizes learner scores and invokes the original direction binding and
compiler. The catalog's 16 strategy/mixed option families retain individual
availability, missing-state and blocker audits. Its five executor-calibration
families remain compiler-owned support, not new strategy labels. The new typed
XP facade materializes only formats already accepted by the original binding
guard, preserving its rejections. The July introducing commits `4a01e38a` and
machine extension `786e6f0b` were inspected; retained JSON only contained later
references, not the original design conversation. No original rule was called
incorrect or rewritten. The crab-pot consumer alone adapts its existing exact
`expected_skill_id/expected_skill_experience_delta` into the Skills input format.

The ranker already invokes its timeline scheduler. A legacy immediate native
candidate may still leave `AllowedNow/AllowedToday` null, which the old binding
correctly refuses. The new producer can certify that local case only from an
original same-state accepted plan and pending native queue, on the loaded
location and within the original time/energy budgets. Explicit false, gate,
wait, opening/closing window, connector, cross-map step, continuation, identity
drift or unknown cost is not converted to true. This proves current permission,
not a future calendar; no old nullable-field or binding rule changed.

Comparisons require matching complete native-effect signatures, including
quantity/item/slot/value, all nongeometry parameters and unknown effect text.
Only four explicit tile coordinates, the original crab-pot stand prefix, and
this producer's already-verified minute-cost field are excluded. Five useful
XP ranges are capped to actual remaining base-skill deficits. A preferred
candidate must guarantee at least the alternative's possible XP in every skill,
with no greater ticks/compiled minutes/energy and at least one strict improvement.
Luck, capped XP, random misses, incomparable tradeoffs and ties are not fabricated
labels. Physical aliases are excluded. Equal-effect grouping and one dominance
witness per dominated choice avoid quadratic cross-family output; when a unique
frontier winner exists, its explicit comparisons cover the other admitted choices.

Actual read-only input is the retained content-addressed
`master-angler-terminal-train-20261105-a/.../before-snapshot-0001.json`, source
SHA `e288a6c6780b6134065234403f436e9a705fd95893b019c8b7e1dbec8aad164e`,
state `2cf253b2c6258e8f8b09c39361e7c91c8c5a87809eeae1a2410a3af680364cae`, tick 128.
The two native-legal crab-pot candidates have equal exact 5 XP/native effects;
the producer independently prefers 30 ticks/1 compiled minute to 210 ticks/5 minutes.
Final `current-preference-d.json` and its cache-disabled public Verify replay
are identical SHA
`016d4cf256c7bbabadf0f3d3ec1be6889fbf0e56850bead976da00dec1492165`.
They remain outside Git at `I:\StardewAITraining\skills-local-20261105`.
The initial unadapted binding rejection is retained as diagnostic `b`, not
misreported as an executor failure or repaired by weakening its flags.

All 48 new checks, complete game-free Core 1612/1612 and Backend 594/594 pass,
zero skipped. Public CLI rejects a promoted formal-training declaration and
source overwrite without creating a success artifact or mutating the source.
The E DLL still has the versioned hash above. The final producer takes about
2.21 seconds/183 MiB peak working set including startup and exact source reading;
it is not a per-frame scan. PR #448 accepted this slice with final CI
`42067595027` and merged as `99d352ca9f0b5efdba562033da45f7777d6afb54`;
its owned branch is removed. Step 3 is not pending or to be reimplemented.

This is retrospective replay of a saved pre-action snapshot, not a new live
Skills decision, native XP/15/25 outcome, dataset row, full method/corpus gate or
formal training. Do not splice the old Master Angler execution into this new
decision's history. Next is step 4: generate this preference before a new
isolated native execution, retain the same-run queue/nonce and actual base-XP,
base-level and scoring-threshold transition, then connect the original source
join/corpus and partition contracts. An explicit boundary fixture may declare
prior XP/levels; it cannot claim their acquisition or inject the measured gain.

## Same-Run Native Threshold Sources

Step 4 now has two new real threshold sources. Before each action the independent
current comparison selected between equal-effect 5-XP crab pots (30 versus 210
projected ticks); `SkillTeacherDispatchBuilder` then used the original
`DailyPlanCompiler`/`ActionQueueCompiler` and the existing one-shot precompiled
loop with native feedback and `--skip-training`. No loop execution, bridge,
candidate producer or movement semantics changed; there is no second executor.

| Run | Declared Prior Fishing XP/Level | Actual Native XP/Level | Scoring Level | Native Ticks |
| --- | --- | --- | --- | --- |
| `skills-level15-train-20261106-b` | 3295 / 5 | 3300 / 6 | 14 to 15 | 128 to 155 |
| `skills-level25-train-20261106-b` | 14995 / 9 | 15000 / 10 | 24 to 25 | 128 to 155 |

`SkillTerminalSaveBoundaryBuilder` prepares only a fresh byte-identical isolated
copy. Shared path/atomic-replace safety was extracted unchanged from the Master
Angler boundary; its regressions remain required. The fixture and deficit
consumer share the single version-pinned XP ladder. Prior levels/XP, ready pots
and second-year spring day 7 are explicit local prerequisites, not evidence of
prior acquisition, travel, placement, baiting or overnight production. No measured
five-point gain, inventory, achievement or pending profession choice was injected.
Other four skills had zero measured XP change.

Public `build-skill-native-threshold-receipt --request ABS --output NEW.json`
rebuilds the independent comparison and original compilation, then invokes the
original strong native reader for mechanical queue, journal/gzip/nonce, actor,
run/version, exact before/after frame and native verified outcome. Every compiled
item must match the independent selected action semantics. Complete materialized
decision JSON must equal the native before frame, including unknown root fields;
state hash alone is insufficient. Raw decision and compressed manifest physical
hashes stay separately recorded. Original hash/reader rules were not relaxed.
`verify-skill-native-threshold-receipt` independently rebuilds the entire receipt.

Receipts and public Verify replays are identical outside Git in
`I:\StardewAITraining\skills-local-20261105`:

- `native-level15-b.json` / `native-level15-b-replay.json`, SHA
  `6c86c4daeacfae7db1f3c18f46eac1607a4f5d041c1500bd88738abce3fb45ee`.
- `native-level25-b.json` / `native-level25-b-replay.json`, SHA
  `9eaffbcf6285c61794d556eab6b77fedfb69bed38c7cdf041d22f9314ae2ed8d`.

Retain their immutable request/source dependencies under the corresponding
ignored `artifacts/runtime-fishing-daily-plan-smoke/<run>` directories, not just
the small reports. The source save directory SHA remains
`68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d`.
Games were hidden/silent and serial; source hashes matched and owned processes
exited. Both sources are day 118, not independent policy partitions.

Diagnostics remain retained. First 15 preparation stopped before execution:
this new caller omitted the original queue-item attempt bound. Corrected 15
executed successfully, but its new consumer initially compared raw-file and
manifest-file hashes. Correct full-frame materialization allowed offline
build/Verify of that same successful action, without recollecting it. First 25
preparation stopped at the old freshness guard before game launch; explicit
rebuild, not bypassing that guard, preceded successful 25. These are not extra
native successes. The public `Test-SkillNativeThresholdReceipt.ps1` probe rejects
six wrong-run/threshold/after, forged-formal/hash and input-overwrite cases without
success artifacts or input mutation. Eight new Skills preflight rejections
preserve all six original cases. Full game-free Core 1634/1634 and Backend
597/597 pass, zero skipped. The old collection-source protection test introduced
in `0916debce` still forbids filtering to a single fishing candidate: only its
first-branch text locator was updated for the new mutually exclusive Skills mode.
That original intent was checked in the introducing commit; the corresponding
local November 5 session directory was unavailable, not claimed as recovered.

Final publication is governed by this slice's PR CI and closing comment; no
repeat native sampling is required just to publish the code. Step 4 sources
are implemented, not typed Teacher dataset rows, corpus,
executable method gate, three-partition coverage or full Skills closure.
Frozen union remains 7/19 executable, 7/19 split-complete, 7/19 ready and 6/11
roots; formal=false. **Next is step 5:** connect these retained sources to the
original Teacher row, criterion corpus and coverage; assess the original local
method/prerequisite responsibility, then collect only missing policy partitions.
Do not repeat successful terminal actions because a wrapper changed, reopen old
accepted roots, or add full-new-save acquisition as a local source prerequisite.

## Original Policy Teacher Rows

PR #449 accepted step 4 at head `18d3ff90`, CI `42076554119`, merge `1681ad9b`.
This slice implements the Teacher-row portion of step 5 without recollecting
either successful terminal action. The original `policy_decision_trajectory.v2`,
feature projector, trajectory builder, dataset, trainer and date partitions are
reused. The sampler now records `skills_teacher_row_path` after native evidence.
Its ordinary loop feedback JSONL is not an independently supervised Teacher row.

The original supervision guards (`e5e95819`, November 3) were inspected along
with the retained session's matching commit/progress context. Their independent
source replay requirement remains intact. Collection v1/v2 still require all
original eight hashes; no CC hash is invented for Skills. A named supervision
source subtype, `policy_teacher_supervision.skills.v1`, requires the actual native
Skills receipt and four common source hashes, and rejects borrowed collection
fields. New optional properties are omitted when null, preserving old serialized
rows. This is not a second trajectory format, dataset builder or training loop.

The exact `(teacher_id, teacher_version)` replay registry routes only explicitly
registered Collection and Skills providers. No unknown-provider fallback or
caller-selected label generation is allowed. The original CLI dataset/model
entry points and Backend use the same registry; single-provider injection still
works. Only explicitly compared eligible candidates get labels. The row rebuilds
the complete neutralized source candidate context, including uncompared choices,
and rejects a self-consistently rehashed but reduced denominator. Existing
backfilled-return handling in dataset admission is unchanged; the public raw-row
Verify compares the complete unbackfilled source row, including returns and
unknown fields.

`build-skill-teacher-row --native-receipt ABS --execution-receipt ABS
--trajectory-id ID --output NEW.json` consumes the unchanged step-4 receipt and
original complete queue receipt. It reuses the existing native reader, aggregate
queue validator and episode/context builders, and also joins the exact individual
execution path, nonce and observed facts. `verify-skill-teacher-row --row ABS
--output NEW.json` independently replays all sources. Internal verified-source
context is reused within each rebuild, not reread through a second verifier.

The generic feature projector intentionally has a compact base view. Inspection
of the actual first rows found total level but no individual skill XP in that
view. Before publication, an explicit snapshot-aware Skills projection was
added, shared by this row builder, Backend learned ranking and loop feedback.
It validates the original snapshot/deficit and exposes all five base/effective
levels, XP, remaining XP, explicit caps and 15/25 base-level deficits. Luck is
excluded; an at-cap next-level field is omitted, not invented. Unknown or
cross-state Skills input rejects. Other goals and the original projector retain
their old feature bytes. The existing model encoder already fits per-feature
means/scales; its normalization was not rewritten. The broader L3 all-goal input
integration remains explicit future work, not a rewrite of accepted old rows.

Two final real source rows and public replays in the same outside-Git directory:

- `teacher-level15-b.json`, SHA
  `bbc5d3da3ace21c202a51a3a4343433ec8eb591c5be8c7ee8dbb4b9f952b38a3`.
- `teacher-level25-b.json`, SHA
  `ae0d1b9e363246618a789b240199b998e124f5e21e06413cdaf2acf6d0b40254`.

`scripts/Test-SkillTeacherRow.ps1` independently verifies those two rows and runs
the original PolicyDataset CLI with the accepted Collection row from
`I:\StardewAITraining\master-angler-terminals-20261105\train\teacher-row.jsonl`.
Final result in `teacher-mixed-probe-c`: 3 accepted, 0 rejected, 3 train/0 validation/
0 test; manifest SHA `54997c147051bff2405a4d76f660c61937e2b99c38386c5bf90c8ba1ed175642`.
Nine public outcome/source/provider/partition/schema/return/unknown-claim and
overwrite probes reject without output or input mutation. Initial probe `a`
correctly rejected the old loop's selected-only feedback; it is retained as a
diagnostic, not repaired by relaxing admission or counted as an accepted row.
The initial `teacher-level*-a`/probe-b artifacts predate the per-skill input
addition and are superseded, not native evidence failures. The b rows were
rebuilt from the very same accepted native receipts, without new game actions.
The Backend's unchanged test also caught a new registry error-classification
regression: unknown identity must retain `teacher_supervision_identity_mismatch`,
not become `teacher_replay_verifier_unavailable`. The new router was corrected;
the old assertion and all old source guards stayed in place.

All 30 new checks, full game-free Core 1664/1664 and Backend 597/597 pass,
zero skipped. Dataset/model CLIs build with zero warnings/errors; original
fishing mapping/preflight, script AST and EVD-338 checkpoint checks pass.
The mixed replay-port unit test also exercises the original trainer with a
tiny synthetic fixture; it is not product model training or native coverage.

These are two genuine supervised Skills source rows, not a criterion corpus or
coverage promotion. Both remain the original day-118 train group. Native receipt
`teacher_dataset_row_verified=false` is an immutable step-4 scope declaration,
not rewritten retroactively; the downstream row has its own original replay.
No game, worker/API, server or product model training was started in this slice;
local-data and all raw/derived data remain outside Git. Publication is governed
by this slice's final CI and closing comment.

**Next:** finish step 5's typed criterion corpus and original coverage/source
registration, assess the original local method responsibility, then collect only
missing policy partitions. Reuse the existing per-criterion evidence path (as in
the Money threshold adapter): 15 and 25 must each retain their own comparison /
native partition intersection; do not credit 25 from a 15-only terminal or union
unrelated partitions at method level. Preserve the rows and source closures;
do not restart the native tests or claim full Skills readiness. Frozen union
remains 7/19 executable, 7/19 split-complete, 7/19 ready and 6/11 roots;
formal=false. L2/L3/L4 responsibilities remain as defined below.

## Implementation Order

1. Verify the actual E-drive game version and live decompilation of base XP
   thresholds, XP application/unmodified levels and Grandpa scoring. Cross-check
   the relevant wiki rules. Inspect the introducing commits and available chat
   JSON before treating any old shared rule as a defect.
2. Add one typed current XP-deficit consumer using the original snapshot
   reader and source fields. Include all five vanilla scoring skills, exclude
   Luck as a trainable XP source, separate effective buffs from base levels,
   and distinguish next-level facts from an unprovided full threshold ladder.
   Unknown/missing/modded or inconsistent state is explicit, not inferred zero.
3. Reuse admitted exact positive-XP candidates and original timing/resource
   contracts for independent alternatives. Planting/watering maintenance remains
   compiler-owned, not a new policy label; zero-effective Luck XP, duplicate
   native actions and effects unrelated to the selected deficit are not progress.
   Do not invent preferences for ties or random non-hits.
4. Bind the selected comparison to the unchanged compiler/executor and original
   fresh before/after receipts. Verify exact XP, base level and the applicable
   scoring threshold; native partial XP is not a finished criterion. Preparation
   of an isolated boundary is declared as a prerequisite, never prior acquisition.
5. Connect a rebuildable source/corpus through the original coverage and
   reconciliation entry points. Preserve original policy partitioning and exact
   run/save/player/state/tick/queue identities. Do not clone an executor, reader,
   verifier or coverage calculator merely to accommodate this consumer.

Each slice uses focused positive/negative checks first. Native runs use only
the isolated E runtime, hidden/silent, one process at a time, with unchanged
source-save hashes and bounded logs. Keep reusable successful evidence rather
than repeating it because a later wrapper or document changed. Test before
pushing code; no local-data, raw saves, model data or large diagnostics in Git.
No worker, paid AI API or server operation is enabled by this intake.

## Local Exit And Global Work

Local closure requires an executable method with lawful typed prerequisites,
independent comparisons and actual native outcomes for both criteria under the
existing train/validation/test contract, plus a rebuilt original coverage
intersection with no local open work. Representative source-family sampling
is allowed; synthetic checks or a single positive XP delta do not satisfy it.
No Skills coverage numerator changes until those facts exist.

The canonical graph's fresh-save five-skill deadline, travel/energy and competing
21-point obligations remain L3/L4 integration work. They are not silently marked
complete, deleted, or reintroduced as a whole-new-save prerequisite to L1.
Where a selected local route needs a shared capability, bind its actual L2
certificate rather than assuming it from the presence of a function.
Full Teacher 21/21, complete new-save play and subsequent Student training and
evaluation remain unchanged project goals.
