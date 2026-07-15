# Controller Audit

## Decision

Accepted for static integration on 2026-07-14.

The final source revision was not built or tested because the user was playing. No game, SMAPI, runtime, or training process was started by the controller.

An intermediate worker draft ran a sandbox-only build despite the no-build instruction. Its generated `bin/obj` files were excluded from the whitelist merge. The accepted final revision changed afterward and therefore has no valid build result.

## Accepted Boundaries

- The request must name an exact ingested `state_hash`; there is no latest-snapshot fallback.
- The live adapter is the only authority for direction labels, factors, scores, completion state, and feedback keys.
- The Core catalog contains binding policy only.
- `earn_money`, `raise_friendships`, and `complete_master_angler` may bind only current, available, timeline-legal candidates with exact permitted option/kind pairs.
- Candidate identity, score, reward, rank, action fields, and time estimates are preserved.
- Existing provenance must match exactly and occur once; conflicts and duplicates block the candidate.
- The direction set is rebuilt from the snapshot. Submitted ranked candidates are bound by the request-level state hash but are not independently hash-verifiable because their contract has no per-candidate state hash.
- The remaining nine directions are explicit planned-contract blockers. CC/Joja route commitment remains unresolved.

## Static Verification

- Controller reviewed the whitelist diff and normalized file contents.
- `git apply --check` passed before merge.
- `git diff --check` passed after merge.
- Tests are defined but not run.
- Build is not run for the accepted final revision.

## Next Slice

Implement the first blocked contribution layer: exact full-shipment progress and current shipment contribution candidates. Start by confirming canonical runtime fields from decompiled game code, then extend transparent state, typed contracts, candidate generation, output recording, and binder eligibility without guessing item completion or reward deltas.

