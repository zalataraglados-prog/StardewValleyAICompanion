# Phase 1E Quest/Mail/Progress Read Slice

Scope: read-only transparent collection for quests, mail flags, special orders, community center / Joja, museum, and collection progress. This slice adds adapter code only; it is intentionally not registered in `ModEntry`.

## Implemented Fields

| Section | Field | Status | Member path |
| --- | --- | --- | --- |
| `quests` | `active_quests` | available when world ready | `Game1.player.questLog` and `Quest.*` public net fields/properties |
| `quests` | `completed_quests` | unavailable | no verified global completed quest collection found |
| `quests` | `mail_received` | available when world ready | `Game1.player.mailReceived` |
| `quests` | `mail_for_tomorrow` | available when world ready | `Game1.player.mailForTomorrow` |
| `quests` | `mailbox` | available when world ready | `Game1.player.mailbox` |
| `quests` | `special_orders` | available when world ready | `Game1.player.team.specialOrders` and `SpecialOrder.*` |
| `quests` | `completed_special_orders` | available when world ready | `Game1.player.team.completedSpecialOrders` |
| `quests` | `accepted_special_order_types` | available when world ready | `Game1.player.team.acceptedSpecialOrderTypes` |
| `world_progress` | `community_center` | available when world ready | `Game1.netWorldState.Value.Bundles`, `.BundleRewards`, `Game1.MasterPlayer.mailReceived` cc flags |
| `world_progress` | `joja_membership` | available when world ready | `Game1.MasterPlayer.mailReceived.Contains("JojaMember")` |
| `world_progress` | `museum` | available when `ArchaeologyHouse` is a `LibraryMuseum` | `LibraryMuseum.museumPieces` |
| `world_progress` | `shipping_collection` | available when world ready | `Game1.MasterPlayer.basicShipped` |
| `world_progress` | `fish_collection` | available when world ready | `Game1.MasterPlayer.fishCaught` |
| `world_progress` | `artifact_collection` | available when world ready | `Game1.MasterPlayer.archaeologyFound` |
| `world_progress` | `mineral_collection` | available when world ready | `Game1.MasterPlayer.mineralsFound` |
| `world_progress` | `cooking_recipes` | available when world ready | `Game1.MasterPlayer.cookingRecipes` |
| `world_progress` | `crafting_recipes` | available when world ready | `Game1.MasterPlayer.craftingRecipes` |
| `world_progress` | `achievements` | available when world ready | `Game1.MasterPlayer.achievements` |
| `world_progress` | `perfection` | unavailable | not verified in this slice |
| `world_progress` | `golden_walnuts` | unavailable | not verified in this slice |

## Decompiled Evidence

