# Friendship Local Intake

## Current Scope

The sole primary root is `raise_friendships`, covering `friendships_5` and
`friendships_10`. PR #453 passed all 38 CI steps at head
`a0c6a1384908352c5776889536a45270db362ccb` and merged as
`712482eac6f1262f309e55ffb7dee4f07699a738`. The accepted union remains
9/19 executable, 9/19 split-complete, 9/19 ready and 7/11 roots.
Friendship intake does not increment these counts or enable formal training.

`CurrentFriendshipDeficitBuilder` is a read-only current-facts consumer. It uses
the existing snapshot hash and readable-status contracts, the bridge's existing
`npcs.grandpa_friendship_progress`, and the original Bootstrap artifact reader
and JSON writer. No bridge, planner, scoring evaluator, candidate binding,
compiler, executor, trajectory admission or trainer rule was changed.

The public command is:

```powershell
dotnet experiments/StardewAI.GoalConditionedBootstrap/bin/Release/net8.0/StardewAI.GoalConditionedBootstrap.dll build-current-friendship-deficit --snapshot <original-snapshot.json> --output <new-output.json>
```

Output follows the existing Bootstrap camelCase convention. An existing output
or non-JSON output is rejected. A blocked result exits 2, with null counts and
empty rows; it is not an empty successful population. The source envelope must
be `snapshot.v1`, game 1.6.15, nonnegative tick and valid original state hash.

## Authority Rechecked On 2026-11-06

The current isolated E runtime DLL was hashed and Utility/Farmer were freshly
decompiled with the installed ILSpy 9.1 tool, not inferred from documentation
examples. Files are outside Git at
`I:\StardewAITraining\friendship-local-20261106\decompile`.

| Source | SHA256 |
| --- | --- |
| E runtime `Stardew Valley.dll` | `7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee` |
| Utility decompile | `f6c4599a8fe6591f1c519299dfb2e363164d80f72ad71640465370f699c61181` |
| Farmer decompile | `6556990203ed8b6596626000d06f180e66c1256702dc1a6c7f1f1557854d0dc6` |

`Utility.getGrandpaScore` adds one point at each native count >=5 and >=10.
`getNumberOfFriendsWithinThisRange(player, 1975, 999999, false)` counts each
qualifying `ForEachVillager` iteration. Its `ForEachLocation` defaults include
interiors but exclude the separate generated-location traversal
(`includeInteriors=true`, `includeGenerated=false`); event actors and
non-villagers are excluded.
`Farmer.tryGetFriendshipLevelForNPC` returns null for an absent dictionary
record, not zero. Native scoring does not require datable NPCs or round to a
displayed heart count. The inclusive upper bound is retained too.

