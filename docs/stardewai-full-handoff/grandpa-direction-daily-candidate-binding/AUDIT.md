# Controller Audit

## Current Decision

Updated on 2026-07-18. Previously accepted coverage remains tested; pet care and museum donation remain runtime-pending until isolated game smokes are recorded.

- Exact ingested `state_hash` remains mandatory; there is no latest-snapshot fallback.
- Direction metadata remains sourced from the live adapter/evaluator output.
- Candidate identity, score, reward, rank, action fields, timing fields, and arrays are preserved.
- Provenance conflicts and duplicate provenance names fail closed.
- Nine directions are directly bindable: `earn_money`, `raise_friendships`, `complete_master_angler`, `complete_full_shipment`, `obtain_skull_key`, `raise_skill_levels`, `earn_pet_love`, `complete_museum_collection`, and `obtain_rusty_key`. Skill-source, pet-care, and museum additions retain explicit runtime-pending status where applicable.
- Full-shipment binding has a direction-specific typed evidence gate. Generic profitable shipping remains valid for `earn_money`, but cannot advance `complete_full_shipment` without exact contribution evidence.
- Skull Key binding requires the exact `mining.obtain_skull_key` envelope, ordinary-mine family, target depth 120, mandatory reward-chest interaction, `player.has_skull_key=true` postcondition, mining-perfect executor profile, and executable current-floor boundary. Missing, duplicate, or conflicting contract parameters fail closed.
- Pet love now reads exact pet GUID/runtime/location/daily grant/friendship/times-pet/gift-trigger state plus assigned pet-bowl/watering-can state. `petLoveMessage` and `MarniePetAdoption` are both projected and recorded. Petting uses native `Pet.checkAction`; bowl filling uses the native `WateringCan` lifecycle. Immediate petting and delayed `Pet.dayUpdate` bowl settlement are distinct, and global-RNG gift selection is observed only after execution.
- Museum progress now shares one transparent candidate/compiler/executor chain. Total collection size is read from dynamic `LibraryMuseum.totalArtifacts`; the Rusty Key threshold and reward action are loaded from `Data/MuseumRewards[museum60]`. Execution uses `LibraryMuseum.OpenDonationMenu`, native `MuseumMenu.receiveLeftClick`, and native close settlement, with no direct museum, inventory, achievement, mail, event, or key mutation.
- The remaining three directions are explicit planned gaps. CC/Joja route commitment remains unresolved.

## Verification

- Full Core tests: 989 passed.
- Backend tests: 60 passed.
- E-drive isolated native shipping smoke: immediate postcondition passed; pending receipt written; overnight stage intentionally skipped for this run.
- Existing prior runtime evidence covers delayed day-end `basicShipped` settlement.
- E-drive isolated Skull Key loop passed with three verified primitives: move to reward chest, native two-stage claim with observed `false -> true`, and native mine exit. All after snapshots were fresh and all state hashes changed.
- No user save or user game process was used.
- The isolated game process and ports 8765/8767 were closed after the run.

## Next Slice

Run isolated native smokes for pet interaction, pet-bowl settlement, and museum donation before counting those paths as runtime-complete. The next blocked Grandpa slice is the shared CC/Joja route and action chain; marriage/house upgrade remains a separate gap.
