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
        var harvestQualifiedId = ItemRegistry.QualifyItemId(crop.indexOfHarvest.Value) ??
            "(O)" + crop.indexOfHarvest.Value;
        return ValidateSpecialOrderCollectItemTarget(
            request,
            ItemRegistry.Create(harvestQualifiedId),
            out currentBefore,
            out reason);
    }

    private static bool ValidateSpecialOrderCollectItemTarget(
        TrainingExecutionRequest request,
        Item item,
        out int? currentBefore,
        out string reason)
    {
        currentBefore = null;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            !string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal))
        {
            return true;
        }
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
        if (!NativeCollectObjectiveMatches(objective, item))
        {
            reason = "special_order_collect_context_tags_drifted";
            return false;
        }
        return true;
    }

    private static bool ValidateSpecialOrderCollectSourceTarget(
        TrainingExecutionRequest request,
        string qualifiedItemId,
        out string reason)
    {
        return ValidateSpecialOrderCollectSourceTarget(
            request,
            new[] { qualifiedItemId },
            out reason);
    }

    private static bool ValidateSpecialOrderCollectSourceTarget(
        TrainingExecutionRequest request,
        IEnumerable<string> qualifiedItemIds,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            !string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal))
        {
            return true;
        }
        if (!request.QuestAcquisitionSourceStep ||
            request.QuestAcquisitionTargetStep ||
            !request.QuestObjectiveIndex.HasValue)
        {
            reason = "special_order_collect_typed_source_required";
            return false;
        }

        var order = Game1.player.team.specialOrders.SingleOrDefault(candidate =>
            string.Equals(candidate.questKey.Value, request.QuestKey, StringComparison.Ordinal));
        if (order is null ||
            request.QuestObjectiveIndex.Value < 0 ||
            request.QuestObjectiveIndex.Value >= order.objectives.Count ||
            order.objectives[request.QuestObjectiveIndex.Value] is not CollectObjective objective)
        {
            reason = "special_order_collect_live_source_identity_not_found";
            return false;
        }
        if (objective.GetCount() != request.QuestExpectedCurrentCount ||
            objective.GetMaxCount() != request.QuestExpectedTargetCount)
        {
            reason = "special_order_collect_source_progress_projection_drifted";
            return false;
        }
        if (!qualifiedItemIds.Any(qualifiedItemId =>
            NativeCollectObjectiveMatches(
                objective,
                ItemRegistry.Create(qualifiedItemId))))
        {
            reason = "special_order_collect_source_context_tags_drifted";
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
        ApplySpecialOrderCollectFeedback(result, request, currentBefore);
    }

    private static void ApplySpecialOrderCollectFeedback(
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

    private static void ApplySpecialOrderCollectSourceFeedback(
        TrainingExecutionResult result,
        TrainingExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            !string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal) ||
            !request.QuestAcquisitionSourceStep ||
            request.QuestAcquisitionTargetStep)
        {
            return;
        }

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

        var progressBefore = request.QuestExpectedCurrentCount ?? 0;
        var progressAfter = objective?.GetCount() ??
            request.QuestExpectedTargetCount ??
            progressBefore;
        var completedAfter = objective?.IsComplete() ?? !presentAfter;
        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestProgressBefore = progressBefore;
        result.QuestProgressAfter = progressAfter;
        result.QuestTargetCount = request.QuestExpectedTargetCount;
        result.QuestPresentBefore = true;
        result.QuestPresentAfter = presentAfter;
        result.QuestCompletedBefore = false;
        result.QuestCompletedAfter = completedAfter;

        if (progressAfter < progressBefore)
        {
            result.Status = "blocked";
            result.PrimitiveVerificationStatus = "observed_mismatch";
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Concat(new[] { "special_order_collect_source_progress_regressed" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            result.BlockReasons = result.BlockReasons
                .Concat(new[] { "special_order_collect_source_progress_regressed" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return;
        }

        if (progressAfter > progressBefore)
        {
            result.ChangedFacts = result.ChangedFacts
                .Concat(new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "special_orders." + request.QuestKey + ".objectives[" +
                            request.QuestObjectiveIndex + "].current_count",
                        Before = progressBefore.ToString(),
                        After = progressAfter.ToString()
                    }
                })
                .ToArray();
        }
        var primitiveVerified = string.Equals(
            result.PrimitiveVerificationStatus,
            "verified",
            StringComparison.Ordinal);
        var observationReason = primitiveVerified
            ? progressAfter > progressBefore
                ? "special_order_collect_source_output_was_natively_collected"
                : "special_order_collect_source_completed_without_predicted_receipt"
            : progressAfter > progressBefore
                ? "special_order_collect_progress_observed_after_source_primitive_mismatch"
                : "special_order_collect_state_observed_after_source_primitive_mismatch";
        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Concat(new[] { observationReason })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
