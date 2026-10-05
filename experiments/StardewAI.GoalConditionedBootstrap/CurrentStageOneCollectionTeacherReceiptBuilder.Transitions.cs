using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherReceiptBuilder
{
    private static PolicyTeacherRequirementTransition[] VerifyTransitions(
        CurrentStageOneCollectionTeacherPreferenceLabel preference,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var selected = preference.SelectedCandidate!;
        var queueItems = preference.CompiledQueue!.Items;
        return selected.RequirementCredits
            .Select(credit => VerifyTransition(
                credit,
                queueItems,
                receipt,
                before,
                after))
            .ToArray();
    }

    private static PolicyTeacherRequirementTransition VerifyTransition(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem[] queueItems,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        return credit.BindingKind switch
        {
            "authoritative_acquisition_endpoint" => VerifyAcquisition(
                credit,
                before,
                after),
            "native_full_shipment_completion" => VerifyFullShipment(
                credit,
                queueItems,
                receipt,
                before,
                after),
            "native_museum_donation_completion" => VerifyCollectionBoolean(
                credit,
                before,
                after,
                "museum",
                "donatable_items",
                "qualified_item_id",
                "donated",
                "native_completion_false_to_true"),
            "native_community_center_payment_completion" =>
                VerifyCommunityCenterPayment(credit, before, after),
            "native_community_center_donation_completion" =>
                VerifyCommunityCenterDonation(
                    credit,
                    CommunityCenterDonationQueueItem(credit, queueItems),
                    before,
                    after),
            "authoritative_window_terminal_attempt" => VerifyCollectionBoolean(
                credit,
                before,
                after,
                "fish_collection_progress",
                "items",
                "qualified_item_id",
                "caught",
                "native_collection_false_to_true"),
            "authoritative_window_rolling_route_step" => VerifyRouteStep(
                credit,
                queueItems.LastOrDefault(item => !string.IsNullOrWhiteSpace(
                    ReadParameter(item, "expected_target_location"))),
                before,
                after),
            "authoritative_collection_rolling_route_step" => VerifyRouteStep(
                credit,
                queueItems.LastOrDefault(item => !string.IsNullOrWhiteSpace(
                    ReadParameter(item, "expected_target_location"))),
                before,
                after),
            _ => Transition(
                credit,
                "unsupported_binding_kind",
                "unsupported",
                "unsupported",
                false)
        };
    }

    private static PolicyTeacherRequirementTransition VerifyAcquisition(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var evidence = ExactInventoryReceiptVerifier.Verify(
            before,
            after,
            credit.QualifiedItemId,
            credit.RequiredQuantity,
            credit.MinimumQuality);
        return Transition(
            credit,
            "exact_inventory_quantity_increased",
            CountText(evidence.BeforeQuantity),
            CountText(evidence.AfterQuantity),
            evidence.Verified);
    }

    private static PolicyTeacherRequirementTransition VerifyFullShipment(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem[] queueItems,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var beforeShipped = ReadCollectionBoolean(
            before,
            "full_shipment_progress",
            "items",
            "qualified_item_id",
            credit.QualifiedItemId,
            "shipped");
        var afterShipped = ReadCollectionBoolean(
            after,
            "full_shipment_progress",
            "items",
            "qualified_item_id",
            credit.QualifiedItemId,
            "shipped");
        var beforePending = ShippingBinCount(before, credit.QualifiedItemId);
        var afterPending = ShippingBinCount(after, credit.QualifiedItemId);
        var beforeInventory = InventoryCount(before, credit.QualifiedItemId, 0);
        var afterInventory = InventoryCount(after, credit.QualifiedItemId, 0);
        var settled = beforeShipped == false && afterShipped == true;
        var pending = beforeShipped == false && afterShipped == false &&
            beforePending.HasValue &&
            afterPending.HasValue &&
            beforeInventory.HasValue &&
            afterInventory.HasValue &&
            afterPending.Value > beforePending.Value &&
            beforeInventory.Value - afterInventory.Value >=
                afterPending.Value - beforePending.Value;
        var terminalBefore = "not_required";
        var terminalAfter = "not_required";
        var terminalStepPending = !settled && !pending &&
            VerifyPendingShipmentTerminalStep(
                credit,
                queueItems,
                receipt,
                beforePending,
                afterPending,
                beforeInventory,
                afterInventory,
                after,
                out terminalBefore,
                out terminalAfter);
        return Transition(
            credit,
            settled
                ? "native_full_shipment_false_to_true"
                : terminalStepPending
                    ? "exact_pending_native_shipment_terminal_step_increased"
                    : "exact_pending_native_shipment_increased",
            $"shipped={BooleanText(beforeShipped)};pending={CountText(beforePending)};inventory={CountText(beforeInventory)};terminal={terminalBefore}",
            $"shipped={BooleanText(afterShipped)};pending={CountText(afterPending)};inventory={CountText(afterInventory)};terminal={terminalAfter}",
            settled || pending || terminalStepPending);
    }

    private static bool VerifyPendingShipmentTerminalStep(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem[] queueItems,
        QueueExecutionReceiptEnvelope? receipt,
        int? queueBeforePending,
        int? queueAfterPending,
        int? queueBeforeInventory,
        int? queueAfterInventory,
        SnapshotEnvelope after,
        out string terminalBefore,
        out string terminalAfter)
    {
        terminalBefore = "unavailable";
        terminalAfter = "unavailable";
        if (receipt is null || credit.RequiredQuantity <= 0 ||
            queueItems.Length == 0 ||
            receipt.StepResults.Length != queueItems.Length)
            return false;

        var matches = queueItems
            .Select((item, index) => (Item: item, Index: index))
            .Where(value => string.Equals(
                    value.Item.OptionId,
                    "executor.ship_inventory_item_to_bin",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(value.Item, "qualified_item_id"),
                    credit.QualifiedItemId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 ||
            matches[0].Index != queueItems.Length - 1)
            return false;

        var terminal = receipt.StepResults.SingleOrDefault(step =>
            step.QueueItemIndex == matches[0].Index &&
            string.Equals(
                step.QueueItemId,
                matches[0].Item.QueueItemId,
                StringComparison.Ordinal));
        if (terminal is null ||
            !terminal.SelectedQueueCandidateCompleted ||
            !string.Equals(terminal.Status, "applied", StringComparison.Ordinal) ||
            !string.Equals(
                terminal.PrimitiveKind,
                "ship_inventory_item_to_bin",
                StringComparison.Ordinal) ||
            !string.Equals(
                terminal.PrimitiveVerificationStatus,
                "verified",
                StringComparison.Ordinal) ||
            !string.Equals(terminal.AfterStateHash, after.StateHash,
                StringComparison.Ordinal))
            return false;

        var inventoryPath = "player.inventory." +
            credit.QualifiedItemId + ".count";
        var shippingPath = "farm.shipping_bin." +
            credit.QualifiedItemId + ".count";
        if (!TryReadChangedFactCounts(
                terminal.ChangedFacts,
                inventoryPath,
                out var terminalInventoryBefore,
                out var terminalInventoryAfter) ||
            !TryReadChangedFactCounts(
                terminal.ChangedFacts,
                shippingPath,
                out var terminalPendingBefore,
                out var terminalPendingAfter))
            return false;

        terminalBefore =
            $"pending={terminalPendingBefore};inventory={terminalInventoryBefore}";
        terminalAfter =
            $"pending={terminalPendingAfter};inventory={terminalInventoryAfter}";
        return terminalInventoryBefore - terminalInventoryAfter ==
                credit.RequiredQuantity &&
            terminalPendingAfter - terminalPendingBefore ==
                credit.RequiredQuantity &&
            queueBeforePending == terminalPendingBefore &&
            queueAfterPending == terminalPendingAfter &&
            queueBeforeInventory.HasValue &&
            terminalInventoryBefore >= queueBeforeInventory.Value &&
            queueAfterInventory == terminalInventoryAfter;
    }

    private static bool TryReadChangedFactCounts(
        System.Text.Json.JsonElement changedFacts,
        string path,
        out int before,
        out int after)
    {
        before = 0;
        after = 0;
        if (changedFacts.ValueKind !=
            System.Text.Json.JsonValueKind.Array)
            return false;
        var matches = changedFacts.EnumerateArray()
            .Where(value => string.Equals(
                ReadString(value, "path"),
                path,
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 &&
            TryReadChangedFactInt(matches[0], "before", out before) &&
            TryReadChangedFactInt(matches[0], "after", out after);
    }

    private static bool TryReadChangedFactInt(
        System.Text.Json.JsonElement fact,
        string property,
        out int result)
    {
        result = 0;
        if (!fact.TryGetProperty(property, out var value))
            return false;
        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number =>
                value.TryGetInt32(out result),
            System.Text.Json.JsonValueKind.String =>
                int.TryParse(value.GetString(), out result),
            _ => false
        };
    }

    private static PolicyTeacherRequirementTransition VerifyCollectionBoolean(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string field,
        string rows,
        string identityProperty,
        string completionProperty,
        string transitionKind)
    {
        var beforeValue = ReadCollectionBoolean(
            before,
            field,
            rows,
            identityProperty,
            credit.QualifiedItemId,
            completionProperty);
        var afterValue = ReadCollectionBoolean(
            after,
            field,
            rows,
            identityProperty,
            credit.QualifiedItemId,
            completionProperty);
        return Transition(
            credit,
            transitionKind,
            beforeValue?.ToString() ?? "unavailable",
            afterValue?.ToString() ?? "unavailable",
            beforeValue == false && afterValue == true);
    }

    private static PolicyTeacherRequirementTransition VerifyCommunityCenterPayment(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var evidence = ExactCommunityCenterPaymentReceiptVerifier.Verify(
            before,
            after,
            credit.RequirementId,
            credit.AlternativeIndex,
            credit.RequiredQuantity);
        return Transition(
            credit,
            evidence.TransitionKind,
            $"money={CountText(evidence.BeforeMoney)};completed={BooleanText(evidence.BeforeNativeCompletion)}",
            $"money={CountText(evidence.AfterMoney)};completed={BooleanText(evidence.AfterNativeCompletion)}",
            evidence.Verified);
    }

    private static PolicyTeacherRequirementTransition VerifyCommunityCenterDonation(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var evidence = ExactCommunityCenterDonationReceiptVerifier.Verify(
            credit,
            queueItem,
            before,
            after);
        return Transition(
            credit,
            "native_community_center_donation_full_projection_transition",
            evidence.BeforeSummary,
            evidence.AfterSummary,
            evidence.Verified);
    }

    private static ActionQueueItem? CommunityCenterDonationQueueItem(
        CurrentCollectionRequirementCredit credit,
        IEnumerable<ActionQueueItem> queueItems)
    {
        const string prefix = "community_center:bundle:";
        var bundleKey = credit.RequirementId.StartsWith(
                prefix,
                StringComparison.Ordinal)
            ? credit.RequirementId[prefix.Length..]
            : string.Empty;
        var matches = queueItems.Where(item =>
                string.Equals(
                    item.OptionId,
                    "executor.donate_community_center_item",
                    StringComparison.Ordinal) &&
                CommunityCenterDonationParameterProtocol.TryParseExecution(
                    item.NormalizedCommand?.Parameters,
                    out var projection) &&
                projection.Binding.BundleDataKey == bundleKey &&
                projection.Binding.BundleIngredientIndex ==
                    credit.AlternativeIndex)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static PolicyTeacherRequirementTransition VerifyRouteStep(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        if (queueItem is null)
        {
            return Transition(
                credit,
                "route_endpoint_evidence_missing",
                "unavailable",
                "unavailable",
                false);
        }
        var expectedLocation = ReadParameter(
            queueItem,
            "expected_target_location");
        var expectedX = ReadIntParameter(
            queueItem,
            "expected_arrival_tile_x");
        var expectedY = ReadIntParameter(
            queueItem,
            "expected_arrival_tile_y");
        var beforeLocation = RequiredStateString(
            before,
            "player",
            "location_id");
        var afterLocation = RequiredStateString(
            after,
            "player",
            "location_id");
        var beforeX = RequiredStateInt(before, "player", "tile_x");
        var beforeY = RequiredStateInt(before, "player", "tile_y");
        var afterX = RequiredStateInt(after, "player", "tile_x");
        var afterY = RequiredStateInt(after, "player", "tile_y");
        var changed = !string.Equals(beforeLocation, afterLocation,
                StringComparison.Ordinal) || beforeX != afterX || beforeY != afterY;
        var arrived = !string.IsNullOrWhiteSpace(expectedLocation) &&
            string.Equals(afterLocation, expectedLocation, StringComparison.Ordinal) &&
            (!expectedX.HasValue || afterX == expectedX.Value) &&
            (!expectedY.HasValue || afterY == expectedY.Value);
        return Transition(
            credit,
            "exact_route_endpoint_reached",
            $"{beforeLocation}:{beforeX},{beforeY}",
            $"{afterLocation}:{afterX},{afterY}",
            changed && arrived);
    }

    private static PolicyTeacherRequirementTransition Transition(
        CurrentCollectionRequirementCredit credit,
        string transitionKind,
        string before,
        string after,
        bool verified)
        => new()
        {
            RequirementSetId = credit.RequirementSetId,
            RequirementId = credit.RequirementId,
            AlternativeIndex = credit.AlternativeIndex,
            QualifiedItemId = credit.QualifiedItemId,
            BindingKind = credit.BindingKind,
            TransitionKind = transitionKind,
            BeforeValue = before,
            AfterValue = after,
            Verified = verified
        };

    private static string CountText(int? value) =>
        value?.ToString() ?? "unavailable";

    private static string BooleanText(bool? value) =>
        value?.ToString() ?? "unavailable";
}
