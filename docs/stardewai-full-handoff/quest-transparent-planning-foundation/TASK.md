# Quest Transparent Planning Foundation: Implementation

Implement a bounded C# current-state quest planning foundation in this sandbox.

Read `ACCEPTANCE.md`, `APPROVED_QUEST_AUDIT.md`, and `CONTROLLER_REVIEW.md` first. Recheck every field against decompiled source; do not trust `DEEPSEEK_FINDINGS.md` where it conflicts with controller review.

Required scope:

1. Extend transparent quest reads with side-effect-free direct fields for every concrete ordinary runtime class, including base `Quest`, and every special-order objective/reward runtime class. Preserve explicit unavailable/unsupported status per row.
2. Add structured current-state quest/special-order candidates with stable identity, progress facts, next high-level action category, exact known targets, blocked diagnostics, and provenance.
3. Wire unbound `quest.advance` availability to these candidates without enabling runtime execution or default policy training.
4. Add a compiler output envelope for the selected quest candidate and preserve exact live evidence, but hard-block with `quest_native_executor_not_implemented` and unknown time/energy.
5. Replace fixed `quest.advance=120` timing with an unknown rule.
6. Add focused tests for runtime-type disambiguation, type-9 subclasses, base Quest, ordinary/special-order separation, missing-field fail-closed behavior, blocked diagnostics, unknown cost, and compiler envelope. Do not run them.
7. Update concise evidence, transparency coverage, risks, notes, and pending test records.

Do not implement low-level quest gameplay executors in this slice. Do not call quest event/probe methods from the observer.

Hard constraints: no build, tests, game, SMAPI, runtime, smoke, deploy, credentials, real-repo edits, process interaction, reset/clean/rebase/push, or state mutation. Commit the final sandbox result.
