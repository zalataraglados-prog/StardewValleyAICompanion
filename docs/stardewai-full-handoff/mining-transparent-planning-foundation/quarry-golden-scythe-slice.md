# Quarry Mine Golden Scythe Slice

## Family Boundary

The Quarry Mine side branch is a generated `MineShaft` with sentinel level and area `77377`. It is not an ordinary mine depth, not Skull Cavern area `121`, and not a `VolcanoDungeon`. `MineShaft.shouldCreateLadderOnThisLevel()` returns false for `77377`, so the objective must not wait for or manufacture a ladder.

The model-facing command is `mining.acquire_golden_scythe`. `mining.reach_depth` rejects `quarry_mine` with `quarry_mine_uses_acquire_golden_scythe_objective`.

## Transparent Inputs

- `mining.current_mine.mine_kind=quarry_mine`
- `mining.current_mine.mine_level=77377`
- `mining.tiles.golden_scythe_altars[]` from exact loaded-map `Action=GoldenScythe`
- `mining.floor_objectives.golden_scythe_applicable`
- `mining.floor_objectives.golden_scythe_claimed` from `mailReceived["gotGoldenScythe"]`
- `mining.player_resources.inventory_capacity.empty_slots`
- `mining.player_resources.golden_scythe_in_inventory`
- `mining.player_resources.golden_scythe_inventory_count`

## Rolling Compiler

1. Apply deadline, health, energy, and immediate-threat gates.
2. If the reward is unclaimed and inventory has no empty slot, block upstream.
3. If a monster makes the route unsafe, compile the existing perfect combat primitive.
4. If the altar is not yet reachable, compare the complete static altar route with current dynamic collision. Clear only the uniquely attributable monster, container, stone, or supported resource clump occupying the first blocked route cell. Ambiguous and unattributed blocks fail closed. Do not descend.
5. If the altar stand tile is reachable but not adjacent, compile `executor.move_to_tile`.
6. When adjacent and unclaimed, compile `executor.interact` with `interaction_kind=map_action` and `expected_action_type=GoldenScythe`.
7. Verify native claim through both `gotGoldenScythe` and increased `(W)53` inventory count.
8. On the next fresh snapshot, compile route-enabling combat/container/stone/resource-clump work only when its exact identity is bound to the static native-exit route. Side-route targets are forbidden.
9. As soon as the exit is reachable, compile the already validated `executor.exit_mine` path and verify the Quarry Mine destination `Mine(67,10)`.

## Decompiled Rules

- `GameLocation.performAction`, branch `GoldenScythe`: when unclaimed and inventory is not full, add `gotGoldenScythe` and grant `ItemRegistry.Create("(W)53")`; when already claimed, the altar itself can perform `MagicWarp Mine 67 10`. The current production compiler uses the separately verified native mine exit after claiming so asynchronous warp handling is not falsely judged by the synchronous interaction primitive.
- `MineShaft.shouldCreateLadderOnThisLevel`: false at level `77377`.
- `MineShaft.isSideBranch`: true only at level `77377`.
- `MeleeWeapon.goldenScytheId`: `53`.

## Validation State

`debug.setup_quarry_mine` and `Invoke-RuntimeQuarryGoldenScytheLoop.ps1` define the repeatable isolated boundary. The setup enters generated mine sentinel `77377`, resets only the isolated fixture's `gotGoldenScythe` mail and `(W)53` inventory items, then verifies the side branch, altar action, unclaimed state, and free inventory slot.

The loop reads only `profile=mining`, compiles one high-level `mining.acquire_golden_scythe` action per fresh snapshot, and records one verified executor row per applied step without fitting during collection. Before the reward is claimed, an exit is a terminal failure. After the native altar interaction is verified through both mail and inventory increase, only route-enabling combat or clearance may precede `executor.exit_mine`; descent and unrelated work remain forbidden. A route-clearance target that leaves its original blocking area may return `combat_disengaged_transit_target`; a target that disappears between snapshot and dispatch may return `combat_target_not_found_or_moved`. The loop accepts only those exact blocked results and only when the after-snapshot is fresh and its state hash changed, then replans without writing false kill success. All other blocked primitives, ladder, shaft, Volcano, stale snapshot, and step-limit paths fail closed. Executor HTTP timeout is independently configurable and set to 600 seconds for this loop because the runtime exit state machine has a larger bounded tick budget than the generic 180-second backend request timeout.

Supported MineShaft resource clumps now compile to `executor.break_resource_clump` with exact anchor, footprint, parent-sheet index, perimeter stand tile, hit tile, and Axe/Pickaxe slot. The executor uses native movement and tool input and verifies natural clump removal; unsupported indexes and insufficient tool upgrades fail closed.
Every route-clearance plan also carries the objective target, objective stand, blocked route cell, attribution status, and expected connectivity gain. Distant blockers retain the four-tile rolling approach horizon without losing this attribution.

Hidden/silent isolated run `runtime-quarry-golden-scythe-20260730-052042` passed the complete boundary. It verified 59 of 59 native actions: 49 moves, 8 melee combats, one `GoldenScythe` interaction, and one `ExitMine_Leave`. The claim changed both `gotGoldenScythe` and `(W)53` inventory count, the exit reached `Mine(67,10)`, all after-snapshots were fresh, all state hashes changed, and 59 training rows were written. The exit action consumed 3116 ticks, which confirms why its client timeout must exceed 180 seconds while remaining bounded by the executor state machine. Melee uses the shared native heavy-hitter input mapping and only attacks from collision-box contact or a cardinally adjacent tile; BFS and native tool clearance handle blocked approach rather than assuming weapon range can cross mine obstacles.

Attribution regression run `runtime-route-attribution-quarry-r3-20260730`
passed the same terminal boundary in 100 rolling steps. It verified 64 native
actions and performed 36 fresh `combat_disengaged_transit_target` replans
without recording false kills. Every after-snapshot was fresh with a changed
state hash; the run claimed the Golden Scythe and exited to `Mine(67,10)`.
This is the current runtime evidence for exact blocker attribution and the
shared MineShaft combat controller.
