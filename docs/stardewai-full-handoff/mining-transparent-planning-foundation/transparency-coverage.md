# Transparency Coverage

| required input/output | snapshot/contract path | source/status | completeness gate | missing/runtime boundary |
|---|---|---|---|---|
| Mine kind, level, area, generated/loaded identity, ordinary/Skull/quarry/dangerous classification | `mining.current_mine` | live `MineShaft.mineLevel`, `getMineArea()`, `GetAdditionalDifficulty()`, area flags | required by `mining.reach_depth` | unavailable outside loaded `MineShaft` |
| Player tile, collision context, exit/entry/elevator/ladder/shaft tiles | `mining.tiles` | live player tile and already-loaded `GameLocation.map` field only | required, currently unavailable for planning | no `Map` getter, no `updateMap`; collision/passability and usability remain unavailable |
| Breakable stones, ore/resource nodes, containers, placed staircases, relevant objects | `mining.objects` | live `MineShaft.objects` rows with identity/category/fragility fields | required, currently unavailable for planning | complete ore/container classification and stone hit/health semantics unavailable |
| Monsters with type, tile, bounding box, health, damage, behavior facts | `mining.monsters` | live `Monster` rows; no AI/drop methods | required | ranged/special behavior unavailable without complete decompile-backed behavior table |
| Floor objectives/gates affecting descent | `mining.floor_objectives` | live mine flags, `mustKillAllMonstersToAdvance()`, enemy/stones/ladder-present facts | required, currently unavailable for planning | water/bridge/special floor constraints and ladder probability unavailable |
| Player mining resources | `mining.player_resources` | live player health/energy/levels/tools/weapons/bombs/stairs/food/time/buffs/deepest mine level | required | exit deadline is only a model/compiler constraint, not observed state |
| Group completeness and forbidden calls | `mining.completeness`, snapshot `unavailable_fields` | adapter metadata | required recursively | fails closed with nested unavailable/stale/error facts and with `not_loaded_mineshaft` |
| Model option surface | `mining.reach_depth` parameters | target depth/location family, latest exit time, reserve health/energy, resource policy | compiler validates | model does not emit low-level mining/combat actions |
| Candidate generation | availability `event_candidates[]` | `MiningReachDepthCandidateBuilder` | requires all mining groups recursively complete | missing nested groups, invalid target, missing elevator unlock facts, and unknown mining cost fail closed; negative ticks/energy are blocked unknown-cost sentinels, not estimates |
| Compiled queue envelope | `action_queue.items[].normalized_command.parameters` | `ActionQueueCompiler` | preserves current depth, read elevator start, target, supplied resource/time constraints, executor profile | queue item blocked with `mining_cost_estimate_unavailable` and `mining_perfect_executor_not_implemented`; timing/energy unknown |
| Runtime execution | none | not implemented in this slice | blocked | no fake low-level actions, calibration rows, or executor claim |

Validation status: tests were added but intentionally not run under the active user-play constraint.
