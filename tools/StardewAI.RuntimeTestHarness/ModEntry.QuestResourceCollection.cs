using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static string? ValidateQuestResourceSourceTarget(
        TrainingExecutionRequest request,
        IEnumerable<string> qualifiedItemIds)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            !string.Equals(request.QuestRuntimeType, "ResourceCollectionQuest", StringComparison.Ordinal))
        {
            return null;
        }
        if (!request.QuestAcquisitionSourceStep ||
            request.QuestAcquisitionTargetStep ||
            !string.Equals(request.QuestFamily, "ordinary_quest", StringComparison.Ordinal))
        {
            return "quest_resource_typed_source_required";
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

        var requiredItemId = quest.ItemId.Value ?? string.Empty;
        var qualifiedRequired = requiredItemId.StartsWith("(", StringComparison.Ordinal)
            ? requiredItemId
            : ItemRegistry.QualifyItemId(requiredItemId) ?? "(O)" + requiredItemId;
        return qualifiedItemIds.Any(itemId => string.Equals(
            itemId,
            qualifiedRequired,
            StringComparison.OrdinalIgnoreCase))
                ? null
                : "quest_resource_source_item_drifted";
    }

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

    private static void ApplyQuestResourceSourceFeedback(
        TrainingExecutionResult result,
        TrainingExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            !request.QuestAcquisitionSourceStep ||
            request.QuestAcquisitionTargetStep ||
            !string.Equals(request.QuestRuntimeType, "ResourceCollectionQuest", StringComparison.Ordinal))
        {
            return;
        }

        var quest = Game1.player.questLog
            .OfType<ResourceCollectionQuest>()
            .SingleOrDefault(candidate => string.Equals(candidate.id.Value, request.QuestId, StringComparison.Ordinal));
        var progressBefore = request.QuestExpectedCurrentCount ?? 0;
        var progressAfter = quest?.numberCollected.Value ??
            request.QuestExpectedTargetCount ??
            progressBefore;
        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestProgressBefore = progressBefore;
        result.QuestProgressAfter = progressAfter;
        result.QuestTargetCount = request.QuestExpectedTargetCount;
        result.QuestPresentBefore = true;
        result.QuestPresentAfter = quest is not null;
        result.QuestCompletedBefore = false;
        result.QuestCompletedAfter = quest?.completed.Value ?? true;

        if (progressAfter < progressBefore)
        {
            result.Status = "blocked";
            result.PrimitiveVerificationStatus = "observed_mismatch";
            result.BlockReasons = result.BlockReasons
                .Concat(new[] { "quest_resource_source_progress_regressed" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return;
        }

        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Concat(new[]
            {
                progressAfter > progressBefore
                    ? "quest_resource_source_output_was_natively_collected"
                    : "quest_resource_source_completed_pending_native_receipt"
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GuaranteedGiantCropOutputIds(GiantCrop giantCrop)
    {
        if (!GiantCrop.TryGetData(giantCrop.Id, out var data) || data?.HarvestItems is null)
        {
            return Array.Empty<string>();
        }

        return data.HarvestItems
            .Where(drop =>
                drop.Chance >= 1f &&
                string.IsNullOrWhiteSpace(drop.Condition) &&
                drop.ForShavingEnchantment != true &&
                string.IsNullOrWhiteSpace(drop.PerItemCondition) &&
                (drop.RandomItemId is null || drop.RandomItemId.Count == 0) &&
                (drop.StackModifiers is null || drop.StackModifiers.Count == 0) &&
                !string.IsNullOrWhiteSpace(drop.ItemId))
            .Select(drop => TryQualifyGuaranteedGiantCropOutput(drop.ItemId))
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string TryQualifyGuaranteedGiantCropOutput(string itemId)
    {
        try
        {
            var qualifiedItemId = ItemRegistry.QualifyItemId(itemId) ?? string.Empty;
            return string.IsNullOrWhiteSpace(qualifiedItemId)
                ? string.Empty
                : ItemRegistry.Create(qualifiedItemId).QualifiedItemId;
        }
        catch
        {
            return string.Empty;
        }
    }
}
