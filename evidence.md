# Evidence

## Local Decompile Evidence

- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Tools\FishingRod.cs:1645-1715` shows `beginUsing` starts a normal timing cast, sets `isTimingCast=true`, `UsingTool=true`, `canMove=false`, `canReleaseTool=false`, and resets `castingPower=0`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Tools\FishingRod.cs:1948-1999` computes bobber landing distance from `castingPower * (getAddedDistance + 4)` for horizontal casts and `castingPower * (getAddedDistance + 3)` for vertical casts, so compiled bobber tile and released power must agree.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Tools\FishingRod.cs:2024-2044` increments `castingPower` during `isTimingCast`, reverses at `0`/`1`, and calls `startCasting()` only after use-tool input is released.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Menus\BobberBar.cs:417-428` recomputes `bobberInBar` and reads `Game1.oldMouseState.LeftButton` every update for bar control.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Menus\BobberBar.cs:523-589` increments catch progress while the fish is in the bar and decrements it while out of the bar.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Menus\BobberBar.cs:594-608` sets `handledFishResult=true` at terminal failure (`distanceFromCatching<=0`) or terminal success (`distanceFromCatching>=1`).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Menus\BobberBar.cs:341-368` only calls `pullFishFromWater(...)` during fade-out when terminal progress is above `0.9`; otherwise it calls `doneFishing(...)`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Tools\FishingRod.cs:480-491` shows a native hook consumes the nibble by calling `location.getFish(...)` inside `DoFunction` while `isNibbling` is true.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Tools\FishingRod.cs:640-654` sends junk/special catches to `pullFishFromWater(...)` but normal fish to delayed minigame startup, so one action must never observe both terminal paths.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Tools\FishingRod.cs:1137-1154` shows `pullFishFromWater(...)` fires an event, and `FishingRod.cs:1874-1881` polls that event later in `tickUpdate`, creating a repeated-hook race unless the harness latches before invoking `DoFunction`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Tools\FishingRod.cs:2304-2333` completes the fish-hold path through `doneHoldingFish(...)` and then `doneFishing(...)`, which is the cleanup boundary the executor must wait for.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\InputState.cs:85-91` and `Game1.cs:517` show the current mouse state is supplied through mutable `Game1.input`; this permits a bounded harness-only native-equivalent hold/release without OS input or direct rod state mutation.

## Repository Evidence

- `src\StardewAI.Core\OptionRegistry\FishingEventCandidateBuilder.cs` now prefers reachable maximum-power casts, emits `target_casting_power` and `max_cast_requested`, and preserves exact stand/bobber parameters.
- `src\StardewAI.Core\Execution\ActionQueueCompiler.cs` rejects forged target-power or max-cast metadata that no longer matches the compiled stand/bobber geometry.
- `tools\StardewAI.RuntimeTestHarness\ModEntry.cs` now holds native use-tool input through `ControlledInputState`, releases only at target power, drives `BobberBar` each relevant update through the controlled input wrapper plus `Game1.oldMouseState`, latches one native hook per nibble before `DoFunction`, and requires `handledFishResult` success for normal fish.
- `tools\StardewAI.LiveTrainingLoop\Program.cs` now supports `--required-verified-actions`; blocked/unverified attempts are progress diagnostics and do not append executor-calibration rows.

## Validation Evidence

- Recovery-chain reconciliation passed Core `422/422` and Backend `49/49` tests on 2026-07-14.
- The runtime harness builds without warnings or errors; this slice did not launch the game.
- High-level `recovery.stabilize_day` now expands every potentially available candidate kind through `DailyPlanCompiler`; cross-map return-home remains fail-closed.

## Executor Capability Reconciliation

- `farm.process_machines`, `economy.buy_supplies`, `exploration.visit_location`, and `executor.interact` no longer carry stale executor-disabled state.
- Runtime-supported atomic IDs `executor.sleep`, `executor.pickup_debris`, `executor.collect_machine_output`, and `executor.load_machine_input` are declared executor-enabled.
- `economy.sell_items`, social, quest, and mining remain disabled and are covered by negative capability-matrix tests.

## Recovery High-Level Chain

- Safe menu close, bounded refresh, already-home late-night sleep, and already-home post-2400 sleep now compile to existing atomic executors.
- Outside-home recovery retains `recovery_cross_map_home_route_unverified`; no arbitrary route or teleport was enabled.
- `current_location.home_context.sleep_executor_enabled=true` now matches the runtime-verified terminal sleep macro, while standalone prompt confirmation remains disabled.
