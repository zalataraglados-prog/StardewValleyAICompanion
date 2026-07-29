using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed record CollectionTaskFixtureState(
        bool IsSpecialOrder,
        string Family,
        string QuestId,
        string QuestKey,
        int? ObjectiveIndex,
        int CurrentCount,
        int TargetCount,
        bool Present,
        bool Complete,
        string AcceptedContextTag);

    private TrainingExecutionResult ExecuteSetupCollectionTaskFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (string.IsNullOrWhiteSpace(request.QuestId) ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            !request.QuestExpectedTargetCount.HasValue ||
            request.QuestExpectedTargetCount.Value <= 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_collection_task_fixture",
                "collection_task_fixture=ready",
                "quest_or_item=missing",
                "collection_task_fixture_parameters_required");
        }

        Item item;
        try
        {
            item = ItemRegistry.Create(request.QualifiedItemId);
        }
        catch
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_collection_task_fixture",
                "collection_task_fixture=ready",
                "qualified_item=invalid",
                "collection_task_fixture_item_invalid");
        }

        if (!TryInstallCollectionTaskFixture(
                request,
                item,
                out var state,
                out var reason))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_collection_task_fixture",
                "collection_task_fixture=ready",
                "collection_task_fixture=not_installed",
                reason);
        }

        return CollectionTaskFixtureResult(
            request,
            state,
            "debug_setup_collection_task_fixture");
    }

    private static bool TryInstallCollectionTaskFixture(
        TrainingExecutionRequest request,
        Item item,
        out CollectionTaskFixtureState state,
        out string reason)
    {
        state = null!;
        reason = string.Empty;
        var specialOrder = string.Equals(
            request.QuestFamily,
            "special_order",
            StringComparison.Ordinal);
        if (!specialOrder &&
            !string.Equals(
                request.QuestFamily,
                "ordinary_quest",
                StringComparison.Ordinal))
        {
            reason = "collection_task_fixture_family_invalid";
            return false;
        }

        if (specialOrder)
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
                reason = "collection_task_fixture_item_context_tag_required";
                return false;
            }

            var order = new SpecialOrder();
            order.questKey.Value = request.QuestId;
            order.questName.Value = "StardewAI runtime collection";
            order.questDescription.Value = "Collect the isolated fixture item.";
            order.requester.Value = "Robin";
            order.questState.Value = SpecialOrderStatus.InProgress;
            order.dueDate.Value = Game1.Date.TotalDays + 7;
            var objective = new CollectObjective();
            objective.description.Value = "Collect the fixture item.";
            objective.maxCount.Value = request.QuestExpectedTargetCount!.Value;
            objective.SetCount(0);
            objective.acceptableContextTagSets.Add(acceptedTag);
            order.AddObjective(objective);
            Game1.player.team.specialOrders.Add(order);
            order.Update();

            state = new CollectionTaskFixtureState(
                true,
                "special_order",
                request.QuestId!,
                request.QuestId!,
                0,
                objective.GetCount(),
                objective.GetMaxCount(),
                Game1.player.team.specialOrders.Contains(order),
                order.questState.Value == SpecialOrderStatus.Complete,
                acceptedTag);
            return true;
        }

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

        var quest = new ResourceCollectionQuest();
        quest.id.Value = request.QuestId;
        quest.ItemId.Value = item.QualifiedItemId;
        quest.number.Value = request.QuestExpectedTargetCount!.Value;
        quest.target.Value = "Robin";
        quest.accepted.Value = true;
        Game1.player.questLog.Add(quest);
        state = new CollectionTaskFixtureState(
            false,
            "ordinary_quest",
            request.QuestId!,
            string.Empty,
            null,
            quest.numberCollected.Value,
            quest.number.Value,
            Game1.player.questLog.Contains(quest),
            quest.completed.Value,
            string.Empty);
        return true;
    }

    private static TrainingExecutionResult CollectionTaskFixtureResult(
        TrainingExecutionRequest request,
        CollectionTaskFixtureState state,
        string primitiveKind)
    {
        var verified = state.Present &&
            !state.Complete &&
            state.CurrentCount == 0 &&
            state.TargetCount == request.QuestExpectedTargetCount;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_native_collection_task_present" }
                : new[] { "collection_task_fixture_state_mismatch" },
            RequestedEffect = "quest_family=" + state.Family +
                ";quest_id=" + state.QuestId +
                ";qualified_item_id=" + request.QualifiedItemId,
            ObservedEffect = "quest_count=" + state.CurrentCount +
                "/" + state.TargetCount +
                ";accepted_context_tag=" + state.AcceptedContextTag,
            QuestCandidateId = "runtime_fixture:" + state.QuestId,
            QuestFamily = state.Family,
            QuestId = state.QuestId,
            QuestKey = state.QuestKey,
            QuestObjectiveIndex = state.ObjectiveIndex,
            QuestProgressBefore = 0,
            QuestProgressAfter = state.CurrentCount,
            QuestTargetCount = state.TargetCount,
            QuestPresentBefore = false,
            QuestPresentAfter = state.Present,
            QuestCompletedBefore = false,
            QuestCompletedAfter = state.Complete,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "collection_task_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "quests.runtime_fixture:" + state.QuestId,
                        Before = "absent",
                        After = "present"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
