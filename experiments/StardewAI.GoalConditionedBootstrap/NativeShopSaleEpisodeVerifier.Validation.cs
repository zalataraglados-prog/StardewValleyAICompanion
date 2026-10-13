using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShopSaleEpisodeVerifier
{
    private static void ValidateEpisodeHeader(
        PlanExecutionEpisodeEnvelope episode)
    {
        if (episode.SchemaVersion != "plan_execution_episode.v1" ||
            string.IsNullOrWhiteSpace(episode.EpisodeId) ||
            string.IsNullOrWhiteSpace(episode.RunId) ||
            episode.OptionId != "executor.sell_shop_item" ||
            episode.Status != "applied" ||
            !episode.Success ||
            episode.TrainingRole != TrainingRoles.ExecutorCalibration ||
            episode.BlockReasons.Length != 0 ||
            episode.PrimitiveKind != "sell_shop_item" ||
            episode.PrimitiveVerificationStatus != "verified")
        {
            throw new InvalidDataException(
                "Native shop-sale execution episode header is invalid.");
        }
    }

    private static void ValidateEpisodeBinding(
        PlanExecutionEpisodeEnvelope episode,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        TrainingExecutionResult result)
    {
        if (episode.SourceStateHash != before.StateHash ||
            episode.AfterStateHash != after.StateHash ||
            !episode.StateHashChanged ||
            episode.SourceStateHash == episode.AfterStateHash ||
            !episode.AfterSnapshotFresh ||
            episode.BeforeGameTick != before.GameTick ||
            episode.AfterGameTick != after.GameTick ||
            episode.AfterGameTick <= episode.BeforeGameTick ||
            episode.RunId != result.RunId ||
            episode.QueueId != result.QueueId ||
            episode.OptionId != result.OptionId ||
            episode.Status != result.Status ||
            episode.PrimitiveKind != result.PrimitiveKind ||
            episode.PrimitiveVerificationStatus !=
                result.PrimitiveVerificationStatus ||
            !episode.PrimitiveVerificationReasons.SequenceEqual(
                result.PrimitiveVerificationReasons,
                StringComparer.Ordinal) ||
            episode.RequestedEffect != result.RequestedEffect ||
            episode.ObservedEffect != result.ObservedEffect ||
            !EqualJson(episode.ChangedFacts, result.ChangedFacts))
        {
            throw new InvalidDataException(
                "Native shop-sale episode does not bind its artifacts.");
        }
    }

    private static void ValidateExecutionResult(
        TrainingExecutionResult result,
        PlanExecutionEpisodeEnvelope episode,
        int moneyBefore,
        int moneyAfter)
    {
        var moneyChanges = result.ChangedFacts.Where(change =>
                change.Path == "player.money" &&
                change.Before == moneyBefore.ToString(
                    CultureInfo.InvariantCulture) &&
                change.After == moneyAfter.ToString(
                    CultureInfo.InvariantCulture))
            .ToArray();
        var reasons = new[]
        {
            "native_shop_menu_click_applied",
            "money_increased_by_exact_sale_value",
            "inventory_count_decreased_by_exact_stack"
        };
        if (result.SchemaVersion != "training_execution_result.v1" ||
            result.BeforeStateHash != episode.SourceStateHash ||
            result.OptionId != "executor.sell_shop_item" ||
            result.Status != "applied" ||
            !result.FeedbackAvailable ||
            result.BlockReasons.Length != 0 ||
            result.PrimitiveKind != "sell_shop_item" ||
            result.PrimitiveVerificationStatus != "verified" ||
            !reasons.All(reason => result.PrimitiveVerificationReasons.Contains(
                reason,
                StringComparer.Ordinal)) ||
            moneyChanges.Length != 1)
        {
            throw new InvalidDataException(
                "Native shop-sale result is not verified.");
        }
    }

    private static ShopSaleCommand ValidateQueueItem(
        PlanExecutionEpisodeEnvelope episode,
        TrainingExecutionResult result,
        uint earnedDelta)
    {
        if (!episode.EffectiveQueueItem.HasValue ||
            episode.EffectiveQueueItem.Value.ValueKind !=
                JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Native shop-sale episode has no effective queue item.");
        }
        var item = episode.EffectiveQueueItem.Value.Deserialize<
            ActionQueueItem>(JsonDefaults.Options) ??
            throw new InvalidDataException(
                "Native shop-sale effective queue item is invalid.");
        var parameters = item.NormalizedCommand.Parameters
            .GroupBy(value => value.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        if (!TrySingleNonEmpty(parameters, "expected_shop_id", out var shopId) ||
            !TrySingleNonEmpty(parameters, "qualified_item_id", out var itemId) ||
            !TrySingleNonNegativeInt(parameters, "slot_index", out var slotIndex) ||
            !TrySinglePositiveInt(parameters, "quantity", out var quantity) ||
            !TrySinglePositiveInt(
                parameters,
                "expected_unit_price",
                out var unitPrice))
        {
            throw new InvalidDataException(
                "Native shop-sale command lacks exact identity or pricing.");
        }
        var expectedDelta = checked((uint)quantity * (uint)unitPrice);
        if (!TrySinglePositiveInt(
                parameters,
                "expected_total_value",
                out var totalValue) ||
            (uint)totalValue != expectedDelta)
        {
            throw new InvalidDataException(
                "Native shop-sale command total value is inconsistent.");
        }
        if (item.QueueItemId != result.QueueItemId ||
            item.OptionId != "executor.sell_shop_item" ||
            item.Status != "pending" ||
            item.BlockingReasons.Length != 0 ||
            item.NormalizedCommand.OptionId != item.OptionId ||
            item.NormalizedCommand.StateHash != episode.SourceStateHash ||
            !item.NormalizedCommand.Steps.Any(step =>
                step.StepType == "sell_shop_item") ||
            expectedDelta != earnedDelta)
        {
            throw new InvalidDataException(
                "Native shop-sale episode does not bind the compiled sale.");
        }
        return new ShopSaleCommand(
            shopId,
            itemId,
            slotIndex,
            quantity,
            unitPrice);
    }
}