The [Grandpa wiki](https://stardewvalleywiki.com/Grandpa#Scoring) corroborates the
five/ten-villager scoring structure; its eight-heart shorthand is not the exact
1975-point native boundary. The [Friendship wiki](https://stardewvalleywiki.com/Friendship)
describes 250 points per displayed heart. The pinned native code, not that
shorthand or a future wiki edit, governs this versioned consumer.

## Current-Facts Contract

- Require exact scoring constants, complete native-iteration status, declared
  population count, contiguous original iteration indices, names, villager and
  event flags, record-existence flags, nullable points and qualification flags.
  Recompute the qualifying count and reject any contradiction or duplicate
  consumed JSON property. No unknown field defaults to zero/false.
- Preserve iteration order and instances. Repeated names with the same native
  dictionary value remain in the scoring denominator and are marked
  `uniqueNpcIdentity=false`; conflicting values for one name block intake.
  This marker is not action availability. The original social binding retains
  its stricter unique-target requirement.
- Explicit absent record plus null points is a known native fact, distinct from
  a missing/unreadable points field. Its point deficit stays null. Only an
  existing record at/below the native maximum receives a numeric additive gap;
  already qualifying rows have zero, above-maximum points have null, and
  negative native integers use a wide integer gap without overflow.
- Current population deficits are `max(0, 5-count)` and `max(0, 10-count)`.
  They say nothing about route feasibility, remaining calendar time, gift
  stock, overnight decay, future NPC presence or the least-cost portfolio.
- `trainingLabelEligible=false` and `nativeOutcomeVerified=false` always.
  Reading an existing state is not a demonstrated action or preference label.
  Day-transition and route inputs are not reverse prerequisites for this read.

## Retained Actual Source

No new game or duplicate native acquisition was run. The public CLI consumed
the original `skill-teacher-before.json` in
`artifacts/runtime-fishing-daily-plan-smoke/skills-level25-test-20261106-a`.
It is an original full source snapshot, not the PowerShell reserialized
diagnostic `fishing-snapshot.json`.

- Physical source SHA256:
  `db85cb906bc3aedf5518b6a8b7b1cad0c3c32dac2365cf5808f6207c82f652a8`.
- Source state hash:
  `ab8f387d21914cba6171eecba1621cd54308eeb266039bf05cb15ccbafda9bfc`, tick 128.
- Ready current read: 40 native instances, qualifying count 0, five/ten deficits
  5/10; 37 absent records remain null, three recorded zero values have gap 1975.
  `Mister Qi` occurs at native indices 31 and 39; both instances are preserved
  and marked ambiguous. This is not claimed to be a new-save social population.
- Two independent public rebuilds, `current-deficit.json` and
  `current-deficit-rebuilt.json` under
  `I:\StardewAITraining\friendship-local-20261106`, have identical SHA256
  `6404205b903b81cb1ac840d8d40b55294ffa958d3a6b648dd415498236d94aa6`.
  Attempted output reuse was rejected, output/source hashes unchanged.

45 focused tests cover inclusive thresholds and count boundaries, absent versus
unreadable records, native duplicate identities, malformed/conflicting fields,
integer overflow, version/hash/frame checks and immutability. Full game-free
Core 1750 and Backend 597 passed with zero skips, as did the PowerShell AST,
EVD-338 checkpoint and whitespace checks. Final CI/publication results belong
in the PR closing record, not inferred from local tests.
No raw evidence, decompiles, local-data, API calls or product model training are
part of this commit.

## Sole Next Slice And Exit Boundaries

Update 2026-11-06: the independent comparison and original-compiler dispatch
consumer is implemented; see [exact compatibility and verification scope](friendship-independent-comparison.md).
The immediate next slice is real same-run native threshold outcomes, not another
current-deficit read or another social executor. The sequence below retains its
full original exit obligations; synthetic protocol checks do not close them.

1. Current deficit intake is implemented. Next, adapt the existing
   `CurrentSocialDayTeacherLabelBuilder`, ordered itinerary/frontier and original
   talk/gift binding to an independently replayable comparison source. The
   existing selected-label object alone does not prove rejected alternatives.
   Record the actual denominator, neutralize learner influence and declare the
   policy's bounded/current-day scope. Do not manufacture ties or preferences.
2. Reuse the original plan/queue and social native executor for same-run actual
   outcomes crossing each local count threshold. Preserve exact source frames,
   run/save/actor, candidate/queue/item/nonce and the actual before/after native
   population. An existing score or fixture setup is not action evidence.
3. Feed original trajectory/dataset/coverage/reconciliation paths with a typed
   source, original partitioning and criterion-specific train/validation/test.
   Independent resets use the explicit independent-decision dataset mode; do
   not infer continuous returns or borrow one criterion's evidence for another.
4. Close only the demonstrated local method responsibility under R2, preserving
   day/weekly gift limits, decay, multi-day portfolio/supply/routes and full
   new-save resource/time obligations as their actual separate responsibilities.
   Retain the original `nearest_threshold_then_earliest_finish.v2` itinerary
   policy and target-ambiguity protections unless a separately traced real bug
   is established. Do not require global Teacher21/21 as an L1 precondition or
   call a current-day policy globally optimal.

This is one sequence, not a second social system. Skills/Rusty/Master and older
accepted gates stay frozen. Marriage/house, Museum and Money remain queued;
Teacher21/21, Student, perfection and human-adaptation goals are unchanged.
