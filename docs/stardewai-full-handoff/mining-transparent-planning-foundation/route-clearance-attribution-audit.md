# Route-Clearance Attribution Audit

## Invariant

An action may claim `transit_route_clearance` only when a fresh transparent
snapshot proves that the exact target occupies a blocked cell on the selected
objective route. Distance or general reachability is not sufficient.

Every attributed dungeon action carries:

- `route_objective_id`
- `route_target_tile_x/y`
- `route_target_stand_tile_x/y`
- `blocked_route_cell_x/y`
- exact target identity or exact object/clump origin
- `blocker_attribution_status`
- `expected_connectivity_gain`

Zero or multiple matches fail closed. A fresh replan must show route progress;
target death alone is not treated as objective completion.
Volcano first checks whether any current dynamic path reaches the objective
stand. An alternate route therefore wins over unnecessary clearance before
the selected static path is attributed.

## Audited Families

| Family | Previous risk | Current rule |
|---|---|---|
| Quarry Golden Scythe altar/exit | Nearest reachable monster, stone, or clump could be mislabeled as route work | Compare full static objective path with dynamic collision; select only the unique entity occupying the first blocked route cell |
| Volcano forward connector/switch | Dynamic fallback could choose the globally nearest monster | Weighted-route blockers remain exact; dynamic fallback filters by the unique runtime identity occupying the blocked route cell |
| Generic `move_to_tile` repair | Multi-clear greedy selection could include a closer side obstacle | Each selected obstacle must strictly reduce the 0-1 BFS minimum number of clearable obstacles remaining to the target |
| Route-connector candidate repair | Candidate enumeration could expose unrelated clear work | Existing implementation already simulates removal and requires the target route to become reachable |
| Machine/storage/social cross-map routing | A blocked route could trigger unrelated local work | These families fail closed or retry; they do not synthesize nearest-target clearance |
| Ordinary mine progression | Nearest combat may occur when no stone or ladder is reachable | This is not labeled route clearance; it remains explicit floor-progression/ladder-generation work |

## Combat Controller Audit

MineShaft explicit combat and reactive parent-action self-defense now construct
the same `ActiveCombatMonster` state and execute the same movement, obstacle
clearance, attack input, emergency-food, feedback, and disengagement code.
The removed manual path had its own target, input, clearance, and tool-restore
state. No second melee attack loop remains.

Movement input is released before melee input. While the native swing or
recovery animation owns the farmer, the controller intentionally does not
move; this is normal attack lock, not navigation failure.

## Regression Coverage

- closer side monster versus farther exact route blocker;
- gate/other-player-style unattributed dynamic block fails closed;
- unrelated Quarry stone/clump is not cleared;
- route metadata reaches compiler parameters;
- fresh post-clear snapshot exposes the native exit;
- generic multi-clear routing skips a closer side obstacle;
- source guards reject restoration of the removed manual combat loop.
- hidden Quarry run completes claim and exit while safely replanning moved
  route targets.
