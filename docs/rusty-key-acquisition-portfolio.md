# Rusty Key Acquisition Portfolio

## Scope And Reuse

`RustyKeyAcquisitionPortfolioBuilder` attaches the Rusty Key deficit to the
existing `AcquisitionRoutePortfolioBuilder`; it does not introduce a planner,
optimizer, reservation ledger, donation compiler or native executor.

The authority remains the complete museum requirement inventory and its
independently rebuilt acquisition axes. Museum per-item progress and exact-item
shape checks are shared with `CurrentCollectionTeacherFrontierBuilder`.
The adapter reads the retained museum value directly; it does not serialize or
duplicate the full snapshot just to call those shared checks. The full state-hash
integrity check remains required once for admission.
The existing acquisition goal alias `goal.grandpa_21` and canonical
`grandpa.maximum_21` remain supported without changing the source goal identity.

The live reward must retain the locked `museum60` / 60-item /
`MarkEventSeen Host 295672` contract. Source bindings, complete per-item counts,
snapshot state hash, save/player, proposal and current ledger revision must agree.
The result keeps every museum item, including already-donated and unselected
items. It never substitutes a handpicked smaller authoritative denominator.

A proposed supply portfolio must select exactly
`max(0, live_threshold - donated_count)` distinct undonated requirement groups.
Each selected group uses the existing exact-item route admission. Over-supply,
duplicate items, already-donated items, foreign scopes, unverified continuation
credit and replacement of reservations belonging to other requirement scopes
are rejected. The shared atomic resource preflight checks concurrent material
and currency claims against active commitments without changing the real ledger.
No new reservation authority or automatic cancellation path is added.

Supply dates may not precede the source day and must leave the following
morning before the Stage One exclusive day-224 deadline. This is a necessary
bound only: it does not prove that later donation routing and opening hours fit.
At 60 or more donated items, empty acquisition transitions to native key
handoff; extra acquisition is rejected and possession of the key is not inferred.

## Entrypoint

`build-rusty-key-acquisition-portfolio` takes the same source arguments as
`build-acquisition-route-portfolio-admission`, including `--proposal` and
`--output`. It rebuilds the shared opportunity/ledger context before evaluating
the proposed threshold scope. There is no caller-supplied admission shortcut.

Optional `--portfolio-admission-output <path>` writes the unchanged nested
generic admission only on success. Existing reservation commit, execution
binding, dispatch and fresh terminal receipt tools consume that artifact through
their existing interfaces. Rejected or already-satisfied scopes do not write
this dispatch-facing output. The main `--output` is diagnostic evidence, not
execution authorization; blocked proposals return exit code 2.

## Evidence Boundary

`AcquisitionPortfolioReady` means threshold-scoped supply/resource preflight,
not that the entire supply-to-key trajectory has passed. The result explicitly
keeps the following false:

- `joint_execution_schedule_verified`;
- `donation_output_custody_verified`;
- `native_donation_and_key_handoff_verified`;
- `teacher_preference_verified`;
- `formal_training_authorized`.

The generic cost vector aggregates elapsed time; this must not be confused with
a verified sequential multi-route schedule. Output acquired later must still
be freshly reserved/protected until native donation. A legal proposal alone is
not proof of its preference over another legal proposal; #394's independent
explicit comparison rules remain in force.

The dedicated `self-test-rusty-key-acquisition-portfolio` uses an explicitly
synthetic 95-item inventory and axis chain to exercise threshold composition
and the real shared reservation service. It tests zero-to-60, 58/59 boundaries,
already-satisfied 60/61/95 states, invalid scopes/identities/rewards/dates,
material overbooking and other-goal protection. The final local run passed all
30 cases. It does not claim to replay
authoritative acquisition-axis compilation or native play. The separate
current-stage-one collection diagnostic replay did complete on `main@085f95a1`;
the earlier `collection-replay-v4` file-access failure was not reproduced, and
neither its cause nor a repair is claimed. Both records remain local.

## Next Closure

The separate [rolling producer](rusty-key-rolling-supply.md) now generates legal
one-item next-step proposals and existing held-donation queues. These are not
automatically full-threshold certificates; this adapter's exact-deficit rule is
unchanged. The controlled runtime now consumes the existing supply/commit and
output-custody lifecycle, including the accepted same-map store/withdraw/donate
boundary. That shared proof does not close the full fresh-save threshold.

Continue the same Rusty Key primary: bind chosen supply to the existing rolling
execution/reservation lifecycle and exact native donation candidate, retaining
the output until it is donated. The [local time gates](rusty-key-time-budget.md)
now reject over-budget held work and inconsistent clocks before offering an
executable queue.
Verify the joint time/opening-hour and following
morning handoff rather than relying on summed costs. Then join this criterion's
independent Teacher comparisons, native outcomes and train/validation/test
partitions with its actual pre-Year-3 completion. The retained Year-5 native
threshold smoke is a shared capability, not evidence for that deadline.

Do not promote frozen coverage counts, require the complete 95-item Museum
achievement, restart unrelated roots, repeat frozen Pet Love/Skull Key evidence,
or substitute a full Day-0 global run for this local closure.
