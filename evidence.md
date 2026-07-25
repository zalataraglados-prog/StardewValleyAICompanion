# Evidence

## Local Decompile Evidence

- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Menus\DialogueBox.cs` shows:
  - `receiveLeftClick()`: checks `transitioning` first (early return if true). Then checks typing progress via `characterIndexInDialogue` and `getCurrentString()` comparison against the full dialogue text; if typing is ongoing, sets `showTyping = false` (completes the current page typing). Then checks `safetyTimer > 0` (returns early). It does **not** check `showTyping` as an initial gate. Reference: `DialogueBox.receiveLeftClick` method body, fields `transitioning`, `characterIndexInDialogue`, `getCurrentString()`, `safetyTimer`.
  - Public fields: `isQuestion` (bool), `responses` (Response[]), `transitioning` (bool), `transitioningBigger` (bool), `transitionInitialized` (bool), `safetyTimer` (int), `characterDialogue` (Dialogue), `characterIndexInDialogue` (int), `characterAdvanceTimer` (int), `questionFinishPauseTimer` (int), `showTyping` (bool), `dialogueFinished` (bool), `dialogueContinuedOnNextPage` (bool), `selectedResponse` (int), `heightForQuestions` (int), `dialogueIcon`, `aboveDialogueImage`, `friendshipJewel`, `newPortraitShakeTimer` (int).
  - `receiveLeftClick` checks `transitioning` first; if transitioning, it returns early. Then it completes typing via `characterIndexInDialogue`/`getCurrentString()` if the full dialogue text hasn't been fully displayed yet, setting `showTyping = false`. Then checks `safetyTimer > 0` for early return. Only after all three gates pass does it advance to the next dialogue page or call `closeDialogue()` to finish. Note: `showTyping` is NOT checked as a gate; instead, active typing progress is completed inline.
  - Questions (`isQuestion=true`, `responses.Length>0`) require explicit answer selection via `chooseResponse`; clicking outside the response area does nothing for questions.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` shows:
  - `Game1.eventUp` is a static boolean field that tracks whether an in-game event is currently active. This is checked as an additional runtime safety gate (project policy, not required by `DialogueBox.receiveLeftClick` itself). The native dialogue advance executor refuses to advance dialogue while `Game1.eventUp` is true to prevent interfering with event-driven dialogue sequences.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs:1532-1550` shows `Farmer.spouse` is the player's NPC spouse or roommate. Its getter returns `netSpouse.Value` when nonempty and otherwise returns `null`; its setter stores `null` as an empty `netSpouse` value. Therefore a null getter result while `Game1.player` is readable proves the known no-spouse state and is exported as an available canonical empty string, not as unavailable data.
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

- Storage acquisition validation passed Core `1191/1191` and Backend `82/82` on 2026-07-25.
- Hidden/silent isolated native Chest crafting passed at `artifacts/runtime-storage-crafting-smoke/runtime-storage-crafting-smoke-20260725-205501/summary.json`: inventory `(BC)130` `0 -> 1`, recipe count `14 -> 15`, exact native ingredient/output multiset verified, and state hash changed.
- Recovery-chain reconciliation passed Core `422/422` and Backend `49/49` tests on 2026-07-14.
- The runtime harness builds without warnings or errors; this slice did not launch the game.
- High-level `recovery.stabilize_day` now expands every potentially available candidate kind through `DailyPlanCompiler`; cross-map return-home remains fail-closed.

## Executor Capability Reconciliation

- `farm.process_machines`, `economy.buy_supplies`, `exploration.visit_location`, and `executor.interact` no longer carry stale executor-disabled state.
- Runtime-supported atomic IDs `executor.sleep`, `executor.pickup_debris`, `executor.collect_machine_output`, and `executor.load_machine_input` are declared executor-enabled.
- `economy.sell_items`, social, quest, and mining remain disabled and are covered by negative capability-matrix tests.
- `executor.close_menu` now supports safe ordinary non-event non-question `DialogueBox` via native SMAPI OverrideButton MouseLeft input state machine (press/release per-tick, waits through `transitioning`/`safetyTimer`, advances typing/pages, detects `dialogueFinished`; no `exitActiveMenu`/`closeDialogue`/coordinate teleport/answer selection). Timeout, changed menu, question emergence, and input failure all release input and block with precise reasons.

## Recovery High-Level Chain

- Safe menu close, bounded refresh, already-home late-night sleep, and already-home post-2400 sleep now compile to existing atomic executors.
- Outside-home recovery now compiles one transparently confirmed route connector at a time toward `home_location_id`, then requires a fresh snapshot before the next connector. The compiler rejects missing graph edges, unmatched current connectors, closed/unresolved gates, and unreachable current-map segments.
- Connector traversal no longer contains the old direct `Game1.currentLocation` / `player.Position` transition path. Standard and touch warps are entered through movement; action warps, locked doors, and building doors use the native `checkAction` lifecycle. Runtime verification is intentionally pending because this implementation slice was requested without game testing.
- `current_location.home_context.sleep_executor_enabled=true` now matches the runtime-verified terminal sleep macro, while standalone prompt confirmation remains disabled.
