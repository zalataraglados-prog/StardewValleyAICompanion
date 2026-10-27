using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    private static int VerifyPurchaseSlice(
        LoadedTransition transition,
        PurchaseBinding binding,
        ActionQueueItem[] effectiveItems,
        int beforeQuantity,
        int afterQuantity,
        int beforeMoney,
        int afterMoney,
        int transitionIndex)
    {
        var items = effectiveItems;
        var purchaseItems = items.Where(value =>
            value.OptionId == "executor.buy_shop_item").ToArray();
        Require(purchaseItems.Length == 1 &&
                items.All(value => PurchaseQueueOptions.Contains(
                        value.OptionId) ||
                    IsSeedAcquisitionSupportingOption(value.OptionId)),
            "Money Revenue crop seed purchase queue shape is invalid at transition " +
            transitionIndex + ".");
        var parameters = purchaseItems[0].NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        var expectedUnitPrice = SingleIntParameter(
            parameters,
            "expected_unit_price");
        Require(SingleParameter(parameters, "qualified_item_id") ==
                    binding.QualifiedItemId &&
                SingleIntParameter(parameters, "quantity") == 1 &&
                SingleParameter(parameters, "expected_shop_id") ==
                    binding.ShopId &&
                SingleParameter(parameters, "expected_stock_id") ==
                    binding.StockId &&
                SingleIntParameter(parameters, "expected_output_stack") ==
                    binding.OutputStack &&
                SingleIntParameter(parameters, "expected_output_quality") ==
                    binding.OutputQuality &&
                SingleIntParameter(parameters, "max_unit_price") ==
                    binding.MaxUnitPrice &&
                expectedUnitPrice is > 0 &&
                expectedUnitPrice <= binding.MaxUnitPrice &&
                afterQuantity - beforeQuantity == binding.OutputStack &&
                beforeMoney - afterMoney == expectedUnitPrice,
            "Money Revenue crop seed purchase did not produce its exact bound item and money deltas at transition " +
            transitionIndex + ".");
        VerifyTransitionStepBoundaries(
            transition,
            effectiveItems,
            transitionIndex,
            allowPurchaseMutation: true);
        return expectedUnitPrice ?? throw new InvalidDataException(
            "Money Revenue crop seed purchase expected price is missing.");
    }

    private static void VerifySupportingStage(
        LoadedTransition transition,
        ActionQueueItem[] effectiveItems,
        int beforeQuantity,
        int afterQuantity,
        int beforeMoney,
        int afterMoney,
        int transitionIndex)
    {
        var optionIds = effectiveItems
            .Select(value => value.OptionId)
            .ToArray();
        Require(optionIds.Length > 0 &&
                optionIds.All(IsSeedAcquisitionSupportingOption) &&
                optionIds.Any(value => value is
                    "executor.move_to_tile" or
                    "executor.traverse_connector" or
                    "executor.interact" or
                    "executor.choose_dialogue_response" or
                    "executor.wait_ticks") &&
                beforeQuantity == afterQuantity &&
                beforeMoney == afterMoney,
            "Money Revenue crop seed supporting stage is not resource neutral at transition " +
            transitionIndex + ".");
        VerifyTransitionStepBoundaries(
            transition,
            effectiveItems,
            transitionIndex,
            allowPurchaseMutation: false);
    }

    private static void VerifyTransitionStepBoundaries(
        LoadedTransition transition,
        ActionQueueItem[] effectiveItems,
        int transitionIndex,
        bool allowPurchaseMutation)
    {
        var steps = transition.Receipt.StepResults ??
            Array.Empty<QueueExecutionStepReceipt>();
        Require(steps.Length == effectiveItems.Length && steps.Length > 0,
            "Money Revenue crop seed supporting step evidence is incomplete at transition " +
            transitionIndex + ".");
        var expected = transition.Before;
        var receiptDirectory = Path.GetDirectoryName(
            Path.GetFullPath(transition.ReceiptPath)) ?? string.Empty;
        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            Require(!string.IsNullOrWhiteSpace(step.AfterSnapshotPath),
                "Money Revenue crop seed supporting step snapshot path is missing at transition " +
                transitionIndex + ", step " + index + ".");
            var snapshotPath = Path.GetFullPath(step.AfterSnapshotPath);
            Require(snapshotPath.StartsWith(
                        receiptDirectory + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase),
                "Money Revenue crop seed supporting step snapshot escaped its receipt directory at transition " +
                transitionIndex + ", step " + index + ".");
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(
                CurrentTeacherFrontierSupport.ReadArtifactText(snapshotPath),
                JsonDefaults.Options) ?? throw new InvalidDataException(
                    "Money Revenue crop seed supporting step snapshot is invalid at transition " +
                    transitionIndex + ", step " + index + ".");
            Require(expected.StateHash == step.SourceStateHash &&
                    after.StateHash == step.AfterStateHash &&
                    after.GameTick == step.AfterGameTick &&
                    expected.SaveId.Value == after.SaveId.Value &&
                    expected.PlayerId.Value == after.PlayerId.Value &&
                    expected.GameVersion == after.GameVersion,
                "Money Revenue crop seed supporting step snapshot identity drifted at transition " +
                transitionIndex + ", step " + index + ".");

            var effectiveItem = effectiveItems[index];
            if (IsVerifiedNativeStoryInterrupt(step, effectiveItem))
            {
                expected = after;
                continue;
            }

            if (allowPurchaseMutation &&
                effectiveItem.OptionId == "executor.buy_shop_item")
            {
                Require(FullShipmentRecoverySettlementBoundaryProjector
                            .TryProjectExpectedResourceMutationInvariant(
                                expected,
                                out var beforeInvariant) &&
                        FullShipmentRecoverySettlementBoundaryProjector
                            .TryProjectExpectedResourceMutationInvariant(
                                after,
                                out var afterInvariant) &&
                        beforeInvariant == afterInvariant,
                    "Money Revenue crop seed purchase step changed unrelated persistent state at transition " +
                    transitionIndex + ", step " + index + ".");
            }
            else
            {
                Require(FullShipmentRecoverySettlementBoundaryProjector
                            .TryProject(expected, out var beforeBoundary) &&
                        FullShipmentRecoverySettlementBoundaryProjector
                            .TryProject(after, out var afterBoundary) &&
                        beforeBoundary == afterBoundary,
                    "Money Revenue crop seed supporting step changed persistent resources at transition " +
                    transitionIndex + ", step " + index + ": " +
                    string.Join(",", FullShipmentRecoverySettlementBoundaryProjector
                        .ChangedPersistentFields(expected, after)) + ".");
            }
            expected = after;
        }
        Require(expected.StateHash == transition.After.StateHash &&
                expected.GameTick == transition.After.GameTick,
            "Money Revenue crop seed supporting step chain did not reach its transition boundary at transition " +
            transitionIndex + ".");
    }

    private static bool IsVerifiedNativeStoryInterrupt(
        QueueExecutionStepReceipt step,
        ActionQueueItem item)
    {
        return step.Status == "applied" &&
            step.AfterSnapshotFresh &&
            step.PrimitiveVerificationStatus == "verified" &&
            item.OptionId == "executor.advance_story_event" &&
            IsStoryInterruptContinuation(item) &&
            step.EffectiveQueueItem is { ValueKind: JsonValueKind.Object } raw &&
            raw.TryGetProperty("runtime_priority_interrupt", out var interrupt) &&
            interrupt.ValueKind == JsonValueKind.String &&
            interrupt.GetString() == "story_event";
    }

    private static PurchaseBinding ReadPurchaseBinding(
        ActionQueueEnvelope queue,
        string expectedQualifiedItemId)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        var anchors = items.Where(value =>
                SingleParameter(
                    value.NormalizedCommand?.Parameters ??
                        Array.Empty<SmallModelActionParameter>(),
                    "continuation.option_id") == "economy.buy_supplies")
            .ToArray();
        Require(anchors.Length > 0 &&
                items.Except(anchors).All(value =>
                    value.OptionId == "executor.close_menu" ||
                    IsStoryInterruptContinuation(value)),
            "Money Revenue crop seed queue lost its continuation identity.");
        var bindings = anchors.Select(value =>
        {
            var parameters = value.NormalizedCommand?.Parameters ??
                Array.Empty<SmallModelActionParameter>();
            return new PurchaseBinding(
                SingleParameter(parameters, "continuation.shop_id"),
                SingleParameter(parameters, "continuation.stock_id"),
                SingleParameter(
                    parameters,
                    "continuation.qualified_item_id"),
                SingleIntParameter(
                    parameters,
                    "continuation.output_stack_per_purchase") ?? -1,
                SingleIntParameter(
                    parameters,
                    "continuation.output_quality") ?? -1,
                SingleIntParameter(
                    parameters,
                    "continuation.max_unit_price") ?? -1,
                SingleIntParameter(
                    parameters,
                    "continuation.quantity") ?? -1);
        }).Distinct().ToArray();
        Require(bindings.Length == 1 &&
                SingleParameter(
                    anchors[0].NormalizedCommand.Parameters,
                    "continuation.option_id") ==
                    "economy.buy_supplies" &&
                !string.IsNullOrWhiteSpace(bindings[0].ShopId) &&
                !string.IsNullOrWhiteSpace(bindings[0].StockId) &&
                bindings[0].QualifiedItemId == expectedQualifiedItemId &&
                bindings[0].OutputStack > 0 &&
                bindings[0].OutputQuality >= 0 &&
                bindings[0].MaxUnitPrice > 0 &&
                bindings[0].Quantity == 1,
            "Money Revenue crop seed continuation binding is incomplete or ambiguous.");
        return bindings[0];
    }

    private static bool IsSeedAcquisitionSupportingOption(string optionId) =>
        MoneyRevenueResourceNeutralTerminalRouteVerifier
            .ResourceNeutralOptions.Contains(optionId) ||
        optionId == "executor.advance_story_event";

    private static bool IsStoryInterruptContinuation(ActionQueueItem item)
    {
        var parameters = item.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        return item.OptionId == "executor.advance_story_event" &&
            SingleParameter(parameters, "continuation.option_id") ==
                "story.advance_event";
    }

    private static string SingleParameter(
        SmallModelActionParameter[] parameters,
        string name)
    {
        var values = parameters.Where(value => value.Name == name)
            .Select(value => value.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }

    private static int? SingleIntParameter(
        SmallModelActionParameter[] parameters,
        string name) => int.TryParse(
            SingleParameter(parameters, name),
            out var value)
                ? value
                : null;
}