| Field/event | Decompiled path | Line/search pattern | Member path | Source kind | Runtime condition |
| --- | --- | --- | --- | --- | --- |
| active quest list | `StardewValley/StardewValley/Farmer.cs` | line 199, `public readonly NetObjectList<Quest> questLog` | `Game1.player.questLog` | public game object | `Context.IsWorldReady && Game1.player != null` |
| quest id/title/description/objective/state | `StardewValley/StardewValley.Quests/Quest.cs` | lines 54-90, 120-240, 251-262 | `Quest.id.Value`, `questTitle`, `questDescription`, `currentObjective`, `questType.Value`, `accepted.Value`, `completed.Value`, `dailyQuest.Value`, `daysLeft.Value`, `moneyReward.Value` | public game object | quest instance from `questLog` |
| mail received / tomorrow | `StardewValley/StardewValley/Farmer.cs` | lines 253-258, 2125-2126 | `Game1.player.mailReceived`, `Game1.player.mailForTomorrow` | public game object | `Context.IsWorldReady && Game1.player != null` |
| mailbox | `StardewValley/StardewValley/Farmer.cs` | line 258 remarks and `Game1.cs` line 1611 `mailbox => player.mailbox` | `Game1.player.mailbox` | public game object | `Context.IsWorldReady && Game1.player != null` |
| team special order sets | `StardewValley/StardewValley/FarmerTeam.cs` | lines 90-99, 296-308 | `Game1.player.team.specialOrders`, `.completedSpecialOrders`, `.acceptedSpecialOrderTypes` | public game object | `Context.IsWorldReady && Game1.player?.team != null` |
| special order fields | `StardewValley/StardewValley.SpecialOrders/SpecialOrder.cs` | lines 57-139, 776-787 | `SpecialOrder.questKey`, `questName`, `questDescription`, `requester`, `orderType`, `dueDate`, `questDuration`, `questState`, `objectives` | public game object | order instance from `team.specialOrders` |
| special order objective fields | `StardewValley/StardewValley.SpecialOrders.Objectives/OrderObjective.cs` | lines 21-28, 53-55 | `OrderObjective.currentCount`, `maxCount`, `description` | public game object | objective instance from `SpecialOrder.objectives` |
| community center bundles | `StardewValley/StardewValley.Network/NetWorldState.cs`; `StardewValley/StardewValley.Locations/CommunityCenter.cs` | `NetWorldState` lines 69, 289, 291; `CommunityCenter` lines 103-107, 338-340 | `Game1.netWorldState.Value.Bundles`, `.BundleRewards` | public game object | `Context.IsWorldReady && Game1.netWorldState?.Value != null` |
| community center area flags | `StardewValley/StardewValley/Farmer.cs`; `StardewValley/StardewValley.Locations/CommunityCenter.cs` | `Farmer.hasCompletedCommunityCenter` lines 7249-7253; `CommunityCenter` lines 732-748 | `Game1.MasterPlayer.mailReceived.Contains("cc*")` | public game object | `Context.IsWorldReady && Game1.MasterPlayer != null` |
| Joja membership | `StardewValley/StardewValley.Locations/CommunityCenter.cs`; `StardewValley/StardewValley.Locations/JojaMart.cs` | `CommunityCenter` lines 513-540; `JojaMart` lines 47, 66-71 | `Game1.MasterPlayer.mailReceived.Contains("JojaMember")` | public game object | `Context.IsWorldReady && Game1.MasterPlayer != null` |
| museum pieces | `StardewValley/StardewValley.Locations/LibraryMuseum.cs`; `StardewValley/StardewValley.Network/NetWorldState.cs` | `LibraryMuseum` lines 49-50, 631; `NetWorldState` line 375 | `Game1.getLocationFromName("ArchaeologyHouse") as LibraryMuseum`, `.museumPieces.Pairs` | public game object | world ready and location cast succeeds |
| collection dictionaries | `StardewValley/StardewValley/Farmer.cs` | lines 220-224, 243, 748-761, 2154-2191 | `Game1.MasterPlayer.basicShipped`, `fishCaught`, `archaeologyFound`, `mineralsFound`, `cookingRecipes`, `craftingRecipes`, `achievements` | public game object | `Context.IsWorldReady && Game1.MasterPlayer != null` |

## Read-Only Audit Allowlist

Allowed source files:
- `src/StardewAI.Contracts/State/ProgressState.cs`
- `src/StardewAI.TransparentBridge/Adapters/ProgressReadAdapter.cs`
- `schemas/json/snapshot.schema.json`
- `docs/phase-1e-progress-read-slice.md`
- `tests/StardewAI.Core.Tests/ProgressContractsTests.cs`

Allowed member paths:
- `Game1.player.questLog`, `Quest` public net fields/properties listed above.
- `Game1.player.mailReceived`, `mailForTomorrow`, `mailbox`.
- `Game1.player.team.specialOrders`, `completedSpecialOrders`, `acceptedSpecialOrderTypes`.
- `SpecialOrder` and `OrderObjective` public net fields listed above.
- `Game1.netWorldState.Value.Bundles`, `BundleRewards`.
- `Game1.MasterPlayer.mailReceived`, collection dictionaries, and `achievements`.
- `Game1.getLocationFromName("ArchaeologyHouse") as LibraryMuseum`, `LibraryMuseum.museumPieces`.

Allowed event subscriptions: none.

Forbidden domains for this slice: game state writes, save writes, input simulation, pathing, buying, selling, quest completion, mail mutation, bundle donation, museum donation, collection mutation, full runtime validation.

Live SMAPI validation status: `not_executed`.

鐪熷疄娓告垙杩愯鏃堕獙鏀跺皻鏈墽琛宍
