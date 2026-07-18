# Controller Audit

## Current Decision

Updated on 2026-07-18. Previously accepted coverage remains tested; the new pet-care slice is static-only pending runtime validation.

- Exact ingested `state_hash` remains mandatory; there is no latest-snapshot fallback.
- Direction metadata remains sourced from the live adapter/evaluator output.
- Candidate identity, score, reward, rank, action fields, timing fields, and arrays are preserved.
- Provenance conflicts and duplicate provenance names fail closed.
- Seven directions are directly bindable: `earn_money`, `raise_friendships`, `complete_master_angler`, `complete_full_shipment`, `obtain_skull_key`, `raise_skill_levels`, and `earn_pet_love`. Skill-source additions and the pet-care chain retain explicit runtime-pending status where applicable.
- Full-shipment binding has a direction-specific typed evidence gate. Generic profitable shipping remains valid for `earn_money`, but cannot advance `complete_full_shipment` without exact contribution evidence.
- Skull Key binding requires the exact `mining.obtain_skull_key` envelope, ordinary-mine family, target depth 120, mandatory reward-chest interaction, `player.has_skull_key=true` postcondition, mining-perfect executor profile, and executable current-floor boundary. Missing, duplicate, or conflicting contract parameters fail closed.
- Pet love now reads exact pet GUID/runtime/location/daily grant/friendship/times-pet/gift-trigger state plus assigned pet-bowl/watering-can state. `petLoveMessage` and `MarniePetAdoption` are both projected and recorded. Petting uses native `Pet.checkAction`; bowl filling uses the native `WateringCan` lifecycle. Immediate petting and delayed `Pet.dayUpdate` bowl settlement are distinct, and global-RNG gift selection is observed only after execution.
- The remaining five directions are explicit planned gaps. CC/Joja route commitment remains unresolved.

## Verification

- Full Core tests: 957 passed.
- Backend tests: 49 passed.
- E-drive isolated native shipping smoke: immediate postcondition passed; pending receipt written; overnight stage intentionally skipped for this run.
- Existing prior runtime evidence covers delayed day-end `basicShipped` settlement.
- E-drive isolated Skull Key loop passed with three verified primitives: move to reward chest, native two-stage claim with observed `false -> true`, and native mine exit. All after snapshots were fresh and all state hashes changed.
- No user save or user game process was used.
- The isolated game process and ports 8765/8767 were closed after the run.

## Next Slice

When testing is permitted, validate the pet interaction and pet-bowl paths in isolation before counting them as runtime-complete. The next implementation gap should share museum transparency between `complete_museum_collection` and `obtain_rusty_key` rather than creating separate donation models.
