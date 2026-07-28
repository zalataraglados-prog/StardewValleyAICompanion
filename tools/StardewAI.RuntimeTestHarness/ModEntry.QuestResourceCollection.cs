using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static string? ValidateQuestResourceReceiptTarget(
        TrainingExecutionRequest request,
        string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            !string.Equals(request.QuestRuntimeType, "ResourceCollectionQuest", StringComparison.Ordinal))
        {
            return null;
        }
        if (!request.QuestAcquisitionTargetStep ||
            !string.Equals(request.QuestFamily, "ordinary_quest", StringComparison.Ordinal))
        {
            return "quest_resource_typed_receipt_required";
        }

        var quest = Game1.player.questLog
            .OfType<ResourceCollectionQuest>()
            .SingleOrDefault(candidate => string.Equals(candidate.id.Value, request.QuestId, StringComparison.Ordinal));
        if (quest is null || quest.completed.Value)
        {
            return "quest_resource_live_identity_not_found";
        }
        if (quest.numberCollected.Value != request.QuestExpectedCurrentCount ||
            quest.number.Value != request.QuestExpectedTargetCount)
        {
            return "quest_resource_progress_projection_drifted";
        }
        return string.Equals(
            quest.ItemId.Value,
            qualifiedItemId,
            StringComparison.OrdinalIgnoreCase)
                ? null
                : "quest_resource_receipt_item_drifted";
    }

    private static void ApplyQuestResourceReceiptFeedback(
        TrainingExecutionResult result,
        TrainingExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            !request.QuestAcquisitionTargetStep ||
            !string.Equals(request.QuestRuntimeType, "ResourceCollectionQuest", StringComparison.Ordinal))
        {
            return;
        }

        var quest = Game1.player.questLog
            .OfType<ResourceCollectionQuest>()
            .SingleOrDefault(candidate => string.Equals(candidate.id.Value, request.QuestId, StringComparison.Ordinal));
        var presentAfter = quest is not null;
        var completedAfter = quest?.completed.Value ?? true;
        var progressBefore = request.QuestExpectedCurrentCount ?? 0;
        var targetCount = request.QuestExpectedTargetCount ?? 0;
        var progressAfter = quest?.numberCollected.Value ?? targetCount;
        var progressed = progressAfter > progressBefore;

        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestProgressBefore = progressBefore;
        result.QuestProgressAfter = progressAfter;
        result.QuestTargetCount = targetCount;
        result.QuestPresentBefore = true;
        result.QuestPresentAfter = presentAfter;
        result.QuestCompletedBefore = false;
        result.QuestCompletedAfter = completedAfter;

        if (progressed)
        {
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Concat(new[] { "matching_resource_collection_quest_count_increased" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            result.ChangedFacts = result.ChangedFacts
                .Concat(new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "quests." + request.QuestCandidateId + ".current_count",
                        Before = progressBefore.ToString(),
                        After = progressAfter.ToString()
                    }
                })
                .ToArray();
            return;
        }

        result.Status = "blocked";
        result.PrimitiveVerificationStatus = "observed_mismatch";
        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Concat(new[] { "resource_received_without_matching_quest_progress" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        result.BlockReasons = result.BlockReasons
            .Concat(new[] { "quest_resource_progress_not_observed" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
