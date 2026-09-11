using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherReceiptBuilder
{
    private static PolicyTeacherRequirementTransition[] VerifyTransitions(
        CurrentStageOneCollectionTeacherPreferenceLabel preference,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var selected = preference.SelectedCandidate!;
        var queueItems = preference.CompiledQueue!.Items;
        return selected.RequirementCredits
            .Select(credit => VerifyTransition(credit, queueItems, before, after))
            .ToArray();
    }

    private static PolicyTeacherRequirementTransition VerifyTransition(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem[] queueItems,
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
            "native_community_center_payment_completion" or
            "native_community_center_donation_completion" =>
                VerifyCommunityCenterAlternative(credit, before, after),
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
        var beforeCount = InventoryCount(
            before,
            credit.QualifiedItemId,
            credit.MinimumQuality);
        var afterCount = InventoryCount(
            after,
            credit.QualifiedItemId,
            credit.MinimumQuality);
        return Transition(
            credit,
            "exact_inventory_quantity_increased",
            CountText(beforeCount),
            CountText(afterCount),
            beforeCount.HasValue &&
            afterCount.HasValue &&
            afterCount.Value > beforeCount.Value);
    }

    private static PolicyTeacherRequirementTransition VerifyFullShipment(
        CurrentCollectionRequirementCredit credit,
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
        return Transition(
            credit,
            settled
                ? "native_full_shipment_false_to_true"
                : "exact_pending_native_shipment_increased",
            $"shipped={BooleanText(beforeShipped)};pending={CountText(beforePending)};inventory={CountText(beforeInventory)}",
            $"shipped={BooleanText(afterShipped)};pending={CountText(afterPending)};inventory={CountText(afterInventory)}",
            settled || pending);
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

    private static PolicyTeacherRequirementTransition VerifyCommunityCenterAlternative(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        const string prefix = "community_center:bundle:";
        var key = credit.RequirementId.StartsWith(prefix, StringComparison.Ordinal)
            ? credit.RequirementId[prefix.Length..]
            : string.Empty;
        var beforeValue = ReadBundleAlternative(
            before,
            key,
            credit.AlternativeIndex,
            credit.QualifiedItemId);
        var afterValue = ReadBundleAlternative(
            after,
            key,
            credit.AlternativeIndex,
            credit.QualifiedItemId);
        return Transition(
            credit,
            "native_bundle_alternative_false_to_true",
            beforeValue?.ToString() ?? "unavailable",
            afterValue?.ToString() ?? "unavailable",
            beforeValue == false && afterValue == true);
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
