# Marriage And House Current Intake

## Current Primary

Friendship PR #465 passed all 38 steps of CI `42208557116` and merged as
`c0e7cc80`; its two criteria, train sources and calibration are accepted and are
not reopened. The accepted union remains **11/19 executable, 11/19 split-complete,
11/19 ready, 8/11 roots, formal=false**. Current primary is
`marriage_and_house_upgrade`; this first intake is not a criterion promotion.

The authoritative condition is one joint point, not two independent rewards:
`player.married_or_roommate=true;farmhouse_upgrade_level>=2`. A current true value
is a fact, not proof that the current decision acquired marriage or a house.
The complete new-save partner/item acquisition, both upgrades, missed-window
calendar and competing 21-point budgets remain separate global obligations.

## Authority And Existing Ownership

The actual E runtime DLL was hashed and freshly decompiled for this slice:
`7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee`.
External `I:\StardewAITraining\marriage-local-20261106\decompile-a` contains:

- Utility `getGrandpaScore`, line 6157: native married/roommate AND actual home
  upgrade level at least two.
- Farmer lines 2803, 5119 and 5132: current-or-pending roommate includes settled
  roommates; native team marriage/engagement does not require an NPC spouse row.
- GameLocation `houseUpgradeAccept`, line 12827: native money/material tuples and
  three-day construction are distinct from completed house-level changes.

| Responsibility | Existing owner reused without changing behavior |
| --- | --- |
| Player/world facts and native upgrade quote | PlayerReadAdapter and WorldProgressReadAdapter.MarriageHouse |
| Current clock and conflicting clock aliases | SnapshotGameClockBudgetReader |
| Direction ownership, direct actions | GrandpaDirectionCatalog |
| All current transition families | Original unscoped dependency graph |
| Complete offline sources and unavailable rows | CandidateOptionAvailabilityEvaluator.ForCompleteOfflineSocialComparison |
| Exact costs, materials, benefits, service access and purchase legality | Existing FarmhouseUpgradeCandidates and original compilers |
| Bouquet/proposals, exact items, relationship and route gates | Existing SocialCandidateBuilder and social route/compiler chain |
| Mechanical housing, social and overnight execution | Existing action queue/native executors and recovery.stabilize_day |

No new executor, score rule, confirmation bypass, price table, Teacher policy or
candidate cap is added. Raw quote parsing does not authorize a purchase: an altered
positive price is retained for audit but rejected by the old native tuple gate.
Level-three capacity benefits are not removed merely because this score needs two.

## Public Intake

`build-current-marriage-house-intake --snapshot ABS --knowledge ABS --dependencies ABS --output NEW`
reads the complete current-authority 19-criterion/21-point index and exact joint
rule. It discovers all five original transition option IDs from the unscoped
direction: housing, partnership, talk, gift and day stabilization. Missing, foreign
or omitted families and stacked local-method views reject rather than shrinking
the denominator. The original direction, including all unresolved obligations,
is retained unchanged and hash-bound.

The Core state reader validates snapshot version/tick/hash, readable and explicit
calendar, and agreement between player and world projections for money, house,
construction and partnership facts. Unknown is not zero. Raw upgrade quote and
native service state are preserved without fabricating a future route/budget.
Pending engagement/construction is distinguished from completed state; a settled
roommate alone is not classified as awaiting settlement. Native team marriage
with an empty NPC spouse string is accepted.

The original evaluator is called once in inspection mode, not through exclusive
recovery or model ranking, so no option is silently removed. The complete raw
option/event/social arrays, unavailable reasons and missing fields are retained.
`currentTransitionSourcesReadable` reports source readability, not action legality
or a complete future acquisition inventory. Default runtime source limits and
execution authorization are unchanged.

`verify-current-marriage-house-intake --intake ABS --output NEW` independently
rebuilds from the exact original paths and compares the whole artifact. Both CLI
outputs must be new JSON files. All label/outcome/formal-training claims remain
false even for an already satisfied snapshot. No policy trajectory is produced.

## Actual Offline Results

External root: `I:\StardewAITraining\marriage-local-20261106`.
The accepted real source is the existing Friendship calibration initial snapshot;
no game or calibration is restarted. Its year-two winter-28 player is married to
Abigail, house level three, no pending construction. The report correctly says
current criterion satisfied, not acquired outcome.

`intake-a.json` and `intake-replay-a.json` have identical SHA256:
`9076133d2808e0c604b702b26d04ee4da382cd6e90ae1529a013b65c968b1936`.
All five source families are readable. All 410 gift rows (currently unavailable)
and 43 talk rows (13 unavailable) are retained. No house or proposal transition
exists in this already-complete source; no replacement candidate is manufactured.
`probe-summary-a.json` binds these counts and unchanged input hashes.

Tests initially exposed missing season/day, game version and tagged-field source
metadata in the new composition of an older minimal social fixture. Only the new
composite test supplies those facts; the old fixture, verifier, clock reader and
runtime were not changed. The final 46 focused cases include an altered quote
rejected by the original purchase gate. Three actual CLI probes reject an invented
label, an omitted original gift family and output overwrite, with no new outputs
or accepted-input changes. Final local Core 2022 / Backend 607 pass with zero
failures/skips; the existing EVD-338 checkpoint and whitespace checks pass. Final
CI/publishing results belong in this PR's closing record.
Raw snapshots/decompilation/probes remain outside Git and local-data is not added.
No worker/paid API, server operation, product training or cleanup is performed.

## Sole Next Slice

Implement an independent current Marriage/House comparison consumer over these
original sources, with explicit native calendar/settlement and resource commitments.
First prepare or inspect a genuinely nonterminal isolated boundary before action;
this already-complete source cannot supply a progress label. Reuse full social
contact/route budgets and the existing housing/partnership/day-settlement chains.
Do not manufacture alternative candidates or negative labels for incomparable,
unavailable or already-completed work.

A purchased upgrade is not a completed house, engagement is not marriage, and a
pending construction/wedding must not cause another purchase/proposal. Construction
and settlement must fit the original Year-3 deadline; an admissible action this
minute is not automatically a feasible scoring plan. Comparison/control mode must
state exactly what has been proved before any native dispatch. Then obtain real
same-run settlement and join the original Teacher/native/corpus/method/partition
chain. Do not narrow the final new-save 21-point/Student goal to this intake.
