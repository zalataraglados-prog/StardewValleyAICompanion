using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool ValidateSpecialOrderCollectCropTarget(
        TrainingExecutionRequest request,
        Crop crop,
        out int? currentBefore,
        out string reason)
    {
        currentBefore = null;
        reason = string.Empty;
        if (!request.QuestAcquisitionTargetStep ||
            !request.QuestObjectiveIndex.HasValue)
        {
            reason = "special_order_collect_typed_target_required";
            return false;
        }

        var order = Game1.player.team.specialOrders.SingleOrDefault(candidate =>
            string.Equals(candidate.questKey.Value, request.QuestKey, StringComparison.Ordinal));
        if (order is null ||
            request.QuestObjectiveIndex.Value < 0 ||
            request.QuestObjectiveIndex.Value >= order.objectives.Count ||
            order.objectives[request.QuestObjectiveIndex.Value] is not CollectObjective objective)
        {
            reason = "special_order_collect_live_identity_not_found";
            return false;
        }

        currentBefore = objective.GetCount();
        if (currentBefore != request.QuestExpectedCurrentCount ||
            objective.GetMaxCount() != request.QuestExpectedTargetCount)
        {
            reason = "special_order_collect_progress_projection_drifted";
            return false;
        }

        var harvestQualifiedId = ItemRegistry.QualifyItemId(crop.indexOfHarvest.Value) ??
            "(O)" + crop.indexOfHarvest.Value;
        var harvestItem = ItemRegistry.Create(harvestQualifiedId);
        if (!NativeCollectObjectiveMatches(objective, harvestItem))
        {
            reason = "special_order_collect_context_tags_drifted";
            return false;
        }
        return true;
    }

    private static bool NativeCollectObjectiveMatches(CollectObjective objective, Item item)
    {
        var tags = item.GetContextTags();
        foreach (var acceptableSet in objective.acceptableContextTagSets)
        {
            var rejected = acceptableSet
                .Split(',')
                .Any(group => !ItemContextTagManager.DoAnyTagsMatch(group.Split('/'), tags));
            if (!rejected)
            {
                return true;
            }
        }
        return false;
    }

    private static void ApplySpecialOrderCollectCropFeedback(
        TrainingExecutionResult result,
        TrainingExecutionRequest request,
        int? currentBefore)
    {
        var order = Game1.player.team.specialOrders.SingleOrDefault(candidate =>
            string.Equals(candidate.questKey.Value, request.QuestKey, StringComparison.Ordinal));
        var presentAfter = order is not null;
        CollectObjective? objective = null;
        if (order is not null &&
            request.QuestObjectiveIndex.HasValue &&
            request.QuestObjectiveIndex.Value >= 0 &&
            request.QuestObjectiveIndex.Value < order.objectives.Count)
        {
            objective = order.objectives[request.QuestObjectiveIndex.Value] as CollectObjective;
        }

        var progressAfter = objective?.GetCount() ?? request.QuestExpectedTargetCount ?? 0;
        var completedAfter = objective?.IsComplete() ?? !presentAfter;
        var progressed = currentBefore.HasValue && progressAfter > currentBefore.Value;
        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestProgressBefore = currentBefore;
        result.QuestProgressAfter = progressAfter;
        result.QuestTargetCount = request.QuestExpectedTargetCount;
        result.QuestPresentBefore = currentBefore.HasValue;
        result.QuestPresentAfter = presentAfter;
        result.QuestCompletedBefore = false;
        result.QuestCompletedAfter = completedAfter;

        if (progressed)
        {
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Concat(new[] { "matching_special_order_collect_count_increased" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            result.ChangedFacts = result.ChangedFacts
                .Concat(new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "special_orders." + request.QuestKey + ".objectives[" +
                            request.QuestObjectiveIndex + "].current_count",
                        Before = currentBefore?.ToString() ?? string.Empty,
                        After = progressAfter.ToString()
                    }
                })
                .ToArray();
            return;
        }

        result.Status = "blocked";
        result.PrimitiveVerificationStatus = "observed_mismatch";
        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Concat(new[] { "crop_harvest_without_matching_special_order_collect_progress" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        result.BlockReasons = result.BlockReasons
            .Concat(new[] { "special_order_collect_progress_not_observed" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
