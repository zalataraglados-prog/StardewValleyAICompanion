using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    private static void ValidateRoute(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowered,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionRouteTargetDateProcessing processing,
        ICollection<string> reasons)
    {
        if (requirement.RouteKind != "harvests_as" ||
            !requirement.SourceId.StartsWith("crop:", StringComparison.Ordinal) ||
            requirement.BlockingReasons.Length != 0 ||
            !requirement.StaticWindowMatchesTargetDate ||
            requirement.UnlockStateMatchesTargetDate != true ||
            requirement.MatchingWindows.Length == 0 ||
            !lowered.RuntimeAdmissionReady ||
            !lowered.TeacherAdmissionReady)
        {
            reasons.Add("support_crop_route_not_authoritatively_admitted");
        }
        if (!reservation.ReservationAxisResolved ||
            reservation.InventoryReservationMatchesTargetDate != true ||
            reservation.ClaimSet is null ||
            !reservation.ClaimSet.AtomicCommitRequired ||
            reservation.ClaimDisposition is not (
                "claim_proposed" or
                "claim_already_committed" or
                "claim_replacement_required"))
        {
            reasons.Add("support_crop_seed_reservation_not_ready");
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

    private static bool CandidateCoveredByClaim(
        PolicyEventCandidatePrediction candidate,
        AcquisitionRouteReservationClaimSet? claimSet) =>
        candidate.SlotIndex.HasValue &&
        claimSet is not null &&
        claimSet.MaterialClaims.Any(claim =>
            claim.SlotIndex == candidate.SlotIndex.Value &&
            claim.QualifiedItemId == candidate.QualifiedItemId &&
            claim.Quantity >= 1) &&
        claimSet.CurrencyClaims.Length == 0;

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
