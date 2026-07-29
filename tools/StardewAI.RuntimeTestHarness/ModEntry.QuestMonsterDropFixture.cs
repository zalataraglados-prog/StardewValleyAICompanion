using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupQuestMonsterDropFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_monster_drop_fixture",
                "quest_monster_drop_fixture=ready",
                "location=not_mineshaft",
                "quest_monster_drop_fixture_requires_mineshaft");
        }
        if (string.IsNullOrWhiteSpace(request.QuestId) ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            !request.QuestExpectedTargetCount.HasValue ||
            request.QuestExpectedTargetCount.Value <= 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_monster_drop_fixture",
                "quest_monster_drop_fixture=ready",
                "quest_or_item=missing",
                "quest_monster_drop_fixture_parameters_required");
        }

        var target = FindMiningCombatFixtureTarget(
            mine,
            requireClearProjectilePath: false,
            requireBombEscape: false);
        if (!target.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_monster_drop_fixture",
                "quest_monster_drop_fixture=ready",
                "target_tile=missing",
                "quest_monster_drop_fixture_no_reachable_tile");
        }

        foreach (var monster in mine.characters.OfType<Monster>().ToArray())
        {
            mine.characters.Remove(monster);
        }
        ClearMiningFixtureArea(mine, target.Value, radius: 4);
        EnsureFixtureInventoryCapacity(Game1.player);

        var item = ItemRegistry.Create(request.QualifiedItemId);
        var monsterTarget = new GreenSlime(
            target.Value.ToVector2() * Game1.tileSize,
            mine.mineLevel);
        monsterTarget.objectsToDrop.Clear();
        monsterTarget.objectsToDrop.Add(item.QualifiedItemId);
        monsterTarget.Speed = 0;
        monsterTarget.moveTowardPlayerThreshold.Value = -1;
        mine.characters.Add(monsterTarget);

        var specialOrderFixture = string.Equals(
            request.QuestFamily,
            "special_order",
            StringComparison.Ordinal);
        ResourceCollectionQuest? quest = null;
        SpecialOrder? order = null;
        CollectObjective? objective = null;
        if (specialOrderFixture)
        {
            foreach (var existing in Game1.player.team.specialOrders
                .Where(candidate => string.Equals(
                    candidate.questKey.Value,
                    request.QuestId,
                    StringComparison.Ordinal))
                .ToArray())
            {
                Game1.player.team.specialOrders.Remove(existing);
            }

            var acceptedTag = item.GetContextTags()
                .OrderBy(value => value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(acceptedTag))
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_quest_monster_drop_fixture",
                    "quest_monster_drop_fixture=ready",
                    "item_context_tag=missing",
                    "quest_monster_drop_fixture_item_context_tag_required");
            }

            order = new SpecialOrder();
            order.questKey.Value = request.QuestId;
            order.questName.Value = "StardewAI runtime monster drop";
            order.questDescription.Value = "Collect a deterministic monster drop.";
            order.requester.Value = "Robin";
            order.questState.Value = SpecialOrderStatus.InProgress;
            order.dueDate.Value = Game1.Date.TotalDays + 7;
            objective = new CollectObjective();
            objective.description.Value = "Collect the fixture drop.";
            objective.maxCount.Value = request.QuestExpectedTargetCount.Value;
            objective.SetCount(0);
            objective.acceptableContextTagSets.Add(acceptedTag);
            order.AddObjective(objective);
            Game1.player.team.specialOrders.Add(order);
            order.Update();
        }
        else
        {
            foreach (var existing in Game1.player.questLog
                .OfType<ResourceCollectionQuest>()
                .Where(candidate => string.Equals(
                    candidate.id.Value,
                    request.QuestId,
                    StringComparison.Ordinal))
                .ToArray())
            {
                Game1.player.questLog.Remove(existing);
            }

            quest = new ResourceCollectionQuest();
            quest.id.Value = request.QuestId;
            quest.ItemId.Value = item.QualifiedItemId;
            quest.number.Value = request.QuestExpectedTargetCount.Value;
            quest.numberCollected.Value = 0;
            quest.target.Value = "Robin";
            quest.accepted.Value = true;
            Game1.player.questLog.Add(quest);
        }

        var weaponSlot = InstallFixtureItem(
            Game1.player,
            new StardewValley.Tools.MeleeWeapon("9"));
        Game1.player.CurrentToolIndex = weaponSlot;
        Game1.player.health = Game1.player.maxHealth;

        var runtimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers
            .GetHashCode(monsterTarget)
            .ToString("X8");
        var currentCount = specialOrderFixture
            ? objective?.GetCount() ?? -1
            : quest?.numberCollected.Value ?? -1;
        var targetCount = specialOrderFixture
            ? objective?.GetMaxCount() ?? -1
            : quest?.number.Value ?? -1;
        var questPresent = specialOrderFixture
            ? order is not null && Game1.player.team.specialOrders.Contains(order)
            : quest is not null && Game1.player.questLog.Contains(quest);
        var verified = mine.characters.Contains(monsterTarget) &&
            questPresent &&
            weaponSlot >= 0 &&
            currentCount == 0 &&
            targetCount == request.QuestExpectedTargetCount.Value &&
            (specialOrderFixture ||
                string.Equals(
                    quest?.ItemId.Value,
                    item.QualifiedItemId,
                    StringComparison.Ordinal));
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = target.Value.X,
            TargetTileY = target.Value.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_quest_monster_drop_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_native_resource_collection_quest_present",
                    "deterministic_monster_drop_present",
                    "native_melee_weapon_present"
                }
                : new[] { "quest_monster_drop_fixture_state_mismatch" },
            RequestedEffect = "quest_family=" +
                (specialOrderFixture ? "special_order_collect" : "resource_collection_quest") +
                ";quest_id=" + request.QuestId +
                ";monster_drop=" + item.QualifiedItemId,
            ObservedEffect = "quest_count=" + currentCount +
                "/" + targetCount +
                ";target_identity=" + runtimeIdentity +
                ";target_type=" + (monsterTarget.GetType().FullName ?? monsterTarget.GetType().Name) +
                ";target_tile=" + target.Value.X + "," + target.Value.Y +
                ";weapon_slot=" + weaponSlot,
            CombatTargetRuntimeType = monsterTarget.GetType().FullName ??
                monsterTarget.GetType().Name,
            CombatTargetRuntimeIdentity = runtimeIdentity,
            CombatTargetName = monsterTarget.Name,
            QuestCandidateId = "runtime_fixture:" + request.QuestId,
            QuestFamily = specialOrderFixture ? "special_order" : "ordinary_quest",
            QuestId = request.QuestId,
            QuestKey = specialOrderFixture ? request.QuestId : string.Empty,
            QuestObjectiveIndex = specialOrderFixture ? 0 : null,
            QuestProgressBefore = 0,
            QuestProgressAfter = currentCount,
            QuestTargetCount = targetCount,
            QuestPresentBefore = false,
            QuestPresentAfter = true,
            QuestCompletedBefore = false,
            QuestCompletedAfter = specialOrderFixture
                ? order?.questState.Value == SpecialOrderStatus.Complete
                : quest?.completed.Value,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "quest_monster_drop_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "quests.runtime_fixture:" + request.QuestId,
                        Before = "absent",
                        After = "present"
                    },
                    new SimulatedFactChange
                    {
                        Path = "mining.monsters[" + runtimeIdentity + "].present",
                        Before = "false",
                        After = "true"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
