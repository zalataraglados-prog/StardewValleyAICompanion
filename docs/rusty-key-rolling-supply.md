# Rusty Key Rolling Supply

## Scope And Reuse

`RustyKeyRollingSupplyProposalBuilder` produces a next-step proposal instead of
requiring a caller to hand-select an entire remaining threshold portfolio. It
reuses the threshold adapter's source/identity/reward validation and complete
per-item museum progress reader. The full museum denominator remains present,
including donated, unavailable and unselected items.

The existing full-threshold certificate is unchanged: its selected distinct
items must equal the live deficit. A one-item rolling proposal is not that
certificate when the deficit is larger than one. Missing current routes for the
other 59 items does not prohibit one valid next step.

## Selection And Outputs

1. Regenerate current museum candidates with the real strategy ledger. Pass them
   through the existing timeline scheduler, daily-plan compiler and action-queue
   compiler. Prefer a legal held-item donation/connector step, keeping material
   protection and exact snapshot/native donation parameters. No caller ranking
   or learner score selects the item.
2. If no held-item donation step compiles, project the shared material graph and
   active reservations first. Distinct owned undonated identities, including only
   history-authenticated custody for this goal/consumer, remain pending donations.
   Do not acquire them again merely because the museum is temporarily unavailable.
   Authorized storage is pending transfer, not immediate donation. If these
   identities cover the remaining deficit, stop supply explicitly without claiming
   threshold completion. Unknown inventory fails closed; protected other-goal
   units do not count. Otherwise rebuild the existing target-date
   acquisition context from its retained source axes. For each undonated museum
   item, propose one exact route occurrence through the existing generic
   portfolio admission and atomic reservation preflight. Reject stale source
   identities and a target date other than the current day. Other goals' claims
   cannot be cancelled or spent.
3. Select an admitted one-item proposal by declared elapsed minutes, then energy,
   then stable route identity. This is a controller policy, not a claim that it
   optimizes the entire remaining collection or strictly dominates alternatives.

Held selection exports the existing compiled plan/queue. Acquisition selection
exports an ordinary portfolio proposal/admission; it still requires the existing
atomic commit, execution binding, current dispatch checks and terminal receipt.
The producer does not create another executor, allocator or reservation store.

Fresh acquisition execution can now consume the independently rebuilt producer
report through `--portfolio-control-selection`, without manufacturing a Teacher
preference. See [control execution admission](control-execution-admission.md).
Native outcomes and custody settlement remain separate from preference labels.

At 60 or more donated items it proposes no further supply and reports that native
key handoff is still required. The source-day bound leaves at least the following
morning before exclusive day 224, but is not a joint donation/opening-hours proof.

## Entrypoint

`build-rusty-key-rolling-supply` requires `--requirement-inventory`, `--snapshot`,
`--strategy-ledger` and `--output`. Legal held-item donation needs no acquisition
forecast, Community Center completion or Master Angler local prerequisite.

Without a held step, provide the same retained source-axis arguments as
`build-acquisition-route-portfolio-admission`, without a caller `--proposal`.
Missing acquisition sources fail closed; they do not fabricate an acquisition.

Optional `--selected-plan-output` / `--selected-queue-output` export a held step;
`--selected-proposal-output` / `--selected-admission-output` export an acquisition
step. Blocked results exit 2 and leave existing selected-output files untouched.
The main report is diagnostic, not native execution authorization.

Held plan/queue/item/primitive identities now rebuild deterministically from the
retained source hashes and selected candidate, using the existing queue identity
stabilizer. Pending-donation IDs are an optional field omitted when empty, keeping
unaffected acquisition artifacts unchanged. See the
[pre-commit step preparation](rusty-key-step-preparation.md) entrypoint for current
execution-stage classification before committing any resources.

This CLI is now a usable proposal producer. It is not yet the autonomous live
loop's selected-root supply/commit orchestration; that integration must consume
these same artifacts and services, not create a parallel donation macro.

## Verification And Remaining Work

`self-test-rusty-key-rolling-supply` passed 25 synthetic cases, exercising the
real candidate/timeline/compiler and shared resource-admission code. Cases cover
partial deficits, only one currently usable item, preserved unavailable routes,
protected inputs, held spare units, deterministic ties, source drift, unknown
sources, date bounds and threshold handoff. The public held-file entrypoint and
actual CLI exported a pending `executor.donate_museum_item` queue. A blocked CLI
returned 2 without changing a pre-existing selected queue's SHA-256. The original
30-case full-threshold suite and focused independent Teacher replay also passed.

Early held-fixture attempts were rejected by existing field confidence/adapter
policy. The synthetic fixture was corrected to the established test metadata;
production validation was not relaxed. These attempts remain local diagnostics.

No game was launched for this slice. Synthetic museum identities and admissions
are not native acquisition, Teacher comparison, a training corpus or an earned
key. `full_threshold_supply_verified`, `donation_output_custody_verified`,
`teacher_preference_label_eligible` and `formal_training_authorized` are always
false. No frozen coverage count is promoted.

Continue the same Rusty Key primary: connect this producer to the existing
rolling supply/commit/execution lifecycle, preserve acquired outputs until exact
native donation, verify the joint time/opening-window and next-morning handoff,
and close the criterion's independent Teacher/native partitions and pre-Year-3
deadline. Existing Year-5 native evidence remains separately scoped.
