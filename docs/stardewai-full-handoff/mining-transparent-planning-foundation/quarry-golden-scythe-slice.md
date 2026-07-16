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
4. If the altar is not yet reachable, continue the existing bomb/container/stone/combat clearance loop. Do not descend.
5. If the altar stand tile is reachable but not adjacent, compile `executor.move_to_tile`.
6. When adjacent and unclaimed, compile `executor.interact` with `interaction_kind=map_action` and `expected_action_type=GoldenScythe`.
7. Verify native claim through both `gotGoldenScythe` and increased `(W)53` inventory count.
8. On the next fresh snapshot, compile the already validated `executor.exit_mine` path and verify the Quarry Mine destination `Mine(67,10)`.

## Decompiled Rules

- `GameLocation.performAction`, branch `GoldenScythe`: when unclaimed and inventory is not full, add `gotGoldenScythe` and grant `ItemRegistry.Create("(W)53")`; when already claimed, the altar itself can perform `MagicWarp Mine 67 10`. The current production compiler uses the separately verified native mine exit after claiming so asynchronous warp handling is not falsely judged by the synchronous interaction primitive.
- `MineShaft.shouldCreateLadderOnThisLevel`: false at level `77377`.
- `MineShaft.isSideBranch`: true only at level `77377`.
- `MeleeWeapon.goldenScytheId`: `53`.

## Validation State

`debug.setup_quarry_mine` and `Invoke-RuntimeQuarryGoldenScytheLoop.ps1` now define the repeatable isolated boundary without running it. The setup enters generated mine sentinel `77377`, optionally resets only the isolated fixture's `gotGoldenScythe` mail and `(W)53` inventory items, then verifies the side branch, altar action, unclaimed state, and free inventory slot.

The loop reads only `profile=mining`, compiles one high-level `mining.acquire_golden_scythe` action per fresh snapshot, and records one verified executor row per step without fitting during collection. Before the reward is claimed, an exit is a terminal failure. After the native altar interaction is verified through both mail and inventory increase, any further clearance action is a terminal failure; only `executor.exit_mine` reaching `Mine(67,10)` succeeds. Ladder, shaft, Volcano, stale snapshot, blocked primitive, and step-limit paths fail closed.

RuntimeTestHarness and LiveTrainingLoop Release builds pass with `EnableModDeploy=false`, and the loop passes PowerShell parsing. Unit and runtime tests were intentionally not executed. No runtime-complete claim is made until an isolated save verifies approach, native claim, fresh after-state, native return, training-row contents, and duration.
