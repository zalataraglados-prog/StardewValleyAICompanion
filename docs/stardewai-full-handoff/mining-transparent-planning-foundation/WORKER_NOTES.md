# Worker Notes

## 2026-07-13 Mining Transparent Planning Foundation

- Branch: `worker/mining-transparent-planning-foundation`.
- Scope implemented in this sandbox only.
- No game launch, SMAPI launch, build, test, smoke script, deployment, RNG mutation, mine generation, ladder discovery call, branch switch, reset, clean, rebase, push, or credential access was performed.
- No fishing-owned files or runtime harness files were edited.

## Completed

- Reworked `MiningReadAdapter` to avoid the mutating `Map` getter, read only already-loaded live fields, and mark incomplete map/collision/object/floor groups unavailable instead of complete.
- Registered `mining.reach_depth` as a parameterized mechanical option with bounded model parameters.
- Added reach-depth candidate generation gated on recursive transparent mining completeness, known target-depth validity, read elevator unlock progress, and an explicit unknown-cost block.
- Updated queue compilation to preserve mining target/resource/retreat envelope fields, keep timing/energy unknown, avoid hard-coded reserve defaults, and block at `mining_cost_estimate_unavailable` plus `mining_perfect_executor_not_implemented`.
- Added focused unit tests for option registration, recursive completeness, incomplete collision/object groups, exact action parsing, elevator unlock/continuation boundaries, optional reserve constraints, compiler boundary, and impossible target rejection.
- Replaced prior-slice handoff files with this slice's evidence, coverage, risk, and pending validation status.

## Validation

- Not run by instruction and active user-play constraint.
- Pending command list is recorded in `test-results.txt`.

## 2026-07-15 Controller Closure

- The original worker state above is historical. Codex subsequently replaced the unavailable collision/object/floor placeholders with decompile-backed loaded-floor reads.
- Collision rows now cover map geometry, objects, characters, terrain features, resource clumps, large terrain features, furniture, animals, and other farmers while excluding the controlled farmer. The cache is purpose-limited and refreshes no later than 30 ticks.
- Breakable stone/container durability, best-pickaxe hit counts, stone ladder previews, treasure-room state, floor gates, and current monster facts are available on a loaded `MineShaft`.
- `profile=mining` limits snapshot work to baseline, route/current-location, and mining domains.
- Offline validation passed. The remaining gate is an isolated E: serialization/performance smoke followed by the dynamic perfect mining executor; no runtime capability is claimed yet.
