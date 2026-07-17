# Controller Audit

## Current Decision

Accepted and tested on 2026-07-17.

- Exact ingested `state_hash` remains mandatory; there is no latest-snapshot fallback.
- Direction metadata remains sourced from the live adapter/evaluator output.
- Candidate identity, score, reward, rank, action fields, timing fields, and arrays are preserved.
- Provenance conflicts and duplicate provenance names fail closed.
- Four directions are directly bindable: `earn_money`, `raise_friendships`, `complete_master_angler`, and `complete_full_shipment`.
- Full-shipment binding has a direction-specific typed evidence gate. Generic profitable shipping remains valid for `earn_money`, but cannot advance `complete_full_shipment` without exact contribution evidence.
- The remaining eight directions are explicit planned gaps. CC/Joja route commitment remains unresolved.

## Verification

- Focused Core tests: 103 passed.
- Full Core tests: 946 passed.
- Backend tests: 49 passed.
- E-drive isolated native shipping smoke: immediate postcondition passed; pending receipt written; overnight stage intentionally skipped for this run.
- Existing prior runtime evidence covers delayed day-end `basicShipped` settlement.
- No user save or user game process was used.
- The isolated game process and ports 8765/8767 were closed after the run.

## Next Slice

Bind `obtain_skull_key` to an exact ordinary-mine reach-depth-120 candidate, then prove native floor-120 key acquisition and the transparent `has_skull_key` postcondition. Do not confuse ordinary mines, Skull Cavern, Quarry Mine 77377, or Volcano Dungeon.
