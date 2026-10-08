using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    private static void ValidateRoute(
        string supportTransitionKind,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowered,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionRouteTargetDateProcessing processing,
        AcquisitionMachineCapacitySupportBinding? capacityBinding,
        ICollection<string> reasons)
    {
        if (requirement.BlockingReasons.Length != 0 ||
            !requirement.StaticWindowMatchesTargetDate ||
            requirement.UnlockStateMatchesTargetDate != true ||
            !lowered.RuntimeAdmissionReady ||
            !lowered.TeacherAdmissionReady)
        {
            reasons.Add("support_route_not_authoritatively_admitted");
        }
        if (supportTransitionKind != "machine_capacity_establishment" &&
            (!reservation.ReservationAxisResolved ||
            reservation.InventoryReservationMatchesTargetDate != true ||
            reservation.ClaimSet is null ||
            !reservation.ClaimSet.AtomicCommitRequired ||
            reservation.ClaimDisposition is not (
                "claim_proposed" or
                "claim_already_committed" or
                "claim_replacement_required")))
        {
            reasons.Add("support_material_reservation_not_ready");
        }

        if (supportTransitionKind == "crop_planting")
        {
            ValidateCropRoute(requirement, processing, reasons);
            return;
        }
        if (supportTransitionKind is
            "machine_input_load" or
            "machine_input_material_transfer")
        {
            ValidateMachineRoute(requirement, processing, reasons);
            return;
        }
        if (supportTransitionKind == "machine_input_purchase")
        {
            ValidateMachinePurchaseRoute(requirement, processing, reasons);
            return;
        }
        if (supportTransitionKind == "machine_capacity_establishment")
        {
            ValidateMachineCapacityRoute(
                requirement,
                reservation,
                capacityBinding,
                reasons);
            return;
        }
        reasons.Add("support_transition_kind_not_bound");
    }

    private static void ValidateMachineCapacityRoute(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionMachineCapacitySupportBinding? binding,
        ICollection<string> reasons)
    {
        var location = reservation.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;
        if (requirement.RouteKind is not (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output") ||
            binding is null ||
            string.IsNullOrWhiteSpace(binding.MachineQualifiedItemId) ||
            string.IsNullOrWhiteSpace(binding.IntentId) ||
            string.IsNullOrWhiteSpace(binding.SupportSourcesJson) ||
            !location.LocationRouteAxisResolved ||
            location.LocationRouteMatchesTargetDate != false ||
            location.LocationRouteAxisStatus != "resolved_location_route_miss" ||
            location.TargetEvaluations.Length != 0 ||
            location.BlockingReasons.Length != 0 ||
            !location.NonMatchingReasons.SequenceEqual(
                new[] { "matching_machine_runtime_location_not_present" },
                StringComparer.Ordinal))
        {
            reasons.Add(
                "support_machine_capacity_missing_fleet_not_authoritatively_proven");
        }
    }

    private static void ValidateMachinePurchaseRoute(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateProcessing processing,
        ICollection<string> reasons)
    {
        if (requirement.RouteKind is not (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output"))
        {
            reasons.Add(
                "support_machine_purchase_route_not_authoritatively_admitted");
        }
        var purchase = PurchasePrerequisite(processing);
        if (!processing.ProcessingLeadTimeAxisResolved ||
            processing.ProcessingLeadTimeMatchesTargetDate != false ||
            processing.ProcessingLeadTimeRequirementKind !=
                "upstream_machine_input_purchase" ||
            processing.BlockingReasons.Length != 0 ||
            purchase is null ||
            purchase.RequiredPurchaseCount <= 0 ||
            purchase.UnitPrice <= 0 ||
            purchase.OutputStackPerPurchase <= 0 ||
            string.IsNullOrWhiteSpace(purchase.StockId))
        {
            reasons.Add("support_machine_purchase_prerequisite_not_proven");
        }
    }

    private static void ValidateCropRoute(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateProcessing processing,
        ICollection<string> reasons)
    {
        if (requirement.RouteKind != "harvests_as" ||
            !requirement.SourceId.StartsWith("crop:", StringComparison.Ordinal) ||
            requirement.BlockingReasons.Length != 0 ||
            !requirement.StaticWindowMatchesTargetDate ||
            requirement.UnlockStateMatchesTargetDate != true ||
            requirement.MatchingWindows.Length == 0)
        {
            reasons.Add("support_crop_route_not_authoritatively_admitted");
        }
        if (!processing.ProcessingLeadTimeAxisResolved ||
            processing.ProcessingLeadTimeMatchesTargetDate != false ||
            processing.ProcessingLeadTimeRequirementKind !=
                "crop_growth_or_ready_crop" ||
            processing.BlockingReasons.Length != 0 ||
            !processing.Evaluations.Any(value =>
                value.ProductionStateKind == "new_crop_from_seed" &&
                value.Status ==
                    "resolved_new_crop_requires_future_daily_growth" &&
                value.OutputReadyOnTargetDate == false))
        {
            reasons.Add("support_crop_processing_miss_not_proven");
        }
    }

    private static void ValidateMachineRoute(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateProcessing processing,
        ICollection<string> reasons)
    {
        if (requirement.RouteKind is not (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output"))
        {
            reasons.Add("support_machine_route_not_authoritatively_admitted");
        }
        if (!processing.ProcessingLeadTimeAxisResolved ||
            processing.ProcessingLeadTimeMatchesTargetDate != false ||
            processing.ProcessingLeadTimeRequirementKind !=
                "native_machine_processing_schedule" ||
            processing.BlockingReasons.Length != 0 ||
            !processing.Evaluations.Any(value =>
                value.ProductionStateKind == "manual_input_processing" &&
                value.MachineScheduleBinding is not null &&
                value.MachineScheduleBinding.RequiredAttemptCount > 0))
        {
            reasons.Add("support_machine_processing_miss_not_proven");
        }
    }

    private static bool CandidateCoveredByClaim(
        PolicyEventCandidatePrediction candidate,
        AcquisitionRouteReservationClaimSet? claimSet,
        string supportTransitionKind,
        string playerInventoryNodeId)
    {
        if (supportTransitionKind == "machine_capacity_establishment")
        {
            return candidate.Available &&
                candidate.OptionId ==
                    "farm.establish_supported_machine_capacity" &&
                candidate.Kind is (
                    "craft_machine_item" or "place_machine_item") &&
                !string.IsNullOrWhiteSpace(candidate.QualifiedItemId) &&
                CurrentTeacherFrontierSupport.TryReadUniqueParameter(
                    candidate,
                    "machine_support_intent_id",
                    out _);
        }
        if (supportTransitionKind == "machine_input_material_transfer")
        {
            var sourceNodeId = ReadStringParameter(
                candidate,
                "source_node_id");
            var quantity = ReadPositiveIntParameter(candidate, "quantity");
            return candidate.SlotIndex.HasValue &&
                quantity.HasValue &&
                claimSet is not null &&
                claimSet.CurrencyClaims.Length == 0 &&
                claimSet.MaterialClaims.Count(claim =>
                    claim.NodeId == sourceNodeId &&
                    claim.SlotIndex == candidate.SlotIndex.Value &&
                    claim.QualifiedItemId == candidate.QualifiedItemId &&
                    claim.Quantity == quantity.Value) == 1;
        }
        if (supportTransitionKind == "machine_input_purchase")
        {
            return candidate.OptionId == "economy.buy_supplies" &&
                candidate.Available &&
                candidate.UnitPrice > 0 &&
                claimSet is not null &&
                claimSet.CurrencyClaims.Length == 1 &&
                claimSet.CurrencyClaims[0].Amount >= candidate.UnitPrice;
        }
        var requiredQuantity = supportTransitionKind == "machine_input_load"
            ? ReadPositiveIntParameter(candidate, "machine_input_required_count")
            : 1;
        return candidate.SlotIndex.HasValue &&
            requiredQuantity.HasValue &&
            candidate.Quantity >= requiredQuantity.Value &&
            (supportTransitionKind != "machine_input_load" ||
             !string.IsNullOrWhiteSpace(playerInventoryNodeId)) &&
            claimSet is not null &&
            claimSet.MaterialClaims.Any(claim =>
                (supportTransitionKind != "machine_input_load" ||
                 claim.NodeId == playerInventoryNodeId) &&
                claim.SlotIndex == candidate.SlotIndex.Value &&
                claim.QualifiedItemId == candidate.QualifiedItemId &&
                claim.Quantity >= requiredQuantity.Value) &&
            claimSet.CurrencyClaims.Length == 0;
    }

    private static string CurrentPlayerInventoryNodeId(
        SnapshotEnvelope snapshot) =>
        snapshot.PlayerId.Status is "available" or "derived" &&
        !string.IsNullOrWhiteSpace(snapshot.PlayerId.Value)
            ? "player:" + snapshot.PlayerId.Value
            : string.Empty;

    private static void ValidateClaimIdentity(
        AcquisitionRouteReservationClaimSet? claimSet,
        string stateHash,
        int ledgerRevision,
        ICollection<string> reasons)
    {
        if (claimSet is null)
            return;
        var claimCount = claimSet.MaterialClaims.Length +
            claimSet.CurrencyClaims.Length;
        if (claimSet.SourceStateHash != stateHash ||
            claimSet.ExpectedLedgerRevision != ledgerRevision ||
            claimCount == 0 ||
            claimSet.MaterialClaims.Any(value =>
                value.StateHash != stateHash ||
                value.ExpectedLedgerRevision != ledgerRevision) ||
            claimSet.CurrencyClaims.Any(value =>
                value.StateHash != stateHash ||
                value.ExpectedLedgerRevision != ledgerRevision))
        {
            reasons.Add("support_reservation_claim_identity_mismatch");
        }
    }
}
