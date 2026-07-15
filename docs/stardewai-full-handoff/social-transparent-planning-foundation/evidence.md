# Evidence

Decompiled rule evidence was collected statically from `I:\StardewValleyAICompanion-decompile`. A later controller run added isolated E: runtime evidence; no claim below relies on documentation examples alone.

## Decompiled Social Rules

- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:735-745` exposes `IsInvisible` as a read of `isInvisible.Value`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:747-758` defines `CanSocialize` as `IsVillager && CanSocializePerData(Name, currentLocation)`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:872-875` shows base `canTalk()` returns true.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:1390-1398` defines `CanReceiveGifts()` as `CanSocialize && !SimpleNonVillagerNPC && Game1.NPCGiftTastes.ContainsKey(Name) && (GetData()?.CanReceiveGifts ?? true)`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:1403-1579` implements gift taste precedence.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:1582-1608` shows item ID and context-tag taste helpers.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:2357-2404` shows gift interaction gates for `not_giftable`, `canBeGivenAsGift`, dumped dialogue, green rain, weekly limits, divorced rejection, daily limit, `receiveGift`, item decrement, and animation.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:2406-2415` shows spouse jealousy uses RNG.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:2307` shows the roommate proposal context-tag branch before ordinary gifts.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:1904-2419` contains special item and NPC-specific gift/delivery branches.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:4811-4819` shows birthday is a pure season/day comparison.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:4836-4858` shows character master data lookup through `Game1.characterData`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:4868-4879` shows `CanSocializePerData` relies on `GameStateQuery.CheckConditions`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs:4898-4979` shows `receiveGift` mutates stats, sounds, gift counters, friendship, emotes, facing, and dialogue. It is NOT called by the executor.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs:5559-5602` shows deterministic `changeFriendship` modifiers.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Object.cs:3093-3104` shows object giftability.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs:771-772` stores NPC friendship data in `Farmer.friendshipData`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs:4146-4158` resets daily/weekly gift counters by `LastGiftDate`.

### Runtime Executor Anchors

The native executor `executor.social_interact` calls `Game1.currentLocation.checkAction()` as the only state-changing social path. Decompiled anchor:

- `GameLocation.cs:7639-7678` — `GameLocation.checkAction` dispatches to NPC bounding-box handling; the NPC branch at ~7673–7678 calls `NPC.checkAction` via the character's `checkAction` override.
- `NPC.cs:2464` — `NPC.checkAction` entry. The talk/gift dispatch path begins at ~2760+, routing through `CanSocialize`, `CanReceiveGifts`, daily/weekly gift limits, `receiveGift`, dialogue, and friendship mutation.
- `NPC.cs:1712` — `tryToReceiveActiveObject` (not called by executor; vanilla path only).
- `NPC.cs:2383-2404` — ordinary gift branch inside `checkAction` that the vanilla runtime follows after the executor calls `Game1.currentLocation.checkAction`.
- `NPC.cs:4898` — `receiveGift` (evidence only; not called directly by executor).

The executor does NOT call `NPC.checkAction`, `tryToReceiveActiveObject`, `receiveGift`, `changeFriendship`, `reduceActiveItemByOne`, or direct NPC position/tile mutation. It only calls `Game1.currentLocation.checkAction`, which internally dispatches to `NPC.checkAction` through the vanilla bounding-box dispatch.

## Global Location Enumeration Anchors

- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Utility.cs:348-391` — `ForEachLocation(Func<GameLocation,bool> action, bool includeInteriors = true, bool includeGenerated = false)` iterates `Game1.locations` (`IList<GameLocation>` backed by `_locations`), then calls `gameLocation.ForEachInstancedInterior(...)` for each when `includeInteriors` is requested, and additionally `MineShaft.activeMines` and `VolcanoDungeon.activeLevels` when `includeGenerated` is requested. Returning `false` stops iteration.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs:1326` — `Game1.locations` is exposed as `IList<GameLocation>` backed by `_locations`. Instanced interiors and active generated levels are additionally enumerated by `Utility.ForEachLocation` when requested via `includeInteriors` and `includeGenerated`.

`NpcReadAdapter.CollectAllLoadedNpcs` now uses `Utility.ForEachLocation(action, includeInteriors:true, includeGenerated:true)` to enumerate all loaded NPCs globally, preserving reference-identity deduplication.

## Implemented Sandbox Surfaces

- `tools/StardewAI.RuntimeTestHarness/ModEntry.cs:ExecuteSocialInteract` — native `executor.social_interact` that validates all preconditions (NPC identity/presence/location/tile/adjacency/action rectangle/visibility/sleeping/CanSocialize/CanReceiveGifts, menu-closed, active-object match for gift, exact slot/item/stack and gift limits; only Stardrop Tea bypasses the daily limit, while spouse, birthday, or Stardrop Tea can bypass the weekly limit), calls `Game1.currentLocation.checkAction`, and records before/after typed output including NPC/player state, friendship counters, dialogue state, menu state, timestamps/ticks, and verification results. Unknown/unresolved state is null/empty, not guessed.
- `tests/StardewAI.Core.Tests/SocialNativeSourceGuardTests.cs` — 27 source-guard tests verifying executor uses only `Game1.currentLocation.checkAction`, no direct mutation calls, records all required fields, handles Stardrop Tea exceptions, one-to-null stack consumption, serialization round-trip, null row semantics.
- `tests/StardewAI.Core.Tests/SocialTransparentPlanningTests.cs` — tests for candidate building, compilation, time budget, and daily-plan integration.
- `src/StardewAI.Core/OptionRegistry/SocialCandidateBuilder.cs` — builds current-state talk/gift candidates with complete legality checking.
- `src/StardewAI.Core/Execution/ActionQueueCompiler.cs:CompileSocialInteractStep` — compiles `executor.social_interact` plan step with validated parameters.
- `src/StardewAI.Core/Training/DailyPlanCompiler.cs` — compiles social candidates into `move_to_social_stand` + `social_interact` plan steps.
- `src/StardewAI.Contracts/Execution/ActionQueueContracts.cs:SocialPlanEnvelope` — typed training recording contract.
- `src/StardewAI.Core/Execution/ActionQueueCompiler.cs:CompileSocialPlan` — builds `SocialPlanEnvelope` with live legality evidence, time/route constraints, expected deterministic outcome, and training recording contract.
- `src/StardewAI.Core/OptionRegistry/OptionRegistry.cs` — registers `executor.social_interact` with required state fields.
- `src/StardewAI.Core/Execution/TimeBudgetValidator.cs` — handles `recovery.stabilize_day`, `social.talk_npc`, `social.gift_npc` with estimated-duration sentinel.

## Isolated Runtime Evidence (2026-07-15)

- PASS summary: `artifacts/runtime-native-social-smoke/runtime-native-social-smoke-20260715-183304/summary.json`.
- Talk selected Pierre in `SeedShop`, moved to `(3,17)` beside NPC `(4,17)`, faced right, opened native `DialogueBox`, and recorded `applied/verified` plus one training row.
- Safe ordinary dialogue advancement used native SMAPI MouseLeft input, closed `DialogueBox -> none`, recorded one press and typed dialogue before/after fields in both execution output and episode.
- Gift selected exact slot 10 `(O)388`, used `Game1.currentLocation.checkAction`, changed stack `4 -> 3`, gifts today/week `0 -> 1`, friendship `66 -> 46`, and recorded `applied/verified` plus one training row.
- Runtime also proved known no-spouse state is transparent (`value="", status="available"`) and that available current-location social candidates survive the bounded 64-row diagnostic cap.
