using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool ValidateQuestItemHarvestTarget(
        TrainingExecutionRequest request,
        Crop crop,
        out int? remainingBefore,
        out string reason)
    {
        remainingBefore = null;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId))
        {
            return true;
        }
        if (string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal) &&
            string.Equals(request.QuestRuntimeType, "SpecialOrder", StringComparison.Ordinal))
        {
            if (request.QuestAcquisitionSourceStep)
            {
                var qualifiedItemId = ItemRegistry.QualifyItemId(crop.indexOfHarvest.Value) ??
                    "(O)" + crop.indexOfHarvest.Value;
                return ValidateSpecialOrderCollectSourceTarget(
                    request,
                    qualifiedItemId,
                    out reason);
            }
            return ValidateSpecialOrderCollectCropTarget(
                request,
                crop,
                out remainingBefore,
                out reason);
        }
        if (string.Equals(request.QuestRuntimeType, "ResourceCollectionQuest", StringComparison.Ordinal) &&
            request.QuestAcquisitionSourceStep)
        {
            var qualifiedItemId = ItemRegistry.QualifyItemId(crop.indexOfHarvest.Value) ??
                "(O)" + crop.indexOfHarvest.Value;
            reason = ValidateQuestResourceSourceTarget(
                request,
                new[] { qualifiedItemId }) ?? string.Empty;
            return string.IsNullOrWhiteSpace(reason);
        }
        if (!request.QuestAcquisitionTargetStep ||
            !string.Equals(request.QuestFamily, "ordinary_quest", StringComparison.Ordinal) ||
            !string.Equals(request.QuestRuntimeType, "ItemHarvestQuest", StringComparison.Ordinal))
        {
            reason = "quest_harvest_typed_target_required";
            return false;
        }

        var quest = Game1.player.questLog
            .OfType<ItemHarvestQuest>()
            .SingleOrDefault(candidate => string.Equals(candidate.id.Value, request.QuestId, StringComparison.Ordinal));
        if (quest is null || quest.completed.Value)
        {
            reason = "quest_harvest_live_identity_not_found";
            return false;
        }

        remainingBefore = quest.Number.Value;
        if (request.QuestExpectedCurrentCount != 0 ||
            request.QuestExpectedTargetCount != remainingBefore)
        {
            reason = "quest_harvest_progress_projection_drifted";
            return false;
        }

        var harvestQualifiedId = ItemRegistry.QualifyItemId(crop.indexOfHarvest.Value) ??
            "(O)" + crop.indexOfHarvest.Value;
        var harvestItem = ItemRegistry.Create(harvestQualifiedId);
        var requiredItemId = quest.ItemId.Value ?? string.Empty;
        var matches = requiredItemId.StartsWith("-", StringComparison.Ordinal)
            ? int.TryParse(requiredItemId, out var category) && harvestItem.Category == category
            : string.Equals(harvestQualifiedId, requiredItemId, StringComparison.Ordinal);
        if (!matches)
        {
            reason = "quest_harvest_item_identity_drifted";
            return false;
        }

        return true;
    }

    private static void ApplyQuestItemHarvestFeedback(
        TrainingExecutionResult result,
        TrainingExecutionRequest request,
        int? remainingBefore)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId))
        {
            return;
        }
        if (string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal) &&
            string.Equals(request.QuestRuntimeType, "SpecialOrder", StringComparison.Ordinal))
        {
            if (request.QuestAcquisitionSourceStep)
            {
                ApplySpecialOrderCollectSourceFeedback(result, request);
                return;
            }
            ApplySpecialOrderCollectCropFeedback(result, request, remainingBefore);
            return;
        }
        if (string.Equals(request.QuestRuntimeType, "ResourceCollectionQuest", StringComparison.Ordinal) &&
            request.QuestAcquisitionSourceStep)
        {
            ApplyQuestResourceSourceFeedback(result, request);
            return;
        }

        var quest = Game1.player.questLog
            .OfType<ItemHarvestQuest>()
            .SingleOrDefault(candidate => string.Equals(candidate.id.Value, request.QuestId, StringComparison.Ordinal));
        var presentAfter = quest is not null;
        var completedAfter = quest?.completed.Value ?? true;
        var remainingAfter = quest?.Number.Value ?? 0;
        var progressed = remainingBefore.HasValue &&
            (remainingAfter < remainingBefore.Value || completedAfter || !presentAfter);

        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestProgressBefore = 0;
        result.QuestProgressAfter = remainingBefore.HasValue
            ? Math.Max(0, remainingBefore.Value - Math.Max(0, remainingAfter))
            : null;
        result.QuestTargetCount = remainingBefore;
        result.QuestPresentBefore = remainingBefore.HasValue;
        result.QuestPresentAfter = presentAfter;
        result.QuestCompletedBefore = false;
        result.QuestCompletedAfter = completedAfter;

        if (progressed)
        {
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Concat(new[] { "matching_item_harvest_quest_remaining_count_decreased" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            result.ChangedFacts = result.ChangedFacts
                .Concat(new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "quests." + request.QuestCandidateId + ".remaining_count",
                        Before = remainingBefore?.ToString() ?? string.Empty,
                        After = remainingAfter.ToString()
                    }
                })
                .ToArray();
            return;
        }

        result.Status = "blocked";
        result.PrimitiveVerificationStatus = "observed_mismatch";
        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Concat(new[] { "harvest_applied_without_matching_item_harvest_quest_progress" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        result.BlockReasons = result.BlockReasons
            .Concat(new[] { "quest_harvest_progress_not_observed" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
