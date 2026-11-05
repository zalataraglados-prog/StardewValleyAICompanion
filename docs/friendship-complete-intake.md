# Friendship Complete Offline Intake

This intake is accepted in #457 (`75e7d9a0`). The new consumer's complete
cross-map contact proof is now implemented; follow [its actual result and
explicit pairwise continuation](friendship-route-contact-budget.md), not the
historical next-step paragraph below.

PR #456 passed all 38 CI steps at `eb8333d4`, merged as `c53b6a0d`.
Its real movement/connector calibration is accepted and is not being repeated.
The sole primary remains `raise_friendships`; accepted totals remain 9/19
executable, 9/19 split-complete, 9/19 ready and 7/11 roots, formal=false.

## Compatibility Boundary

The old routed social source deliberately applies a 64-candidate window.
Introducing commit `354603b6b` was inspected; a bounded retained-JSON search
did not recover its detailed conversation. Do not invent that context or treat
the runtime budget as a bug. Both original public evaluator constructors still
use 64, with identical route lowering, ordering and cache ownership.

Only `CandidateOptionAvailabilityEvaluator.ForCompleteOfflineSocialComparison()`
opts into the complete original social source. The new Friendship consumer uses
it; runtime callers, old Teacher, native executor, binder, ranker and compilers
are unchanged. No second candidate generator or route implementation is added.

Preference artifacts now use `current_friendship_teacher_preference.v2`, with
the explicit source-policy ID, actual social occurrence count and applied limit
(null for this offline intake). Old v1 diagnostic/protocol artifacts are retained
but must be rebuilt from their sources; they cannot silently acquire the new
completeness claim. Verify rebuilds the whole v2 artifact from original inputs.

All social duplicate IDs remain visible. If any occurrence is available,
allowed-now/today, or lacks an explicit rejection, that ID blocks admission.
When every occurrence is explicitly unavailable with neither time permission,
the full original duplicate occurrences and reasons are retained in the audit,
not deduplicated away, dispatched or used as negative labels. Such unavailable
duplicates do not block unrelated targets. Cross-family aliases retain the old
ranker's semantics; missing fields and omitted available IDs still block.

## Actual Retained-Source Replay

No game was launched and no source file was edited. The real raw snapshot and
calibration are the retained sources recorded in [real calibration](friendship-real-calibration.md).
Public Build/Verify outputs are outside Git:

`I:\StardewAITraining\friendship-local-20261106\complete-offline-intake-b.json`
and `complete-offline-intake-b-verified.json`, both SHA256
`d2d7323bed2eaeff00000e407ea59e206687b31d8fc89511620fd22f275c49d4`.

- Gift source: 410 complete occurrences, 0 ranked/currently available choices.
- Talk source: 43 occurrences, 42 distinct IDs, 30 ranked choices.
- Two native Mister Qi instances (Club and QiNutRoom) both cannot socialize;
  their duplicate route ID and both unavailable occurrences remain in the audit.
- Complete denominator is true; no missing fields, omitted available IDs,
  capped source or potentially available ambiguous ID is hidden.
- The original Teacher is ready and proposes Lewis via Farm toward ManorHouse;
  its verified itinerary has three connectors and calibrated contact at 08:40.
- The independent consumer is still blocked with
  `original_social_control_not_in_admitted_comparison_denominator`: its immediate
  geometry adapter does not yet support these cross-map candidates. No NPC
  threshold action, preference label or training sample is claimed.

The earlier `complete-offline-intake-a.json` retains the first full-source
diagnostic before the unavailable-duplicate distinction; it is not an accepted
supervised source. It was not overwritten or deleted.

## Tests And Sole Continuation

The 64/65-source tests verify both old constructors keep their original prefix,
the explicit offline source retains every occurrence, and subsequent calls on
the runtime instance remain bounded. At 65, the old immediate compiler matcher
also has a separate bounded source: that last candidate remains visible but is
uncompared/rejected, not bypassed or falsely labelled. This does not affect the
actual 43-occurrence talk source; it remains an explicit compatibility limit.
Available duplicate IDs still block; unavailable duplicates retain both full
records. Legacy-schema and edited-limit claims fail replay. The focused social,
comparison and itinerary suite passes 53 checks without skips. Complete local
Core 1806 / Backend 597 pass without skips, as do all 242 PowerShell AST checks,
the unchanged EVD-338 checkpoint and whitespace checks. Final CI is recorded in
the PR closing record.

**Next: adapt only the new comparison consumer's cross-map case to the existing
calibrated contact frontier, complete route budget and original one-connector
compiler/replan contract.** First replay the same retained actual sources.
Do not compare only first-connector cost as if it were complete contact cost,
invent remote stand coordinates, or change the old runtime executor to satisfy
the new consumer. Preserve incompatible route/resource tradeoffs explicitly.
Then collect isolated same-run five/ten native thresholds and join the original
trajectory/corpus/coverage chain, without reopening accepted movement evidence.
L1/L2/L3/L4, full Teacher21/21, Student, perfection and adaptation scope are
unchanged. No worker/API, server, model training, raw-data commit or cleanup.
