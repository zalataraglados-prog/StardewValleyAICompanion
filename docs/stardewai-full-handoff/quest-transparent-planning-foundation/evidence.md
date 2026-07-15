# Evidence

## Local Decompile Evidence

- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:23` shows `Quest` is concrete with 11 XmlInclude subclasses. Fields: `id`, `questType`, `accepted`, `completed`, `dailyQuest`, `showNew`, `canBeCancelled`, `destroy`, `moneyReward`, `daysLeft`, `dayQuestAccepted`, `nextQuests`, `rewardDescription`, `modData`. Constants `type_basic=1` through `type_weeding=11`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:47-51` - `_currentObjective`, `_questDescription`, `_questTitle` are public backing fields.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:96` - `_loadedDescription` is private (not accessible externally). Line 98: `_loadedTitle` is protected (not accessible externally).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:120-198` - `questTitle` getter mutates `_loadedTitle`, calls `GetRawQuestFields`, builds title per quest type. Not observer-safe.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:201-222` - `questDescription` getter checks `_loadedDescription`, calls `reloadDescription()`, then `GetRawQuestFields`. Not observer-safe.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:225-242` - `currentObjective` getter calls `GetRawQuestFields` and `reloadObjective()` every access (no loaded flag). Not observer-safe.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\CraftingQuest.cs:14` field `ItemId` (NetString) with XmlElement `indexToCraft`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\ItemDeliveryQuest.cs:19-27` fields: `target` (NetString), `ItemId` (NetString), `number` (NetInt default 1). `targetMessage` (plain string at line 15).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\SlayMonsterQuest.cs:16-42` fields: `monsterName`, `target`, `monster`, `numberToKill`, `reward`, `numberKilled`, `ignoreFarmMonsters` (NetBool default true). `targetMessage` (plain string at line 13).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\SocializeQuest.cs:13-16` fields: `whoToGreet` (NetStringList), `total` (NetInt).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\GoSomewhereQuest.cs:9` field: `whereToGo` (NetString).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\FishingQuest.cs:12-31` fields: `target`, `numberToFish`, `reward`, `numberFished`, `ItemId` (XmlElement `whichFish`). `targetMessage` (plain string at line 15).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\HaveBuildingQuest.cs:11` field: `buildingType` (NetString).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\ItemHarvestQuest.cs:10-14` fields: `ItemId` (XmlElement `itemIndex`), `Number` (NetInt). Constructor sets `questType.Value = 9`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\ResourceCollectionQuest.cs:12-32` fields: `target`, `targetMessage` (NetString at line 16), `numberCollected`, `number`, `reward`, `ItemId` (XmlElement `resource`).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\LostItemQuest.cs:13-33` fields: `npcName`, `locationOfItem`, `ItemId` (XmlElement `itemIndex`), `tileX`, `tileY`, `itemFound`. Constructor sets `questType.Value = 9`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\SecretLostItemQuest.cs:10-26` fields: `npcName`, `friendshipReward`, `exclusiveQuestId`, `ItemId` (XmlElement `itemIndex`), `itemFound`. Constructor sets `questType.Value = 9`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\SpecialOrder.cs` has `questKey`, `questName`, `questDescription`, `requester`, `orderType`, `specialRule`, `dueDate`, `questDuration`, `questState`, `objectives`, `rewards`, `participants`, `unclaimedRewards`, `donatedItems`, `appliedSpecialRules`, `_isIslandOrder`. Also: `preSelectedItems` (NetStringDictionary), `selectedRandomElements` (NetStringDictionary<int, NetInt>), `generationSeed` (NetInt), `readyForRemoval` (NetBool), `itemToRemoveOnEnd` (NetString), `mailToRemoveOnEnd` (NetString).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\OrderObjective.cs` base: `currentCount` (NetIntDelta), `maxCount`, `description`, `_complete`, `failOnCompletion` (NetBool default false). `IsComplete()` at line 165-168 directly returns `_complete`. XmlInclude: `CollectObjective`, `DeliverObjective`, `DonateObjective`, `FishObjective`, `GiftObjective`, `JKScoreObjective`, `ReachMineFloorObjective`, `ShipObjective`, `SlayObjective`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\OrderReward.cs` XmlInclude: `FriendshipReward`, `GemsReward`, `MailReward`, `MoneyReward`, `ObjectReward`, `ResetEventReward`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\CollectObjective.cs:7` field: `acceptableContextTagSets` (NetStringList).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\DeliverObjective.cs:8-17` fields: `acceptableContextTagSets`, `targetName`, `message`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\DonateObjective.cs:10-28` fields: `dropBox`, `dropBoxGameLocation`, `dropBoxTileLocation` (NetVector2), `acceptableContextTagSets`, `minimumCapacity` (NetInt default -1), `confirmed` (NetBool default false).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\FishObjective.cs:7` field: `acceptableContextTagSets`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\GiftObjective.cs:7-10` fields: `acceptableContextTagSets`, `minimumLikeLevel` (NetEnum<LikeLevels>).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\JKScoreObjective.cs:7` no extra fields.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\ReachMineFloorObjective.cs:7` field: `skullCave` (NetBool).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\ShipObjective.cs:9-15` fields: `acceptableContextTagSets`, `useShipmentValue` (NetBool).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\SlayObjective.cs:7-9` fields: `targetNames` (NetStringList), `ignoreFarmMonsters` (NetBool default true).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\MoneyReward.cs` fields: `amount` (NetInt), `multiplier` (NetFloat default 1f).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\FriendshipReward.cs` fields: `targetName` (NetString), `amount` (NetInt).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\GemsReward.cs` field: `amount` (NetInt).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\MailReward.cs` fields: `noLetter` (NetBool default true), `grantedMails` (NetStringList), `host` (NetBool default false).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\ObjectReward.cs` fields: `itemKey` (NetString), `amount` (NetInt).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\ResetEventReward.cs` field: `resetEvents` (NetStringList).

## Repository Evidence

- `src\StardewAI.Contracts\State\ProgressState.cs` defines `QuestProgressRef` with backing-field-based `Title`/`Description`/`CurrentObjective`, `TitleAvailable`/`DescriptionAvailable`, lifecycle fields (`RewardDescription`, `ShowNew`, `Destroy`, `NextQuests`), `RuntimeType`, `PerTypeQuestFields` (with `Reward`, `TargetMessage`). `SpecialOrderProgressRef` adds `Participants`, `SeenParticipants`, `UnclaimedRewards` (Dictionary), `DonatedItems` (SpecialOrderDonatedItemRef[]), `PreSelectedItems`, `SelectedRandomElements`, `GenerationSeed`, `ReadyForRemoval`, `ItemToRemoveOnEnd`, `MailToRemoveOnEnd`.
- `src\StardewAI.TransparentBridge\Adapters\ProgressReadAdapter.cs` no longer calls `questTitle`/`questDescription`/`currentObjective` getters; reads `_questTitle`/`_questDescription`/`_currentObjective` public backing fields directly. Populates all new special-order lifecycle/state fields from direct Net fields.
- `src\StardewAI.Core\OptionRegistry\QuestCandidateBuilder.cs` `CategorizeOrdinaryNextAction` case 9 uses `CategorizeType9NextAction` for decompile-backed next-action categories. Empty `specialRule` (string.Empty) is valid; only null is unavailable. `CategorizeSpecialOrderNextAction` returns `selectedObjectiveIndex` for single-objective binding. `BuildCompilerEnvelope` filters by all supplied identities, rejects ambiguous/absent.
- `src\StardewAI.Core\Execution\ActionQueueCompiler.cs` `CompileQuestPlan` reconstructs candidates from live snapshot, requires identity, cross-checks all parameters, populates envelope from matched live evidence.

## Items 39-45 Evidence

### Item 39
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:102` — `Quest` implements `IHaveModData` with public `modData` (ModDataDictionary).
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Quests\Quest.cs:94` — `obsolete_completionString` public field (preserved for legacy saves).
- Bridge `ProgressQuestReadAdapter` reads `quest.modData.Pairs` ordered by key for deterministic serialization.
- `QuestProgressRef.ModData` emitted as `Dictionary<string, string>`.
- `QuestProgressRef.ObsoleteCompletionString` emitted as `string` (empty if null).

### Item 40
- `Quest._questTitle` is initialized to `""` (Quest.cs:51).
- `Quest._questDescription` is initialized to `""` (Quest.cs:49).
- `Quest._currentObjective` is initialized to `""` (Quest.cs:47).
- `TitleAvailable` now fails closed when `_questTitle` is null or empty.
- `DescriptionAvailable` now fails closed when `_questDescription` is null or empty.
- New `CurrentObjectiveAvailable` field emitted alongside `current_objective`.

### Item 41
- `ReadQuestProgressMapper` extracted as a pure side-effect-free mapper implementing `IQuestProgressMapper`.
- `ProgressQuestReadAdapter` now accepts `IQuestProgressMapper` via constructor injection (defaults to `ReadQuestProgressMapper`).
- Direct mapper tests verify interface contract, concrete instantiation, and full mapping coverage.
- All existing `QuestCandidateBuilder` tests continue to exercise the mapper-to-candidate pipeline.

### Item 42
- `Quest.modData` and `obsolete_completionString` evidence documented above.
- `transparency-coverage.md` rows added for `mod_data` and `obsolete_completion_string`.

### Item 43
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.SpecialOrders\SpecialOrder.cs:192-201` — `OnFail()` writes `null` to `donatedItems[i]` before removal. Live `donatedItems` may contain null entries.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs:187` — `Item.QualifiedItemId` read for qualified ID alongside raw `ItemId`.
- `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Item.cs:87` — `Item.modData` read for donated item mod data.
- Bridge adapter filters null entries, emits `QualifiedItemId`, `ItemId`, `ModData`, `Stack`, `Quality`. No direct donor-ID field exists in `SpecialOrder.donatedItems` collection; the donor-ID claim has been removed.

### Item 44
- `CategorizeSpecialOrderNextAction` already captures `selectedObjectiveIndex` via `out` parameter (Item 31).
- `QuestCandidateRef.SelectedObjectiveIndex` now emitted in the candidate DTO.
- `QuestCompilerEnvelope.SelectedObjectiveIndex` and `QuestPlanEnvelope.SelectedObjectiveIndex` populated from matched candidate.
- `CompileQuestPlan` cross-checks the model-supplied `selected_objective_index` against live evidence.
- `BuildCompilerEnvelope` validates `requestedObjectiveIndex` with malformed/mismatch rejection.

### Item 45
- `BuildCompilerEnvelope` now rejects malformed `requestedTargetCount`, `requestedCurrentCount`, `requestedObjectiveIndex` with explicit `_malformed:` block reasons instead of silently skipping comparison.
- `CompileQuestPlan` in `ActionQueueCompiler` distinguishes: missing identity, not found, ambiguous match, malformed numeric, and mismatch.
- Consistent null/empty/whitespace handling via `string.IsNullOrWhiteSpace` guard before parsing.

## Items 30-38 Evidence

### Item 30
- `ProgressReadAdapter.ReadPerTypeQuestFields` now has `case Quest _ when quest.GetType() == typeof(Quest)` before the 11 subclass cases. Only truly unknown derived classes hit `default`.

### Item 31
- `CategorizeSpecialOrderNextAction` signature: `out int selectedObjectiveIndex`. `BuildSpecialOrderCandidate` uses `objectives[selectedObjectiveIndex]` consistently.

### Item 32
- `CategorizeType9NextAction` differentiates `LostItemQuest`, `SecretLostItemQuest` (with item-found state), and `ItemHarvestQuest` using `PerTypeQuestFields` evidence, produces specific action categories.

### Item 33
- `order.SpecialRule is null` is the only trigger for `special_rule_unavailable`. `string.Empty` passes through without diagnostic.

### Item 34
- `SpecialOrderProgressRef` has `Participants`, `SeenParticipants`, `UnclaimedRewards` as `Dictionary<long, bool>` ordered by key. `DonatedItems` as `SpecialOrderDonatedItemRef[]`.

### Item 35
- Six special-order lifecycle/randomization fields added: `PreSelectedItems`, `SelectedRandomElements`, `GenerationSeed`, `ReadyForRemoval`, `ItemToRemoveOnEnd`, `MailToRemoveOnEnd`. All populated from direct Net field reads.

### Item 36
- All three observable presentation fields (`questTitle`, `questDescription`, `currentObjective`) are read from their public backing fields (`_questTitle`, `_questDescription`, `_currentObjective`). `TitleAvailable`/`DescriptionAvailable` computed from field state.

### Item 37
- Lifecycle fields: `rewardDescription`, `showNew`, `destroy`, `nextQuests` added to `QuestProgressRef`. Subclass `reward`/`targetMessage` added to `PerTypeQuestFields`.

### Item 38
- `ActionQueueCompiler.CompileQuestPlan` reconstructs from `quests.active_quests`/`quests.special_orders` JSON. Requires identity. Filters by all supplied identities. Rejects not-found and ambiguous matches. Cross-checks runtime type, next action, target NPC/location, item ID, counts. Populates from matched live candidate only. Attaches `LiveEvidence`.
