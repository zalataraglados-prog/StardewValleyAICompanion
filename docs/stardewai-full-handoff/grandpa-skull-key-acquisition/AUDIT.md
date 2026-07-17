# Grandpa Skull Key Acquisition Audit

## Status

Accepted on 2026-07-17.

- Model-facing objective: `mining.obtain_skull_key`.
- Candidate kind: `mining_obtain_skull_key_plan_envelope`.
- Valid family: ordinary mines, levels 1 through 120 only.
- Mandatory terminal interaction: floor-120 overlay reward chest containing `SpecialItem(4)`.
- Completion postcondition: transparent `player.has_skull_key=true`.
- Skull Cavern, Quarry Mine `77377`, and Volcano Dungeon are rejected.

## Runtime Proof

Artifact: `artifacts/runtime-mining-reach-depth/runtime-skull-key-20260717-180537/summary.json`.

1. `executor.move_to_tile`: approached the transparent reward chest.
2. `executor.interact`: native open/claim sequence; observed `has_skull_key false -> true`.
3. `executor.exit_mine`: used the floor-120 two-tile exit stand and native ExitMine dialogue.

All three primitives were applied and verified. Every after snapshot was fresh, every state hash changed, and three executor-calibration rows were written. The isolated fixture reset is debug-only; the production executor never writes `hasSkullKey` directly.

## Remaining Independent Issue

A 119-to-120 attempt first selected a random breakable container and hit `break_container_swing_budget_exceeded`. This is tracked as RISK-009. It does not invalidate the dedicated floor-120 key chain, but the longer 119-to-120 regression should be rerun after container budget calibration.
