# Controller Audit

## Current Decision

Accepted and tested on 2026-07-17.

- Exact ingested `state_hash` remains mandatory; there is no latest-snapshot fallback.
- Direction metadata remains sourced from the live adapter/evaluator output.
- Candidate identity, score, reward, rank, action fields, timing fields, and arrays are preserved.
- Provenance conflicts and duplicate provenance names fail closed.
- Six directions are directly bindable: `earn_money`, `raise_friendships`, `complete_master_angler`, `complete_full_shipment`, `obtain_skull_key`, and `raise_skill_levels`. The last direction is statically complete but its newly added source-specific runtime paths remain pending validation.
- Full-shipment binding has a direction-specific typed evidence gate. Generic profitable shipping remains valid for `earn_money`, but cannot advance `complete_full_shipment` without exact contribution evidence.
- Skull Key binding requires the exact `mining.obtain_skull_key` envelope, ordinary-mine family, target depth 120, mandatory reward-chest interaction, `player.has_skull_key=true` postcondition, mining-perfect executor profile, and executable current-floor boundary. Missing, duplicate, or conflicting contract parameters fail closed.
- The remaining six directions are explicit planned gaps. CC/Joja route commitment remains unresolved.

## Verification

- Full Core tests: 957 passed.
- Backend tests: 49 passed.
- E-drive isolated native shipping smoke: immediate postcondition passed; pending receipt written; overnight stage intentionally skipped for this run.
- Existing prior runtime evidence covers delayed day-end `basicShipped` settlement.
- E-drive isolated Skull Key loop passed with three verified primitives: move to reward chest, native two-stage claim with observed `false -> true`, and native mine exit. All after snapshots were fresh and all state hashes changed.
- No user save or user game process was used.
- The isolated game process and ports 8765/8767 were closed after the run.

## Next Slice

Choose the next one of the six blocked Grandpa directions. `earn_pet_love` is the narrowest independent candidate: add exact pet identity/friendship/daily-interaction transparency, then bind a native pet interaction without conflating the pet-water bonus or Marnie mail threshold with the immediate petting result.
