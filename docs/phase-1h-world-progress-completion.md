# Phase 1H World Progress Completion

Scope: close the world progress gaps for read-only transparent state. This slice verifies and exposes `world_progress.perfection` and `world_progress.golden_walnuts`, and re-checks `quests.completed_quests`.

## Implemented Fields

| Section | Field | Status | Member path |
| --- | --- | --- | --- |
| `quests` | `completed_quests` | unavailable | no verified global completed quest ID collection found |
| `world_progress` | `perfection.percent_complete` | available when world ready | `StardewValley.Utility.percentGameComplete()` |
| `world_progress` | `perfection.percent_floor` | available when world ready | `Math.Floor(Utility.percentGameComplete() * 100f)` mirrors Qi tracker display |
| `world_progress` | `perfection.perfection_waivers` | available when world ready | `Game1.netWorldState.Value.PerfectionWaivers` |
| `world_progress` | `perfection.effective_percent_with_waivers` | available when world ready | `Utility.percentGameComplete() + PerfectionWaivers * 0.01f` |
| `world_progress` | `perfection.is_complete_with_waivers` | available when world ready | `effective_percent_with_waivers >= 1f` |
| `world_progress` | `golden_walnuts.current` | available when world ready | `Game1.netWorldState.Value.GoldenWalnuts` |
| `world_progress` | `golden_walnuts.found` | available when world ready | `Game1.netWorldState.Value.GoldenWalnutsFound` |
| `world_progress` | `golden_walnuts.found_capped_for_perfection` | available when world ready | `Math.Min(GoldenWalnutsFound, 130)` |
| `world_progress` | `golden_walnuts.perfection_target` | available when world ready | constant `130` from perfection/Qi tracker code |
| `world_progress` | `golden_walnuts.qi_room_actual_found` | available when world ready | `Math.Max(0, GoldenWalnutsFound - 1)` |
| `world_progress` | `golden_walnuts.qi_room_unlock_target` | available when world ready | constant `100` from Qi walnut room door code |
| `world_progress` | `golden_walnuts.qi_room_unlocked` | available when world ready | `qi_room_actual_found >= 100` |

## Decompiled Evidence

| Field/event | Decompiled path | Line/search pattern | Member path or formula | Result |
| --- | --- | --- | --- | --- |
| stored walnut balance | `StardewValley/StardewValley.Network/NetWorldState.cs` | lines 101, 389-397, 575, 632 | `goldenWalnuts`, `GoldenWalnuts`, `AddField(goldenWalnuts, "goldenWalnuts")`, special currency registration | verified read |
| stored walnuts found | `StardewValley/StardewValley.Network/NetWorldState.cs` | lines 106, 401-409, 576 | `goldenWalnutsFound`, `GoldenWalnutsFound`, `AddField(goldenWalnutsFound, "goldenWalnutsFound")` | verified read |
| stored perfection waivers | `StardewValley/StardewValley.Network/NetWorldState.cs` | lines 120, 451-459, 580 | `perfectionWaivers`, `PerfectionWaivers`, `AddField(perfectionWaivers, "perfectionWaivers")` | verified read |
| save persistence for fields | `StardewValley/StardewValley/SaveGame.cs` | lines 418-431, 1196-1206 | save/load maps `GoldenWalnuts`, `GoldenWalnutsFound`, `PerfectionWaivers` | verified stored world state |
| perfection score | `StardewValley/StardewValley/Utility.cs` | lines 3131-3163, `percentGameComplete` | returns 0..1 and uses `Math.Min(Game1.netWorldState.Value.GoldenWalnutsFound, 130)` for walnut component | verified read/computation |
| perfection with waivers | `StardewValley/StardewValley/Game1.cs` | line 8638 | `Utility.percentGameComplete() + PerfectionWaivers * 0.01f >= 1f` | verified completion threshold |
| Qi tracker display | `StardewValley/StardewValley/GameLocation.cs` | lines 8308-8333, `ShowQiCat` | reads `PerfectionWaivers`, floors `Utility.percentGameComplete() * 100f`, displays `Math.Min(GoldenWalnutsFound, 130) + "/130"` | verified display-aligned fields |
| Qi walnut room unlock | `StardewValley/StardewValley.Locations/IslandWest.cs` | lines 385-388, `IsQiWalnutRoomDoorUnlocked` | `actualFoundWalnutsCount = Math.Max(0, GoldenWalnutsFound - 1); return actualFoundWalnutsCount >= 100` | verified read/computation |
| active quest storage only | `StardewValley/StardewValley/Farmer.cs` | line 199 | `public readonly NetObjectList<Quest> questLog` | not a completed quest history |
| quest completion behavior | `StardewValley/StardewValley.Quests/Quest.cs` | lines 580-637 | sets `completed.Value`, increments `Game1.stats.QuestsCompleted` for some quest types, may remove quest from `questLog`, adds dialogue event `questComplete_{id}` | no verified completed quest ID collection |
| quest log cleanup | `StardewValley/StardewValley.Menus/QuestLog.cs` | lines 230-238 | removes destroyed quests and shows only non-hidden current quest entries | reinforces no durable completed quest list |

## Unavailable Items

`quests.completed_quests` remains unavailable.

Reason: the verified public paths show `Farmer.questLog` as current quest storage. Completion may leave a completed quest in the current log until reward/cleanup, or remove it immediately when no reward remains. `Game1.stats.QuestsCompleted` is a count, and generated mail/dialogue flags are not a reliable completed quest ID set. No verified global completed quest ID collection was found in the decompiled paths above.

## Read-Only Audit Allowlist

Allowed source files for this slice:
- `src/StardewAI.TransparentBridge/Adapters/ProgressReadAdapter.cs`
- `src/StardewAI.Contracts/State/ProgressState.cs`
- `tests/StardewAI.Core.Tests/ProgressContractsTests.cs`
- `docs/phase-1h-world-progress-completion.md`

Allowed member paths:
- `StardewValley.Utility.percentGameComplete()`
- `Game1.netWorldState.Value.PerfectionWaivers`
- `Game1.netWorldState.Value.GoldenWalnuts`
- `Game1.netWorldState.Value.GoldenWalnutsFound`

Allowed event subscriptions: none.

Forbidden actions: game state writes, save writes, quest mutation, mail mutation, walnut mutation, waiver purchases, input simulation, or derived completed quest ID inference from mail/dialogue flags.

Live SMAPI validation status: `not_executed`.
